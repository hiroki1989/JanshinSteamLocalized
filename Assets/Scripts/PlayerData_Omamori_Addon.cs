using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// お守りシステム：保有・装備・ロール・効果集約（PlayerPrefsベース）
/// 既存の PlayerData を壊さないよう partial にする。
/// </summary>
public static partial class PlayerData
{
    
// ====== 公開API（EquipManagerやゲーム側から使う） ======
public static HashSet<int> OwnedOmamori
{
    get => LoadOwnedHash();
    set => SaveOwnedHash(value);
}

// ★追加：最大所持数
public const int MaxOwnedOmamori = 20;
public static bool DiscardOwnedOmamori(int id)
{
    if (id == 0) return false;

    var owned = LoadOwnedHash();
    if (!owned.Remove(id)) return false;

    SaveOwnedHash(owned);

    // 破棄したものが装備中なら外す（複数装備対応）
    if (EquippedOmamori == id)
        EquippedOmamori = 0;

    var equipped = LoadEquippedOmamoriIds();
    bool removed = equipped.RemoveAll(x => x == id) > 0;
    if (removed)
        SaveEquippedOmamoriIds(equipped);

    // 個別データも削除（任意だが、残すメリットがないので削除）
    PlayerPrefs.DeleteKey($"Omamori_{id}");
    PlayerPrefs.Save();
    return true;
}

private static LocalizationManager.Language GetOmamoriLanguage_Local()
{
    if (LocalizationManager.Instance != null)
        return LocalizationManager.Instance.CurrentLanguage;

    return LocalizationManager.Language.Japanese;
}

private static string RarityToLocalized_Local(OmamoriRarity r)
{
    string key = r.ToString();
    string localized = LocalizationManager.Rarity(key);

    if (string.IsNullOrEmpty(localized))
        return RarityToJp(r);

    string keyLike = "rarity." + key;
    if (string.Equals(localized, keyLike, StringComparison.Ordinal))
        return RarityToJp(r);

    return localized;
}
private static string EffectToLocalized_Local(OmamoriEffect e)
{
    switch (GetOmamoriLanguage_Local())
    {
        case LocalizationManager.Language.English:
            switch (e)
            {
                case OmamoriEffect.MaxHPPercentUp:         return "Max HP Up";
                case OmamoriEffect.MaxMPPercentUp:         return "Max MP Up";
                case OmamoriEffect.DamageTakenPercentDown: return "Damage Taken Down";
                case OmamoriEffect.SkillMpCostPercentDown: return "Skill MP Cost Down";
                case OmamoriEffect.MpRegenPercentUp:       return "MP Regen Up Per Turn";
                case OmamoriEffect.IyuHealPercentUp:       return "癒 Up";
                case OmamoriEffect.GekiDamagePercentUp:    return "撃 Up";
                case OmamoriEffect.ShunAddPercentUp:       return "瞬 Up";
                case OmamoriEffect.StartMPFlatAdd:         return "Start Battle MP Up";
                case OmamoriEffect.WinMPFlatAdd:           return "Win MP Up";
                case OmamoriEffect.DamageDealtPercentUp:   return "Damage Dealt Up";
                default:                                   return e.ToString();
            }

        case LocalizationManager.Language.ChineseSimplified:
            switch (e)
            {
                case OmamoriEffect.MaxHPPercentUp:         return "最大HP提升";
                case OmamoriEffect.MaxMPPercentUp:         return "最大MP提升";
                case OmamoriEffect.DamageTakenPercentDown: return "所受伤害降低";
                case OmamoriEffect.SkillMpCostPercentDown: return "技能MP消耗降低";
                case OmamoriEffect.MpRegenPercentUp:       return "每回合MP回复提升";
                case OmamoriEffect.IyuHealPercentUp:       return "癒提升";
                case OmamoriEffect.GekiDamagePercentUp:    return "撃提升";
                case OmamoriEffect.ShunAddPercentUp:       return "瞬提升";
                case OmamoriEffect.StartMPFlatAdd:         return "开局MP提升";
                case OmamoriEffect.WinMPFlatAdd:           return "和了时MP提升";
                case OmamoriEffect.DamageDealtPercentUp:   return "造成伤害提升";
                default:                                   return e.ToString();
            }

        case LocalizationManager.Language.Japanese:
        default:
            return EffectToJp(e);
    }
}
private static string BuildOmamoriEffectLine_Local(EffectEntry e)
{
    string valueText = BuildOmamoriEffectValueText_Local(e);

    if (string.IsNullOrEmpty(valueText))
        return $"- {EffectToLocalized_Local(e.type)}";

    return $"- {EffectToLocalized_Local(e.type)} +{valueText}";
}

private static string BuildOmamoriEffectValueText_Local(EffectEntry e)
{
    float pctRounded = Mathf.Round(e.amountPercent * 100f) / 100f;

    switch (e.type)
    {
        case OmamoriEffect.StartMPFlatAdd:
        case OmamoriEffect.WinMPFlatAdd:
            return Mathf.RoundToInt(pctRounded * 100f).ToString();

        case OmamoriEffect.MaxHPPercentUp:
        case OmamoriEffect.MaxMPPercentUp:
        case OmamoriEffect.DamageTakenPercentDown:
        case OmamoriEffect.SkillMpCostPercentDown:
        case OmamoriEffect.MpRegenPercentUp:
        case OmamoriEffect.IyuHealPercentUp:
        case OmamoriEffect.GekiDamagePercentUp:
        case OmamoriEffect.ShunAddPercentUp:
        case OmamoriEffect.DamageDealtPercentUp:
        default:
            return ToPct(pctRounded);
    }
}
private static string SpecialPrefixLocalized_Local()
{
    switch (GetOmamoriLanguage_Local())
    {
        case LocalizationManager.Language.English:
            return "-Special ";
        case LocalizationManager.Language.ChineseSimplified:
            return "-特殊 ";
        case LocalizationManager.Language.Japanese:
        default:
            return "-特殊 ";
    }
}

private static string SpecialToLocalized_Local(OmamoriSpecial s)
{
    switch (GetOmamoriLanguage_Local())
    {
        case LocalizationManager.Language.English:
            switch (s)
            {
                case OmamoriSpecial.YakumanNext3WinsDouble: return "After a Yakuman, the next 3 winning damages are doubled";
                case OmamoriSpecial.ExtraDoraOnKan:         return "When declaring a Kan, add 1 extra Dora indicator";
                default:                                    return "";
            }

        case LocalizationManager.Language.ChineseSimplified:
            switch (s)
            {
                case OmamoriSpecial.YakumanNext3WinsDouble: return "役满后，接下来3次和了伤害变为2倍";
                case OmamoriSpecial.ExtraDoraOnKan:         return "杠时，追加1张宝牌指示牌";
                default:                                    return "";
            }

        case LocalizationManager.Language.Japanese:
        default:
            return SpecialToJp(s);
    }
}
private static string UniqueEffectToLocalized_Local(UniqueOmamoriEffectKind k)
{
    switch (GetOmamoriLanguage_Local())
    {
        case LocalizationManager.Language.English:
            switch (k)
            {
                case UniqueOmamoriEffectKind.Amaterasu_HpPlus10000: return "HP +10000";
                case UniqueOmamoriEffectKind.Susanoo_MpPlus10000: return "MP +10000";
                case UniqueOmamoriEffectKind.Bastet_MpCostHalf: return "MP cost reduced by 50%";
                case UniqueOmamoriEffectKind.Shiva_East1_PlayerDamageDown50: return "In East 1, damage to the player is reduced by 50%";
                case UniqueOmamoriEffectKind.Anubis_East1_EnemyDamageUp50: return "In East 1, damage to the enemy is increased by 50%";
                case UniqueOmamoriEffectKind.Freyja_SkillCastsPlus2: return "Skill uses per turn +2";
                case UniqueOmamoriEffectKind.Poseidon_MpRegenDouble: return "MP regeneration each turn is doubled";
                case UniqueOmamoriEffectKind.Odin_DisableEnemySkills: return "Disable enemy skills";
                case UniqueOmamoriEffectKind.Luna_Heal2PctPerTurn: return "Recover 2% of max HP each turn";
                case UniqueOmamoriEffectKind.Zeus_DamageUp30: return "Damage to enemies +30%";
case UniqueOmamoriEffectKind.Hades_DyeMaster: return "When the Dyer skill is activated, the selected tile is converted into the tile with the same number in the suit that is most common in your hand";
case UniqueOmamoriEffectKind.Hades_Calligrapher: return "When the Calligrapher skill is activated, the selected tile is converted into the honor tile that is most common in your hand";
case UniqueOmamoriEffectKind.Hades_Capitalist: return "When Capitalist is equipped, Gold gained from a win is multiplied by 1.5";
                default: return "";
            }

        case LocalizationManager.Language.ChineseSimplified:
            switch (k)
            {
                case UniqueOmamoriEffectKind.Amaterasu_HpPlus10000: return "HP提升10000";
                case UniqueOmamoriEffectKind.Susanoo_MpPlus10000: return "MP提升10000";
                case UniqueOmamoriEffectKind.Bastet_MpCostHalf: return "MP消耗减少50%";
                case UniqueOmamoriEffectKind.Shiva_East1_PlayerDamageDown50: return "东1局玩家受到的伤害减少50%";
                case UniqueOmamoriEffectKind.Anubis_East1_EnemyDamageUp50: return "东1局对敌人造成的伤害提升50%";
                case UniqueOmamoriEffectKind.Freyja_SkillCastsPlus2: return "每回合可使用技能次数+2";
                case UniqueOmamoriEffectKind.Poseidon_MpRegenDouble: return "每回合MP回复量变为2倍";
                case UniqueOmamoriEffectKind.Odin_DisableEnemySkills: return "敌人的技能无效化";
                case UniqueOmamoriEffectKind.Luna_Heal2PctPerTurn: return "每回合恢复最大HP的2%";
                case UniqueOmamoriEffectKind.Zeus_DamageUp30: return "对敌人造成的伤害提升30%";
case UniqueOmamoriEffectKind.Hades_DyeMaster: return "发动染色师技能时，将选中的牌转换为手牌中数量最多花色的同数字牌";
case UniqueOmamoriEffectKind.Hades_Calligrapher: return "发动书家技能时，将选中的牌转换为手牌中数量最多的字牌";
case UniqueOmamoriEffectKind.Hades_Capitalist: return "装备资本家时，和了获得的Gold变为1.5倍";
                default: return "";
            }

        case LocalizationManager.Language.Japanese:
        default:
            return UniqueEffectToText(k);
    }
}
private static string BuildUniqueOmamoriTitle_Local(string enemyName, int level, bool includeEffectCount, int effectCount)
{
    string displayEnemyName = enemyName;

    if (string.IsNullOrEmpty(displayEnemyName))
    {
        switch (GetOmamoriLanguage_Local())
        {
            case LocalizationManager.Language.English:
                displayEnemyName = "Enemy";
                break;

            case LocalizationManager.Language.ChineseSimplified:
                displayEnemyName = "敌人";
                break;

            case LocalizationManager.Language.Japanese:
            default:
                displayEnemyName = "敵";
                break;
        }
    }
    else
    {
        try
        {
            if (LocalizationManager.Instance != null)
            {
                string localized = LocalizationManager.Instance.GetEnemyDisplayName(displayEnemyName);
                if (!string.IsNullOrEmpty(localized))
                    displayEnemyName = localized;
            }
        }
        catch
        {
        }
    }

    switch (GetOmamoriLanguage_Local())
    {
        case LocalizationManager.Language.English:
            return $"<color=#FF0000>[Relic of {displayEnemyName}]</color> Lv.{level}";

        case LocalizationManager.Language.ChineseSimplified:
            return $"<color=#FF0000>【{displayEnemyName}的神器】</color>　Lv.{level}";

        case LocalizationManager.Language.Japanese:
        default:
            return $"<color=#FF0000>【{displayEnemyName}の神器】</color>　Lv.{level}";
    }
}
public static bool TryGetOmamoriRarityKey(int id, out string rarityKey)
{
    rarityKey = "";
    if (id == 0) return false;
    if (!TryDecode(id, out var o)) return false;

    rarityKey = o.rarity.ToString();
    return !string.IsNullOrEmpty(rarityKey);
}
public static string GetOmamoriName_RewardUI_Localized(int id)
{
    if (id == 0)
    {
        switch (GetOmamoriLanguage_Local())
        {
            case LocalizationManager.Language.English:
                return "No Reward";

            case LocalizationManager.Language.ChineseSimplified:
                return "无奖励";

            case LocalizationManager.Language.Japanese:
            default:
                return "報酬なし";
        }
    }

    if (!TryDecode(id, out var o))
    {
        switch (GetOmamoriLanguage_Local())
        {
            case LocalizationManager.Language.English:
                return $"Unknown Omamori #{id}";

            case LocalizationManager.Language.ChineseSimplified:
                return $"未知御守#{id}";

            case LocalizationManager.Language.Japanese:
            default:
                return $"未知のお守り#{id}";
        }
    }

    if (o.isUnique)
    {
        return BuildUniqueOmamoriTitle_Local(
            o.uniqueEnemyName,
            o.level,
            false,
            o.effects != null ? o.effects.Count : 0
        );
    }

    string rarityText = RarityToLocalized_Local(o.rarity);

    switch (GetOmamoriLanguage_Local())
    {
        case LocalizationManager.Language.English:
            return $"{rarityText}  Lv.{o.level}"
                 + (o.special != OmamoriSpecial.None ? " + Special" : "");

        case LocalizationManager.Language.ChineseSimplified:
            return $"{rarityText}　Lv.{o.level}"
                 + (o.special != OmamoriSpecial.None ? " + 特殊" : "");

        case LocalizationManager.Language.Japanese:
        default:
            return $"{rarityText}　Lv.{o.level}"
                 + (o.special != OmamoriSpecial.None ? " + 特殊" : "");
    }
}
public static string GetOmamoriEffectsOnlyText_RewardUI_Localized(int id, bool includeSpecial = true)
{
    if (id == 0) return "";
    if (!TryDecode(id, out var o)) return "";

    var lines = new List<string>();

    if (o.effects != null)
    {
        foreach (var e in o.effects)
            lines.Add(BuildOmamoriEffectLine_Local(e));
    }

    if (includeSpecial && o.special != OmamoriSpecial.None)
        lines.Add($"{SpecialPrefixLocalized_Local()}{SpecialToLocalized_Local(o.special)}");

    if (o.isUnique)
    {
        string u = UniqueEffectToLocalized_Local(o.uniqueKind);
        if (!string.IsNullOrEmpty(u))
            lines.Add($"<color=#FF0000>{u}</color>");
    }

    return string.Join("\n", lines);
}

public static string GetOmamoriName_RewardUI(int id)
{
    return GetOmamoriName_RewardUI_Localized(id);
}

public static string GetOmamoriEffectsOnlyText_RewardUI(int id, bool includeSpecial = true)
{
    return GetOmamoriEffectsOnlyText_RewardUI_Localized(id, includeSpecial);
}

private static string GetOmamoriUnequippedText_Local()
{
    switch (GetOmamoriLanguage_Local())
    {
        case LocalizationManager.Language.English:
            return "Unequipped";

        case LocalizationManager.Language.ChineseSimplified:
            return "未装备";

        case LocalizationManager.Language.Japanese:
        default:
            return "未装備";
    }
}

private static string GetUnknownOmamoriText_Local(int id)
{
    switch (GetOmamoriLanguage_Local())
    {
        case LocalizationManager.Language.English:
            return $"Unknown Omamori #{id}";

        case LocalizationManager.Language.ChineseSimplified:
            return $"未知御守#{id}";

        case LocalizationManager.Language.Japanese:
        default:
            return $"未知のお守り#{id}";
    }
}

private static string GetSpecialSuffix_Local()
{
    switch (GetOmamoriLanguage_Local())
    {
        case LocalizationManager.Language.English:
            return " + Special";

        case LocalizationManager.Language.ChineseSimplified:
            return " + 特殊";

        case LocalizationManager.Language.Japanese:
        default:
            return " + 特殊";
    }
}

private static string GetEffectCountLabel_Local(int count)
{
    switch (GetOmamoriLanguage_Local())
    {
        case LocalizationManager.Language.English:
            return $"(Effects {count})";

        case LocalizationManager.Language.ChineseSimplified:
            return $"（效果{count}）";

        case LocalizationManager.Language.Japanese:
        default:
            return $"（効果{count}）";
    }
}

public static string GetOmamoriName_Localized(int id)
{
    if (id == 0) return GetOmamoriUnequippedText_Local();
    if (!TryDecode(id, out var o)) return GetUnknownOmamoriText_Local(id);

    if (o.isUnique)
    {
        return BuildUniqueOmamoriTitle_Local(
            o.uniqueEnemyName,
            o.level,
            false,
            o.effects != null ? o.effects.Count : 0
        );
    }

    string rarityText = RarityToLocalized_Local(o.rarity);

    switch (GetOmamoriLanguage_Local())
    {
        case LocalizationManager.Language.English:
            return $"{rarityText} Lv.{o.level}"
                 + (o.special != OmamoriSpecial.None ? GetSpecialSuffix_Local() : "");

        case LocalizationManager.Language.ChineseSimplified:
            return $"{rarityText} Lv.{o.level}"
                 + (o.special != OmamoriSpecial.None ? GetSpecialSuffix_Local() : "");

        case LocalizationManager.Language.Japanese:
        default:
            return $"{rarityText} Lv.{o.level}"
                 + (o.special != OmamoriSpecial.None ? GetSpecialSuffix_Local() : "");
    }
}
public static string GetOmamoriDesc_Localized(int id)
{
    if (id == 0) return GetOmamoriUnequippedText_Local();
    if (!TryDecode(id, out var o)) return GetUnknownOmamoriText_Local(id);

    var lines = new List<string>();
    lines.Add(GetOmamoriName_Localized(id));

    if (o.effects != null)
    {
        foreach (var e in o.effects)
            lines.Add(BuildOmamoriEffectLine_Local(e));
    }

    if (o.special != OmamoriSpecial.None)
    {
        switch (GetOmamoriLanguage_Local())
        {
            case LocalizationManager.Language.English:
                lines.Add($"[Special] {SpecialToLocalized_Local(o.special)}");
                break;

            case LocalizationManager.Language.ChineseSimplified:
                lines.Add($"[特殊] {SpecialToLocalized_Local(o.special)}");
                break;

            case LocalizationManager.Language.Japanese:
            default:
                lines.Add($"[特殊] {SpecialToLocalized_Local(o.special)}");
                break;
        }
    }

    if (o.isUnique)
    {
        string u = UniqueEffectToLocalized_Local(o.uniqueKind);
        if (!string.IsNullOrEmpty(u))
        {
            lines.Add($"<color=#FF0000>{u}</color>");
        }
    }

    return string.Join("\n", lines);
}
public static string GetOmamoriText_EquipUI_Localized(int id, bool includeSpecial = true)
{
    if (id == 0) return GetOmamoriUnequippedText_Local();
    if (!TryDecode(id, out var o)) return GetUnknownOmamoriText_Local(id);

    var lines = new List<string>();

    if (o.isUnique)
    {
        lines.Add(BuildUniqueOmamoriTitle_Local(
            o.uniqueEnemyName,
            o.level,
            false,
            o.effects != null ? o.effects.Count : 0
        ));
    }
    else
    {
        string rarityText = RarityToLocalized_Local(o.rarity);
        string hex = ColorUtility.ToHtmlStringRGBA(RarityToColor_EquipUI(o.rarity));
        lines.Add($"<color=#{hex}>{rarityText}</color>　Lv.{o.level}");
    }

    if (o.effects != null)
    {
        foreach (var e in o.effects)
            lines.Add(BuildOmamoriEffectLine_Local(e));
    }

    if (includeSpecial && o.special != OmamoriSpecial.None)
        lines.Add($"{SpecialPrefixLocalized_Local()}{SpecialToLocalized_Local(o.special)}");

    if (o.isUnique)
    {
        string u = UniqueEffectToLocalized_Local(o.uniqueKind);
        if (!string.IsNullOrEmpty(u))
            lines.Add($"<color=#FF0000>{u}</color>");
    }

    return string.Join("\n", lines);
}
    public static int EquippedOmamori
    {
        get => PlayerPrefs.GetInt(KeyEquipped, 0);
        set => PlayerPrefs.SetInt(KeyEquipped, value);
    }

