using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum AchievementId
{
    YakumanWin = 0,
    Kokushi = 1,
    Suuankou = 2,
    Daisangen = 3,
    Tsuuiisou = 4,
    Ryuuiisou = 5,
    Shousuushii = 6,
    Daisuushii = 7,
    Chuuren = 8,
    Chinroutou = 9,
    Suukantsu = 10,
    Chihou = 11,
    Tenhou = 12,

    Score100k = 20,
    Score200k = 21,
    Score500k = 22,
    Score800k = 23,
    Score1000k = 24,

    Tier1Clear = 30,
    Tier2Clear = 31,
    Tier3Clear = 32,
    Tier4Clear = 33,
    Tier5Clear = 34,

    HadesDefeat = 40,
    DyeMasterTier1Clear = 50,
    CalligrapherTier1Clear = 51,
    CapitalistTier1Clear = 52,

    LegendaryOmamori = 60,
    LegendarySpecialTile = 61,
    ShinkiGet = 62
}

public static class AchievementSystem
{
    private const string KeyReadyPrefix = "ACHV_READY_";
    private const string KeyClaimedPrefix = "ACHV_CLAIMED_";

    public static bool IsReady(AchievementId id)
    {
        return PlayerPrefs.GetInt(KeyReadyPrefix + ((int)id).ToString(), 0) == 1;
    }

    public static bool IsClaimed(AchievementId id)
    {
        return PlayerPrefs.GetInt(KeyClaimedPrefix + ((int)id).ToString(), 0) == 1;
    }

    public static void MarkReady(AchievementId id)
    {
        if (IsReady(id)) return;
        PlayerPrefs.SetInt(KeyReadyPrefix + ((int)id).ToString(), 1);
        PlayerPrefs.Save();
    }

    public static bool TryClaim(AchievementId id, int gemReward)
    {
        if (!IsReady(id)) return false;
        if (IsClaimed(id)) return false;

        PlayerPrefs.SetInt(KeyClaimedPrefix + ((int)id).ToString(), 1);
        PlayerPrefs.Save();

        try
        {
            if (gemReward > 0) SpecialTileSystem.AddGems(gemReward);
        }
        catch { }

        return true;
    }

    public static int GetUnclaimedReadyRewardCount()
    {
        int count = 0;

        Array values = Enum.GetValues(typeof(AchievementId));
        for (int i = 0; i < values.Length; i++)
        {
            AchievementId id = (AchievementId)values.GetValue(i);

            if (IsReady(id) && !IsClaimed(id))
            {
                count++;
            }
        }

        return count;
    }

    public static bool HasUnclaimedReadyReward()
    {
        return GetUnclaimedReadyRewardCount() > 0;
    }

    private static string NormalizeSkillName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        var s = raw.Trim();

        s = s.Replace("　", "");
        s = s.Replace(" ", "");
        s = s.Replace("-", "");
        s = s.Replace("_", "");

        s = s.ToLowerInvariant();
        return s;
    }
