using System;
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

internal static class TutorialCommerceSetup
{
    const string Request = "Temp/TutorialQA/commerce-request";
    const string ShopPath = "Assets/Scenes/ShopScene.unity";
    const string MenuPath = "Assets/Scenes/MenuScene.unity";
    const string PrefabPath = "Assets/Resources/Commerce/IAPShop.prefab";
    [InitializeOnLoadMethod] static void Schedule() {
        if (File.Exists(Request)) EditorApplication.delayCall += Run;
    }
    [MenuItem("Tools/Janshin/Apply Tutorial and Commerce Setup")]
    static void Run() {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Run; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try {
            foreach (string path in new[] {ShopPath, MenuPath}) {
                var current = SceneManager.GetSceneByPath(path);
                if (current.IsValid() && current.isLoaded && current.isDirty) throw new Exception(path + " has unsaved edits.");
            }
            Tutorial();
            Directory.CreateDirectory("Assets/Resources/Commerce"); AssetDatabase.Refresh();
            CreateShopPrefab();
            EditScene(MenuPath, scene => {
                var roots = scene.GetRootGameObjects();
                var services = roots.SelectMany(r=>r.GetComponentsInChildren<AdsInitializer>(true)).Single();
                if (!AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Commerce/Services.prefab"))
                    PrefabUtility.SaveAsPrefabAsset(services.gameObject, "Assets/Resources/Commerce/Services.prefab");
                var reward = roots.SelectMany(r=>r.GetComponentsInChildren<MenuRewardedAdButton>(true)).Single();
                var so = new SerializedObject(reward);
                var button = (Button)so.FindProperty("rewardButton").objectReferenceValue;
                if (!so.FindProperty("statusLabel").objectReferenceValue) {
                    var status = Text("RewardStatus", button.transform, new Vector2(0,-60), new Vector2(440,52), 18);
                    so.FindProperty("statusLabel").objectReferenceValue = status;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            });
            EditScene(MenuPath, scene => {
                var canvas = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).First();
                if (!scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<IAPShopPanel>(true)).Any()) {
                    var overlay = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                    var host = new GameObject("Commerce"); SceneManager.MoveGameObjectToScene(host, scene);
                    host.AddComponent<CommerceBootstrap>();
                    var panel = host.AddComponent<IAPShopPanel>();
                    var entry = Button("OpenGemShop", canvas.transform, new Vector2(0,135), new Vector2(640,70));
                    var rect = (RectTransform)entry.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f,0);
                    Bind(panel, "shopPanelRoot", overlay);
                    Bind(panel, "openShopButton", entry);
                    Bind(panel, "gemCountTMP", scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TMP_Text>(true)).FirstOrDefault(t=>t.name=="gemsTMP"));
                    string[] buttons = {"closeShopButton","removeAdsButton","buyGems100Button","buyGems500Button","buyGems1200Button","restorePurchasesButton","retryButton","privacyButton"};
                    string[] names = {"Close","RemoveAds","Gems10","Gems55","Gems130","Restore","Retry","Privacy"};
                    for(int i=0;i<names.Length;i++) Bind(panel,buttons[i],overlay.GetComponentsInChildren<Button>(true).First(b=>b.name==names[i]));
                    string[] labels = {"titleLabel","descriptionLabel","statusLabel","removeAdsBadge","currentGemsLabel"};
                    string[] textNames = {"Title","Description","Status","PurchasedBadge","Balance"};
                    for(int i=0;i<labels.Length;i++) Bind(panel,labels[i],overlay.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name==textNames[i]));
                    for(int i=1;i<=4;i++) Bind(panel,new[]{"removeAdsLabel","gems100Label","gems500Label","gems1200Label"}[i-1],
                        overlay.GetComponentsInChildren<Button>(true).First(b=>b.name==names[i]).GetComponentInChildren<TMP_Text>(true));
                    panel.RefreshUI(); overlay.SetActive(false);
                }
            });
            // The v8.7.0 settings asset is missing from the project. Supply only Google test IDs.
            var settingsType = AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings")).FirstOrDefault(t=>t!=null);
            const string settingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(settingsPath);
            if (!settings) { Directory.CreateDirectory("Assets/GoogleMobileAds/Resources"); settings = ScriptableObject.CreateInstance(settingsType); AssetDatabase.CreateAsset(settings, settingsPath); }
            var settingsSO = new SerializedObject(settings);
            if (string.IsNullOrEmpty(settingsSO.FindProperty("adMobIOSAppId").stringValue))
                settingsSO.FindProperty("adMobIOSAppId").stringValue = "ca-app-pub-3940256099942544~1458002511";
            settingsSO.FindProperty("delayAppMeasurementInit").boolValue = true;
            settingsSO.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(settings);
            File.WriteAllText("Temp/TutorialQA/commerce-setup-results.txt","SUCCESS");
            typeof(FirstMatchTutorialQA).GetMethod("Run",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            CommerceQA.Run();
        } catch(Exception e) { File.WriteAllText("Temp/TutorialQA/commerce-setup-results.txt","FAIL "+e); Debug.LogException(e); }
    }
    static void Tutorial() {
        var content = AssetDatabase.LoadAssetAtPath<FirstMatchTutorialContent>("Assets/Resources/Tutorial/FirstMatchTutorialContent.asset");
        var step = content.pages.Single(p=>p.title.japanese=="パッシブスキル");
        string original = step.body.japanese + step.hint.japanese;
        step.compactBody = true;
        step.body.english = "Passives activate when you win with a matching yaku. Each character has different yaku. Only some are unlocked at first; spend Gold to add or level them up. Equipping special tiles also strengthens passives.\n\n<sprite=0> : A matching win adds a percentage damage bonus.\n<sprite=1> : A matching win restores HP equal to a percentage of the base score.\n<sprite=2> : A matching win restores MP equal to a percentage of the base score.";
        step.body.chineseSimplified = "以对应的役和牌时，被动技能就会生效。对应的役因角色而异。起初只解锁部分役，可消耗Gold追加或升级。获得并装备特殊牌也能强化被动技能。\n\n<sprite=0> ：以对应的役和牌时，按一定比例增加对敌伤害。\n<sprite=1> ：以对应的役和牌时，回复相当于基础点数一定比例的HP。\n<sprite=2> ：以对应的役和牌时，回复相当于基础点数一定比例的MP。";
        step.hint.english = "Adapt your target yaku to the situation, whether you need heavy damage against the gods or your HP is running low.";
        step.hint.chineseSimplified = "想对诸神造成大量伤害时，或自身HP所剩无几时，灵活调整目标役吧。";
        if (step.body.japanese + step.hint.japanese != original) throw new Exception("Japanese changed");
        AddPage(content,"お守り","Omamori","御守",
            "お守りは、装備することで能力を強化するアイテムです。最大HPの増加や被ダメージの軽減など、お守りごとに異なる効果があります。\n\n神々を倒して報酬のお守りを手に入れたら、装備画面で効果を確認して装備しましょう。キャラクターや狙う役に合った組み合わせを選ぶことが大切です。",
            "Omamori are items that strengthen you when equipped. Each has its own effects, such as increasing maximum HP or reducing incoming damage.\n\nAfter defeating the gods and earning an Omamori, check its effects and equip it on the equipment screen. Choose a combination that suits your character and target yaku.",
            "御守是装备后可强化能力的道具。不同御守有不同效果，例如提升HP上限或减少所受伤害。\n\n击败诸神并获得御守奖励后，在装备界面确认效果并装备。选择适合角色及目标役的组合吧。",
            "持っているだけでは効果は発揮されません。対局前に装備を確認しましょう。",
            "Owning an Omamori is not enough. Check your equipment before the match.",
            "仅持有御守不会生效。对局前请确认装备。");
        AddPage(content,"お札","Ofuda","符札",
            "お札は、挑戦中に手に入る強化アイテムです。対局の合間の強化画面で購入でき、和了時のダメージやHP・MPの回復などを助けます。\n\n持てるお札は最大3枚です。所持枠と効果を確認し、今の手作りや戦い方に合うものを選びましょう。お札の効果はその挑戦中に有効です。",
            "Ofuda are upgrades obtained during a run. Buy them on the upgrade screen between matches to help with damage or HP and MP recovery when you win.\n\nYou can hold up to three Ofuda. Check your slots and their effects, then choose ones that suit your hands and strategy. Their effects last for the current run.",
            "符札是在挑战途中获得的强化道具。可在对局之间的强化界面购买，帮助提升和牌伤害或回复HP、MP等。\n\n最多可持有3张符札。确认持有栏位及效果，选择适合当前牌型与战术的符札。效果在本次挑战中有效。",
            "所持中のお札は対局画面で確認できます。お守りやスキルとの相性も考えましょう。",
            "Check your Ofuda on the match screen, and consider how they work with your Omamori and skills.",
            "可在对局界面查看持有的符札，也要考虑与御守、技能的搭配。");
        EditorUtility.SetDirty(content); AssetDatabase.SaveAssetIfDirty(content);
    }
    static void AddPage(FirstMatchTutorialContent c,string ja,string en,string zh,string bodyJa,string bodyEn,string bodyZh,string hintJa,string hintEn,string hintZh) {
        if(c.pages.Any(p=>p.title.japanese==ja)) return;
        c.pages.Insert(c.pages.Count-1,new FirstMatchTutorialContent.Step {
            editorLabel=ja,title=new FirstMatchTutorialContent.Localized(ja,en,zh),
            body=new FirstMatchTutorialContent.Localized(bodyJa,bodyEn,bodyZh),
            hint=new FirstMatchTutorialContent.Localized(hintJa,hintEn,hintZh)
        });
    }
    static void EditScene(string path,Action<Scene> edit) {
        var scene=SceneManager.GetSceneByPath(path); bool opened=!scene.IsValid()||!scene.isLoaded;
        var previous=SceneManager.GetActiveScene();
        if(opened) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try { edit(scene); EditorSceneManager.MarkSceneDirty(scene); if(!EditorSceneManager.SaveScene(scene)) throw new Exception("Save failed: "+path); }
        finally { if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous); if(opened) EditorSceneManager.CloseScene(scene,true); }
    }
    static void Bind(Object target,string name,Object value) {
        var so=new SerializedObject(target); so.FindProperty(name).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size) {
        var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent,false); rt.gameObject.layer=5;
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f); rt.anchoredPosition=pos; rt.sizeDelta=size; return rt;
    }
    static TextMeshProUGUI Text(string name,Transform parent,Vector2 pos,Vector2 size,float fontSize) {
        var text=Rect(name,parent,pos,size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font=TMP_Settings.defaultFontAsset; text.fontSize=fontSize;
        text.enableAutoSizing=true; text.fontSizeMin=18; text.fontSizeMax=fontSize;
        text.color=Color.white; text.alignment=TextAlignmentOptions.Center; text.raycastTarget=false; return text;
    }
    static Button Button(string name,Transform parent,Vector2 pos,Vector2 size) {
        var rt=Rect(name,parent,pos,size); var image=rt.gameObject.AddComponent<Image>(); image.color=new Color(.19f,.28f,.37f);
        var button=rt.gameObject.AddComponent<Button>(); button.targetGraphic=image;
        Text("Label",rt,Vector2.zero,size-new Vector2(24,8),26); return button;
    }
    static void NormalizeShopRoot(GameObject root)
    {
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rect = root.GetComponent<RectTransform>();
        rect.localScale = Vector3.one; rect.sizeDelta = new Vector2(1600,900); rect.pivot = new Vector2(.5f,.5f);
        var initial = new System.Collections.Generic.Dictionary<string,string> {
            {"Title","宝石・広告カット"},{"Description","広告カットは自動表示の全画面広告を停止します。任意のリワード広告は引き続き利用できます。"},
            {"Balance","所持宝石：0"},{"PurchasedBadge","広告カット購入済み"},{"Status","ストアの準備中です。購入はまだ利用できません。"}
        };
        var buttons = new System.Collections.Generic.Dictionary<string,string> {
            {"RemoveAds","広告カット　—"},{"Gems10","宝石 ×10　—"},{"Gems55","宝石 ×55　—"},{"Gems130","宝石 ×130　—"},
            {"Restore","購入を復元"},{"Retry","ストアへ再接続"},{"Privacy","広告のプライバシー設定"},{"Close","閉じる"}
        };
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true)) {
            if(!string.IsNullOrEmpty(text.text)) continue;
            if(initial.TryGetValue(text.name,out var content)) text.text=content;
            else if(text.name=="Label" && buttons.TryGetValue(text.transform.parent.name,out var label)) text.text=label;
        }
    }

    static void CreateShopPrefab() {
        if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) {
            var existing=PrefabUtility.LoadPrefabContents(PrefabPath);
            try { NormalizeShopRoot(existing); PrefabUtility.SaveAsPrefabAsset(existing,PrefabPath); } finally { PrefabUtility.UnloadPrefabContents(existing); } return;
        }
        var root=new GameObject("IAPShop",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        try {
            var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=32000;
            var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900); scaler.matchWidthOrHeight=.5f;
            var shade=root.AddComponent<Image>(); shade.color=new Color(0,0,0,.86f);
            var card=Rect("ShopCard",root.transform,Vector2.zero,new Vector2(780,780));
            card.gameObject.AddComponent<Image>().color=new Color(.045f,.075f,.095f);
            Text("Title",card,new Vector2(0,338),new Vector2(700,50),36);
            Text("Description",card,new Vector2(0,276),new Vector2(680,65),22);
            Text("Balance",card,new Vector2(0,218),new Vector2(680,38),27).color=new Color(1,.8f,.35f);
            Button("RemoveAds",card,new Vector2(0,150),new Vector2(680,58));
            Text("PurchasedBadge",card,new Vector2(0,104),new Vector2(680,24),19).color=new Color(.5f,1,.6f);
            Button("Gems10",card,new Vector2(0,53),new Vector2(680,58));
            Button("Gems55",card,new Vector2(0,-15),new Vector2(680,58));
            Button("Gems130",card,new Vector2(0,-83),new Vector2(680,58));
            Button("Restore",card,new Vector2(-175,-148),new Vector2(330,46));
            Button("Retry",card,new Vector2(175,-148),new Vector2(330,46));
            Button("Privacy",card,new Vector2(0,-204),new Vector2(680,42));
            Text("Status",card,new Vector2(0,-268),new Vector2(680,60),21);
            Button("Close",card,new Vector2(0,-343),new Vector2(280,52));
            NormalizeShopRoot(root); PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        } finally { Object.DestroyImmediate(root); }
    }
}