    public static List<int> EquippedOmamoriIds
    {
        get => LoadEquippedOmamoriIds();
        set => SaveEquippedOmamoriIds(value);
    }
public static int LastGrantedOmamoriId => PlayerPrefs.GetInt(KeyLastGranted, 0);
public enum UniqueOmamoriEffectKind
{
    None = 0,

    Amaterasu_HpPlus10000,
    Susanoo_MpPlus10000,
    Bastet_MpCostHalf,
    Shiva_East1_PlayerDamageDown50,
    Anubis_East1_EnemyDamageUp50,
    Freyja_SkillCastsPlus2,
    Poseidon_MpRegenDouble,
    Odin_DisableEnemySkills,
    Luna_Heal2PctPerTurn,
    Zeus_DamageUp30,
    Hades_DyeMaster,
    Hades_Calligrapher,
    Hades_Capitalist
}
static readonly string KeyEquippedIds = "EquippedOmamoriIdsV1";

static List<int> LoadEquippedOmamoriIds()
{
    var list = new List<int>();

    string raw = "";
    try { raw = PlayerPrefs.GetString(KeyEquippedIds, ""); } catch { raw = ""; }

    if (!string.IsNullOrEmpty(raw))
    {
        var seen = new HashSet<int>();
        foreach (var s in raw.Split(','))
        {
            if (!int.TryParse(s, out var id)) continue;
            id = Mathf.Max(0, id);
            if (id <= 0) continue;
            if (seen.Add(id)) list.Add(id);
        }
    }

    // 旧単一装備との互換
    try
    {
        int single = EquippedOmamori;
        if (single > 0 && !list.Contains(single))
            list.Insert(0, single);
    }
    catch { }

    return list;
}

static void SaveEquippedOmamoriIds(IEnumerable<int> ids)
{
    var list = new List<int>();
    var seen = new HashSet<int>();

    if (ids != null)
    {
        foreach (var rawId in ids)
        {
            int id = Mathf.Max(0, rawId);
            if (id <= 0) continue;
            if (seen.Add(id)) list.Add(id);
        }
    }

    string csv = list.Count == 0 ? "" : string.Join(",", list);
    PlayerPrefs.SetString(KeyEquippedIds, csv);

    // 旧単一装備との互換
    PlayerPrefs.SetInt(KeyEquipped, list.Count > 0 ? list[0] : 0);
    PlayerPrefs.Save();
}

static IEnumerable<int> EnumerateEquippedOmamoriIds()
{
    var yielded = new HashSet<int>();

    var list = LoadEquippedOmamoriIds();
    for (int i = 0; i < list.Count; i++)
    {
        int id = Mathf.Max(0, list[i]);
        if (id <= 0) continue;
        if (yielded.Add(id)) yield return id;
    }

    int single = 0;
    try { single = EquippedOmamori; } catch { single = 0; }
    single = Mathf.Max(0, single);

    if (single > 0 && yielded.Add(single))
        yield return single;
}
public static bool IsEquippedUniqueEffect(UniqueOmamoriEffectKind kind)
{
    if (kind == UniqueOmamoriEffectKind.None) return false;

    foreach (var id in EnumerateEquippedOmamoriIds())
    {
        if (id == 0) continue;
        if (!TryDecode(id, out var o)) continue;
        if (o == null) continue;

        if (o.isUnique && o.uniqueKind == kind)
            return true;
    }

    return false;
}
public static int GrantUniqueOmamori(string enemyName, UniqueOmamoriEffectKind kind, int level)
{
    if (kind == UniqueOmamoriEffectKind.None) return -1;

    var owned = LoadOwnedHash();
    if (owned.Count >= MaxOwnedOmamori) return -1;

    // ユニークお守りでも、効果ロール前に必ずExcel/既定値を初期化する
    if (!OmamoriExcelV2.TryEnsureLoaded())
        OmamoriExcelV2.UseBuiltInDefaults();

    // ★神器レベル：通常お守りと同じレベル設定にする
    //   引数 level が 1 以下（未指定/旧呼び出し）の場合は、
    //   通常お守りと同じく「このRunで倒した人数 ± ランダムオフセット」で算出する。
    int lv;
    if (level >= 2)
    {
        // 呼び出し側が明示的にレベルを渡してきた場合はそれを尊重
        lv = Mathf.Max(1, level);
    }
    else
    {
        int defeatedThisRun = 0;
        try { defeatedThisRun = Mathf.Max(0, PlayerPrefs.GetInt("Run_DefeatedEnemyCount", 0)); } catch { defeatedThisRun = 0; }

        int lvForLottery = defeatedThisRun;
        try
        {
            int randomOffset = UnityEngine.Random.Range(-2, 3); // -2,-1,0,1,2（通常お守りと同じ）
            lvForLottery = defeatedThisRun + randomOffset;
        }
        catch
        {
            lvForLottery = defeatedThisRun;
        }

        lv = Mathf.Max(1, lvForLottery);
    }

    // 特殊効果に加えて付与する通常効果は3つ
    var effects = OmamoriExcelV2.RollEffects(3, lv);

    var inst = new OmamoriInstance
    {
        rarity = OmamoriRarity.Legendary,
        level = lv,
        effects = effects,
        special = OmamoriSpecial.None,

        isUnique = true,
        uniqueEnemyName = (enemyName ?? "").Trim(),
        uniqueKind = kind
    };

    int id = EncodeAndPersist(inst);

    if (!owned.Contains(id))
    {
        owned.Add(id);
        SaveOwnedHash(owned);
    }

    // ★実績：神器入手（ユニークお守り）
    try { AchievementSystem.NotifyShinkiObtained(); } catch { }

    // ★実績：レジェンダリーお守り（ユニークは常にレジェ扱い）
    try { AchievementSystem.NotifyLegendaryOmamoriObtained(); } catch { }

    // 報酬画面表示用
    PlayerPrefs.SetInt(KeyLastGranted, id);
    PlayerPrefs.Save();

    return id;
}
public static bool TryGetOmamori(int id, out OmamoriInstance inst)
{
    inst = null;
    return TryDecode(id, out inst);
}
private static string UniqueEffectToText(UniqueOmamoriEffectKind k)
{
    switch (k)
    {
        case UniqueOmamoriEffectKind.Amaterasu_HpPlus10000: return "HP10000上昇";
        case UniqueOmamoriEffectKind.Susanoo_MpPlus10000: return "MP10000上昇";
        case UniqueOmamoriEffectKind.Bastet_MpCostHalf: return "MP消費量50％減少";
        case UniqueOmamoriEffectKind.Shiva_East1_PlayerDamageDown50: return "東1局のプレイヤーへのダメージ50％減少";
        case UniqueOmamoriEffectKind.Anubis_East1_EnemyDamageUp50: return "東1局の敵へのダメージ50％上昇";
        case UniqueOmamoriEffectKind.Freyja_SkillCastsPlus2: return "1ターンで使用できるスキル回数＋２";
        case UniqueOmamoriEffectKind.Poseidon_MpRegenDouble: return "毎ターンのMP回復量2倍";
        case UniqueOmamoriEffectKind.Odin_DisableEnemySkills: return "敵のスキルを無効化";
        case UniqueOmamoriEffectKind.Luna_Heal2PctPerTurn: return "毎ターン最大HPの２％回復";
        case UniqueOmamoriEffectKind.Zeus_DamageUp30: return "敵へのダメージ30％上昇";

        case UniqueOmamoriEffectKind.Hades_DyeMaster:
            return "染色師のスキルを発動すると、選択した牌を手牌で最も多い色の同数字の牌に変換する";
        case UniqueOmamoriEffectKind.Hades_Calligrapher:
            return "書家のスキルを発動すると、選択した牌を手牌で最も多い字牌に変換する";
        case UniqueOmamoriEffectKind.Hades_Capitalist:
            return "資産家が装備すると、和了によるGold取得量が1.5倍になる";
        default: return "";
    }
}
    public static string GetOmamoriName(int id)
    {
        if (id == 0) return "未装備";
        if (!TryDecode(id, out var o)) return $"未知のお守り#{id}";

        // ★ユニーク：レア度表記は「【XXの神器】」で赤表示
        if (o.isUnique)
        {
            string en = string.IsNullOrEmpty(o.uniqueEnemyName) ? "敵" : o.uniqueEnemyName;
            return $"<color=#FF0000>【{en}の神器】</color> Lv.{o.level}（効果{(o.effects?.Count ?? 0)}）";
        }

        return $"{RarityToJp(o.rarity)} Lv.{o.level}（効果{(o.effects?.Count ?? 0)}）"
             + (o.special != OmamoriSpecial.None ? " + 特殊" : "");
    }

