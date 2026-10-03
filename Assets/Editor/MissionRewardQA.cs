using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MissionRewardQA
{
    static void Check(bool ok,string message){if(!ok)throw new Exception("Mission reward QA: "+message);}
    public static void Run()
    {
        string company=PlayerSettings.companyName;var language=LocalizationManager.Instance.CurrentLanguage;GameObject preview=null;
        try
        {
            PlayerSettings.companyName="JanshinMissionRewardQA";
            PlayerPrefs.DeleteKey(DevilContracts.SaveKey);PlayerPrefs.DeleteKey(DevilContracts.RunKey);PlayerPrefs.SetString(SeventeenStepsMode.ModeKey,"Normal");
            MissionSystem.ResetForNewRun();GameManager.RunCurrency.Set(100);
            var pool=new System.Collections.Generic.List<MissionSystem.MissionYakuEntry>{new MissionSystem.MissionYakuEntry{yakuKey="PINFU",displayNameOverride="平和",difficulty=MissionSystem.Difficulty.Normal}};
            MissionSystem.AssignForEnemy(0,pool);
            Check(MissionSystem.CheckCompletion(new System.Collections.Generic.List<string>{"平和(+1)"}),"qualification not recorded");
            PlayerPrefs.SetInt(MissionSystem.PendingDevilKey,0);
            Check(!MissionSystem.IsCompleted&&GameManager.RunCurrency.Get()==100&&DevilContracts.Load().entries[0].unlocked==0,"win paid or granted early");
            MissionSystem.Load();Check(MissionSystem.HasPendingCompletion,"eligibility lost after reload");
            Check(MissionRewardSettlement.Begin(false)==null,"defeat paid current mission");
            var n=MissionRewardSettlement.Begin();Check(n!=null&&n.stage==1&&n.newContract&&GameManager.RunCurrency.Get()==1100,"shop settlement failed");
            Check(DevilContracts.Load().entries[0].unlocked==1&&MissionSystem.IsCompleted,"shop contract/completion missing");
            MissionRewardSettlement.Begin();Check(GameManager.RunCurrency.Get()==1100,"reload paid twice");
            MissionRewardSettlement.Acknowledge(n);Check(MissionRewardSettlement.Read().stage==2,"contract shown before mission confirmation");
            MissionRewardSettlement.Begin();Check(GameManager.RunCurrency.Get()==1100,"contract notice paid twice");
            MissionRewardSettlement.Acknowledge(MissionRewardSettlement.Read());Check(MissionRewardSettlement.Begin()==null,"completed notice repeats");
            MissionSystem.ResetForNewRun();MissionSystem.AssignForEnemy(0,pool);MissionSystem.CheckCompletion(new System.Collections.Generic.List<string>{"平和"});PlayerPrefs.SetInt(MissionSystem.PendingDevilKey,0);
            n=MissionRewardSettlement.Begin();Check(n!=null&&!n.newContract,"owned contract reacquired");MissionRewardSettlement.Acknowledge(n);Check(MissionRewardSettlement.Read()==null,"owned contract has second popup");
            MissionSystem.ResetForNewRun();MissionSystem.AssignForEnemy(9,pool);MissionSystem.CheckCompletion(new System.Collections.Generic.List<string>{"平和"});PlayerPrefs.SetInt(MissionSystem.PendingDevilKey,9);MissionRewardSettlement.DeferCurrent();
            MissionSystem.AssignForEnemy(10,pool);n=MissionRewardSettlement.Begin(false);Check(n!=null&&n.devilId==9,"secret-route reward lost");
            MissionRewardSettlement.Acknowledge(n);MissionRewardSettlement.Acknowledge(MissionRewardSettlement.Read());
            Check(MissionRewardSettlement.Begin(false)==null,"secret reward repeated");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            preview=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("DevilContracts/MissionRewardView"));var reward=preview.GetComponent<MissionRewardView>();
            foreach(var lang in new[]{LocalizationManager.Language.Japanese,LocalizationManager.Language.English,LocalizationManager.Language.ChineseSimplified})
            {
                LocalizationManager.Instance.SetLanguage(lang);
                for(int id=0;id<11;id++)
                {
                    Check(DevilContractIcons.Get(id)!=null,"missing art "+id);
                    reward.Configure(new MissionRewardSettlement.Notice{stage=2,devilId=id,newContract=true});Canvas.ForceUpdateCanvases();
                    CheckText(preview,lang+" reward "+id);
                }
            }
            LocalizationManager.Instance.SetLanguage(LocalizationManager.Language.Japanese);
            reward.Configure(new MissionRewardSettlement.Notice{stage=2,devilId=0,newContract=true});
            BattleHUDQA.Capture("Logs/DevilContracts/Acquisition.png",1920,1080);
            BattleHUDQA.Capture("Logs/DevilContracts/AcquisitionPhone.png",2340,1080);
            reward.Configure(new MissionRewardSettlement.Notice{stage=1,gold=1000,missionName="平和"});BattleHUDQA.Capture("Logs/DevilContracts/MissionComplete.png",1920,1080);
            UnityEngine.Object.DestroyImmediate(preview);
            var save=DevilContracts.Load();foreach(var e in save.entries)e.unlocked=1;save.equipped=0;DevilContracts.Save(save);
            preview=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("DevilContracts/ContractView"));var view=preview.GetComponent<DevilContractView>();
            foreach(var lang in new[]{LocalizationManager.Language.Japanese,LocalizationManager.Language.English,LocalizationManager.Language.ChineseSimplified})
            {
                LocalizationManager.Instance.SetLanguage(lang);
                for(int id=0;id<11;id++){typeof(DevilContractView).GetField("selected",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,id);view.Refresh();Canvas.ForceUpdateCanvases();CheckText(preview,lang+" ledger "+id);}
            }
            LocalizationManager.Instance.SetLanguage(LocalizationManager.Language.Japanese);typeof(DevilContractView).GetField("selected",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,0);view.Refresh();
            BattleHUDQA.Capture("Logs/DevilContracts/Ledger.png",1920,1080);BattleHUDQA.Capture("Logs/DevilContracts/LedgerTablet.png",1440,1080);
            foreach(var e in save.entries)e.unlocked=0;save.equipped=-1;DevilContracts.Save(save);view.Refresh();Check(!view.selectedPortrait.gameObject.activeSelf&&view.devilName.text=="？？？","locked art leaked");
            typeof(DevilContractView).GetField("selected",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,10);view.Refresh();Check(!view.entryLabels[10].text.Contains(DevilContractCatalog.Get(10).god.Text),"secret god leaked");
            File.WriteAllText("Logs/DevilContracts/MissionRewardsVerified.txt","PASS: eligibility only during win; shop payment and completion; ordered acknowledgement; reload idempotence; existing ownership; defeat excluded; secret-route deferred reward; 11 demon sprites; all three languages; locked identity hidden; desktop, phone and tablet captures.");
        }
        finally{if(preview)UnityEngine.Object.DestroyImmediate(preview);LocalizationManager.Instance.SetLanguage(language);PlayerSettings.companyName=company;MissionSystem.Load();}
    }
    static void CheckText(GameObject root,string context)
    {
        foreach(var t in root.GetComponentsInChildren<TMP_Text>(true)){if(!t.gameObject.activeInHierarchy)continue;t.ForceMeshUpdate();Check(!t.isTextOverflowing,context+" overflow "+t.name+" "+t.text);}
    }
}
