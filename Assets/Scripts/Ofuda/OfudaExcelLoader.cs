// Assets/Scripts/Ofuda/OfudaExcelLoader.cs  (CSVローダ)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class OfudaExcelLoader
{
    // ofuda.csv を読み込み、条件/効果と価格帯マップを返す
    public sealed class Catalog
    {
        public List<OfudaCondition> conditions = new();
        public List<OfudaEffect>    effects    = new();
        // priceMap: (maxProbSum, rarity, priceK, priceFixed)
        public List<(float maxProbSum, string rarity, float priceK, int priceFixed)> priceMap = new();
    }

public static Catalog Load(string overridePath = null)
{
    var cat = new Catalog();

#if !UNITY_EDITOR && !USE_STREAMING_OFUDA_CSV
    try
    {
        var so = Resources.Load<OfudaCatalogSO>("OfudaCatalogSO");
        if (so != null)
        {
            cat.conditions = new List<OfudaCondition>();
            if (so.conditions != null)
            {
                for (int i = 0; i < so.conditions.Count; i++)
                {
                    cat.conditions.Add(so.conditions[i]);
                }
            }

            cat.effects = new List<OfudaEffect>();
            if (so.effects != null)
            {
                for (int i = 0; i < so.effects.Count; i++)
                {
                    cat.effects.Add(so.effects[i]);
                }
            }

            cat.priceMap = new List<(float maxProbSum, string rarity, float priceK, int priceFixed)>();
            if (so.priceMap != null)
            {
                for (int i = 0; i < so.priceMap.Count; i++)
                {
                    var b = so.priceMap[i];
                    if (b == null) continue;
                    cat.priceMap.Add((b.maxProbSum, b.rarity, b.priceK, b.priceFixed));
                }
            }

            if (cat.priceMap.Count > 0)
            {
                cat.priceMap.Sort((a, b) => a.maxProbSum.CompareTo(b.maxProbSum));
            }
            else
            {
                cat.priceMap.Add((0.09f, "レジェンダリー", 500f * 0.09f, 500));
                cat.priceMap.Add((0.14f, "エピック",       450f * 0.14f, 450));
                cat.priceMap.Add((0.19f, "レア",           400f * 0.19f, 400));
                cat.priceMap.Add((0.24f, "コモン",         350f * 0.24f, 350));
                cat.priceMap.Add((1.00f, "ノーマル",       300f * 1.00f, 300));
            }

            return cat;
        }
        Debug.LogError("[OfudaExcelLoader] OfudaCatalogSO not found in Resources. Assets/Resources/OfudaCatalogSO.asset を作成してください。");
    }
    catch (Exception ex)
    {
        Debug.LogError($"[OfudaExcelLoader] Load SO failed: {ex}");
    }
#endif

    // デフォルト: StreamingAssets/ofuda.csv（Editor / 開発用）
    string csvPath = overridePath;
    if (string.IsNullOrEmpty(csvPath))
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        csvPath = Path.Combine(Application.streamingAssetsPath, "ofuda.csv");
#else
        csvPath = Path.Combine(Application.streamingAssetsPath, "ofuda.csv");
#endif
    }

    try
    {
        if (!File.Exists(csvPath))
        {
            Debug.LogWarning($"[OfudaExcelLoader] CSV not found: {csvPath}");
            return cat;
        }

        var all = File.ReadAllLines(csvPath);
        if (all.Length == 0) return cat;

        var headers = SplitCsv(all[0]);
        int idxTYPE = headers.FindIndex(s => s.Equals("TYPE", StringComparison.OrdinalIgnoreCase));
        int idxKEY  = headers.FindIndex(s => s.Equals("KEY",  StringComparison.OrdinalIgnoreCase));
        int idxLABEL= headers.FindIndex(s => s.Equals("LABEL",StringComparison.OrdinalIgnoreCase));
        int idxPROB = headers.FindIndex(s => s.Equals("PROB", StringComparison.OrdinalIgnoreCase));
        int idxMAG  = headers.FindIndex(s => s.Equals("MAGNITUDE", StringComparison.OrdinalIgnoreCase));
        int idxUNIT = headers.FindIndex(s => s.Equals("UNIT", StringComparison.OrdinalIgnoreCase));

        int idxMaxProbSum = headers.FindIndex(s => s.Equals("MAXPROBSUM", StringComparison.OrdinalIgnoreCase));
        int idxRarity     = headers.FindIndex(s => s.Equals("RARITY", StringComparison.OrdinalIgnoreCase));
        int idxPriceK     = headers.FindIndex(s => s.Equals("PRICEK", StringComparison.OrdinalIgnoreCase));
        int idxPriceFixed = headers.FindIndex(s => s.Equals("PRICEFIXED", StringComparison.OrdinalIgnoreCase));

        for (int i = 1; i < all.Length; i++)
        {
            var row = SplitCsv(all[i]);
            if (row.Count == 0) continue;

            string type  = Get(row, idxTYPE);
            string key   = Get(row, idxKEY);
            string label = Get(row, idxLABEL);
            float prob   = ParseFloat(Get(row, idxPROB), 0f);
            float mag    = ParseFloat(Get(row, idxMAG), 0f);
            string unit  = Get(row, idxUNIT);

            if (string.IsNullOrWhiteSpace(type)) continue;

            if (type.Equals("COND", StringComparison.OrdinalIgnoreCase))
            {
                cat.conditions.Add(new OfudaCondition{ key = key, label = label, prob = Mathf.Clamp01(prob) });
            }
            else if (type.Equals("EFFECT", StringComparison.OrdinalIgnoreCase))
            {
                cat.effects.Add(new OfudaEffect{
                    key = key, label = label, prob = Mathf.Clamp01(prob),
                    magnitude = mag, unit = unit ?? ""
                });
            }
            else if (type.Equals("PRICEMAP", StringComparison.OrdinalIgnoreCase))
            {
                float maxProbSum = ParseFloat(Get(row, idxMaxProbSum), 0f);
                string rarity = Get(row, idxRarity);
                float priceK = ParseFloat(Get(row, idxPriceK), 0f);
                int priceFixed = 0;
                var pf = Get(row, idxPriceFixed);
                if (!string.IsNullOrWhiteSpace(pf))
                {
                    if (!int.TryParse(pf, NumberStyles.Integer, CultureInfo.InvariantCulture, out priceFixed))
                        int.TryParse(pf, NumberStyles.Integer, CultureInfo.CurrentCulture, out priceFixed);
                }

                if (maxProbSum > 0f && !string.IsNullOrWhiteSpace(rarity))
                {
                    cat.priceMap.Add((maxProbSum, rarity, priceK, priceFixed));
                }
            }
        }

        if (cat.priceMap.Count == 0)
        {
            cat.priceMap.Add((0.09f, "レジェンダリー", 500f * 0.09f, 500));
            cat.priceMap.Add((0.14f, "エピック",       450f * 0.14f, 450));
            cat.priceMap.Add((0.19f, "レア",           400f * 0.19f, 400));
            cat.priceMap.Add((0.24f, "コモン",         350f * 0.24f, 350));
            cat.priceMap.Add((1.00f, "ノーマル",       300f * 1.00f, 300));
        }
        else
        {
            cat.priceMap.Sort((a, b) => a.maxProbSum.CompareTo(b.maxProbSum));
        }
    }
    catch (Exception ex)
    {
        Debug.LogError($"[OfudaExcelLoader] Load failed: {ex}");
    }

    return cat;
}
    private static string Get(List<string> row, int idx)
        => (idx >= 0 && idx < row.Count) ? row[idx] : "";

    private static float ParseFloat(string s, float def)
    {
        if (string.IsNullOrWhiteSpace(s)) return def;
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
        if (float.TryParse(s, out v)) return v;
        return def;
    }

    private static List<string> SplitCsv(string s)
    {
        // ダブルクォート対応の簡易CSV分割
        var list = new List<string>();
        bool inQ = false;
        var cur = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\"')
            {
                if (inQ && i + 1 < s.Length && s[i + 1] == '\"')
                {
                    cur.Append('\"'); i++; // エスケープされたダブルクォート
                }
                else
                {
                    inQ = !inQ;
                }
            }
            else if (c == ',' && !inQ)
            {
                list.Add(cur.ToString());
                cur.Length = 0;
            }
            else
            {
                cur.Append(c);
            }
        }
        list.Add(cur.ToString());
        return list;
    }
}
