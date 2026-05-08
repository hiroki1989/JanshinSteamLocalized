
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

// Add-on for progression, enemy UI (name/portrait), and stage loop control.
// This file is a PARTIAL of GameManager. It does not modify gameplay specs.
public partial class GameManager : MonoBehaviour
{
    // ======= Enemy Catalog (20 fixed names) =======

    // ======= UI (drag & assign in Inspector; if null, handled gracefully) =======
[Header("Enemy UI")]
// enemyNameTMP は他ファイルの定義を使用（ここでは削除）
[SerializeField] private UnityEngine.UI.Image   enemyPortrait;

[Header("Player UI")]
[SerializeField] private UnityEngine.UI.Image   playerPortrait;
    // PlayerPrefs keys (fallback if PlayerData not present)
    private const string KeyCurrentEnemyIndex = "CurrentEnemyIndex";
    private const string KeyLoopCount        = "EnemyLoopCount";

    // Helper: get/set current enemy index (0-based, unlimited; 20で1周)
    public static int GetCurrentEnemyIndex()
    {
        try
        {
            // Prefer external PlayerData if available
            var t = typeof(PlayerData);
            var prop = t.GetProperty("CurrentEnemyIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop != null) return Mathf.Max(0, (int)prop.GetValue(null, null));
            var prop2 = t.GetProperty("CurrentEnemy", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop2 != null) return Mathf.Max(0, (int)prop2.GetValue(null, null));
        }
        catch { }
        return PlayerPrefs.GetInt(KeyCurrentEnemyIndex, 0);
    }
    public static void SetCurrentEnemyIndex(int idx)
    {
        // Also try to set PlayerData if it exists
        try
        {
            var t = typeof(PlayerData);
            var prop = t.GetProperty("CurrentEnemyIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop != null) { prop.SetValue(null, idx, null); }
            else
            {
                var prop2 = t.GetProperty("CurrentEnemy", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop2 != null) prop2.SetValue(null, idx, null);
            }
        }
        catch { }
        PlayerPrefs.SetInt(KeyCurrentEnemyIndex, Mathf.Max(0, idx));
        PlayerPrefs.Save();
    }
    public static int GetLoopCount()
    {
        return PlayerPrefs.GetInt(KeyLoopCount, 0);
    }
    public static void SetLoopCount(int loop)
    {
        PlayerPrefs.SetInt(KeyLoopCount, Mathf.Max(0, loop));
        PlayerPrefs.Save();
    }

    // ---- NOTE ----
    // We intentionally DO NOT define OnEnable/Start/Update here to avoid duplicate method conflicts.
    // Instead, a bootstrapper component will call this internal init method once on scene start.
    private void __Progression_InternalInit()
    {
        try
        {
            
SetupEnemyProgression();
        SetupEnemyUI();
        SetupPlayerUI();
        // Carry over player's HP across enemy transitions (load if saved)
// Carry over player's HP across enemy transitions (load if saved)
// フル回復フラグが立っている場合は読み戻しスキップ
// フル回復フラグが立っている場合は読み戻しスキップ
try
{
    bool pendingFullHeal = false;
    try { pendingFullHeal = PlayerPrefs.GetInt(GameManager.KeyPendingFullHeal, 0) != 0; } catch {}

    // --- HP のみ読み戻し ---
    if (!pendingFullHeal && PlayerPrefs.HasKey("Run_PlayerHP"))
    {
        int saved = PlayerPrefs.GetInt("Run_PlayerHP", -1);
        if (saved >= 0)
        {
            playerHP = Mathf.Clamp(saved, 0, playerMaxHP);
        }
    }

    // --- MP のみ読み戻し ---
    if (!pendingFullHeal && PlayerPrefs.HasKey("Run_PlayerMP"))
    {
        int savedMp = PlayerPrefs.GetInt("Run_PlayerMP", -1);
        if (savedMp >= 0)
        {
            // 有効最大 MP を超えないようにして復元
            _mp = ClampToEffectiveMaxMP(savedMp);
        }
    }
}
catch {}

// HP / MP UI があれば更新しておく
UpdateHpUI();
try { UpdateMpUI(); } catch {}

}
        catch { /* keep robust */ }
    }

private void SetupEnemyProgression()
{
    int idxAbs = Mathf.Max(0, GetCurrentEnemyIndex());

    int count = 0;
    try { count = EnemyConfigExcel.GetNormalEnemyCount(); } catch { count = 0; }
    if (count <= 0) { count = 1; }

    int loop  = idxAbs / count;
    int local = idxAbs % count;
    SetLoopCount(loop);

    ApplyEnemyConfigFromExcel(idxAbs);

    float tierMult = 1f;
    try { tierMult = GetCurrentTierMultiplier(); } catch { tierMult = 1f; }

    enemyAttackMultiplier = tierMult * Mathf.Pow(1.05f, loop);
}
private const string KeyCurrentTier = "PF_CurrentTier";
private const string KeyUnlockedTierMax = "PF_UnlockedTierMax";

public static int GetCurrentTier()
{
    return Mathf.Max(1, PlayerPrefs.GetInt(KeyCurrentTier, 1));
}

public static void SetCurrentTier(int tier)
{
    PlayerPrefs.SetInt(KeyCurrentTier, Mathf.Max(1, tier));
    PlayerPrefs.Save();
}

public static int GetUnlockedTierMax()
{
    return Mathf.Max(1, PlayerPrefs.GetInt(KeyUnlockedTierMax, 1));
}

public static void SetUnlockedTierMax(int tier)
{
    PlayerPrefs.SetInt(KeyUnlockedTierMax, Mathf.Max(1, tier));
    PlayerPrefs.Save();
}

public static float GetCurrentTierMultiplier()
{
    int tier = GetCurrentTier();
    return 1f + 0.3f * (tier - 1);
}

public static int GetCurrentGlobalLevelNumber()
{
    int tier = GetCurrentTier();
    int localIndex = Mathf.Max(0, GetCurrentEnemyIndex()); // 0..9想定
    return (tier - 1) * 10 + (localIndex + 1);
}
public static bool IsCurrentTierLastLevel()
{
    int idxAbs = Mathf.Max(0, GetCurrentEnemyIndex());

    int count = 0;
    try { count = EnemyConfigExcel.GetNormalEnemyCount(); } catch { count = 0; }
    if (count <= 0) count = 10;

    int local = idxAbs % count;
    return local >= (count - 1);
}

public static void UnlockNextTierIfCleared()
{
    int tier = GetCurrentTier();
    int unlocked = GetUnlockedTierMax();
    int next = tier + 1;
    if (next > unlocked)
    {
        SetUnlockedTierMax(next);
    }
}
private void SetupEnemyUI()
{
    int idxAbs = Mathf.Max(0, GetCurrentEnemyIndex());

    if (EnemyConfigExcel.TryGetForRuntimeIndex(idxAbs, out var cfg) && !string.IsNullOrEmpty(cfg.name))
    {
        try { RefreshEnemyNameUIFromCurrentConfig(); } catch {}
        try { TryLoadEnemyRunPortraitByName(cfg.name); } catch {}
    }
    else
    {
        // Excelが取れないときは何も表示しない（保険なし）
        try { SetEnemyNameOnUI(string.Empty); } catch {}
        if (enemyPortrait) enemyPortrait.enabled = false;
    }
}
private void TryLoadEnemyRunPortraitByName(string enemyName)
{
    if (!enemyPortrait) return;

    if (string.IsNullOrEmpty(enemyName))
    {
        enemyPortrait.enabled = false;
        return;
    }

    // 念のため「 +N」などが入っていたら落とす
    const string marker = " +";
    int markerPos = enemyName.IndexOf(marker, StringComparison.Ordinal);
    if (markerPos > 0)
    {
        enemyName = enemyName.Substring(0, markerPos);
    }

    enemyName = enemyName.Trim();
    if (string.IsNullOrEmpty(enemyName))
    {
        enemyPortrait.enabled = false;
        return;
    }

    // Assets/Resources/EnemyPortraits/{enemyName}_portrait.png を想定
    string path = "EnemyPortraits/" + enemyName + "_portrait";
    Sprite sp = Resources.Load<Sprite>(path);

    enemyPortrait.sprite = sp;
    enemyPortrait.preserveAspect = true;
    enemyPortrait.enabled = (sp != null);
}

private void SetupPlayerUI()
{
    try
    {
        var skill = GetEquippedSkill();
        string skillName = (skill != null) ? skill.ToString() : string.Empty;

        TryLoadPlayerRunPortraitBySkillName(skillName);
    }
    catch
    {
        if (playerPortrait) playerPortrait.enabled = false;
    }
}

private void TryLoadPlayerRunPortraitBySkillName(string skillName)
{
    if (!playerPortrait) return;

    if (string.IsNullOrEmpty(skillName))
    {
        playerPortrait.enabled = false;
        return;
    }

    skillName = skillName.Trim();
    if (string.IsNullOrEmpty(skillName))
    {
        playerPortrait.enabled = false;
        return;
    }

    // Assets/Resources/PlayerPortraits/{SkillName}_portrait.png を想定
    string path = "PlayerPortraits/" + skillName + "_portrait";
    Sprite sp = Resources.Load<Sprite>(path);

    playerPortrait.sprite = sp;
    playerPortrait.preserveAspect = true;
    playerPortrait.enabled = (sp != null);
}

public static void AdvanceToNextEnemy()
{
    int cur  = GetCurrentEnemyIndex();
    int next = cur + 1;

// ★通常進行の敵数は Key<10 のみ（Key=10 は裏ボス専用）
int total = 0;
try { total = EnemyConfigExcel.GetNormalEnemyCount(); } catch { total = 0; }
if (total <= 0) total = 1; // 0割回避

    int loopBefore = GetLoopCount();
    int loopAfter  = (next / total);
    bool clearedThisRun = (next >= total);

    if (loopAfter > loopBefore) SetLoopCount(loopAfter);
    if (next >= total) next = 0; // 末尾→先頭へ

    SetCurrentEnemyIndex(next);

if (clearedThisRun && loopAfter > loopBefore)
{
    try { PlayerPrefs.SetInt("PF_PendingFullHeal", 1); PlayerPrefs.Save(); } catch {}
    try
    {
        PlayerPrefs.DeleteKey("Run_PlayerHP");
        PlayerPrefs.DeleteKey("Run_PlayerMP");
        PlayerPrefs.Save();
    }
    catch {}
}

}
public static void StartNextBattleScene(string battleSceneName = "RunScene")
{
    // Optional: enemy conversation before battle
    string enemyTalkScene = PlayerPrefs.GetString("EnemyDialogueScene", "");
    string angelTalkScene = PlayerPrefs.GetString("AngelDialogueScene", "");

    // ★重要：一度使った「会話シーン指定」は必ず消す（残ると永遠に会話へ飛ぶ）
    if (!string.IsNullOrEmpty(angelTalkScene))
    {
        try { PlayerPrefs.DeleteKey("AngelDialogueScene"); } catch {}
    }
    if (!string.IsNullOrEmpty(enemyTalkScene))
    {
        try { PlayerPrefs.DeleteKey("EnemyDialogueScene"); } catch {}
    }

    if (!string.IsNullOrEmpty(angelTalkScene))
    {
        // ★最初から対局開始（天使会話を経由）では、必ず最大値まで回復して開始させる
        //   - お守り等で上がった最大HP/最大MPを反映した上で満タン化したいので
        //     RunScene側の __ApplyRunBonusesAndRefreshUI() に委ねる（PF_PendingFullHeal を立てる）
        try
        {
            PlayerPrefs.SetInt("PF_PendingFullHeal", 1);
            PlayerPrefs.DeleteKey("Run_PlayerHP");
            PlayerPrefs.DeleteKey("Run_PlayerMP");

            // 新規開始時に前回Runの成長が混ざると%計算もズレるので必ず消す
            PlayerPrefs.DeleteKey("Run_HPBonus");
            PlayerPrefs.DeleteKey("Run_MPBonus");
            PlayerPrefs.DeleteKey("Run_SkillCastsBonus");

            // ★重要：前回の敗北/クリア会話モードが残っていると「Defeat→報酬」に飛ぶ
            PlayerPrefs.SetString("PF_AngelDialogueMode", "Start");

            // 天使会話の次遷移先：敵会話が指定されているならそこ、無ければRunへ
            string next = !string.IsNullOrEmpty(enemyTalkScene) ? enemyTalkScene : battleSceneName;
            PlayerPrefs.SetString("PF_AngelDialogueNextScene", next);

            PlayerPrefs.Save();
        }
        catch { }

        SceneManager.LoadScene(angelTalkScene);
        return;
    }

    if (!string.IsNullOrEmpty(enemyTalkScene))
    {
        SceneManager.LoadScene(enemyTalkScene);
        return;
    }

    SceneManager.LoadScene(battleSceneName);
}
    // 2025/11/13 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

// 敵の攻撃力を計算するための基礎値
private int baseAttackPower = 10; // 基本攻撃力（必要に応じて調整可能）

// 敵の攻撃力を計算するプロパティ
private int enemyAttackPower
{
    get
    {
        // 攻撃力は進行度に応じて倍率をかけて計算
        return Mathf.RoundToInt(baseAttackPower * enemyAttackMultiplier);
    }
}
}
