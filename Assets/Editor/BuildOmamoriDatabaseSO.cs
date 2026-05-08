#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class BuildOmamoriDatabaseSO
{
    private const string OutputFolder = "Assets/Resources";
    private const string OutputAssetPath = "Assets/Resources/OmamoriDatabaseSO.asset";

    [MenuItem("Tools/MasterData/Build OmamoriDatabaseSO (BuiltInDefaults)")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        var asset = ScriptableObject.CreateInstance<OmamoriDatabaseSO>();

        asset.rarityRows = new System.Collections.Generic.List<OmamoriRarityRowSO>
        {
            new OmamoriRarityRowSO{ rarity=PlayerData.OmamoriRarity.Normal,    baseW=50f, perDefeat=-0.10f, count=1 },
            new OmamoriRarityRowSO{ rarity=PlayerData.OmamoriRarity.Common,    baseW=20f, perDefeat=-0.10f, count=2 },
            new OmamoriRarityRowSO{ rarity=PlayerData.OmamoriRarity.Rare,      baseW=10f, perDefeat= 0.00f, count=3 },
            new OmamoriRarityRowSO{ rarity=PlayerData.OmamoriRarity.Epic,      baseW= 9f, perDefeat= 0.10f, count=4 },
            new OmamoriRarityRowSO{ rarity=PlayerData.OmamoriRarity.Legendary, baseW= 1f, perDefeat= 0.10f, count=5 },
        };

        asset.effectRows = new System.Collections.Generic.List<OmamoriEffectRowSO>
        {
            new OmamoriEffectRowSO{ effect=PlayerData.OmamoriEffect.MaxHPPercentUp,         w=10 },
            new OmamoriEffectRowSO{ effect=PlayerData.OmamoriEffect.MaxMPPercentUp,         w= 8 },
            new OmamoriEffectRowSO{ effect=PlayerData.OmamoriEffect.IyuHealPercentUp,       w=10 },
            new OmamoriEffectRowSO{ effect=PlayerData.OmamoriEffect.GekiDamagePercentUp,    w=10 },
            new OmamoriEffectRowSO{ effect=PlayerData.OmamoriEffect.ShunAddPercentUp,       w=10 },
            new OmamoriEffectRowSO{ effect=PlayerData.OmamoriEffect.DamageTakenPercentDown, w= 8 },
            new OmamoriEffectRowSO{ effect=PlayerData.OmamoriEffect.SkillMpCostPercentDown, w= 4 },
            new OmamoriEffectRowSO{ effect=PlayerData.OmamoriEffect.MpRegenPercentUp,       w= 4 },
        };

        asset.specialRows = new System.Collections.Generic.List<OmamoriSpecialRowSO>
        {
            new OmamoriSpecialRowSO{ sp=PlayerData.OmamoriSpecial.None, w=100 },
        };

        asset.baseAtLv1 = 10f;
        asset.perLv = 5f;
        asset.rangeMinus = 2;
        asset.rangePlus = 2;
        asset.allowDup = false;

        AssetDatabase.DeleteAsset(OutputAssetPath);
        AssetDatabase.CreateAsset(asset, OutputAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[BuildOmamoriDatabaseSO] Built: " + OutputAssetPath);
    }
}
#endif