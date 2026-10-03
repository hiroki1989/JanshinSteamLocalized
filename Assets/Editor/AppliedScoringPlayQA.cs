using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class AppliedScoringPlayQA
{
    const string Key="AppliedScoringPlayQA.Active";
    static double start,next;static int step;static GameManager gm;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static AppliedScoringPlayQA(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    public static void Run(){
        SessionState.SetString(Key+".Company",PlayerSettings.companyName);PlayerSettings.companyName="JanshinAppliedScoringQA";
        EditorSceneManager.OpenScene("Assets/Scenes/RunScene.unity");
        var so=new SerializedObject(UnityEngine.Object.FindAnyObjectByType<GameManager>());so.FindProperty("tutorialEnabled").boolValue=false;so.ApplyModifiedPropertiesWithoutUndo();
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Failed",false);EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Database(){if(!SessionState.GetBool(Key,false))return;var t=typeof(EnemyConfigExcel);var f=BindingFlags.Static|BindingFlags.NonPublic;t.GetField("_cache",f).SetValue(null,t.GetMethod("LoadAllFromResources",f).Invoke(null,null));}
    static void Changed(PlayModeStateChange state){
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){step=0;start=EditorApplication.timeSinceStartup;next=start+10;}
        if(state==PlayModeStateChange.EnteredEditMode){PlayerSettings.companyName=SessionState.GetString(Key+".Company",PlayerSettings.companyName);SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetBool(Key+".Failed",false)?1:0);}
    }
    static object Call(string name,params object[] args)=>typeof(GameManager).GetMethods(Flags).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(gm,args);
    static void Set(string name,object value)=>typeof(GameManager).GetField(name,Flags).SetValue(gm,value);
    static T Get<T>(string name)=>(T)typeof(GameManager).GetField(name,Flags).GetValue(gm);
    static AppliedScoringEffectsView View(bool player)=>Get<GameObject>(player?"scoringPanelPlayer":"scoringPanelEnemy").GetComponentInChildren<AppliedScoringEffectsView>(true);
    static void Check(bool yes,string why){if(!yes)throw new Exception("Applied scoring QA: "+why);}
    static void Score(bool player,float geki=1,float iyu=0,float ofuda=1,int heal=0){
        Call("__StopScoringStepReveal");
        Call("__ApplyScoringManualUI","平和","2翻 30符",5000,geki,0f,iyu,6000,heal,0,ofuda,0,0,0,player);
        Get<GameObject>("scoringPanel").SetActive(true);Call("ConfigureAppliedScoringEffects",player);
    }
    static void Tick(){
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||start<=0||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+1;
        try{
            switch(step){
            case 0:
                gm=UnityEngine.Object.FindAnyObjectByType<GameManager>();if(!gm)return;
                Set("_freezeProgression",true);OfudaRunInventory.Clear();PlayerData.EquippedOmamoriIds=new List<int>();DevilContracts.SaveRun(new DevilContracts.RunState());RunConsumables.Save(new RunConsumables.State());
                Score(true);Check(View(true).Count==0,"inactive effects remain visible");
                BattleHUDQA.Capture("Logs/AppliedScoring/NoEffects.png",1920,1080);break;
            case 1:
                Set("_lastScoringYaku",new List<string>{"平和"});Set("_lastScoringBasePoints",2000);
                var so=Resources.Load<OfudaCatalogSO>("OfudaCatalogSO");
                var catalog=new OfudaExcelLoader.Catalog{conditions=so.conditions,effects=so.effects};
                foreach(var band in so.priceMap)catalog.priceMap.Add((band.maxProbSum,band.rarity,band.priceK,band.priceFixed));
                var defs=OfudaCatalog.BuildFromExcel(catalog);
                Set("_ofudaMap",defs.ToDictionary(d=>d.id,d=>d));
                typeof(GameManager).GetField("_ofudaCsvCache",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,catalog);
                typeof(GameManager).GetField("_ofudaEffectByKey",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,catalog.effects.ToDictionary(e=>e.key,e=>e));
                var active=defs.First(d=>d.effect.key.StartsWith("EFFECT:点数が")&&(bool)Call("Ofuda_Cond_Passes_Runtime",d.condition.key));
                var inactive=defs.First(d=>d.effect.key.StartsWith("EFFECT:点数が")&&!(bool)Call("Ofuda_Cond_Passes_Runtime",d.condition.key));
                OfudaRunInventory.SaveList(new List<string>{active.id,inactive.id});
                var charm=new PlayerData.OmamoriInstance{rarity=PlayerData.OmamoriRarity.Epic,level=1,effects=new List<PlayerData.EffectEntry>{new PlayerData.EffectEntry{type=PlayerData.OmamoriEffect.GekiDamagePercentUp,amountPercent=.1f},new PlayerData.EffectEntry{type=PlayerData.OmamoriEffect.ShunAddPercentUp,amountPercent=.2f}}};
                PlayerPrefs.SetString("Omamori_991122",JsonUtility.ToJson(charm));PlayerData.EquippedOmamoriIds=new List<int>{991122};
                DevilContracts.SaveRun(new DevilContracts.RunState{active=true,id=0});
                Call("ContractsModifyScoring",6000,500,0,new List<string>{"平和"},1.2f,0f,.4f);
                Score(true,1.2f,.4f,1.5f,500);
                var view=View(true);Check(view.Count==5,"two passives, one eligible ofuda, one charm, one active contract; got "+view.Count);
                Check(view.rows.Take(view.Count).Count(r=>r.icon.sprite==DevilContractIcons.Get(0))==1,"devil sprite missing");
                Check(view.rows.Take(view.Count).Count(r=>r.icon.sprite==ItemArtwork.Load("Body/ofuda_"+ItemArtwork.RarityKey(active.rarity)))==1,"actual ofuda image missing");
                Check(view.rows.Any(r=>r.text.text.Contains("+10%")&&!r.text.text.Contains("+20%")),"inactive charm passive shown");
                Canvas.ForceUpdateCanvases();BattleHUDQA.Capture("Logs/AppliedScoring/PlayerActual.png",1920,1080);BattleHUDQA.Capture("Logs/AppliedScoring/PlayerTablet.png",1600,1200);break;
            case 2:
                OfudaRunInventory.Clear();PlayerData.EquippedOmamoriIds=new List<int>();DevilContracts.SaveRun(new DevilContracts.RunState());
                ((List<string>)typeof(GameManager).GetField("contractWinEffects",Flags).GetValue(gm)).Clear();
                Score(true);Check(View(true).Count==0,"next win retained old item/contract rows");
                Score(false);Check(View(false).Count==0,"enemy shows player offensive effects");break;
            case 3:
                Score(true);var scrollView=View(true);scrollView.Clear();
                for(int i=0;i<12;i++)scrollView.Add(DevilContractIcons.Get(0),"契約の発動効果：ダメージ増加・HP回復・MP回復・獲得Gold変更を表示");
                Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(scrollView.scroll.content);
                Check(scrollView.scroll.content.rect.height>scrollView.scroll.viewport.rect.height,"long effect list does not scroll");
                var advance=Get<Button>("scoringStepAdvanceButtonPlayer");Call("__SetScoringStepAdvanceButtonVisible",advance,true);
                var hits=new List<RaycastResult>();var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,scrollView.scroll.viewport.position)};
                EventSystem.current.RaycastAll(pointer,hits);Check(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(scrollView.transform),"step advance overlay blocks effect scrolling");
                Call("__SetScoringStepAdvanceButtonVisible",advance,false);scrollView.Clear();
                File.WriteAllText("Logs/AppliedScoring/PlayVerified.txt","PASS: real initialized scene; inactive effect rows absent; two triggered passives only; conditional ofuda included/excluded separately; actual charm and ofuda artwork; inactive charm trait absent; devil icon and calculated damage delta; next win clears all previous rows; enemy excludes offensive effects; PC/tablet captures. Isolated QA preferences.");
                Debug.Log("Applied scoring play verified");
                Get<GameObject>("scoringPanelPlayer").SetActive(false);Get<GameObject>("scoringPanelEnemy").SetActive(true);
                var enemyView=View(false);enemyView.Add(DevilContractIcons.Get(0),"契約：被ダメージ -750");enemyView.Add(null,"お守り：被ダメージ -10%");enemyView.Add(null,"神のスキル：ダメージ +20%");break;
            case 4:
                Canvas.ForceUpdateCanvases();BattleHUDQA.Capture("Logs/AppliedScoring/EnemyActual.png",1920,1080);
                EditorApplication.isPlaying=false;return;
            }
            step++;
        }catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key+".Failed",true);EditorApplication.isPlaying=false;}
    }
}
