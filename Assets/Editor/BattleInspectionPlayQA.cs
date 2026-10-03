using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

[InitializeOnLoad]
public static class BattleInspectionPlayQA
{
    const string Key="BattleInspectionPlayQA.Active";
    static int step;static double start,next;static GameManager gm;static BattleInspectionHUD ui;static bool previousFreeze;static float previousTimeScale;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static BattleInspectionPlayQA(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    public static void Run()
    {
        if(File.Exists("Logs/BattleInspection/PlayVerified.txt"))File.Delete("Logs/BattleInspection/PlayVerified.txt");
        SessionState.SetString(Key+".Company",PlayerSettings.companyName);PlayerSettings.companyName="JanshinInspectionQA";
        EditorSceneManager.OpenScene("Assets/Scenes/RunScene.unity");
        var so=new SerializedObject(UnityEngine.Object.FindAnyObjectByType<GameManager>());
        so.FindProperty("tutorialEnabled").boolValue=false;so.ApplyModifiedPropertiesWithoutUndo();
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".Failed",false);EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void UseBuildEnemyDatabase()
    {
        if(!SessionState.GetBool(Key,false))return;
        var type=typeof(EnemyConfigExcel);var flags=BindingFlags.Static|BindingFlags.NonPublic;
        type.GetField("_cache",flags).SetValue(null,type.GetMethod("LoadAllFromResources",flags).Invoke(null,null));
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){step=0;start=EditorApplication.timeSinceStartup;next=start+9;}
        if(state==PlayModeStateChange.EnteredEditMode){PlayerSettings.companyName=SessionState.GetString(Key+".Company",PlayerSettings.companyName);SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetBool(Key+".Failed",false)?1:0);}
    }
    static void Check(bool condition,string message){if(!condition)throw new Exception("Inspection play QA: "+message);}
    static SkillDescriptionPopup Popup()=>UnityEngine.Object.FindAnyObjectByType<SkillDescriptionPopup>();
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||start<=0)return;
        var now=EditorApplication.timeSinceStartup;if(now<next)return;next=now+.6;
        try{
            if(now-start>55)throw new Exception("Inspection play QA timed out");
            switch(step){
                case 0:
                    gm=UnityEngine.Object.FindAnyObjectByType<GameManager>();ui=UnityEngine.Object.FindAnyObjectByType<BattleInspectionHUD>();
                    if(!gm||!ui||!ui.specialTilesButton.interactable)return;
                    previousTimeScale=Time.timeScale;
                    previousFreeze=(bool)typeof(GameManager).GetField("_freezeProgression",Flags).GetValue(gm);
                    var entries=new System.Collections.Generic.List<SpecialTileSystem.Entry>{new SpecialTileSystem.Entry{baseType=SpecialTileSystem.BaseType.Pin5,rarity=SpecialTileSystem.Rarity.Legendary,effectId=3,traitBonusPacked="平和=1;混一色=2"}};
                    PlayerPrefs.SetString("SP_Equipped",(string)typeof(SpecialTileSystem).GetMethod("SerializeList",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{entries}));
                    DevilContracts.SaveRun(new DevilContracts.RunState{active=true,id=10,mercy=1,power=2,temporaryYaku="立直",temporaryTrait=(int)SkillSetAsset.Trait.Geki});
                    gm.RefreshBattleInspection(ui);Check(ui.contractButton.gameObject.activeInHierarchy&&ui.contractIcon.sprite==DevilContractIcons.Get(10),"frozen contract icon");
                    BattleHUDQA.Capture("Logs/BattleInspection/PlayingHUD.png",1920,1080);
                    ui.contractButton.onClick.Invoke();Check(Popup()!=null&&Time.timeScale==0,"contract button opens paused popup");break;
                case 1:BattleHUDQA.Capture("Logs/BattleInspection/ContractPopup.png",1920,1080);Popup().Close();break;
                case 2:
                    Check(Mathf.Approximately(Time.timeScale,previousTimeScale),"contract close restores time: "+Time.timeScale+" expected "+previousTimeScale);
                    ui.specialTilesButton.onClick.Invoke();Check(Popup()!=null,"special tile button opens popup");break;
                case 3:BattleHUDQA.Capture("Logs/BattleInspection/SpecialTilesPopup.png",1920,1080);Popup().Close();break;
                case 4:
                    Check(ui.enemySkillsButton.gameObject.activeInHierarchy&&!string.IsNullOrEmpty(ui.enemySkillsLabel.text),"enemy skill names visible");
                    ui.enemySkillsButton.onClick.Invoke();Check(Popup()!=null,"enemy skill click opens popup");break;
                case 5:BattleHUDQA.Capture("Logs/BattleInspection/EnemySkillsPopup.png",1920,1080);Popup().Close();break;
                case 6:
                    Check(Mathf.Approximately(Time.timeScale,previousTimeScale)&&(bool)typeof(GameManager).GetField("_freezeProgression",Flags).GetValue(gm)==previousFreeze,"progression resumes after all popups");
                    File.WriteAllText("Logs/BattleInspection/PlayVerified.txt","PASS: real scene button listeners; frozen contract sprite; enemy skill name and popup; special tile effect popup; modal pause and restored progression; captured real gameplay.");
                    Debug.Log("Battle inspection play verified");EditorApplication.isPlaying=false;return;
            }
            step++;
        }catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key+".Failed",true);EditorApplication.isPlaying=false;}
    }
}
