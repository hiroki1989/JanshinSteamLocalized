using System;
using System.Collections.Generic;
using UnityEngine;

public static class OfudaCatalog
{
    // CSV→全組合せ。HP/MPの同軸（CONDがHP/MPを含むときEFFECT側もHP/MP）の組は除外
    public static List<OfudaDef> BuildFromExcel(OfudaExcelLoader.Catalog src)
    {
        var list = new List<OfudaDef>();
        if (src == null) return list;

        foreach (var cond in src.conditions)
        foreach (var eff  in src.effects)
        {
            if (IsLegacyUnsupportedCondition(cond)) continue;
            if (ConflictsHPMP(cond, eff)) continue;

            float sumProb = Mathf.Max(0.0001f, cond.prob + eff.prob);

string rarity = "ノーマル";
int    price  = 300;
foreach (var band in src.priceMap)
{
    if (sumProb <= band.maxProbSum)
    {
        rarity = band.rarity;
        if (band.priceFixed > 0)
            price = band.priceFixed;
        else
            price = Mathf.Clamp(Mathf.RoundToInt(band.priceK / Mathf.Max(0.0001f, band.maxProbSum)), 1, 999999);
        break;
    }
}

// 内部保持用レア度は日本語で統一
switch ((rarity ?? "").Trim().ToLowerInvariant())
{
    case "legendary":
    case "レジェンダリー":
        rarity = "レジェンダリー";
        break;
    case "epic":
    case "エピック":
        rarity = "エピック";
        break;
    case "rare":
    case "レア":
        rarity = "レア";
        break;
    case "common":
    case "コモン":
        rarity = "コモン";
        break;
    case "normal":
    case "ノーマル":
    default:
        rarity = "ノーマル";
        break;
}

string id   = $"{cond.key}__{eff.key}";
string name = BuildLocalizedDisplayName(rarity, cond, eff);
string desc = BuildLocalizedDescription(cond, eff);
list.Add(new OfudaDef {
    id = id,
    condition = cond,
    effect = eff,
    combinedProb = sumProb,
    price = price,
    rarity = rarity,
    displayName = name,
    description = desc,
});
        }
        return list;
    }

