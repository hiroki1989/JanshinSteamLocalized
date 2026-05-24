// EnemySkills_Addon.cs
// 敵スキルの管理（enemy_config.xlsx から読み込み、ターンごとに自動発動）

using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

[Serializable]
public partial class GameManager : MonoBehaviour
{
    // ========================
    //  内部状態
    // ========================

    // 現在の敵に紐づくスキル一覧（EnemyConfigExcel から設定）
    private readonly List<EnemySkillConfig> _enemySkills = new List<EnemySkillConfig>();

    // 各スキルごとの「前回発動からの経過ターン数」
    // Z 列の指定ターン数に対して、
    //   Z-3ターン目：10%
    //   Z-2ターン目：40%
    //   Z-1ターン目：70%
    //   Z ターン目以降：100%
    // で発動判定を行い、発動したらそのスキルのカウントを 0 にリセットする。
    private readonly List<int> _enemySkillTurnCounters = new List<int>();

    // どのランタイム index の敵用か（デバッグ用）
    private int _enemySkillsOwnerRuntimeIndex = -1;

// 「怒り」：次の敵の和了ダメージ倍率
private float _enemySkillAngerMultiplier = 1f;

// ★追加：直近のスコア計算で「実際に適用された」怒り倍率（UI表示用）
private float _enemySkillLastAppliedAngerMultiplier = 1f;

// 「防御」：次のプレイヤー和了ダメージの軽減率（0〜1）
// 例：0.25 なら 25% 軽減
private float _enemySkillPlayerDamageDownRate = 0f;

// ★追加：直近のスコア計算で「実際に適用された」防御軽減率（UI表示用）
private float _enemySkillLastAppliedDefenseRate = 0f;

// ★仕様変更：怒り/防御は「次の1回」ではなく、Yターンの間だけ有効
private int _enemySkillAngerTurnRemaining   = 0;
private int _enemySkillDefenseTurnRemaining = 0;

    // 「毒」：残りターン数と毎ターンダメージ
    private int _enemySkillPoisonTurnRemaining = 0;
    private int _enemySkillPoisonDamagePerTurn = 0;

    // 「麻痺」：残りターン数
    private int _enemySkillParalysisTurnRemaining = 0;

    // カットイン中フラグ（GameManager 側のボタンロック判定で参照）
    private bool _enemySkillCutinRunning = false;
    // ========================
    //  演出（Inspector設定）
    // ========================
    [Header("Enemy Skill FX (Trick/Disturb)")]
    [SerializeField] private Color enemySkillTrickFxColor = new Color(0.55f, 0.25f, 0.95f, 1f);
    [SerializeField, Range(0f, 1f)] private float enemySkillTrickFxMaxAlpha = 0.55f;
    [SerializeField] private float enemySkillTrickFxSeconds = 0.5f;

    [SerializeField] private float enemySkillDisturbMpAnimSeconds = 1.0f;

    private Sprite _enemySkillTrickGradientSpriteCache = null;

