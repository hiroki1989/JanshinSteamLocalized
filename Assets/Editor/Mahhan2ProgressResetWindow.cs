#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class Mahhan2ProgressResetWindow : EditorWindow
{
    [MenuItem("Tools/mahhan2/Progress Reset")]
    public static void Open()
    {
        GetWindow<Mahhan2ProgressResetWindow>("Progress Reset");
    }

    // ====== Projectで実際に使われている PlayerPrefs キー（CSから確認できたもの） ======
    private const string KeyHighScore = "HighScore";
    private const string KeyLastRunScore = "LastRunScore";

    private const string KeyEnemiesDefeated = "EnemiesDefeated";

    private const string KeyRunHpBonus = "Run_HPBonus";
    private const string KeyRunMpBonus = "Run_MPBonus";
    private const string KeyRunSkillCastsBonus = "Run_SkillCastsBonus";

    private const string KeyRunOfuda = "RunOfuda";
    private const string KeyRunOfudaLastJson = "RunOfuda_LastJSON";

    private const string KeyOwnedOmamori = "OwnedOmamoriIdsV1";
    private const string KeyEquippedOmamori = "EquippedOmamoriIdV1";
    private const string KeyLastGrantedOmamori = "LastGrantedOmamoriIdV1";
// Gem（特別牌シーンに関連する可能性が高いが、これは「保留中の宝石報酬」）
private const string KeyGemPendingRoll = "Gem_PendingRoll";
private const string KeyGemPendingEnemyExcelKey = "Gem_PendingEnemyExcelKey";
private const string KeyGemPendingEnemyName = "Gem_PendingEnemyName";
private const string KeyGemPendingIsZeus = "Gem_PendingIsZeus";

// Gem（初回撃破判定フラグ）
private const string KeyGemFirstDefeatedPrefixByExcel = "Gem_FirstDefeated_";
private const string KeyGemFirstDefeatedPrefixByName  = "Gem_FirstDefeated_Name_";


    private Vector2 _scroll;

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Mahhan2 進行状況リセット（Editor専用）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "この画面のボタンは PlayerPrefs のキーを削除します。\n" +
            "ゲーム中のUIからではなく、UnityEditor上で進行状況を個別に初期化したい場合に使います。",
            MessageType.Info);

        EditorGUILayout.Space(8);
        DrawCurrentSnapshot();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("個別リセット", EditorStyles.boldLabel);

        DrawResetButton(
            "ハイスコアをリセット",
            "対象キー: HighScore / LastRunScore",
            () => DeleteKeys(KeyHighScore, KeyLastRunScore)
        );

        DrawResetButton(
            "敵を倒した履歴をリセット",
            "対象キー: EnemiesDefeated",
            () => DeleteKeys(KeyEnemiesDefeated)
        );

        DrawResetButton(
            "HP/MP/ターン上限 強化状況をリセット（UpgradeSceneの強化）",
            "対象キー: Run_HPBonus / Run_MPBonus / Run_SkillCastsBonus",
            () => DeleteKeys(KeyRunHpBonus, KeyRunMpBonus, KeyRunSkillCastsBonus)
        );

        DrawResetButton(
            "お札の取得状況をリセット（ラン中の保持）",
            "対象キー: RunOfuda / RunOfuda_LastJSON",
            () => DeleteKeys(KeyRunOfuda, KeyRunOfudaLastJson)
        );

        DrawResetButton(
            "お守りの所持状況をリセット",
            "対象キー: OwnedOmamoriIdsV1 / EquippedOmamoriIdV1 / LastGrantedOmamoriIdV1 / Omamori_{id}",
            ResetOmamori
        );

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("特別牌（所持状況）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "このProjectアップロード範囲には SpecialTileSystem の保存キーが含まれていないため、\n" +
            "ここでは『SpecialTileSystem にリセット用の静的メソッドがあればそれを呼ぶ』方式にしています。\n" +
            "もし未実装なら、SpecialTileSystem 側に Reset メソッドを追加してください。",
            MessageType.Warning);

        DrawResetButton(
            "特別牌の所持状況をリセット（SpecialTileSystemが対応している場合のみ）",
            "呼び出し候補: SpecialTileSystem.ResetAllProgress / ResetOwned / ResetAllOwned / ResetSpecialTilesProgressNow",
            () =>
            {
                if (!TryInvokeAnyStaticNoArg(
                        "SpecialTileSystem",
                        "ResetAllProgress",
                        "ResetOwned",
                        "ResetAllOwned",
                        "ResetSpecialTilesProgressNow"))
                {
                    Debug.LogWarning("[Progress Reset] SpecialTileSystem のリセットメソッドが見つかりませんでした。");
                }
            }
        );

        EditorGUILayout.Space(10);
EditorGUILayout.LabelField("（参考）宝石報酬の保留情報", EditorStyles.boldLabel);
DrawResetButton(
    "宝石報酬の保留状態をリセット（Gem_Pending*）",
    "対象キー: Gem_PendingRoll / Gem_PendingEnemyExcelKey / Gem_PendingEnemyName / Gem_PendingIsZeus",
    () => DeleteKeys(KeyGemPendingRoll, KeyGemPendingEnemyExcelKey, KeyGemPendingEnemyName, KeyGemPendingIsZeus)
);

DrawResetButton(
    "宝石の初回撃破フラグをリセット（Gem_FirstDefeated_*）",
    "対象キー: Gem_FirstDefeated_{excelKey} / Gem_FirstDefeated_Name_{enemyBaseName}",
    ResetGemFirstDefeatedFlags
);


        EditorGUILayout.Space(12);
        EditorGUILayout.EndScrollView();
    }
