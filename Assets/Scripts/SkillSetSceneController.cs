using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class SkillSetSceneController : MonoBehaviour
{
    [Header("Scene / Prefs")]
    [SerializeField] private string menuSceneName = "Menu";           // メニューシーン名
    [SerializeField] private string prefEquippedKey = "EquippedSkillSetId"; // 装備中 SkillSet の ID
[Header("Skill Equip UI")]
[SerializeField] private string skillSetResourcesFolder = "SkillSets";  // Resources/SkillSets/*.asset
[SerializeField] private SkillSetAsset fallbackSkillSet;                // 万が一用のフォールバック

[SerializeField] private TextMeshProUGUI skillNameTMP;            // ★ 職業名（DisplayName）表示用
[SerializeField] private TextMeshProUGUI skillActionNameTMP;      // ★ 発動能力名表示用
[SerializeField] private Image skillIconImage;                    // ★ スキルに応じた画像表示用 Image
[SerializeField] private TextMeshProUGUI skillDescriptionTMP;     // ★ Asset の Identity.Description を表示する TMP
[SerializeField] private TextMeshProUGUI gekiYakuTMP;             // ★ 「撃」役だけ（テキストに「撃」は入れない）
[SerializeField] private TextMeshProUGUI shunYakuTMP;             // ★ 「瞬」役だけ
[SerializeField] private TextMeshProUGUI iyuYakuTMP;              // ★ 「癒」役だけ
[Header("Skill Unlock Restriction")]
[SerializeField] private bool restrictCalligrapherUntilFreyjaDefeat = true;
[SerializeField] private bool restrictCapitalistUntilZeusDefeat = true;

[Header("Skill Unlock UI")]
[SerializeField] private Button dyeMasterEquipButton;
[SerializeField] private Button calligrapherEquipButton;
[SerializeField] private Button capitalistEquipButton;

[SerializeField] private GameObject dyeMasterUnavailableIcon;
[SerializeField] private GameObject calligrapherUnavailableIcon;
[SerializeField] private GameObject capitalistUnavailableIcon;

    // PlayerPrefs のキー（GameManager_SkillMP_Addon.cs と合わせる）
    private const string PrefKeyActiveSkill = "EquippedActiveSkill";
private static string NormalizeActiveSkillId_Local(string id)
{
    if (string.IsNullOrWhiteSpace(id)) return "";

    id = id.Trim();

    if (string.IsNullOrEmpty(id)) return "";

    SkillSetAsset[] loadedSets = null;
    try
    {
        loadedSets = Resources.LoadAll<SkillSetAsset>("SkillSets");
    }
    catch
    {
        loadedSets = null;
    }

    if (loadedSets != null)
    {
        foreach (var set in loadedSets)
        {
            if (set == null || set.activeSkills == null) continue;

            foreach (var entry in set.activeSkills)
            {
                if (entry == null) continue;

                string canonical = entry.activeSkillName ?? "";

                if (!string.IsNullOrEmpty(canonical) &&
                    string.Equals(id, canonical, StringComparison.OrdinalIgnoreCase))
                {
                    return canonical;
                }

                if (!string.IsNullOrEmpty(entry.displayName) &&
                    string.Equals(id, entry.displayName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return canonical;
                }

                if (!string.IsNullOrEmpty(entry.displayNameEnglish) &&
                    string.Equals(id, entry.displayNameEnglish.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return canonical;
                }

                if (!string.IsNullOrEmpty(entry.displayNameChineseSimplified) &&
                    string.Equals(id, entry.displayNameChineseSimplified.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return canonical;
                }
            }
        }
    }

    return id;
}
private void SyncSkillRestrictionFlagsToPrefs()
{
    PlayerData.SetSkillRestrictionEnabled("EnhanceHand", restrictCalligrapherUntilFreyjaDefeat);
    PlayerData.SetSkillRestrictionEnabled("Capitalist", restrictCapitalistUntilZeusDefeat);
}
private static string GetLocalizedSkillActionName_Local(SkillSetAsset.SkillEntry activeEntry, string activeSkillName)
{
    if (activeEntry != null)
    {
        string localized = activeEntry.GetLocalizedActionName();
        if (!string.IsNullOrEmpty(localized))
            return localized;
    }

    if (string.IsNullOrWhiteSpace(activeSkillName))
        return string.Empty;

    string key = activeSkillName.Trim();
    string localizedFallback = LocalizationManager.ActiveSkillAction(key);
    string unresolved = "active_skill_action." + key;

    if (string.Equals(localizedFallback, unresolved, StringComparison.Ordinal))
        return string.Empty;

    return localizedFallback;
}
private void ApplySkillLockState(Button button, GameObject unavailableIcon, string skillKey)
{
    bool usable = PlayerData.IsSkillUsable(skillKey);

    if (button)
        button.interactable = usable;

    if (unavailableIcon)
        unavailableIcon.SetActive(!usable);
}

private void RefreshSkillUnlockButtons()
{
    ApplySkillLockState(dyeMasterEquipButton, dyeMasterUnavailableIcon, "RandomMan");
    ApplySkillLockState(calligrapherEquipButton, calligrapherUnavailableIcon, "EnhanceHand");
    ApplySkillLockState(capitalistEquipButton, capitalistUnavailableIcon, "Capitalist");
}

private string NormalizeToUsableSkill_Local(string active)
{
    string normalized = NormalizeActiveSkillId_Local(active);

    if (string.IsNullOrWhiteSpace(normalized))
        return "RandomMan";

    if (PlayerData.IsSkillUsable(normalized))
        return normalized;

    return "RandomMan";
}
private SkillSetAsset FindOwnerSetByActiveSkillId_Local(string activeSkillId)
{
    if (string.IsNullOrWhiteSpace(activeSkillId))
        return null;

    if (_loadedSets == null || _loadedSets.Length == 0)
        LoadSkillSets();

    string normalized = NormalizeActiveSkillId_Local(activeSkillId);

    if (_loadedSets != null)
    {
        foreach (var set in _loadedSets)
        {
            if (set == null || set.activeSkills == null)
                continue;

            foreach (var entry in set.activeSkills)
            {
                if (entry == null)
                    continue;

                string entryId = NormalizeActiveSkillId_Local(entry.activeSkillName);
                if (!string.IsNullOrEmpty(entryId) &&
                    string.Equals(entryId, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return set;
                }
            }
        }
    }

    return null;
}
public void OnClickEquipSkill(string rawSkillId)
{
    if (_loadedSets == null || _loadedSets.Length == 0)
        LoadSkillSets();

    SyncSkillRestrictionFlagsToPrefs();

    string normalized = NormalizeActiveSkillId_Local(rawSkillId);
    if (string.IsNullOrEmpty(normalized))
        return;

    if (!PlayerData.IsSkillUsable(normalized))
    {
        RefreshSkillUnlockButtons();
        return;
    }

    SkillSetAsset ownerSet = FindOwnerSetByActiveSkillId_Local(normalized);
    if (ownerSet == null)
        return;

    _selectedActiveSkillName = normalized;
    _selectedOwnerSet = ownerSet;
    _currentSet = ownerSet;

    PlayerPrefs.SetString(prefEquippedKey, ownerSet.id);
    PlayerPrefs.SetString(PrefKeyActiveSkill, normalized);
    PlayerPrefs.Save();

    RefreshSkillUnlockButtons();

    // ここでもUIを再読込しない。
}
public void OnClickEquipCurrentSkill()
{
    SyncSkillRestrictionFlagsToPrefs();

    string active = NormalizeActiveSkillId_Local(_selectedActiveSkillName);
    SkillSetAsset ownerSet = _selectedOwnerSet;

    if (string.IsNullOrEmpty(active))
        return;

    if (!PlayerData.IsSkillUsable(active))
    {
        RefreshSkillUnlockButtons();
        return;
    }

    if (ownerSet == null)
        ownerSet = FindOwnerSetByActiveSkillId_Local(active);

    if (ownerSet == null)
        return;

    _currentSet = ownerSet;

    PlayerPrefs.SetString(prefEquippedKey, ownerSet.id);
    PlayerPrefs.SetString(PrefKeyActiveSkill, active);
    PlayerPrefs.Save();

    RefreshSkillUnlockButtons();

    // ここではUIを再読込しない。
    // いま表示中の翻訳済みテキストをそのまま維持する。
}
private void DisableLocalizedTextUIForDrivenLabel(TMP_Text tmp)
{
    if (tmp == null) return;

    var localized = tmp.GetComponent<LocalizedTextUI>();
    if (localized != null)
    {
        localized.enabled = false;
    }
}
private void RefreshSkillUIAfterEquipClick_Local()
{
    DisableLocalizedTextUIDriversForSkillScene();

    string active = NormalizeActiveSkillId_Local(_selectedActiveSkillName);
    SkillSetAsset ownerSet = _selectedOwnerSet;

    if (string.IsNullOrEmpty(active))
        return;

    if (ownerSet == null)
        ownerSet = FindOwnerSetByActiveSkillId_Local(active);

    if (ownerSet == null)
        return;

    _currentSet = ownerSet;

    _lastEquippedId = ownerSet.id;
    _lastActiveSkillName = active;

    var loc = LocalizationManager.Instance;
    _lastLanguage = (loc != null) ? loc.CurrentLanguage : LocalizationManager.Language.Japanese;

    UpdateSkillUIForSelectedSkill_Local(active, ownerSet);
}
private void DisableLocalizedTextUIDriversForSkillScene()
{
    DisableLocalizedTextUIForDrivenLabel(skillNameTMP);
    DisableLocalizedTextUIForDrivenLabel(skillDescriptionTMP);
    DisableLocalizedTextUIForDrivenLabel(gekiYakuTMP);
    DisableLocalizedTextUIForDrivenLabel(shunYakuTMP);
    DisableLocalizedTextUIForDrivenLabel(iyuYakuTMP);
}
    private static string GetSkillSceneFixedText_Local(string key)
    {
        return LocalizationManager.Fixed(key);
    }

private static string CanonSkillYakuName_Local(string s)
{
    if (string.IsNullOrEmpty(s)) return "";
    s = s.Trim().Replace("　", " ");
    s = s.Replace('（', '(').Replace('）', ')');
    int p0 = s.IndexOf('(');
    if (p0 >= 0) s = s.Substring(0, p0);
    s = s.Trim();

    if (s == "風牌") return "役牌";
    if (s.StartsWith("風牌")) return "役牌";
    if (s == "役牌") return "役牌";
    if (s.StartsWith("役牌")) return "役牌";
    if (s == "白" || s == "發" || s == "発" || s == "中") return "役牌";

    return s;
}
    private static string NormalizeSkillYakuKey_Local(string yakuName)
    {
        string s = CanonSkillYakuName_Local(yakuName);
        if (string.IsNullOrEmpty(s)) return "";

        if (string.Equals(s, "KOKUSHI", StringComparison.OrdinalIgnoreCase) || s == "国士無双" || s == "国士无双" || s.Equals("Kokushi", StringComparison.OrdinalIgnoreCase) || s.Equals("Kokushi Musou", StringComparison.OrdinalIgnoreCase)) return "KOKUSHI";
        if (string.Equals(s, "CHIITOITSU", StringComparison.OrdinalIgnoreCase) || s == "七対子") return "CHIITOITSU";
        if (string.Equals(s, "MENZEN_TSUMO", StringComparison.OrdinalIgnoreCase) || s == "門前清自摸和" || s == "自摸" || s == "ツモ") return "MENZEN_TSUMO";
        if (string.Equals(s, "TANYAO", StringComparison.OrdinalIgnoreCase) || s == "タンヤオ" || s == "断么九" || s == "断幺九") return "TANYAO";
        if (string.Equals(s, "PINFU", StringComparison.OrdinalIgnoreCase) || s == "平和") return "PINFU";
        if (string.Equals(s, "YAKUHAI", StringComparison.OrdinalIgnoreCase) || s == "役牌") return "YAKUHAI";
        if (string.Equals(s, "IIPEIKOU", StringComparison.OrdinalIgnoreCase) || s == "一盃口") return "IIPEIKOU";
        if (string.Equals(s, "RYANPEIKOU", StringComparison.OrdinalIgnoreCase) || s == "二盃口") return "RYANPEIKOU";
        if (string.Equals(s, "SANSHOKU_DOUJUN", StringComparison.OrdinalIgnoreCase) || s == "三色同順") return "SANSHOKU_DOUJUN";
        if (string.Equals(s, "ITTSU", StringComparison.OrdinalIgnoreCase) || s == "一気通貫") return "ITTSU";
        if (string.Equals(s, "CHANTA", StringComparison.OrdinalIgnoreCase) || s == "チャンタ") return "CHANTA";
        if (string.Equals(s, "JUNCHAN", StringComparison.OrdinalIgnoreCase) || s == "純チャン") return "JUNCHAN";
        if (string.Equals(s, "TOITOI", StringComparison.OrdinalIgnoreCase) || s == "対々和") return "TOITOI";
        if (string.Equals(s, "SANANKOU", StringComparison.OrdinalIgnoreCase) || s == "三暗刻") return "SANANKOU";
        if (string.Equals(s, "SANKANTSU", StringComparison.OrdinalIgnoreCase) || s == "三カンツ" || s == "三槓子") return "SANKANTSU";
        if (string.Equals(s, "SANSHOKU_DOUKOU", StringComparison.OrdinalIgnoreCase) || s == "三色同刻") return "SANSHOKU_DOUKOU";
        if (string.Equals(s, "SHOUSANGEN", StringComparison.OrdinalIgnoreCase) || s == "小三元") return "SHOUSANGEN";
        if (string.Equals(s, "HONROUTOU", StringComparison.OrdinalIgnoreCase) || s == "混老頭") return "HONROUTOU";
        if (string.Equals(s, "HONITSU", StringComparison.OrdinalIgnoreCase) || s == "混一色") return "HONITSU";
        if (string.Equals(s, "CHINITSU", StringComparison.OrdinalIgnoreCase) || s == "清一色") return "CHINITSU";

        if (string.Equals(s, "CHUUREN_POUTOU", StringComparison.OrdinalIgnoreCase) || s == "九蓮宝燈") return "CHUUREN_POUTOU";
        if (string.Equals(s, "DAISANGEN", StringComparison.OrdinalIgnoreCase) || s == "大三元") return "DAISANGEN";
        if (string.Equals(s, "DAISUUSHI", StringComparison.OrdinalIgnoreCase) || s == "大四喜") return "DAISUUSHI";
        if (string.Equals(s, "SHOUSUUSHI", StringComparison.OrdinalIgnoreCase) || s == "小四喜") return "SHOUSUUSHI";
        if (string.Equals(s, "TSUUIISOU", StringComparison.OrdinalIgnoreCase) || s == "字一色") return "TSUUIISOU";
        if (string.Equals(s, "CHINROUTOU", StringComparison.OrdinalIgnoreCase) || s == "清老頭") return "CHINROUTOU";
        if (string.Equals(s, "RYUUIISOU", StringComparison.OrdinalIgnoreCase) || s == "緑一色") return "RYUUIISOU";
        if (string.Equals(s, "SUUANKOU", StringComparison.OrdinalIgnoreCase) || s == "四暗刻") return "SUUANKOU";
        if (string.Equals(s, "SUUKANTSU", StringComparison.OrdinalIgnoreCase) || s == "四カンツ") return "SUUKANTSU";

        return "";
    }

    private static string LocalizeSkillYakuDisplay_Local(string yakuName)
    {
        string canonicalName = CanonSkillYakuName_Local(yakuName);
        string key = NormalizeSkillYakuKey_Local(canonicalName);

        if (string.IsNullOrEmpty(key))
            return canonicalName;

        switch (key)
        {
            case "CHUUREN_POUTOU":
            case "DAISANGEN":
            case "DAISUUSHI":
            case "SHOUSUUSHI":
            case "TSUUIISOU":
            case "CHINROUTOU":
            case "RYUUIISOU":
            case "SUUANKOU":
            case "SUUKANTSU":
                return LocalizationManager.Yakuman(key);

            default:
                return LocalizationManager.Yaku(key);
        }
    }

private SkillSetAsset[] _loadedSets;
private SkillSetAsset _currentSet;
private string _lastEquippedId = null;
private string _lastActiveSkillName = null;
private LocalizationManager.Language? _lastLanguage = null;
private string _selectedActiveSkillName = null;
private SkillSetAsset _selectedOwnerSet = null;
public void OnClickConfirm()
{
    // ここで一度、強制的にPrefs→UI反映を走らせ、setId空ならデフォルト確定もさせる
    RefreshFromPrefs(force: true);

    string setId = PlayerPrefs.GetString(prefEquippedKey, "");
    string active = PlayerPrefs.GetString(PrefKeyActiveSkill, "");

    string normalizedActive = NormalizeActiveSkillId_Local(active);
    if (!string.IsNullOrEmpty(normalizedActive) &&
        !string.Equals(active, normalizedActive, StringComparison.Ordinal))
    {
        active = normalizedActive;
        PlayerPrefs.SetString(PrefKeyActiveSkill, active);
    }

    Debug.Log($"[SKILL_SAVE] (before Save) setId='{setId}', active='{active}'");

    PlayerPrefs.Save();
    setId = PlayerPrefs.GetString(prefEquippedKey, "");
    active = PlayerPrefs.GetString(PrefKeyActiveSkill, "");
    Debug.Log($"[SKILL_SAVE] (after Save) setId='{setId}', active='{active}'");

    SceneManager.LoadScene(menuSceneName);
}
    // 「戻る」：選択を反映せずにメニューへ戻る場合
    public void OnClickBackToMenu()
    {
        SceneManager.LoadScene(menuSceneName);
    }
private void Start()
{
    SyncSkillRestrictionFlagsToPrefs();
    RefreshSkillUnlockButtons();
    StartCoroutine(RefreshSkillUIOnNextFrame_Local());
}
public void OnClickSelectSkill(string rawSkillId)
{
    if (_loadedSets == null || _loadedSets.Length == 0)
        LoadSkillSets();

    SyncSkillRestrictionFlagsToPrefs();

    string normalized = NormalizeActiveSkillId_Local(rawSkillId);
    if (string.IsNullOrEmpty(normalized))
        return;

    if (!PlayerData.IsSkillUsable(normalized))
    {
        RefreshSkillUnlockButtons();
        return;
    }

    _selectedActiveSkillName = normalized;
    _selectedOwnerSet = FindOwnerSetByActiveSkillId_Local(normalized);
    _currentSet = _selectedOwnerSet;

    DisableLocalizedTextUIDriversForSkillScene();
    UpdateSkillUIForSelectedSkill_Local(normalized, _selectedOwnerSet);

    _lastActiveSkillName = normalized;
    var loc = LocalizationManager.Instance;
    _lastLanguage = (loc != null) ? loc.CurrentLanguage : LocalizationManager.Language.Japanese;
}
private void UpdateSkillUIForSelectedSkill_Local(string activeSkillName, SkillSetAsset ownerSet)
{
    if (string.IsNullOrEmpty(activeSkillName))
        return;

    activeSkillName = NormalizeActiveSkillId_Local(activeSkillName);

    SkillSetAsset hostSet = ownerSet;
    SkillSetAsset.SkillEntry activeEntry = null;
    if (_loadedSets != null)
    {
        foreach (var s in _loadedSets)
        {
            if (s == null || s.activeSkills == null) continue;

            var entry = s.activeSkills.FirstOrDefault(e =>
                e != null &&
                !string.IsNullOrEmpty(e.activeSkillName) &&
                string.Equals(NormalizeActiveSkillId_Local(e.activeSkillName), activeSkillName, StringComparison.OrdinalIgnoreCase));

            if (entry != null)
            {
                hostSet = s;
                activeEntry = entry;
                break;
            }
        }
    }
_selectedActiveSkillName = activeSkillName;
_selectedOwnerSet = hostSet;
_currentSet = hostSet;

if (skillNameTMP)
{
        string name = string.Empty;

        if (activeEntry != null)
            name = activeEntry.GetLocalizedDisplayName();
        else if (hostSet != null)
            name = hostSet.GetLocalizedDisplayName();
        else
            name = activeSkillName;

        skillNameTMP.text = name ?? string.Empty;
    }

    if (skillIconImage)
    {
        Sprite cutinSprite = null;
        string path = $"PlayerCutins/{activeSkillName}_victory";
        cutinSprite = Resources.Load<Sprite>(path);

        skillIconImage.sprite = cutinSprite;
        skillIconImage.enabled = (cutinSprite != null);
    }

    if (skillDescriptionTMP)
    {
        string desc = string.Empty;

        if (activeEntry != null)
            desc = activeEntry.GetLocalizedDescription();
        else if (hostSet != null && hostSet.activeSkills != null)
        {
            var fallbackEntry = hostSet.activeSkills.FirstOrDefault(e =>
                e != null &&
                !string.IsNullOrEmpty(e.activeSkillName) &&
                string.Equals(NormalizeActiveSkillId_Local(e.activeSkillName), activeSkillName, StringComparison.OrdinalIgnoreCase));

            if (fallbackEntry != null)
                desc = fallbackEntry.GetLocalizedDescription();
        }

        skillDescriptionTMP.text = desc ?? string.Empty;
    }

    string geText = "";
    string shText = "";
    string iyText = "";

    const string COLOR_LOCKED = "#808080";
    const string COLOR_UNLOCK = "#FFF2A8";

    string FormatTraitListWithInitialLevels(System.Collections.Generic.List<string> src, SkillSetAsset.Trait trait)
    {
        if (src == null) return "";

        var cleaned = src.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
        if (cleaned.Count <= 0) return "";

        float GetTraitUpgradeDeltaFromPrefs(SkillSetAsset.Trait t)
        {
            float fallback = 0.05f;
            if (t == SkillSetAsset.Trait.Iyu) fallback = 0.02f;

            string key = "PF_TraitUpgradeDelta_Geki";
            if (t == SkillSetAsset.Trait.Shun) key = "PF_TraitUpgradeDelta_Shun";
            if (t == SkillSetAsset.Trait.Iyu)  key = "PF_TraitUpgradeDelta_Iyu";

            try
            {
                var s = PlayerPrefs.GetString(key, "");
                if (!string.IsNullOrEmpty(s) && float.TryParse(s, out float v))
                    return Mathf.Max(0f, v);
            }
            catch { }

            return Mathf.Max(0f, fallback);
        }

        float[] table = null;
        bool tableIsMultiplier = false;
        try
        {
            if (hostSet != null)
            {
                var tp = hostSet.GetType();

                var flags = System.Reflection.BindingFlags.Instance
                          | System.Reflection.BindingFlags.Public
                          | System.Reflection.BindingFlags.NonPublic;

                float[] GetFloatArrayByAnyName(params string[] names)
                {
                    if (names == null) return null;

                    for (int i = 0; i < names.Length; i++)
                    {
                        var n = names[i];
                        if (string.IsNullOrEmpty(n)) continue;

                        var f = tp.GetField(n, flags);
                        if (f == null) continue;

                        var v = f.GetValue(hostSet) as float[];
                        if (v != null) return v;
                    }
                    return null;
                }

                switch (trait)
                {
                    case SkillSetAsset.Trait.Geki:
                        table = GetFloatArrayByAnyName(
                            "gekiDamageMulByDiff",
                            "gekiMultiplierByDiff",
                            "gekiMulByDiff"
                        );
                        tableIsMultiplier = true;
                        break;

                    case SkillSetAsset.Trait.Shun:
                        table = GetFloatArrayByAnyName(
                            "shunMpHealMulByDiff",
                            "shunMpPctByDiff",
                            "shunMpRateByDiff"
                        );
                        tableIsMultiplier = false;
                        break;

                    case SkillSetAsset.Trait.Iyu:
                        table = GetFloatArrayByAnyName(
                            "iyuHealMulByDiff",
                            "iyuHealPctByDiff",
                            "iyuHealRateByDiff"
                        );
                        tableIsMultiplier = false;
                        break;
                }
            }
        }
        catch
        {
            table = null;
            tableIsMultiplier = false;
        }

        float deltaPerLevel = 0f;
        try { deltaPerLevel = GetTraitUpgradeDeltaFromPrefs(trait); } catch { deltaPerLevel = 0f; }

        float CalcEffectAdd(string yakuKey, int lvForEffect)
        {
            lvForEffect = Mathf.Max(1, lvForEffect);

            float add = 0f;

            if (table != null && table.Length > 0 && hostSet != null && hostSet.traitMap != null)
            {
                int di = 0;
                try
                {
                    var entry = hostSet.traitMap.FirstOrDefault(t =>
                        t != null &&
                        t.trait == trait &&
                        !string.IsNullOrEmpty(t.yakuName) &&
                        (
                            NormalizeSkillYakuKey_Local(t.yakuName.Trim()) == NormalizeSkillYakuKey_Local(yakuKey) ||
                            CanonSkillYakuName_Local(t.yakuName.Trim()) == CanonSkillYakuName_Local(yakuKey)
                        ));

                    if (entry != null)
                        di = Mathf.Clamp((int)entry.difficulty, 0, table.Length - 1);
                }
                catch { di = 0; }

                var v = Mathf.Max(0f, table[Mathf.Clamp(di, 0, table.Length - 1)]);
                add = tableIsMultiplier ? Mathf.Max(0f, v - 1f) : v;
            }
            else
            {
                add = 0f;
            }

            if (deltaPerLevel > 0f)
            {
                int deltaLv = Mathf.Max(0, lvForEffect - 1);
                add += deltaPerLevel * deltaLv;
            }

            return Mathf.Max(0f, add);
        }

        string FormatPct(float add01)
        {
            float pct = Mathf.Max(0f, add01) * 100f;
            if (Mathf.Abs(pct - Mathf.Round(pct)) < 0.0001f)
                return $"{Mathf.RoundToInt(pct)}%";
            return $"{pct:0.##}%";
        }

        var parts = new System.Collections.Generic.List<string>();
        bool firstAssigned = false;

        for (int i = 0; i < cleaned.Count; i++)
        {
            string yakuRaw = cleaned[i];
            if (string.IsNullOrEmpty(yakuRaw)) continue;

            string yakuCanonicalName = CanonSkillYakuName_Local(yakuRaw);
            string yakuDisplay = LocalizeSkillYakuDisplay_Local(yakuCanonicalName);

            int lv = 0;
            if (!firstAssigned)
            {
                lv = 1;
                firstAssigned = true;
            }

            float add = CalcEffectAdd(yakuCanonicalName, lv);

            string color = (lv <= 0) ? COLOR_LOCKED : COLOR_UNLOCK;
            parts.Add($"<color={color}>{yakuDisplay} Lv.{lv} {FormatPct(add)}</color>");
        }

        return string.Join(" / ", parts);
    }

    if (hostSet != null)
    {
        var yakuTuple = hostSet.GetTraitYakuFor(activeSkillName);
        var ge = yakuTuple.ge ?? new System.Collections.Generic.List<string>();
        var sh = yakuTuple.sh ?? new System.Collections.Generic.List<string>();
        var iy = yakuTuple.iy ?? new System.Collections.Generic.List<string>();

        geText = FormatTraitListWithInitialLevels(ge, SkillSetAsset.Trait.Geki);
        shText = FormatTraitListWithInitialLevels(sh, SkillSetAsset.Trait.Shun);
        iyText = FormatTraitListWithInitialLevels(iy, SkillSetAsset.Trait.Iyu);
    }

    if (gekiYakuTMP) gekiYakuTMP.text = geText;
    if (shunYakuTMP) shunYakuTMP.text = shText;
    if (iyuYakuTMP)  iyuYakuTMP.text  = iyText;
}
private IEnumerator RefreshSkillUIOnNextFrame_Local()
{
    yield return null;
    RefreshFromPrefs(force: true);
}
private void Awake()
{
    LoadSkillSets();
    DisableLocalizedTextUIDriversForSkillScene();
}
private void OnEnable()
{
    LocalizationManager.LanguageChanged -= HandleLanguageChanged_Local;
    LocalizationManager.LanguageChanged += HandleLanguageChanged_Local;

    RefreshFromPrefs(force: true);
}
private void OnDisable()
{
    LocalizationManager.LanguageChanged -= HandleLanguageChanged_Local;

    if (_languageRefreshCoroutine != null)
    {
        StopCoroutine(_languageRefreshCoroutine);
        _languageRefreshCoroutine = null;
    }
}
private Coroutine _languageRefreshCoroutine;

private void HandleLanguageChanged_Local(LocalizationManager.Language language)
{
    if (_languageRefreshCoroutine != null)
    {
        StopCoroutine(_languageRefreshCoroutine);
    }

    _languageRefreshCoroutine = StartCoroutine(RefreshSkillUILate_Local());
}

private IEnumerator RefreshSkillUILate_Local()
{
    yield return null;
    DisableLocalizedTextUIDriversForSkillScene();
    RefreshFromPrefs(force: true);
    _languageRefreshCoroutine = null;
}

    private void LoadSkillSets()
    {
        _loadedSets = Resources.LoadAll<SkillSetAsset>(skillSetResourcesFolder)
                               .Where(s => s != null).ToArray();

        if ((_loadedSets == null || _loadedSets.Length == 0) && fallbackSkillSet)
        {
            _loadedSets = new[] { fallbackSkillSet };
        }
    }

    private SkillSetAsset FindSetById(string id)
    {
        if (string.IsNullOrEmpty(id) || _loadedSets == null) return null;

        foreach (var s in _loadedSets)
        {
            if (!s) continue;
            if (s.id == id) return s;
        }
        return null;
    }
private void RefreshFromPrefs(bool force)
{
    if (_loadedSets == null || _loadedSets.Length == 0)
    {
        LoadSkillSets();
    }

    SyncSkillRestrictionFlagsToPrefs();

    string id = PlayerPrefs.GetString(prefEquippedKey, "");
    string active = PlayerPrefs.GetString(PrefKeyActiveSkill, "");

    string normalizedActive = NormalizeToUsableSkill_Local(active);
    if (!string.IsNullOrEmpty(normalizedActive) &&
        !string.Equals(active, normalizedActive, StringComparison.Ordinal))
    {
        active = normalizedActive;
        PlayerPrefs.SetString(PrefKeyActiveSkill, active);

        SkillSetAsset activeOwner = FindOwnerSetByActiveSkillId_Local(active);
        if (activeOwner != null && !string.IsNullOrEmpty(activeOwner.id))
        {
            id = activeOwner.id;
            PlayerPrefs.SetString(prefEquippedKey, id);
        }

        PlayerPrefs.Save();
    }

    RefreshSkillUnlockButtons();

    // setId が空なら「RandomMan の所属セット」をデフォルトとして確定させる（Build初回対策）
    if (string.IsNullOrEmpty(id))
    {
        SkillSetAsset def = null;

        if (_loadedSets != null && _loadedSets.Length > 0)
        {
            def = FindOwnerSetByActiveSkillId_Local("RandomMan");

            if (def == null)
            {
                def = _loadedSets.FirstOrDefault(s =>
                    s != null && string.Equals((s.id ?? "").Trim(), "SET_RANDOMMAN", StringComparison.OrdinalIgnoreCase));
            }

            if (def == null) def = _loadedSets[0];
        }

        if (def != null && !string.IsNullOrEmpty(def.id))
        {
            id = def.id;
            PlayerPrefs.SetString(prefEquippedKey, id);

            // active も空なら、必ず RandomMan を入れる
            if (string.IsNullOrEmpty(active))
            {
                active = NormalizeActiveSkillId_Local("RandomMan");
                PlayerPrefs.SetString(PrefKeyActiveSkill, active);
            }

            PlayerPrefs.Save();
            Debug.Log($"[SKILL_AUTO_DEFAULT] setId='{id}', active='{active}'");
        }
    }
var loc = LocalizationManager.Instance;
var currentLanguage = (loc != null) ? loc.CurrentLanguage : LocalizationManager.Language.Japanese;

if (!force &&
    id == _lastEquippedId &&
    active == _lastActiveSkillName &&
    _lastLanguage.HasValue &&
    _lastLanguage.Value == currentLanguage)
{
    return;
}
    Debug.Log($"[SKILL_PREF_TICK] force={force} setId='{id}' active='{active}' prevSetId='{_lastEquippedId}' prevActive='{_lastActiveSkillName}'");

_lastEquippedId      = id;
_lastActiveSkillName = active;
_lastLanguage        = currentLanguage;

_currentSet = FindSetById(id);
if (_currentSet == null && _loadedSets != null && _loadedSets.Length > 0)
{
    _currentSet = _loadedSets[0];
}

_selectedActiveSkillName = active;
_selectedOwnerSet = FindOwnerSetByActiveSkillId_Local(active);

DisableLocalizedTextUIDriversForSkillScene();
UpdateSkillUI();
}

private void UpdateSkillUI()
{
    // --- 現在のアクティブスキル名を PlayerPrefs から取得 ---
    string activeSkillName = NormalizeActiveSkillId_Local(PlayerPrefs.GetString(PrefKeyActiveSkill, ""));

    if (!string.IsNullOrEmpty(activeSkillName))
    {
        string stored = PlayerPrefs.GetString(PrefKeyActiveSkill, "");
        if (!string.Equals(stored, activeSkillName, StringComparison.Ordinal))
        {
            PlayerPrefs.SetString(PrefKeyActiveSkill, activeSkillName);
            PlayerPrefs.Save();
        }
    }

    SkillSetAsset hostSet = null;
    SkillSetAsset.SkillEntry activeEntry = null;
if (!string.IsNullOrEmpty(activeSkillName) && _loadedSets != null)
{
    foreach (var s in _loadedSets)
    {
        if (s == null || s.activeSkills == null) continue;

        var entry = s.activeSkills
            .FirstOrDefault(e =>
                e != null &&
                (
                    (!string.IsNullOrEmpty(e.activeSkillName) &&
                     string.Equals(e.activeSkillName.Trim(), activeSkillName, StringComparison.OrdinalIgnoreCase))
                    ||
                    (!string.IsNullOrEmpty(e.displayName) &&
                     string.Equals(NormalizeActiveSkillId_Local(e.displayName), activeSkillName, StringComparison.OrdinalIgnoreCase))
                    ||
                    (!string.IsNullOrEmpty(e.displayNameEnglish) &&
                     string.Equals(NormalizeActiveSkillId_Local(e.displayNameEnglish), activeSkillName, StringComparison.OrdinalIgnoreCase))
                    ||
                    (!string.IsNullOrEmpty(e.displayNameChineseSimplified) &&
                     string.Equals(NormalizeActiveSkillId_Local(e.displayNameChineseSimplified), activeSkillName, StringComparison.OrdinalIgnoreCase))
                ));

        if (entry != null)
        {
            hostSet = s;
            activeEntry = entry;
            break;
        }
    }
}
    // フォールバック：見つからなければ現在の SkillSet を使う
    if (hostSet == null)
    {
        hostSet = _currentSet;
    }
    // ==========================
    // 0. 職業名（ローカライズ済み DisplayName を最優先）
    // ==========================
    if (skillNameTMP)
    {
        string name = string.Empty;

        if (activeEntry != null)
        {
            name = activeEntry.GetLocalizedDisplayName();
        }
        else if (hostSet != null)
        {
            name = hostSet.GetLocalizedDisplayName();
        }
        else if (!string.IsNullOrEmpty(activeSkillName))
        {
            name = activeSkillName;
        }

        skillNameTMP.text = name ?? string.Empty;
    }
if (skillActionNameTMP)
{
    string actionName = GetLocalizedSkillActionName_Local(activeEntry, activeSkillName);
    skillActionNameTMP.text = actionName ?? string.Empty;
}
    if (skillIconImage)
    {
        Sprite cutinSprite = null;

        if (!string.IsNullOrEmpty(activeSkillName))
        {
            string path = $"PlayerCutins/{activeSkillName}_victory";
            cutinSprite = Resources.Load<Sprite>(path);
        }

        skillIconImage.sprite  = cutinSprite;
        skillIconImage.enabled = (cutinSprite != null);
    }
if (skillDescriptionTMP)
{
    string desc = string.Empty;

    if (activeEntry != null)
    {
        desc = activeEntry.GetLocalizedDescription();
    }
    else if (!string.IsNullOrEmpty(activeSkillName) && hostSet != null && hostSet.activeSkills != null)
    {
        var fallbackEntry = hostSet.activeSkills.FirstOrDefault(e =>
            e != null &&
            !string.IsNullOrEmpty(e.activeSkillName) &&
            string.Equals(NormalizeActiveSkillId_Local(e.activeSkillName), activeSkillName, StringComparison.OrdinalIgnoreCase));

        if (fallbackEntry != null)
        {
            desc = fallbackEntry.GetLocalizedDescription();
        }
    }

    skillDescriptionTMP.text = desc ?? string.Empty;
}
    string geText = "";
    string shText = "";
    string iyText = "";

    // Lv0（未解放扱い）＝グレー、Lv1以上＝薄黄色
    const string COLOR_LOCKED = "#808080";
    const string COLOR_UNLOCK = "#FFF2A8";

string FormatTraitListWithInitialLevels(System.Collections.Generic.List<string> src, SkillSetAsset.Trait trait)
{
    if (src == null) return "";

    var cleaned = src.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
    if (cleaned.Count <= 0) return "";

    // Δ（UpgradeManager が PlayerPrefs に保存している）
    float GetTraitUpgradeDeltaFromPrefs(SkillSetAsset.Trait t)
    {
        float fallback = 0.05f;
        if (t == SkillSetAsset.Trait.Iyu) fallback = 0.02f;

        string key = "PF_TraitUpgradeDelta_Geki";
        if (t == SkillSetAsset.Trait.Shun) key = "PF_TraitUpgradeDelta_Shun";
        if (t == SkillSetAsset.Trait.Iyu)  key = "PF_TraitUpgradeDelta_Iyu";

        try
        {
            var s = PlayerPrefs.GetString(key, "");
            if (!string.IsNullOrEmpty(s) && float.TryParse(s, out float v))
                return Mathf.Max(0f, v);
        }
        catch { }

        return Mathf.Max(0f, fallback);
    }
// 難易度別テーブル（hostSet から取得）
// ★SerializeField(private) も拾えるように BindingFlags を付け、旧名/新名も複数候補で拾う
float[] table = null;
bool tableIsMultiplier = false; // Geki は true（1.20 などの倍率を保持）
try
{
    if (hostSet != null)
    {
        var tp = hostSet.GetType();

        var flags = System.Reflection.BindingFlags.Instance
                  | System.Reflection.BindingFlags.Public
                  | System.Reflection.BindingFlags.NonPublic;

        float[] GetFloatArrayByAnyName(params string[] names)
        {
            if (names == null) return null;

            for (int i = 0; i < names.Length; i++)
            {
                var n = names[i];
                if (string.IsNullOrEmpty(n)) continue;

                var f = tp.GetField(n, flags);
                if (f == null) continue;

                var v = f.GetValue(hostSet) as float[];
                if (v != null) return v;
            }
            return null;
        }

        switch (trait)
        {
            case SkillSetAsset.Trait.Geki:
                table = GetFloatArrayByAnyName(
                    "gekiDamageMulByDiff",
                    "gekiMultiplierByDiff",
                    "gekiMulByDiff"
                );
                tableIsMultiplier = true;  // ←倍率
                break;

            case SkillSetAsset.Trait.Shun:
                table = GetFloatArrayByAnyName(
                    "shunMpHealMulByDiff",
                    "shunMpPctByDiff",
                    "shunMpRateByDiff"
                );
                tableIsMultiplier = false; // ←％
                break;

            case SkillSetAsset.Trait.Iyu:
                table = GetFloatArrayByAnyName(
                    "iyuHealMulByDiff",
                    "iyuHealPctByDiff",
                    "iyuHealRateByDiff"
                );
                tableIsMultiplier = false; // ←％
                break;
        }
    }
}
catch
{
    table = null;
    tableIsMultiplier = false;
}
    float deltaPerLevel = 0f;
    try { deltaPerLevel = GetTraitUpgradeDeltaFromPrefs(trait); } catch { deltaPerLevel = 0f; }

    float CalcEffectAdd(string yakuKey, int lvForEffect)
    {
        // Lv0表示でも効果量はLv1の値を出す仕様
        lvForEffect = Mathf.Max(1, lvForEffect);

        float add = 0f;

        // ベース（難易度テーブル）
        if (table != null && table.Length > 0 && hostSet != null && hostSet.traitMap != null)
        {
            int di = 0;
            try
            {
                var entry = hostSet.traitMap.FirstOrDefault(t =>
                    t != null &&
                    t.trait == trait &&
                    !string.IsNullOrEmpty(t.yakuName) &&
                    (
                        NormalizeSkillYakuKey_Local(t.yakuName.Trim()) == NormalizeSkillYakuKey_Local(yakuKey) ||
                        CanonSkillYakuName_Local(t.yakuName.Trim()) == CanonSkillYakuName_Local(yakuKey)
                    ));

                if (entry != null)
                    di = Mathf.Clamp((int)entry.difficulty, 0, table.Length - 1);
            }
            catch { di = 0; }
            var v = Mathf.Max(0f, table[Mathf.Clamp(di, 0, table.Length - 1)]);
            add = tableIsMultiplier ? Mathf.Max(0f, v - 1f) : v;
        }
        else
        {
            add = 0f;
        }

        // Δ：Lv2から (Lv-1)×Δ を加算
        if (deltaPerLevel > 0f)
        {
            int deltaLv = Mathf.Max(0, lvForEffect - 1);
            add += deltaPerLevel * deltaLv;
        }

        return Mathf.Max(0f, add);
    }

    string FormatPct(float add01)
    {
        float pct = Mathf.Max(0f, add01) * 100f;
        if (Mathf.Abs(pct - Mathf.Round(pct)) < 0.0001f)
            return $"{Mathf.RoundToInt(pct)}%";
        return $"{pct:0.##}%";
    }

    // 先頭の有効な該当役のみ Lv1、それ以降は Lv0
    var parts = new System.Collections.Generic.List<string>();
    bool firstAssigned = false;

    for (int i = 0; i < cleaned.Count; i++)
    {
        string yakuRaw = cleaned[i];
        if (string.IsNullOrEmpty(yakuRaw)) continue;

        string yakuCanonicalName = CanonSkillYakuName_Local(yakuRaw);
        string yakuDisplay = LocalizeSkillYakuDisplay_Local(yakuCanonicalName);

        int lv = 0;
        if (!firstAssigned)
        {
            lv = 1;
            firstAssigned = true;
        }

        float add = CalcEffectAdd(yakuCanonicalName, lv);

        string color = (lv <= 0) ? COLOR_LOCKED : COLOR_UNLOCK;
        parts.Add($"<color={color}>{yakuDisplay} Lv.{lv} {FormatPct(add)}</color>");
    }
    return string.Join(" / ", parts);
}
    if (hostSet != null && !string.IsNullOrEmpty(activeSkillName))
    {
        // SkillSetAsset.GetTraitYakuFor を使用して、
        // アクティブスキルごとの個別設定 → 無ければ traitMap の値を取得
        var yakuTuple = hostSet.GetTraitYakuFor(activeSkillName);
        var ge = yakuTuple.ge ?? new System.Collections.Generic.List<string>();
        var sh = yakuTuple.sh ?? new System.Collections.Generic.List<string>();
        var iy = yakuTuple.iy ?? new System.Collections.Generic.List<string>();

geText = FormatTraitListWithInitialLevels(ge, SkillSetAsset.Trait.Geki);
shText = FormatTraitListWithInitialLevels(sh, SkillSetAsset.Trait.Shun);
iyText = FormatTraitListWithInitialLevels(iy, SkillSetAsset.Trait.Iyu);

    }

    if (gekiYakuTMP) gekiYakuTMP.text = geText;
    if (shunYakuTMP) shunYakuTMP.text = shText;
    if (iyuYakuTMP)  iyuYakuTMP.text  = iyText;
}


    // ==========================
    // Dropdown から直接呼び出すためのフック（任意）
    // ==========================

    /// <summary>
    /// Menu 側の Dropdown の OnValueChanged などから呼び出し、
    /// 最新の PlayerPrefs の値で UI を強制更新したい場合に使う。
    /// </summary>
    public void ForceRefreshUIFromPrefs()
    {
        RefreshFromPrefs(force: true);
    }
}
