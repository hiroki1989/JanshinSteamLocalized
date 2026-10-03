using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ContractRevisionGameplayQA {
 static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool condition,string message){if(!condition)throw new Exception("Contract revision: "+message);}
 public static void Run(){
  string company=PlayerSettings.companyName;var mode=SeventeenStepsMode.Current;GameObject owner=null;
  try{
   PlayerSettings.companyName="JanshinContractRevisionPreview";
   PlayerPrefs.SetString(SeventeenStepsMode.ModeKey,"Normal");
   var legacy=new DevilContracts.SaveData();for(int i=0;i<11;i++)legacy.entries[i]=new DevilContracts.Progress{unlocked=1,mercy=3,power=2,activeMercy=3,activePower=2};
   PlayerPrefs.SetString(DevilContracts.SaveKey,JsonUtility.ToJson(legacy));var migrated=DevilContracts.Load();
   Check(migrated.entries[0].mercy==10&&migrated.entries[0].power==7&&migrated.entries[0].activePower==7,"legacy research migration");
   SpecialTileSystem.SetGems(110);for(int i=0;i<10;i++)Check(DevilContracts.ResearchBranch(0,2),"resonance research");
   Check(SpecialTileSystem.GetGems()==0&&!DevilContracts.ResearchBranch(0,2),"resonance price/cap");
   DevilContracts.SetBranchDepth(0,2,10);DevilContracts.Equip(0);DevilContracts.BeginRun();
   Check(DevilContracts.Run().resonanceRank==10&&DevilContracts.Run().powerRank==7,"frozen three-branch loadout");
   var w=new DevilContracts.WinContext{colored=true,ofudaTypes=3,passiveTypes=3,hp=200,maxHp=1000,ofudaMultiplier=2};
   for(int id=0;id<11;id++){int previous=0;for(int rank=0;rank<=10;rank++){
    DevilContracts.SaveRun(new DevilContracts.RunState{active=true,version=2,id=id,powerRank=rank,power=DevilContracts.Tier(rank),destroyed=10,spent=1500,relicStacks=2});
    int value=DevilContracts.Outgoing(1000,w,out _,out _);Check(value>=previous,"non-monotonic power rank "+id+"/"+rank);previous=value;
   }}
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SeventeenStepsMode.Current=new SeventeenStepsMode.State();
   owner=new GameObject("GaidenSelectionPreview");var controller=owner.AddComponent<SeventeenStepsController>();
   var root=SeventeenStepsUI.CreateCanvas(owner.transform,"外伝モード");var board=SeventeenStepsUI.Rect("Board",root,Vector2.zero,new Vector2(1920,1080));
   void Set(string key,object value)=>typeof(SeventeenStepsController).GetField(key,flags).SetValue(controller,value);
   var hand=new List<int>{0,1,2,3,4,5,9,10,11,18,19,20,27};var deck=hand.Concat(Enumerable.Range(0,21)).ToList();
   Set("root",root);Set("content",board);Set("score",SeventeenStepsUI.Label(root,"",Vector2.zero,new Vector2(100,40),20));Set("status",SeventeenStepsUI.Label(root,"",new Vector2(0,-430),new Vector2(1250,25),21));
   Set("deck",deck);Set("enemyDeck",deck);Set("enemyHand",hand);Set("enemyReady",true);Set("building",true);Set("skillUses",2);
   var selection=(HashSet<int>)typeof(SeventeenStepsController).GetField("selected",flags).GetValue(controller);for(int i=0;i<13;i++)selection.Add(i);
   typeof(SeventeenStepsController).GetMethod("RenderSelection",flags).Invoke(controller,null);Canvas.ForceUpdateCanvases();
   var preview=board.GetComponentsInChildren<TMP_Text>().First(t=>t.text.StartsWith("役・翻数　"));Check(preview.rectTransform.anchoredPosition.y<0,"preview not below deck");
   BattleHUDQA.Capture("Logs/ContractRevision/GaidenSelection.png",1920,1080);BattleHUDQA.Capture("Logs/ContractRevision/GaidenSelectionPhone.png",2340,1080);
   UnityEngine.Object.DestroyImmediate(owner);owner=null;
   RelicInventoryPresentation.Run();SilentButtonSoundQA.Run();
   File.WriteAllText("Logs/ContractRevision/GameplayVerified.txt","PASS: old research migration; three-branch frozen state; resonance costs/cap; all 11 contracts across 10 power ranks monotonic; lower Gaiden preview; relic header/inventory render.");
  }finally{if(owner)UnityEngine.Object.DestroyImmediate(owner);SeventeenStepsMode.Current=mode;PlayerSettings.companyName=company;}
 }
}
