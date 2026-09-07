// Preview-scene checks: never starts a match, saves a scene, or changes player inventory.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static class ItemPresentationQA
{
    const string Folder = "Temp/ItemPresentationQA";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly List<string> Results = new();
    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
    static void Set(object o, string n, object value) => o.GetType().GetField(n, Flags).SetValue(o, value);
    static void Check(bool ok, string name) { Results.Add((ok ? "PASS " : "FAIL ") + name); if (!ok) Debug.LogError("[ItemPresentationQA] " + name); }
    static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).First();
    static PlayerData.OmamoriInstance Sample(int unique = 0) => new PlayerData.OmamoriInstance {
        isUnique = unique > 0, uniqueKind = (PlayerData.UniqueOmamoriEffectKind)unique,
        rarity = PlayerData.OmamoriRarity.Legendary, level = 25
    };
    [InitializeOnLoadMethod]
    static void Watch() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (File.Exists(Folder + "/request") && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
        { File.Delete(Folder + "/request"); Run(); }
    }
    [MenuItem("Tools/Janshin/Validate Item Presentation")]
    public static void Run()
    {
        Directory.CreateDirectory(Folder); Results.Clear();
        try
        {
            var art = Directory.GetFiles("Assets/Resources/ItemArtwork", "*.png", SearchOption.AllDirectories);
            foreach (var file in art)
            {
                string path = file.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.spriteImportMode != SpriteImportMode.Single || importer.maxTextureSize != 512)
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Check(AssetDatabase.LoadAssetAtPath<Sprite>(path), "Sprite " + Path.GetFileName(path));
                Check(importer.spriteImportMode == SpriteImportMode.Single && importer.maxTextureSize == 512, "Import " + Path.GetFileName(path));
            }
            Check(art.Length == 35, "All 35 body/aura images available");
            foreach (string name in new[] { "RunScene", "EquipScene", "StageClearScene", "UpgradeScene" }) CheckScene(name);
        }
        catch (Exception e) { Results.Add("FAIL " + e); Debug.LogException(e); }
        finally { File.WriteAllLines(Folder + "/results.txt", Results); }
    }
    static void ShowAncestors(Transform t) { for (; t; t = t.parent) t.gameObject.SetActive(true); }
    static void CheckScene(string name)
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + name + ".unity");
        try
        {
            var labels = new List<TMP_Text>();
            if (name == "RunScene")
            {
                var gm = Find<GameManager>(scene);
                Call(gm, "UpdateOmamoriIconUI_Manual");
                Call(gm, "UpdateOfudaIconsUI_Manual", (object)new[] { "normal", "rare", "legendary" });
                var icon = (Image)Get(gm, "_omamoriIconImage"); ItemArtwork.Omamori(icon, Sample(12));
                var text = (TMP_Text)Get(gm, "_omamoriInfoTMP");
                text.text = "【ハデスの神器】 Lv.25\n書家のスキルで選択牌を最も多い字牌に変換\nHP +3000／MP +1500"; labels.Add(text);
                var ofuda = (TextMeshProUGUI[])Get(gm, "_ofudaInfoTMPs");
                for (int i = 0; i < ofuda.Length; i++) { ofuda[i].text = new[] { "ノーマル\n萬子の護符", "レア\n一気通貫の護符", "レジェンダリー\n大三元の護符" }[i]; labels.Add(ofuda[i]); }
                Call(gm, "WireActiveSkillDescription");
                Check(((TMP_Text)Get(gm, "_skillActionNameTMP")).GetComponent<SkillDescriptionTap>() != null, "Active skill label receives taps");
                Render(scene, name, labels);
                CheckTargets(gm);
                var popup = SkillDescriptionPopup.Show(gm.transform, "色寄せ", "選択した手牌を、最も多い色の同じ数字の牌に変換します。\n\nMPを消費します。使用回数と必要MPを確認してから発動してください。", () => {});
                Check(Time.timeScale == 0, "Skill description pauses play");
                Render(scene, "SkillDescription", popup.GetComponentsInChildren<TMP_Text>().Where(t => t.name != "Description").ToList());
                Object.DestroyImmediate(popup.gameObject);
                Check(Time.timeScale == 1, "Skill description restores play speed");
            }
            else if (name == "EquipScene")
            {
                var manager = Find<EquipManager>(scene); Call(manager, "RefreshEquippedOmamoriIcon", 0);
                var icon = (Image)Get(manager, "equippedOmamoriIconImage"); ItemArtwork.Omamori(icon, Sample(10));
                var label = (TMP_Text)Get(manager, "equippedEffectsTMP"); label.text = "【ゼウスの神器】 Lv.25\n敵へのダメージ30％上昇\nHP +3000／MP +1500"; labels.Add(label);
                OwnedRows(scene, (Transform)Get(manager, "ownedListParent"), (GameObject)Get(manager, "omamoriItemPrefab"), labels);
                Render(scene, name, labels);
            }
            else if (name == "StageClearScene")
            {
                var manager = Find<StageClearManager>(scene);
                Call(manager, "SetOmamoriIconVisual", 0, "legendary", "レジェンダリー");
                var icon = (Image)Get(manager, "omamoriIconImage"); ItemArtwork.Omamori(icon, Sample()); ShowAncestors(icon.transform);
                var label = (TMP_Text)Get(manager, "omamoriDescTMP"); label.text = "レジェンダリー Lv.25\nHP +3000\nMP +1500\n和了ダメージ +15％"; labels.Add(label);
                Render(scene, name, labels);
                PreviewUnique(scene, manager, name + "-Unique");
                ((GameObject)Get(manager, "uniqueOmamoriResultPanelRoot")).SetActive(false);
                labels.Clear();
                OwnedRows(scene, (Transform)Get(manager, "ownedListParent"), (GameObject)Get(manager, "ownedItemPrefab"), labels);
                Render(scene, name + "-Owned", labels);
            }
            else
            {
                var store = Find<UpgradeOfudaStore>(scene);
                var slots = (Array)Get(store, "offerSlots");
                int i = 0;
                foreach (var slot in slots)
                {
                    var icon = (Image)Get(slot, "iconImage"); var label = (TMP_Text)Get(slot, "nameTMP"); var price = (TMP_Text)Get(slot, "priceTMP");
                    ItemArtwork.Ofuda(icon, new[] { "normal", "epic", "legendary" }[i]);
                    label.text = new[] { "ノーマル\n萬子の護符", "エピック\n一気通貫の護符", "レジェンダリー\n大三元の護符" }[i++]; price.text = "1,500 Gold";
                    ItemArtwork.ShopSlot(icon, label, price); ShowAncestors(icon.transform); labels.Add(label); labels.Add(price);
                }
                Render(scene, name, labels);
                PreviewUnique(scene, Find<UpgradeSceneMenu>(scene), name + "-Unique");
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    static void OwnedRows(Scene scene, Transform parent, GameObject prefab, List<TMP_Text> labels)
    {
        Check(parent && prefab, scene.name + " owned list references");
        if (!parent || !prefab) return;
        for (int i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject);
        ShowAncestors(parent);
        foreach (int unique in new[] { 0, 12, 10 })
        {
            var row = Object.Instantiate(prefab, parent); var label = row.GetComponentInChildren<TMP_Text>(true);
            label.text = (unique == 0 ? "【レジェンダリー】" : unique == 12 ? "【ハデスの神器】" : "【ゼウスの神器】") + " Lv.25\nHP +3000\nMP +1500\n一気通貫のダメージ +15％\nスキルの消費MP −20％";
            var icon = row.transform.Find("Icon")?.GetComponent<Image>() ?? ItemArtwork.EnsureIcon(row.transform);
            ItemArtwork.Omamori(icon, Sample(unique)); ItemArtwork.OwnedRow(row, icon, label); labels.Add(label);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)parent);
    }
    static void PreviewUnique(Scene scene, object manager, string name)
    {
        var panel = (GameObject)Get(manager, "uniqueOmamoriResultPanelRoot"); var desc = (TMP_Text)Get(manager, "uniqueOmamoriDescTMP"); var title = (TMP_Text)Get(manager, "uniqueOmamoriTitleTMP");
        if (!panel || !desc) return;
        ItemArtwork.UniquePanel(panel, desc, title, 1);
        ItemArtwork.Omamori(desc.transform.parent.Find("UniqueItemIcon").GetComponent<Image>(), Sample(12));
        desc.text = "【ハデスの神器】 Lv.25\n\n書家のスキルを発動すると、選択した牌を手牌で最も多い字牌に変換する。\n\nHP +3000\nMP +1500";
        ShowAncestors(panel.transform); Render(scene, name, new List<TMP_Text> { desc });
    }
    static void CheckTargets(GameManager gm)
    {
        var enemy = (List<string>)Get(gm, "enemyDiscards"); enemy.Clear(); enemy.AddRange(new[] { "m1", "m1" });
        var player = (List<string>)Get(gm, "discards"); player.Clear(); player.AddRange(new[] { "m1", "m1" });
        foreach (string field in new[] { "enemyDiscardArea", "discardArea" })
        {
            var area = (Transform)Get(gm, field); string prefix = field == "enemyDiscardArea" ? "EnemyDiscard_" : "PlayerDiscard_";
            for (int i = 0; i < 2; i++) { var go = new GameObject(prefix + i + "_m1", typeof(RectTransform)); go.transform.SetParent(area, false); }
        }
        Set(gm, "_lastPlayerRonEnemyDiscardIndex", 0); Set(gm, "_winStrikeEnemyDiscardIndex", 1); Set(gm, "_winStrikePlayerDiscardIndex", 0);
        var ron = (RectTransform)Call(gm, "ResolveWinStrikeTarget", true, false, "m1");
        var tsumo = (RectTransform)Call(gm, "ResolveWinStrikeTarget", false, true, "m1");
        var enemyRon = (RectTransform)Call(gm, "ResolveWinStrikeTarget", false, false, "m1");
        Check(ron && ron.name == "EnemyDiscard_0_m1", "Player Ron selects exact duplicate tile");
        Check(tsumo && tsumo.name == "EnemyDiscard_1_m1", "Enemy Tsumo selects exact duplicate tile");
        Check(enemyRon && enemyRon.name == "PlayerDiscard_0_m1", "Enemy Ron selects exact duplicate tile");
    }
    static void Render(Scene scene, string name, List<TMP_Text> labels)
    {
        var cameraGO = new GameObject("Item QA Camera"); SceneManager.MoveGameObjectToScene(cameraGO, scene);
        var camera = cameraGO.AddComponent<Camera>(); camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f,.07f,.08f); camera.cullingMask = 1 << 31;
        foreach (var root in scene.GetRootGameObjects()) foreach (var tr in root.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 31;
        foreach (var canvas in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)))
        { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; }
        try
        {
            foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(2532,1170) })
            {
                var rt = new RenderTexture(size.x,size.y,24); camera.targetTexture = rt;
                Canvas.ForceUpdateCanvases();
                foreach (var label in labels) { label.ForceMeshUpdate(true); Check(!label.isTextOverflowing && label.rectTransform.rect.height > 0, name + " " + size + " " + label.name + " text fits"); }
                camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
                var png = new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                png.ReadPixels(new Rect(0,0,size.x,size.y),0,0); png.Apply(); File.WriteAllBytes(Folder + "/" + name + "-" + size.x + ".png",png.EncodeToPNG());
                Object.DestroyImmediate(png); RenderTexture.active = previous; camera.targetTexture = null; Object.DestroyImmediate(rt);
            }
        }
        finally { Object.DestroyImmediate(cameraGO); }
    }
}
