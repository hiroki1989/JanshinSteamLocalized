#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BuildEnemyConfigDatabaseSO
{
    private const string OutputFolder = "Assets/Resources";
    private const string OutputAssetPath = "Assets/Resources/EnemyConfigDatabaseSO.asset";

    [MenuItem("Tools/MasterData/Build EnemyConfigDatabaseSO from Excel")]
    public static void Build()
    {
        Dictionary<int, EnemyConfig> dict = EnemyConfigExcel.LoadAll();

        if (dict == null || dict.Count == 0)
        {
            Debug.LogError("[BuildEnemyConfigDatabaseSO] EnemyConfigExcel.LoadAll() returned empty.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        var asset = ScriptableObject.CreateInstance<EnemyConfigDatabaseSO>();
        asset.entries = new List<EnemyConfigEntrySO>();

        foreach (var kv in dict)
        {
            var e = new EnemyConfigEntrySO();
            e.excelKey = kv.Key;
            e.config = kv.Value;
            asset.entries.Add(e);
        }

        AssetDatabase.DeleteAsset(OutputAssetPath);
        AssetDatabase.CreateAsset(asset, OutputAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[BuildEnemyConfigDatabaseSO] Built: " + OutputAssetPath);
    }
}
#endif