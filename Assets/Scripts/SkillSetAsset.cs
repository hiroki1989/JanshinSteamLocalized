using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[CreateAssetMenu(menuName = "Mahjan/Skill Set", fileName = "SkillSet_XXXX")]
public class SkillSetAsset : ScriptableObject
{
    // === CSV取込で使う「役→特性」マップ ===
[System.Serializable]
public enum Trait
{
    None = 0,
    Geki,   // 撃
    Shun,   // 瞬
    Iyu     // 癒
}

[System.Serializable]
public class YakuTraitEntry
{
    public string yakuName;          // 役名（CSVの yakuName と一致）
    public Trait trait;              // 撃/瞬/癒
    public YakuDifficulty difficulty;   // ★ここを YakuDifficulty に変更
}

// ImportSkillSetsFromCsv が詰め込む先
public List<YakuTraitEntry> traitMap = new List<YakuTraitEntry>();

[Header("Identity")]
public string id;
public string displayName;
public string displayNameEnglish;
public string displayNameChineseSimplified;

[TextArea] public string description;
[TextArea] public string descriptionEnglish;
[TextArea] public string descriptionChineseSimplified;

public string GetLocalizedDisplayName()
{
    var lm = LocalizationManager.Instance;
    if (lm == null)
        return string.IsNullOrEmpty(displayName) ? "" : displayName;

    switch (lm.CurrentLanguage)
    {
        case LocalizationManager.Language.English:
            if (!string.IsNullOrEmpty(displayNameEnglish)) return displayNameEnglish;
            return displayName ?? "";

        case LocalizationManager.Language.ChineseSimplified:
            if (!string.IsNullOrEmpty(displayNameChineseSimplified)) return displayNameChineseSimplified;
            return displayName ?? "";

        case LocalizationManager.Language.Japanese:
        default:
            return displayName ?? "";
    }
}

public string GetLocalizedDescription()
{
    var lm = LocalizationManager.Instance;
    if (lm == null)
        return string.IsNullOrEmpty(description) ? "" : description;

    switch (lm.CurrentLanguage)
    {
        case LocalizationManager.Language.English:
            if (!string.IsNullOrEmpty(descriptionEnglish)) return descriptionEnglish;
            return description ?? "";

        case LocalizationManager.Language.ChineseSimplified:
            if (!string.IsNullOrEmpty(descriptionChineseSimplified)) return descriptionChineseSimplified;
            return description ?? "";

        case LocalizationManager.Language.Japanese:
        default:
            return description ?? "";
    }
}

[Header("MP Settings (Inspectorで編集可能)")]
    public int maxMP = 10;           // 最大MP
    public int startMP = 10;         // 開始時MP（初期値）
    public int regenPerTurn = 1;     // 1ターン毎回復MP（③）
    public int regenOnWin = 3;       // 和了毎回復MP（③）

    [Header("Run / 対局ごとの回復量")]
    public int recoverHpPerBattle = 0;   // 敵を倒して次の敵に進むときに回復するHP量
    public int recoverMpPerBattle = 0;   // 敵を倒して次の敵に進むときに回復するMP量

[Serializable]
public class SkillEntry
{
    public string activeSkillName;
    public int mpCost = 2;

    [Header("UI")]
    public Sprite icon;

[Header("Display Name")]
public string displayName;
public string displayNameEnglish;
public string displayNameChineseSimplified;

[Header("Action Skill Name")]
public string actionName;
public string actionNameEnglish;
public string actionNameChineseSimplified;

[Header("Description")]
[TextArea] public string description;
[TextArea] public string descriptionEnglish;
[TextArea] public string descriptionChineseSimplified;

    [Header("Overrides (optional): このスキル専用の該当役")]
    public List<string> gekiYaku = new();
    public List<string> shunYaku = new();
    public List<string> iyuYaku  = new();

