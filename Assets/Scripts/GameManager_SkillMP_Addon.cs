
// GameManager_SkillMP_Addon.cs
// Drop-in partial for GameManager (do not add as a separate component; just keep this file in project)
// Replaces MP handling, case-insensitive skill resolution, and routes button to OnClickSkill_MP.
// Place outside of any "Editor" folder.
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic; // ← 追加（List<string> に必要）
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

public partial class GameManager : MonoBehaviour
{
    // ===== Omamori helpers (MP) =====
private PlayerData.OmamoriStats __Om() {
    try { return PlayerData.GetEquippedStats(); } catch { return default; }
}
private int EffectiveMaxMP()
{
    // ★中断復元中は「中断時の最大MP」をラン中だけ固定
    if (_suspendLoadoutLocked && _suspendLockedEffectiveMaxMP >= 0)
    {
        return Mathf.Max(0, _suspendLockedEffectiveMaxMP);
    }

    int baseMax = _skillSet ? _skillSet.maxMP : 0;
    // お守りの最大MP%上昇を反映（maxMpUp は 0.04 = +4% のような小数表現）
    var om = __Om();
    int omMax = Mathf.RoundToInt(baseMax * (1f + Mathf.Max(0f, om.maxMpUp)));

    int runMp = 0;
    try { runMp = Mathf.Max(0, PlayerPrefs.GetInt("Run_MPBonus", 0)); } catch {}

    // ★Shop永続強化（宝石購入分）
    int permMp = 0;
    try { permMp = Mathf.Max(0, PlayerPrefs.GetInt("Perm_MPBonus", 0)); } catch { permMp = 0; }

   int uniqueMp = 0;
try
{
    if (PlayerData.IsEquippedUniqueEffect(PlayerData.UniqueOmamoriEffectKind.Susanoo_MpPlus10000))
    {
        uniqueMp = 10000;
    }
}
catch { uniqueMp = 0; }

return Mathf.Max(0, omMax + runMp + permMp + uniqueMp);

}

// “実効”最大MPでクランプ
private int ClampToEffectiveMaxMP(int v) => Mathf.Clamp(v, 0, EffectiveMaxMP());
// お札など「MPが満タン」条件用（実効最大MP基準）
public bool IsPlayerMpFull()
{
    int max = EffectiveMaxMP();
    if (max <= 0) return false;
    return _mp >= max; // 「==」ではなく「>=」にする（最大MP変動や丸め対策）
}

// デバッグや他条件用（必要なら）
public int GetPlayerMp_Current() => _mp;
public int GetPlayerMp_MaxEffective() => EffectiveMaxMP();
    // ===== パネル表示用に、最後に使われた係数を覚えておく =====
private float _lastGekiMul = 0f;   // 撃の倍率（基礎点 × 倍率 = 追加ダメの倍率分）
private int   _lastShunAdd = 0;    // 瞬の固定加算ダメ
private float _lastIyuMul  = 0f;   // 癒の回復倍率（基礎点 × 倍率 = 回復量）

// 既にある想定：撃/瞬の合算ダメ, 癒の回復量（最終数値）
private int   _lastTraitAttack = 0;
private int   _lastTraitHeal   = 0;

    [Header("MP (new)")]
    [Header("Per-Turn Skill Limit")]
[SerializeField] private int maxSkillCastsPerTurn = 1; // ← Inspectorで設定可能（既定1）
private int _skillCastsUsedThisTurn = 0;
[Header("Skill Info (Run)")]
[SerializeField] private TMPro.TextMeshProUGUI skillInfoTMP;   // ← いま対局画面で「スキル説明」を出している TMP を割り当て
[SerializeField, TextArea] private string defaultSkillDescription = "説明未設定のスキルです。";
[SerializeField] private TextMeshProUGUI mpTMP;   // Auto側が作るTextを拾う想定（未割当OK）
[SerializeField] private Slider mpSlider;         // Auto側が作るSliderを拾う想定（未割当OK）
    [SerializeField] private Image  mpFillImage;      // optional fill image (if using Image.fillAmount)
    [SerializeField] private SkillSetAsset fallbackSkillSet; // used when PlayerPrefs id is empty/invalid
    [SerializeField] private string skillSetResourcesFolder = "SkillSets"; // Assets/Resources/SkillSets/*.asset
    [SerializeField] private YakuTraitMapAsset yakuTraitMap; // kept for trait calculation (not used here)
    [SerializeField] private CharmLoadoutAsset equippedCharm; // kept for damage/heal modifiers (not used here)

    private SkillSetAsset _skillSet;
    private int _mp = 0;

    // ★追加：装備スキル不整合の警告を一度だけ出すためのフラグ
    private bool _loggedMismatchedActiveSkill = false;

    private const string PrefKeySet = "EquippedSkillSetId";       // PlayerPrefs key for equipped skill set
    private const string PrefKeyActive = "EquippedActiveSkill";   // PlayerPrefs key for active skill name

    private const string CanonicalSkillIdRandomMan = "RandomMan";
    private const string CanonicalSkillIdEnhanceHand = "EnhanceHand";

    private string GetCanonicalSkillId(ActiveSkill skill)
    {
        switch (skill)
        {
            case ActiveSkill.RandomMan:
                return CanonicalSkillIdRandomMan;

            case ActiveSkill.EnhanceHand:
                return CanonicalSkillIdEnhanceHand;

            default:
                return skill.ToString();
        }
    }
    private string NormalizeSkillIdForStorage(string id)
    {
        var e = SkillIdToEnum(id);
        if (e == ActiveSkill.None)
            return string.IsNullOrEmpty(id) ? "" : id.Trim();

        return GetCanonicalSkillId(e);
    }

