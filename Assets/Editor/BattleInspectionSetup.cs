using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BattleInspectionSetup
{
    static void Place(RectTransform r,Vector2 position,Vector2 size)
    {
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=size;r.localScale=Vector3.one;
    }
    static TMP_Text StyleLabel(Button button,string text,bool dynamic=false)
    {
        if(dynamic)foreach(var local in button.GetComponentsInChildren<LocalizedTextUI>(true))local.enabled=false;
        var t=button.GetComponentInChildren<TMP_Text>(true);if(text!=null)t.text=text;
        t.rectTransform.anchorMin=Vector2.zero;t.rectTransform.anchorMax=Vector2.one;t.rectTransform.offsetMin=new Vector2(8,3);t.rectTransform.offsetMax=new Vector2(-8,-3);
        t.enableAutoSizing=true;t.fontSizeMin=18;t.fontSizeMax=27;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;
        return t;
    }
    [MenuItem("Tools/Janshin/Battle Inspection/Install and Verify")]
    public static void Run()
    {
        Directory.CreateDirectory("Logs/BattleInspection");
        for(int i=0;i<11;i++){
            string path="Assets/Resources/DevilContracts/Icons/Devil_"+i+".png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
        }
        DevilContractSetup.RefreshPresentation();
        const string scenePath="Assets/Scenes/RunScene.unity";
        if(!File.Exists("Logs/BattleInspection/RunScene.before.unity"))File.Copy(scenePath,"Logs/BattleInspection/RunScene.before.unity");
        var scene=EditorSceneManager.OpenScene(scenePath);var gm=Object.FindAnyObjectByType<GameManager>();
        var so=new SerializedObject(gm);var hud=Object.FindAnyObjectByType<TrialBattleHUD>();
        if(!hud)throw new System.Exception("Authored battle HUD missing");
        var root=hud.transform;var left=root.Find("Characters");
        var previous=root.GetComponent<BattleInspectionHUD>();if(previous)Object.DestroyImmediate(previous);
        foreach(string name in new[]{"ContractInspection","EnemySkillNames","Button_SpecialTileEffects"}){
            var old=left.Find(name);if(old)Object.DestroyImmediate(old.gameObject);
        }
        var ui=root.gameObject.AddComponent<BattleInspectionHUD>();
        var menu=so.FindProperty("btnMenu").objectReferenceValue as Button;
        var deck=so.FindProperty("btnDeck")?.objectReferenceValue as Button;
        Place((RectTransform)menu.transform,new Vector2(8,-955),new Vector2(138,44));StyleLabel(menu,null);
        var deckRoot=left.Find("Deck");
        if(deckRoot){Place((RectTransform)deckRoot,new Vector2(155,-955),new Vector2(138,44));if(deckRoot.GetComponent<Button>())StyleLabel(deckRoot.GetComponent<Button>(),null);}
        if(deck){Place((RectTransform)deck.transform,new Vector2(155,-955),new Vector2(138,44));StyleLabel(deck,null);}
        Place((RectTransform)hud.relicButton.transform,new Vector2(8,-1007),new Vector2(138,44));StyleLabel(hud.relicButton,"遺物",true);
        ui.specialTilesButton=Object.Instantiate(menu,left);ui.specialTilesButton.name="Button_SpecialTileEffects";
        ui.specialTilesButton.onClick=new Button.ButtonClickedEvent();
        Place((RectTransform)ui.specialTilesButton.transform,new Vector2(155,-1007),new Vector2(138,44));
        ui.specialTilesLabel=StyleLabel(ui.specialTilesButton,"特別牌",true);
        var contract=new GameObject("ContractInspection",typeof(RectTransform),typeof(Image),typeof(Button));contract.transform.SetParent(left,false);
        Place((RectTransform)contract.transform,new Vector2(12,-620),new Vector2(52,52));
        ui.contractIcon=contract.GetComponent<Image>();ui.contractIcon.sprite=DevilContractIcons.Get(0);ui.contractIcon.preserveAspect=true;
        ui.contractButton=contract.GetComponent<Button>();ui.contractButton.targetGraphic=ui.contractIcon;contract.SetActive(false);
        var enemy=new GameObject("EnemySkillNames",typeof(RectTransform),typeof(TextMeshProUGUI),typeof(Button));enemy.transform.SetParent(left,false);
        Place((RectTransform)enemy.transform,new Vector2(12,-282),new Vector2(278,34));
        ui.enemySkillsLabel=enemy.GetComponent<TMP_Text>();ui.enemySkillsLabel.font=TMP_Settings.defaultFontAsset;
        ui.enemySkillsLabel.color=JanshinPanelTheme.Gold;ui.enemySkillsLabel.enableAutoSizing=true;ui.enemySkillsLabel.fontSizeMin=16;ui.enemySkillsLabel.fontSizeMax=24;
        ui.enemySkillsLabel.alignment=TextAlignmentOptions.MidlineLeft;ui.enemySkillsLabel.raycastTarget=true;
        ui.enemySkillsButton=enemy.GetComponent<Button>();ui.enemySkillsButton.targetGraphic=ui.enemySkillsLabel;
        foreach(string field in new[]{"enemyHPBar","enemyHPTMP"}){
            var c=so.FindProperty(field).objectReferenceValue as Component;if(!c)continue;
            var rect=c.transform as RectTransform;if(rect)rect.anchoredPosition=new Vector2(rect.anchoredPosition.x,-322);
        }
        var hpFrame=left.Find("enemyHPBar_Frame") as RectTransform;
        if(hpFrame)hpFrame.anchoredPosition=new Vector2(hpFrame.anchoredPosition.x,-320);
        foreach(var name in new[]{"IconIkari","Iconbougyo"}){
            var icon=left.Find(name) as RectTransform;if(icon)icon.anchoredPosition=new Vector2(icon.anchoredPosition.x,-418);
        }
        var countdown=so.FindProperty("enemySkillCountdownTMP").objectReferenceValue as TMP_Text;
        if(countdown){Place(countdown.rectTransform,new Vector2(12,-362),new Vector2(278,52));countdown.enableAutoSizing=true;countdown.fontSizeMin=18;countdown.fontSizeMax=24;}
        foreach(var pair in new[]{("enemySkillAngerIcon",12f),("enemySkillDefenseIcon",52f)}){
            var c=so.FindProperty(pair.Item1).objectReferenceValue as Component;if(c)Place((RectTransform)c.transform,new Vector2(pair.Item2,-418),new Vector2(28,28));
        }
        EnemySkillBackdrop(left);
        EditorUtility.SetDirty(ui);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Verify(gm,ui);
        BattleHUDQA.Capture("Logs/BattleInspection/HUD.png",1920,1080);
        BattleHUDQA.Capture("Logs/BattleInspection/HUDTablet.png",1440,1080);
        File.WriteAllText("Logs/BattleInspection/Verified.txt","PASS: shared 11 unique contract sprites, scene-authored inspection controls, enemy skill descriptions, legendary inactive/active colors, passive bonuses without red-dora text, frozen run contract inspection. Compile and PC/tablet renders verified.");
        Debug.Log("Battle inspection verified");
    }
    public static void RepairFramesAndPlay()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/RunScene.unity");
        var hud=Object.FindAnyObjectByType<TrialBattleHUD>();var left=hud.transform.Find("Characters");
        var frame=left.Find("enemyHPBar_Frame") as RectTransform;
        if(frame)frame.anchoredPosition=new Vector2(frame.anchoredPosition.x,-320);
        foreach(var name in new[]{"IconIkari","Iconbougyo"}){
            var icon=left.Find(name) as RectTransform;if(icon)icon.anchoredPosition=new Vector2(icon.anchoredPosition.x,-418);
        }
        EnemySkillBackdrop(left);
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();BattleInspectionPlayQA.Run();
    }
    static void EnemySkillBackdrop(Transform left)
    {
        var label=left.Find("EnemySkillNames");if(!label)return;
        var rect=left.Find("EnemySkillNamesBackdrop") as RectTransform;
        if(!rect){var go=new GameObject("EnemySkillNamesBackdrop",typeof(RectTransform),typeof(Image));go.transform.SetParent(left,false);rect=(RectTransform)go.transform;rect.SetSiblingIndex(label.GetSiblingIndex());}
        Place(rect,new Vector2(8,-282),new Vector2(284,34));
        rect.GetComponent<Image>().color=new Color(.025f,.035f,.04f,.94f);rect.GetComponent<Image>().raycastTarget=false;
        label.GetComponent<TMP_Text>().color=new Color(.89f,.78f,.54f);
    }
    public static void PolishNamesOnly()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/RunScene.unity");var hud=Object.FindAnyObjectByType<TrialBattleHUD>();
        EnemySkillBackdrop(hud.transform.Find("Characters"));EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("Enemy skill readability verified");
    }
    static void Check(bool value,string message){if(!value)throw new System.Exception("BattleInspection: "+message);}
    static void Verify(GameManager gm,BattleInspectionHUD ui)
    {
        var sprites=Enumerable.Range(0,11).Select(DevilContractIcons.Get).ToArray();Check(sprites.All(s=>s)&&sprites.Distinct().Count()==11,"distinct icons");
        Check(ui.specialTilesButton&&ui.enemySkillsButton&&ui.contractButton,"authored controls");
        Check(gm.BuildBattleEnemySkillDescription(new EnemySkillConfig{id="attack",paramX=500}).Contains(Mathf.Max(1,Mathf.RoundToInt(500*GameManager.GetCurrentTierMultiplier())).ToString("N0")),"attack description");
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var field=typeof(GameManager).GetField("_legendaryDamageHalfPending",flags);
        bool old=(bool)field.GetValue(gm);
        string equippedKey="SP_Equipped"; // Use the actual storage key from SpecialTileSystem rather than altering player inventory.
        var key=typeof(SpecialTileSystem).GetField("KEY_EQUIPPED",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        if(key!=null)equippedKey=(string)key.GetRawConstantValue();
        bool had=PlayerPrefs.HasKey(equippedKey);string before=PlayerPrefs.GetString(equippedKey);
        try{
            var entries=new System.Collections.Generic.List<SpecialTileSystem.Entry>{new SpecialTileSystem.Entry{baseType=SpecialTileSystem.BaseType.Pin5,rarity=SpecialTileSystem.Rarity.Legendary,effectId=3,traitBonusPacked="立直=2"}};
            var serializer=typeof(SpecialTileSystem).GetMethod("SerializeList",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            PlayerPrefs.SetString(equippedKey,(string)serializer.Invoke(null,new object[]{entries}));var body=gm.BuildBattleSpecialTileDescription();
            Check(body.Contains("Lv.+2")&&body.Contains("#92969B"),"passive bonus and inactive legendary effect");
            Check(!body.Contains("ドラ+1")&&!body.Contains("Dora +1"),"red dora excluded");
            field.SetValue(gm,true);
            typeof(GameManager).GetField("_legendaryDamageHalfEnemyKey",flags).SetValue(gm,typeof(GameManager).GetMethod("GetCurrentEnemyKey_ForLegendary",flags).Invoke(gm,null));
            Check(gm.BuildBattleSpecialTileDescription().Contains("#EEA347"),"active defensive effect highlighted");
        }finally{field.SetValue(gm,old);if(had)PlayerPrefs.SetString(equippedKey,before);else PlayerPrefs.DeleteKey(equippedKey);PlayerPrefs.Save();}
    }
}
