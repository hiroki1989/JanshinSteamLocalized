// EnemyConfigExcel.cs （NPOI不使用・ExcelDataReader版）
// 置き場所: Assets/Scripts/
// 依存DLL: ExcelDataReader.dll

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ExcelDataReader;  // ← ExcelDataReader を使用
[Serializable]
public class EnemySkillConfig
{
    // Excel 側の Skill?_Id / Skill?_X / Skill?_Y / Skill?_Z をそのまま格納する単位
    public string id;
    public int paramX;
    public int paramY;
    public int paramZ;

    public EnemySkillConfig() { }

    public EnemySkillConfig(string id, int x, int y, int z)
    {
        this.id    = id;
        this.paramX = x;
        this.paramY = y;
        this.paramZ = z;
    }
}
[Serializable]
public class EnemyConfig
{
    public int    index;

    // 内部基準名・既存資産参照用（日本語のまま維持）
    public string name;

    // 表示名（多言語）
    public string displayNameJapanese;
    public string displayNameEnglish;
    public string displayNameChineseSimplified;

    public int    maxHP;

    // ★敵スキル設定（enemy_config.xlsx の列から読込）
    // 例：Skill1Id / Skill1X / Skill1Y / Skill1Z, Skill2Id ... といった列
    public List<EnemySkillConfig> skills = new List<EnemySkillConfig>();

    public int[] man = new int[10];
    public int[] pin = new int[10];
    public int[] sou = new int[10];
    public int honors = 1;
    public string deck;   // ★ AF列「Deck」を格納するフィールドを追加

    public EnemyConfig(int idx)
    {
        index = idx;
        maxHP = 100;
        for (int i = 1; i <= 9; i++) { man[i] = 1; pin[i] = 1; sou[i] = 1; }
        honors = 1;
    }
public string GetLocalizedDisplayName()
{
    var lm = LocalizationManager.Instance;
    if (lm == null)
    {
        if (!string.IsNullOrEmpty(displayNameJapanese)) return displayNameJapanese;
        if (!string.IsNullOrEmpty(name)) return name;
        return "";
    }

    switch (lm.CurrentLanguage)
    {
        case LocalizationManager.Language.English:
            if (!string.IsNullOrEmpty(displayNameEnglish)) return displayNameEnglish;

            if (!string.IsNullOrEmpty(name))
            {
                string byDict = lm.GetEnemyDisplayName(name);
                if (!string.IsNullOrEmpty(byDict) && byDict != name)
                    return byDict;
            }

            if (!string.IsNullOrEmpty(displayNameJapanese)) return displayNameJapanese;
            if (!string.IsNullOrEmpty(name)) return name;
            return "";

        case LocalizationManager.Language.ChineseSimplified:
            if (!string.IsNullOrEmpty(displayNameChineseSimplified)) return displayNameChineseSimplified;

            if (!string.IsNullOrEmpty(name))
            {
                string byDict = lm.GetEnemyDisplayName(name);
                if (!string.IsNullOrEmpty(byDict) && byDict != name)
                    return byDict;
            }

            if (!string.IsNullOrEmpty(displayNameJapanese)) return displayNameJapanese;
            if (!string.IsNullOrEmpty(name)) return name;
            return "";

        case LocalizationManager.Language.Japanese:
        default:
            if (!string.IsNullOrEmpty(displayNameJapanese)) return displayNameJapanese;
            if (!string.IsNullOrEmpty(name)) return name;
            return "";
    }
}
    public string GetLocalizedDisplayNameWithLoop(int loop)
    {
        string baseName = GetLocalizedDisplayName();
        if (string.IsNullOrEmpty(baseName)) return "";

        return (loop > 0) ? $"{baseName} +{loop}" : baseName;
    }