    // ★追加：このターンは敵スキルを「発動させない」抑止ターン（敵リーチ宣言/敵和了）
    private int _enemyRiichiDeclaredTurnCounter = -1;
    private int _enemyWinDeclaredTurnCounter    = -1;
[System.Serializable]
private class EnemySkillDisplayNameEntry
{
    public string skillId;       // ExcelのSkill?_Idに入っている文字（例: "妨害" / "disturb" など）
    public string displayName;   // ゲーム内表記（例: "封殺" など）
}
[Header("Enemy Skill Display Names (UI only)")]
[SerializeField] private List<EnemySkillDisplayNameEntry> enemySkillDisplayNameTable
    = new List<EnemySkillDisplayNameEntry>();

private const string EnemySkillIdAnger = "anger";
private const string EnemySkillIdPoison = "poison";
private const string EnemySkillIdParalysis = "paralysis";
private const string EnemySkillIdAttack = "attack";
private const string EnemySkillIdDefense = "defense";
private const string EnemySkillIdDisturb = "disturb";
private const string EnemySkillIdTrick = "trick";

private static string EnemySkills_NormalizeSkillId(string rawSkillId)
{
    if (string.IsNullOrEmpty(rawSkillId)) return string.Empty;

    string key = rawSkillId.Trim().ToLowerInvariant();

    if (key == "怒り" || key == "いかり" || key == "ikari" || key == "anger")
        return EnemySkillIdAnger;

    if (key == "毒" || key == "どく" || key == "doku" || key == "poison")
        return EnemySkillIdPoison;

    if (key == "麻痺" || key == "まひ" || key == "mahi" || key == "paralysis")
        return EnemySkillIdParalysis;

    if (key == "攻撃" || key == "こうげき" || key == "attack")
        return EnemySkillIdAttack;

    if (key == "防御" || key == "ぼうぎょ" || key == "defence" || key == "defense")
        return EnemySkillIdDefense;

    if (key == "妨害" || key == "ぼうがい" || key == "disturb" || key == "jam")
        return EnemySkillIdDisturb;

    if (key == "細工" || key == "さいく" || key == "trick" || key == "saiku")
        return EnemySkillIdTrick;

    return key;
}
private static string EnemySkills_GetDefaultDisplayNameByCanonicalId(string canonicalSkillId)
{
    return LocalizationManager.EnemySkill(canonicalSkillId);
}
private static string EnemySkills_GetFixedText_Local(string key)
{
    return LocalizationManager.Fixed(key);
}
public string EnemySkills_GetDisplayName(string rawSkillId)
{
    if (string.IsNullOrEmpty(rawSkillId)) return string.Empty;

    string raw = rawSkillId.Trim();

    if (EnemyDialogueController.TryResolveSharedEnemySkillDisplayName(raw, out var sharedDisplayName))
    {
        return sharedDisplayName;
    }

    string canonical = NormalizeEnemySkillKey_Local(raw);

    if (!string.IsNullOrEmpty(canonical))
    {
        string localized = LocalizationManager.EnemySkill(canonical);
        string lookupKey = "enemy_skill." + canonical;

        if (!string.IsNullOrEmpty(localized) &&
            !string.Equals(localized, lookupKey, StringComparison.OrdinalIgnoreCase))
        {
            return localized;
        }
    }

    string rawLower = raw.ToLowerInvariant();

    if (enemySkillDisplayNameTable != null)
    {
        for (int i = 0; i < enemySkillDisplayNameTable.Count; i++)
        {
            var e = enemySkillDisplayNameTable[i];
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.skillId)) continue;
            if (string.IsNullOrEmpty(e.displayName)) continue;

            string key = e.skillId.Trim();
            if (key == raw) return e.displayName;

            string keyLower = key.ToLowerInvariant();
            if (keyLower == rawLower) return e.displayName;
        }
    }

    return raw;
}
    private void EnemySkills_SetFromConfig(EnemyConfig cfg, int runtimeIndex)
    {
        _enemySkillsOwnerRuntimeIndex = runtimeIndex;
        _enemySkills.Clear();
        _enemySkillTurnCounters.Clear(); // ★追加：スキルごとのターンカウンタもリセット

        // ランタイム状態もリセット（怒り/防御/毒/麻痺）
        EnemySkills_ResetRuntimeStates();

        if (cfg == null || cfg.skills == null || cfg.skills.Count == 0)
        {
            EnemySkills_UpdateCountdownUI();
            return;
        }

        foreach (var s in cfg.skills)
        {
            if (s == null) continue;
            if (string.IsNullOrEmpty(s.id)) continue;
            _enemySkills.Add(new EnemySkillConfig
            {
                id     = s.id,
                paramX = s.paramX,
                paramY = s.paramY,
                paramZ = s.paramZ,
            });

            // ★追加：読み込んだスキル 1 つにつきカウンタ 0 を追加
            _enemySkillTurnCounters.Add(0);
        }

        EnemySkills_UpdateCountdownUI();
    }
private void EnemySkills_ResetRuntimeStates()
{
    _enemySkillAngerMultiplier        = 1f;
    _enemySkillPlayerDamageDownRate   = 0f;
    _enemySkillPoisonTurnRemaining    = 0;
    _enemySkillPoisonDamagePerTurn    = 0;
    _enemySkillParalysisTurnRemaining = 0;

    _enemySkillAngerTurnRemaining   = 0;
    _enemySkillDefenseTurnRemaining = 0;

    // ★追加：直近適用実績も初期化（UI表示用）
    _enemySkillLastAppliedAngerMultiplier = 1f;
    _enemySkillLastAppliedDefenseRate     = 0f;

    // ★追加：スキルターンカウンタも 0 にリセット
    for (int i = 0; i < _enemySkillTurnCounters.Count; i++)
    {
        _enemySkillTurnCounters[i] = 0;
    }
}

