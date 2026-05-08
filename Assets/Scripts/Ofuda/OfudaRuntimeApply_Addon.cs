// Assets/Scripts/Ofuda/OfudaRuntimeApply_Addon.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public partial class GameManager
{
private void ApplyOfudaScoringModifiers(
    string winTile, int fu, int han, List<string> yaku, int baseScore,
    ref float mult, ref int extra, List<string> lines,
    out int hpHealAbs, out int mpHealAbs)
{
    hpHealAbs = 0;
    mpHealAbs = 0;

    var ofudaIds = LoadRunOfudaIds();
    if (ofudaIds == null || ofudaIds.Count == 0) return;

    foreach (var id in ofudaIds)
    {
        if (string.IsNullOrEmpty(id)) continue;
        var parts = id.Split(new[] { "__" }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) continue;

        var cond   = parts[0];
        var effect = parts[1];

        if (!Ofuda_Cond_Passes(cond, winTile, fu, han, yaku, baseScore)) continue;

        // ---- 点数倍率 ----
        if (IsOfudaScoreMultiplierEffect(effect))
        {
            float mul = ResolveMultiplier(effect);
            if (mul > 0f)
            {
                mult *= mul;
                lines?.Add($"お札：{Pretty(effect)} → 倍率 ×{mul:0.##}");
            }
            continue;
        }

        // ---- HP回復（最大値％） ----
        if (IsOfudaHpHealEffect(effect))
        {
            float pct = ResolvePercent01(effect);
            int add   = Mathf.RoundToInt(playerMaxHP * pct);
            if (add > 0)
            {
                hpHealAbs += add;
                lines?.Add($"お札：{Pretty(effect)} → HP +{add}");
            }
            continue;
        }

        // ---- MP回復（最大値％） ----
        if (IsOfudaMpHealEffect(effect))
        {
            float pct  = ResolvePercent01(effect);
            int maxMp = GetMaxMpLike(0);
            if (maxMp > 0)
            {
                int add = Mathf.RoundToInt(maxMp * pct);
                if (add > 0)
                {
                    mpHealAbs += add;
                    lines?.Add($"お札：{Pretty(effect)} → MP +{add}");
                }
            }
            continue;
        }
    }
}
    private static OfudaExcelLoader.Catalog _ofudaCsvCache;
    private static Dictionary<string, OfudaEffect> _ofudaEffectByKey;
private static void EnsureOfudaCsvCache()
{
    if (_ofudaCsvCache != null && _ofudaEffectByKey != null) return;

    _ofudaCsvCache = OfudaExcelLoader.Load();
    _ofudaEffectByKey = new Dictionary<string, OfudaEffect>(StringComparer.Ordinal);

    if (_ofudaCsvCache != null && _ofudaCsvCache.effects != null)
    {
        foreach (var e in _ofudaCsvCache.effects)
        {
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.key)) continue;

            string rawKey = e.key;
            string normalizedKey = NormalizeOfudaKey(e.key);

            if (!_ofudaEffectByKey.ContainsKey(rawKey))
                _ofudaEffectByKey.Add(rawKey, e);

            if (!string.IsNullOrEmpty(normalizedKey) && !_ofudaEffectByKey.ContainsKey(normalizedKey))
                _ofudaEffectByKey.Add(normalizedKey, e);
        }
    }
}
private static bool TryGetEffectMagnitudeFromCsv(string effectKey, out float magnitude, out string unit)
{
    magnitude = 0f;
    unit = "";

    if (string.IsNullOrEmpty(effectKey)) return false;

    EnsureOfudaCsvCache();

    if (_ofudaEffectByKey == null) return false;

    OfudaEffect eff = null;

    if (!_ofudaEffectByKey.TryGetValue(effectKey, out eff) || eff == null)
    {
        string normalizedKey = NormalizeOfudaKey(effectKey);
        if (string.IsNullOrEmpty(normalizedKey)) return false;
        if (!_ofudaEffectByKey.TryGetValue(normalizedKey, out eff) || eff == null) return false;
    }

    magnitude = eff.magnitude;
    unit = eff.unit ?? "";
    return true;
}
    private static float ResolvePercent01(string effectKey)
    {
        if (TryGetEffectMagnitudeFromCsv(effectKey, out var mag, out var unit))
        {
            if (mag > 0f)
            {
                if (unit == "%") return mag / 100f;
                if (mag <= 1f) return mag;
                return mag / 100f;
            }
        }

        return ParsePercentFromEffect(effectKey, 0f);
    }

    private static float ResolveMultiplier(string effectKey)
    {
        if (TryGetEffectMagnitudeFromCsv(effectKey, out var mag, out var unit))
        {
            if (mag > 0f)
            {
                return mag;
            }
        }

        return ParseMultiplierFromEffect(effectKey, 1f);
    }
