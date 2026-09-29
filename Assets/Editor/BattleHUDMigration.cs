using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Authoring utility: changes are saved into RunScene, never generated in the player.
public static class BattleHUDMigration
{
    static Canvas canvas;
    static GameManager gm;
    static SerializedObject so;
    static TMP_FontAsset font;
    static readonly Color Ink = new Color(.025f,.035f,.04f,.90f);
    static readonly Color Gold = new Color(.59f,.46f,.27f,.85f);
    static readonly Color Ivory = new Color(.96f,.92f,.81f,1);
    static T Ref<T>(string name) where T:UnityEngine.Object { return so.FindProperty(name)?.objectReferenceValue as T; }
    static RectTransform Rect(string name, Transform parent)
    { var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r; }
    static void Place(RectTransform r, Transform parent, float x,float y,float w,float h)
    {
        r.SetParent(parent,false); r.localScale=Vector3.one;r.localRotation=Quaternion.identity;
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);
    }
    static void Move(string field,Transform parent,float x,float y,float w,float h)
    {
        var value=so.FindProperty(field)?.objectReferenceValue;
        var t=value is Component c ? c.transform : value is GameObject g ? g.transform : null;
        if(t is RectTransform r)Place(r,parent,x,y,w,h);
    }
    static Image Solid(string name,Transform parent,float x,float y,float w,float h,Color color)
    {var r=Rect(name,parent);Place(r,parent,x,y,w,h);var im=r.gameObject.AddComponent<Image>();im.color=color;im.raycastTarget=false;return im;}
    static RectTransform Panel(string name,Transform parent,float x,float y,float w,float h)
    {
        var r=Rect(name,parent);Place(r,parent,x,y,w,h);
        var im=r.gameObject.AddComponent<Image>();im.color=Ink;im.raycastTarget=false;
        Solid("GoldTop",r,0,0,w,1.4f,Gold);Solid("GoldBottom",r,0,h-1.4f,w,1.4f,Gold);
        Solid("GoldLeft",r,0,0,1.4f,h,Gold);Solid("GoldRight",r,w-1.4f,0,1.4f,h,Gold);
        return r;
    }
    static TMP_Text Text(string name,Transform parent,string value,float x,float y,float w,float h,float size)
    {var r=Rect(name,parent);Place(r,parent,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;Style(t,size);return t;}
    static void Style(TMP_Text t,float size=26)
    {
        if(!t)return;
        t.color=Ivory;t.enableAutoSizing=false;t.fontSize=size;t.richText=true;
        t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;
        t.alignment=TextAlignmentOptions.MidlineLeft;t.margin=new Vector4(3,2,3,2);
    }
    static RectTransform Scroll(Transform parent,float x,float y,float w,float h)
    {
        var r=Rect("ScrollView",parent);Place(r,parent,x,y,w,h);
        var scroll=r.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
        var viewport=Rect("Viewport",r);Place(viewport,r,0,0,w-9,h);viewport.gameObject.AddComponent<RectMask2D>();
        var surface=viewport.gameObject.AddComponent<Image>();surface.color=Color.clear;surface.raycastTarget=true;
        var content=Rect("Content",viewport);Place(content,viewport,0,0,w-9,0);
        var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=8;layout.padding=new RectOffset(5,5,4,4);layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=viewport;scroll.content=content;
        var track=Solid("ScrollTrack",r,w-5,0,4,h,new Color(.2f,.2f,.18f,.5f));
        var handle=Solid("Handle",track.transform,0,0,4,h*.4f,Gold);handle.raycastTarget=true;
        var bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;bar.direction=Scrollbar.Direction.BottomToTop;scroll.verticalScrollbar=bar;
        return content;
    }
    static void Row(Transform content,Image icon,TMP_Text text,float minHeight)
    {
        var r=Rect("Row_"+text.name,content);
        var row=r.gameObject.AddComponent<HorizontalLayoutGroup>();row.spacing=10;row.padding=new RectOffset(4,4,6,6);row.childControlHeight=true;row.childControlWidth=true;row.childForceExpandWidth=false;row.childForceExpandHeight=false;row.childAlignment=TextAnchor.UpperLeft;
        r.gameObject.AddComponent<LayoutElement>().minHeight=minHeight;
        if(icon){icon.transform.SetParent(r,false);icon.transform.localScale=Vector3.one;icon.preserveAspect=true;var le=icon.GetComponent<LayoutElement>()??icon.gameObject.AddComponent<LayoutElement>();le.minWidth=48;le.preferredWidth=48;le.minHeight=62;le.preferredHeight=62;}
        text.transform.SetParent(r,false);text.transform.localScale=Vector3.one;Style(text,25);
        var element=text.GetComponent<LayoutElement>()??text.gameObject.AddComponent<LayoutElement>();element.flexibleWidth=1;
    }
    public static void Apply()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RunScene.unity");
        Directory.CreateDirectory("Logs/BattleRedesignBackup");
        if(!File.Exists("Logs/BattleRedesignBackup/RunScene.unity"))File.Copy("Assets/Scenes/RunScene.unity","Logs/BattleRedesignBackup/RunScene.unity");
        gm=UnityEngine.Object.FindAnyObjectByType<GameManager>();so=new SerializedObject(gm);
        canvas=UnityEngine.Object.FindAnyObjectByType<Canvas>();font=Ref<TMP_Text>("roundTMP").font;
        if(canvas.transform.Find("BattleHUD_Authored"))throw new Exception("Layout already authored; edit the saved scene instead of applying twice.");
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var root=Rect("BattleHUD_Authored",canvas.transform);Place(root,canvas.transform,0,0,1920,1080);
        root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,.5f);root.anchoredPosition=Vector2.zero;
        // Place persistent HUD underneath all existing modal/cut-in panels.
        root.SetAsFirstSibling();
        var hud=root.gameObject.AddComponent<TrialBattleHUD>();
        var bg=Solid("GodBackground",root,0,0,1920,1080,Color.white);hud.background=bg;
        Solid("ReadabilityShade",root,0,0,1920,1080,new Color(0,.015f,.02f,.54f));
        var left=Panel("Characters",root,12,14,300,1052);
        var header=Panel("RoundAndMission",root,324,14,1150,104);
        var enemyCards=Panel("GodHandSurface",root,324,132,1150,110);
        Panel("GodDiscardsSurface",root,324,250,1150,166);
        Panel("PlayerDiscardsSurface",root,324,428,1150,166);
        Panel("MeldSurface",root,324,606,1150,78);
        Panel("PlayerHandSurface",root,324,748,1150,108);
        Panel("DrawChoicesSurface",root,324,866,1150,102);
        var passive=Panel("PassiveSkills",root,1488,14,420,416);Text("Title",passive,"パッシブ",12,4,390,38,30);
        var passiveContent=Scroll(passive,8,47,402,358);
        var charm=Panel("Charms",root,1488,442,420,304);Text("Title",charm,"お守り",12,4,390,38,30);
        var charmContent=Scroll(charm,8,47,402,246);
        var talisman=Panel("Talismans",root,1488,758,420,308);Text("Title",talisman,"お札",12,4,390,38,30);
        var talismanContent=Scroll(talisman,8,47,402,250);
        // Reuse all game-owned references so click actions and tutorial focus follow the new hierarchy.
        Portrait("enemyPortrait",left,8,6,284,240);
        Move("enemyNameTMP",left,12,242,278,40);Style(Ref<TMP_Text>("enemyNameTMP"),32);
        Bar("enemyHPBar","enemyHPTMP",left,12,288,new Color(.65f,.10f,.10f));
        Move("enemySkillCountdownTMP",left,12,328,278,62);Style(Ref<TMP_Text>("enemySkillCountdownTMP"),24);
        Move("enemySkillAngerIcon",left,12,395,32,32);Move("enemySkillDefenseIcon",left,52,395,32,32);
        Portrait("playerPortrait",left,8,446,284,230);
        Move("_skillNameTMP",left,12,676,276,40);Style(Ref<TMP_Text>("_skillNameTMP"),30);
        Bar("playerHPBar","playerHPTMP",left,12,722,new Color(.19f,.43f,.25f));
        Bar("playerMPBar","playerMPTMP",left,12,764,new Color(.17f,.35f,.65f));
        Move("enemySkillPoisonIcon",left,208,680,30,30);Move("enemySkillParalysisIcon",left,246,680,30,30);
        Move("_skillActionNameTMP",left,12,810,276,40);Style(Ref<TMP_Text>("_skillActionNameTMP"),28);
        var actionContent=Scroll(left,10,854,280,96);var action=Ref<TMP_Text>("_skillDescTMP");Row(actionContent,null,action,80);
        Move("btnMenu",left,8,985,89,50);Move("btnDeck",left,106,985,89,50);
        var deck=canvas.transform.Find("Deck");if(deck)Place((RectTransform)deck,left,106,985,89,50);
        var relic=UnityEngine.Object.Instantiate(Ref<Button>("btnMenu"),left);relic.name="Button_ConsumableInventory";relic.onClick=new Button.ButtonClickedEvent();Place((RectTransform)relic.transform,left,204,985,89,50);hud.relicButton=relic;
        foreach(var l in relic.GetComponentsInChildren<LocalizedTextUI>(true))l.enabled=false;
        relic.GetComponentInChildren<TMP_Text>(true).text="遺物";
        Move("roundTMP",header,18,4,255,52);Style(Ref<TMP_Text>("roundTMP"),38);
        Move("turnTMP",header,290,6,220,46);Style(Ref<TMP_Text>("turnTMP"),28);
        Move("scoreTMP",header,530,6,180,46);Style(Ref<TMP_Text>("scoreTMP"),26);
        Move("targetTMP",header,530,55,180,38);
        Move("missionDisplayTMP",header,18,60,700,34);Style(Ref<TMP_Text>("missionDisplayTMP"),25);
        var dora=canvas.transform.Find("TopBar/wanpaiArea");if(dora){Place((RectTransform)dora,header,790,14,340,76);dora.localScale=Vector3.one*.65f;}
        Move("enemyHandArea",root,346,138,1090,100);
        Move("enemyTenpaiHandManualRoot",root,346,138,1090,100);
        Move("enemyMeldsManualRoot",root,346,138,1090,100);
        Move("enemyDiscardArea",root,338,259,1118,146);Move("discardArea",root,338,437,1118,146);
        ConfigureGrid(Ref<RectTransform>("enemyDiscardArea"));ConfigureGrid(Ref<RectTransform>("discardArea"));
        Move("handArea",root,344,752,1110,98);
        so.FindProperty("playerHandAreaPositionWithoutOpenMeld").vector2Value=new Vector2(344,-752);
        so.FindProperty("playerHandAreaPositionWithOpenMeld").vector2Value=new Vector2(344,-752);
        Move("offerArea",root,344,872,1110,90);
        Move("meldArea",root,344,610,1110,70);
        var slots=so.FindProperty("playerMeldSlots");for(int i=0;i<slots.arraySize;i++){var r=slots.GetArrayElementAtIndex(i).objectReferenceValue as RectTransform;if(r){Place(r,root,344+i*270,608,260,75);r.localScale=Vector3.one*.6f;}}
        Move("playerSeatTMP",root,332,698,110,40);Style(Ref<TMP_Text>("playerSeatTMP"),28);
        Move("shantenTMP",root,455,698,180,40);Style(Ref<TMP_Text>("shantenTMP"),26);
        Move("enemyRiichiStatusTMP",root,1210,204,240,32);
        var waits=Ref<GameObject>("playerTenpaiWaitsRoot");if(waits){Place((RectTransform)waits.transform,root,660,698,760,40);waits.transform.localScale=Vector3.one*.40f;}
        Move("legendaryOngoingEffectsTMP",root,324,970,1150,18);Style(Ref<TMP_Text>("legendaryOngoingEffectsTMP"),18);
        Move("tenpaiBadgeTMP",root,458,694,180,40);
        var bottom=canvas.transform.Find("BottomBar") as RectTransform;Place(bottom,root,324,988,1150,66);
        foreach(var lg in bottom.GetComponents<LayoutGroup>())UnityEngine.Object.DestroyImmediate(lg);
        var flow=bottom.gameObject.AddComponent<HorizontalLayoutGroup>();flow.spacing=6;flow.childControlWidth=true;flow.childControlHeight=true;flow.childForceExpandWidth=true;flow.childForceExpandHeight=true;
        foreach(var b in bottom.GetComponentsInChildren<Button>(true)){var le=b.GetComponent<LayoutElement>()??b.gameObject.AddComponent<LayoutElement>();le.minWidth=90;le.flexibleWidth=1;StyleButton(b);}
        foreach(var b in left.GetComponentsInChildren<Button>(true))StyleButton(b);
        var skillPanel=canvas.transform.Find("SkillPanel");
        string[] traits={"_skillTraitGekiTMP","_skillTraitIyuTMP","_skillTraitShunTMP"};string[] icons={"Geki","Yu","Syun"};
        for(int i=0;i<3;i++)Row(passiveContent,skillPanel.Find(icons[i]).GetComponent<Image>(),Ref<TMP_Text>(traits[i]),104);
        Move("tutorialPassiveFocus",passive,0,44,420,365);
        var originalCharm=Ref<TMP_Text>("_omamoriInfoTMP");var originalIcon=Ref<Image>("_omamoriIconImage");
        hud.charmDescriptions=new TMP_Text[3];hud.charmIcons=new Image[3];
        for(int i=0;i<3;i++){var t=i==0?originalCharm:UnityEngine.Object.Instantiate(originalCharm,charmContent);var icon=i==0?originalIcon:UnityEngine.Object.Instantiate(originalIcon,charmContent);t.name="CharmDescription"+i;icon.name="CharmIcon"+i;Row(charmContent,icon,t,88);hud.charmDescriptions[i]=t;hud.charmIcons[i]=icon;}
        var texts=so.FindProperty("_ofudaInfoTMPs");var pictures=so.FindProperty("_ofudaIconImages");
        for(int i=0;i<3;i++)Row(talismanContent,pictures.GetArrayElementAtIndex(i).objectReferenceValue as Image,texts.GetArrayElementAtIndex(i).objectReferenceValue as TMP_Text,88);
        so.FindProperty("ofudaPanel").objectReferenceValue=talisman;
        so.FindProperty("authoredBattleHUD").objectReferenceValue=hud;
        hud.players=new[]{Visual("RandomMan",true),Visual("RandomHonor",true),Visual("Capitalist",true)};
        hud.gods=new[]{"アマテラス","アヌビス","ポセイドン","ゼウス","オーディン","シヴァ","スサノオ","バステト","フレイヤ","ルーナ","ハデス"}.Select(n=>Visual(n,false)).ToArray();
        bg.sprite=hud.gods[0].background;
        so.ApplyModifiedPropertiesWithoutUndo();
        // Old decorative backing is not gameplay. All referenced controls have been moved out.
        foreach(var name in new[]{"TopBar","PlayerHP","PlayerMP","EnemyHP","SkillPanel","OmamoriPanel","OfudaPanel","OfferArea"}){var old=canvas.transform.Find(name);if(old)old.gameObject.SetActive(false);}
        foreach(Transform child in canvas.transform)if(child.name.StartsWith("Image")||child.name=="Panel_Background (1)"||child.name=="LabelSpecialEffect")child.gameObject.SetActive(false);
        Move("skillTMP",root,324,962,450,24);
        EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);EditorSceneManager.SaveScene(gm.gameObject.scene);
        Capture("Logs/TrialBattleAuthored.png");Debug.Log("TRIAL_BATTLE_LAYOUT_SAVED");
    }
    static TrialBattleHUD.CharacterVisual Visual(string key,bool player)
    {
        return new TrialBattleHUD.CharacterVisual { key=key,portrait=Resources.Load<Sprite>((player?"PlayerCutins/":"EnemyCutins/")+key+(player?"_victory":"")),background=Resources.Load<Sprite>("BattleBackgrounds/"+key) };
    }
    static void Portrait(string field,Transform parent,float x,float y,float w,float h)
    {
        var viewport=Rect(field+"_PortraitViewport",parent);Place(viewport,parent,x,y,w,h);viewport.gameObject.AddComponent<RectMask2D>();
        Move(field,viewport,-14,0,w+28,h*1.8f);var image=Ref<Image>(field);image.preserveAspect=true;image.color=Color.white;
        image.sprite=Resources.Load<Sprite>(field=="enemyPortrait"?"EnemyCutins/アマテラス":"PlayerCutins/RandomMan_victory");
    }
    static void Bar(string field,string textField,Transform parent,float x,float y,Color fill)
    {
        Panel(field+"_Frame",parent,x-2,y-2,280,32);
        Move(field,parent,x,y,276,28);var image=Ref<Image>(field);image.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");image.color=fill;image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Horizontal;
        Move(textField,parent,x+4,y,268,28);Style(Ref<TMP_Text>(textField),24);Ref<TMP_Text>(textField).alignment=TextAlignmentOptions.Center;
    }
    static void ConfigureGrid(RectTransform r)
    {
        var g=r.GetComponent<GridLayoutGroup>();if(g){g.cellSize=new Vector2(56,66);g.spacing=new Vector2(5,6);g.constraint=GridLayoutGroup.Constraint.FixedColumnCount;g.constraintCount=18;g.childAlignment=TextAnchor.UpperLeft;g.padding=new RectOffset(4,4,2,2);}
    }
    static void StyleButton(Button b)
    {
        var im=b.GetComponent<Image>();if(im){im.color=new Color(.06f,.065f,.055f,1);im.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");im.type=Image.Type.Sliced;}
        var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.87f,.59f);colors.pressedColor=new Color(.65f,.54f,.32f);b.colors=colors;
        foreach(var t in b.GetComponentsInChildren<TMP_Text>(true)){Style(t,27);t.enableAutoSizing=true;t.fontSizeMin=19;t.fontSizeMax=27;t.alignment=TextAlignmentOptions.Center;t.rectTransform.anchorMin=Vector2.zero;t.rectTransform.anchorMax=Vector2.one;t.rectTransform.offsetMin=new Vector2(4,3);t.rectTransform.offsetMax=new Vector2(-4,-3);}
    }
    public static void Polish()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RunScene.unity");
        gm=UnityEngine.Object.FindAnyObjectByType<GameManager>();so=new SerializedObject(gm);
        canvas=gm.GetComponentInParent<Canvas>();if(!canvas)canvas=UnityEngine.Object.FindAnyObjectByType<Canvas>();
        font=Ref<TMP_Text>("roundTMP").font;
        var root=canvas.transform.Find("BattleHUD_Authored");
        var old=canvas.transform.Find("Panel_Background");if(old)old.gameObject.SetActive(false);
        var hud=root.GetComponent<TrialBattleHUD>();
        var header=root.Find("RoundAndMission");
        Move("scoreTMP",header,495,6,200,44);Ref<TMP_Text>("scoreTMP").enableAutoSizing=true;Ref<TMP_Text>("scoreTMP").fontSizeMin=20;Ref<TMP_Text>("scoreTMP").fontSizeMax=26;
        if(!header.Find("GoldIcon")) {var bag=Solid("GoldIcon",header,698,12,32,32,Color.white);bag.sprite=Resources.Load<Sprite>("Consumables/CurrencyBag");bag.preserveAspect=true;hud.goldAmount=Text("GoldAmount",header,"0",738,10,115,38,26);}
        var dora=Ref<RectTransform>("wanpaiArea");Place(dora,header,865,40,480,100);dora.localScale=Vector3.one*.55f;
        if(!header.Find("DoraCaption"))Text("DoraCaption",header,"ドラ表示牌",865,3,240,30,22);
        var tiles=PrefabUtility.LoadPrefabContents("Assets/Prefabs/TilePrefab.prefab");
        foreach(var path in new[]{"Art","Art/Image"}) { var r=tiles.transform.Find(path) as RectTransform;if(r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;} }
        var tileAsset=PrefabUtility.SaveAsPrefabAsset(tiles,"Assets/Prefabs/TrialBattleTile.prefab");PrefabUtility.UnloadPrefabContents(tiles);
        so.FindProperty("tilePrefab").objectReferenceValue=tileAsset;
        foreach(var field in new[]{"enemyDiscardArea","discardArea"}) {var grid=Ref<RectTransform>(field).GetComponent<GridLayoutGroup>();grid.cellSize=new Vector2(44,61);grid.spacing=new Vector2(2,7);grid.constraintCount=24;}
        foreach(var child in root.GetComponentsInChildren<Image>(true))
            if(child.name=="Characters"||child.name=="PassiveSkills"||child.name=="Charms"||child.name=="Talismans") child.color=new Color(.025f,.035f,.04f,.98f);
        foreach(var file in Directory.GetFiles("Assets/Resources/BattleBackgrounds","*.png"))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.maxTextureSize=2048;importer.mipmapEnabled=false;importer.SaveAndReimport();
        }
        hud.gods= new[]{"アマテラス","アヌビス","ポセイドン","ゼウス","オーディン","シヴァ","スサノオ","バステト","フレイヤ","ルーナ","ハデス"}.Select(n=>Visual(n,false)).ToArray();
        hud.background.sprite=hud.gods[0].background;
        foreach(var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            t.fontSharedMaterial=t.font.material;
            t.color=Ivory;
        }
        foreach(var b in root.GetComponentsInChildren<Button>(true))
        {
            var outline=b.GetComponent<Outline>()??b.gameObject.AddComponent<Outline>();outline.effectColor=Gold;outline.effectDistance=new Vector2(1,-1);
            foreach(var text in b.GetComponentsInChildren<TMP_Text>(true)){text.textWrappingMode=TextWrappingModes.NoWrap;text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=27;}
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);EditorSceneManager.SaveScene(gm.gameObject.scene);
        Capture("Logs/TrialBattleAuthored.png");
    }
    public static void Audit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RunScene.unity");
        var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        var so = new SerializedObject(gm);
        var p = so.GetIterator();
        var text = new StringBuilder();
        while(p.NextVisible(true))
        {
            if(p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue)
            {
                var c = p.objectReferenceValue as Component;
                text.AppendLine(p.propertyPath + " = " + (c ? Path(c.transform) : p.objectReferenceValue.name));
            }
        }
        foreach(var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            foreach(var t in c.GetComponentsInChildren<RectTransform>(true))
            {
                if(t.GetComponentsInParent<Canvas>(true).Length != 1) continue;
                var tmp=t.GetComponent<TMP_Text>();
                text.AppendLine(Path(t)+" active="+t.gameObject.activeSelf+" pos="+t.anchoredPosition+" size="+t.sizeDelta+" scale="+t.localScale+ (tmp?" text="+tmp.text.Replace("\n"," / "):""));
            }
        File.WriteAllText("Logs/TrialBattleAudit.txt",text.ToString());
        Capture("Logs/TrialBattleBefore.png");
        Debug.Log("TRIAL_BATTLE_AUDIT_OK");
    }
    public static void Refine()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RunScene.unity");
        gm=UnityEngine.Object.FindAnyObjectByType<GameManager>();so=new SerializedObject(gm);
        canvas=UnityEngine.Object.FindAnyObjectByType<Canvas>();font=Ref<TMP_Text>("roundTMP").font;
        var root=canvas.transform.Find("BattleHUD_Authored");var hud=root.GetComponent<TrialBattleHUD>();
        var path="Assets/Resources/TrialBattleUI/AntiqueFrame.png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spriteBorder=new Vector4(145,145,145,145);importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.SaveAndReimport();
        var frame=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        // Detailed, nine-sliced borders are ordinary Images saved in the hierarchy.
        foreach(var panel in root.GetComponentsInChildren<Image>(true).Where(i=>i.transform.Find("GoldTop")).ToArray())
        {
            foreach(var n in new[]{"GoldTop","GoldBottom","GoldLeft","GoldRight"})panel.transform.Find(n).gameObject.SetActive(false);
            var border=panel.transform.Find("AntiqueBorder")?.GetComponent<Image>();
            if(!border)border=Solid("AntiqueBorder",panel.transform,0,0,0,0,Color.white);
            border.sprite=frame;border.type=Image.Type.Sliced;border.pixelsPerUnitMultiplier=5;
            Stretch(border.rectTransform);border.transform.SetAsLastSibling();
        }
        // No separate meld panel: all concealed tiles and four meld slots share this row.
        root.Find("MeldSurface").gameObject.SetActive(false);
        var row=root.Find("PlayerTileRow") as RectTransform;
        if(!row){row=Rect("PlayerTileRow",root);Place(row,root,336,750,1126,102);}
        hud.handRow=row.GetComponent<TrialHandRowLayout>()??row.gameObject.AddComponent<TrialHandRowLayout>();
        hud.handRow.hand=Ref<RectTransform>("handArea");hud.handRow.hand.SetParent(row,false);
        var slots=so.FindProperty("playerMeldSlots");hud.handRow.meldSlots=new RectTransform[slots.arraySize];
        for(int i=0;i<slots.arraySize;i++){var s=slots.GetArrayElementAtIndex(i).objectReferenceValue as RectTransform;hud.handRow.meldSlots[i]=s;if(s)s.SetParent(row,false);}
        foreach(var group in hud.handRow.hand.GetComponents<LayoutGroup>())group.enabled=false;
        foreach(var field in new[]{"discardArea","enemyDiscardArea"}) {var grid=Ref<RectTransform>(field).GetComponent<GridLayoutGroup>();grid.spacing=new Vector2(0,7);grid.cellSize=new Vector2(46,64);grid.constraintCount=24;}
        foreach(var field in new[]{"offerArea","enemyHandArea"}) {var flow=Ref<RectTransform>(field)?.GetComponent<HorizontalLayoutGroup>();if(flow){flow.spacing=0;flow.childAlignment=TextAnchor.MiddleCenter;}}
        var header=root.Find("RoundAndMission") as RectTransform;Place(header,root,324,14,1150,132);
        Move("roundTMP",header,24,6,250,60);Move("turnTMP",header,285,12,250,50);
        Move("scoreTMP",header,560,12,265,40);
        Move("missionDisplayTMP",header,24,82,650,38);
        Place((RectTransform)header.Find("GoldIcon"),header,718,85,32,32);Place(hud.goldAmount.rectTransform,header,760,82,85,38);
        Place((RectTransform)header.Find("DoraCaption"),header,866,0,272,30);
        hud.doraArea=Ref<RectTransform>("wanpaiArea");Place(hud.doraArea,header,864,31,274,98);
        foreach(var layout in hud.doraArea.GetComponents<LayoutGroup>())layout.enabled=false;
        Place((RectTransform)root.Find("GodHandSurface"),root,324,156,1150,110);Move("enemyHandArea",root,346,161,1090,100);
        Move("enemyTenpaiHandManualRoot",root,346,161,1090,100);Move("enemyMeldsManualRoot",root,346,161,1090,100);
        Place((RectTransform)root.Find("GodDiscardsSurface"),root,324,278,1150,190);
        Place((RectTransform)root.Find("PlayerDiscardsSurface"),root,324,480,1150,194);
        Move("enemyDiscardArea",root,338,297,1118,146);Move("discardArea",root,338,505,1118,146);
        so.FindProperty("tutorialEnemyDiscardFocus").objectReferenceValue=root.Find("GodDiscardsSurface");
        Move("enemyRiichiStatusTMP",root,1220,226,220,35);Move("enemyRiichiStatusBGObject",root,1210,226,240,35);
        var riichiBG=Ref<GameObject>("enemyRiichiStatusBGObject");if(riichiBG){riichiBG.GetComponent<Image>().color=new Color(0,0,0,.65f);riichiBG.transform.SetSiblingIndex(Ref<TMP_Text>("enemyRiichiStatusTMP").transform.GetSiblingIndex());}
        var seat= root.Find("SeatFrame") as RectTransform;if(!seat)seat=Panel("SeatFrame",root,330,693,120,46);
        Move("playerSeatTMP",seat,3,1,114,44);Ref<TMP_Text>("playerSeatTMP").alignment=TextAlignmentOptions.Center;
        AddFrame(seat,frame);
        var left=root.Find("Characters");
        foreach(var field in new[]{"enemyPortrait","playerPortrait"})
        {
            var image=Ref<Image>(field);var viewport=(RectTransform)image.transform.parent;
            Place(viewport,left,0,field=="enemyPortrait"?0:442,300,442);
            Place(image.rectTransform,viewport,-118,-12,536,804);image.preserveAspect=true;
        }
        // Bust artwork remains behind the name and gauges, as in the approved mockup.
        foreach(var field in new[]{"enemyNameTMP","_skillNameTMP"})
        {
            var text=Ref<TMP_Text>(field);var backing=left.Find(field+"_Ink")?.GetComponent<Image>();
            if(!backing)backing=Solid(field+"_Ink",left,8,field=="enemyNameTMP"?242:676,284,44,new Color(0,0,0,.94f));
            backing.transform.SetAsLastSibling();text.transform.SetAsLastSibling();
        }
        foreach(var name in new[]{"enemyHPBar_Frame","playerHPBar_Frame","playerMPBar_Frame"})left.Find(name).SetAsLastSibling();
        foreach(var field in new[]{"enemyHPBar","playerHPBar","playerMPBar","enemyHPTMP","playerHPTMP","playerMPTMP","enemySkillCountdownTMP","enemySkillAngerIcon","enemySkillDefenseIcon","enemySkillPoisonIcon","enemySkillParalysisIcon","_skillActionNameTMP"})
        {var obj=so.FindProperty(field)?.objectReferenceValue;var t=obj is Component c?c.transform:obj is GameObject g?g.transform:null;if(t)t.SetAsLastSibling();}
        hud.skillButtonLabel=Ref<Button>("btnSkill").GetComponentInChildren<TMP_Text>(true);
        foreach(var l in hud.skillButtonLabel.GetComponents<LocalizedTextUI>())l.enabled=false;
        var allButtons=root.GetComponentsInChildren<Button>(true);hud.interactiveFrames=new Image[allButtons.Length];
        for(int i=0;i<allButtons.Length;i++)
        {
            var b=allButtons[i];var image=AddFrame((RectTransform)b.transform,frame);hud.interactiveFrames[i]=image;
            var colors=b.colors;colors.normalColor=new Color(.13f,.12f,.09f);colors.highlightedColor=new Color(.32f,.26f,.15f);colors.pressedColor=new Color(.45f,.32f,.15f);colors.disabledColor=new Color(.035f,.04f,.045f,.9f);b.colors=colors;
            if(b.targetGraphic)b.targetGraphic.color=Color.white;
            foreach(var text in b.GetComponentsInChildren<TMP_Text>(true)){text.textWrappingMode=TextWrappingModes.NoWrap;text.fontSizeMin=15;text.fontSizeMax=29;}
        }
        // Save icon layers in the scene as well; runtime code only changes their sprites.
        foreach(var icon in hud.charmIcons){ItemArtwork.Ofuda(icon,"Normal");ItemArtwork.Clear(icon);}
        foreach(var icon in Ref<Image>("_omamoriIconImage").GetComponentsInChildren<Image>(true))icon.raycastTarget=false;
        var ofudaIcons=so.FindProperty("_ofudaIconImages");for(int i=0;i<ofudaIcons.arraySize;i++){var icon=ofudaIcons.GetArrayElementAtIndex(i).objectReferenceValue as Image;ItemArtwork.Ofuda(icon,"Normal");ItemArtwork.Clear(icon);}
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);EditorSceneManager.SaveScene(gm.gameObject.scene);
        Capture("Logs/TrialBattleRefined.png");
    }
    static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    static Image AddFrame(RectTransform parent,Sprite sprite)
    {
        var im=parent.Find("AntiqueBorder")?.GetComponent<Image>();if(!im)im=Solid("AntiqueBorder",parent,0,0,0,0,Color.white);
        im.sprite=sprite;im.type=Image.Type.Sliced;im.pixelsPerUnitMultiplier=5;Stretch(im.rectTransform);im.transform.SetAsLastSibling();return im;
    }
    static string Path(Transform t) { return t.parent ? Path(t.parent)+"/"+t.name : t.name; }
    static void Capture(string path) { }
    public static void Install()
    {
        Directory.CreateDirectory("Logs");
        EditorSceneManager.OpenScene("Assets/Scenes/RunScene.unity");
        if(!UnityEngine.Object.FindAnyObjectByType<TrialBattleHUD>()) { Apply(); Polish(); }
        Refine();
        FixRequestedDetails();
        Debug.Log("BATTLE_HUD_MIGRATION_OK");
    }
    public static void FixRequestedDetails()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RunScene.unity");
        gm=UnityEngine.Object.FindAnyObjectByType<GameManager>();so=new SerializedObject(gm);
        var hud=UnityEngine.Object.FindAnyObjectByType<TrialBattleHUD>();var root=(RectTransform)hud.transform;
        font=Ref<TMP_Text>("roundTMP").font;
        var header=root.Find("RoundAndMission");
        Place((RectTransform)header.Find("DoraCaption"),header,864,8,270,30);
        var caption=header.Find("DoraCaption").GetComponent<TMP_Text>();Style(caption,24);
        Place(hud.doraArea,header,864,40,274,90);hud.doraTileSize=new Vector2(64,90);
        // Wait tiles have their own 13 slots: long multi-sided waits still fit on one row.
        var waits=Ref<GameObject>("playerTenpaiWaitsRoot");
        Place((RectTransform)waits.transform,root,850,681,610,64);
        foreach(var layout in waits.GetComponents<LayoutGroup>())layout.enabled=false;
        var slots=so.FindProperty("playerTenpaiWaitSlots");slots.arraySize=13;
        for(int i=0;i<13;i++)
        {
            var slot=waits.transform.Find("WaitSlot"+i) as RectTransform;
            if(!slot)slot=Rect("WaitSlot"+i,waits.transform);
            Place(slot,waits.transform,i*46,0,44,62);
            slots.GetArrayElementAtIndex(i).objectReferenceValue=slot;
        }
        foreach(Transform old in waits.transform)if(!old.name.StartsWith("WaitSlot"))old.gameObject.SetActive(false);
        so.FindProperty("overridePlayerTenpaiWaitTileSize").boolValue=true;
        so.FindProperty("playerTenpaiWaitTileSize").vector2Value=new Vector2(44,62);
        var label=root.Find("WaitTilesCaption")?.GetComponent<TMP_Text>();
        if(!label)label=Text("WaitTilesCaption",root,"待ち牌",746,695,104,36,28);
        hud.waitsRoot=waits;hud.waitsCaption=label;
        Move("shantenTMP",root,456,696,260,40);Style(Ref<TMP_Text>("shantenTMP"),29);
        Move("tenpaiBadgeTMP",root,704,700,38,32);Style(Ref<TMP_Text>("tenpaiBadgeTMP"),22);
        Move("enemyRiichiStatusTMP",root,1210,283,245,36);Style(Ref<TMP_Text>("enemyRiichiStatusTMP"),28);
        Ref<TMP_Text>("enemyRiichiStatusTMP").alignment=TextAlignmentOptions.MidlineRight;
        Move("enemyDiscardArea",root,338,324,1118,136);
        var bg=Ref<GameObject>("enemyRiichiStatusBGObject");if(bg)bg.SetActive(false);
        so.FindProperty("enemyRiichiStatusBGObject").objectReferenceValue=null;
        var offer=Ref<RectTransform>("offerArea");var offerLayout=offer.GetComponent<HorizontalLayoutGroup>();
        if(offerLayout){offerLayout.childAlignment=TextAnchor.MiddleCenter;offerLayout.spacing=0;}
        foreach(var panelName in new[]{"Charms","Talismans","PassiveSkills"})
        {
            var panel=(RectTransform)root.Find(panelName);
            var scroll=panel.GetComponentInChildren<ScrollRect>(true);
            Place((RectTransform)scroll.transform,panel,12,48,panel.rect.width-24,panel.rect.height-62);
            var viewport=scroll.viewport;viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;
            viewport.offsetMin=new Vector2(0,0);viewport.offsetMax=new Vector2(-18,0);
            var track=(RectTransform)scroll.verticalScrollbar.transform;
            track.anchorMin=new Vector2(1,0);track.anchorMax=new Vector2(1,1);track.pivot=new Vector2(1,.5f);
            track.offsetMin=new Vector2(-8,4);track.offsetMax=new Vector2(-2,-4);
            var handle=scroll.verticalScrollbar.handleRect;handle.pivot=new Vector2(.5f,.5f);handle.offsetMin=handle.offsetMax=Vector2.zero;
            scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            var content=scroll.content;content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;
            content.pivot=new Vector2(0,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=new Vector2(0,content.sizeDelta.y);
        }
        foreach(var text in hud.charmDescriptions)if(text)text.text="ー";
        so.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);EditorSceneManager.SaveScene(gm.gameObject.scene);
        File.WriteAllText("Logs/BattleHUDMigrationResult.txt","Installed scene-authored HUD and requested corrections.");
    }
}