    // --- 互換エイリアス（GameManager の既存参照に合わせる）---
    public int[] weightMan    => man;
    public int[] weightPin    => pin;
    public int[] weightSou    => sou;
    public int   weightHonors => honors;
}
public static class EnemyConfigExcel
{
static EnemyConfigExcel()
{
    TryRegisterCodePages();  // あれば登録、無ければ何もしない
}
    private const string DefaultFileName = "enemy_config.xlsx";
    private const string DefaultSheetName = "Enemies";

    private static Dictionary<int, EnemyConfig> _cache;

public static Dictionary<int, EnemyConfig> LoadAll(string filePath = null, string sheetName = null)
{
    if (_cache != null) return _cache;

#if !UNITY_EDITOR && !USE_STREAMING_ENEMY_EXCEL
    _cache = LoadAllFromResources();
    return _cache;
#else
    filePath ??= Path.Combine(Application.streamingAssetsPath, DefaultFileName);
    sheetName ??= DefaultSheetName;

    var dict = new Dictionary<int, EnemyConfig>();
    try
    {
        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"[EnemyConfigExcel] File not found: {filePath}");
            _cache = dict;
            return dict;
        }

        using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = ExcelReaderFactory.CreateReader(stream))
        {
            bool found = string.Equals(reader.Name, sheetName, StringComparison.OrdinalIgnoreCase);
            while (!found && reader.NextResult())
                found = string.Equals(reader.Name, sheetName, StringComparison.OrdinalIgnoreCase);

            ReadSheet(reader, dict);
        }
    }
    catch (Exception e)
    {
        Debug.LogError($"[EnemyConfigExcel] Load failed: {e.GetType().Name} {e.Message}");
    }

    _cache = dict;
    return dict;
