using System;
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
using Object=UnityEngine.Object;

internal static class ConsumablesQA
{
    const string Folder="Logs/ConsumablesQA";
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static readonly List<string> results=new();
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
    static T Find<T>(Scene s)where T:Component=>s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).First();
    static void Check(bool ok,string label){results.Add((ok?"PASS ":"FAIL ")+label);}
    [InitializeOnLoadMethod]static void Watch(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
    static void Poll(){if(File.Exists(Folder+"/request")&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode){File.Delete(Folder+"/request");Run();}}
    [MenuItem("Tools/Janshin/Validate Consumables")]
    public static void Run()
    {
        Directory.CreateDirectory(Folder);results.Clear();
        bool had=PlayerPrefs.HasKey("RunConsumablesV1"),hadGold=PlayerPrefs.HasKey("RunGold");
        string previous=PlayerPrefs.GetString("RunConsumablesV1");int gold=GameManager.RunCurrency.Get();float speed=Time.timeScale;var random=UnityEngine.Random.state;
        try
        {
            Check(RunConsumables.All.Length==20&&RunConsumables.All.Select(d=>d.id).Distinct().Count()==20,"20 distinct item definitions");
            foreach(var d in RunConsumables.All){Check(new[]{300,500,700}.Contains(d.price),"Price "+d.id);Check(d.Icon,"Sprite "+d.id);}
            var rng=new System.Random(9);
            for(int i=0;i<100;i++){var offers=RunConsumables.RollOffers(rng);if(offers.Length!=3||offers.Distinct().Count()!=3)throw new Exception("Duplicate offers");}
            Check(true,"100 shop entries each offer 3 different items");
            var state=new RunConsumables.State{shield=true};
            Check(RunConsumables.IncomingDamage(state,1000,5000,false)==1000&&state.shield,"Scale does not affect enemy skills or get consumed by them");
            Check(RunConsumables.IncomingDamage(state,1000,5000,true)==500&&!state.shield,"Scale halves one enemy win");
            Check(RunConsumables.IncomingDamage(state,1000,5000,true)==1000,"Scale is consumed only once");
            state=new RunConsumables.State{effigy=true,bloodPact=true};
            Check(RunConsumables.IncomingDamage(state,1000,500,false)==499&&!state.effigy,"Effigy survives lethal boosted damage at HP1");
            state=new RunConsumables.State{bag=new List<int>{1,12},shield=true,enemySeal=5};RunConsumables.Save(state);RunConsumables.EndBattle();
            Check(RunConsumables.Load().bag.Count==2&&!RunConsumables.Load().shield&&RunConsumables.Load().enemySeal==0,"Victory keeps inventory and clears opponent effects");
            RunConsumables.ResetRun();Check(RunConsumables.Load().bag.Count==0,"New run / defeat clears inventory");
            CheckStore();CheckBattle();
        }
        catch(Exception e){results.Add("FAIL "+e);}
        finally
        {
            if(had)PlayerPrefs.SetString("RunConsumablesV1",previous);else PlayerPrefs.DeleteKey("RunConsumablesV1");
            if(hadGold)GameManager.RunCurrency.Set(gold);else PlayerPrefs.DeleteKey("RunGold");
            PlayerPrefs.Save();Time.timeScale=speed;UnityEngine.Random.state=random;File.WriteAllLines(Folder+"/results.txt",results);
        }
    }
    static void CheckStore()
    {
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/UpgradeScene.unity");
        try
        {
            var menu=Find<UpgradeSceneMenu>(scene);
            foreach(var field in new[]{"ofudaStoreRoot","gemResultPanelRoot","uniqueOmamoriResultPanelRoot"})if(Get(menu,field)is GameObject g)g.SetActive(false);
            var manager=(UpgradeManager)Get(menu,"upgradeManager");if(manager)manager.gameObject.SetActive(false);
            ((GameObject)Get(menu,"menuRoot")).SetActive(true);
            Call(menu,"BuildConsumableStore");Render(scene,"UpgradeMenu");
            ((GameObject)Get(menu,"menuRoot")).SetActive(false);
            GameManager.RunCurrency.Set(2000);
            foreach(var mode in new[]{UpgradeManager.UpgradeSectionMode.StatusOnly,UpgradeManager.UpgradeSectionMode.DeckOnly,UpgradeManager.UpgradeSectionMode.TraitOnly}){
                manager.gameObject.SetActive(true);manager.ApplySectionMode(mode);
                if(mode==UpgradeManager.UpgradeSectionMode.TraitOnly){var sample=(TMP_Text)Get(manager,"traitUpgradeOfferTMP");if(sample)sample.text="断么九\nLv.1（10%） → Lv.2（20%）\n500";}
                Render(scene,"Upgrade-"+mode);
            }
            manager.ForceCloseAllSectionRoots();manager.gameObject.SetActive(false);
            var ofuda=(GameObject)Get(menu,"ofudaStoreRoot");ofuda.SetActive(true);var store=Find<UpgradeOfudaStore>(scene);Call(store,"RebuildCatalogLocalized");Call(store,"BuildOffers");Call(store,"RefreshEquippedOfudaUI");
            Set(store,"_offering",new List<OfudaDef>{new OfudaDef{id="preview1",rarity="コモン",displayName="コモン\n和了時にHPを5%回復",price=300},new OfudaDef{id="preview2",rarity="レア",displayName="レア\n満貫以上でダメージ10%増加",price=500},new OfudaDef{id="preview3",rarity="レジェンダリー",displayName="レジェンダリー\n和了時にMPを20%回復",price=700}});
            Call(store,"RefreshOfferSlotsUI");
            foreach(var owned in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<UpgradeOfudaSlotsPanel>(true)))owned.RefreshUI();
            UpgradePanelPresentation.Ofuda(ofuda);Render(scene,"Upgrade-Ofuda");
            Set(store,"_offering",new List<OfudaDef>());
            Call(store,"RefreshOfferSlotsUI");
            ofuda.SetActive(false);ofuda.SetActive(true);UpgradePanelPresentation.Ofuda(ofuda);
            foreach(var button in ofuda.transform.Find("OfferSlotsRow").GetComponentsInChildren<Button>(true)){
                var face=button.GetComponent<Image>();
                Check(face && face.enabled && face.type==Image.Type.Simple && !face.preserveAspect && face.color.a==1f,"Empty ofuda offer uses an opaque rect-sized background");
                var frame=button.transform.Find("PresentationFrame")as RectTransform;
                Check(frame && frame.sizeDelta==((RectTransform)button.transform).sizeDelta,"Ofuda frame matches background bounds after reopening");
            }
            Render(scene,"Upgrade-Ofuda-Empty");ofuda.SetActive(false);
            ((GameObject)Get(menu,"menuRoot")).SetActive(true);
            RunConsumables.ResetRun();GameManager.RunCurrency.Set(2000);
            Call(menu,"OpenConsumableStore");Set(menu,"consumableSelected",0);Call(menu,"RefreshConsumableOffers");
            var offers=(int[])Get(menu,"consumableOffers");int price=RunConsumables.Get(offers[0]).price;
            Render(scene,"ItemShop");Call(menu,"BuyConsumable");
            Check(GameManager.RunCurrency.Get()==2000-price&&RunConsumables.Load().bag.SequenceEqual(new[]{offers[0]}),"Purchase charges correct Gold and grants item");
            Call(menu,"BuyConsumable");Check(GameManager.RunCurrency.Get()==2000-price&&RunConsumables.Load().bag.Count==1,"Same offer cannot be purchased twice");
            RunConsumables.Save(new RunConsumables.State{bag=new List<int>{1,2,3,4}});Set(menu,"consumableSelected",1);Call(menu,"BuyConsumable");
            Check(RunConsumables.Load().bag.Count==4&&GameManager.RunCurrency.Get()==2000-price,"Full bag cannot spend Gold");
            RunConsumables.ResetRun();GameManager.RunCurrency.Set(0);Call(menu,"BuyConsumable");Check(RunConsumables.Load().bag.Count==0,"Insufficient Gold cannot grant an item");
            ((ConsumableWindow)Get(menu,"consumableStore")).Close();
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    static void CheckBattle()
    {
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/RunScene.unity");
        try
        {
            var gm=Find<GameManager>(scene);
            Set(gm,"playerHP",5000);Set(gm,"playerMaxHP",10000);Set(gm,"enemyHP",10000);
            var phase=gm.GetType().GetField("phase",F);phase.SetValue(gm,Enum.Parse(phase.FieldType,"Offer"));
            var offers=(List<string>)Get(gm,"offers");offers.Clear();offers.AddRange(new[]{"Man1","Man2","Pin3","Sou4"});
            var hand=(List<string>)Get(gm,"hand");hand.Clear();hand.AddRange(Enumerable.Repeat("Man5",13));
            CheckEffects(gm);
            RunConsumables.Save(new RunConsumables.State{bag=new List<int>{1,12,13,17},enemySeal=5});
            for(int i=0;i<6;i++){Call(gm,"ConsumablesOnEnemyTurn");Check((bool)Get(gm,"_consumableSealThisEnemyTurn")== (i<5),"Enemy seal turn "+(i+1));}
            RunConsumables.Save(new RunConsumables.State{bag=new List<int>{1,12,13,17},usedThisTurn=true,castsBonus=1,regeneration=4});
            Call(gm,"ConsumablesOnPlayerTurn");var state=RunConsumables.Load();
            Check(!state.usedThisTurn&&state.castsBonus==0&&state.regeneration==3&&(int)Get(gm,"playerHP")==5800,"Next draw resets per-turn use and applies regeneration");
            Call(gm,"EnsureConsumableButton");Render(scene,"BattleButton");
            Call(gm,"OpenConsumableInventory");var w=(ConsumableWindow)Get(gm,"_consumableWindow");Check(w,"Inventory opens on player turn");
            if(w){Set(gm,"_consumableSlot",3);Set(gm,"_consumableHand",0);Call(gm,"RefreshConsumableInventory");Render(scene,"InventoryTargets");w.Close();}
            var snapshotType=typeof(GameManager).GetNestedType("SuspendSnapshot",BindingFlags.NonPublic);
            object snap=Activator.CreateInstance(snapshotType,true);
            var saved=new RunConsumables.State{bag=new List<int>{12,13},shield=true,enemySeal=3,usedThisTurn=true,castsBonus=1};
            snapshotType.GetField("consumables").SetValue(snap,saved);
            var restored=(RunConsumables.State)snapshotType.GetField("consumables").GetValue(JsonUtility.FromJson(JsonUtility.ToJson(snap),snapshotType));
            Check(restored.shield&&restored.enemySeal==3&&restored.usedThisTurn&&restored.castsBonus==1&&restored.bag.SequenceEqual(saved.bag),"Suspend snapshot preserves inventory, buffs, and turn usage");
            CheckCatalog(scene,gm.transform);
            Set(gm,"_preparedForSceneUnload",true);
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    static void CheckCatalog(Scene scene,Transform parent)
    {
        var lm=LocalizationManager.Instance;var language=lm.CurrentLanguage;
        try
        {
            foreach(var lang in new[]{LocalizationManager.Language.Japanese,LocalizationManager.Language.English,LocalizationManager.Language.ChineseSimplified})
            {
                Set(lm,"currentLanguage",lang);
                var w=ConsumableWindow.Open(parent,ConsumableWindow.T("アイテム図鑑","Consumables","道具图鉴"));
                try
                {
                    for(int i=0;i<RunConsumables.All.Length;i++)
                    {
                        var d=RunConsumables.All[i];w.Items(RunConsumables.All.Skip(i/4*4).Take(4).Select(x=>x.id).ToArray(),_=>{},true,null,i%4);
                        w.Detail.text=d.Name+"\n"+d.Description;Canvas.ForceUpdateCanvases();
                        foreach(var label in w.GetComponentsInChildren<TMP_Text>()){label.ForceMeshUpdate(true);Check(!label.isTextOverflowing,"Catalog "+lang+" "+d.id+" fits "+label.name);}
                        if(i%4==0&&lang==LocalizationManager.Language.Japanese)Render(scene,"Catalog"+(i/4+1));
                    }
                    if(lang!=LocalizationManager.Language.Japanese)Render(scene,"Catalog-"+lang);
                }finally{w.Close();}
            }
        }finally{Set(lm,"currentLanguage",language);}
    }
    static void CheckEffects(GameManager gm)
    {
        var state=new RunConsumables.State();
        Set(gm,"playerHP",1000);Set(gm,"playerMaxHP",10000);Call(gm,"ApplyConsumableEffect",1,state);Check((int)Get(gm,"playerHP")==4000,"Crimson Dew restores 30% max HP");
        Set(gm,"_mp",0);int maxMp=(int)Call(gm,"EffectiveMaxMP");Call(gm,"ApplyConsumableEffect",2,state);Check((int)Get(gm,"_mp")==Mathf.CeilToInt(maxMp*.4f),"Moon spring restores 40% max MP");
        Set(gm,"_mp",0);Set(gm,"playerHP",1000);Call(gm,"ApplyConsumableEffect",3,state);Check((int)Get(gm,"playerHP")==3000&&(int)Get(gm,"_mp")==Mathf.CeilToInt(maxMp*.2f),"Amber nectar restores both resources");
        Set(gm,"playerHP",1000);Set(gm,"_mp",0);Call(gm,"ApplyConsumableEffect",4,state);Check((int)Get(gm,"playerHP")==800&&(int)Get(gm,"_mp")==Mathf.CeilToInt(maxMp*.6f),"Chalice exchanges current HP for MP");
        Set(gm,"playerHP",1000);Call(gm,"ApplyConsumableEffect",5,state);Check((int)Get(gm,"playerHP")==1800&&state.regeneration==4,"Elixir heals now and schedules 4 heals");
        foreach(int id in new[]{6,7,8,9,10,12,13,15,20})Call(gm,"ApplyConsumableEffect",id,state);
        Check(state.geki&&state.shun&&state.iyu&&state.castsBonus==1&&state.freeCast&&state.enemySeal==5&&state.shield&&state.effigy&&state.bloodPact,"All 9 buff items activate their own effects");
        RunConsumables.Save(state);Check(RunConsumables.TraitBonus(SkillSetAsset.Trait.Geki)==1&&RunConsumables.TraitBonus(SkillSetAsset.Trait.Shun)==1&&RunConsumables.TraitBonus(SkillSetAsset.Trait.Iyu)==1,"Three passive categories receive their temporary level bonus");
        Check((int)Call(gm,"ComputeFinalSkillMpCost",100)==0,"Free cast overrides MP cost");Call(gm,"ConsumablesConsumeFreeCast");Check(!RunConsumables.Load().freeCast,"Successful skill consumes free cast");
        Set(gm,"_currentScoringAttackerIsPlayer",true);Check((int)Call(gm,"ConsumablesModifyOutgoingWin",1000)==1500&&!RunConsumables.Load().bloodPact,"Blood pact boosts exactly one player win");
        Set(gm,"enemyHP",5000);Call(gm,"ApplyConsumableEffect",11,state);Check((int)Get(gm,"enemyHP")==4000,"Thunder wedge deals fixed 1000 damage");
        Set(gm,"_enemySkillPoisonTurnRemaining",3);Set(gm,"_enemySkillParalysisTurnRemaining",3);Call(gm,"ApplyConsumableEffect",14,state);Check((int)Get(gm,"_enemySkillPoisonTurnRemaining")==0&&(int)Get(gm,"_enemySkillParalysisTurnRemaining")==0,"Silver bell clears current poison and paralysis");
        var deck=(Stack<string>)Get(gm,"deck");deck.Clear();foreach(var tile in new[]{"Man9","Pin8","Sou7","East","West","Red"})deck.Push(tile);
        var offers=(List<string>)Get(gm,"offers");var pool=deck.Concat(offers).OrderBy(t=>t).ToArray();Call(gm,"ApplyConsumableEffect",16,state);Check(offers.Count==4&&deck.Concat(offers).OrderBy(t=>t).SequenceEqual(pool),"Compass redraw preserves the tile pool and offer count");
        var hand=(List<string>)Get(gm,"hand");Set(gm,"_consumableHand",0);Set(gm,"_consumableReplacement","Sou5");Call(gm,"ApplyConsumableEffect",17,state);Check(hand[0]=="Sou5","Brush applies chosen suit");
        Set(gm,"_consumableReplacement","Sou6");Call(gm,"ApplyConsumableEffect",18,state);Check(hand[0]=="Sou6","Thread applies chosen neighboring rank");
        var discards=(List<string>)Get(gm,"discards");discards.Clear();discards.Add("Pin2");Set(gm,"_consumableDiscard",0);Call(gm,"ApplyConsumableEffect",19,state);Check(hand[0]=="Pin2"&&discards[0]=="Sou6"&&(bool)Get(gm,"suppressTsumoThisOffer"),"Recall swaps tiles without creating a tsumo draw");
        Set(gm,"playerHP",5000);Set(gm,"enemyHP",10000);hand[0]="Man5";
    }
    static void Render(Scene scene,string name)
    {
        var go=new GameObject("ConsumableQA Camera");SceneManager.MoveGameObjectToScene(go,scene);var camera=go.AddComponent<Camera>();
        camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<31;
        foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
        foreach(var canvas in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Canvas>(true))){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;}
        try
        {
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1920,1200)})
            {
                var rt=new RenderTexture(size.x,size.y,24);camera.targetTexture=rt;Canvas.ForceUpdateCanvases();
                foreach(var w in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ConsumableWindow>()))
                    foreach(var label in w.GetComponentsInChildren<TMP_Text>()){label.ForceMeshUpdate(true);Check(!label.isTextOverflowing,name+" "+size+" fits "+label.name);}
                Canvas.ForceUpdateCanvases();
                foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
                var png=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,size.x,size.y),0,0);png.Apply();File.WriteAllBytes(Folder+"/"+name+"-"+size.x+"x"+size.y+".png",png.EncodeToPNG());
                RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(png);Object.DestroyImmediate(rt);
            }
        }finally{Object.DestroyImmediate(go);}
    }
}
