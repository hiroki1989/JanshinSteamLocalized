using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static class MissionRewardPlayQA
{
    const string Key="MissionRewardPlayQA";
    static double next;static int step;static int balance;
    static MissionRewardPlayQA(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    public static void Run()
    {
        SessionState.SetString(Key+"Company",PlayerSettings.companyName);PlayerSettings.companyName="JanshinMissionRewardPlayQA";
        PlayerPrefs.SetString(SeventeenStepsMode.ModeKey,"Normal");PlayerPrefs.DeleteKey(DevilContracts.SaveKey);PlayerPrefs.DeleteKey(DevilContracts.RunKey);
        MissionSystem.ResetForNewRun();GameManager.RunCurrency.Set(123);
        MissionSystem.AssignForEnemy(0,new List<MissionSystem.MissionYakuEntry>{new MissionSystem.MissionYakuEntry{yakuKey="PINFU",displayNameOverride="平和",difficulty=MissionSystem.Difficulty.Normal}});
        MissionSystem.CheckCompletion(new List<string>{"平和"});PlayerPrefs.SetInt(MissionSystem.PendingDevilKey,0);PlayerPrefs.Save();
        EditorSceneManager.OpenScene("Assets/Scenes/UpgradeScene.unity");
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Failed",false);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange s)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(s==PlayModeStateChange.EnteredPlayMode){step=0;next=EditorApplication.timeSinceStartup+6;}
        if(s==PlayModeStateChange.EnteredEditMode){PlayerSettings.companyName=SessionState.GetString(Key+"Company","OwlGameStudio");SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetBool(Key+"Failed",false)?1:0);}
    }
    static void Check(bool ok,string why){if(!ok)throw new Exception("Mission reward play QA: "+why);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+2;
        try
        {
            var view=UnityEngine.Object.FindAnyObjectByType<MissionRewardView>();
            if(step==0)
            {
                Check(view&&view.confirm.interactable,"mission popup not ready");Check(MissionRewardSettlement.Read().stage==1,"wrong first popup");
                balance=GameManager.RunCurrency.Get();Check(balance==1123,"shop did not add exactly 1000 Gold");
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,view.confirm.transform.position)},hits);
                Check(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(view.transform),"popup cannot receive clicks");
                BattleHUDQA.Capture("Logs/DevilContracts/ShopMissionActual.png",1920,1080);view.confirm.onClick.Invoke();
            }
            else if(step==1)
            {
                Check(view&&view.confirm.interactable&&MissionRewardSettlement.Read().stage==2,"contract does not follow confirmation");
                Check(view.portrait.sprite==DevilContractIcons.Get(0)&&view.icon.sprite==DevilContractIcons.Get(0),"shared portrait/icon missing");
                Check(GameManager.RunCurrency.Get()==balance,"contract notice added Gold again");
                BattleHUDQA.Capture("Logs/DevilContracts/ShopContractActual.png",1920,1080);view.confirm.onClick.Invoke();
            }
            else
            {
                Check(!view&&MissionRewardSettlement.Read()==null,"reward modal did not close");
                Check(GameManager.RunCurrency.Get()==balance,"closing changed Gold");
                bool restored=false;foreach(var r in UnityEngine.Object.FindObjectsByType<BaseRaycaster>(FindObjectsSortMode.None))if(r.enabled)restored=true;
                Check(restored,"shop input not restored");
                File.WriteAllText("Logs/DevilContracts/ShopPlayVerified.txt","PASS: actual UpgradeScene; mission then contract; confirmation clickable; input restored; Gold awarded once; shared artwork.");
                Debug.Log("Mission reward shop play verified");EditorApplication.isPlaying=false;return;
            }
            step++;
        }
        catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key+"Failed",true);EditorApplication.isPlaying=false;}
    }
}