private bool Ofuda_Cond_Passes(string cond, string winTile, int fu, int han, List<string> yaku, int baseScore)
{
    if (string.IsNullOrEmpty(cond))
        return false;

    string limitKey = NormalizeOfudaLimitKey_Local(cond);
    string normalized = NormalizeOfudaCond(cond);

    // _lastScoringBasePoints には Scoring.TryScoreWin(...).basePoint を入れている。
    // よって満貫以上判定のしきい値は
    // 満貫=2000, 跳満=3000, 倍満=4000, 三倍満=6000, 役満=8000
    switch (limitKey)
    {
        case "ManganUp":
            return baseScore >= 2000;

        case "HanemanUp":
            return baseScore >= 3000;

        case "BaimanUp":
            return baseScore >= 4000;

        case "SanbaimanUp":
            return baseScore >= 6000;

        case "Yakuman":
            return baseScore >= 8000;
    }

    if (normalized.Contains("3ターン以内"))
        return _playerTsumoCountThisRound > 0 && _playerTsumoCountThisRound <= 3;

    if (normalized.Contains("カンをした状態"))
        return HasKanMeld();

    if (normalized.Contains("ポンをした状態"))
        return HasPonMeld();

    if (normalized.Contains("チーをした状態"))
        return HasChiMeld();

    if (normalized.Contains("ホンイツ") || normalized.Contains("清一色"))
        return ListContainsAnyYaku(yaku, "清一色", "混一色", "ホンイツ");

    if (normalized.Contains("対々和") || normalized.Contains("七対子"))
        return ListContainsAnyYaku(yaku, "対々和", "七対子");

    if (normalized.Contains("平和"))
        return ListContainsAnyYaku(yaku, "平和");

    if (CondIsHpLePercent(normalized, 10)) return IsPlayerHpPercentLessOrEqual(0.10f);
    if (CondIsHpLePercent(normalized, 25)) return IsPlayerHpPercentLessOrEqual(0.25f);
    if (CondIsHpLePercent(normalized, 50)) return IsPlayerHpPercentLessOrEqual(0.50f);
    if (CondIsHpGePercent(normalized, 50)) return IsPlayerHpPercentGreaterOrEqual(0.50f);
    if (CondIsHpGePercent(normalized, 75)) return IsPlayerHpPercentGreaterOrEqual(0.75f);
    if (CondIsHpEq100Percent(normalized))  return IsPlayerHpPercentGreaterOrEqual(1.00f);

    if (CondIsMpEq100Percent(normalized))  return IsPlayerMpPercentGreaterOrEqual(1.00f);
    if (CondIsMpLePercent(normalized, 10)) return IsPlayerMpPercentLessOrEqual(0.10f);
    if (CondIsMpLePercent(normalized, 25)) return IsPlayerMpPercentLessOrEqual(0.25f);
    if (CondIsMpLePercent(normalized, 50)) return IsPlayerMpPercentLessOrEqual(0.50f);
    if (CondIsMpGePercent(normalized, 50)) return IsPlayerMpPercentGreaterOrEqual(0.50f);
    if (CondIsMpGePercent(normalized, 75)) return IsPlayerMpPercentGreaterOrEqual(0.75f);

    return false;
}
private static string NormalizeOfudaLimitKey_Local(string cond)
{
    if (string.IsNullOrEmpty(cond))
        return "";

    string s = NormalizeOfudaCond(cond);
    s = s.Replace("COND:", "");

    if (s == "ManganUp" || s.Contains("満貫以上"))
        return "ManganUp";

    if (s == "HanemanUp" || s.Contains("跳満以上"))
        return "HanemanUp";

    if (s == "BaimanUp" || s.Contains("倍満以上"))
        return "BaimanUp";

    if (s == "SanbaimanUp" || s.Contains("三倍満以上"))
        return "SanbaimanUp";

    if (s == "Yakuman" || s.Contains("役満"))
        return "Yakuman";

    return s;
}
private static string NormalizeOfudaKey(string key)
{
    if (string.IsNullOrEmpty(key)) return "";
    return key.Replace("COND:", "").Replace("EFFECT:", "").Trim();
}
private static bool IsOfudaScoreMultiplierEffect(string effect)
{
    return !string.IsNullOrEmpty(effect) && effect.StartsWith("EFFECT:点数が", StringComparison.Ordinal);
}