private static void ResetGemFirstDefeatedFlags()
{
    try
    {
        // EnemyConfigExcel の全データから、excelKey と name を拾って初回撃破キーを消す
        var all = EnemyConfigExcel.LoadAll();
        foreach (var kv in all)
        {
            int excelKey = kv.Key;
            var cfg = kv.Value;

            PlayerPrefs.DeleteKey(KeyGemFirstDefeatedPrefixByExcel + excelKey.ToString());

            string baseName = StripLoopSuffix(cfg != null ? cfg.name : "");
            if (!string.IsNullOrEmpty(baseName))
            {
                PlayerPrefs.DeleteKey(KeyGemFirstDefeatedPrefixByName + baseName);
            }
        }

        PlayerPrefs.Save();
    }
    catch (Exception e)
    {
        Debug.LogWarning("[Progress Reset] ResetGemFirstDefeatedFlags failed\n" + e);
    }
}

// UpgradeManager と同じ仕様： "アマテラス +1" のような周回サフィックスを除去
private static string StripLoopSuffix(string name)
{
    if (string.IsNullOrEmpty(name)) return name;

    int p = name.LastIndexOf(" +", StringComparison.Ordinal);
    if (p < 0) return name;

    string tail = name.Substring(p + 2);
    for (int i = 0; i < tail.Length; i++)
    {
        if (tail[i] < '0' || tail[i] > '9') return name;
    }

    return name.Substring(0, p).TrimEnd();
}

    private void DrawCurrentSnapshot()
    {
        EditorGUILayout.LabelField("現在値（目安）", EditorStyles.boldLabel);

        int hi = PlayerPrefs.GetInt(KeyHighScore, 0);
        int last = PlayerPrefs.GetInt(KeyLastRunScore, 0);

        int hpB = PlayerPrefs.GetInt(KeyRunHpBonus, 0);
        int mpB = PlayerPrefs.GetInt(KeyRunMpBonus, 0);
        int scB = PlayerPrefs.GetInt(KeyRunSkillCastsBonus, 0);

        bool hasEnemiesDefeated = PlayerPrefs.HasKey(KeyEnemiesDefeated);
        bool hasRunOfuda = PlayerPrefs.HasKey(KeyRunOfuda) || PlayerPrefs.HasKey(KeyRunOfudaLastJson);

        int ownedOmamoriCount = LoadOwnedOmamoriIds().Length;

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField($"HighScore: {hi}");
            EditorGUILayout.LabelField($"LastRunScore: {last}");
            EditorGUILayout.LabelField($"EnemiesDefeated key exists: {hasEnemiesDefeated}");
            EditorGUILayout.LabelField($"Run bonuses  HP:{hpB}  MP:{mpB}  Turns:{scB}");
            EditorGUILayout.LabelField($"Run Ofuda key exists: {hasRunOfuda}");
            EditorGUILayout.LabelField($"Owned Omamori count (from OwnedOmamoriIdsV1): {ownedOmamoriCount}");
        }
    }

    private void DrawResetButton(string title, string detail, Action action)
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(detail, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("実行"))
            {
                bool ok = EditorUtility.DisplayDialog(
                    "確認",
                    $"{title}\n\n{detail}\n\n本当に実行しますか？",
                    "実行する",
                    "やめる");

                if (!ok) return;

                try
                {
                    action?.Invoke();
                    Debug.Log($"[Progress Reset] Done: {title}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Progress Reset] Failed: {title}\n{e}");
                }
            }
        }
    }

    private static void DeleteKeys(params string[] keys)
    {
        foreach (var k in keys.Where(s => !string.IsNullOrEmpty(s)))
        {
            PlayerPrefs.DeleteKey(k);
        }
        PlayerPrefs.Save();
    }

    private static int[] LoadOwnedOmamoriIds()
    {
        try
        {
            string raw = PlayerPrefs.GetString(KeyOwnedOmamori, "");
            if (string.IsNullOrEmpty(raw)) return Array.Empty<int>();

            var list = new List<int>();
            foreach (var s in raw.Split(','))
            {
                if (int.TryParse(s, out var id)) list.Add(id);
            }
            return list.Distinct().ToArray();
        }
        catch
        {
            return Array.Empty<int>();
        }
    }

    private static void ResetOmamori()
    {
        // OwnedOmamoriIdsV1 に入っている id を使って Omamori_{id} を消す
        var ids = LoadOwnedOmamoriIds();
        foreach (var id in ids)
        {
            if (id != 0)
            {
                PlayerPrefs.DeleteKey($"Omamori_{id}");
            }
        }

        PlayerPrefs.DeleteKey(KeyOwnedOmamori);
        PlayerPrefs.DeleteKey(KeyEquippedOmamori);
        PlayerPrefs.DeleteKey(KeyLastGrantedOmamori);
        PlayerPrefs.Save();
    }

    private static bool TryInvokeAnyStaticNoArg(string typeName, params string[] methodNames)
    {
        try
        {
            Type t = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(typeName);
                if (t != null) break;
            }
            if (t == null) return false;

            const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            foreach (var m in methodNames)
            {
                var mi = t.GetMethod(m, BF);
                if (mi == null) continue;
                if (mi.GetParameters().Length != 0) continue;

                mi.Invoke(null, null);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
#endif
