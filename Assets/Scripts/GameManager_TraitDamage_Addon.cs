// GameManager_TraitDamage_Addon_FIX2.cs
// Consolidated partial: provides ApplyTraitAndCharmEffectsAfterWin() + robust ComputeGekiShunIyu()
// Safe w.r.t. null traitMap entries and case-insensitive yaku names.
// Place in Assets/Scripts/ (not under Editor). Do not attach; partial merges into GameManager.
using System; // StringComparison
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;

public partial class GameManager : MonoBehaviour
{
    // Last computed values (for UI or later reference if needed)

    private static string NormalizeYakuName(string s)
{
    if (string.IsNullOrEmpty(s)) return "";
    var t = s.Trim();

    // 全角→半角の揺れを吸収
    t = t.Replace('（', '(').Replace('）', ')').Replace('×', 'x');

    // 括弧内（例: "(+1)"）を除去
    t = Regex.Replace(t, @"\((.*?)\)", "");

    // 末尾の "x数字"（例: "ドラx2"）を除去
    t = Regex.Replace(t, @"\s*[xX]\s*\d+\s*$", "");

    // 末尾の単独数字や "+数字" を除去（例: "平和 1", "平和 +1" 等の保険）
    t = Regex.Replace(t, @"\s*(\+)?\d+\s*$", "");

    // 余分な空白を整理
    return t.Trim();
}