    // CSVのprobを重みとして、条件・効果を独立抽選して1枚返す
    public static OfudaDef PickOneByCsvProb(OfudaExcelLoader.Catalog src, System.Random rng = null)
    {
        if (src == null) return null;
        rng ??= new System.Random();

        for (int guard = 64; guard-- > 0; )
        {
            int ci = WeightedPickIndex(src.conditions, c => Mathf.Max(0f, c.prob), rng);
            if (ci < 0) return null;
            var cond = src.conditions[ci];
            if (IsLegacyUnsupportedCondition(cond)) continue;

            // 効果の重み付き抽選
            int ei = WeightedPickIndex(src.effects, e => Mathf.Max(0f, e.prob), rng);
            if (ei < 0) return null;
            var eff = src.effects[ei];

            // HP/MP同軸のNG組合せは引き直し
            if (ConflictsHPMP(cond, eff)) continue;
            // 価格帯・レア度（BuildFromExcel と同じルール）
            float sumProb = Mathf.Max(0.0001f, cond.prob + eff.prob);
            string rarity = "ノーマル";
            int price = 300;
            foreach (var band in src.priceMap)
            {
                if (sumProb <= band.maxProbSum)
                {
                    rarity = band.rarity;
                    if (band.priceFixed > 0)
                        price = band.priceFixed;
                    else
                        price = Mathf.Clamp(Mathf.RoundToInt(band.priceK / Mathf.Max(0.0001f, band.maxProbSum)), 1, 999999);
                    break;
                }
            }

            // 内部保持用レア度は日本語で統一
            switch ((rarity ?? "").Trim().ToLowerInvariant())
            {
                case "legendary":
                case "レジェンダリー":
                    rarity = "レジェンダリー";
                    break;
                case "epic":
                case "エピック":
                    rarity = "エピック";
                    break;
                case "rare":
                case "レア":
                    rarity = "レア";
                    break;
                case "common":
                case "コモン":
                    rarity = "コモン";
                    break;
                case "normal":
                case "ノーマル":
                default:
                    rarity = "ノーマル";
                    break;
            }

            string id = $"{cond.key}__{eff.key}";
            string name = BuildLocalizedDisplayName(rarity, cond, eff);
            string desc = BuildLocalizedDescription(cond, eff);

            return new OfudaDef
            {
                id = id,
                condition = cond,
                effect = eff,
                combinedProb = sumProb,
                price = price,
                rarity = rarity,
                displayName = name,
                description = desc,
            };
        }
        return null;
    }
    private static string BuildLocalizedDisplayName(string rarity, OfudaCondition cond, OfudaEffect eff)
    {
        string rarityLabel = GetLocalizedRarityLabel(rarity);
        string condLabel = GetLocalizedConditionLabel(cond);
        string effLabel = GetLocalizedEffectLabel(eff);

        return $"【{rarityLabel}】{condLabel} {effLabel}";
    }
private static bool IsLegacyUnsupportedCondition(OfudaCondition cond)
{
    if (cond == null) return false;

    string raw = !string.IsNullOrEmpty(cond.key) ? cond.key : cond.label;
    string key = NormalizeOfudaConditionKey(raw);

    return key == "Dealer" || key == "Child";
}
private static string BuildLocalizedDescription(OfudaCondition cond, OfudaEffect eff)
{
    string condLabel = GetLocalizedConditionLabel(cond);
    string effLabel = GetLocalizedEffectLabel(eff);

    if (string.IsNullOrEmpty(condLabel)) return effLabel ?? "";
    if (string.IsNullOrEmpty(effLabel)) return condLabel ?? "";

    return $"{condLabel} {effLabel}";
}
private static string NormalizeOfudaConditionKey(string key)
{
    if (string.IsNullOrEmpty(key)) return "";

    string s = key.Trim();
    s = s.Replace("COND:", "");
    s = s.Replace("　", " ");
    s = s.Replace("％", "%");

    if (s == "HonitsuOrChinitsu") return "HonitsuOrChinitsu";
    if (s == "ToitoiOrChiitoitsu") return "ToitoiOrChiitoitsu";

    bool hasHonitsu = s.Contains("混一色") || s.Contains("ホンイツ") || s.Contains("Honitsu");
    bool hasChinitsu = s.Contains("清一色") || s.Contains("Chinitsu");
    if (hasHonitsu && hasChinitsu)
        return "HonitsuOrChinitsu";

    bool hasToitoi = s.Contains("対々和") || s.Contains("Toitoi");
    bool hasChiitoitsu = s.Contains("七対子") || s.Contains("Chiitoitsu");
    if (hasToitoi && hasChiitoitsu)
        return "ToitoiOrChiitoitsu";

    if (s == "満貫以上" || s == "ManganUp" || (s.Contains("満貫以上") && s.Contains("和了")))
        return "ManganUp";

    if (s == "跳満以上" || s == "HanemanUp" || (s.Contains("跳満以上") && s.Contains("和了")))
        return "HanemanUp";

    if (s == "倍満以上" || s == "BaimanUp" || (s.Contains("倍満以上") && s.Contains("和了")))
        return "BaimanUp";

    if (s == "三倍満以上" || s == "SanbaimanUp" || (s.Contains("三倍満以上") && s.Contains("和了")))
        return "SanbaimanUp";

    if (s == "役満以上" || s == "役満" || s == "Yakuman" || (s.Contains("役満") && s.Contains("和了")))
        return "Yakuman";

    if (s == "リーチ" || s == "Riichi" || (s.Contains("リーチ") && s.Contains("和了")))
        return "Riichi";

    if (s == "ツモ" || s == "自摸" || s == "Tsumo" || ((s.Contains("ツモ") || s.Contains("自摸")) && s.Contains("和了")))
        return "Tsumo";

    if (s == "ロン" || s == "Ron" || (s.Contains("ロン") && s.Contains("和了")))
        return "Ron";

    if (s == "親" || s == "Dealer" || (s.Contains("親") && s.Contains("和了")))
        return "Dealer";

    if (s == "子" || s == "Child" || (s.Contains("子") && s.Contains("和了")))
        return "Child";

    if (s == "平和" || s == "Pinfu" || (s.Contains("平和") && s.Contains("和了")))
        return "Pinfu";

    if (s == "タンヤオ" || s == "断么九" || s == "Tanyao" || ((s.Contains("タンヤオ") || s.Contains("断么九")) && s.Contains("和了")))
        return "Tanyao";

    if (s == "一盃口" || s == "Iipeikou" || (s.Contains("一盃口") && s.Contains("和了")))
        return "Iipeikou";

    if (s == "役牌" || s == "Yakuhai" || (s.Contains("役牌") && s.Contains("和了")))
        return "Yakuhai";

    if (s == "対々和" || s == "Toitoi" || (s.Contains("対々和") && s.Contains("和了")))
        return "Toitoi";

    if (s == "三暗刻" || s == "Sanankou" || (s.Contains("三暗刻") && s.Contains("和了")))
        return "Sanankou";

    if (s == "混一色" || s == "ホンイツ" || s == "Honitsu" || ((s.Contains("混一色") || s.Contains("ホンイツ")) && s.Contains("和了")))
        return "Honitsu";

    if (s == "清一色" || s == "Chinitsu" || (s.Contains("清一色") && s.Contains("和了")))
        return "Chinitsu";

    if (s == "七対子" || s == "Chiitoitsu" || (s.Contains("七対子") && s.Contains("和了")))
        return "Chiitoitsu";

    if (s == "4順子" || s == "４順子" || s == "Shuntsu4") return "Shuntsu4";
    if (s == "4刻子" || s == "４刻子" || s == "Koutsu4") return "Koutsu4";

    if (s.Contains("3ターン以内")) return "Within3Turns";
    if (s.Contains("カンをした状態")) return "AfterKan";
    if (s.Contains("ポンをした状態")) return "AfterPon";
    if (s.Contains("チーをした状態")) return "AfterChi";

    if (s.Contains("HP") && s.Contains("10") && s.Contains("%") && s.Contains("以下")) return "HP10OrLess";
    if (s.Contains("HP") && s.Contains("25") && s.Contains("%") && s.Contains("以下")) return "HP25OrLess";
    if (s.Contains("HP") && s.Contains("50") && s.Contains("%") && s.Contains("以下")) return "HP50OrLess";
    if (s.Contains("HP") && s.Contains("50") && s.Contains("%") && s.Contains("以上")) return "HP50OrMore";
    if (s.Contains("HP") && s.Contains("75") && s.Contains("%") && s.Contains("以上")) return "HP75OrMore";
    if (s.Contains("HP") && s.Contains("100")) return "HP100";

    if (s.Contains("MP") && s.Contains("10") && s.Contains("%") && s.Contains("以下")) return "MP10OrLess";
    if (s.Contains("MP") && s.Contains("25") && s.Contains("%") && s.Contains("以下")) return "MP25OrLess";
    if (s.Contains("MP") && s.Contains("50") && s.Contains("%") && s.Contains("以下")) return "MP50OrLess";
    if (s.Contains("MP") && s.Contains("50") && s.Contains("%") && s.Contains("以上")) return "MP50OrMore";
    if (s.Contains("MP") && s.Contains("75") && s.Contains("%") && s.Contains("以上")) return "MP75OrMore";
    if (s.Contains("MP") && s.Contains("100")) return "MP100";

    return s;
}
private static bool TryGetEffectValue(OfudaEffect eff, out float magnitude, out string unit)
{
    magnitude = 0f;
    unit = "";

    if (eff == null) return false;

    if (eff.magnitude != 0f)
    {
        magnitude = eff.magnitude;
        unit = eff.unit ?? "";
        return true;
    }

    string raw = eff.key ?? "";
    if (string.IsNullOrEmpty(raw))
        raw = eff.label ?? "";

    raw = raw.Replace("％", "%");

    try
    {
        if (raw.Contains("倍"))
        {
            int mulIndex = raw.IndexOf("倍", StringComparison.Ordinal);
            int start = mulIndex - 1;
            while (start >= 0)
            {
                char c = raw[start];
                if ((c >= '0' && c <= '9') || c == '.')
                    start--;
                else
                    break;
            }

            string num = raw.Substring(start + 1, mulIndex - (start + 1)).Trim();
            if (float.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ||
                float.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out v))
            {
                magnitude = v;
                unit = "mul";
                return true;
            }
        }

        if (raw.Contains("%"))
        {
            int pctIndex = raw.IndexOf("%", StringComparison.Ordinal);
            int start = pctIndex - 1;
            while (start >= 0)
            {
                char c = raw[start];
                if ((c >= '0' && c <= '9') || c == '.')
                    start--;
                else
                    break;
            }

            string num = raw.Substring(start + 1, pctIndex - (start + 1)).Trim();
            if (float.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ||
                float.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out v))
            {
                magnitude = v;
                unit = "%";
                return true;
            }
        }
    }
    catch
    {
    }

    return false;
}