private static bool IsOfudaHpHealEffect(string effect)
{
    return !string.IsNullOrEmpty(effect) && effect.StartsWith("EFFECT:HPを", StringComparison.Ordinal);
}

private static bool IsOfudaMpHealEffect(string effect)
{
    return !string.IsNullOrEmpty(effect) && effect.StartsWith("EFFECT:MPを", StringComparison.Ordinal);
}
    private bool ListContainsAnyYaku(List<string> yaku, params string[] keys)
    {
        if (yaku == null || keys == null) return false;
        foreach (var s in yaku)
        {
            if (string.IsNullOrEmpty(s)) continue;
            foreach (var k in keys) if (!string.IsNullOrEmpty(k) && s.Contains(k)) return true;
        }
        return false;
    }
private static string NormalizeOfudaCond(string cond)
{
    if (string.IsNullOrEmpty(cond)) return "";
    return cond.Replace("％", "%").Replace("　", " ").Trim();
}

private static bool CondIsHpLePercent(string cond, int percent)
{
    string s = NormalizeOfudaCond(cond);
    return s.Contains("HP") && s.Contains(percent.ToString()) && s.Contains("%") && s.Contains("以下");
}

private static bool CondIsHpGePercent(string cond, int percent)
{
    string s = NormalizeOfudaCond(cond);
    return s.Contains("HP") && s.Contains(percent.ToString()) && s.Contains("%") && s.Contains("以上");
}

private static bool CondIsHpEq100Percent(string cond)
{
    string s = NormalizeOfudaCond(cond);
    return s.Contains("HP") && s.Contains("100") && s.Contains("%") && s.Contains("状態");
}

private static bool CondIsMpLePercent(string cond, int percent)
{
    string s = NormalizeOfudaCond(cond);
    return s.Contains("MP") && s.Contains(percent.ToString()) && s.Contains("%") && s.Contains("以下");
}

private static bool CondIsMpGePercent(string cond, int percent)
{
    string s = NormalizeOfudaCond(cond);
    return s.Contains("MP") && s.Contains(percent.ToString()) && s.Contains("%") && s.Contains("以上");
}

private static bool CondIsMpEq100Percent(string cond)
{
    string s = NormalizeOfudaCond(cond);
    return s.Contains("MP") && s.Contains("100") && s.Contains("%") && s.Contains("状態");
}

private bool IsPlayerHpPercentLessOrEqual(float threshold01)
{
    if (playerMaxHP <= 0) return false;
    float ratio = (float)playerHP / (float)playerMaxHP;
    return ratio <= threshold01;
}

private bool IsPlayerHpPercentGreaterOrEqual(float threshold01)
{
    if (playerMaxHP <= 0) return false;
    float ratio = (float)playerHP / (float)playerMaxHP;
    return ratio >= threshold01;
}

private bool IsPlayerMpPercentLessOrEqual(float threshold01)
{
    int mp = GetMpLike(-1);
    int mx = GetMaxMpLike(-1);
    if (mp < 0 || mx <= 0) return false;

    float ratio = (float)mp / (float)mx;
    return ratio <= threshold01;
}

private bool IsPlayerMpPercentGreaterOrEqual(float threshold01)
{
    int mp = GetMpLike(-1);
    int mx = GetMaxMpLike(-1);
    if (mp < 0 || mx <= 0) return false;

    float ratio = (float)mp / (float)mx;
    return ratio >= threshold01;
}
private int GetMpLike(int fallback)
{
    // このプロジェクトのMP実体は GameManager_SkillMP_Addon.cs の private int _mp
    // partial class なのでここから直接参照できる
    try
    {
        return _mp;
    }
    catch
    {
        // 万一アクセスできない環境でも落とさない
        return fallback;
    }
}
private int GetMaxMpLike(int fallback)
{
    // このプロジェクトの最大MPは GameManager_SkillMP_Addon.cs の EffectiveMaxMP() が正
    try
    {
        int mx = EffectiveMaxMP();
        return mx > 0 ? mx : fallback;
    }
    catch
    {
        return fallback;
    }
}