    private SkillSetAsset FindOwnerSkillSetBySkillId_Local(IEnumerable<SkillSetAsset> sets, string rawSkillId)
    {
        if (sets == null) return null;

        string normalized = NormalizeSkillIdForStorage(rawSkillId);
        if (string.IsNullOrEmpty(normalized)) return null;

        foreach (var set in sets)
        {
            if (set == null || set.activeSkills == null) continue;

            foreach (var entry in set.activeSkills)
            {
                if (entry == null) continue;

                string entryId = NormalizeSkillIdForStorage(entry.activeSkillName);
                if (!string.IsNullOrEmpty(entryId) &&
                    string.Equals(entryId, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return set;
                }
            }
        }

        return null;
    }

    private string LocalizeActiveSkillDisplayName_Local(string displayOrId)
    {
        if (string.IsNullOrEmpty(displayOrId)) return "";

        string normalized = NormalizeSkillIdForStorage(displayOrId);
        if (string.IsNullOrEmpty(normalized))
            return displayOrId ?? "";

        if (string.Equals(normalized, CanonicalSkillIdRandomMan, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, CanonicalSkillIdEnhanceHand, StringComparison.OrdinalIgnoreCase))
        {
            return LocalizationManager.ActiveSkill(normalized);
        }

        return displayOrId ?? "";
    }
    private string GetSkillInfoFixedText_Local(string key)
    {
        return LocalizationManager.Fixed(key);
    }

    private string GetSkillMpFixedText_Local(string key)
    {
        return LocalizationManager.Fixed(key);
    }
private string LocalizeSkillPanelYakuName_Local(string rawYakuName)
{
    if (string.IsNullOrEmpty(rawYakuName))
        return "";

    string src = rawYakuName.Trim();
    var lm = LocalizationManager.Instance;
    if (lm == null)
        return src;

    string openTag = "";
    string closeTag = "";
    string body = src;

    if (body.StartsWith("<color=", StringComparison.OrdinalIgnoreCase))
    {
        int tagEnd = body.IndexOf('>');
        if (tagEnd >= 0)
        {
            openTag = body.Substring(0, tagEnd + 1);
            body = body.Substring(tagEnd + 1);
        }
    }

    if (body.EndsWith("</color>", StringComparison.OrdinalIgnoreCase))
    {
        int closeStart = body.LastIndexOf("</color>", StringComparison.OrdinalIgnoreCase);
        if (closeStart >= 0)
        {
            closeTag = body.Substring(closeStart);
            body = body.Substring(0, closeStart);
        }
    }

    string suffix = "";
    string baseName = body.Trim();

    int lvIndex = baseName.IndexOf(" Lv.", StringComparison.Ordinal);
    if (lvIndex < 0)
        lvIndex = baseName.IndexOf(" LV.", StringComparison.Ordinal);
    if (lvIndex < 0)
        lvIndex = baseName.IndexOf(" lv.", StringComparison.Ordinal);

    if (lvIndex >= 0)
    {
        suffix = baseName.Substring(lvIndex);
        baseName = baseName.Substring(0, lvIndex).Trim();
    }

    string localizedBase = baseName;

    switch (baseName)
    {
        case "国士無双": localizedBase = lm.GetYakumanDisplayName("KOKUSHI"); break;
        case "七対子": localizedBase = lm.GetYakuDisplayName("CHIITOITSU"); break;
        case "門前清自摸和": localizedBase = lm.GetYakuDisplayName("MENZEN_TSUMO"); break;
        case "タンヤオ": localizedBase = lm.GetYakuDisplayName("TANYAO"); break;
        case "平和": localizedBase = lm.GetYakuDisplayName("PINFU"); break;
        case "役牌": localizedBase = lm.GetYakuDisplayName("YAKUHAI"); break;
        case "一盃口": localizedBase = lm.GetYakuDisplayName("IIPEIKOU"); break;
        case "二盃口": localizedBase = lm.GetYakuDisplayName("RYANPEIKOU"); break;
        case "三色同順": localizedBase = lm.GetYakuDisplayName("SANSHOKU_DOUJUN"); break;
        case "一気通貫": localizedBase = lm.GetYakuDisplayName("ITTSU"); break;
        case "チャンタ": localizedBase = lm.GetYakuDisplayName("CHANTA"); break;
        case "純チャン": localizedBase = lm.GetYakuDisplayName("JUNCHAN"); break;
        case "対々和": localizedBase = lm.GetYakuDisplayName("TOITOI"); break;
        case "三暗刻": localizedBase = lm.GetYakuDisplayName("SANANKOU"); break;
        case "三カンツ": localizedBase = lm.GetYakuDisplayName("SANKANTSU"); break;
        case "三色同刻": localizedBase = lm.GetYakuDisplayName("SANSHOKU_DOUKOU"); break;
        case "小三元": localizedBase = lm.GetYakuDisplayName("SHOUSANGEN"); break;
        case "混老頭": localizedBase = lm.GetYakuDisplayName("HONROUTOU"); break;
        case "混一色": localizedBase = lm.GetYakuDisplayName("HONITSU"); break;
        case "清一色": localizedBase = lm.GetYakuDisplayName("CHINITSU"); break;

        case "九蓮宝燈": localizedBase = lm.GetYakumanDisplayName("CHUUREN_POUTOU"); break;
        case "大三元": localizedBase = lm.GetYakumanDisplayName("DAISANGEN"); break;
        case "大四喜": localizedBase = lm.GetYakumanDisplayName("DAISUUSHI"); break;
        case "小四喜": localizedBase = lm.GetYakumanDisplayName("SHOUSUUSHI"); break;
        case "字一色": localizedBase = lm.GetYakumanDisplayName("TSUUIISOU"); break;
        case "清老頭": localizedBase = lm.GetYakumanDisplayName("CHINROUTOU"); break;
        case "緑一色": localizedBase = lm.GetYakumanDisplayName("RYUUIISOU"); break;
        case "四暗刻": localizedBase = lm.GetYakumanDisplayName("SUUANKOU"); break;
        case "四カンツ": localizedBase = lm.GetYakumanDisplayName("SUUKANTSU"); break;
    }

    return openTag + localizedBase + suffix + closeTag;
}
private string BuildSkillDescText()
{
    var active = ResolveActiveSkillForMP();

    if (_skillSet == null || active == ActiveSkill.None)
    {
        return "MP Cost 0\n0/0";
    }

    int baseCost;
    if (!TryGetActiveSkillMpCost(active, out baseCost))
    {
        baseCost = 0;
    }

    int finalCost = ComputeFinalSkillMpCost(baseCost);

    int maxCasts = Mathf.Max(1, GetMaxSkillCastsThisTurn());
    int remainingCasts = Mathf.Clamp(maxCasts - _skillCastsUsedThisTurn, 0, maxCasts);

    return $"MP Cost {finalCost}\n{remainingCasts}/{maxCasts}";
}
private IEnumerator __PlayerSkill_ConvertOfferTiles_WithMagicFx_Co(List<int> targetOfferIndices, List<string> newIds, Color fxColor)
{
    _playerSkillTransformRunning = true;
    UpdateButtons();

    try
    {
        try { EnableAllHandButtons(false); } catch { }
        try { EnableAllOfferButtons(false); } catch { }

        try
        {
            if (selHand != null) selHand.Clear();
            if (selOffer != null) selOffer.Clear();

            RebuildRaiseOverlays(handArea, selHand, hand);
            RebuildRaiseOverlays(offerArea, selOffer, offers);
        }
        catch { }

        if (!_playerSkillCutinRunning)
        {
            try { StartPlayerSkillCutin(GetActiveSkillActionNameSafe(ResolveActiveSkillForMP())); } catch { }
        }

        while (_playerSkillCutinRunning)
            yield return null;

        try
        {
            if (offerArea != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(offerArea);
            }
        }
        catch { }

        float fxDur = 0.5f;
        try { fxDur = Mathf.Max(0.01f, enemySkillTrickFxSeconds); } catch { fxDur = 0.5f; }

        yield return __PlayerSkill_TrickMagicFx_OfferTiles_Co(targetOfferIndices, fxDur, fxColor);

        if (offers != null)
        {
            for (int i = 0; i < targetOfferIndices.Count && i < newIds.Count; i++)
            {
                int idx = targetOfferIndices[i];
                if (idx < 0 || idx >= offers.Count) continue;

                offers[idx] = newIds[i];
            }
        }

        RefreshOfferUI();
        EvaluateWinUI_New();

        if (statusTMP) statusTMP.text = "ツモ場の4枚をランダムな牌に変換しました";
    }
    finally
    {
        _playerSkillTransformRunning = false;

        try { EnableAllHandButtons(true); } catch { }
        try { EnableAllOfferButtons(true); } catch { }

        UpdateButtons();
    }
}

private IEnumerator __PlayerSkill_TrickMagicFx_OfferTiles_Co(List<int> targetOfferIndices, float dur, Color fxColor)
{
    if (offerArea == null) yield break;
    if (targetOfferIndices == null || targetOfferIndices.Count == 0) yield break;

    try
    {
        if (AudioManager.Instance)
        {
            AudioManager.Instance.PlayPlayerSkillTransformSE();
        }
    }
    catch { }

    Sprite grad = null;
    try { grad = __EnemySkill_GetOrCreateTrickGradientSprite(); } catch { grad = null; }
    if (grad == null) yield break;

    var overlays = new List<RectTransform>();

    for (int i = 0; i < targetOfferIndices.Count; i++)
    {
        int idx = targetOfferIndices[i];
        if (idx < 0) continue;
        if (idx >= offerArea.childCount) continue;

        var tileTf = offerArea.GetChild(idx);
        if (tileTf == null) continue;

        GameObject go = new GameObject("PlayerSkill_TrickFxOverlay");
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

        float maxA = 0.75f;
        try { maxA = Mathf.Clamp01(enemySkillTrickFxMaxAlpha); } catch { maxA = 0.75f; }

        Color c = fxColor;
        c.a = maxA;
        img.color = c;

        overlays.Add(rt);
    }

    float t = 0f;
    dur = Mathf.Max(0.01f, dur);

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

            rt.sizeDelta = new Vector2(0f, h * p);
        }

        yield return null;
    }