public static string GetDisplayTitle(AchievementId id)
{
    var lm = LocalizationManager.Instance;

    if (id == AchievementId.HadesDefeat)
    {
        bool revealed = IsReady(AchievementId.HadesDefeat) || IsClaimed(AchievementId.HadesDefeat);

        if (revealed)
        {
            if (lm != null)
            {
                string localized = lm.GetText("achievement.hades_defeat");
                if (!string.IsNullOrEmpty(localized) && localized != "achievement.hades_defeat")
                    return localized;
            }

            return "ハデスを撃破";
        }

        if (lm != null)
        {
            string hidden = lm.GetText("achievement.hades_defeat_hidden");
            if (!string.IsNullOrEmpty(hidden) && hidden != "achievement.hades_defeat_hidden")
                return hidden;
        }

        return "???を撃破";
    }

    string key = "";
    switch (id)
    {
        case AchievementId.YakumanWin: key = "achievement.yakuman_win"; break;
        case AchievementId.Kokushi: key = "achievement.kokushi"; break;
        case AchievementId.Suuankou: key = "achievement.suuankou"; break;
        case AchievementId.Daisangen: key = "achievement.daisangen"; break;
        case AchievementId.Tsuuiisou: key = "achievement.tsuuiisou"; break;
        case AchievementId.Ryuuiisou: key = "achievement.ryuuiisou"; break;
        case AchievementId.Shousuushii: key = "achievement.shousuushii"; break;
        case AchievementId.Daisuushii: key = "achievement.daisuushii"; break;
        case AchievementId.Chuuren: key = "achievement.chuuren"; break;
        case AchievementId.Chinroutou: key = "achievement.chinroutou"; break;
        case AchievementId.Suukantsu: key = "achievement.suukantsu"; break;
        case AchievementId.Chihou: key = "achievement.chihou"; break;
        case AchievementId.Tenhou: key = "achievement.tenhou"; break;

        case AchievementId.Score100k: key = "achievement.score_100k"; break;
        case AchievementId.Score200k: key = "achievement.score_200k"; break;
        case AchievementId.Score500k: key = "achievement.score_500k"; break;
        case AchievementId.Score800k: key = "achievement.score_800k"; break;
        case AchievementId.Score1000k: key = "achievement.score_1000k"; break;

        case AchievementId.Tier1Clear: key = "achievement.tier1_clear"; break;
        case AchievementId.Tier2Clear: key = "achievement.tier2_clear"; break;
        case AchievementId.Tier3Clear: key = "achievement.tier3_clear"; break;
        case AchievementId.Tier4Clear: key = "achievement.tier4_clear"; break;
        case AchievementId.Tier5Clear: key = "achievement.tier5_clear"; break;

        case AchievementId.DyeMasterTier1Clear: key = "achievement.dyemaster_tier1_clear"; break;
        case AchievementId.CalligrapherTier1Clear: key = "achievement.calligrapher_tier1_clear"; break;
        case AchievementId.CapitalistTier1Clear: key = "achievement.capitalist_tier1_clear"; break;

        case AchievementId.LegendaryOmamori: key = "achievement.legendary_omamori"; break;
        case AchievementId.LegendarySpecialTile: key = "achievement.legendary_special_tile"; break;
        case AchievementId.ShinkiGet: key = "achievement.shinki_get"; break;
    }

    if (lm != null && !string.IsNullOrEmpty(key))
    {
        string localized = lm.GetText(key);
        if (!string.IsNullOrEmpty(localized) && localized != key)
            return localized;
    }

    switch (id)
    {
        case AchievementId.YakumanWin: return "役満を和了";
        case AchievementId.Kokushi: return "国士無双を和了";
        case AchievementId.Suuankou: return "四暗刻を和了";
        case AchievementId.Daisangen: return "大三元を和了";
        case AchievementId.Tsuuiisou: return "字一色を和了";
        case AchievementId.Ryuuiisou: return "緑一色を和了";
        case AchievementId.Shousuushii: return "小四喜を和了";
        case AchievementId.Daisuushii: return "大四喜を和了";
        case AchievementId.Chuuren: return "九蓮宝燈を和了";
        case AchievementId.Chinroutou: return "清老頭を和了";
        case AchievementId.Suukantsu: return "四カンツを和了";
        case AchievementId.Chihou: return "地和を和了";
        case AchievementId.Tenhou: return "天和を和了";

        case AchievementId.Score100k: return "スコア10万点達成";
        case AchievementId.Score200k: return "スコア20万点達成";
        case AchievementId.Score500k: return "スコア50万点達成";
        case AchievementId.Score800k: return "スコア80万点達成";
        case AchievementId.Score1000k: return "スコア100万点達成";

        case AchievementId.Tier1Clear: return "Tier1をクリア";
        case AchievementId.Tier2Clear: return "Tier2をクリア";
        case AchievementId.Tier3Clear: return "Tier3をクリア";
        case AchievementId.Tier4Clear: return "Tier4をクリア";
        case AchievementId.Tier5Clear: return "Tier5をクリア";

        case AchievementId.DyeMasterTier1Clear: return "染色師でTier1をクリア";
        case AchievementId.CalligrapherTier1Clear: return "書家でTier1をクリア";
        case AchievementId.CapitalistTier1Clear: return "資産家でTier1をクリア";

        case AchievementId.LegendaryOmamori: return "レジェンダリーお守りを入手";
        case AchievementId.LegendarySpecialTile: return "レジェンダリー特別牌を入手";
        case AchievementId.ShinkiGet: return "神器を入手";
    }

    return id.ToString();
}
    private static bool ContainsAny(string s, params string[] keys)
    {
        if (string.IsNullOrEmpty(s)) return false;
        for (int i = 0; i < keys.Length; i++)
        {
            if (string.IsNullOrEmpty(keys[i])) continue;
            if (s.Contains(keys[i])) return true;
        }
        return false;
    }
    private static string NormalizeYakuText(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        var s = raw.Trim();

        s = s.Replace("＋", "+");
        s = s.Replace("　", "");
        s = s.Replace(" ", "");
        s = s.Replace("-", "");
        s = s.Replace("_", "");
        s = s.Replace("'", "");
        s = s.Replace("’", "");

        s = s.Replace("四槓子", "四カンツ");
        s = s.Replace("四杠子", "四カンツ");
        s = s.Replace("九莲宝灯", "九蓮宝燈");
        s = s.Replace("纯正九莲宝灯", "純正九蓮宝燈");
        s = s.Replace("清老头", "清老頭");
        s = s.Replace("绿一色", "緑一色");
        s = s.Replace("国士无双", "国士無双");
        s = s.Replace("四暗刻单骑", "四暗刻単騎");
        s = s.Replace("役满", "役満");
        s = s.Replace("天胡", "天和");
        s = s.Replace("地胡", "地和");

        s = s.ToLowerInvariant();
        return s;
    }
        private static bool ContainsYakuAlias(List<string> normalized, params string[] aliases)
    {
        if (normalized == null || normalized.Count == 0) return false;
        if (aliases == null || aliases.Length == 0) return false;

        for (int i = 0; i < normalized.Count; i++)
        {
            if (ContainsAny(normalized[i], aliases)) return true;
        }

        return false;
    }
    public static void NotifyPlayerWin(List<string> yakuList, int scorePoints)
    {
        try
        {
            NotifyScoreThresholds(scorePoints);
        }
        catch { }

        try
        {
            NotifyYakumanByYakuList(yakuList);
        }
        catch { }
    }
