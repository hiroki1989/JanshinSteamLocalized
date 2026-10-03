using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DevilContractArtSetup
{
    static TMP_FontAsset font;
    static RectTransform R(string n,Transform p,float x,float y,float w,float h)=>ConsumableWindow.Rect(n,p,new Vector2(x,y),new Vector2(w,h));
    static Image I(string n,Transform p,float x,float y,float w,float h,Sprite s,Color color)
    {var i=R(n,p,x,y,w,h).gameObject.AddComponent<Image>();i.sprite=s;i.color=color;i.raycastTarget=false;return i;}
    static TMP_Text T(string n,Transform p,float x,float y,float w,float h,float size)
    {var t=ConsumableWindow.Label(n,p,"",new Vector2(x,y),new Vector2(w,h),size);t.font=font;t.fontSharedMaterial=font.material;t.color=JanshinPanelTheme.Ivory;t.fontSizeMin=size*.8f;t.margin=new Vector4(8,4,8,4);return t;}
    static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    static void Skin(Image face,Color tint)
    {var theme=face.GetComponent<JanshinPanelTheme>();if(theme)theme.preserveRarityGradient=true;face.sprite=null;face.type=Image.Type.Simple;face.color=tint;}
    static Sprite Background=>Resources.Load<Sprite>("DevilContracts/Art/Sanctum");
    public static void Run()
    {
        Directory.CreateDirectory("Logs/DevilContracts");
        AssetDatabase.Refresh();
        foreach(string path in Directory.GetFiles("Assets/Resources/DevilContracts","*.png",SearchOption.AllDirectories))
        {
            var imp=AssetImporter.GetAtPath(path) as TextureImporter;if(!imp)continue;
            imp.textureShape=TextureImporterShape.Texture2D;imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;imp.mipmapEnabled=false;imp.maxTextureSize=2048;imp.textureCompression=TextureImporterCompression.CompressedHQ;imp.alphaIsTransparency=true;imp.SaveAndReimport();
        }
        font=LocalizationManager.Instance.GetBodyFont()??TMP_Settings.defaultFontAsset;
        BuildLedger();BuildReward();AssetDatabase.SaveAssets();
        MissionRewardQA.Run();
        Debug.Log("Devil contract art and mission rewards verified");
    }
    public static void UpdatePanelLayout()
    {
        font=LocalizationManager.Instance.GetBodyFont()??TMP_Settings.defaultFontAsset;
        BuildLedger();AssetDatabase.SaveAssets();MissionRewardQA.Run();
        Debug.Log("Devil contract panel layout verified");
    }
    static void BuildLedger()
    {
        const string path="Assets/Resources/DevilContracts/ContractView.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);var v=root.GetComponent<DevilContractView>();
        foreach(string n in new[]{"SanctumArt","SanctumShade"}){var old=root.transform.Find(n);if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);}
        var bg=I("SanctumArt",root.transform,0,0,1920,1080,Background,Color.white);Stretch(bg.rectTransform);bg.transform.SetSiblingIndex(1);
        var shade=I("SanctumShade",root.transform,0,0,1920,1080,null,new Color(0,0,0,.32f));Stretch(shade.rectTransform);shade.transform.SetSiblingIndex(2);
        var backdrop=root.transform.Find("Backdrop").GetComponent<Image>();backdrop.color=Color.black;
        var ledger=root.transform.Find("ContractLedger");Skin(ledger.GetComponent<Image>(),new Color(.018f,.018f,.025f,.55f));
        var details=ledger.Find("SelectedContract");var oldArt=details.Find("SelectedDemonArt");if(oldArt)UnityEngine.Object.DestroyImmediate(oldArt.gameObject);
        v.selectedPortrait=I("SelectedDemonArt",details,260,40,930,930,null,new Color(.65f,.60f,.65f,.34f));v.selectedPortrait.preserveAspect=true;v.selectedPortrait.transform.SetAsFirstSibling();
        foreach(var face in ledger.GetComponentsInChildren<JanshinPanelTheme>(true))
        {if(face.transform==ledger)continue;Skin(face.face,new Color(.025f,.021f,.03f,.88f));}
        for(int i=0;i<v.entryIcons.Length;i++)if(v.entryIcons[i]){v.entryIcons[i].sprite=DevilContractIcons.Get(i);v.entryIcons[i].rectTransform.sizeDelta=new Vector2(64,64);}
        v.selectedIcon.rectTransform.sizeDelta=new Vector2(94,94);
        v.selectedIcon.rectTransform.anchoredPosition=new Vector2(-385,296);
        v.devilName.rectTransform.anchoredPosition=new Vector2(36,307);
        v.contractName.rectTransform.anchoredPosition=new Vector2(36,255);
        v.contractName.rectTransform.sizeDelta=new Vector2(810,45);
        v.title.color=JanshinPanelTheme.Gold;
        v.subtitle.text="";v.subtitle.gameObject.SetActive(false);
        v.footer.text="";v.footer.gameObject.SetActive(false);
        var oldGem=ledger.Find("MenuGemIcon");if(oldGem)UnityEngine.Object.DestroyImmediate(oldGem.gameObject);
        var gem=I("MenuGemIcon",ledger,735,437,48,48,AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Dark UI/New Icons/White Gem 2.png"),Color.white);
        if(!gem.sprite)throw new Exception("Menu gem sprite missing");
        gem.preserveAspect=true;
        v.gems.rectTransform.anchoredPosition=new Vector2(815,437);
        v.gems.rectTransform.sizeDelta=new Vector2(110,52);v.gems.alignment=TextAlignmentOptions.Left;
        foreach(var label in v.entryLabels){label.alignment=TextAlignmentOptions.Center;label.rectTransform.anchoredPosition=Vector2.zero;label.rectTransform.sizeDelta=new Vector2(250,68);}

        // Slender double rules and inset corners keep the new artwork in the established gold UI family.
        foreach(string n in new[]{"HeaderRule","FooterRule"}){var old=ledger.Find(n);if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);}
        I("HeaderRule",ledger,0,372,1670,2,null,new Color(.66f,.46f,.25f,.8f));
        I("FooterRule",ledger,0,-400,1670,2,null,new Color(.66f,.46f,.25f,.8f));
        PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
    }
    static void BuildReward()
    {
        var root=new GameObject("MissionAndContractReward",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var c=root.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=32200;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var v=root.AddComponent<MissionRewardView>();v.group=root.AddComponent<CanvasGroup>();
        var bg=I("Sanctum",root.transform,0,0,1920,1080,Background,Color.white);Stretch(bg.rectTransform);bg.raycastTarget=true;
        var shade=I("Veil",root.transform,0,0,1920,1080,null,new Color(0,0,0,.55f));Stretch(shade.rectTransform);
        v.card=R("AcquisitionPanel",root.transform,0,0,1740,860);
        var face=v.card.gameObject.AddComponent<Image>();JanshinPanelTheme.Apply(face);Skin(face,new Color(.018f,.018f,.027f,.90f));
        var portraitFrame=R("DemonPortrait",v.card,-437,0,800,800);v.portraitRoot=portraitFrame.gameObject;
        v.portrait=I("Portrait",portraitFrame,0,0,800,800,null,Color.white);v.portrait.preserveAspect=true;
        var frame=I("AntiqueFrame",portraitFrame,0,0,800,800,JanshinPanelTheme.Frame,Color.white);frame.type=Image.Type.Sliced;frame.pixelsPerUnitMultiplier=5;
        // Body lives in a separate dark column, never over the demon's face.
        var column=R("RewardDetails",v.card,430,0,800,790);
        v.heading=T("Heading",column,0,333,740,62,43);v.heading.color=JanshinPanelTheme.Gold;
        v.accent=I("AccentRule",column,0,280,690,2,null,JanshinPanelTheme.Gold);
        v.icon=I("SharedDevilIcon",column,-278,205,94,94,null,Color.white);v.icon.preserveAspect=true;
        v.nameText=T("Name",column,56,205,558,90,43);
        v.body=T("Effects",column,0,-30,712,344,32);v.body.alignment=TextAlignmentOptions.TopLeft;
        v.footer=T("PermanentOwnership",column,0,-237,716,82,25);
        var b=ConsumableWindow.Button("Confirm",column,"",new Vector2(0,-338),new Vector2(416,78),null);v.confirm=b;v.buttonLabel=b.GetComponentInChildren<TMP_Text>();v.buttonLabel.font=font;v.buttonLabel.fontSharedMaterial=font.material;v.buttonLabel.fontSizeMax=34;
        v.goldIcon=Resources.Load<Sprite>("Consumables/CurrencyBag");
        // The mission phase uses the same authored layout with a large reward seal on the left.
        var seal=I("GoldSeal",v.card,-437,0,220,220,v.goldIcon,JanshinPanelTheme.Gold);seal.transform.SetSiblingIndex(1);
        PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/DevilContracts/MissionRewardView.prefab");UnityEngine.Object.DestroyImmediate(root);
    }
}