    for (int i = 0; i < overlays.Count; i++)
    {
        if (overlays[i] != null)
            Destroy(overlays[i].gameObject);
    }
}
    private List<string> LocalizeSkillPanelYakuList_Local(List<string> source)
    {
        var result = new List<string>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            string localized = LocalizeSkillPanelYakuName_Local(source[i]);
            if (!string.IsNullOrEmpty(localized))
                result.Add(localized);
        }

        return result;
    }

void Start()
{
    Debug.Log($"[SKILL_LOAD] setId='{PlayerPrefs.GetString(PrefKeySet, "")}', active='{PlayerPrefs.GetString(PrefKeyActive, "")}'");

    var resourcesSets = Resources.LoadAll<SkillSetAsset>(skillSetResourcesFolder)
                                 .Where(s => s != null)
                                 .ToArray();

    string equippedId = PlayerPrefs.GetString(PrefKeySet, "");
    string activeName = PlayerPrefs.GetString(PrefKeyActive, "");

    string normalizedActiveName = NormalizeSkillIdForStorage(activeName);
    if (!string.IsNullOrEmpty(normalizedActiveName) &&
        !string.Equals(activeName, normalizedActiveName, StringComparison.Ordinal))
    {
        activeName = normalizedActiveName;
        PlayerPrefs.SetString(PrefKeyActive, activeName);
        try { PlayerPrefs.Save(); } catch { }
    }

    SkillSetAsset defaultSet = null;

    if (resourcesSets != null && resourcesSets.Length > 0)
    {
        defaultSet = FindOwnerSkillSetBySkillId_Local(resourcesSets, CanonicalSkillIdRandomMan);
    }

    if (!defaultSet && fallbackSkillSet) defaultSet = fallbackSkillSet;

    if (!defaultSet && resourcesSets != null && resourcesSets.Length > 0) defaultSet = resourcesSets[0];
    if (!_suspendRestoredThisSession)
    {
        if (string.IsNullOrEmpty(equippedId) && defaultSet)
        {
            PlayerPrefs.SetString(PrefKeySet, defaultSet.id);
            equippedId = defaultSet.id;
        }
        if (string.IsNullOrEmpty(activeName))
        {
            const string defaultSkillId = CanonicalSkillIdRandomMan;

            if (SkillIdToEnum(defaultSkillId) != ActiveSkill.None)
            {
                PlayerPrefs.SetString(PrefKeyActive, defaultSkillId);
                activeName = defaultSkillId;
            }
            else if (defaultSet && defaultSet.activeSkills != null && defaultSet.activeSkills.Count > 0)
            {
                var e0 = defaultSet.activeSkills[0];
                if (e0 != null && !string.IsNullOrEmpty(e0.activeSkillName))
                {
                    string normalized = NormalizeSkillIdForStorage(e0.activeSkillName);
                    PlayerPrefs.SetString(PrefKeyActive, normalized);
                    activeName = normalized;
                }
            }
        }
        try { PlayerPrefs.Save(); } catch { }
    }
    else
    {
        // 中断復元のときは、GameManager.TryLoadSuspendSnapshot() がセットした保留値を優先
        if (_pendingSuspendLoadoutApply)
        {
            equippedId = _pendingSuspendSkillSetId ?? "";
            activeName = _pendingSuspendActiveSkillName ?? "";

            try
            {
                PlayerPrefs.SetString(PrefKeySet, equippedId);
                PlayerPrefs.SetString(PrefKeyActive, activeName);
                PlayerPrefs.Save();
            }
            catch { }
        }
    }

    SkillSetAsset activeOwnerSet = null;

    if (resourcesSets != null && resourcesSets.Length > 0)
    {
        activeOwnerSet = FindOwnerSkillSetBySkillId_Local(resourcesSets, activeName);
    }

    if (string.IsNullOrEmpty(equippedId) && activeOwnerSet != null && !string.IsNullOrEmpty(activeOwnerSet.id))
    {
        equippedId = activeOwnerSet.id;
        PlayerPrefs.SetString(PrefKeySet, equippedId);
        try { PlayerPrefs.Save(); } catch { }
    }

    // 装備セット解決
    _skillSet = resourcesSets.FirstOrDefault(s => s.id == equippedId);

    if (!_skillSet && activeOwnerSet != null)
    {
        _skillSet = activeOwnerSet;
        if (!string.IsNullOrEmpty(_skillSet.id))
        {
            PlayerPrefs.SetString(PrefKeySet, _skillSet.id);
            try { PlayerPrefs.Save(); } catch { }
        }
    }

    if (!_skillSet && fallbackSkillSet) _skillSet = fallbackSkillSet;
    if (!_skillSet && resourcesSets != null && resourcesSets.Length > 0)
    {
        _skillSet = resourcesSets[0];
        Debug.LogWarning($"[MP] EquippedSkillSetId='{equippedId}' not found. Fallback to first '{_skillSet.displayName}'.");
    }
    Debug.Log($"[MP] equippedId='{equippedId}', loaded='{(_skillSet ? _skillSet.id : "null")}', " +
              $"resources={(resourcesSets != null ? resourcesSets.Length : 0)}, fallback='{(fallbackSkillSet ? fallbackSkillSet.id : "null")}', " +
              $"max={(_skillSet ? _skillSet.maxMP : 0)}, start={(_skillSet ? _skillSet.startMP : 0)}");

    if (!_skillSet)
    {
        Debug.LogError("[MP] No SkillSet could be loaded. Put assets under Resources/SkillSets or assign Fallback Skill Set.");
        return;
    }

    int max = EffectiveMaxMP();

    // 中断復元なら「スナップショットのMP」をそのまま採用
    if (_suspendRestoredThisSession && _pendingSuspendLoadoutApply)
    {
        _mp = Mathf.Clamp(_pendingSuspendPlayerMP, 0, max);
    }
    else
    {
        // 通常初期化
        _mp = Mathf.Clamp(_skillSet.startMP, 0, max);

        // PF_PendingFullHeal が立っているときだけ満タン
        try
        {
            bool pendingFullHeal = false;
            try { pendingFullHeal = PlayerPrefs.GetInt("PF_PendingFullHeal", 0) == 1; } catch { }

            if (pendingFullHeal)
            {
                _mp = Mathf.Clamp(max, 0, max);
                try
                {
                    PlayerPrefs.SetInt("PF_PendingFullHeal", 0);
                    PlayerPrefs.Save();
                }
                catch { }
            }
            else if (PlayerPrefs.HasKey("Run_PlayerMP"))
            {
                int saved = PlayerPrefs.GetInt("Run_PlayerMP", -1);
                if (saved >= 0) _mp = Mathf.Clamp(saved, 0, max);
            }
        }
        catch { }
    }
if (mpSlider) mpSlider.maxValue = Mathf.Max(1, max);
UpdateMpUI();

if (btnSkill)
{
    btnSkill.onClick.RemoveAllListeners();
    btnSkill.onClick.AddListener(OnClickSkill_MP);
    Debug.Log("[SKILL_MP] wired MP handler");
}

UpdateSkillInfoUI();
UpdateRightInfoUI_Manual();

// 中断復元の保留フラグはここで消費
_pendingSuspendLoadoutApply = false;

// ★ミッションUI初期化（MissionBootstrap不要。Start()の末尾で直接呼ぶ）
try { InitMissionUI(); } catch (System.Exception e) { Debug.LogWarning("[Mission] InitMissionUI in Start() error: " + e); }
}
[Header("MP Gain Animation")]
[SerializeField] private float mpRegenAnimSeconds = 0.4f;

