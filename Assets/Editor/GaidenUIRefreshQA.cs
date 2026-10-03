using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class GaidenUIRefreshQA
{
    static BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Run()
    {
        Directory.CreateDirectory("Logs/GaidenUIRefresh");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var owner=new GameObject("Preview");var gm=owner.AddComponent<SeventeenStepsController>();
        var root=SeventeenStepsUI.CreateCanvas(owner.transform,"外伝モード");
        void Set(string n,object value)=>typeof(SeventeenStepsController).GetField(n,Flags).SetValue(gm,value);
        Set("root",root);Set("enemyReady",true);Set("building",true);
        typeof(SeventeenStepsController).GetMethod("OpenGameMenu",Flags).Invoke(gm,null);
        BattleHUDQA.Capture("Logs/GaidenUIRefresh/Menu.png",1920,1080);
        UnityEngine.Object.DestroyImmediate(((RectTransform)typeof(SeventeenStepsController).GetField("menuOverlay",Flags).GetValue(gm)).gameObject);Set("menuOverlay",null);
        var tiles=new List<int>{0,1,2,3,4,5,9,10,11,18,19,20,27};Set("playerHand",tiles);Set("enemyHand",tiles);Set("playerRiichi",true);
        var win=new SeventeenStepsRules.Win{points=12000,han=6,fu=30,detail="立直(+1) + 一発(+1) + 平和(+1) + 三色同順(+2) + ドラ(+1) | 6翻 30符"};
        var co=(IEnumerator)typeof(SeventeenStepsController).GetMethod("ScorePresentation",Flags).Invoke(gm,new object[]{true,win,27,12000});while(co.MoveNext()){}
        var labels=root.GetComponentsInChildren<TMPro.TMP_Text>(true);
        if(!labels.Any(t=>t.text.Contains("跳満")&&!t.text.Contains("符")))throw new Exception("Limit label or hidden fu missing");
        if(labels.Any(t=>t.text.Contains("6翻")&&t.text.Contains("30符")))throw new Exception("Fu visible for haneman");
        BattleHUDQA.Capture("Logs/GaidenUIRefresh/Score.png",1920,1080);
        BattleHUDQA.Capture("Logs/GaidenUIRefresh/ScoreTablet.png",1440,1080);
        var data=new SeventeenStepsController.SuspendedRound{run=new SeventeenStepsMode.State{enemy=2,round=2,playerScore=12300},deck=new List<int>{7,2,4},selected=new List<int>{2},required=new List<int>{1},designatedKeys=new List<int>{1},designatedValues=new List<int>{2},skillUses=1,missedRon=true,enemyRiichi=true,turn=8};
        var restored=JsonUtility.FromJson<SeventeenStepsController.SuspendedRound>(JsonUtility.ToJson(data));
        if(!restored.deck.SequenceEqual(data.deck)||restored.run.playerScore!=12300||!restored.missedRon||restored.skillUses!=1||restored.designatedValues[0]!=2)throw new Exception("Suspend serialization lost state");
        SeventeenStepsRules.Deal(new System.Random(7),out var deck,out var enemy);
        if(deck.SequenceEqual(deck.OrderBy(t=>t)))throw new Exception("Deal was sorted before animation");
        File.WriteAllText("Logs/GaidenUIRefresh/Verified.txt","PASS: compilation, menu/score renders, suspend data roundtrip, unsorted deal. No gameplay save or scene modified.");
        Debug.Log("Gaiden UI verification complete");
    }
}