    public string GetLocalizedDisplayName()
    {
        var lm = LocalizationManager.Instance;
        if (lm == null)
            return string.IsNullOrEmpty(displayName) ? "" : displayName;

        switch (lm.CurrentLanguage)
        {
            case LocalizationManager.Language.English:
                if (!string.IsNullOrEmpty(displayNameEnglish)) return displayNameEnglish;
                return displayName ?? "";

            case LocalizationManager.Language.ChineseSimplified:
                if (!string.IsNullOrEmpty(displayNameChineseSimplified)) return displayNameChineseSimplified;
                return displayName ?? "";

            case LocalizationManager.Language.Japanese:
            default:
                return displayName ?? "";
        }
    }
public string GetLocalizedActionName()
{
    var lm = LocalizationManager.Instance;
    if (lm == null)
        return string.IsNullOrEmpty(actionName) ? "" : actionName;

    switch (lm.CurrentLanguage)
    {
        case LocalizationManager.Language.English:
            if (!string.IsNullOrEmpty(actionNameEnglish)) return actionNameEnglish;
            return actionName ?? "";

        case LocalizationManager.Language.ChineseSimplified:
            if (!string.IsNullOrEmpty(actionNameChineseSimplified)) return actionNameChineseSimplified;
            return actionName ?? "";

        case LocalizationManager.Language.Japanese:
        default:
            return actionName ?? "";
    }
}
    public string GetLocalizedDescription()
    {
        var lm = LocalizationManager.Instance;
        if (lm == null)
            return string.IsNullOrEmpty(description) ? "" : description;

        switch (lm.CurrentLanguage)
        {
            case LocalizationManager.Language.English:
                if (!string.IsNullOrEmpty(descriptionEnglish)) return descriptionEnglish;
                return description ?? "";

            case LocalizationManager.Language.ChineseSimplified:
                if (!string.IsNullOrEmpty(descriptionChineseSimplified)) return descriptionChineseSimplified;
                return description ?? "";

            case LocalizationManager.Language.Japanese:
            default:
                return description ?? "";
        }
    }
}
    [Header("Active Skills used in RunScene")]
    public List<SkillEntry> activeSkills = new();

    public enum YakuDifficulty { Easy, Normal, Hard, Yakuman }
[Header("撃/瞬/癒 係数（難易度別）")]
public float[] gekiMultiplierByDiff = { 1.10f, 1.10f, 1.10f, 1.10f }; // 撃：役点×倍率（与ダメ倍率）
[Tooltip("※従来仕様（ダメージ固定加点）。瞬はMP回復へ仕様変更後は参照しません。")]
public int[]   shunAddByDiff        = { 3000,  3000,  3000,  3000  }; // （非推奨）瞬：固定加点
public float[] iyuHealMulByDiff     = { 0.30f, 0.30f, 0.30f, 0.30f }; // 癒：役点×倍率ぶんHP回復

[Tooltip("瞬：該当役が含まれると、この％（難易度別）を合算してMPを回復（和了点×％）。例：0.10=10%")]
public float[] shunMpPctByDiff      = { 0.30f, 0.30f, 0.30f, 0.30f }; // 瞬のMP回復率（係数）
    // ⑪ 強化用の係数（例：レベルごとに上乗せ）
    [Header("Upgrade Scaling (per level)")]
    public int level = 0;                 // ラン内/恒常いずれでもOK（簡便に int）
    public float gekiPerLevel = 0.05f;    // レベル1ごとに+0.05
    public int   shunPerLevel = 300;      // レベル1ごとに+300
    public float iyuPerLevel  = 0.02f;    // レベル1ごとに+0.02

    // 実効係数を取得
    public (float mul, int add, float healMul) GetTraitCoeffs(YakuDifficulty diff)
    {
        int i = Mathf.Clamp((int)diff, 0, 3);
        float mul = gekiMultiplierByDiff[i] + level * gekiPerLevel;
        int   add = shunAddByDiff[i]        + level * shunPerLevel;
        float hmu = iyuHealMulByDiff[i]     + level * iyuPerLevel;
        return (mul, add, hmu);
    }