// ★追加：MP消費（減少）も同じ「グーっ」演出にする
[SerializeField] private float mpSpendAnimSeconds = 0.25f;

private Coroutine _mpRegenAnimCo = null;

private IEnumerator __AnimatePlayerMP_Visual_Co(int from, int to, float seconds)
{
    bool isDecrease = (to < from);

    if (isDecrease)
    {
        _mpDecreaseAnimRunning = true;
        UpdateButtons();
    }

    float dur = Mathf.Max(0.01f, seconds);
    float t = 0f;

    while (t < dur)
    {
        t += Time.deltaTime;
        float p = Mathf.Clamp01(t / dur);

        int disp = Mathf.RoundToInt(Mathf.Lerp(from, to, p));
        __UpdatePlayerMpUI_VisualOnly(disp);

        yield return null;
    }

    __UpdatePlayerMpUI_VisualOnly(to);
    UpdateMpUI();

    if (isDecrease)
    {
        _mpDecreaseAnimRunning = false;
        UpdateButtons();
    }
}
private int ComputeFinalSkillMpCost(int baseCost)
{
    var om = __Om();

    float mul = 1f - Mathf.Clamp01(om.skillMpCostDown);

    // ★ユニーク：バステト（MP消費量50%減少）
    try
    {
        if (PlayerData.IsEquippedUniqueEffect(PlayerData.UniqueOmamoriEffectKind.Bastet_MpCostHalf))
        {
            mul *= 0.5f;
        }
    }
    catch { }
    // ★特別牌：レジェンダリー⑤（次の局だけMP消費量50%減少）
    try
    {
        if (IsLegendaryHalfMpCostActive())
        {
            mul *= 0.5f;

            _legendaryHalfMpCostTriggeredThisScoring = true;
            _legendaryHalfMpCostTriggeredSourceTiles.Clear();
            _legendaryHalfMpCostTriggeredSourceTiles.AddRange(_legendaryHalfMpCostReservedSourceTiles);
        }
    }
    catch { }

    int finalCost = Mathf.RoundToInt(Mathf.Max(0, baseCost) * Mathf.Max(0f, mul));
    return Mathf.Max(0, finalCost);
}
public void TryRegenMP_TurnStart()
{
    if (_skillSet == null) return;

    var om = __Om();
    int regen = Mathf.Max(0, _skillSet.regenPerTurn);
    regen = Mathf.RoundToInt(regen * (1f + Mathf.Max(0f, om.mpRegenUp)));

    // ★ユニーク：ポセイドン（毎ターンMP回復量2倍）
    try
    {
        if (PlayerData.IsEquippedUniqueEffect(PlayerData.UniqueOmamoriEffectKind.Poseidon_MpRegenDouble))
        {
            regen = Mathf.RoundToInt(regen * 2f);
        }
    }
    catch { }

    int startMp = Mathf.Max(0, _mp);
    _mp = ClampToEffectiveMaxMP(_mp + regen);
    int endMp = Mathf.Max(0, _mp);

    _skillCastsUsedThisTurn = 0;

    // ★見た目だけ「グーっと」回復（値は既に確定済み）
    if (endMp != startMp)
    {
        try
        {
            if (_mpRegenAnimCo != null) StopCoroutine(_mpRegenAnimCo);
        }
        catch { }

        _mpRegenAnimCo = StartCoroutine(__AnimatePlayerMP_Visual_Co(startMp, endMp, mpRegenAnimSeconds));
    }
    else
    {
        UpdateMpUI();
    }

    // ★ユニーク：ルーナ（毎ターン最大HPの2%回復）
    try
    {
        if (PlayerData.IsEquippedUniqueEffect(PlayerData.UniqueOmamoriEffectKind.Luna_Heal2PctPerTurn))
        {
            int heal = Mathf.RoundToInt(Mathf.Max(1, playerMaxHP) * 0.02f);
            playerHP = Mathf.Clamp(playerHP + Mathf.Max(0, heal), 0, Mathf.Max(1, playerMaxHP));
            UpdateHpUI();
        }
    }
    catch { }

    UpdateSkillInfoUI();
    UpdateButtons();
}
public void CallOnWinRecoveredMP()
{
    if (_skillSet == null) return;

    var om = __Om();
    int regen = Mathf.Max(0, _skillSet.regenOnWin);
    // お守りによる勝利時固定加算は未実装。regen のみ反映。
    _mp = ClampToEffectiveMaxMP(_mp + regen);
    UpdateMpUI();
}