private void EnemySkills_OnPlayerTurnStart()
{
    // ★ユニーク：オーディン（敵スキル無効化）
    try
    {
        if (PlayerData.IsEquippedUniqueEffect(PlayerData.UniqueOmamoriEffectKind.Odin_DisableEnemySkills))
        {
            // 表示も消す
            try { EnemySkills_ResetRuntimeStates(); } catch { }
            try { EnemySkills_UpdateCountdownUI(); } catch { }
            return;
        }
    }
    catch { }
    if (_enemySkillPoisonTurnRemaining > 0 && _enemySkillPoisonDamagePerTurn > 0)
    {
        // ★仕様変更：和了ダメージと同じようにHPゲージがグーっと減る演出で反映
        StartCoroutine(__EnemySkill_PoisonTickDamage_Co(_enemySkillPoisonDamagePerTurn));

        _enemySkillPoisonTurnRemaining--;
        if (statusTMP)
        {
            statusTMP.text =
                EnemySkills_GetFixedText_Local("poison_tick_prefix")
                + _enemySkillPoisonDamagePerTurn
                + EnemySkills_GetFixedText_Local("poison_tick_middle")
                + _enemySkillPoisonTurnRemaining
                + EnemySkills_GetFixedText_Local("poison_tick_suffix");
        }
    }

    // ★麻痺：ターン経過で残りターンを減らす
    if (_enemySkillParalysisTurnRemaining > 0)
    {
        _enemySkillParalysisTurnRemaining--;
        if (_enemySkillParalysisTurnRemaining <= 0 && statusTMP)
        {
            statusTMP.text = EnemySkills_GetFixedText_Local("paralysis_recovered");
        }
    }
    EnemySkills_UpdateCountdownUI();
    EnemySkills_RefreshStatusEffectsUI();
}
private System.Collections.IEnumerator __EnemySkill_PoisonTickDamage_Co(int baseDamage)
{
    // ★この時点で凍結しておく：プレイヤー操作/進行を防ぐ
    _freezeProgression = true;

    // 直前に別の演出（敵和了ダメージ/敵スキル攻撃など）が走っていたら完了待ち
    while (_enemyWinDamageAnimating)
        yield return null;

    // ダメージ演出（1秒で徐々に減る）
    if (baseDamage > 0)
        yield return __EnemySkill_ApplyDamageToPlayerAnimated_ThenMaybeDefeat_Co(baseDamage);

    if (Mathf.Max(0, playerHP) > 0)
    {
        _freezeProgression = false;
    }
}
void EnemySkills_OnEnemyTurn(int enemyTurnCounter)
    {
        // ★怒り/防御：残りターンを進める（ここは「敵ターン開始」＝「プレイヤーターンが終わった」タイミング）
        if (_enemySkillAngerTurnRemaining > 0)
        {
            _enemySkillAngerTurnRemaining--;
            if (_enemySkillAngerTurnRemaining <= 0)
            {
                _enemySkillAngerTurnRemaining = 0;
                _enemySkillAngerMultiplier = 1f;
            }
        }
        if (_enemySkillDefenseTurnRemaining > 0)
        {
            _enemySkillDefenseTurnRemaining--;
            if (_enemySkillDefenseTurnRemaining <= 0)
            {
                _enemySkillDefenseTurnRemaining = 0;
                _enemySkillPlayerDamageDownRate = 0f;
            }
        }
        EnemySkills_RefreshStatusEffectsUI();

        if (_enemySkills.Count == 0)
        {
            EnemySkills_UpdateCountdownUI();
            return;
        }
        // ★追加：敵が「このターンに立直した」or「このターンに和了した」場合は
        //         スキル発動だけ抑止する（カウントは進める）
        bool suppressFireThisTurn =
            (enemyTurnCounter == _enemyRiichiDeclaredTurnCounter) ||
            (enemyTurnCounter == (_enemyRiichiDeclaredTurnCounter + 1)) ||
            (enemyTurnCounter == _enemyWinDeclaredTurnCounter);

        // スキル数に合わせてカウンタリストのサイズを調整
        while (_enemySkillTurnCounters.Count < _enemySkills.Count)
        {
            _enemySkillTurnCounters.Add(0);
        }
        while (_enemySkillTurnCounters.Count > _enemySkills.Count)
        {
            _enemySkillTurnCounters.RemoveAt(_enemySkillTurnCounters.Count - 1);
        }

        // 各スキルごとに発動判定
        for (int i = 0; i < _enemySkills.Count; i++)
        {
            var cfg = _enemySkills[i];
            if (cfg == null) continue;

            // ★追加：プレイヤーが立直した後は「細工」だけ発動しない
            bool suppressThisSkill =
                suppressFireThisTurn ||
                (isRiichi && EnemySkills_IsTrickSkill(cfg));

            // Z <= 0 の場合は「毎ターン 100%」とみなす
            int threshold = (cfg.paramZ > 0) ? cfg.paramZ : 1;

            // このスキルの「前回発動からの経過ターン」を 1 増やす
            int turns = _enemySkillTurnCounters[i] + 1;

            float prob = 0f;

            if (threshold <= 1)
            {
                // Z <= 1 は毎ターン 100%
                prob = 1f;
            }
            else if (turns >= threshold)
            {
                // ちょうど Z ターン目は 100%
                prob = 1f;
            }
            else if (turns == threshold - 1)
            {
                // 1ターン前は 70%
                prob = 0.70f;
            }
            else if (turns == threshold - 2)
            {
                // 2ターン前は 40%
                prob = 0.40f;
            }
            else if (turns == threshold - 3)
            {
                // 3ターン前は 10%
                prob = 0.10f;
            }
            else
            {
                // それより前は 0%
                prob = 0f;
            }

            bool fire = false;

            if (prob >= 1f)
            {
                fire = true;
            }
            else if (prob > 0f)
            {
                // rng は GameManager で既に使用している System.Random
                double r = rng.NextDouble();
                if (r < prob) fire = true;
            }

            // ★修正：抑止条件が成立している場合は「発動だけ」しない（カウントは未発動扱いで進める）
            if (fire && !suppressThisSkill)
            {
                EnemySkills_Activate(cfg);
                // ★スキル発動後：ターンカウントを初期化
                _enemySkillTurnCounters[i] = 0;
            }
            else
            {
                // 未発動の場合はカウントを進める
                _enemySkillTurnCounters[i] = turns;
            }
        }

        EnemySkills_UpdateCountdownUI();
    }
private static bool EnemySkills_IsTrickSkill(EnemySkillConfig cfg)
{
    if (cfg == null || string.IsNullOrEmpty(cfg.id)) return false;
    return string.Equals(
        EnemySkills_NormalizeSkillId(cfg.id),
        EnemySkillIdTrick,
        StringComparison.Ordinal);
}
    private bool EnemySkills_IsPlayerParalyzed()
    {
        return _enemySkillParalysisTurnRemaining > 0;
    }