public static void NotifyEnemyWin(List<string> yakuList)
{
    try
    {
        NotifyYakumanByYakuList(yakuList);
    }
    catch { }
}

// ★追加：Run合計スコア（1人目開始〜敗北 or Tierクリアまで）でスコア実績を判定する
public static void NotifyRunFinishedScore(int runTotalScore)
{
    try
    {
        NotifyScoreThresholds(runTotalScore);
    }
    catch { }
}

private static void NotifyScoreThresholds(int scorePoints)
{
    if (scorePoints >= 100000) MarkReady(AchievementId.Score100k);
    if (scorePoints >= 200000) MarkReady(AchievementId.Score200k);
    if (scorePoints >= 500000) MarkReady(AchievementId.Score500k);
    if (scorePoints >= 800000) MarkReady(AchievementId.Score800k);
    if (scorePoints >= 1000000) MarkReady(AchievementId.Score1000k);
}
    private static void NotifyYakumanByYakuList(List<string> yakuList)
    {
        if (yakuList == null || yakuList.Count == 0) return;

        var normalized = new List<string>();
        for (int i = 0; i < yakuList.Count; i++)
        {
            normalized.Add(NormalizeYakuText(yakuList[i]));
        }

        bool hasKokushi = ContainsYakuAlias(
            normalized,
            "国士",
            "国士無双",
            "国士无双",
            "kokushi",
            "kokushimusou",
            "thirteenorphans",
            "thirteenorphans13wait",
            "thirteenorphansthirteenwait",
            "十三幺九");

        bool hasSuuankou = ContainsYakuAlias(
            normalized,
            "四暗刻",
            "四暗刻単騎",
            "四暗刻单骑",
            "suuankou",
            "suuankoutanki",
            "fourconcealedtriplets",
            "fourconcealedtripletssinglewait");

        bool hasDaisangen = ContainsYakuAlias(
            normalized,
            "大三元",
            "daisangen",
            "bigthreedragons",
            "bigthreedragon");

        bool hasTsuuiisou = ContainsYakuAlias(
            normalized,
            "字一色",
            "tsuuiisou",
            "allhonors",
            "allhonours");

        bool hasRyuuiisou = ContainsYakuAlias(
            normalized,
            "緑一色",
            "绿一色",
            "ryuuiisou",
            "allgreen");

        bool hasShousuushii = ContainsYakuAlias(
            normalized,
            "小四喜",
            "shousuushii",
            "littlefourwinds",
            "littlefourwind");

        bool hasDaisuushii = ContainsYakuAlias(
            normalized,
            "大四喜",
            "daisuushii",
            "bigfourwinds",
            "bigfourwind");

        bool hasChuuren = ContainsYakuAlias(
            normalized,
            "九蓮",
            "九蓮宝燈",
            "九莲宝灯",
            "純正九蓮宝燈",
            "纯正九莲宝灯",
            "chuuren",
            "chuurenpoutou",
            "junseichuurenpoutou",
            "ninegates");

        bool hasChinroutou = ContainsYakuAlias(
            normalized,
            "清老頭",
            "清老头",
            "chinroutou",
            "allterminals");

        bool hasSuukantsu = ContainsYakuAlias(
            normalized,
            "四カンツ",
            "四槓子",
            "四杠子",
            "suukantsu",
            "fourkans");

        bool hasChihou = ContainsYakuAlias(
            normalized,
            "地和",
            "地胡",
            "chihou",
            "earthlyhand",
            "blessingofearth");

        bool hasTenhou = ContainsYakuAlias(
            normalized,
            "天和",
            "天胡",
            "tenhou",
            "heavenlyhand",
            "blessingofheaven");

        bool hasGenericYakuman = ContainsYakuAlias(
            normalized,
            "役満",
            "yakuman");

        bool isYakuman =
            hasKokushi ||
            hasSuuankou ||
            hasDaisangen ||
            hasTsuuiisou ||
            hasRyuuiisou ||
            hasShousuushii ||
            hasDaisuushii ||
            hasChuuren ||
            hasChinroutou ||
            hasSuukantsu ||
            hasChihou ||
            hasTenhou ||
            hasGenericYakuman;

        if (isYakuman) MarkReady(AchievementId.YakumanWin);

        if (hasKokushi) MarkReady(AchievementId.Kokushi);
        if (hasSuuankou) MarkReady(AchievementId.Suuankou);
        if (hasDaisangen) MarkReady(AchievementId.Daisangen);
        if (hasTsuuiisou) MarkReady(AchievementId.Tsuuiisou);
        if (hasRyuuiisou) MarkReady(AchievementId.Ryuuiisou);
        if (hasShousuushii) MarkReady(AchievementId.Shousuushii);
        if (hasDaisuushii) MarkReady(AchievementId.Daisuushii);
        if (hasChuuren) MarkReady(AchievementId.Chuuren);
        if (hasChinroutou) MarkReady(AchievementId.Chinroutou);
        if (hasSuukantsu) MarkReady(AchievementId.Suukantsu);
        if (hasChihou) MarkReady(AchievementId.Chihou);
        if (hasTenhou) MarkReady(AchievementId.Tenhou);
    }
    public static void NotifyTierCleared(int tier, string equippedSkillName)
    {
        if (tier <= 0) return;

        if (tier == 1) MarkReady(AchievementId.Tier1Clear);
        if (tier == 2) MarkReady(AchievementId.Tier2Clear);
        if (tier == 3) MarkReady(AchievementId.Tier3Clear);
        if (tier == 4) MarkReady(AchievementId.Tier4Clear);
        if (tier == 5) MarkReady(AchievementId.Tier5Clear);

        if (tier == 1)
        {
            if (!string.IsNullOrEmpty(equippedSkillName))
            {
                var s = NormalizeSkillName(equippedSkillName);

                if (
                    s == "randomman" ||
                    s == "dyemaster" ||
                    s == "染色師" ||
                    s == "染色师"
                )
                    MarkReady(AchievementId.DyeMasterTier1Clear);

                if (
                    s == "enhancehand" ||
                    s == "calligrapher" ||
                    s == "書家" ||
                    s == "书家"
                )
                    MarkReady(AchievementId.CalligrapherTier1Clear);

                if (
                    s == "capitalist" ||
                    s == "資産家" ||
                    s == "资产家"
                )
                    MarkReady(AchievementId.CapitalistTier1Clear);
            }
        }
    }
    public static void NotifyHadesDefeated()
    {
        MarkReady(AchievementId.HadesDefeat);
    }

    public static void NotifyLegendaryOmamoriObtained()
    {
        MarkReady(AchievementId.LegendaryOmamori);
    }

    public static void NotifyLegendarySpecialTileObtained()
    {
        MarkReady(AchievementId.LegendarySpecialTile);
    }

    public static void NotifyShinkiObtained()
    {
        MarkReady(AchievementId.ShinkiGet);
    }

    public static void ReconcileFromSaveData()
    {
        try
        {
            var owned = PlayerData.OwnedOmamori;
            if (owned != null && owned.Count > 0)
            {
                foreach (var id in owned)
                {
                    if (id == 0) continue;

                    if (PlayerData.TryGetOmamori(id, out var inst) && inst != null)
                    {
                        if (inst.isUnique) NotifyShinkiObtained();
                        if (inst.rarity == PlayerData.OmamoriRarity.Legendary) NotifyLegendaryOmamoriObtained();
                    }
                }
            }
        }
        catch { }

        try
        {
            var spOwned = SpecialTileSystem.GetOwned();
            if (spOwned != null)
            {
                for (int i = 0; i < spOwned.Count; i++)
                {
                    if (spOwned[i].rarity == SpecialTileSystem.Rarity.Legendary)
                    {
                        NotifyLegendarySpecialTileObtained();
                        break;
                    }
                }
            }
        }
        catch { }
    }
}