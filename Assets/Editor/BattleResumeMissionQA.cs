using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BattleResumeMissionQA
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static FieldInfo Field(string name)=>typeof(GameManager).GetField(name,Flags);
    static void Set(GameManager gm,string name,object value)=>Field(name).SetValue(gm,value);
    static object Get(GameManager gm,string name)=>Field(name).GetValue(gm);
    static object Call(GameManager gm,string name,params object[] args)=>typeof(GameManager).GetMethod(name,Flags).Invoke(gm,args);
    static void Check(bool ok,string message){if(!ok)throw new Exception("BattleResume QA: "+message);}
    static IList List(GameManager gm,string name)=>(IList)Get(gm,name);
    public static void Run()
    {
        string company=PlayerSettings.companyName;int oldIndex=ProgressionFlowController.CurrentEnemyIndex;int oldPlayerEnemy=PlayerData.CurrentEnemy;
        GameObject go=null;
        try {
            PlayerSettings.companyName="JanshinBattleResumeQA";PlayerPrefs.DeleteAll();
            EditorSceneManager.OpenScene("Assets/Scenes/RunScene.unity",OpenSceneMode.Single);
            var gm=UnityEngine.Object.FindAnyObjectByType<GameManager>();Check(gm!=null,"RunScene GameManager missing");go=gm.gameObject;
            typeof(ProgressionFlowController).GetProperty("CurrentEnemyIndex").SetValue(null,2);PlayerPrefs.SetInt("PF_CurrentEnemyIndex",2);PlayerData.CurrentEnemy=0;
            Set(gm,"playerHP",3456);Set(gm,"playerMaxHP",6000);Set(gm,"enemyHP",4321);Set(gm,"enemyMaxHP",9000);Set(gm,"_mp",23);
            Set(gm,"roundNumber",6);Set(gm,"_playerTsumoCountThisRound",9);Set(gm,"_skillCastsUsedThisTurn",1);
            Set(gm,"_enemySkillPoisonTurnRemaining",3);Set(gm,"_enemySkillPoisonDamagePerTurn",120);
            Set(gm,"_playerIppatsuEligible",true);Set(gm,"_playerIsDoubleRiichi",true);Set(gm,"_playerRiichiDiscardHighlightIndex",2);
            Set(gm,"needDiscardCount",2);Set(gm,"discardedThisTurn",1);
            Set(gm,"phase",Enum.Parse(Field("phase").FieldType,"NeedDiscardN"));
            List(gm,"hand").Add("Man1");List(gm,"hand").Add("Pin2");List(gm,"offers").Add("Sou3");
            List(gm,"discards").Add("East");List(gm,"enemyDiscards").Add("West");
            List(gm,"melds").Add(new List<string>{"Man2","Man2","Man2","Man2"});
            List(gm,"_enemyCommittedMelds").Add(new List<string>{"Pin3","Pin3","Pin3"});
            List(gm,"_enemySkillTurnCounters").Add(4);List(gm,"_enemySkillTurnCounters").Add(2);
            var deck=(Stack<string>)Get(gm,"deck");deck.Push("Man8");deck.Push("Pin9");
            var enemyDeck=(Stack<string>)Get(gm,"enemyDeck");enemyDeck.Push("Sou8");enemyDeck.Push("Sou9");
            ((HashSet<int>)Get(gm,"selHand")).Add(1);((HashSet<int>)Get(gm,"enemyEffectAppliedIndices")).Add(0);
            ((Queue<List<string>>)Get(gm,"_enemyTurnHistory")).Enqueue(new List<string>{"West","North"});
            RunConsumables.Save(new RunConsumables.State{bag=new List<int>{1,2},enemySeal=4,shield=true,usedThisTurn=true});
            Call(gm,"SaveSuspendSnapshot",true);
            var json=PlayerPrefs.GetString("Run_SuspendJSON");Check(json.Contains("\"currentEnemyIndex\":2"),"saved stale PlayerData enemy index");
            Check(json.Contains("meldRows"),"serializable kan rows missing");
            Call(gm,"ValidateBattleResume");Check(PlayerPrefs.GetInt("Run_HasSuspend")==1,"valid snapshot rejected");
            Set(gm,"roundNumber",1);Set(gm,"_playerTsumoCountThisRound",0);Set(gm,"playerHP",1);Set(gm,"_mp",0);
            Set(gm,"_enemySkillPoisonTurnRemaining",0);Set(gm,"_skillCastsUsedThisTurn",0);Set(gm,"_playerIppatsuEligible",false);
            foreach(string name in new[]{"hand","offers","discards","enemyDiscards","melds","_enemyCommittedMelds","_enemySkillTurnCounters"})List(gm,name).Clear();
            deck.Clear();enemyDeck.Clear();RunConsumables.ResetRun();
            Check((bool)Call(gm,"TryLoadSuspendSnapshot"),"restore returned false; inspect log");
            Check((int)Get(gm,"roundNumber")==6&&(int)Get(gm,"_playerTsumoCountThisRound")==9,"round/turn reset");
            Check((int)Get(gm,"playerHP")==3456&&(int)Get(gm,"enemyHP")==4321&&(int)Get(gm,"_mp")==23,"HP/MP changed");
            Check(List(gm,"hand").Count==2&&List(gm,"offers").Count==1&&List(gm,"discards").Count==1,"tiles lost");
            Check(((List<string>)List(gm,"melds")[0]).Count==4&&List(gm,"_enemyCommittedMelds").Count==1,"kan/enemy meld lost");
            Check(deck.Pop()=="Pin9"&&enemyDeck.Pop()=="Sou9","wall order changed");
            Check((int)Get(gm,"_enemySkillPoisonTurnRemaining")==3&&(int)Get(gm,"_skillCastsUsedThisTurn")==1,"poison/skill counters lost");
            Check((bool)Get(gm,"_playerIppatsuEligible")&&(int)Get(gm,"_playerRiichiDiscardHighlightIndex")==2,"riichi state lost");
            Check(RunConsumables.Load().enemySeal==4&&RunConsumables.Load().usedThisTurn,"relic effects lost");
            Check(Get(gm,"phase").ToString()=="NeedDiscardN"&&(int)Get(gm,"needDiscardCount")==2,"discard phase lost");
            Check(!(bool)Get(gm,"_freezeProgression"),"restored battle frozen");
            var text=new GameObject("Mission",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();text.transform.SetParent(go.transform);Set(gm,"missionDisplayTMP",text);
            MissionSystem.ResetForNewRun();MissionSystem.AssignForEnemy(2,new List<MissionSystem.MissionYakuEntry>{new MissionSystem.MissionYakuEntry{yakuKey="PINFU",displayNameOverride="平和",difficulty=MissionSystem.Difficulty.Easy}});
            int gold=GameManager.RunCurrency.Get();Call(gm,"CheckMissionOnPlayerWin",new List<string>{"平和"});
            Check(MissionSystem.HasPendingCompletion&&text.text.StartsWith("<s>"),"completed mission not struck through immediately");
            Check(GameManager.RunCurrency.Get()==gold&&!MissionSystem.IsCompleted,"reward settled during win");
            MissionSystem.Load();Call(gm,"RefreshMissionDisplayText");Check(text.text.StartsWith("<s>"),"pending strike lost after reload");
            Set(gm,"phase",Enum.Parse(Field("phase").FieldType,"Offer"));Set(gm,"isRiichi",true);
            List(gm,"offers").Clear();for(int i=0;i<4;i++)List(gm,"offers").Add("Man1");
            Check(!(bool)Call(gm,"CanUseConsumableNow"),"relic available during riichi");
            for(int id=1;id<=20;id++)Check((string)Call(gm,"ConsumableUnavailable",id,new RunConsumables.State())!=null,"relic bypass "+id);
            Directory.CreateDirectory("Logs/BattleResume");File.WriteAllText("Logs/BattleResume/Verified.txt","PASS: snapshot survives enemy index mismatch; round, turn, walls, tiles, discards, kans, enemy melds, HP/MP, poison, skill casts, riichi/ippatsu, relic effects and discard phase restored; mission struck through before shop reward; all 20 relics blocked during riichi.");
        } finally {
            if(go)UnityEngine.Object.DestroyImmediate(go);typeof(ProgressionFlowController).GetProperty("CurrentEnemyIndex").SetValue(null,oldIndex);PlayerData.CurrentEnemy=oldPlayerEnemy;
            PlayerSettings.companyName=company;MissionSystem.Load();
        }
    }
}