private void EnemySkills_Activate(EnemySkillConfig cfg)
{
    if (cfg == null || string.IsNullOrEmpty(cfg.id)) return;

    string key = EnemySkills_NormalizeSkillId(cfg.id);
    string enemyName = GetCurrentEnemyBaseNameForResources();
    string skillDisplayName = EnemySkills_GetDisplayName(cfg.id);
    EnemySkills_PlayCutin(enemyName, skillDisplayName);

    if (key == EnemySkillIdAnger)
    {
        // ★仕様：Excel の X は「加算％」。例）1.2倍にしたい → 20
        int addPct = Mathf.Max(0, cfg.paramX);
        float mul = 1f + (addPct / 100f);

        // ★仕様変更：Y ターンの間だけ有効（その間に発生した「敵の和了」に毎回適用）
        int turn = Mathf.Max(1, cfg.paramY);

        _enemySkillAngerMultiplier = Mathf.Max(_enemySkillAngerMultiplier, mul);
        _enemySkillAngerTurnRemaining = Mathf.Max(_enemySkillAngerTurnRemaining, turn);

        if (statusTMP)
        {
            float pct = Mathf.Max(0f, (_enemySkillAngerMultiplier - 1f) * 100f);
            statusTMP.text =
                EnemySkills_GetFixedText_Local("anger_status_prefix")
                + turn
                + EnemySkills_GetFixedText_Local("anger_status_middle")
                + pct.ToString("0.###")
                + EnemySkills_GetFixedText_Local("anger_status_suffix");
        }
    }
else if (key == EnemySkillIdPoison)
    {
        int turn = Mathf.Max(1, cfg.paramX);
        int dmg = Mathf.Max(1, cfg.paramY);

        float tierMult = 1f;
        try { tierMult = GameManager.GetCurrentTierMultiplier(); } catch { tierMult = 1f; }

        int scaledDmg = Mathf.Max(1, Mathf.RoundToInt(dmg * tierMult));

        // 既に毒状態なら、より長いターン/大きいダメージを優先して上書き
        _enemySkillPoisonTurnRemaining = Math.Max(_enemySkillPoisonTurnRemaining, turn);
        _enemySkillPoisonDamagePerTurn = Math.Max(_enemySkillPoisonDamagePerTurn, scaledDmg);
        if (statusTMP)
        {
            statusTMP.text =
                EnemySkills_GetFixedText_Local("poison_status_prefix")
                + turn
                + EnemySkills_GetFixedText_Local("poison_status_middle")
                + scaledDmg
                + EnemySkills_GetFixedText_Local("poison_status_suffix");
        }
    }
    else if (key == EnemySkillIdParalysis)
    {
        int turn = Mathf.Max(1, cfg.paramX);
        _enemySkillParalysisTurnRemaining = Math.Max(_enemySkillParalysisTurnRemaining, turn);
        if (statusTMP)
        {
            statusTMP.text =
                EnemySkills_GetFixedText_Local("paralysis_status_prefix")
                + turn
                + EnemySkills_GetFixedText_Local("paralysis_status_suffix");
        }
    }
    else if (key == EnemySkillIdAttack)
    {
        int dmg = Mathf.Max(1, cfg.paramX);

        float tierMult = 1f;
        try { tierMult = GameManager.GetCurrentTierMultiplier(); } catch { tierMult = 1f; }

        int scaledDmg = Mathf.Max(1, Mathf.RoundToInt(dmg * tierMult));

        // ★仕様変更：スキルのカットインが終わったタイミングで、
        //           和了ダメージと同じ演出（1秒で徐々に減る）でHPへ反映する
        StartCoroutine(__EnemySkill_AttackDamageAfterCutin_Co(scaledDmg));
        if (statusTMP)
        {
            statusTMP.text =
                EnemySkills_GetFixedText_Local("skill_quoted_prefix")
                + skillDisplayName
                + EnemySkills_GetFixedText_Local("skill_quoted_middle")
                + scaledDmg
                + EnemySkills_GetFixedText_Local("attack_status_suffix");
        }
    }
    else if (key == EnemySkillIdDefense)
    {
        // X % 軽減
        float rate = cfg.paramX / 100f;
        rate = Mathf.Clamp01(rate);

        // ★仕様変更：Y ターンの間だけ有効（その間に発生した「プレイヤーの和了」に毎回適用）
        int turn = Mathf.Max(1, cfg.paramY);

        _enemySkillPlayerDamageDownRate = Mathf.Max(_enemySkillPlayerDamageDownRate, rate);
        _enemySkillDefenseTurnRemaining = Mathf.Max(_enemySkillDefenseTurnRemaining, turn);

        if (statusTMP)
        {
            statusTMP.text =
                EnemySkills_GetFixedText_Local("defense_status_prefix")
                + skillDisplayName
                + EnemySkills_GetFixedText_Local("defense_status_middle")
                + turn
                + EnemySkills_GetFixedText_Local("defense_status_turn_middle")
                + (rate * 100f).ToString("0.#")
                + EnemySkills_GetFixedText_Local("defense_status_suffix");
        }
    }
    else if (key == EnemySkillIdDisturb)
    {
        int mpLoss = Mathf.Max(0, cfg.paramX);
        if (mpLoss > 0)
        {
            // ★変更：カットイン終了後にMPゲージをグーっと減らす
            StartCoroutine(__EnemySkill_DisturbMpAfterCutin_Co(mpLoss));
if (statusTMP)
{
    statusTMP.text =
        EnemySkills_GetFixedText_Local("skill_quoted_prefix")
        + skillDisplayName
        + EnemySkills_GetFixedText_Local("skill_quoted_middle")
        + EnemySkills_GetFixedText_Local("disturb_status_prefix")
        + mpLoss
        + EnemySkills_GetFixedText_Local("disturb_status_suffix");
}
        }
    }
    else if (key == EnemySkillIdTrick)
    {
        int count = Mathf.Max(1, cfg.paramX);

        // ★変更：カットイン終了後に対象牌へ魔法演出（0.5秒）→その後に牌を変更
        EnemySkills_ApplyTrickToPlayerHand(count);
        if (statusTMP)
        {
            statusTMP.text =
                EnemySkills_GetFixedText_Local("skill_quoted_prefix")
                + skillDisplayName
                + EnemySkills_GetFixedText_Local("skill_quoted_middle")
                + EnemySkills_GetFixedText_Local("trick_status_suffix");
        }
    }

    // 個別効果の後にカウントダウン UI を更新（仕様どおり Z ターン毎の状態を反映）
    EnemySkills_UpdateCountdownUI();
}
private static string NormalizeEnemySkillKey_Local(string rawSkillId)
{
    if (string.IsNullOrEmpty(rawSkillId)) return "";

    string raw = rawSkillId.Trim();
    string lower = raw.ToLowerInvariant();

    if (lower == "anger" || raw == "怒り") return "anger";
    if (lower == "poison" || raw == "毒") return "poison";
    if (lower == "paralysis" || raw == "麻痺") return "paralysis";
    if (lower == "attack" || raw == "攻撃") return "attack";
    if (lower == "defense" || raw == "防御") return "defense";
    if (lower == "disturb" || raw == "妨害") return "disturb";
    if (lower == "trick" || raw == "細工" || raw == "さいく") return "trick";

    if (lower.StartsWith("enemy_skill."))
        return lower.Substring("enemy_skill.".Length);

    return "";
}
private System.Collections.IEnumerator __EnemySkill_AttackDamageAfterCutin_Co(int baseDamage)
{
    // ★この時点で凍結しておく：次ターン開始を絶対に防ぐ
    _freezeProgression = true;

    // カットイン終了待ち
    while (_enemySkillCutinRunning)
        yield return null;

    // ダメージ演出（1秒で徐々に減る）
    if (baseDamage > 0)
        yield return __EnemySkill_ApplyDamageToPlayerAnimated_ThenMaybeDefeat_Co(baseDamage);

    // __EnemySkill_ApplyDamageToPlayerAnimated_ThenMaybeDefeat_Co が継続可能なら凍結解除する。
    // （HP0の場合は敗北演出へ入り、凍結解除されない）

    if (Mathf.Max(0, playerHP) > 0)
    {
        _freezeProgression = false;
    }
}