    // ActiveSkill → MPコスト
    public bool TryGetMpCostFor(string activeSkillName, out int cost)
    {
        foreach (var e in activeSkills)
            if (string.Equals(e.activeSkillName, activeSkillName, StringComparison.Ordinal))
            { cost = Mathf.Max(0, e.mpCost); return true; }
        cost = 0; return false;
    }
    public (List<string> ge, List<string> sh, List<string> iy) GetTraitYakuFor(string activeSkillName)
{
    var e = activeSkills.FirstOrDefault(x =>
        string.Equals(x.activeSkillName, activeSkillName, StringComparison.Ordinal));

    // そのスキルに個別指定があればそれを優先
    if (e != null && (e.gekiYaku.Count + e.shunYaku.Count + e.iyuYaku.Count) > 0)
        return (e.gekiYaku, e.shunYaku, e.iyuYaku);

    // フォールバック：セット全体の traitMap
    var ge = traitMap.Where(t => t != null && t.trait == Trait.Geki).Select(t => t.yakuName).ToList();
    var sh = traitMap.Where(t => t != null && t.trait == Trait.Shun).Select(t => t.yakuName).ToList();
    var iy = traitMap.Where(t => t != null && t.trait == Trait.Iyu ).Select(t => t.yakuName).ToList();
    return (ge, sh, iy);
}

private static string NormalizeTraitProgressYakuKey_Local(string yakuName)
{
    if (string.IsNullOrWhiteSpace(yakuName)) return "";

    string s = yakuName.Trim().Replace("　", " ");
    s = s.Replace('（', '(').Replace('）', ')');

    int p0 = s.IndexOf('(');
    if (p0 >= 0) s = s.Substring(0, p0);

    s = s.Trim();
    if (string.IsNullOrWhiteSpace(s)) return "";

    if (s.StartsWith("yaku.", StringComparison.OrdinalIgnoreCase))
    {
        string tail = s.Substring("yaku.".Length).Trim();
        return tail.ToUpperInvariant();
    }

    if (s.StartsWith("yakuman.", StringComparison.OrdinalIgnoreCase))
    {
        string tail = s.Substring("yakuman.".Length).Trim();
        return tail.ToUpperInvariant();
    }

if (s == "風牌") return "YAKUHAI";
if (s.StartsWith("風牌", StringComparison.Ordinal)) return "YAKUHAI";
if (s == "役牌") return "YAKUHAI";
if (s.StartsWith("役牌", StringComparison.Ordinal)) return "YAKUHAI";
if (s == "白" || s == "發" || s == "発" || s == "中") return "YAKUHAI";

    if (s == "平和" || s == "ピンフ" || s.Equals("Pinfu", StringComparison.OrdinalIgnoreCase)) return "PINFU";
    if (s == "タンヤオ" || s == "断么九" || s == "断幺九" || s.Equals("Tanyao", StringComparison.OrdinalIgnoreCase)) return "TANYAO";
    if (s == "一盃口" || s.Equals("Iipeikou", StringComparison.OrdinalIgnoreCase)) return "IIPEIKOU";
    if (s == "二盃口" || s.Equals("Ryanpeikou", StringComparison.OrdinalIgnoreCase)) return "RYANPEIKOU";
    if (s == "三色同順" || s.Equals("Sanshoku_Doujun", StringComparison.OrdinalIgnoreCase)) return "SANSHOKU_DOUJUN";
    if (s == "一気通貫" || s.Equals("Ittsu", StringComparison.OrdinalIgnoreCase)) return "ITTSU";
    if (s == "チャンタ" || s.Equals("Chanta", StringComparison.OrdinalIgnoreCase)) return "CHANTA";
    if (s == "純チャン" || s.Equals("Junchan", StringComparison.OrdinalIgnoreCase)) return "JUNCHAN";
    if (s == "対々和" || s.Equals("Toitoi", StringComparison.OrdinalIgnoreCase)) return "TOITOI";
    if (s == "三暗刻" || s.Equals("Sanankou", StringComparison.OrdinalIgnoreCase)) return "SANANKOU";
    if (s == "三カンツ" || s == "三槓子" || s.Equals("Sankantsu", StringComparison.OrdinalIgnoreCase)) return "SANKANTSU";
    if (s == "三色同刻" || s.Equals("Sanshoku_Doukou", StringComparison.OrdinalIgnoreCase)) return "SANSHOKU_DOUKOU";
    if (s == "小三元" || s.Equals("Shousangen", StringComparison.OrdinalIgnoreCase)) return "SHOUSANGEN";
    if (s == "混老頭" || s.Equals("Honroutou", StringComparison.OrdinalIgnoreCase)) return "HONROUTOU";
    if (s == "混一色" || s == "ホンイツ" || s.Equals("Honitsu", StringComparison.OrdinalIgnoreCase)) return "HONITSU";
    if (s == "清一色" || s.Equals("Chinitsu", StringComparison.OrdinalIgnoreCase)) return "CHINITSU";
if (s == "七対子" || s.Equals("Chiitoitsu", StringComparison.OrdinalIgnoreCase)) return "CHIITOITSU";
if (s == "門前清自摸和" || s.Equals("Menzen_Tsumo", StringComparison.OrdinalIgnoreCase)) return "MENZEN_TSUMO";

if (s == "国士無双" || s == "国士无双" || s.Equals("Kokushi", StringComparison.OrdinalIgnoreCase) || s.Equals("Kokushi Musou", StringComparison.OrdinalIgnoreCase)) return "KOKUSHI";
if (s == "九蓮宝燈" || s.Equals("Chuuren_Poutou", StringComparison.OrdinalIgnoreCase)) return "CHUUREN_POUTOU";
    if (s == "大三元" || s.Equals("Daisangen", StringComparison.OrdinalIgnoreCase)) return "DAISANGEN";
    if (s == "大四喜" || s.Equals("Daisuushi", StringComparison.OrdinalIgnoreCase)) return "DAISUUSHI";
    if (s == "小四喜" || s.Equals("Shousuushi", StringComparison.OrdinalIgnoreCase)) return "SHOUSUUSHI";
    if (s == "字一色" || s.Equals("Tsuuiisou", StringComparison.OrdinalIgnoreCase)) return "TSUUIISOU";
    if (s == "清老頭" || s.Equals("Chinroutou", StringComparison.OrdinalIgnoreCase)) return "CHINROUTOU";
    if (s == "緑一色" || s.Equals("Ryuuiisou", StringComparison.OrdinalIgnoreCase)) return "RYUUIISOU";
    if (s == "四暗刻" || s.Equals("Suuankou", StringComparison.OrdinalIgnoreCase)) return "SUUANKOU";
    if (s == "四カンツ" || s == "四槓子" || s.Equals("Suukantsu", StringComparison.OrdinalIgnoreCase)) return "SUUKANTSU";

    return s.ToUpperInvariant();
}

private string PrefKey_Unlocked(Trait t, string activeSkillName)
    => $"PF_TraitUnlocked_{id}_{activeSkillName}_{t}";

private string PrefKey_Level(Trait t, string activeSkillName, string yakuName)
    => $"PF_TraitLevel_{id}_{activeSkillName}_{t}_{NormalizeTraitProgressYakuKey_Local(yakuName)}";

public void EnsureInitialTraitUnlocks(string activeSkillName)
{
    if (string.IsNullOrWhiteSpace(activeSkillName)) return;

    bool any =
        !string.IsNullOrEmpty(PlayerPrefs.GetString(PrefKey_Unlocked(Trait.Geki, activeSkillName), "")) ||
        !string.IsNullOrEmpty(PlayerPrefs.GetString(PrefKey_Unlocked(Trait.Shun, activeSkillName), "")) ||
        !string.IsNullOrEmpty(PlayerPrefs.GetString(PrefKey_Unlocked(Trait.Iyu,  activeSkillName), ""));

    if (any) return;

    var all = GetTraitYakuFor(activeSkillName);

    void UnlockFirst(Trait tr, List<string> list)
    {
        if (list == null) return;
        var first = list.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (string.IsNullOrWhiteSpace(first)) return;
        SetUnlockedList(tr, activeSkillName, new List<string> { first.Trim() });
    }

    UnlockFirst(Trait.Geki, all.ge);
    UnlockFirst(Trait.Shun, all.sh);
    UnlockFirst(Trait.Iyu,  all.iy);
}

public (List<string> ge, List<string> sh, List<string> iy) GetUnlockedTraitYakuFor(string activeSkillName)
{
    EnsureInitialTraitUnlocks(activeSkillName);

    var ge = GetUnlockedList(Trait.Geki, activeSkillName);
    var sh = GetUnlockedList(Trait.Shun, activeSkillName);
    var iy = GetUnlockedList(Trait.Iyu,  activeSkillName);

    return (ge, sh, iy);
}

public bool IsUnlockedTraitYaku(string activeSkillName, Trait trait, string yakuName)
{
    if (string.IsNullOrWhiteSpace(yakuName)) return false;
    EnsureInitialTraitUnlocks(activeSkillName);
    var list = GetUnlockedList(trait, activeSkillName);
    return list.Any(x => string.Equals(x, yakuName.Trim(), StringComparison.OrdinalIgnoreCase));
}

public void UnlockTraitYaku(string activeSkillName, Trait trait, string yakuName)
{
    if (string.IsNullOrWhiteSpace(activeSkillName)) return;
    if (string.IsNullOrWhiteSpace(yakuName)) return;

    EnsureInitialTraitUnlocks(activeSkillName);

    var list = GetUnlockedList(trait, activeSkillName);
    var norm = yakuName.Trim();
    if (!list.Any(x => string.Equals(x, norm, StringComparison.OrdinalIgnoreCase)))
    {
        list.Add(norm);
        SetUnlockedList(trait, activeSkillName, list);
    }
}

public int GetTraitYakuLevel(string activeSkillName, Trait trait, string yakuName)
{
    if (string.IsNullOrWhiteSpace(activeSkillName)) return 0;
    if (string.IsNullOrWhiteSpace(yakuName)) return 0;

    string norm = NormalizeTraitProgressYakuKey_Local(yakuName);
    if (string.IsNullOrWhiteSpace(norm)) return 0;

    return PlayerPrefs.GetInt(PrefKey_Level(trait, activeSkillName, norm), 0);
}

public int AddTraitYakuLevel(string activeSkillName, Trait trait, string yakuName, int add = 1)
{
    if (string.IsNullOrWhiteSpace(activeSkillName)) return 0;
    if (string.IsNullOrWhiteSpace(yakuName)) return 0;

    string norm = NormalizeTraitProgressYakuKey_Local(yakuName);
    if (string.IsNullOrWhiteSpace(norm)) return 0;

    var key = PrefKey_Level(trait, activeSkillName, norm);
    int cur = PlayerPrefs.GetInt(key, 0);
    cur = Mathf.Max(0, cur + Mathf.Max(0, add));
    PlayerPrefs.SetInt(key, cur);
    PlayerPrefs.Save();
    return cur;
}

public YakuDifficulty GetDifficultyForYaku(string yakuName)
{
    if (string.IsNullOrWhiteSpace(yakuName)) return YakuDifficulty.Normal;

    var norm = NormalizeTraitProgressYakuKey_Local(yakuName);

    var hit = traitMap.FirstOrDefault(t => t != null &&
        !string.IsNullOrWhiteSpace(t.yakuName) &&
        string.Equals(
            NormalizeTraitProgressYakuKey_Local(t.yakuName),
            norm,
            StringComparison.OrdinalIgnoreCase));

    return hit != null ? hit.difficulty : YakuDifficulty.Normal;
}

private List<string> GetUnlockedList(Trait trait, string activeSkillName)
{
    var csv = PlayerPrefs.GetString(PrefKey_Unlocked(trait, activeSkillName), "");
    if (string.IsNullOrEmpty(csv)) return new List<string>();
    return csv.Split(',')
        .Select(s => (s ?? "").Trim())
        .Where(s => !string.IsNullOrEmpty(s))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}

private void SetUnlockedList(Trait trait, string activeSkillName, List<string> list)
{
    var norm = (list ?? new List<string>())
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Select(s => s.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    PlayerPrefs.SetString(PrefKey_Unlocked(trait, activeSkillName), string.Join(",", norm));
    PlayerPrefs.Save();
}
// SkillSetAsset.cs に追加
public void ResetTraitYakuProgressForActiveSkill(string activeSkillName)
{
    if (string.IsNullOrWhiteSpace(activeSkillName)) return;

    // ① 解放済みリストを消す（既存のキー生成関数 PrefKey_Unlocked を使用）
    try { PlayerPrefs.DeleteKey(PrefKey_Unlocked(Trait.Geki, activeSkillName)); } catch {}
    try { PlayerPrefs.DeleteKey(PrefKey_Unlocked(Trait.Shun, activeSkillName)); } catch {}
    try { PlayerPrefs.DeleteKey(PrefKey_Unlocked(Trait.Iyu,  activeSkillName)); } catch {}

    // ② レベル（強化）を消す：GetTraitYakuFor は (ge, sh, iy) タプルなので、それぞれ回す
    try
    {
        var map = GetTraitYakuFor(activeSkillName); // (List<string> ge, List<string> sh, List<string> iy)

        void ClearLevelsForList(Trait trait, List<string> list)
        {
            if (list == null) return;
            foreach (var y in list)
            {
                if (string.IsNullOrWhiteSpace(y)) continue;
                try { PlayerPrefs.DeleteKey(PrefKey_Level(trait, activeSkillName, y.Trim())); } catch {}
            }
        }

        ClearLevelsForList(Trait.Geki, map.ge);
        ClearLevelsForList(Trait.Shun, map.sh);
        ClearLevelsForList(Trait.Iyu,  map.iy);
    }
    catch {}

    try { PlayerPrefs.Save(); } catch {}
}

public void ResetAllTraitYakuProgress()
{
    // この SkillSetAsset に登録されている activeSkills を列挙してリセット
    try
    {
        if (activeSkills == null) return;

        foreach (var s in activeSkills)
        {
            if (s == null) continue;
            if (string.IsNullOrWhiteSpace(s.activeSkillName)) continue;
            ResetTraitYakuProgressForActiveSkill(s.activeSkillName);
        }
    }
    catch {}

    try { PlayerPrefs.Save(); } catch {}
}

}