#endif
}
private static bool TryGetStringAny(
    IExcelDataReader reader,
    Dictionary<string,int> col,
    out string val,
    params string[] keys)
{
    val = null;
    if (keys == null || keys.Length == 0) return false;

    for (int i = 0; i < keys.Length; i++)
    {
        string key = keys[i];
        if (string.IsNullOrEmpty(key)) continue;

        if (TryGetString(reader, col, key, out val))
            return true;
    }

    val = null;
    return false;
}
private static Dictionary<int, EnemyConfig> LoadAllFromResources()
{
    var dict = new Dictionary<int, EnemyConfig>();
    try
    {
        var db = Resources.Load<EnemyConfigDatabaseSO>("EnemyConfigDatabaseSO");
        if (db == null)
        {
            Debug.LogError("[EnemyConfigExcel] EnemyConfigDatabaseSO not found in Resources. Assets/Resources/EnemyConfigDatabaseSO.asset を作成してください。");
            return dict;
        }
        dict = db.ToDictionary();
        return dict;
    }
    catch (Exception e)
    {
        Debug.LogError($"[EnemyConfigExcel] LoadAllFromResources failed: {e.GetType().Name} {e.Message}");
        return new Dictionary<int, EnemyConfig>();
    }
}
    public static bool TryGet(int enemyIndex, out EnemyConfig cfg)
    {
        if (_cache == null) LoadAll();
        return _cache.TryGetValue(enemyIndex, out cfg);
    }
    // ===== 内部処理 =====
    // ===== 内部処理 =====
    private static void ReadSheet(IExcelDataReader reader, Dictionary<int, EnemyConfig> dict)
    {
        // ヘッダ
        if (!reader.Read()) return;
        var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int c = 0; c < reader.FieldCount; c++)
        {
            string hn = (reader.GetValue(c)?.ToString() ?? "").Trim();
            if (!string.IsNullOrEmpty(hn) && !col.ContainsKey(hn)) col[hn] = c;
        }

        // データ行
        while (reader.Read())
        {
            if (!TryGetInt(reader, col, "EnemyIndex", out var idx)) continue;

var cfg = new EnemyConfig(idx);

if (TryGetString(reader, col, "Name", out var nm))
    cfg.name = nm;

// 表示名（多言語）
if (TryGetStringAny(reader, col, out var nameJa, "NameJa", "DisplayNameJa", "JapaneseName", "NameJP"))
    cfg.displayNameJapanese = nameJa;
else
    cfg.displayNameJapanese = cfg.name;

if (TryGetStringAny(reader, col, out var nameEn, "NameEn", "DisplayNameEn", "NameEnglish", "EnglishName"))
    cfg.displayNameEnglish = nameEn;

if (TryGetStringAny(reader, col, out var nameZhHans, "NameZhHans", "DisplayNameZhHans", "NameZh", "ChineseSimplifiedName"))
    cfg.displayNameChineseSimplified = nameZhHans;

if (TryGetInt(reader, col, "MaxHP", out var hp))   cfg.maxHP  = Mathf.Max(1, hp);
if (TryGetInt(reader, col, "Honors", out var hon)) cfg.honors = Mathf.Max(0, hon);
if (TryGetString(reader, col, "Deck", out var deck)) cfg.deck = deck;
            bool perNum =
                HasAnyKeys(col, "Man1","Man2","Man3","Man4","Man5","Man6","Man7","Man8","Man9") ||
                HasAnyKeys(col, "Pin1","Pin2","Pin3","Pin4","Pin5","Pin6","Pin7","Pin8","Pin9") ||
                HasAnyKeys(col, "Sou1","Sou2","Sou3","Sou4","Sou5","Sou6","Sou7","Sou8","Sou9");
            if (perNum)
            {
                for (int n = 1; n <= 9; n++)
                {
                    if (TryGetInt(reader, col, "Man"+n, out var v1)) cfg.man[n] = Mathf.Max(0, v1);
                    if (TryGetInt(reader, col, "Pin"+n, out var v2)) cfg.pin[n] = Mathf.Max(0, v2);
                    if (TryGetInt(reader, col, "Sou"+n, out var v3)) cfg.sou[n] = Mathf.Max(0, v3);
                }
            }
            else
            {
                if (TryGetInt(reader, col, "Man", out var w1)) for (int n = 1; n <= 9; n++) cfg.man[n] = Mathf.Max(0, w1);
                if (TryGetInt(reader, col, "Pin", out var w2)) for (int n = 1; n <= 9; n++) cfg.pin[n] = Mathf.Max(0, w2);
                if (TryGetInt(reader, col, "Sou", out var w3)) for (int n = 1; n <= 9; n++) cfg.sou[n] = Mathf.Max(0, w3);
            }

            // ★ ここから追記：Skill1〜Skill3 を読み取って cfg.skills に詰める
            ReadEnemySkillsFromColumns(reader, col, cfg);

            dict[idx] = cfg;
        }
    }
public const int SecretBossExcelKey = 10;

public static int GetNormalEnemyCount()
{
    var dict = LoadAll();
    if (dict == null || dict.Count == 0) return 0;

    int c = 0;
    foreach (var k in dict.Keys)
    {
        if (k < SecretBossExcelKey) c++;
    }
    return c;
}