// ★ 重複していた EffectiveMaxMP() は「削除」する
//   既にファイル前半に「お守り込みの実効最大MP」版が定義されています（そちらを唯一の定義に）。

private void UpdateMpUI()
{
    // Inspector手動参照のみ（自動探索はしない）
    int maxLogic = EffectiveMaxMP();
    int effMax   = Mathf.Max(1, maxLogic);
    int cur      = Mathf.Clamp(_mp, 0, effMax);

    // 1) 旧来の（手動割り当て）Slider/Textもそのまま対応
    if (mpTMP != null) mpTMP.text = $"MP {cur}/{effMax}";
    if (mpSlider != null)
    {
        if (!Mathf.Approximately(mpSlider.maxValue, effMax))
            mpSlider.maxValue = effMax;
        mpSlider.value = cur;
    }

    // 2) HPと同じ「手動UI（Image/TMP）」も更新（GameManager.cs 内のフィールド）
    //    - playerMPTMP: 書式は playerMPConfig.textFormat（{cur}/{max}）を使用
    //    - playerMPBar: Image.Type.Filled + fillAmount を利用、色やFill設定は playerMPConfig を適用
    if (playerMPTMP)
    {
        string fmt = (playerMPConfig != null && !string.IsNullOrEmpty(playerMPConfig.textFormat))
            ? playerMPConfig.textFormat : "{cur}/{max}";
        playerMPTMP.text = fmt.Replace("{cur}", cur.ToString()).Replace("{max}", effMax.ToString());
    }
    if (playerMPBar)
    {
        // 見た目設定の適用
        if (playerMPConfig != null)
        {
            playerMPBar.type = playerMPConfig.fillType;
            if (playerMPBar.type == UnityEngine.UI.Image.Type.Filled)
            {
                playerMPBar.fillMethod = playerMPConfig.fillMethod;
                playerMPBar.fillOrigin = playerMPConfig.fillOrigin;
                playerMPBar.fillAmount = effMax > 0 ? (float)cur / effMax : 0f;
            }
            if (playerMPConfig.overrideColor) playerMPBar.color = playerMPConfig.color;
        }
        else
        {
            // 既定（横方向フィル）
            playerMPBar.type       = UnityEngine.UI.Image.Type.Filled;
            playerMPBar.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            playerMPBar.fillOrigin = 0;
            playerMPBar.fillAmount = effMax > 0 ? (float)cur / effMax : 0f;
        }
    }

    // ★ Run中のMPを保存（次の敵へ引き継ぐ）
    try
    {
        PlayerPrefs.SetInt("Run_PlayerMP", Mathf.Max(0, _mp));
        PlayerPrefs.Save();
    }
    catch {}

    // ★追加：状態異常UI（麻痺/毒）の色＆アイコンを「通常UI更新の上書き後」に必ず復元する
    try { EnemySkills_RefreshStatusEffectsUI(); } catch {}
}
private ActiveSkill SkillIdToEnum(string id)
{
    if (string.IsNullOrEmpty(id)) return ActiveSkill.None;

    id = id.Trim();

    // 旧日本語保存値・表示名との互換
    if (id == "染色師") return ActiveSkill.RandomMan;
    if (id == "書家")   return ActiveSkill.EnhanceHand;

    // 今後の内部標準ID
    if (string.Equals(id, CanonicalSkillIdRandomMan, StringComparison.OrdinalIgnoreCase))
        return ActiveSkill.RandomMan;
    if (string.Equals(id, CanonicalSkillIdEnhanceHand, StringComparison.OrdinalIgnoreCase))
        return ActiveSkill.EnhanceHand;

    // 念のため旧名/内部名も許容
    if (string.Equals(id, "RandomHonor", StringComparison.OrdinalIgnoreCase))
        return ActiveSkill.RandomHonor;

    // それ以外は従来通り、列挙名として解釈
    if (Enum.TryParse(id, true, out ActiveSkill e)) return e;
    return ActiveSkill.None;
}
private ActiveSkill ResolveActiveSkillForMP()
{
    // 1) ユーザーが装備画面で保存した SkillID を最優先で採用
    var prefStr = PlayerPrefs.GetString(PrefKeyActive, "");
    var prefEnum = SkillIdToEnum(prefStr);
    if (!string.IsNullOrEmpty(prefStr) && prefEnum != ActiveSkill.None)
    {
        string normalizedPrefStr = NormalizeSkillIdForStorage(prefStr);

        if (!string.Equals(prefStr, normalizedPrefStr, StringComparison.Ordinal))
        {
            PlayerPrefs.SetString(PrefKeyActive, normalizedPrefStr);
            PlayerPrefs.Save();
        }

        // 参考ログ：現在のセットに含まれていない場合は警告だけ（※一度だけ出す）
        if (!(_skillSet != null && _skillSet.activeSkills != null &&
              _skillSet.activeSkills.Any(a =>
                 !string.IsNullOrEmpty(a.activeSkillName) &&
                 GetSkillIdCandidates(prefEnum)
                     .Any(id => string.Equals(a.activeSkillName, id, StringComparison.OrdinalIgnoreCase)))))
        {
#if UNITY_EDITOR
            if (!_loggedMismatchedActiveSkill)
            {
                Debug.LogWarning($"[MP] EquippedActiveSkill '{prefStr}' is not found in current SkillSet '{_skillSet?.displayName}'. Using it anyway.");
                _loggedMismatchedActiveSkill = true;
            }
#endif
        }
        return prefEnum;
    }

    // 2) 保存が空の場合は、必ず内部標準IDをデフォルトとして保存・採用する
    const string defaultSkillId = CanonicalSkillIdRandomMan;
    var defaultEnum = SkillIdToEnum(defaultSkillId);
    if (defaultEnum != ActiveSkill.None)
    {
        PlayerPrefs.SetString(PrefKeyActive, defaultSkillId);
        PlayerPrefs.Save();
        Debug.Log($"[MP] Active skill initialized to default: {defaultEnum} (id='{defaultSkillId}')");
        return defaultEnum;
    }

    // 3) 念のためのフォールバック：それでもダメなら SkillSet 先頭
    if (_skillSet != null && _skillSet.activeSkills != null && _skillSet.activeSkills.Count > 0)
    {
        var firstName = _skillSet.activeSkills[0].activeSkillName ?? "";
        var firstEnum = SkillIdToEnum(firstName);
        if (firstEnum != ActiveSkill.None)
        {
            string normalizedFirstName = NormalizeSkillIdForStorage(firstName);
            PlayerPrefs.SetString(PrefKeyActive, normalizedFirstName);
            PlayerPrefs.Save();
            Debug.Log($"[MP] Active skill initialized to first of set: {firstEnum} (id='{normalizedFirstName}')");
            return firstEnum;
        }
    }

    return ActiveSkill.None;
}
private int GetMaxSkillCastsThisTurn()
{
    int baseLimit = Mathf.Max(1, maxSkillCastsPerTurn);
    int extraLegendary = GetLegendaryExtraSkillCasts();
    int runExtra = 0; try { runExtra = Mathf.Max(0, PlayerPrefs.GetInt("Run_SkillCastsBonus", 0)); } catch { }

    int uniqueExtra = 0;
    try
    {
        if (PlayerData.IsEquippedUniqueEffect(PlayerData.UniqueOmamoriEffectKind.Freyja_SkillCastsPlus2))
        {
            uniqueExtra = 2;
        }
    }
    catch { uniqueExtra = 0; }

    return Mathf.Max(1, baseLimit + extraLegendary + runExtra + uniqueExtra);
}