private System.Collections.IEnumerator __EnemySkill_ApplyDamageToPlayerAnimated_ThenMaybeDefeat_Co(int baseDamage)
{
    int startHP = Mathf.Max(0, playerHP);

    int dmg = Mathf.Max(0, baseDamage);
    // ApplyDamageToPlayer と同じ計算：被ダメ軽減（例: 0.20 = 20%軽減）
    dmg = Mathf.RoundToInt(dmg * (1f - Mathf.Clamp01(_om.dmgTakenDown)));
    if (dmg < 0) dmg = 0;

    int endHP = Mathf.Max(0, startHP - dmg);

    // SE（和了ダメージ演出と同じSE/Sourceを使う）
    if (enemyWinDamageSESource != null && enemyWinDamageSEClip != null)
    {
        try { enemyWinDamageSESource.PlayOneShot(enemyWinDamageSEClip); } catch {}
    }

    float dur = Mathf.Max(0.01f, enemyWinDamageAnimSeconds);
    float t = 0f;

    while (t < dur)
    {
        t += Time.deltaTime;
        float p = Mathf.Clamp01(t / dur);

        int dispHP = Mathf.RoundToInt(Mathf.Lerp(startHP, endHP, p));
        __UpdatePlayerHpUI_VisualOnly(dispHP);

        yield return null;
    }

    playerHP = endHP;
    UpdateHpUI();

    if (Mathf.Max(0, playerHP) <= 0)
    {
        // ★修正：StartDefeatTransitionIfNeeded を使い _defeatTransitionRunning を
        //   確実にセットする（直接 StartCoroutine すると二重起動や進行再開を防げない）
        StartDefeatTransitionIfNeeded();
        yield break;
    }
}
    /// <summary>
    /// 「細工」ラッパー：
    /// カットイン終了後に「魔法演出」→演出完了後に手牌を書き換える。
    /// </summary>
    private void EnemySkills_ApplyTrickToPlayerHand(int x)
    {
        StartCoroutine(__EnemySkill_TrickAfterCutin_Co(x));
    }

    /// <summary>
    /// 「細工」：手牌のランダムな X 枚をランダムな牌に変換（即時反映しない）
    /// ここでは「対象インデックス」と「変換後ID」を作るだけ。
    /// </summary>
    private void EnemySkills_ApplyTrick(int x)
    {
        // 旧：即時に hand[idx] を書き換えて RefreshAll() していた
        // 新：このメソッド自体は互換のため残すが、直接は使わない
        StartCoroutine(__EnemySkill_TrickAfterCutin_Co(x));
    }

    private System.Collections.IEnumerator __EnemySkill_TrickAfterCutin_Co(int x)
    {
        if (hand == null || hand.Count == 0 || x <= 0) yield break;

        // ★この時点で凍結しておく：プレイヤー操作/進行を防ぐ
        _freezeProgression = true;

        // カットイン終了待ち
        while (_enemySkillCutinRunning)
            yield return null;

        int count = Mathf.Clamp(x, 1, hand.Count);

        // 対象インデックスをシャッフルして先頭 count 個を対象にする
        var indices = new List<int>();
        for (int i = 0; i < hand.Count; i++) indices.Add(i);

        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        var targets = new List<int>();
        var newIds  = new List<string>();

        for (int k = 0; k < count; k++)
        {
            int idx = indices[k];
            if (idx < 0 || idx >= hand.Count) continue;

            int rIndex = rng.Next(34);
            string newId = IndexToId(rIndex);

            targets.Add(idx);
            newIds.Add(newId);
        }

        // 念のためレイアウト確定（handArea 子の rect が 0 になる事故を減らす）
        try
        {
            if (handArea != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(handArea);
            }
        }
        catch { }

        // 魔法演出（0.5秒）
        float fxDur = Mathf.Max(0.01f, enemySkillTrickFxSeconds);
        yield return __EnemySkill_TrickMagicFx_Co(targets, fxDur);

        // 演出が終わったら実際に書き換える
        for (int i = 0; i < targets.Count && i < newIds.Count; i++)
        {
            int idx = targets[i];
            if (idx < 0 || idx >= hand.Count) continue;
            hand[idx] = newIds[i];
        }

        RefreshAll();

string skillDisplayName = EnemySkills_GetDisplayName(EnemySkillIdTrick);
if (statusTMP)
{
    statusTMP.text =
        EnemySkills_GetFixedText_Local("skill_quoted_prefix")
        + skillDisplayName
        + EnemySkills_GetFixedText_Local("skill_quoted_middle")
        + EnemySkills_GetFixedText_Local("trick_done_suffix");
}

        _freezeProgression = false;
    }

    private System.Collections.IEnumerator __EnemySkill_TrickMagicFx_Co(List<int> targetHandIndices, float dur)
    {
        if (handArea == null) yield break;
        if (targetHandIndices == null || targetHandIndices.Count == 0) yield break;

        Sprite grad = __EnemySkill_GetOrCreateTrickGradientSprite();

        var overlays = new List<RectTransform>();

        for (int i = 0; i < targetHandIndices.Count; i++)
        {
            int idx = targetHandIndices[i];
            if (idx < 0) continue;
            if (idx >= handArea.childCount) continue;

            var tileTf = handArea.GetChild(idx);
            if (tileTf == null) continue;

            var tileRt = tileTf as RectTransform;
            if (tileRt == null) continue;

            // overlay 生成
            GameObject go = new GameObject("EnemySkill_TrickFxOverlay");
            go.transform.SetParent(tileTf, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 0f);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.sprite = grad;
            img.type = UnityEngine.UI.Image.Type.Sliced;

            // 色（Inspector色）＋最大アルファ
            Color c = enemySkillTrickFxColor;
            c.a = Mathf.Clamp01(enemySkillTrickFxMaxAlpha);
            img.color = c;

            overlays.Add(rt);
        }

        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);

            for (int i = 0; i < overlays.Count; i++)
            {
                var rt = overlays[i];
                if (rt == null) continue;

                var parentRt = rt.parent as RectTransform;
                float h = 0f;
                try { h = (parentRt != null) ? parentRt.rect.height : 0f; } catch { h = 0f; }
                if (h <= 0.001f) h = 120f; // 万が一 0 の場合の保険

                rt.sizeDelta = new Vector2(0f, Mathf.Lerp(0f, h, p));
            }

            yield return null;
        }

        // 後片付け
        for (int i = 0; i < overlays.Count; i++)
        {
            var rt = overlays[i];
            if (rt == null) continue;
            try { GameObject.Destroy(rt.gameObject); } catch { }
        }
    }

    private Sprite __EnemySkill_GetOrCreateTrickGradientSprite()
    {
        if (_enemySkillTrickGradientSpriteCache != null) return _enemySkillTrickGradientSpriteCache;

        try
        {
            // 1 x H の縦グラデーション（下0 → 上1）
            int w = 1;
            int h = 128;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < h; y++)
            {
                float a = (h <= 1) ? 1f : (float)y / (h - 1);
                // RGB は白、実色は Image.color で付ける
                tex.SetPixel(0, y, new Color(1f, 1f, 1f, a));
            }

            tex.Apply(false, false);

            var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            _enemySkillTrickGradientSpriteCache = sp;
            return sp;
        }
        catch
        {
            return null;
        }
    }