    public static string GetOmamoriDesc(int id)
    {
        if (id == 0) return "お守りは未装備です。";
        if (!TryDecode(id, out var o)) return "不明なお守り";

        var lines = new List<string>();
        lines.Add(GetOmamoriName(id));

        if (o.effects != null)
        {
            foreach (var e in o.effects)
                lines.Add($"- {EffectToJp(e.type)} +{ToPct(e.amountPercent)}");
        }
        if (o.special != OmamoriSpecial.None)
            lines.Add($"[特殊] {SpecialToJp(o.special)}");

        // ★ユニーク：固有効果を赤字で追記
        if (o.isUnique)
        {
            string u = UniqueEffectToText(o.uniqueKind);
            if (!string.IsNullOrEmpty(u))
            {
                lines.Add($"<color=#FF0000>{u}</color>");
            }
        }

        return string.Join("\n", lines);
    }
    public static OmamoriStats GetEquippedStats()
    {
        var total = default(OmamoriStats);

        foreach (var id in EnumerateEquippedOmamoriIds())
        {
            if (id == 0) continue;
            if (!TryDecode(id, out var o)) continue;
            if (o == null) continue;

            var s = Accumulate(o);
            total.maxHpUp += s.maxHpUp;
            total.maxMpUp += s.maxMpUp;
            total.dmgTakenDown += s.dmgTakenDown;
            total.skillMpCostDown += s.skillMpCostDown;
            total.mpRegenUp += s.mpRegenUp;
            total.iyuHealUp += s.iyuHealUp;
            total.gekiDmgUp += s.gekiDmgUp;
            total.shunAddUp += s.shunAddUp;
            total.startMpAdd += s.startMpAdd;
            total.winMpAdd += s.winMpAdd;
            total.dmgDealtUp += s.dmgDealtUp;
        }

        return total;
    }
    public static bool EquippedHasSpecial(OmamoriSpecial s)
    {
        foreach (var id in EnumerateEquippedOmamoriIds())
        {
            if (id == 0) continue;
            if (!TryDecode(id, out var o)) continue;
            if (o == null) continue;

            if (o.special == s)
                return true;
        }

        return false;
    }
public static int GrantRandomOmamori(int rewardLevel)
{
    var inst = Roll(rewardLevel);
    int id = EncodeAndPersist(inst);

    var owned = LoadOwnedHash();
    if (!owned.Contains(id)) { owned.Add(id); SaveOwnedHash(owned); }

    // ★実績：レジェンダリーお守り
    try
    {
        if (inst != null && inst.rarity == OmamoriRarity.Legendary)
        {
            AchievementSystem.NotifyLegendaryOmamoriObtained();
        }
    }
    catch { }

    // ★追加：今回付与したお守りIDを記録（報酬画面で表示に使う）
    PlayerPrefs.SetInt(KeyLastGranted, id);
    PlayerPrefs.Save();
    return id;
}
    // ====== 内部：データ定義 ======
    public enum OmamoriRarity { Normal, Common, Rare, Epic, Legendary }
    public enum OmamoriEffect
    {
        MaxHPPercentUp,
        MaxMPPercentUp,
        DamageTakenPercentDown,
        SkillMpCostPercentDown,
        MpRegenPercentUp,
        IyuHealPercentUp,
        GekiDamagePercentUp,
        ShunAddPercentUp,
        StartMPFlatAdd,      // 戦闘開始時のMP+固定
WinMPFlatAdd,        // 和了(勝利)時のMP+固定
DamageDealtPercentUp // 与ダメージ+%
    }
    public enum OmamoriSpecial { None, YakumanNext3WinsDouble, ExtraDoraOnKan }

