#if UNITY_EDITOR
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CreateMenuSkillCatalog
{
    // カタログに入れるスキル名（GameManager.ActiveSkill の列挙名と一致）
    static readonly string[] CatalogSkillNames = new[]
    {
        "RandomMan",
        "RandomYaochu",
        "RandomHonor",
        "RandomChunchan",
        "AddDoraIndicator",
        "DuplicateAndDiscardOther",
    };

    const string DefaultSavePath = "Assets/SkillSet_SET_MENU_CATALOG.asset";

    [MenuItem("Tools/Mahjan/Create Menu Skill Catalog")]
    public static void CreateOrUpdate()
    {
        // 既存があれば更新、なければ新規
        var asset = AssetDatabase.LoadAssetAtPath<SkillSetAsset>(DefaultSavePath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<SkillSetAsset>();
            AssetDatabase.CreateAsset(asset, DefaultSavePath);
            Debug.Log("[MenuCatalog] 新規作成: " + DefaultSavePath);
        }
        else
        {
            Debug.Log("[MenuCatalog] 既存を更新: " + DefaultSavePath);
        }

        // 1) activeSkills を CatalogSkillNames で上書き
        if (asset.activeSkills == null) asset.activeSkills = new List<SkillSetAsset.SkillEntry>();
        asset.activeSkills.Clear();
        foreach (var n in CatalogSkillNames.Distinct())
        {
            asset.activeSkills.Add(new SkillSetAsset.SkillEntry
            {
                activeSkillName = n,
                mpCost = 2 // 仮。必要ならインスペクタで調整
            });
        }

        // 2) traitMap をどこかの SkillSetAsset から拝借（最初に見つかった非空のもの）
        //    ※撃/瞬/癒の該当役表示のため。見つからなければ空のままでOK（後で手動で入れ替え可）
        TryCopyTraitMapFromAnyExistingAsset(asset);

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(asset);
        Debug.Log("[MenuCatalog] 完了。SkillSet_SET_MENU_CATALOG.asset を利用してください。");
    }

    static void TryCopyTraitMapFromAnyExistingAsset(SkillSetAsset target)
    {
        if (target.traitMap != null && target.traitMap.Count > 0) return; // すでに入っていれば触らない

        var guids = AssetDatabase.FindAssets("t:SkillSetAsset");
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (path == DefaultSavePath) continue; // 自分は除外
            var src = AssetDatabase.LoadAssetAtPath<SkillSetAsset>(path);
            if (src != null && src.traitMap != null && src.traitMap.Count > 0)
            {
                target.traitMap = new List<SkillSetAsset.YakuTraitEntry>(src.traitMap);
                Debug.Log($"[MenuCatalog] traitMap を流用: {path} から {DefaultSavePath} へコピー");
                return;
            }
        }
        Debug.Log("[MenuCatalog] traitMap のコピー元が見つかりませんでした（空のまま作成）。");
    }
}
#endif