private System.Collections.IEnumerator __EnemySkill_DisturbMpAfterCutin_Co(int mpLoss)
{
    if (mpLoss <= 0) yield break;

    // ★この時点で凍結しておく：次ターン開始を防ぐ
    _freezeProgression = true;

    // カットイン終了待ち
    while (_enemySkillCutinRunning)
        yield return null;

    int startMp = Mathf.Max(0, _mp);
    int endMp   = Mathf.Max(0, startMp - Mathf.Max(0, mpLoss));

    // ★追加：MPダメージでも、和了ダメージ演出と同じSE/Sourceを使う
    if (startMp != endMp)
    {
        if (enemyWinDamageSESource != null && enemyWinDamageSEClip != null)
        {
            try { enemyWinDamageSESource.PlayOneShot(enemyWinDamageSEClip); } catch { }
        }
    }

    // ★追加：MP減少演出中フラグ（この場面だけ暗くしない＆操作ロック）
    if (startMp != endMp)
    {
        _mpDecreaseAnimRunning = true;
        UpdateButtons();
    }

    float dur = Mathf.Max(0.01f, enemySkillDisturbMpAnimSeconds);
    float t = 0f;

    int maxMp = 1;
    try { maxMp = Mathf.Max(1, EffectiveMaxMP()); } catch { maxMp = 1; }

    while (t < dur)
    {
        t += Time.deltaTime;
        float p = Mathf.Clamp01(t / dur);

        int dispMp = Mathf.RoundToInt(Mathf.Lerp(startMp, endMp, p));
        __UpdatePlayerMpUI_VisualOnly(dispMp, maxMp);

        yield return null;
    }

    _mp = endMp;

    // 最終値を正規UI更新で確定（保存/状態異常UI復元含む）
    try { UpdateMpUI(); } catch { }
    try { UpdateHpUI(); } catch { }

    _freezeProgression = false;

    // ★追加：演出終了で即ボタン復帰
    if (startMp != endMp)
    {
        _mpDecreaseAnimRunning = false;
        UpdateButtons();
    }
}
private void __UpdatePlayerMpUI_VisualOnly(int dispCur, int dispMax)
{
    int effMax = Mathf.Max(1, dispMax);
    int cur    = Mathf.Clamp(dispCur, 0, effMax);

    // 1) SkillMP Addon 側の手動UI（Slider/Text）
    if (mpTMP != null) mpTMP.text = $"MP {cur}/{effMax}";
    if (mpSlider != null)
    {
        if (!Mathf.Approximately(mpSlider.maxValue, effMax))
            mpSlider.maxValue = effMax;
        mpSlider.value = cur;
    }

    // 2) GameManager.cs 側の手動UI（Image/TMP）
    if (playerMPTMP)
    {
        string fmt = (playerMPConfig != null && !string.IsNullOrEmpty(playerMPConfig.textFormat))
            ? playerMPConfig.textFormat : "{cur}/{max}";
        playerMPTMP.text = fmt.Replace("{cur}", cur.ToString()).Replace("{max}", effMax.ToString());
    }

    if (playerMPBar)
    {
        if (playerMPConfig != null)
        {
            playerMPBar.type = playerMPConfig.fillType;
            if (playerMPBar.type == UnityEngine.UI.Image.Type.Filled)
            {
                playerMPBar.fillMethod = playerMPConfig.fillMethod;
                playerMPBar.fillOrigin = playerMPConfig.fillOrigin;
                playerMPBar.fillAmount = (effMax > 0) ? (float)cur / effMax : 0f;
            }
            if (playerMPConfig.overrideColor) playerMPBar.color = playerMPConfig.color;
        }
        else
        {
            playerMPBar.type       = UnityEngine.UI.Image.Type.Filled;
            playerMPBar.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            playerMPBar.fillOrigin = 0;
            playerMPBar.fillAmount = (effMax > 0) ? (float)cur / effMax : 0f;
        }
    }
}

    // ========================
    //  カットイン演出
    // ========================
    private System.Collections.IEnumerator EnemySkills_ShowCutinCoroutine(string skillLabel)
    {
        _enemySkillCutinRunning = true;

        if (!enemySkillCutinRoot)
        {
            _enemySkillCutinRunning = false;
            yield break;
        }

        // テキスト設定（スキル名そのまま）
        if (enemySkillCutinTextTMP)
        {
            enemySkillCutinTextTMP.text = skillLabel;
        }

        // 画像：敵リーチと同じカットイン画像を流用
        try
        {
            if (enemySkillCutinImage && enemyRiichiImage)
            {
                enemySkillCutinImage.sprite = enemyRiichiImage.sprite;
            }
        }
        catch { }

        // カットイン表示
        enemySkillCutinRoot.SetActive(true);

        // 敵リーチと同じ 3 秒表示（フェードは Animator 側）
        yield return new WaitForSeconds(3f);

        enemySkillCutinRoot.SetActive(false);
        _enemySkillCutinRunning = false;
    }