    /// <summary>
    /// Public entry used from GameManager.ShowScoring(...).
    /// Computes 撃/瞬/癒 based on yaku list and baseScore, stores the result, and appends breakdown lines.
    /// NOTE: この関数は HP を直接変更しません（ダブル適用を避けるため）。
    ///       実際のダメージ/回復反映は、呼び出し側で _lastTraitAttack/_lastTraitHeal を採用してください。
    /// </summary>
// GameManager_TraitDamage_Addon.cs（変更後）
public void ApplyTraitAndCharmEffectsAfterWin(List<string> yaku, int baseScore, List<string> scoringLines = null)
{
    var (atk, heal, shunAdd, gekiMul) = ComputeGekiShunIyu(yaku, baseScore);

    // ==== “今回出たか”を厳密に判定 ====
    bool hasShun = (shunAdd > 0);
    bool hasIyu  = (heal    > 0);
    bool hasGeki = (Mathf.Abs(gekiMul - 1f) > 0.0001f);

    // —— お守りは“該当時だけ”適用 ——
    //  ここで「複利（逐次乗算）・途中丸め」を完全に廃し、
    //  %は合算して最後に一度だけ丸める。
    float shunAddF = shunAdd;
    float healF    = heal;
    float attackF;

    try
    {
        var s = PlayerData.GetEquippedStats();
        float omShun = (hasShun && s.shunAddUp > 0f) ? s.shunAddUp : 0f;   // 例: 0.04
        float omHeal = (hasIyu  && s.iyuHealUp  > 0f) ? s.iyuHealUp  : 0f;
        float omGeki = (hasGeki && s.gekiDmgUp  > 0f) ? s.gekiDmgUp  : 0f;

        // 瞬は加算値にのみ直線的に適用（合算 1+%）
        shunAddF *= (1f + omShun);

        // 癒は回復量にのみ直線的に適用（合算 1+%）
        healF    *= (1f + omHeal);

        // 撃は最終攻撃にのみ直線的に適用（合算 1+%）
        attackF   = (baseScore + shunAddF) * Mathf.Max(1f, gekiMul) * (1f + omGeki);
    }
    catch
    {
        // 取得不能時はお守りなしとして計算（最後に一度だけ丸め）
        attackF = (baseScore + shunAddF) * Mathf.Max(1f, gekiMul);
    }

    // 丸めは最後に一度だけ
    int attackAfterGekiMul = Mathf.RoundToInt(attackF);
    heal    = Mathf.RoundToInt(healF);
    shunAdd = Mathf.RoundToInt(shunAddF);

    _lastTraitAttack = attackAfterGekiMul;
    _lastTraitHeal   = heal;

    // ==== UI行（該当時だけ） ====
    if (scoringLines != null)
    {
        var loc = LocalizationManager.Instance;

        string traitShun = loc != null ? loc.GetFixedText("trait_shun") : "瞬";
        string traitGeki = loc != null ? loc.GetFixedText("trait_geki") : "撃";
        string traitIyu  = loc != null ? loc.GetFixedText("trait_iyu")  : "癒";

        string hpSuffix = loc != null ? loc.GetFixedText("trait_hp_suffix") : "HP";

        string omamoriShunPrefix = loc != null ? loc.GetFixedText("omamori_trait_shun_prefix") : "お守り（瞬） +";
        string omamoriIyuPrefix  = loc != null ? loc.GetFixedText("omamori_trait_iyu_prefix")  : "お守り（癒） +";
        string omamoriGekiPrefix = loc != null ? loc.GetFixedText("omamori_trait_geki_prefix") : "お守り（撃） +";

        string percentSuffix = loc != null ? loc.GetFixedText("percent_suffix") : "%";

        string recommendedDamagePrefix = loc != null ? loc.GetFixedText("recommended_damage_prefix") : "→ 推奨与ダメージ: ";
        string recommendedRecoverPrefix = loc != null ? loc.GetFixedText("recommended_recover_prefix") : " / 回復: ";

        if (hasShun && shunAdd != 0)                scoringLines.Add($"{traitShun} +{shunAdd:#,0}");
        if (hasGeki && Math.Abs(gekiMul - 1f) > 1e-4) scoringLines.Add($"{traitGeki} ×{gekiMul:0.##}");
        if (hasIyu && heal > 0)                    scoringLines.Add($"{traitIyu} +{heal:#,0}{hpSuffix}");

        var s = PlayerData.GetEquippedStats();
        if (hasShun && s.shunAddUp > 0f) scoringLines.Add($"{omamoriShunPrefix}{Mathf.RoundToInt(s.shunAddUp * 100f)}{percentSuffix}");
        if (hasIyu && s.iyuHealUp > 0f)  scoringLines.Add($"{omamoriIyuPrefix}{Mathf.RoundToInt(s.iyuHealUp * 100f)}{percentSuffix}");
        if (hasGeki && s.gekiDmgUp > 0f) scoringLines.Add($"{omamoriGekiPrefix}{Mathf.RoundToInt(s.gekiDmgUp * 100f)}{percentSuffix}");

        scoringLines.Add($"{recommendedDamagePrefix}{_lastTraitAttack:#,0}{recommendedRecoverPrefix}{_lastTraitHeal:#,0}");
    }
}
private (int attackValue, int healValue, int shunAddTotal, float gekiMulTotal)
    ComputeGekiShunIyu(List<string> yaku, int baseScore)
{
    if (yaku == null || yaku.Count == 0 || baseScore <= 0)
        return (baseScore, 0, 0, 1f);

    var (geList, shList, iyList, hostSet) = GetCurrentSkillTraitYakuForScoring();

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

    float gekiPct = 0f;
    float shunPct = 0f;
    float iyuPct = 0f;

    float gekiDelta = 0f;
    float shunDelta = 0f;
    float iyuDelta = 0f;

    try { gekiDelta = Mathf.Max(0f, GetTraitUpgradeDeltaFromPrefs(SkillSetAsset.Trait.Geki, hostSet)); } catch { gekiDelta = 0f; }
    try { shunDelta = Mathf.Max(0f, GetTraitUpgradeDeltaFromPrefs(SkillSetAsset.Trait.Shun, hostSet)); } catch { shunDelta = 0f; }
    try { iyuDelta = Mathf.Max(0f, GetTraitUpgradeDeltaFromPrefs(SkillSetAsset.Trait.Iyu, hostSet)); } catch { iyuDelta = 0f; }

    void AccumulateTraitPct(
        List<string> keys,
        SkillSetAsset.Trait trait,
        ref float totalPct,
        float deltaPerLevel)
    {
        if (keys == null || keys.Count == 0)
            return;

        var countedTraitNormSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var keyRaw in keys)
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

            int effectiveLv = GetTraitEffectiveLevelForScoring(hostSet, activeSkillName, trait, key);
            if (effectiveLv <= 0)
                continue;

            float pct = 0f;

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

            if (hostSet != null && hostSet.traitMap != null && table != null && table.Length > 0)
            {
                SkillSetAsset.YakuTraitEntry matchedEntry = null;

                for (int i = 0; i < hostSet.traitMap.Count; i++)
                {
                    var e = hostSet.traitMap[i];
                    if (e == null)
                        continue;

                    if (e.trait != trait)
                        continue;

                    if (string.IsNullOrWhiteSpace(e.yakuName))
                        continue;

                    string entryNorm = NormalizeTraitJudgeYakuName_Local(e.yakuName);
                    if (string.IsNullOrEmpty(entryNorm))
                        continue;

                    if (!string.Equals(entryNorm, traitNorm, StringComparison.OrdinalIgnoreCase))
                        continue;

                    matchedEntry = e;
                    break;
                }

                if (matchedEntry != null)
                {
                    int di = Mathf.Clamp((int)matchedEntry.difficulty, 0, table.Length - 1);
                    float raw = Mathf.Max(0f, table[di]);
                    pct = tableIsMultiplier ? Mathf.Max(0f, raw - 1f) : raw;
                }
                else
                {
                    float raw = Mathf.Max(0f, table[0]);
                    pct = tableIsMultiplier ? Mathf.Max(0f, raw - 1f) : raw;
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
    }

    AccumulateTraitPct(geList, SkillSetAsset.Trait.Geki, ref gekiPct, gekiDelta);
    AccumulateTraitPct(shList, SkillSetAsset.Trait.Shun, ref shunPct, shunDelta);
    AccumulateTraitPct(iyList, SkillSetAsset.Trait.Iyu, ref iyuPct, iyuDelta);

    float gekiMul = 1f + Mathf.Max(0f, gekiPct);
    int shunAdd = Mathf.RoundToInt(baseScore * Mathf.Max(0f, shunPct));
    int healValue = Mathf.RoundToInt(baseScore * Mathf.Max(0f, iyuPct));
    int attackValue = Mathf.RoundToInt((baseScore + shunAdd) * Mathf.Max(1f, gekiMul));

    return (attackValue, healValue, shunAdd, gekiMul);
}
}
