#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class BuildOfudaCatalogSO
{
    private const string OutputFolder = "Assets/Resources";
    private const string OutputAssetPath = "Assets/Resources/OfudaCatalogSO.asset";

    [MenuItem("Tools/MasterData/Build OfudaCatalogSO from CSV")]
    public static void Build()
    {
        var cat = OfudaExcelLoader.Load();

        if (cat == null)
        {
            Debug.LogError("[BuildOfudaCatalogSO] OfudaExcelLoader.Load() returned null.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        var asset = ScriptableObject.CreateInstance<OfudaCatalogSO>();

        asset.conditions = new System.Collections.Generic.List<OfudaCondition>();
        if (cat.conditions != null)
        {
            for (int i = 0; i < cat.conditions.Count; i++)
            {
                asset.conditions.Add(cat.conditions[i]);
            }
        }

        asset.effects = new System.Collections.Generic.List<OfudaEffect>();
        if (cat.effects != null)
        {
            for (int i = 0; i < cat.effects.Count; i++)
            {
                asset.effects.Add(cat.effects[i]);
            }
        }

        asset.priceMap = new System.Collections.Generic.List<OfudaPriceBandSO>();
        if (cat.priceMap != null)
        {
            for (int i = 0; i < cat.priceMap.Count; i++)
            {
                var b = cat.priceMap[i];
                var row = new OfudaPriceBandSO();
                row.maxProbSum = b.maxProbSum;
                row.rarity = b.rarity;
                row.priceK = b.priceK;
                row.priceFixed = b.priceFixed;
                asset.priceMap.Add(row);
            }
        }

        AssetDatabase.DeleteAsset(OutputAssetPath);
        AssetDatabase.CreateAsset(asset, OutputAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[BuildOfudaCatalogSO] Built: " + OutputAssetPath);
    }
}
#endif