private void EnemySkills_ModifyDamageBeforeApply(
    ref int totalDamage,
    ref int mpRecovered,
    ref int hpRecovered)
{
    bool isPlayerAttack = _currentScoringAttackerIsPlayer;
    bool isEnemyAttack  = !isPlayerAttack;

    // ★毎回まず「今回の計算で適用された値」を初期化（表示はこの値を見る）
    _enemySkillLastAppliedAngerMultiplier = 1f;
    _enemySkillLastAppliedDefenseRate     = 0f;

    if (isEnemyAttack && _enemySkillAngerTurnRemaining > 0 && _enemySkillAngerMultiplier > 1f)
    {
        _enemySkillLastAppliedAngerMultiplier = _enemySkillAngerMultiplier;

        totalDamage = Mathf.Max(0, Mathf.RoundToInt(totalDamage * _enemySkillAngerMultiplier));
    }

    if (isPlayerAttack && _enemySkillDefenseTurnRemaining > 0 && _enemySkillPlayerDamageDownRate > 0f)
    {
        _enemySkillLastAppliedDefenseRate = _enemySkillPlayerDamageDownRate;

        float factor = 1f - Mathf.Clamp01(_enemySkillPlayerDamageDownRate);
        totalDamage = Mathf.Max(0, Mathf.RoundToInt(totalDamage * factor));
    }
}
private void EnemySkills_UpdateCountdownUI()
{
    if (!enemySkillCountdownTMP) return;

    if (_enemySkills.Count == 0)
    {
        enemySkillCountdownTMP.text = "";
        return;
    }

    // 念のため、スキル数とカウンタ数を揃える
    while (_enemySkillTurnCounters.Count < _enemySkills.Count)
    {
        _enemySkillTurnCounters.Add(0);
    }
    while (_enemySkillTurnCounters.Count > _enemySkills.Count)
    {
        _enemySkillTurnCounters.RemoveAt(_enemySkillTurnCounters.Count - 1);
    }

    var lines = new System.Text.StringBuilder();

    for (int i = 0; i < _enemySkills.Count; i++)
    {
        var cfg = _enemySkills[i];
        if (cfg == null || string.IsNullOrEmpty(cfg.id)) continue;

        // ★IDはそのまま、表示だけ差し替える
        string skillDisplayName = EnemySkills_GetDisplayName(cfg.id);

        int threshold = (cfg.paramZ > 0) ? cfg.paramZ : 1;
        int turnsSince = (i < _enemySkillTurnCounters.Count)
            ? _enemySkillTurnCounters[i]
            : 0;

        if (threshold <= 1)
        {
            // Z <= 1 は毎ターン発動扱い
            lines.AppendLine(skillDisplayName + EnemySkills_GetFixedText_Local("countdown_every_turn_suffix"));
            continue;
        }

        // 「Zターン目で100%」になるまでの残りターン
        int remain = Mathf.Max(0, threshold - turnsSince);

        lines.AppendLine(
            skillDisplayName
            + EnemySkills_GetFixedText_Local("countdown_remain_prefix")
            + remain
            + EnemySkills_GetFixedText_Local("countdown_remain_middle")
            + threshold
            + EnemySkills_GetFixedText_Local("countdown_remain_suffix"));
    }

    enemySkillCountdownTMP.text = lines.ToString();
}
void EnemySkills_RefreshStatusEffectsUI()
{
    try
    {
        // 対象UIが無ければ何もしない（デグレ防止）
        if (playerHPBar == null && playerMPBar == null) return;

        Color NormalizeAlpha(Color c)
        {
            if (c.a <= 0.001f) c.a = 1f;
            return c;
        }

        // まだ通常色をキャッシュしていなければキャッシュ
        // ★重要：キャッシュ元が「一時的に透明化された色」だと、以後ずっと透明で復元されてしまう。
        //         そのため、キャッシュする色は必ず alpha=1 に補正して保存する。
        if (!_enemySkillStatusUiColorsCached)
        {
            if (playerHPBar != null) _enemySkillNormalHpBarColor = NormalizeAlpha(playerHPBar.color);
            if (playerMPBar != null) _enemySkillNormalMpBarColor = NormalizeAlpha(playerMPBar.color);
            _enemySkillStatusUiColorsCached = true;
        }

        // --- 毒：HPバー色＋アイコン ---
        bool poisonActive = (_enemySkillPoisonTurnRemaining > 0);

        if (playerHPBar != null)
        {
            if (poisonActive)
            {
                playerHPBar.color = NormalizeAlpha(enemySkillPoisonHpColor);
            }
            else
            {
                playerHPBar.color = _enemySkillNormalHpBarColor;
            }
        }

        if (enemySkillPoisonIcon != null)
        {
            enemySkillPoisonIcon.SetActive(poisonActive);
        }

        // --- 麻痺：MPバー色＋アイコン ---
        bool paralysisActive = (_enemySkillParalysisTurnRemaining > 0);

        if (playerMPBar != null)
        {
            if (paralysisActive)
            {
                // ★麻痺中は常に黄色（敵ターン/プレイヤーターン関係なく維持）
                playerMPBar.color = NormalizeAlpha(enemySkillParalysisMpColor);
            }
            else
            {
                playerMPBar.color = _enemySkillNormalMpBarColor;
            }
        }

        if (enemySkillParalysisIcon != null)
        {
            enemySkillParalysisIcon.SetActive(paralysisActive);
        }

        // --- 怒り：アイコン ---
        bool angerActive = (_enemySkillAngerTurnRemaining > 0);
        if (enemySkillAngerIcon != null)
        {
            enemySkillAngerIcon.SetActive(angerActive);
        }

        // --- 防御：アイコン ---
        bool defenseActive = (_enemySkillDefenseTurnRemaining > 0);
        if (enemySkillDefenseIcon != null)
        {
            enemySkillDefenseIcon.SetActive(defenseActive);
        }
    }
    catch
    {
        // 失敗しても進行を止めない
    }
}

}