// 将来の拡張フック（今は0を返す）
// 例：equippedCharm や runItemIds に応じて +1 する、などを実装予定。
private int GetLegendaryExtraSkillCasts()
{
    // if (equippedCharm && equippedCharm.Has("ExtraSkillCast")) return 1;
    // if (HasRunItem("SkillCast+1")) return 1;
    return 0;
}
private bool TryGetActiveSkillMpCost(ActiveSkill s, out int cost)
{
    cost = 0;
    if (s == ActiveSkill.None) return false;

    // まず装備中セット(_skillSet)の activeSkills から探す（ここが一次ソース）
    SkillSetAsset.SkillEntry entry = null;
    if (_skillSet != null && _skillSet.activeSkills != null)
    {
        string skillName = s.ToString();
        var candidates = GetSkillIdCandidates(s);

        entry = _skillSet.activeSkills.FirstOrDefault(a =>
            a != null &&
            !string.IsNullOrEmpty(a.activeSkillName) &&
            (string.Equals(a.activeSkillName, skillName, StringComparison.OrdinalIgnoreCase) ||
             candidates.Any(id => string.Equals(a.activeSkillName, id, StringComparison.OrdinalIgnoreCase))));

        if (entry == null)
        {
            entry = _skillSet.activeSkills.FirstOrDefault(a =>
                a != null &&
                !string.IsNullOrEmpty(a.displayName) &&
                (string.Equals(a.displayName, skillName, StringComparison.OrdinalIgnoreCase) ||
                 candidates.Any(id => string.Equals(a.displayName, id, StringComparison.OrdinalIgnoreCase))));
        }
    }

    if (entry == null)
    {
Debug.LogWarning($"[MP] mpCost not found in equipped set '{(_skillSet ? _skillSet.id : "null")}' for active skill '{s}'. Treating cost=0.");
        return false;
    }

    cost = Mathf.Max(0, entry.mpCost);
    return true;
}
private void OnClickSkill_MP()
{
    if (_tutorialRunning) return;
    __RecoverStalePlayerSkillBusyFlags();

    if (EnemySkills_IsPlayerParalyzed())
    {
        statusTMP?.SetText(GetSkillMpFixedText_Local("skill_paralyzed_cannot_use"));
        return;
    }
    if (_skillSet == null)
    {
        statusTMP?.SetText(GetSkillMpFixedText_Local("skill_not_equipped"));
        return;
    }
    var perTurnLimit = Mathf.Max(0, GetMaxSkillCastsThisTurn()); // 既定は [SerializeField] maxSkillCastsPerTurn=1
    if (perTurnLimit > 0 && _skillCastsUsedThisTurn >= perTurnLimit)
    {
        statusTMP?.SetText(GetSkillMpFixedText_Local("skill_turn_limit_reached"));
        return;
    }

    // アクティブスキルを現在のSkillSetから判定し、その mpCost（基礎）を取得
    var active = ResolveActiveSkillForMP();
    int baseCost;
    if (!TryGetActiveSkillMpCost(active, out baseCost))
    {
        baseCost = 0; // セットに見つからなければ0消費
    }

    // ★重要：最終コストは必ず共通関数を通す（お守り減少 + バステト半減）
    int finalCost = ComputeFinalSkillMpCost(baseCost);

    if (_mp < finalCost)
    {
        statusTMP?.SetText(GetSkillMpFixedText_Local("skill_not_enough_mp"));
        return;
    }
    // ★ここが重要：効果が「成功した時だけ」MPを支払う
    _lastSkillApplied = false;

TryInvokeEquippedSkill();

if (!_lastSkillApplied)
{
    // 失敗（選択不足、局内回数制限、想定外の状態など）
    // MPは減らさない／回数も消費しない
    statusTMP?.SetText(GetSkillMpFixedText_Local("skill_activation_failed_invalid_target"));
    return;
}

// 成功したのでMP消費
int startMp = Mathf.Max(0, _mp);
_mp = Mathf.Max(0, _mp - finalCost);
int endMp = Mathf.Max(0, _mp);
// ★見た目は「グーっ」と減る（値は既に確定済み）
if (endMp != startMp)
{
    try
    {
        if (_mpRegenAnimCo != null) StopCoroutine(_mpRegenAnimCo);
    }
    catch { }

    _mpRegenAnimCo = StartCoroutine(__AnimatePlayerMP_Visual_Co(startMp, endMp, mpSpendAnimSeconds));
}
else
{
    UpdateMpUI();
}

_skillCastsUsedThisTurn++;
UpdateSkillInfoUI();
UpdateButtons();
statusTMP?.SetText(GetSkillMpFixedText_Local("skill_activated"));
}
private void RandomMan()
{
    _lastSkillApplied = false;

    // 手牌1枚選択が必要
    int selIdx = (selHand != null && selHand.Count == 1) ? selHand.First() : -1;
    if (selIdx < 0)
    {
        statusTMP?.SetText(GetSkillMpFixedText_Local("skill_select_hand_target"));
        return;
    }
    if (hand == null || selIdx >= hand.Count)
    {
        statusTMP?.SetText(GetSkillMpFixedText_Local("skill_invalid_selection"));
        return;
    }
    bool hasHadesUnique = false;
    try
    {
        hasHadesUnique = PlayerData.IsEquippedUniqueEffect(PlayerData.UniqueOmamoriEffectKind.Hades_DyeMaster);
    }
    catch { hasHadesUnique = false; }

    string srcId = hand[selIdx];
    bool selectedIsNumberTile = TryParseSuitNum(srcId, out var srcSuit, out var srcNum);
    bool selectedIsSuitTile = selectedIsNumberTile && srcSuit < 3;

    string newId = null;

    if (hasHadesUnique)
    {
        int[] counts = new int[3];

        for (int i = 0; i < hand.Count; i++)
        {
            if (i == selIdx) continue;

            if (!TryParseSuitNum(hand[i], out var suit, out var num)) continue;
            if (suit >= 3) continue;

            counts[suit]++;
        }

        int bestSuit = 0;
        int bestCount = counts[0];

        for (int s = 1; s < 3; s++)
        {
            if (counts[s] > bestCount)
            {
                bestCount = counts[s];
                bestSuit = s;
            }
        }

        string suitName = (bestSuit == 0) ? "Man" : (bestSuit == 1) ? "Pin" : "Sou";

        if (selectedIsSuitTile)
        {
            // 神器通常仕様：
            // 選択した数牌 → 手牌で最も多い色の同数字牌
            newId = suitName + srcNum.ToString();
        }
        else
        {
            // 神器装備中でも、字牌選択時は通常効果扱い
            // → 手牌で最も多い色のランダムな数牌
            newId = suitName + rng.Next(1, 10).ToString();
        }
    }
    else
    {
        // 通常の染色師：
        // 数牌でも字牌でも発動可能
        // → 手牌で最も多い色のランダムな数牌
        string suit = GetMajorSuitExcludingIndex(selIdx);
        newId = suit + rng.Next(1, 10);
    }
    if (string.IsNullOrEmpty(newId))
    {
        statusTMP?.SetText(GetSkillMpFixedText_Local("skill_apply_failed"));
        return;
    }
    _lastSkillApplied = true;

    StartCoroutine(__PlayerSkill_ConvertOneTile_WithCutinAndMagicFx_Co(
        selIdx,
        newId,
        dyeMasterTransformFxColor
    ));
}
private void RandomHonor()
{
    _lastSkillApplied = false;

    int selIdx = (selHand != null && selHand.Count == 1) ? selHand.First() : -1;
    if (selIdx < 0)
    {
        statusTMP?.SetText(GetSkillMpFixedText_Local("skill_select_hand_target"));
        return;
    }
    if (hand == null || selIdx >= hand.Count)
    {
        statusTMP?.SetText(GetSkillMpFixedText_Local("skill_invalid_selection"));
        return;
    }

    string[] honors = { "East","South","West","North","White","Green","Red" };
    string newId = honors[rng.Next(0, honors.Length)];

    _lastSkillApplied = true;

    StartCoroutine(__PlayerSkill_ConvertOneTile_WithCutinAndMagicFx_Co(
        selIdx,
        newId,
        calligrapherTransformFxColor
    ));
}
private void EnhanceHand()
{
    _lastSkillApplied = false;

    int selIdx = (selHand != null && selHand.Count == 1) ? selHand.First() : -1;
    if (selIdx < 0)
    {
        statusTMP?.SetText(GetSkillMpFixedText_Local("skill_select_hand_target"));
        return;
    }
    if (hand == null || selIdx >= hand.Count)
    {
        statusTMP?.SetText(GetSkillMpFixedText_Local("skill_invalid_selection"));
        return;
    }
    if (!TryParseSuitNum(hand[selIdx], out var srcSuit, out var srcNum))
    {
        statusTMP?.SetText(GetSkillMpFixedText_Local("skill_select_number_for_calligrapher"));
        return;
    }
    if (srcSuit >= 3)
    {
        statusTMP?.SetText(GetSkillMpFixedText_Local("skill_select_number_for_calligrapher"));
        return;
    }

    bool hasHadesUnique = false;
    try
    {
        hasHadesUnique = PlayerData.IsEquippedUniqueEffect(PlayerData.UniqueOmamoriEffectKind.Hades_Calligrapher);
    }
    catch { hasHadesUnique = false; }

    string newId = null;

    if (hasHadesUnique)
    {
        string[] honors = { "East", "South", "West", "North", "White", "Green", "Red" };
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < honors.Length; i++)
        {
            counts[honors[i]] = 0;
        }

        for (int i = 0; i < hand.Count; i++)
        {
            if (i == selIdx) continue;

            string id = hand[i];
            if (string.IsNullOrEmpty(id)) continue;

            bool isNumberTile = TryParseSuitNum(id, out var suit, out var num);
            if (isNumberTile && suit < 3) continue;

            for (int h = 0; h < honors.Length; h++)
            {
                if (string.Equals(id, honors[h], StringComparison.OrdinalIgnoreCase))
                {
                    counts[honors[h]]++;
                    break;
                }
            }
        }

        string bestHonor = honors[0];
        int bestCount = counts[bestHonor];

        for (int i = 1; i < honors.Length; i++)
        {
            string cand = honors[i];
            int c = counts[cand];
            if (c > bestCount)
            {
                bestCount = c;
                bestHonor = cand;
            }
        }

        newId = bestHonor;
    }
    else
    {
        string suit =
            (srcSuit == 0) ? "Man" :
            (srcSuit == 1) ? "Pin" :
            "Sou";

        newId = suit + "5";
    }
    if (string.IsNullOrEmpty(newId))
    {
        statusTMP?.SetText(GetSkillMpFixedText_Local("skill_apply_failed"));
        return;
    }
    _lastSkillApplied = true;

    StartCoroutine(__PlayerSkill_ConvertOneTile_WithCutinAndMagicFx_Co(
        selIdx,
        newId,
        calligrapherTransformFxColor
    ));
}
private IEnumerator __PlayerSkill_ConvertOneTile_WithCutinAndMagicFx_Co(int selIdx, string newId, Color fxColor)
{
    _playerSkillTransformRunning = true;
    UpdateButtons();

    try
    {
        // ここから演出終了まで、操作を全停止
        try { EnableAllHandButtons(false); } catch { }
        try { EnableAllOfferButtons(false); } catch { }

        // ★追加：カットインと同時に「選択（牌が上にずれる演出）」を解除する
        try
        {
            if (selHand != null) selHand.Clear();
            if (selOffer != null) selOffer.Clear();

            RebuildRaiseOverlays(handArea, selHand, hand);
            RebuildRaiseOverlays(offerArea, selOffer, offers);
        }
        catch { }

        // カットイン開始
   // カットイン開始
try { StartPlayerSkillCutin(GetActiveSkillActionNameSafe(ResolveActiveSkillForMP())); } catch { }

        // カットイン終了待ち
        while (_playerSkillCutinRunning)
            yield return null;

        // レイアウト確定（細工と同じ保険）
        try
        {
            if (handArea != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(handArea);
            }
        }
        catch { }

        // 魔法エフェクト（敵スキル「細工」と同様の伸びるグラデ）
        float fxDur = 0.5f;
        try { fxDur = Mathf.Max(0.01f, enemySkillTrickFxSeconds); } catch { fxDur = 0.5f; }
        yield return __PlayerSkill_TrickMagicFx_OneTile_Co(selIdx, fxDur, fxColor);

        bool ok = ReplaceHandAt(selIdx, newId);
        if (!ok)
        {
            statusTMP?.SetText(GetSkillMpFixedText_Local("skill_apply_failed"));
        }
        else
        {
            if (phase == Phase.Offer)
            {
                _afterSkillNoHandDiscardOnce = true;
                selHand.Clear();
                selOffer.Clear();
                RebuildRaiseOverlays(handArea, selHand, hand);
                RebuildRaiseOverlays(offerArea, selOffer, offers);
            }
        }
    }
    finally
    {
        // ★重要：途中で例外や中断があっても、必ず操作を戻す
        _playerSkillTransformRunning = false;

        try { EnableAllHandButtons(true); } catch { }
        try { EnableAllOfferButtons(true); } catch { }

        UpdateButtons();
    }
}
private string BuildSkillActionNameText()
{
    var active = ResolveActiveSkillForMP();

    if (_skillSet == null || active == ActiveSkill.None)
        return "ー";

    string actionName = GetActiveSkillActionNameSafe(active);
    if (string.IsNullOrEmpty(actionName))
        return "ー";

    return actionName;
}
private IEnumerator __PlayerSkill_TrickMagicFx_OneTile_Co(int targetHandIndex, float dur, Color fxColor)
{
    if (handArea == null) yield break;
    if (targetHandIndex < 0) yield break;
    if (targetHandIndex >= handArea.childCount) yield break;

    // ★追加：魔法演出の開始と同時にSE
    try
    {
        if (AudioManager.Instance)
        {
            AudioManager.Instance.PlayPlayerSkillTransformSE();
        }
    }
    catch { }

    Sprite grad = null;
    try { grad = __EnemySkill_GetOrCreateTrickGradientSprite(); } catch { grad = null; }
    if (grad == null) yield break;

    var tileTf = handArea.GetChild(targetHandIndex);
    if (tileTf == null) yield break;

    GameObject go = new GameObject("PlayerSkill_TrickFxOverlay");
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

    float maxA = 0.75f;
    try { maxA = Mathf.Clamp01(enemySkillTrickFxMaxAlpha); } catch { maxA = 0.75f; }

    Color c = fxColor;
    c.a = maxA;
    img.color = c;

    float t = 0f;
    dur = Mathf.Max(0.01f, dur);

    while (t < dur)
    {
        t += Time.deltaTime;
        float p = Mathf.Clamp01(t / dur);

        float h = 0f;
        var parentRt = tileTf as RectTransform;
        try { h = (parentRt != null) ? parentRt.rect.height : 0f; } catch { h = 0f; }
        if (h <= 0.001f) h = 120f;

        rt.sizeDelta = new Vector2(0f, Mathf.Lerp(0f, h, p));
        yield return null;
    }

    try { GameObject.Destroy(go); } catch { }
}
private void TryInvokeEquippedSkill()
{
    var active = ResolveActiveSkillForMP();

    switch (active)
    {
        case ActiveSkill.RandomMan:
            RandomMan();
            return;

        case ActiveSkill.RandomHonor:
            RandomHonor();
            return;

        case ActiveSkill.EnhanceHand:
            EnhanceHand();
            return;

        case ActiveSkill.Capitalist:
        {
            _lastSkillApplied = false;
            bool ok = ApplyCapitalistSkill();
            if (ok)
            {
                _lastSkillApplied = true;
            }
            return;
        }

        default:
            OnClickSkill();
            return;
    }
}
private void UpdateSkillInfoUI()
{
    if (skillInfoTMP)
    {
        skillInfoTMP.text = BuildSkillInfoText();
    }

    UpdateRightInfoUI_Manual();
}
private string BuildSkillInfoText()
{
    var active = ResolveActiveSkillForMP();

    string skillName = GetGameFixedText_Local("rightinfo_no_skill_equipped");

    if (_skillSet != null && active != ActiveSkill.None)
    {
        skillName = GetActiveSkillDisplayName(active);
        if (string.IsNullOrEmpty(skillName))
        {
            skillName = active.ToString();
        }
    }

    return skillName;
}
int RecoverMpByShunYakuIfAny(List<string> yaku, int baseScore)
{
    if (yaku == null || yaku.Count == 0 || baseScore <= 0)
        return 0;

    var (_geList, shList, _iyList, hostSet) = GetCurrentSkillTraitYakuForScoring();

    if (shList == null || shList.Count == 0 || hostSet == null)
        return 0;

    var hitSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    for (int i = 0; i < yaku.Count; i++)
    {
        string norm = NormalizeTraitJudgeYakuName_Local(yaku[i]);
        if (!string.IsNullOrEmpty(norm))
            hitSet.Add(norm);
    }

    string activeSkillName = "";
    try
    {
        var active = ResolveActiveSkillForMP();
        activeSkillName = active.ToString();
    }
    catch
    {
        activeSkillName = "";
    }

    float deltaPerLevel = 0f;
    try
    {
        deltaPerLevel = Mathf.Max(0f, GetTraitUpgradeDeltaFromPrefs(SkillSetAsset.Trait.Shun, hostSet));
    }
    catch
    {
        deltaPerLevel = 0f;
    }

    float totalPct = 0f;
    var countedTraitNormSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var keyRaw in shList)
    {
        string key = (keyRaw ?? "").Trim();
        if (string.IsNullOrEmpty(key))
            continue;

        string traitNorm = NormalizeTraitJudgeYakuName_Local(key);
        if (string.IsNullOrEmpty(traitNorm))
            continue;

        if (!hitSet.Contains(traitNorm))
            continue;

        if (countedTraitNormSet.Contains(traitNorm))
            continue;

        int effectiveLv = GetTraitEffectiveLevelForScoring(hostSet, activeSkillName, SkillSetAsset.Trait.Shun, key);
        if (effectiveLv <= 0)
            continue;

        float pct = 0f;

        if (hostSet.traitMap != null)
        {
            var entry = hostSet.traitMap.FirstOrDefault(e =>
                e != null &&
                e.trait == SkillSetAsset.Trait.Shun &&
                !string.IsNullOrWhiteSpace(e.yakuName) &&
                string.Equals(
                    NormalizeTraitJudgeYakuName_Local(e.yakuName),
                    traitNorm,
                    StringComparison.OrdinalIgnoreCase));

            if (entry != null)
            {
                int di = Mathf.Clamp((int)entry.difficulty, 0, hostSet.shunMpPctByDiff.Length - 1);
                pct = Mathf.Max(0f, hostSet.shunMpPctByDiff[di]);
            }
            else if (hostSet.shunMpPctByDiff != null && hostSet.shunMpPctByDiff.Length > 0)
            {
                pct = Mathf.Max(0f, hostSet.shunMpPctByDiff[0]);
            }
        }

        if (deltaPerLevel > 0f)
        {
            int deltaLv = Mathf.Max(0, effectiveLv - 1);
            pct += deltaPerLevel * deltaLv;
        }

        totalPct += Mathf.Max(0f, pct);
        countedTraitNormSet.Add(traitNorm);
    }

    if (totalPct <= 0f)
        return 0;

    int add = Mathf.RoundToInt(baseScore * totalPct);
    if (add <= 0)
        return 0;

    _mp = ClampToEffectiveMaxMP(_mp + add);
    UpdateMpUI();
    return add;
}
}