public static bool IsSecretBossKey(int excelKey)
{
    return excelKey == SecretBossExcelKey;
}

    /// Excel の Skill?_Id / Skill?_X / Skill?_Y / Skill?_Z 列から EnemyConfig.skills に変換して詰める。
    /// 対象列が存在しなければ何もしない。
    /// 
    /// 列名は「Skill1Id / Skill1X ...」と「Skill1_Id / Skill1_X ...」の
    /// どちらでも動くようにしている（既存シートとの互換のため）。
    /// </summary>
    private static void ReadEnemySkillsFromColumns(
        IExcelDataReader reader,
        Dictionary<string,int> col,
        EnemyConfig cfg)
    {
        if (col == null || cfg == null) return;

        // 最大3スキル分読み込む（列が無ければスキップされる）
        for (int s = 1; s <= 3; s++)
        {
            // --- Id 列名を柔軟に解決（Skill1Id / Skill1_Id の両方対応）---
            string idCol1 = "Skill" + s + "Id";
            string idCol2 = "Skill" + s + "_Id";
            string idKey = null;

            if (col.ContainsKey(idCol1))      idKey = idCol1;
            else if (col.ContainsKey(idCol2)) idKey = idCol2;

            if (string.IsNullOrEmpty(idKey)) continue; // このスロット自体が存在しない

            if (!TryGetString(reader, col, idKey, out var id) || string.IsNullOrEmpty(id))
                continue; // 空ならスキップ

            var sc = new EnemySkillConfig { id = id };

            // --- X / Y / Z もアンダースコアあり・なし両方を見る ---
            string xCol1 = "Skill" + s + "X";
            string xCol2 = "Skill" + s + "_X";
            string yCol1 = "Skill" + s + "Y";
            string yCol2 = "Skill" + s + "_Y";
            string zCol1 = "Skill" + s + "Z";
            string zCol2 = "Skill" + s + "_Z";

            // X
            if (col.ContainsKey(xCol1) && TryGetInt(reader, col, xCol1, out var px1))
                sc.paramX = px1;
            else if (col.ContainsKey(xCol2) && TryGetInt(reader, col, xCol2, out var px2))
                sc.paramX = px2;

            // Y
            if (col.ContainsKey(yCol1) && TryGetInt(reader, col, yCol1, out var py1))
                sc.paramY = py1;
            else if (col.ContainsKey(yCol2) && TryGetInt(reader, col, yCol2, out var py2))
                sc.paramY = py2;

            // Z
            if (col.ContainsKey(zCol1) && TryGetInt(reader, col, zCol1, out var pz1))
                sc.paramZ = pz1;
            else if (col.ContainsKey(zCol2) && TryGetInt(reader, col, zCol2, out var pz2))
                sc.paramZ = pz2;

            // （Y や Z が 0 のままでも仕様通り「0」として扱う）
            cfg.skills.Add(sc);
        }
    }


    private static bool TryGetString(IExcelDataReader reader, Dictionary<string,int> col, string key, out string val)
    {
        val = null;
        if (!col.TryGetValue(key, out var c)) return false;
        val = (reader.GetValue(c)?.ToString() ?? "").Trim();
        return val.Length > 0;
    }

    private static bool TryGetInt(IExcelDataReader reader, Dictionary<string,int> col, string key, out int val)
    {
        val = 0;
        if (!col.TryGetValue(key, out var c)) return false;
        var obj = reader.GetValue(c);
        if (obj == null) return false;
        if (obj is double d) { val = (int)Math.Round(d); return true; }
        if (obj is float f) { val = (int)Math.Round(f); return true; }
        if (obj is int i)   { val = i; return true; }
        return int.TryParse(obj.ToString(), out val);
    }

    private static bool HasAnyKeys(Dictionary<string,int> col, params string[] keys)
    {
        foreach (var k in keys) if (col.ContainsKey(k)) return true;
        return false;
    }

    // ================= ここから追記（重複していた内側のクラス定義は削除）=================

    /// <summary>
    /// ランタイムの敵インデックス（0,1,2...）を Excel のキーにマップする。
    /// Excelが 1..N（1始まり）のときも 0..N-1（0始まり）のときも動く。N+1人目以降はループ。
    /// </summary>
    public static int MapRuntimeIndexToExcelKey(int runtimeIdx)
    {
        var dict = LoadAll();
        if (dict == null || dict.Count == 0) return runtimeIdx;

        // キー範囲を調べる（0始まりか1始まりかを判定）
        int minKey = int.MaxValue, maxKey = int.MinValue;
        bool hasZero = false, hasOne = false;
        foreach (var k in dict.Keys)
        {
            if (k < minKey) minKey = k;
            if (k > maxKey) maxKey = k;
            if (k == 0) hasZero = true;
            if (k == 1) hasOne  = true;
        }

        if (!hasZero && hasOne)
        {
            // 1始まり（1..maxKey）
            int cycle = Mathf.Max(1, maxKey);              // 例: 20
            int m = runtimeIdx % cycle;                     // 0..cycle-1
            return (m == 0) ? cycle : m;                    // → 1..cycle に丸める
        }
        else
        {
            // 0始まり（0..maxKey）
            int cycle = Mathf.Max(1, maxKey + 1);           // 例: 20個なら 20
            int m = runtimeIdx % cycle;                     // 0..cycle-1
            if (m < 0) m += cycle;
            return m;                                       // → 0..cycle-1 に丸める
        }
    }

    /// <summary>
    /// ランタイムの敵インデックスで Excel 行を取得（ループ対応）。
    /// まず runtimeIdx をそのままキーとして試し、無ければ上のマッピングで探す。
    /// </summary>
    public static bool TryGetForRuntimeIndex(int runtimeIdx, out EnemyConfig cfg)
    {
        if (_cache == null) LoadAll();
        if (_cache.TryGetValue(runtimeIdx, out cfg)) return true; // たまたま完全一致していればそれを優先
        int key = MapRuntimeIndexToExcelKey(runtimeIdx);
        return _cache.TryGetValue(key, out cfg);
    }
