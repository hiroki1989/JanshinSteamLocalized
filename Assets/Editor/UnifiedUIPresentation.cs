using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class UnifiedUIPresentation
{
    static readonly string[] Scenes={"RunScene","EnemyDialogue","StageClearScene","UpgradeScene","MenuScene","SpecialTileScene","EquipScene","ShopScene"};
    public static void Install()
    {
        Directory.CreateDirectory("Logs/UnifiedUIBackup");
        Directory.CreateDirectory("Assets/Resources/UnifiedUI");
        var texture=new Texture2D(4,64,TextureFormat.RGBA32,false);
        for(int y=0;y<64;y++)for(int x=0;x<4;x++)texture.SetPixel(x,y,Color.Lerp(new Color(.025f,.032f,.04f,.98f),new Color(.09f,.105f,.115f,.98f),y/63f));
        texture.Apply();File.WriteAllBytes("Assets/Resources/UnifiedUI/InkSurface.png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.Refresh();var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Resources/UnifiedUI/InkSurface.png");
        importer.textureType=TextureImporterType.Sprite;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
        var report=new List<string>();
        foreach(var name in Scenes)
        {
            string path="Assets/Scenes/"+name+".unity";
            string backup="Logs/UnifiedUIBackup/"+name+".unity";if(!File.Exists(backup))File.Copy(path,backup);
            var scene=EditorSceneManager.OpenScene(path);
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var buttons=all.Select(t=>t.GetComponent<Button>()).Where(b=>b).ToArray();
            var events=buttons.ToDictionary(b=>b,b=>b.onClick.GetPersistentEventCount());
            var scopes=new List<Transform>();
            if(name=="RunScene"){
                foreach(var n in new[]{"ScoringPanel","PlayerScoringPanel","EnemyScoringPanel","MenuPanel"})
                    scopes.AddRange(all.Where(t=>t.name==n));
            }else if(name=="MenuScene"){
                LayoutMenu(all);
                scopes.AddRange(all.Where(t=>new[]{"Button_Play","Button_Equip","Button_SkillSet","Button_Shop","Button_SpecialTile"}.Contains(t.name)));
                var relic=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(b=>b.name=="ConsumableInventoryButton");if(relic)scopes.Add(relic.transform);
            }else scopes.AddRange(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).Where(c=>c.isRootCanvas).Select(c=>c.transform));
            int faces=0;
            foreach(var root in scopes.Distinct())
            {
                if(!root.GetComponent<JanshinThemeTextScope>())root.gameObject.AddComponent<JanshinThemeTextScope>();
                foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))JanshinPanelTheme.Text(text);
                foreach(var image in root.GetComponentsInChildren<Image>(true).ToArray())
                {
                    if(image.name=="UnifiedGoldBorder")continue;
                    string asset=image.sprite?AssetDatabase.GetAssetPath(image.sprite):"";
                    bool frame=asset.Contains("GUI_Parts/Gui_parts/Frame")||asset.Contains("Consumables/PanelFrame");
                    bool panel=image.name.StartsWith("Panel")&&!image.name.StartsWith("Panel_Background") && (!image.sprite||asset=="Resources/unity_builtin_extra");
                    bool named=new[]{"RewardPanel","MenuPanel","PlayerScoringPanel","EnemyScoringPanel","ScoringPanel","ownedPanelRoot","gemResultPanelRoot","uniqueOmamoriResultPanelRoot"}.Contains(image.name);
                    if(frame||panel||named){JanshinPanelTheme.Apply(image);faces++;}
                }
                foreach(var b in root.GetComponentsInChildren<Button>(true)){
                    // Mahjong tile faces, suit choices and their hit regions keep their art.
                    if(b.transform.Find("Art")||b.name.StartsWith("Tile")||new[]{"man","sou","pin"}.Any(s=>b.name.ToLowerInvariant().StartsWith(s)))continue;
                    JanshinPanelTheme.Button(b);
                }
            }
            foreach(var pair in events)if(!pair.Key||pair.Key.onClick.GetPersistentEventCount()!=pair.Value)throw new Exception("Button binding changed: "+name);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            report.Add(name+": surfaces="+faces+", existing button bindings preserved="+events.Count);
        }
        AssetDatabase.SaveAssets();File.WriteAllLines("Logs/UnifiedUIResult.txt",report);
    }
    static void LayoutMenu(Transform[] all)
    {
        var canvas=all.Select(t=>t.GetComponent<Canvas>()).First(c=>c&&c.isRootCanvas);
        var source=all.First(t=>t.name=="Button_Equip").GetComponent<Button>();
        var relic=all.FirstOrDefault(t=>t.name=="ConsumableInventoryButton");
        if(!relic){
            var b=UnityEngine.Object.Instantiate(source,canvas.transform,false);b.name="ConsumableInventoryButton";relic=b.transform;
            b.onClick=new Button.ButtonClickedEvent();var handler=b.gameObject.AddComponent<MenuRelicButton>();UnityEventTools.AddPersistentListener(b.onClick,handler.Open);
            foreach(var loc in b.GetComponentsInChildren<LocalizedTextUI>(true))loc.enabled=false;
            foreach(var t in b.GetComponentsInChildren<TMP_Text>(true))t.text="遺物";
        }
        var order=new[]{"Button_SkillSet","Button_Play","Button_Equip","Button_Shop","Button_SpecialTile","ConsumableInventoryButton"};
        for(int i=0;i<order.Length;i++){
            var t=i==5?relic:all.First(x=>x.name==order[i]);var r=(RectTransform)t;r.SetParent(canvas.transform,false);
            r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.localScale=Vector3.one;r.sizeDelta=new Vector2(400,96);r.anchoredPosition=new Vector2((i%3-1)*460,i<3?-320:-438);
            JanshinPanelTheme.Button(t.GetComponent<Button>());
            foreach(var text in t.GetComponentsInChildren<TMP_Text>(true)){
                var tr=text.rectTransform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=new Vector2(18,8);tr.offsetMax=new Vector2(-18,-8);
                text.enableAutoSizing=true;text.fontSizeMin=26;text.fontSizeMax=42;text.alignment=TextAlignmentOptions.Center;
            }
        }
    }
}