private int ReflectGetIntLike(string[] names, int fallback)
{
    if (names == null || names.Length == 0) return fallback;

    foreach (var n in names)
    {
        if (TryReflectGetInt(n, out var v)) return v;
    }
    return fallback;
}

private bool TryReflectGetInt(string fieldOrPropName, out int value)
{
    value = default;
    try
    {
        var t = this.GetType();

        var f = t.GetField(fieldOrPropName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(int))
        {
            value = (int)f.GetValue(this);
            return true;
        }

        var p = t.GetProperty(fieldOrPropName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        if (p != null && p.PropertyType == typeof(int))
        {
            value = (int)p.GetValue(this);
            return true;
        }
    }
    catch { }

    return false;
}

    private bool HasPonMeld()
    {
        if (melds == null) return false;
        foreach (var m in melds)
        {
            if (m == null) continue;
            var ids = m.Select(x => x != null && x.EndsWith("*") ? x[..^1] : x).ToList();
            if (ids.Count == 3 && ids[0] == ids[1] && ids[1] == ids[2]) return true;
        }
        return false;
    }

    private bool HasChiMeld()
    {
        if (melds == null) return false;
        foreach (var m in melds)
        {
            if (m == null) continue;
            var ids = m.Select(x => x != null && x.EndsWith("*") ? x[..^1] : x).ToList();
            if (ids.Count == 3 &&
                TryParseSuitNum(ids[0], out var s0, out var n0) &&
                TryParseSuitNum(ids[1], out var s1, out var n1) &&
                TryParseSuitNum(ids[2], out var s2, out var n2) &&
                s0 == s1 && s1 == s2)
            {
                var ns = new[] { n0, n1, n2 }.OrderBy(x => x).ToArray();
                if (ns[0] + 1 == ns[1] && ns[1] + 1 == ns[2]) return true;
            }
        }
        return false;
    }

    private bool HasKanMeld()
    {
        if (melds == null) return false;
        foreach (var m in melds)
        {
            if (m == null) continue;
            var ids = m.Select(x => x != null && x.EndsWith("*") ? x[..^1] : x).ToList();
            if (ids.Count == 4 && ids.Distinct().Count() == 1) return true;
        }
        return false;
    }
private static string Pretty(string effectKey)
{
    if (TryGetEffectLabelFromCsv(effectKey, out var label) && !string.IsNullOrEmpty(label))
        return label;

    return (effectKey ?? "").Replace("EFFECT:", "").Replace("COND:", "");
}
private static bool TryGetEffectLabelFromCsv(string effectKey, out string label)
{
    label = "";

    if (string.IsNullOrEmpty(effectKey)) return false;

    EnsureOfudaCsvCache();

    if (_ofudaEffectByKey == null) return false;
    if (!_ofudaEffectByKey.TryGetValue(effectKey, out var eff) || eff == null) return false;

    label = eff.label ?? "";
    return !string.IsNullOrEmpty(label);
}
private float TryGetLegacyLastOfudaMagnitudeOr1()
{
    try
    {
        if (TryGetLastOfudaMagnitude(out float jsonMag) && jsonMag > 0f) return jsonMag;
    }
    catch
    {
    }
    return 1f;
}
    private static float ParseMultiplierFromEffect(string effect, float def)
    {
        try
        {
            int s = effect.IndexOf('が'); int e = effect.IndexOf('倍');
            if (s >= 0 && e > s)
            {
                var sub = effect[(s + 1)..e].Trim();
                if (float.TryParse(sub, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
                if (float.TryParse(sub, NumberStyles.Float, CultureInfo.CurrentCulture,     out v)) return v;
            }
        } catch {}
        return def;
    }

    private static float ParsePercentFromEffect(string effect, float def)
    {
        try
        {
            int s = effect.IndexOf('を'); int e = effect.IndexOf('％');
            if (s >= 0 && e > s)
            {
                var sub = effect[(s + 1)..e].Trim();
                if (float.TryParse(sub, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v / 100f;
                if (float.TryParse(sub, NumberStyles.Float, CultureInfo.CurrentCulture,     out v)) return v / 100f;
            }
        } catch {}
        return def;
    }

    private int ReflectGetInt(string fieldName, int fallback)
    {
        try
        {
            var f = typeof(GameManager).GetField(fieldName,
                     System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(int))
                return (int)f.GetValue(this);
        } catch {}
        return fallback;
    }
private static List<string> LoadRunOfudaIds()
{
    // ★順序付き3枠の共通APIからロード（ここで最大3枠が保証される）
    var list = OfudaRunInventory.LoadList();
    return list ?? new List<string>();
}
    private static bool TryGetLastOfudaMagnitude(out float magnitude)
    {
        magnitude = 0f;
        try
        {
            var raw = PlayerPrefs.GetString("LastOfudaJson", "");
            if (string.IsNullOrEmpty(raw)) return false;
            var key = "\"magnitude\"";
            int i = raw.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (i >= 0)
            {
                int j = raw.IndexOfAny("0123456789.-".ToCharArray(), i + key.Length);
                int k = j; while (k < raw.Length && "0123456789.eE+-".IndexOf(raw[k]) >= 0) k++;
                if (float.TryParse(raw.Substring(j, k - j), NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                { magnitude = f; return true; }
            }
        } catch {}
        return false;
    }
private void ApplyRunOfudaModifiers(List<string> yaku, ref float mult, ref int extra, List<string> lines)
{
    var ofudaIds = LoadRunOfudaIds();
    if (ofudaIds == null || ofudaIds.Count == 0) return;

    foreach (var id in ofudaIds)
    {
        if (string.IsNullOrEmpty(id)) continue;

        string cond = null, effect = null;
        var parts = id.Split(new[] { "__" }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2) { cond = parts[0]; effect = parts[1]; }
        else if (id.StartsWith("EFFECT:")) { effect = id; }
        else if (id.StartsWith("COND:"))   { cond   = id; } // 効果なし→無視

        // 直前の和了文脈で条件判定
        if (!string.IsNullOrEmpty(cond))
        {
            if (!Ofuda_Cond_Passes_Runtime(cond)) continue;
        }

        if (string.IsNullOrEmpty(effect)) continue;

        if (IsOfudaScoreMultiplierEffect(effect))
        {
            float mul = ResolveMultiplier(effect);
            if (mul > 0f && mul != 1f)
            {
                mult *= mul;
                lines?.Add($"お札：{Pretty(effect)} → 倍率 ×{mul:0.##}");
            }
        }
        // HP/MP%回復は PostScoring でまとめて処理
    }
}
private void ApplyRunOfuda_PostScoring(ref int damage, ref int hpHeal, ref int mpHeal, List<string> lines)
{
    var ofudaIds = LoadRunOfudaIds();
    if (ofudaIds == null || ofudaIds.Count == 0) return;

    foreach (var id in ofudaIds)
    {
        if (string.IsNullOrEmpty(id)) continue;

        string cond = null, effect = null;
        var parts = id.Split(new[] { "__" }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2) { cond = parts[0]; effect = parts[1]; }
        else if (id.StartsWith("EFFECT:")) { effect = id; }
        else if (id.StartsWith("COND:"))   { cond   = id; }

        // 直前の和了文脈で条件判定
        if (!string.IsNullOrEmpty(cond))
        {
            if (!Ofuda_Cond_Passes_Runtime(cond)) continue;
        }
        if (string.IsNullOrEmpty(effect)) continue;

        if (IsOfudaHpHealEffect(effect))
        {
            float pct = ResolvePercent01(effect);
            int add   = Mathf.RoundToInt(playerMaxHP * pct);
            if (add > 0)
            {
                hpHeal += add;
                lines?.Add($"お札：{Pretty(effect)} → HP +{add}");
            }
        }

        // --- MP 回復％ ---
        if (IsOfudaMpHealEffect(effect))
        {
            int maxMp = GetMaxMpLike(0);
            if (maxMp > 0)
            {
                float pct = ResolvePercent01(effect);
                int add   = Mathf.RoundToInt(maxMp * pct);
                if (add > 0)
                {
                    mpHeal += add;
                    lines?.Add($"お札：{Pretty(effect)} → MP +{add}");
                }
            }
        }
    }
}
private bool Ofuda_Cond_Passes_Runtime(string cond)
{
    // winTile は使っていないので null でOK
    return Ofuda_Cond_Passes(
        winTile: null,
        fu: 0,
        han: 0,
        yaku: _lastScoringYaku,
        baseScore: _lastScoringBasePoints,
        cond: cond);
}

}