    [Serializable]
    public struct EffectEntry
    {
        public OmamoriEffect type;
        public float amountPercent; // 0.02 = +2%
    }
[Serializable]
public class OmamoriInstance
{
    public OmamoriRarity rarity;
    public int level;                    // >=1
    public List<EffectEntry> effects;    // rarity依存の個数
    public OmamoriSpecial special;       // Legendaryのみ付与

    // ★ユニーク（神器）
    public bool isUnique = false;
    public string uniqueEnemyName = "";
    public UniqueOmamoriEffectKind uniqueKind = UniqueOmamoriEffectKind.None;
}

    public struct OmamoriStats
    {
        public float maxHpUp;        // +%
        public float maxMpUp;        // +%
        public float dmgTakenDown;   // -%
        public float skillMpCostDown;// -%
        public float mpRegenUp;      // +%
        public float iyuHealUp;      // +%
        public float gekiDmgUp;      // +%
        public float shunAddUp;      // +%
        public int   startMpAdd;  // 開始時MPの固定加算
public int   winMpAdd;    // 勝利時MPの固定加算
public float dmgDealtUp;  // 与ダメージ増加(割合 0.10 = +10%)
    }
static OmamoriInstance Roll(int rewardLevel)
{
    // Excel（v2）設定を読み込み（なければ従来値でフォールバック）
    if (!OmamoriExcelV2.TryEnsureLoaded())
        OmamoriExcelV2.UseBuiltInDefaults(); // 安全網

    // GameManager から渡された「最終的な報酬レベル」をそのまま使う
    int level = Mathf.Max(1, rewardLevel);
    Debug.Log($"[OmamoriDebug] Roll start. rewardLevel={rewardLevel} level={level}");

    // レア度のドリフトも、現在の報酬レベルを基準にする
    var rarity = OmamoriExcelV2.RollRarity(level);
    Debug.Log($"[OmamoriDebug] RollRarity result. level={level} rarity={rarity}");

    // レア度ごとの効果個数
    int effectsCount = OmamoriExcelV2.GetEffectCount(rarity);
    Debug.Log($"[OmamoriDebug] GetEffectCount result. rarity={rarity} effectsCount={effectsCount}");

    // 効果プールから重み付きで effectsCount 個を抽選（効果ごとにInspectorスケールで%決定）
    var chosen = OmamoriExcelV2.RollEffects(effectsCount, level);
    Debug.Log($"[OmamoriDebug] RollEffects result. chosenCount={(chosen != null ? chosen.Count : 0)}");

    if (chosen != null)
    {
        for (int i = 0; i < chosen.Count; i++)
        {
            Debug.Log($"[OmamoriDebug] ChosenEffect[{i}] type={chosen[i].type} amountPercent={chosen[i].amountPercent}");
        }
    }

    // 特殊効果は廃止（Legendaryでも付与しない）
    var special = OmamoriSpecial.None;

    return new OmamoriInstance {
        rarity  = rarity,
        level   = level,
        effects = chosen,
        special = special
    };
}
    static OmamoriRarity RollRarity(int defeated)
    {
        // 初期分布
        float normal = 50f, common = 20f, rare = 10f, epic = 9f, leg = 1f;
        // 倒すごとに Epic/Legend +0.1、Normal/Common -0.1
        float delta = 0.1f * Mathf.Max(0, defeated);
        epic += delta; leg += delta;
        normal = Mathf.Max(0f, normal - delta);
        common = Mathf.Max(0f, common - delta);

        float sum = normal + common + rare + epic + leg;
        float r = UnityEngine.Random.Range(0f, sum);
        if ((r -= normal) < 0f) return OmamoriRarity.Normal;
        if ((r -= common) < 0f) return OmamoriRarity.Common;
        if ((r -= rare)   < 0f) return OmamoriRarity.Rare;
        if ((r -= epic)   < 0f) return OmamoriRarity.Epic;
        return OmamoriRarity.Legendary;
    }

