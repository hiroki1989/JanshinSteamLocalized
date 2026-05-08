#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;
using System;
using System.Collections.Generic;

public static class CsvMini
{
    public static List<string[]> Parse(string text)
    {
        var rows = new List<string[]>();
        using (var sr = new StringReader(text))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
                rows.Add(ParseLine(line));
        }
        return rows;
    }
    private static string[] ParseLine(string line)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        bool inQ = false;
        for (int i=0;i<line.Length;i++)
        {
            char c = line[i];
            if (inQ)
            {
                if (c=='"')
                {
                    if (i+1<line.Length && line[i+1]=='"') { sb.Append('"'); i++; }
                    else inQ=false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c==',') { list.Add(sb.ToString()); sb.Length=0; }
                else if (c=='"') inQ=true;
                else sb.Append(c);
            }
        }
        list.Add(sb.ToString());
        return list.ToArray();
    }
}

public class ImportSkillSetsFromCsv : EditorWindow
{
    private const string Folder = "Assets/Data/SkillSets";

    [MenuItem("Tools/Mahjan/Import SkillSets (from Excel CSV)")]
    private static void ImportSkillSets()
    {
        string path = EditorUtility.OpenFilePanel("Select SkillSets_from_excel.csv", Application.dataPath, "csv");
        if (string.IsNullOrEmpty(path)) return;

        Directory.CreateDirectory(Folder);

        string text = File.ReadAllText(path, Detect(path));
        var rows = CsvMini.Parse(text);
        if (rows.Count == 0) { Debug.LogError("CSV empty"); return; }
        var head = rows[0];
        var col = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        for (int i=0;i<head.Length;i++) col[head[i].Trim()] = i;

        string[] req = {
            "id","displayName","description","maxMP","startMP","regenPerTurn","regenOnWin","skills",
            "gekiEasy","gekiNormal","gekiHard","gekiYakuman",
            "shunEasy","shunNormal","shunHard","shunYakuman",
            "iyuEasy","iyuNormal","iyuHard","iyuYakuman",
            "gekiPerLevel","shunPerLevel","iyuPerLevel","level"
        };
        foreach (var r in req) if (!col.ContainsKey(r)) { Debug.LogError("Missing column: "+r); return; }

        int created=0, updated=0;
        for (int r=1;r<rows.Count;r++)
        {
            var line = rows[r];
            if (line.Length==0) continue;
            string id = Get(line, col["id"]);
            if (string.IsNullOrEmpty(id)) continue;

            string assetPath = $"{Folder}/SkillSet_{Sanitize(id)}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<SkillSetAsset>(assetPath);
            bool isNew = false;
            if (!asset) { asset = ScriptableObject.CreateInstance<SkillSetAsset>(); isNew=true; }

            asset.id          = id;
            asset.displayName = Get(line, col["displayName"]);
            asset.description = Get(line, col["description"]);
            asset.maxMP       = ToInt(Get(line, col["maxMP"]), 10);
            asset.startMP     = ToInt(Get(line, col["startMP"]), asset.maxMP);
            asset.regenPerTurn= ToInt(Get(line, col["regenPerTurn"]), 1);
            asset.regenOnWin  = ToInt(Get(line, col["regenOnWin"]), 3);

            // Active skills
            asset.activeSkills = ParseSkills(Get(line, col["skills"]));

            // Coeffs
            asset.gekiMultiplierByDiff = new float[]{ ToF(Get(line,col["gekiEasy"]),1.1f), ToF(Get(line,col["gekiNormal"]),1.1f), ToF(Get(line,col["gekiHard"]),1.1f), ToF(Get(line,col["gekiYakuman"]),1.1f) };
            asset.shunAddByDiff        = new int[]{ ToInt(Get(line,col["shunEasy"]),3000), ToInt(Get(line,col["shunNormal"]),3000), ToInt(Get(line,col["shunHard"]),3000), ToInt(Get(line,col["shunYakuman"]),3000) };
            asset.iyuHealMulByDiff     = new float[]{ ToF(Get(line,col["iyuEasy"]),0.3f), ToF(Get(line,col["iyuNormal"]),0.3f), ToF(Get(line,col["iyuHard"]),0.3f), ToF(Get(line,col["iyuYakuman"]),0.3f) };

            asset.gekiPerLevel = ToF(Get(line, col["gekiPerLevel"]), 0.05f);
            asset.shunPerLevel = ToInt(Get(line, col["shunPerLevel"]), 300);
            asset.iyuPerLevel  = ToF(Get(line, col["iyuPerLevel"]), 0.02f);
            asset.level        = ToInt(Get(line, col["level"]), 0);

            if (isNew) AssetDatabase.CreateAsset(asset, assetPath);
            EditorUtility.SetDirty(asset);
            if (isNew) created++; else updated++;
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[Import SkillSets] Created:{created} Updated:{updated}");
    }

    [MenuItem("Tools/Mahjan/Import YakuTraits per SkillSet CSV")]
    private static void ImportYakuTraitPerSet()
    {
        string path = EditorUtility.OpenFilePanel("Select YakuTraits_per_set.csv", Application.dataPath, "csv");
        if (string.IsNullOrEmpty(path)) return;

        string text = File.ReadAllText(path, Detect(path));
        var rows = CsvMini.Parse(text);
        if (rows.Count == 0) { Debug.LogError("CSV empty"); return; }

        var head = rows[0];
        var col = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        for (int i=0;i<head.Length;i++) col[head[i].Trim()] = i;

        string[] req = { "skillSetId","yakuName","trait","difficulty" };
        foreach (var r in req) if (!col.ContainsKey(r)) { Debug.LogError("Missing column: "+r); return; }

        // Build mapping of id -> list entries
        var map = new Dictionary<string, List<SkillSetAsset.YakuTraitEntry>>(StringComparer.Ordinal);
        for (int r=1;r<rows.Count;r++)
        {
            var line = rows[r];
            string sid = Get(line, col["skillSetId"]);
            if (string.IsNullOrEmpty(sid)) continue;

            if (!map.TryGetValue(sid, out var list)) { list = new List<SkillSetAsset.YakuTraitEntry>(); map[sid]=list; }

            var e = new SkillSetAsset.YakuTraitEntry();
            e.yakuName = Get(line, col["yakuName"]);
            var t = Get(line, col["trait"]);
            e.trait = t.Equals("Geki", StringComparison.OrdinalIgnoreCase) ? SkillSetAsset.Trait.Geki
                   : t.Equals("Shun", StringComparison.OrdinalIgnoreCase) ? SkillSetAsset.Trait.Shun
                   : SkillSetAsset.Trait.Iyu;

            var d = Get(line, col["difficulty"]);
            e.difficulty = d.Equals("Easy", StringComparison.OrdinalIgnoreCase) ? SkillSetAsset.YakuDifficulty.Easy
                         : d.Equals("Hard", StringComparison.OrdinalIgnoreCase) ? SkillSetAsset.YakuDifficulty.Hard
                         : d.Equals("Yakuman", StringComparison.OrdinalIgnoreCase) ? SkillSetAsset.YakuDifficulty.Yakuman
                         : SkillSetAsset.YakuDifficulty.Normal;

            list.Add(e);
        }

        // Apply to assets
        int updated=0, missing=0;
        string[] assetPaths = AssetDatabase.FindAssets("t:SkillSetAsset");
        foreach (var guid in assetPaths)
        {
            string ap = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<SkillSetAsset>(ap);
            if (asset == null) continue;

            if (map.TryGetValue(asset.id ?? "", out var entries))
            {
                asset.traitMap = entries;
                EditorUtility.SetDirty(asset);
                updated++;
            }
            else
            {
                // keep existing, count missing
                missing++;
            }
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[Import YakuTraits per Set] Updated sets:{updated}, Without mapping:{missing}");
    }

    // helpers
    private static string Get(string[] row, int i) => (i>=0 && i<row.Length) ? (row[i]??"").Trim() : "";
    private static int ToInt(string s, int def) => int.TryParse(s, out var v) ? v : def;
    private static float ToF(string s, float def) => float.TryParse(s, out var v) ? v : def;
    private static Encoding Detect(string path)
    {
        try { using var sr = new StreamReader(path, new UTF8Encoding(false, true)); sr.Peek(); return new UTF8Encoding(false, true); }
        catch { return Encoding.GetEncoding(932); } // Shift_JIS fallback
    }
    private static List<SkillSetAsset.SkillEntry> ParseSkills(string s)
    {
        var list = new List<SkillSetAsset.SkillEntry>();
        if (string.IsNullOrWhiteSpace(s)) return list;
        foreach (var part in s.Split(new[]{';'}, StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split(new[]{':'}, StringSplitOptions.RemoveEmptyEntries);
            var e = new SkillSetAsset.SkillEntry();
            e.activeSkillName = kv[0].Trim();
            e.mpCost = (kv.Length > 1 && int.TryParse(kv[1].Trim(), out var c)) ? c : 0;
            list.Add(e);
        }
        return list;
    }
    private static string Sanitize(string s)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }
}
#endif