private static string FormatOfudaValue(float magnitude, string unit)
{
    if (unit == "%")
        return magnitude.ToString("0.#") + "%";

    if (unit == "mul")
        return "×" + magnitude.ToString("0.###");

    if (string.IsNullOrEmpty(unit))
        return magnitude.ToString("0.###");

    return magnitude.ToString("0.###") + unit;
}

private static string FormatOfudaPercentValue(float magnitude, string unit)
{
    string normalizedUnit = (unit ?? "").Trim();

    if (normalizedUnit == "％")
        normalizedUnit = "%";

    float displayValue = magnitude;

    if (normalizedUnit == "%")
    {
        displayValue = magnitude;
    }
    else if (magnitude > 0f && magnitude <= 1f)
    {
        displayValue = magnitude * 100f;
    }

    return displayValue.ToString("0.#") + "%";
}
private static string NormalizeOfudaEffectKey(string key)
{
    if (string.IsNullOrEmpty(key)) return "";

    string s = key.Trim();
    s = s.Replace("　", " ");
    s = s.Replace("％", "%");

    if (s == "DmgPlus") return "DmgPlus";
    if (s == "DmgMul") return "DmgMul";
    if (s == "HpHeal") return "HpHeal";
    if (s == "MpHeal") return "MpHeal";
    if (s == "GoldPlus") return "GoldPlus";
    if (s == "GoldMul") return "GoldMul";
    if (s == "ScorePlus") return "ScorePlus";
    if (s == "ScoreMul") return "ScoreMul";
    if (s == "HanPlus") return "HanPlus";
    if (s == "FuPlus") return "FuPlus";

    if (s.StartsWith("EFFECT:点数が", StringComparison.Ordinal))
    {
        if (s.Contains("倍")) return "ScoreMul";
        return "ScorePlus";
    }

    if (s.StartsWith("EFFECT:HPを", StringComparison.Ordinal))
        return "HpHeal";

    if (s.StartsWith("EFFECT:MPを", StringComparison.Ordinal))
        return "MpHeal";

    if (s.Contains("ダメージ") && s.Contains("倍"))
        return "DmgMul";

    if (s.Contains("ダメージ"))
        return "DmgPlus";

    if (s.Contains("GOLD") && s.Contains("倍"))
        return "GoldMul";

    if (s.Contains("GOLD") || s.Contains("Gold") || s.Contains("gold"))
        return "GoldPlus";

    if (s.Contains("翻"))
        return "HanPlus";

    if (s.Contains("符"))
        return "FuPlus";

    return s;
}
private static string GetLocalizedParenText(string inner)
{
    var lang = GetCurrentLanguage();
    switch (lang)
    {
        case LocalizationManager.Language.English:
            return $" ({inner})";

        default:
            return $"（{inner}）";
    }
}
private static LocalizationManager.Language GetCurrentLanguage()
{
    try
    {
        if (LocalizationManager.Instance != null)
            return LocalizationManager.Instance.CurrentLanguage;
    }
    catch { }

    return LocalizationManager.Language.Japanese;
}
private static string GetLocalizedRarityLabel(string rarity)
{
    var lang = GetCurrentLanguage();

    switch (lang)
    {
        case LocalizationManager.Language.English:
            switch ((rarity ?? "").Trim().ToLowerInvariant())
            {
                case "レジェンダリー":
                case "legendary":
                    return "Legendary";
                case "エピック":
                case "epic":
                    return "Epic";
                case "レア":
                case "rare":
                    return "Rare";
                case "コモン":
                case "common":
                    return "Common";
                case "ノーマル":
                case "normal":
                default:
                    return "Normal";
            }

        case LocalizationManager.Language.ChineseSimplified:
            switch ((rarity ?? "").Trim().ToLowerInvariant())
            {
                case "レジェンダリー":
                case "legendary":
                    return "传说";
                case "エピック":
                case "epic":
                    return "史诗";
                case "レア":
                case "rare":
                    return "稀有";
                case "コモン":
                case "common":
                    return "普通";
                case "ノーマル":
                case "normal":
                default:
                    return "标准";
            }

        default:
            switch ((rarity ?? "").Trim().ToLowerInvariant())
            {
                case "レジェンダリー":
                case "legendary":
                    return "レジェンダリー";
                case "エピック":
                case "epic":
                    return "エピック";
                case "レア":
                case "rare":
                    return "レア";
                case "コモン":
                case "common":
                    return "コモン";
                case "ノーマル":
                case "normal":
                default:
                    return "ノーマル";
            }
    }
}
private static string GetLocalizedConditionLabel(OfudaCondition cond)
{
    if (cond == null) return "";

    var lang = GetCurrentLanguage();
    string rawKey = cond.key ?? "";
    string key = NormalizeOfudaConditionKey(rawKey);

    switch (lang)
    {
        case LocalizationManager.Language.English:
            switch (key)
            {
                case "ManganUp":   return "when winning with Mangan or above";
                case "HanemanUp":  return "when winning with Haneman or above";
                case "BaimanUp":   return "when winning with Baiman or above";
                case "SanbaimanUp":return "when winning with Sanbaiman or above";
                case "Yakuman":    return "when winning with Yakuman";
                case "Riichi":     return "when winning after declaring Riichi";
                case "Tsumo":      return "when winning by Tsumo";
                case "Ron":        return "when winning by Ron";
                case "Dealer":     return "when winning as Dealer";
                case "Child":      return "when winning as Non-Dealer";
                case "Pinfu":      return "when winning with Pinfu";
                case "Tanyao":     return "when winning with Tanyao";
                case "Iipeikou":   return "when winning with Iipeikou";
                case "Yakuhai":    return "when winning with Yakuhai";
                case "Toitoi":     return "when winning with Toitoi";
                case "ToitoiOrChiitoitsu": return "when winning with Toitoi or Chiitoitsu";
                case "Sanankou":   return "when winning with Sanankou";
                case "Honitsu":    return "when winning with Honitsu";
                case "HonitsuOrChinitsu": return "when winning with Honitsu or Chinitsu";
                case "Chinitsu":   return "when winning with Chinitsu";
                case "Chiitoitsu": return "when winning with Chiitoitsu";
                case "Shuntsu4":   return "when winning with 4 sequences";
                case "Koutsu4":    return "when winning with 4 triplets";
                case "Within3Turns": return "when winning within 3 turns";
                case "AfterKan":   return "when winning after declaring a Kan";
                case "AfterPon":   return "when winning after declaring a Pon";
                case "AfterChi":   return "when winning after declaring a Chi";
                case "HP10OrLess": return "when winning with HP at 10% or less";
                case "HP25OrLess": return "when winning with HP at 25% or less";
                case "HP50OrLess": return "when winning with HP at 50% or less";
                case "HP50OrMore": return "when winning with HP at 50% or more";
                case "HP75OrMore": return "when winning with HP at 75% or more";
                case "HP100":      return "when winning with full HP";
                case "MP10OrLess": return "when winning with MP at 10% or less";
                case "MP25OrLess": return "when winning with MP at 25% or less";
                case "MP50OrLess": return "when winning with MP at 50% or less";
                case "MP50OrMore": return "when winning with MP at 50% or more";
                case "MP75OrMore": return "when winning with MP at 75% or more";
                case "MP100":      return "when winning with full MP";
                default: return string.IsNullOrEmpty(cond.label) ? rawKey : cond.label;
            }

        case LocalizationManager.Language.ChineseSimplified:
            switch (key)
            {
                case "ManganUp":    return "和了满贯以上时";
                case "HanemanUp":   return "和了跳满以上时";
                case "BaimanUp":    return "和了倍满以上时";
                case "SanbaimanUp": return "和了三倍满以上时";
                case "Yakuman":     return "和了役满时";
                case "Riichi":      return "立直后和了时";
                case "Tsumo":       return "自摸和了时";
                case "Ron":         return "荣和时";
                case "Dealer":      return "以庄家身份和了时";
                case "Child":       return "以子家身份和了时";
                case "Pinfu":       return "和了平和时";
                case "Tanyao":      return "和了断幺九时";
                case "Iipeikou":    return "和了一杯口时";
                case "Yakuhai":     return "和了役牌时";
                case "Toitoi":      return "和了对对和时";
                case "ToitoiOrChiitoitsu": return "和了对对和或七对子时";
                case "Sanankou":    return "和了三暗刻时";
                case "Honitsu":     return "和了混一色时";
                case "HonitsuOrChinitsu": return "和了混一色或清一色时";
                case "Chinitsu":    return "和了清一色时";
                case "Chiitoitsu":  return "和了七对子时";
                case "Shuntsu4":    return "以4个顺子和了时";
                case "Koutsu4":     return "以4个刻子和了时";
                case "Within3Turns":return "3回合内和了时";
                case "AfterKan":    return "在杠牌状态下和了时";
                case "AfterPon":    return "在碰牌状态下和了时";
                case "AfterChi":    return "在吃牌状态下和了时";
                case "HP10OrLess":  return "HP在10%以下时和了";
                case "HP25OrLess":  return "HP在25%以下时和了";
                case "HP50OrLess":  return "HP在50%以下时和了";
                case "HP50OrMore":  return "HP在50%以上时和了";
                case "HP75OrMore":  return "HP在75%以上时和了";
                case "HP100":       return "HP全满时和了";
                case "MP10OrLess":  return "MP在10%以下时和了";
                case "MP25OrLess":  return "MP在25%以下时和了";
                case "MP50OrLess":  return "MP在50%以下时和了";
                case "MP50OrMore":  return "MP在50%以上时和了";
                case "MP75OrMore":  return "MP在75%以上时和了";
                case "MP100":       return "MP全满时和了";
                default: return string.IsNullOrEmpty(cond.label) ? rawKey : cond.label;
            }

        default:
            return string.IsNullOrEmpty(cond.label) ? rawKey : cond.label;
    }
}
private static string GetLocalizedEffectLabel(OfudaEffect eff)
{
    if (eff == null) return "";

    var lang = GetCurrentLanguage();
    string rawKey = eff.key ?? "";
    string key = NormalizeOfudaEffectKey(rawKey);

    bool hasValue = TryGetEffectValue(eff, out float magnitude, out string unit);

    string valueText = "";
    if (hasValue)
    {
        switch (key)
        {
            case "HpHeal":
            case "MpHeal":
                valueText = FormatOfudaPercentValue(magnitude, unit);
                break;

            default:
                valueText = FormatOfudaValue(magnitude, unit);
                break;
        }
    }

    switch (lang)
    {
        case LocalizationManager.Language.English:
            switch (key)
            {
                case "DmgPlus":
                    return hasValue ? $"increase damage by {valueText}" : "increase damage";
                case "DmgMul":
                    return hasValue ? $"increase damage multiplier to {valueText}" : "increase damage multiplier";
                case "HpHeal":
                    return hasValue ? $"recover HP by {valueText}" : "recover HP";
                case "MpHeal":
                    return hasValue ? $"recover MP by {valueText}" : "recover MP";
                case "GoldPlus":
                    return hasValue ? $"increase Gold by {valueText}" : "increase Gold";
                case "GoldMul":
                    return hasValue ? $"increase Gold multiplier to {valueText}" : "increase Gold multiplier";
                case "ScorePlus":
                    return hasValue ? $"increase score by {valueText}" : "increase score";
                case "ScoreMul":
                    return hasValue ? $"increase score multiplier to {valueText}" : "increase score multiplier";
                case "HanPlus":
                    return hasValue ? $"increase Han by {valueText}" : "increase Han";
                case "FuPlus":
                    return hasValue ? $"increase Fu by {valueText}" : "increase Fu";
                default:
                    return string.IsNullOrEmpty(eff.label) ? rawKey : eff.label;
            }

        case LocalizationManager.Language.ChineseSimplified:
            switch (key)
            {
                case "DmgPlus":
                    return hasValue ? $"伤害提高{valueText}" : "伤害提高";
                case "DmgMul":
                    return hasValue ? $"伤害倍率提高至{valueText}" : "伤害倍率提高";
                case "HpHeal":
                    return hasValue ? $"恢复{valueText}HP" : "恢复HP";
                case "MpHeal":
                    return hasValue ? $"恢复{valueText}MP" : "恢复MP";
                case "GoldPlus":
                    return hasValue ? $"金币增加{valueText}" : "金币增加";
                case "GoldMul":
                    return hasValue ? $"金币倍率提高至{valueText}" : "金币倍率提高";
                case "ScorePlus":
                    return hasValue ? $"点数增加{valueText}" : "点数增加";
                case "ScoreMul":
                    return hasValue ? $"点数倍率提高至{valueText}" : "点数倍率提高";
                case "HanPlus":
                    return hasValue ? $"番数增加{valueText}" : "番数增加";
                case "FuPlus":
                    return hasValue ? $"符数增加{valueText}" : "符数增加";
                default:
                    return string.IsNullOrEmpty(eff.label) ? rawKey : eff.label;
            }

        default:
            return string.IsNullOrEmpty(eff.label) ? rawKey : eff.label;
    }
}
    // 複数枚ユニークに抽選（既所持ID除外）
    public static List<OfudaDef> PickOffers(OfudaExcelLoader.Catalog src, int count, HashSet<string> exclude, System.Random rng = null)
    {
        var list = new List<OfudaDef>();
        if (src == null || count <= 0) return list;
        rng ??= new System.Random();

        var tried = new HashSet<string>();
        int guard = 1024;
        while (list.Count < count && guard-- > 0)
        {
            var one = PickOneByCsvProb(src, rng);
            if (one == null) break;
            if ((exclude != null && exclude.Contains(one.id)) || tried.Contains(one.id)) continue;
            list.Add(one);
            tried.Add(one.id);
        }
        return list;
    }

    // 重み配列からインデックスを返す
    public static int WeightedPickIndex<T>(IList<T> src, Func<T,float> weight, System.Random rng = null)
    {
        if (src == null || src.Count == 0) return -1;
        rng ??= new System.Random();

        float sum = 0f;
        for (int i = 0; i < src.Count; i++)
            sum += Mathf.Max(0f, weight(src[i]));

        if (sum <= 0f)
        {
            // すべて0なら等確率
            return rng.Next(src.Count);
        }

        double r = rng.NextDouble() * sum;
        float acc = 0f;
        for (int i = 0; i < src.Count; i++)
        {
            acc += Mathf.Max(0f, weight(src[i]));
            if (acc >= r) return i;
        }
        // 浮動小数の端数で落ちたときは最後を返す
        return src.Count - 1;
    }

    // CONDにHP/MPが含まれるなら、EFFECTに同軸(HP/MP)が含まれる組合せは不可
    private static bool ConflictsHPMP(OfudaCondition cond, OfudaEffect eff)
    {
        bool condHP = (!string.IsNullOrEmpty(cond?.key)   && cond.key.Contains("HP"))
                   || (!string.IsNullOrEmpty(cond?.label) && cond.label.Contains("HP"));
        bool condMP = (!string.IsNullOrEmpty(cond?.key)   && cond.key.Contains("MP"))
                   || (!string.IsNullOrEmpty(cond?.label) && cond.label.Contains("MP"));

        bool effHP  = (!string.IsNullOrEmpty(eff?.key)    && eff.key.Contains("HP"))
                   || (!string.IsNullOrEmpty(eff?.label)  && eff.label.Contains("HP"));
        bool effMP  = (!string.IsNullOrEmpty(eff?.key)    && eff.key.Contains("MP"))
                   || (!string.IsNullOrEmpty(eff?.label)  && eff.label.Contains("MP"));

        if (condHP && effHP) return true;
        if (condMP && effMP) return true;
        return false;
    }
}
