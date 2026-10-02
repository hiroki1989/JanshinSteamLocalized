using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class UnifiedUIRefinement
{
    static readonly string[] Scenes={"SpecialTileScene","EquipScene","ShopScene","MenuScene","TierSelectScene","EnemyDialogue"};
    static T Ref<T>(SerializedObject so,string key) where T:UnityEngine.Object => so.FindProperty(key)?.objectReferenceValue as T;
    static void Place(RectTransform r,Transform parent,float x,float y,float w,float h){r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.localScale=Vector3.one;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
    static void Label(TMP_Text t,Transform p,float x,float y,float w,float h,float size=36){if(!t)return;Place(t.rectTransform,p,x,y,w,h);t.alignment=TextAlignmentOptions.Center;t.enableAutoSizing=true;t.fontSizeMin=24;t.fontSizeMax=size;t.margin=new Vector4(8,4,8,4);JanshinPanelTheme.Text(t);}
    public static void Run()
    {
        foreach(var name in Scenes){
            var path="Assets/Scenes/"+name+".unity";var backup="Logs/UnifiedUIBackup/"+name+".unity";if(!File.Exists(backup))File.Copy(path,backup);
            var scene=EditorSceneManager.OpenScene(path);var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var canvas=all.Select(t=>t.GetComponent<Canvas>()).First(c=>c&&c.isRootCanvas);
            if(name=="TierSelectScene"){
                if(!canvas.GetComponent<JanshinThemeTextScope>())canvas.gameObject.AddComponent<JanshinThemeTextScope>();
                foreach(var b in canvas.GetComponentsInChildren<Button>(true))JanshinPanelTheme.Button(b);
                var controller=UnityEngine.Object.FindAnyObjectByType<TierSelectController>();var so=new SerializedObject(controller);
                var root=Ref<GameObject>(so,"tierSelectPanel").GetComponent<RectTransform>();root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
                var start=Ref<Button>(so,"startButton");Place((RectTransform)start.transform,root,0,-55,520,104);
                var drop=Ref<TMP_Dropdown>(so,"tierDropdown");Place((RectTransform)drop.transform,root,0,85,520,90);JanshinPanelTheme.Apply(drop.GetComponent<Image>());
                foreach(var im in drop.GetComponentsInChildren<Image>(true))if(im.name=="Template")JanshinPanelTheme.Apply(im);
                foreach(var t in drop.GetComponentsInChildren<TMP_Text>(true)){JanshinPanelTheme.Text(t);t.enableAutoSizing=true;t.fontSizeMin=22;t.fontSizeMax=32;t.margin=new Vector4(16,10,26,10);}
                var back=Ref<Button>(so,"backToMenuButton");if(back)Place((RectTransform)back.transform,root,-700,-430,340,90);
                var settings=AssetDatabase.LoadAssetAtPath<GaidenUISettings>("Assets/Resources/SeventeenSteps/GaidenUISettings.asset");
                if(settings){settings.buttonPosition=new Vector2(0,-205);settings.buttonSize=new Vector2(520,120);settings.frameSprite=JanshinPanelTheme.Frame;settings.frameColor=new Color(.9f,.73f,.38f);EditorUtility.SetDirty(settings);}
            }
            if(name=="SpecialTileScene"||name=="EquipScene"||name=="ShopScene"){
                // These backgrounds used to be transparent frame-only siblings. Put their
                // new opaque fills behind headers, portraits and all interactive content.
                var panels=all.Where(t=>t.parent==canvas.transform&&t.name.StartsWith("Panel")&&!t.name.StartsWith("Panel_Background")&&t.GetComponent<JanshinPanelTheme>()).OrderBy(t=>t.GetSiblingIndex()).ToArray();
                foreach(var p in panels)p.SetAsFirstSibling();
                foreach(var bg in all.Where(t=>t.parent==canvas.transform&&t.name.StartsWith("Panel_Background")).OrderByDescending(t=>t.GetSiblingIndex()))bg.SetAsFirstSibling();
                var gem=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Dark UI/New Icons/White Gem 2.png");
                foreach(var im in canvas.GetComponentsInChildren<Image>(true))if(im.name.Contains("Gem")||(im.sprite&&AssetDatabase.GetAssetPath(im.sprite).Contains("Gem"))){im.sprite=gem;im.color=Color.white;im.material=null;}
                foreach(var b in canvas.GetComponentsInChildren<Button>(true)){
                    if(JanshinPanelTheme.IsTileControl(b))continue;
                    JanshinPanelTheme.Button(b);
                    foreach(var text in b.GetComponentsInChildren<TMP_Text>(true))text.margin=new Vector4(12,7,12,7);
                }
            }
            if(name=="SpecialTileScene"){
                var owner=UnityEngine.Object.FindAnyObjectByType<SpecialTileSceneController>();var so=new SerializedObject(owner);
                var icons=so.FindProperty("equippedSlotImages");
                for(int i=0;i<icons.arraySize;i++){
                    var im=icons.GetArrayElementAtIndex(i).objectReferenceValue as Image;if(!im)continue;
                    var theme=im.GetComponent<JanshinPanelTheme>();if(theme)UnityEngine.Object.DestroyImmediate(theme);
                    var border=im.transform.Find("UnifiedGoldBorder");if(border)UnityEngine.Object.DestroyImmediate(border.gameObject);
                    im.sprite=null;im.color=Color.white;im.preserveAspect=true;
                }
                ThemePrefab(Ref<GameObject>(so,"ownedItemPrefab"));
                foreach(var t in canvas.GetComponentsInChildren<TMP_Text>(true))if(t.name=="EquippedText"||t.name=="OwnedText"){t.transform.SetAsLastSibling();t.color=JanshinPanelTheme.Ivory;}
                foreach(var key in new[]{"buyButton","expandSlotButton"}){
                    var b=Ref<Button>(so,key);if(!b)continue;
                    var texts=b.GetComponentsInChildren<TMP_Text>(true);
                    foreach(var t in texts){bool cost=t.text.Contains("×")||t.text.StartsWith("x");Label(t,b.transform,cost?60:0,cost?-40:20,cost?150:330,cost?42:65,cost?30:38);}
                    foreach(var im in b.GetComponentsInChildren<Image>(true))if(im.name.Contains("Gem"))Place(im.rectTransform,b.transform,-25,-40,30,30);
                }
            }
            if(name=="EquipScene"){
                var owner=UnityEngine.Object.FindAnyObjectByType<EquipManager>();ThemePrefab(Ref<GameObject>(new SerializedObject(owner),"omamoriItemPrefab"));
            }
            if(name=="ShopScene")LayoutShop(canvas);
            if(name=="MenuScene")foreach(var theme in canvas.GetComponentsInChildren<JanshinPanelTheme>(true)){var c=theme.face.color;c.a=1;theme.face.color=c;var b=theme.GetComponent<Button>();if(b)JanshinPanelTheme.Button(b);}
            if(name=="EnemyDialogue")ImportSkillNames();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();File.WriteAllText("Logs/UnifiedUIRefined.txt","Refined tile art exclusions, layering, owned-row backgrounds, gem icons, shop cards and Tier selection.\n");
    }
    static void ThemePrefab(GameObject prefab)
    {
        if(!prefab)return;var path=AssetDatabase.GetAssetPath(prefab);if(string.IsNullOrEmpty(path))return;
        var backup="Logs/UnifiedUIBackup/"+path;Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!File.Exists(backup))File.Copy(path,backup);
        var root=PrefabUtility.LoadPrefabContents(path);
        try{
            foreach(var im in root.GetComponentsInChildren<Image>(true))if(im.transform==root.transform||im.name=="Background"||im.name=="BG")JanshinPanelTheme.Apply(im);
            if(!root.GetComponent<JanshinThemeTextScope>())root.AddComponent<JanshinThemeTextScope>();
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    static void LayoutShop(Canvas canvas)
    {
        var manager=UnityEngine.Object.FindAnyObjectByType<ShopManager>();var so=new SerializedObject(manager);
        var children=canvas.GetComponentsInChildren<TMP_Text>(true);
        foreach(var im in canvas.GetComponentsInChildren<Image>(true))if(im.transform.parent==canvas.transform&&im.name.StartsWith("Image")&&im.color.a>.9f&&Mathf.Max(im.color.r,im.color.g,im.color.b)-Mathf.Min(im.color.r,im.color.g,im.color.b)>.5f)im.enabled=false;
        for(int i=0;i<2;i++){
            bool hp=i==0;var b=Ref<Button>(so,hp?"buyHpButton":"buyMpButton");Place((RectTransform)b.transform,canvas.transform,hp?-360:360,0,560,400);JanshinPanelTheme.Button(b);
            var cost=Ref<TMP_Text>(so,hp?"hpCostTMP":"mpCostTMP");var amount=Ref<TMP_Text>(so,hp?"hpAddAmountTMP":"mpAddAmountTMP");var total=Ref<TMP_Text>(so,hp?"addedHpTMP":"addedMpTMP");
            var title=b.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t!=cost);Label(title,b.transform,0,130,500,74,48);
            Label(amount,b.transform,0,45,480,70,46);Label(cost,b.transform,30,-50,170,54,36);Label(total,b.transform,95,-137,230,50,36);
            var totalLabel=children.FirstOrDefault(t=>t.name==(hp?"HP (1)":"MP (1)"));Label(totalLabel,b.transform,-125,-137,235,50,30);
            foreach(var im in b.GetComponentsInChildren<Image>(true)){
                if(im.name=="UnifiedGoldBorder")continue;
                if(im.sprite&&AssetDatabase.GetAssetPath(im.sprite).Contains("Gem"))Place(im.rectTransform,b.transform,-55,-50,38,38);
                else if(im.name.StartsWith("Image"))im.enabled=false;
            }
        }
        var back=Ref<Button>(so,"backButton");Place((RectTransform)back.transform,canvas.transform,0,-365,380,96);
        foreach(var t in back.GetComponentsInChildren<TMP_Text>(true))Label(t,back.transform,0,0,340,70,40);
    }
    static void ImportSkillNames()
    {
        const string path="Assets/Resources/EnemySkillNames.asset";
        var asset=AssetDatabase.LoadAssetAtPath<EnemySkillNamesSO>(path);if(asset)return;
        asset=ScriptableObject.CreateInstance<EnemySkillNamesSO>();
        var so=new SerializedObject(UnityEngine.Object.FindAnyObjectByType<EnemyDialogueController>());var table=so.FindProperty("enemySkillDisplayNameTable");
        for(int i=0;i<table.arraySize;i++){
            var row=table.GetArrayElementAtIndex(i);
            asset.entries.Add(new EnemySkillNamesSO.Entry{id=EnemySkillNamesSO.Canonical(row.FindPropertyRelative("skillId").stringValue),japanese=row.FindPropertyRelative("displayNameJapanese").stringValue,english=row.FindPropertyRelative("displayNameEnglish").stringValue,chineseSimplified=row.FindPropertyRelative("displayNameChineseSimplified").stringValue});
        }
        AssetDatabase.CreateAsset(asset,path);
    }
}