private static void TryRegisterCodePages()
{
    try
    {
        // Build環境で Encoding 1252 等が必要になるため、CodePages を登録する
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }
    catch (Exception e)
    {
        Debug.LogWarning($"[EnemyConfigExcel] CodePages register skipped: {e.GetType().Name} {e.Message}");
    }
}
    // ===== ここから追加 =====

    /// <summary>
    /// 画像や Resources.Load のキーとして扱いやすい形に正規化。
    /// 小文字化 + NFKC 正規化 + 非英数字を '_' に置換 + 連続 '_' を1つに圧縮 + 前後の '_' を除去。
    /// 例: "朱雀-改 01" -> "朱雀_改_01"
    /// </summary>
    public static string SanitizeForResource(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        string norm = s.Trim().ToLowerInvariant()
                         .Normalize(System.Text.NormalizationForm.FormKC);

        var sb = new System.Text.StringBuilder(norm.Length);
        foreach (var ch in norm)
        {
            sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }
        // "__" のような連続アンダースコアを1つに
        string once = System.Text.RegularExpressions.Regex
                        .Replace(sb.ToString(), "_{2,}", "_");
        // 先頭末尾の '_' を除去
        return once.Trim('_');
    }

    /// <summary>
    /// 比較用に文字列を正規化（空白や記号を除去）。完全一致→緩和一致の順で探索。
    /// </summary>
    private static string NormalizeForCompare(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        string norm = s.Trim().ToLowerInvariant()
                       .Normalize(System.Text.NormalizationForm.FormKC);
        var sb = new System.Text.StringBuilder(norm.Length);
        foreach (var ch in norm)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch); // 記号・空白は落とす
        }
        return sb.ToString();
    }

    /// <summary>
    /// Excel からロード済みキャッシュを対象に「敵名」で検索。
    /// 1) 完全一致（区別あり）→ 2) 正規化した上での緩和一致 の順で探索します。
    /// </summary>
    public static bool TryGetByName(string enemyName, out EnemyConfig cfg)
    {
        cfg = null;
        if (string.IsNullOrEmpty(enemyName))
            return false;

        if (_cache == null) LoadAll();

        // 1) 完全一致（文字大小区別・そのまま）
        foreach (var kv in _cache)
        {
            var v = kv.Value;
            if (v != null && !string.IsNullOrEmpty(v.name) &&
                string.Equals(v.name, enemyName, StringComparison.Ordinal))
            {
                cfg = v;
                return true;
            }
        }

        // 2) 緩和一致（NFKC + 小文字 + 英数字以外除去）
        string key = NormalizeForCompare(enemyName);
        foreach (var v in _cache.Values)
        {
            if (v != null && !string.IsNullOrEmpty(v.name) &&
                NormalizeForCompare(v.name) == key)
            {
                cfg = v;
                return true;
            }
        }
        return false;
    }

}
// ← クラス終端（ここでファイル終わり）
