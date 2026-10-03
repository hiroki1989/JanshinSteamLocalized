using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DevilContractSetup
{
    static TMP_FontAsset font;
    static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)=>ConsumableWindow.Rect(name,parent,pos,size);
    static TMP_Text Label(string name,Transform parent,string text,Vector2 pos,Vector2 size,float point,bool left=false)
    {
        var t=ConsumableWindow.Label(name,parent,text,pos,size,point);t.font=font;t.fontSharedMaterial=font?font.material:null;
        t.color=JanshinPanelTheme.Ivory;t.fontSizeMin=point*.75f;t.margin=new Vector4(3,2,3,2);
        if(left)t.alignment=TextAlignmentOptions.MidlineLeft;return t;
    }
    static Image Face(RectTransform rect)
    {
        var i=rect.gameObject.AddComponent<Image>();JanshinPanelTheme.Apply(i);return i;
    }
    static Button Button(string name,Transform parent,string text,Vector2 pos,Vector2 size)
    {
        var rt=Rect(name,parent,pos,size);var image=Face(rt);var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=image;
        Label("Label",rt,text,Vector2.zero,size-new Vector2(24,12),30);JanshinPanelTheme.Button(b);return b;
    }
    static GameObject CanvasRoot(string name,int order)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var c=go.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=order;
        var s=go.GetComponent<CanvasScaler>();s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;s.referenceResolution=new Vector2(1920,1080);s.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var shade=Rect("Backdrop",go.transform,Vector2.zero,new Vector2(10000,10000)).gameObject.AddComponent<Image>();shade.color=new Color(0,0,0,.84f);return go;
    }
    public static void RefreshPresentation()
    {
        Directory.CreateDirectory("Logs/DevilContracts");
        font=LocalizationManager.Instance.GetBodyFont()??TMP_Settings.defaultFontAsset;
        BuildView();
        DevilContractQA.Run();
        Debug.Log("Contract presentation verified");
    }
    [MenuItem("Tools/Janshin/Devil Contracts/Install and Verify")]
    public static void Run()
    {
        Directory.CreateDirectory("Logs/DevilContracts");
        font=Resources.Load<TMP_FontAsset>("Tutorial/Fonts/Japanese")??TMP_Settings.defaultFontAsset;
        var json=File.ReadAllText("Assets/Resources/DevilContracts/CatalogSource.json");
        var catalog=AssetDatabase.LoadAssetAtPath<DevilContractCatalog>("Assets/Resources/DevilContracts/Catalog.asset");
        if(!catalog){catalog=ScriptableObject.CreateInstance<DevilContractCatalog>();AssetDatabase.CreateAsset(catalog,"Assets/Resources/DevilContracts/Catalog.asset");}
        JsonUtility.FromJsonOverwrite(json,catalog);EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        BuildView();BuildNotice();InstallMenu();
        DevilContractQA.Run();
        Debug.Log("Devil contracts installed and verified");
    }
    public static void BuildTenRankView(){
        font=LocalizationManager.Instance.GetBodyFont()??TMP_Settings.defaultFontAsset;
        var catalog=AssetDatabase.LoadAssetAtPath<DevilContractCatalog>("Assets/Resources/DevilContracts/Catalog.asset");
        JsonUtility.FromJsonOverwrite(File.ReadAllText("Assets/Resources/DevilContracts/CatalogSource.json"),catalog);
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();BuildView();
    }
    public static void BuildView()
    {
        var go=CanvasRoot("DevilContracts",32000);var v=go.AddComponent<DevilContractView>();v.gameFont=font;
        var panel=Rect("ContractLedger",go.transform,Vector2.zero,new Vector2(1800,980));Face(panel);
        v.title=Label("Title",panel,"悪魔の契約",new Vector2(0,447),new Vector2(1300,70),48);
        v.subtitle=Label("Subtitle",panel,"",new Vector2(0,392),new Vector2(1600,40),24);
        v.gems=Label("Gems",panel,"",new Vector2(727,437),new Vector2(240,52),32);
        var left=Rect("ContractListFrame",panel,new Vector2(-650,-5),new Vector2(420,690));Face(left);
        var viewport=Rect("Viewport",left,Vector2.zero,new Vector2(400,668));viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
        var content=Rect("Contracts",viewport,Vector2.zero,new Vector2(390,11*88));content.anchorMin=content.anchorMax=content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;
        var scroll=left.gameObject.AddComponent<ScrollRect>();scroll.content=content;scroll.viewport=viewport;scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=50;scroll.movementType=ScrollRect.MovementType.Clamped;
        for(int i=0;i<11;i++){
            v.entries[i]=Button("Contract_"+i,content,"",new Vector2(0,-44-i*88),new Vector2(382,80));
            var entryRect=(RectTransform)v.entries[i].transform;entryRect.anchorMin=entryRect.anchorMax=new Vector2(.5f,1);entryRect.anchoredPosition=new Vector2(0,-44-i*88);
            v.entryLabels[i]=v.entries[i].GetComponentInChildren<TMP_Text>();v.entryLabels[i].fontSizeMax=30;v.entryLabels[i].fontSizeMin=18;
            v.entryLabels[i].rectTransform.anchoredPosition=new Vector2(27,0);v.entryLabels[i].rectTransform.sizeDelta=new Vector2(282,68);
            var icon=Rect("DevilIcon",v.entries[i].transform,new Vector2(-156,0),new Vector2(48,48)).gameObject.AddComponent<Image>();
            icon.sprite=DevilContractIcons.Get(i);icon.preserveAspect=true;icon.raycastTarget=false;v.entryIcons[i]=icon;
        }
        var details=Rect("SelectedContract",panel,new Vector2(235,0),new Vector2(1240,950));
        v.devilName=Label("Devil",details,"",new Vector2(0,307),new Vector2(1200,65),43);
        v.selectedIcon=Rect("DevilIcon",details,new Vector2(-310,307),new Vector2(62,62)).gameObject.AddComponent<Image>();
        v.selectedIcon.preserveAspect=true;v.selectedIcon.raycastTarget=false;
        v.devilName.rectTransform.anchoredPosition=new Vector2(40,307);v.devilName.rectTransform.sizeDelta=new Vector2(620,65);
        v.contractName=Label("ContractTitle",details,"",new Vector2(0,255),new Vector2(1160,45),28);
        var benefit=Rect("BenefitCard",details,new Vector2(-303,166),new Vector2(590,128));Face(benefit);
        v.benefit=Label("Benefit",benefit,"",Vector2.zero,new Vector2(546,116),27,true);
        var price=Rect("DrawbackCard",details,new Vector2(303,166),new Vector2(590,128));Face(price);
        v.drawback=Label("Drawback",price,"",Vector2.zero,new Vector2(546,116),27,true);
        v.synergy=Label("Synergy",details,"",new Vector2(0,71),new Vector2(1200,48),24);
        v.mercyTitle=Label("MercyPath",details,"",new Vector2(-392,20),new Vector2(380,44),28);
        v.powerTitle=Label("PowerPath",details,"",new Vector2(0,20),new Vector2(380,44),28);
        v.resonanceTitle=Label("ResonancePath",details,"",new Vector2(392,20),new Vector2(380,44),28);
        var tree=Rect("BranchingSkillTree",details,new Vector2(0,-199),new Vector2(1200,400));
        var treeViewport=Rect("Viewport",tree,Vector2.zero,new Vector2(1200,400));
        treeViewport.gameObject.AddComponent<Image>().color=Color.clear;treeViewport.gameObject.AddComponent<RectMask2D>();
        var treeContent=Rect("TreeNodes",treeViewport,Vector2.zero,new Vector2(1180,400));
        treeContent.anchorMin=treeContent.anchorMax=treeContent.pivot=new Vector2(.5f,1);treeContent.anchoredPosition=Vector2.zero;
        void Link(string name,float x,float y,float w,float h){var line=Rect(name,treeContent,new Vector2(x,y),new Vector2(w,h));line.anchorMin=line.anchorMax=new Vector2(.5f,1);line.anchoredPosition=new Vector2(x,y);var image=line.gameObject.AddComponent<Image>();image.color=JanshinPanelTheme.Gold;image.raycastTarget=false;}
        Link("SharedContractRoot",0,-3,784,2);Link("RootStem",0,-1,2,2);
        // Five pairs per path keep every icon on screen. Lines follow each path in rank order.
        for(int branch=0;branch<3;branch++){
            float center=(branch-1)*392;
            Link("RootBranch_"+branch,center,-23,2,40);Link("RootTurn_"+branch,center-40,-43,80,2);
            for(int row=0;row<5;row++){
                float y=-43-row*78;Link("Pair_"+branch+"_"+row,center,y,160,2);
                if(row<4)Link("NextPair_"+branch+"_"+row,center+(row%2==0?80:-80),y-39,2,78);
            }
        }
        for(int i=0;i<30;i++){
            int depth=i%10;float x=(i/10-1)*392+((depth%2==depth/2%2)?-80:80);float y=-43-(depth/2)*78;
            var b=Button("Node_"+i,treeContent,"",new Vector2(x,y),new Vector2(70,70));v.nodes[i]=b;
            var rt=(RectTransform)b.transform;rt.anchorMin=rt.anchorMax=new Vector2(.5f,1);rt.anchoredPosition=new Vector2(x,y);
            UnityEngine.Object.DestroyImmediate(b.GetComponentInChildren<TMP_Text>().gameObject);
            v.nodeTitles[i]=Label("NodeTitle",b.transform,"",new Vector2(0,63),new Vector2(344,32),24,true);
            v.nodeBodies[i]=Label("Effect",b.transform,"",new Vector2(0,-1),new Vector2(344,94),22,true);v.nodeBodies[i].fontSizeMin=12;
            v.nodeStates[i]=Label("StateAndCost",b.transform,"",new Vector2(0,-66),new Vector2(344,30),20,true);
            v.nodeTitles[i].gameObject.SetActive(false);v.nodeBodies[i].gameObject.SetActive(false);v.nodeStates[i].gameObject.SetActive(false);
            var symbol=Rect("NodeSymbol",b.transform,Vector2.zero,new Vector2(44,44)).gameObject.AddComponent<Image>();symbol.sprite=DevilContractIcons.Branch(i/10);symbol.preserveAspect=true;symbol.raycastTarget=false;v.nodeIcons[i]=symbol;
        }
        v.status=Label("EquippedStatus",panel,"",new Vector2(-650,-385),new Vector2(400,45),24);
        v.equip=Button("SignContract",panel,"",new Vector2(380,-439),new Vector2(360,65));
        v.unequip=Button("UnequipContract",panel,"",new Vector2(-30,-439),new Vector2(320,65));
        v.close=Button("Back",panel,"",new Vector2(-650,-439),new Vector2(320,65));
        v.footer=Label("Rules",panel,"",new Vector2(196,352),new Vector2(1260,40),18);
        var confirm=Rect("ConfirmResearch",go.transform,Vector2.zero,new Vector2(10000,10000));confirm.gameObject.AddComponent<Image>().color=new Color(0,0,0,.8f);v.confirmRoot=confirm.gameObject;
        var card=Rect("ConfirmationCard",confirm,Vector2.zero,new Vector2(960,590));Face(card);
        v.confirmBody=Label("Confirmation",card,"",new Vector2(0,88),new Vector2(840,328),32);v.confirmBody.fontSizeMin=22;
        v.confirmGem=Rect("GemIcon",card,new Vector2(-43,-120),new Vector2(44,44)).gameObject.AddComponent<Image>();v.confirmGem.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Dark UI/New Icons/White Gem 2.png");v.confirmGem.preserveAspect=true;v.confirmGem.raycastTarget=false;
        v.confirmCost=Label("GemCost",card,"",new Vector2(43,-120),new Vector2(106,48),32);v.confirmCost.alignment=TextAlignmentOptions.Left;
        v.confirmYes=Button("Unlock",card,"",new Vector2(205,-202),new Vector2(350,76));v.confirmNo=Button("Cancel",card,"",new Vector2(-205,-202),new Vector2(350,76));
        confirm.gameObject.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(go,"Assets/Resources/DevilContracts/ContractView.prefab");UnityEngine.Object.DestroyImmediate(go);
    }
    static void BuildNotice()
    {
        var go=CanvasRoot("CerberusPassiveReveal",32100);var view=go.AddComponent<DevilContractNotice>();view.group=go.AddComponent<CanvasGroup>();
        var shade=go.transform.Find("Backdrop").GetComponent<Image>();shade.color=new Color(0,0,0,.48f);
        view.card=Rect("BlessingCard",go.transform,new Vector2(0,45),new Vector2(1000,310));Face(view.card);
        view.title=Label("Title",view.card,"",new Vector2(0,87),new Vector2(920,62),38);view.title.color=JanshinPanelTheme.Gold;
        view.body=Label("TemporaryPassive",view.card,"",new Vector2(0,-28),new Vector2(920,140),38);
        PrefabUtility.SaveAsPrefabAsset(go,"Assets/Resources/DevilContracts/PassiveNotice.prefab");UnityEngine.Object.DestroyImmediate(go);
    }
    static void InstallMenu()
    {
        string path="Assets/Scenes/MenuScene.unity";string backup="Logs/DevilContracts/Backup/MenuScene.unity";
        if(!File.Exists(backup))File.Copy(path,backup);
        var scene=EditorSceneManager.OpenScene(path);
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var old=all.FirstOrDefault(t=>t.name=="Button_DevilContracts");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
        var source=all.First(t=>t.name=="Button_Equip").GetComponent<Button>();
        var canvas=source.GetComponentInParent<Canvas>();
        var b=Button("Button_DevilContracts",canvas.transform,"悪魔の契約",new Vector2(757,-196),new Vector2(344,82));
        b.gameObject.AddComponent<MenuDevilContractButton>();
        b.transform.SetSiblingIndex(source.transform.parent==canvas.transform ? source.transform.GetSiblingIndex()+1 : 1);
        var label=b.GetComponentInChildren<TMP_Text>();var srcText=source.GetComponentInChildren<TMP_Text>();if(srcText){label.font=srcText.font;label.fontSharedMaterial=srcText.fontSharedMaterial;}label.fontSizeMax=32;label.fontSizeMin=24;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        BattleHUDQA.Capture("Logs/DevilContracts/Menu.png",1920,1080);
    }
}