    static OmamoriSpecial RollLegendarySpecial()
    {
        // Excelの例を実装
        var candidates = new List<OmamoriSpecial> {
            OmamoriSpecial.YakumanNext3WinsDouble,
            OmamoriSpecial.ExtraDoraOnKan
        };
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

static OmamoriStats Accumulate(OmamoriInstance o)
{
    var s = default(OmamoriStats);
    if (o?.effects == null) return s;

    foreach (var e in o.effects)
    {
        // ★表示(ToPct)が整数%なので、計算側も 1%刻みに揃える（例: 0.0442 → 0.04）
        float pct = Mathf.Round(e.amountPercent * 100f) / 100f;

        switch (e.type)
        {
            case OmamoriEffect.MaxHPPercentUp:         s.maxHpUp        += pct; break;
            case OmamoriEffect.MaxMPPercentUp:         s.maxMpUp        += pct; break;
            case OmamoriEffect.DamageTakenPercentDown: s.dmgTakenDown   += pct; break;
            case OmamoriEffect.SkillMpCostPercentDown: s.skillMpCostDown+= pct; break;
            case OmamoriEffect.MpRegenPercentUp:       s.mpRegenUp      += pct; break;
            case OmamoriEffect.IyuHealPercentUp:       s.iyuHealUp      += pct; break;
            case OmamoriEffect.GekiDamagePercentUp:    s.gekiDmgUp      += pct; break;
            case OmamoriEffect.ShunAddPercentUp:       s.shunAddUp      += pct; break;

            case OmamoriEffect.StartMPFlatAdd:
                s.startMpAdd += Mathf.RoundToInt(pct * 100f);
                break;

            case OmamoriEffect.WinMPFlatAdd:
                s.winMpAdd   += Mathf.RoundToInt(pct * 100f);
                break;

            case OmamoriEffect.DamageDealtPercentUp:
                s.dmgDealtUp += pct;
                break;
        }
    }
    return s;
}


    // ====== シリアライズ（ID→JSON保持・一覧はID配列） ======
    const string KeyOwned = "OwnedOmamoriIdsV1";
    const string KeyEquipped = "EquippedOmamoriIdV1";
    const string KeyLastGranted = "LastGrantedOmamoriIdV1";
    static readonly List<OmamoriEffect> s_allEffects = new()
    {
        OmamoriEffect.MaxHPPercentUp,
        OmamoriEffect.MaxMPPercentUp,
        OmamoriEffect.DamageTakenPercentDown,
        OmamoriEffect.SkillMpCostPercentDown,
        OmamoriEffect.MpRegenPercentUp,
        OmamoriEffect.IyuHealPercentUp,
        OmamoriEffect.GekiDamagePercentUp,
        OmamoriEffect.ShunAddPercentUp,
        // 末尾に追加
OmamoriEffect.StartMPFlatAdd,
OmamoriEffect.WinMPFlatAdd,
OmamoriEffect.DamageDealtPercentUp
    };

    static int EncodeAndPersist(OmamoriInstance o)
    {
        // IDは簡易に乱数＋時刻から生成（重複しにくい）
        int id = Mathf.Abs(Guid.NewGuid().GetHashCode());
        string json = JsonUtility.ToJson(o);
        PlayerPrefs.SetString($"Omamori_{id}", json);
        return id;
    }

    static bool TryDecode(int id, out OmamoriInstance o)
    {
        o = null;
        string json = PlayerPrefs.GetString($"Omamori_{id}", null);
        if (string.IsNullOrEmpty(json)) return false;
        o = JsonUtility.FromJson<OmamoriInstance>(json);
        return o != null;
    }
static HashSet<int> LoadOwnedHash()
{
    var set = new HashSet<int>();
    var raw = PlayerPrefs.GetString(KeyOwned, "");
    if (string.IsNullOrEmpty(raw)) return set;
    foreach (var s in raw.Split(','))
        if (int.TryParse(s, out var id)) set.Add(id);
    return set;
}

static void SaveOwnedHash(HashSet<int> ids)
{
    string raw = (ids == null || ids.Count == 0) ? "" : string.Join(",", ids);
    PlayerPrefs.SetString(KeyOwned, raw);
    PlayerPrefs.Save();
}


    // ====== 表示ユーティリティ ======
    static string RarityToJp(OmamoriRarity r) => r switch {
        OmamoriRarity.Normal    => "ノーマル",
        OmamoriRarity.Common    => "コモン",
        OmamoriRarity.Rare      => "レア",
        OmamoriRarity.Epic      => "エピック",
        OmamoriRarity.Legendary => "レジェンダリー",
        _ => r.ToString()
    };
    static string EffectToJp(OmamoriEffect e) => e switch {
        OmamoriEffect.MaxHPPercentUp         => "最大HP上昇",
        OmamoriEffect.MaxMPPercentUp         => "最大MP上昇",
        OmamoriEffect.DamageTakenPercentDown => "被ダメージ減少",
        OmamoriEffect.SkillMpCostPercentDown => "スキルMP消費減少",
        OmamoriEffect.MpRegenPercentUp       => "毎ターンMP回復上昇",
        OmamoriEffect.IyuHealPercentUp       => "癒の回復上昇",
        OmamoriEffect.GekiDamagePercentUp    => "撃のダメージ上昇",
        OmamoriEffect.ShunAddPercentUp       => "瞬の加算上昇",
        _ => e.ToString()
    };
    static string SpecialToJp(OmamoriSpecial s) => s switch {
        OmamoriSpecial.YakumanNext3WinsDouble => "役満後、次の3回の和了ダメージ×2",
        OmamoriSpecial.ExtraDoraOnKan         => "カン時、ドラ表示牌+1",
        _ => ""
    };
    static string ToPct(float f) => $"{Mathf.RoundToInt(f * 100f)}%";
 static void Shuffle<T>(IList<T> arr){ for(int i=arr.Count-1;i>0;i--){int j=UnityEngine.Random.Range(0,i+1); (arr[i],arr[j])=(arr[j],arr[i]);} }

public struct RuntimeOmamoriEffectScale
{
    public PlayerData.OmamoriEffect effect;
    public float basePercentAtLevel1;
    public float percentPerLevel;
}

static readonly Dictionary<PlayerData.OmamoriEffect, RuntimeOmamoriEffectScale> _runtimeEffectScales
    = new Dictionary<PlayerData.OmamoriEffect, RuntimeOmamoriEffectScale>();

// ★永続化キー（UpgradeScene を経由せずにお守りをロールしても Inspector 値を使えるようにする）
private const string PrefKey_RuntimeEffectScales = "OmamoriRuntimeEffectScalesV1";
private static bool _runtimeScalesLoadedFromPrefs = false;

public static void ClearRuntimeEffectScales()
{
    _runtimeEffectScales.Clear();
}

public static void SetRuntimeEffectScales(IEnumerable<RuntimeOmamoriEffectScale> entries)
{
    _runtimeEffectScales.Clear();
    if (entries == null) return;

    foreach (var e in entries)
    {
        _runtimeEffectScales[e.effect] = e;
    }

    // ★PlayerPrefs に永続化（アプリ再起動後も UpgradeScene を経由しなくても読み込める）
    try
    {
        var sb = new System.Text.StringBuilder();
        foreach (var kv in _runtimeEffectScales)
        {
            if (sb.Length > 0) sb.Append('|');
            sb.Append((int)kv.Key).Append(',')
              .Append(kv.Value.basePercentAtLevel1.ToString("R")).Append(',')
              .Append(kv.Value.percentPerLevel.ToString("R"));
        }
        UnityEngine.PlayerPrefs.SetString(PrefKey_RuntimeEffectScales, sb.ToString());
        UnityEngine.PlayerPrefs.Save();
        _runtimeScalesLoadedFromPrefs = true;
    }
    catch { }
}

/// <summary>
/// PlayerPrefs から永続化済みのスケール設定を復元する（静的辞書が空のときだけ）
/// </summary>
private static void EnsureRuntimeScalesFromPrefs()
{
    if (_runtimeScalesLoadedFromPrefs) return;
    _runtimeScalesLoadedFromPrefs = true;

    if (_runtimeEffectScales.Count > 0) return;

    try
    {
        string raw = UnityEngine.PlayerPrefs.GetString(PrefKey_RuntimeEffectScales, "");
        if (string.IsNullOrEmpty(raw)) return;

        var parts = raw.Split('|');
        foreach (var part in parts)
        {
            var tokens = part.Split(',');
            if (tokens.Length < 3) continue;

            if (!int.TryParse(tokens[0], out int effectInt)) continue;
            if (!float.TryParse(tokens[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float baseP)) continue;
            if (!float.TryParse(tokens[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float perP)) continue;

            var eff = (PlayerData.OmamoriEffect)effectInt;
            _runtimeEffectScales[eff] = new RuntimeOmamoriEffectScale
            {
                effect = eff,
                basePercentAtLevel1 = baseP,
                percentPerLevel = perP
            };
        }
    }
    catch { }
}

internal static bool TryGetRuntimeEffectScaleForRoll(PlayerData.OmamoriEffect effect, out float baseP, out float perP)
{
    // ★UpgradeScene を経由していない場合に備え、PlayerPrefs から復元を試みる
    EnsureRuntimeScalesFromPrefs();

    if (_runtimeEffectScales.TryGetValue(effect, out var row))
    {
        baseP = row.basePercentAtLevel1;
        perP = row.percentPerLevel;
        return true;
    }

    baseP = 0f;
    perP = 0f;
    return false;
}

// ====== Excel/CSV から “お守り” 定義を読む軽量DB ======
// 期待配置: Application.streamingAssetsPath / Omamori.xlsx  または  Omamori.csv
// ・xlsx を置けるなら ExcelDataReader を導入すれば自動使用
// ・xlsx が無ければ csv を読む（UTF-8 / ヘッダ付き）
static class OmamoriExcelV2
{
    // キャッシュ
    static bool _loaded;
    static readonly Dictionary<string, PlayerData.OmamoriRarity> _rarityMap =
        new(StringComparer.OrdinalIgnoreCase) {
            {"Normal",PlayerData.OmamoriRarity.Normal},
            {"Common",PlayerData.OmamoriRarity.Common},
            {"Rare",PlayerData.OmamoriRarity.Rare},
            {"Epic",PlayerData.OmamoriRarity.Epic},
            {"Legendary",PlayerData.OmamoriRarity.Legendary},
        };
static readonly Dictionary<string, PlayerData.OmamoriEffect> _effectMap = BuildEffectMap();
static readonly Dictionary<string, PlayerData.OmamoriSpecial> _specialMap = BuildSpecialMap();

static Dictionary<string, PlayerData.OmamoriEffect> BuildEffectMap()
{
    var d = new Dictionary<string, PlayerData.OmamoriEffect>(StringComparer.OrdinalIgnoreCase);
    foreach (PlayerData.OmamoriEffect e in Enum.GetValues(typeof(PlayerData.OmamoriEffect)))
        d[e.ToString()] = e;
    return d;
}
static Dictionary<string, PlayerData.OmamoriSpecial> BuildSpecialMap()
{
    var d = new Dictionary<string, PlayerData.OmamoriSpecial>(StringComparer.OrdinalIgnoreCase);
    foreach (PlayerData.OmamoriSpecial s in Enum.GetValues(typeof(PlayerData.OmamoriSpecial)))
        d[s.ToString()] = s;
    return d;
}


    class RarityRow { public PlayerData.OmamoriRarity rarity; public float baseW; public float perDefeat; public int count; }
    class EffectRow { public PlayerData.OmamoriEffect effect; public int w; }
    class SpecialRow{ public PlayerData.OmamoriSpecial sp; public int w; }
    static List<RarityRow>  R;    // お守り_設定
    static List<EffectRow>  E;    // お守り_効果
    static List<SpecialRow> S;    // お守り_特殊
    static float baseAtLv1 = 5f, perLv = 1f;

static bool IsBannedEffect(PlayerData.OmamoriEffect e)
{
    return e == PlayerData.OmamoriEffect.StartMPFlatAdd
        || e == PlayerData.OmamoriEffect.WinMPFlatAdd
        || e == PlayerData.OmamoriEffect.DamageDealtPercentUp;
}
static int rangeMinus = 2, rangePlus = 2;
    static bool allowDup = false;

public static bool TryEnsureLoaded()
{
    if (_loaded) return true;
    _loaded = true;

    // 既定の空を作る
    R = new(); E = new(); S = new();
#if !UNITY_EDITOR && !USE_STREAMING_OMAMORI_EXCEL
    try
    {
        var db = Resources.Load<OmamoriDatabaseSO>("OmamoriDatabaseSO");
        if (db != null)
        {
            if (db.rarityRows != null)
            {
                for (int i = 0; i < db.rarityRows.Count; i++)
                {
                    var rr = db.rarityRows[i];
                    if (rr == null) continue;
                    R.Add(new RarityRow{ rarity=rr.rarity, baseW=rr.baseW, perDefeat=rr.perDefeat, count=Mathf.Max(1, rr.count) });
                }
            }

            if (db.effectRows != null)
            {
                for (int i = 0; i < db.effectRows.Count; i++)
                {
                    var er = db.effectRows[i];
                    if (er == null) continue;
                    if (IsBannedEffect(er.effect)) continue;
                    E.Add(new EffectRow{ effect=er.effect, w=Mathf.Max(1, er.w) });
                }
            }

            if (db.specialRows != null)
            {
                for (int i = 0; i < db.specialRows.Count; i++)
                {
                    var sr = db.specialRows[i];
                    if (sr == null) continue;
                    S.Add(new SpecialRow{ sp=sr.sp, w=Mathf.Max(0, sr.w) });
                }
            }

            baseAtLv1 = db.baseAtLv1;
            perLv = db.perLv;
            rangeMinus = db.rangeMinus;
            rangePlus = db.rangePlus;
            allowDup = db.allowDup;

            if (R.Count == 0 || E.Count == 0)
            {
                Debug.LogWarning("[OmamoriDebug] OmamoriDatabaseSO loaded but R or E is empty. UseBuiltInDefaults will be applied.");
                UseBuiltInDefaults();
            }

            return true;
        }
        Debug.LogError("[OmamoriDebug] OmamoriDatabaseSO not found in Resources. Assets/Resources/OmamoriDatabaseSO.asset を作成してください。");
    }
    catch (Exception ex)
    {
        Debug.LogError($"[OmamoriDebug] Load SO failed. {ex}");
    }

    UseBuiltInDefaults();
    return true;
#endif
    string debugPath = System.IO.Path.Combine(Application.streamingAssetsPath, "Omamori.xlsx");
    Debug.Log($"[OmamoriDebug] TryEnsureLoaded start. path={debugPath}");

#if EXCEL_DATA_READER
    try {
        var path = System.IO.Path.Combine(Application.streamingAssetsPath, "Omamori.xlsx");
        Debug.Log($"[OmamoriDebug] EXCEL_DATA_READER enabled. fileExists={System.IO.File.Exists(path)} path={path}");

        if (System.IO.File.Exists(path))
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            using var fs = System.IO.File.Open(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
            using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(fs);
            do {
                var name = reader.Name?.Trim();
                Debug.Log($"[OmamoriDebug] Reading sheet: {name}");

                if (string.Equals(name, "お守り_設定", StringComparison.OrdinalIgnoreCase))
                {
                    bool header = true;
                    while (reader.Read())
                    {
                        if (header) { header=false; continue; }
                        var rar = reader.GetValue(0)?.ToString(); if (string.IsNullOrWhiteSpace(rar)) continue;
                        if (!_rarityMap.TryGetValue(rar, out var rr)) continue;
                        float wBase = ToF(reader.GetValue(1), 0f);
                        float wDef  = ToF(reader.GetValue(2), 0f);
                        int   cnt   = (int)ToF(reader.GetValue(3), 1f);
                        R.Add(new RarityRow{ rarity=rr, baseW=wBase, perDefeat=wDef, count=Mathf.Max(1,cnt)});
                        Debug.Log($"[OmamoriDebug] RarityRow loaded. rarity={rr} baseW={wBase} perDefeat={wDef} count={Mathf.Max(1,cnt)}");
                    }
                }
                else if (string.Equals(name, "お守り_効果", StringComparison.OrdinalIgnoreCase))
                {
                    bool header = true;
                    while (reader.Read())
                    {
                        if (header) { header=false; continue; }
                        var key = reader.GetValue(0)?.ToString(); if (string.IsNullOrWhiteSpace(key)) continue;
                        if (!_effectMap.TryGetValue(key, out var eff)) continue;
                        if (IsBannedEffect(eff)) continue;
                        int w = Mathf.Max(1, (int)ToF(reader.GetValue(1), 1f));
                        E.Add(new EffectRow{ effect=eff, w=w });
                        Debug.Log($"[OmamoriDebug] EffectRow loaded. effect={eff} weight={w}");
                    }
                }
                else if (string.Equals(name, "お守り_パラメータ", StringComparison.OrdinalIgnoreCase))
                {
                    bool header = true;
                    while (reader.Read())
                    {
                        if (header) { header=false; continue; }
                        var k = reader.GetValue(0)?.ToString()?.Trim(); var v = reader.GetValue(1)?.ToString();
                        if (string.Equals(k,"BasePercentAtLevel1",StringComparison.OrdinalIgnoreCase)) baseAtLv1 = ParseF(v,5f);
                        else if (string.Equals(k,"PercentPerLevel",StringComparison.OrdinalIgnoreCase))   perLv    = ParseF(v,1f);
                        else if (string.Equals(k,"LevelRangeMinus",StringComparison.OrdinalIgnoreCase))  rangeMinus = (int)ParseF(v,2);
                        else if (string.Equals(k,"LevelRangePlus", StringComparison.OrdinalIgnoreCase))  rangePlus  = (int)ParseF(v,2);
                        else if (string.Equals(k,"AllowDuplicatesPerAmulet",StringComparison.OrdinalIgnoreCase)) allowDup = ParseBool(v,false);

                        Debug.Log($"[OmamoriDebug] Parameter loaded. key={k} value={v}");
                    }
                }
                else if (string.Equals(name, "お守り_特殊", StringComparison.OrdinalIgnoreCase))
                {
                    bool header = true;
                    while (reader.Read())
                    {
                        if (header) { header=false; continue; }
                        var key = reader.GetValue(0)?.ToString(); if (string.IsNullOrWhiteSpace(key)) continue;
                        if (!_specialMap.TryGetValue(key, out var sp)) continue;
                        int w = Mathf.Max(0, (int)ToF(reader.GetValue(1), 0f));
                        S.Add(new SpecialRow{ sp=sp, w=w });
                        Debug.Log($"[OmamoriDebug] SpecialRow loaded. special={sp} weight={w}");
                    }
                }
            } while (reader.NextResult());
        }
    } catch (Exception ex) {
        Debug.LogError($"[OmamoriDebug] Excel load failed. {ex}");
    }
#else
    Debug.LogWarning("[OmamoriDebug] EXCEL_DATA_READER is NOT enabled.");
#endif

    Debug.Log($"[OmamoriDebug] After load. R.Count={R.Count} E.Count={E.Count} S.Count={S.Count}");

    // 1つも読めなかったらデフォルトを充填
    if (R.Count==0 || E.Count==0)
    {
        Debug.LogWarning("[OmamoriDebug] R or E is empty. UseBuiltInDefaults will be applied.");
        UseBuiltInDefaults();
    }

    if (R != null)
    {
        for (int i = 0; i < R.Count; i++)
        {
            var row = R[i];
            Debug.Log($"[OmamoriDebug] FinalRarityRow[{i}] rarity={row.rarity} baseW={row.baseW} perDefeat={row.perDefeat} count={row.count}");
        }
    }

    return true;
}
    public static void UseBuiltInDefaults()
    {
        // レア度：初期分布＋倒した数ドリフト（スクショ準拠）
        R = new() {
            new(){ rarity=PlayerData.OmamoriRarity.Normal,    baseW=50f, perDefeat=-0.10f, count=1 },
            new(){ rarity=PlayerData.OmamoriRarity.Common,    baseW=20f, perDefeat=-0.10f, count=2 },
            new(){ rarity=PlayerData.OmamoriRarity.Rare,      baseW=10f, perDefeat= 0.00f, count=3 },
            new(){ rarity=PlayerData.OmamoriRarity.Epic,      baseW= 9f, perDefeat= 0.10f, count=4 },
            new(){ rarity=PlayerData.OmamoriRarity.Legendary, baseW= 1f, perDefeat= 0.10f, count=5 },
        };
E = new() {
    new(){ effect=PlayerData.OmamoriEffect.MaxHPPercentUp,            w=10 },
    new(){ effect=PlayerData.OmamoriEffect.MaxMPPercentUp,            w= 8 },
    new(){ effect=PlayerData.OmamoriEffect.IyuHealPercentUp,          w=10 },
    new(){ effect=PlayerData.OmamoriEffect.GekiDamagePercentUp,       w=10 },
    new(){ effect=PlayerData.OmamoriEffect.ShunAddPercentUp,          w=10 },
    new(){ effect=PlayerData.OmamoriEffect.DamageTakenPercentDown,    w= 8 },
    new(){ effect=PlayerData.OmamoriEffect.SkillMpCostPercentDown,    w= 4 },
    new(){ effect=PlayerData.OmamoriEffect.MpRegenPercentUp,          w= 4 },
};
S = new() {
    new(){ sp=PlayerData.OmamoriSpecial.None, w=100 },
};
baseAtLv1 = 3f; perLv = 1f; rangeMinus=2; rangePlus=2; allowDup=false;

    }
public static PlayerData.OmamoriRarity RollRarity(int defeated)
{
    // defeated に応じて重み変動
    float sum = 0f;
    var weights = new float[R.Count];
    for (int i=0;i<R.Count;i++)
    {
        var w = Mathf.Max(0f, R[i].baseW + R[i].perDefeat * Mathf.Max(0, defeated));
        weights[i] = w;
        sum += w;
        Debug.Log($"[OmamoriDebug] RollRarity weight[{i}] rarity={R[i].rarity} baseW={R[i].baseW} perDefeat={R[i].perDefeat} defeated={Mathf.Max(0, defeated)} finalWeight={w} count={R[i].count}");
    }

    Debug.Log($"[OmamoriDebug] RollRarity totalWeight={sum}");

    if (sum <= 0f)
    {
        Debug.LogWarning("[OmamoriDebug] RollRarity totalWeight <= 0. Normal will be returned.");
        return PlayerData.OmamoriRarity.Normal;
    }

    float r = UnityEngine.Random.Range(0f, sum);
    Debug.Log($"[OmamoriDebug] RollRarity randomValue={r}");

    float remain = r;
    for (int i=0;i<R.Count;i++)
    {
        remain -= weights[i];
        if (remain < 0f)
        {
            Debug.Log($"[OmamoriDebug] RollRarity selected rarity={R[i].rarity}");
            return R[i].rarity;
        }
    }

    Debug.Log($"[OmamoriDebug] RollRarity fallback selected rarity={R[^1].rarity}");
    return R[^1].rarity;
}

public static int GetEffectCount(PlayerData.OmamoriRarity rar)
{
    for (int i = 0; i < R.Count; i++)
        if (R[i].rarity == rar) return R[i].count;
    return 1;
}

    public static (int minus,int plus) GetLevelRange() => (rangeMinus, rangePlus);

    public static (float baseAtLv1,float perLv) GetScaling() => (baseAtLv1, perLv);

public static List<PlayerData.EffectEntry> RollEffects(int count, int level)
{
    var res = new List<PlayerData.EffectEntry>(count);

    if (!_loaded)
    {
        if (!TryEnsureLoaded())
            UseBuiltInDefaults();
    }

    if (E == null || E.Count == 0) return res;

    // 重複可否
    var used = new HashSet<PlayerData.OmamoriEffect>();

    for (int k = 0; k < count; k++)
    {
        // 重み総和（廃止効果は常に除外）
        int sum = 0;
        foreach (var it in E)
        {
            if (IsBannedEffect(it.effect)) continue;
            if (!allowDup && used.Contains(it.effect)) continue;
            sum += Mathf.Max(0, it.w);
        }
        if (sum <= 0) break;

        int pick = UnityEngine.Random.Range(0, sum);
        PlayerData.OmamoriEffect chosen = E[0].effect;

        foreach (var it in E)
        {
            if (IsBannedEffect(it.effect)) continue;
            if (!allowDup && used.Contains(it.effect)) continue;

            pick -= Mathf.Max(0, it.w);
            if (pick < 0)
            {
                chosen = it.effect;
                break;
            }
        }

        if (IsBannedEffect(chosen)) continue;

        used.Add(chosen);

        // 効果ごとのスケーリング（StageClearManager から流し込まれた値を優先）
        float baseP = baseAtLv1;
        float perP = perLv;

        if (PlayerData.TryGetRuntimeEffectScaleForRoll(chosen, out var runtimeBase, out var runtimePer))
        {
            baseP = runtimeBase;
            perP = runtimePer;
        }

        float amountPct = (baseP + perP * (level - 1)) / 100f;
        res.Add(new PlayerData.EffectEntry { type = chosen, amountPercent = amountPct });
    }

    return res;
}
public static PlayerData.OmamoriSpecial RollSpecial()
{
    return PlayerData.OmamoriSpecial.None;
}
    static float ToF(object v, float defV){ if (v==null) return defV; float f; return float.TryParse(v.ToString(), out f) ? f : defV; }
    static float ParseF(string s, float d){ if (string.IsNullOrEmpty(s)) return d; if (float.TryParse(s, out var f)) return f; return d; }
    static bool  ParseBool(string s, bool d){ if (string.IsNullOrEmpty(s)) return d; if (bool.TryParse(s, out var b)) return b; if (int.TryParse(s, out var i)) return i!=0; return d; }
}
public static string GetOmamoriText_EquipUI(int id, bool includeSpecial = true)
{
    if (id == 0) return "未装備";
    if (!TryDecode(id, out var o)) return $"未知のお守り#{id}";

    var lines = new List<string>();

    // 1行目：神器は赤い【XXの神器】、通常はレア度色
    if (o.isUnique)
    {
        string en = string.IsNullOrEmpty(o.uniqueEnemyName) ? "敵" : o.uniqueEnemyName;
        lines.Add($"<color=#FF0000>【{en}の神器】</color>　Lv.{o.level}");
    }
    else
    {
        string rarityJp = RarityToJp(o.rarity);
        string hex = ColorUtility.ToHtmlStringRGBA(RarityToColor_EquipUI(o.rarity));
        lines.Add($"<color=#{hex}>{rarityJp}</color>　Lv.{o.level}");
    }

    // 2行目以降：効果を -XXX 形式で列挙
    if (o.effects != null)
    {
        foreach (var e in o.effects)
            lines.Add($"-{EffectToJp(e.type)} +{ToPct(e.amountPercent)}");
    }

    // 特殊効果（必要なら表示）
    if (includeSpecial && o.special != OmamoriSpecial.None)
        lines.Add($"-特殊 {SpecialToJp(o.special)}");

    // ★ユニーク固有効果（赤字）
    if (o.isUnique)
    {
        string u = UniqueEffectToText(o.uniqueKind);
        if (!string.IsNullOrEmpty(u))
            lines.Add($"<color=#FF0000>{u}</color>");
    }

    return string.Join("\n", lines);
}

private static Color RarityToColor_EquipUI(OmamoriRarity rarity)
{
    // RunSceneのお札UIと同じ意図：Normal=白 / Common=青 / Rare=黄 / Epic=紫 / Legendary=橙
    switch (rarity)
    {
        case OmamoriRarity.Normal:
            return Color.white;

        case OmamoriRarity.Common:
            return new Color(0.3f, 0.7f, 1.0f, 1f); // 青

        case OmamoriRarity.Rare:
            return new Color(1.0f, 0.92f, 0.2f, 1f); // 黄

        case OmamoriRarity.Epic:
            return new Color(0.6f, 0.2f, 0.8f, 1f); // 紫

        case OmamoriRarity.Legendary:
            return new Color(1f, 0.6f, 0f, 1f); // 橙
    }

    return Color.white;
}
public static bool TryGetOmamoriRarityJp(int id, out string rarityJp)
{
    rarityJp = "";
    if (id == 0) return false;
    if (!TryDecode(id, out var o)) return false;

    rarityJp = RarityToLocalized_Local(o.rarity);
    return !string.IsNullOrEmpty(rarityJp);
}
public static bool TryGetOmamoriRarityColor(int id, out Color color)
{
    color = Color.white;
    if (id == 0) return false;
    if (!TryDecode(id, out var o)) return false;

    if (o.isUnique)
    {
        color = Color.red;
        return true;
    }

    color = RarityToColor_EquipUI(o.rarity);
    return true;
}


}
