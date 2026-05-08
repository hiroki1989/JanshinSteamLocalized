// Assets/Editor/CreateHpBarPrefabs.cs
// Generates PlayerHPBar.prefab / EnemyHPBar.prefab and a combined HPHUD.prefab under Assets/UI/HPBars.
// Minimal, compile-clean, and doesn't touch your existing gameplay code.
//
// Usage:
//   1) Place this file under Assets/Editor/
//   2) In Unity: Tools > HP Bars > Create HP Bar Prefabs
//   3) (Optional) Tools > HP Bars > Create HP HUD (Group) to get a root with both bars.
//   4) Drag the prefabs under your top Canvas. Assign them to GameManager if you want runtime control.
//
// Notes:
// - Bars are Image (Filled, Horizontal, red).
// - Anchored to the top of the screen: Player (left), Enemy (right).
// - Prefabs do not create a Canvas; put them under an existing Canvas in your scene.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public static class CreateHpBarPrefabs
{
    private const string kFolder = "Assets/UI/HPBars";

    [MenuItem("Tools/HP Bars/Create HP Bar Prefabs")]
    public static void CreateBars()
    {
        EnsureFolder(kFolder);

        var player = MakeHpBarGO("PlayerHPBar",
            new Vector2(0.02f, 0.97f), new Vector2(0.48f, 0.99f));

        var enemy  = MakeHpBarGO("EnemyHPBar",
            new Vector2(0.52f, 0.97f), new Vector2(0.98f, 0.99f));

        SavePrefab(player, Path.Combine(kFolder, "PlayerHPBar.prefab"));
        SavePrefab(enemy,  Path.Combine(kFolder, "EnemyHPBar.prefab"));

        Debug.Log("Created: PlayerHPBar.prefab / EnemyHPBar.prefab in " + kFolder);
    }

    [MenuItem("Tools/HP Bars/Create HP HUD (Group)")]
    public static void CreateHudGroup()
    {
        EnsureFolder(kFolder);

        var root = new GameObject("HPHUD", typeof(RectTransform));
        var rtRoot = root.GetComponent<RectTransform>();
        rtRoot.anchorMin = new Vector2(0f, 1f);
        rtRoot.anchorMax = new Vector2(1f, 1f);
        rtRoot.pivot     = new Vector2(0.5f, 1f);
        rtRoot.anchoredPosition = Vector2.zero;
        rtRoot.sizeDelta = Vector2.zero;

        var p = MakeHpBarGO("PlayerHPBar",
            new Vector2(0.02f, 0.97f), new Vector2(0.48f, 0.99f));
        p.transform.SetParent(root.transform, false);

        var e = MakeHpBarGO("EnemyHPBar",
            new Vector2(0.52f, 0.97f), new Vector2(0.98f, 0.99f));
        e.transform.SetParent(root.transform, false);

        SavePrefab(root, Path.Combine(kFolder, "HPHUD.prefab"));
        Debug.Log("Created: HPHUD.prefab in " + kFolder);
    }

    private static GameObject MakeHpBarGO(string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        img.fillAmount = 1f;
        img.color = new Color(0.85f, 0.1f, 0.1f, 1f); // red

        return go;
    }

    private static void SavePrefab(GameObject go, string assetPath)
    {
        // Ensure parent folders exist
        var dir = Path.GetDirectoryName(assetPath).Replace("\\", "/");
        EnsureFolder(dir);

        // Save and destroy the scene instance
        PrefabUtility.SaveAsPrefabAsset(go, assetPath);
        Object.DestroyImmediate(go);
        AssetDatabase.Refresh();
    }

    private static void EnsureFolder(string folderPath)
    {
        folderPath = folderPath.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        var parts = folderPath.Split('/');
        var path = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = path + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(path, parts[i]);
            path = next;
        }
    }
}
#endif
