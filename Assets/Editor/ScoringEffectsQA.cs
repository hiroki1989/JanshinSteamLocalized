using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
public static class ScoringEffectsQA
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static void Set(GameManager g,string n,object v)=>typeof(GameManager).GetField(n,F).SetValue(g,v);
 static object Call(GameManager g,string n,params object[] a)=>typeof(GameManager).GetMethod(n,F).Invoke(g,a);
 static object Get(GameManager g,string n)=>typeof(GameManager).GetField(n,F).GetValue(g);
 static void Check(bool b,string s){if(!b)throw new Exception("ScoringEffectsQA: "+s);}
 static AppliedScoringEffectsView View(GameManager g,bool p)=>((GameObject)Get(g,p?"scoringPanelPlayer":"scoringPanelEnemy")).GetComponentInChildren<AppliedScoringEffectsView>(true);
 static int Incoming(GameManager g,int d,bool c)=>(int)Call(g,"CalculateEnemyWinIncoming",d,c);
 static void Skill(GameManager g,bool player){Set(g,"_currentScoringAttackerIsPlayer",player);object[] a={8000,0,0};typeof(GameManager).GetMethod("EnemySkills_ModifyDamageBeforeApply",F).Invoke(g,a);Check((int)a[0]==(player?6800:10000),"skill damage");}
 public static void Run()
 {
  string company=PlayerSettings.companyName;
  try{
   PlayerSettings.companyName="JanshinScoringEffectsQA";PlayerPrefs.DeleteAll();
   EditorSceneManager.OpenScene("Assets/Scenes/RunScene.unity",OpenSceneMode.Single);
   var g=UnityEngine.Object.FindAnyObjectByType<GameManager>();Check(g,"authored manager");
   foreach(bool side in new[]{true,false})foreach(var text in ((GameObject)Get(g,side?"scoringPanelPlayer":"scoringPanelEnemy")).GetComponentsInChildren<TMP_Text>(true))text.text="";
   Set(g,"playerHP",20000);Set(g,"roundNumber",2);PlayerData.EquippedOmamoriIds=new List<int>();RunConsumables.ResetRun();
   Set(g,"_enemySkillDefenseTurnRemaining",2);Set(g,"_enemySkillPlayerDamageDownRate",.15f);Skill(g,true);
   Call(g,"ConfigureAppliedScoringEffects",true);var pv=View(g,true);Check(pv.Count==1,"guard row count "+pv.Count);
   Check(pv.rows[0].icon.sprite&&pv.rows[0].icon.gameObject.activeSelf,"guard icon missing");Check(pv.rows[0].text.text.Contains("15"),"guard effect missing");
   var guardColor=pv.rows[0].icon.color;Check(guardColor.a>0,"guard invisible");
   Call(g,"AppendConsumableScoringEffectToPanel",true);Call(g,"ConfigureAppliedScoringEffects",true);Check(pv.rows[0].icon.sprite,"relic removed guard");
   Set(g,"_enemySkillAngerTurnRemaining",2);Set(g,"_enemySkillAngerMultiplier",1.25f);Skill(g,false);
   Set(g,"scoringStepRevealEnabled",false);Call(g,"__StartScoringStepReveal",false);var ev=View(g,false);Check(ev.Count==1&&ev.rows[0].icon.sprite,"enemy reveal skipped icon binding");Check(ev.rows[0].text.text.Contains("25"),"anger text");
   var item=new PlayerData.OmamoriInstance{rarity=PlayerData.OmamoriRarity.Common,level=1,effects=new List<PlayerData.EffectEntry>{new PlayerData.EffectEntry{type=PlayerData.OmamoriEffect.DamageTakenPercentDown,amountPercent=.1f}}};
   int id=(int)typeof(PlayerData).GetMethod("EncodeAndPersist",F).Invoke(null,new object[]{item});PlayerData.EquippedOmamoriIds=new List<int>{id};
   Check(Incoming(g,8000,false)==7200,"omamori reduction");Call(g,"ConfigureAppliedScoringEffects",false);Check(ev.Count==2,"omamori row");Check(ev.rows[0].icon.sprite==ItemArtwork.Load("Body/"+ItemArtwork.OmamoriKey(item)),"omamori art");Check(ev.rows[0].text.text.Contains("-10%"),"omamori effect");
   RunConsumables.Save(new RunConsumables.State{shield=true,bloodPact=true});
   Check(Incoming(g,8000,false)==4500,"preview modifiers once");Check(Incoming(g,8000,false)==4500,"repeat preview");Check(RunConsumables.Load().shield,"preview consumed shield");
   Call(g,"ConfigureAppliedScoringEffects",false);Check(ev.Count==4,"separate skill and relic rows");Check(ev.rows[2].icon.sprite==Resources.Load<Sprite>("Consumables/item_20")&&ev.rows[3].icon.sprite==Resources.Load<Sprite>("Consumables/item_13"),"relic artwork");
   Check(Incoming(g,8000,true)==4500&&!RunConsumables.Load().shield,"commit mismatch or not consumed");Check(Incoming(g,8000,false)==9000,"shield consumed more than once");
   PlayerData.EquippedOmamoriIds=new List<int>();Set(g,"playerHP",2000);RunConsumables.Save(new RunConsumables.State{effigy=true});Check(Incoming(g,8000,false)==1999&&RunConsumables.Load().effigy,"effigy preview");Call(g,"ConfigureAppliedScoringEffects",false);Check(ev.rows[ev.Count-1].icon.sprite==Resources.Load<Sprite>("Consumables/item_15"),"effigy icon");Check(Incoming(g,8000,true)==1999&&!RunConsumables.Load().effigy,"effigy commit");
   RunConsumables.ResetRun();Set(g,"playerHP",20000);
   var shiva=new PlayerData.OmamoriInstance{rarity=PlayerData.OmamoriRarity.Legendary,level=1,isUnique=true,uniqueKind=PlayerData.UniqueOmamoriEffectKind.Shiva_East1_PlayerDamageDown50,effects=new List<PlayerData.EffectEntry>()};
   int shivaId=(int)typeof(PlayerData).GetMethod("EncodeAndPersist",F).Invoke(null,new object[]{shiva});PlayerData.EquippedOmamoriIds=new List<int>{shivaId};Set(g,"roundNumber",1);
   Check(Incoming(g,8000,false)==4000,"unique omamori reduction");Call(g,"ConfigureAppliedScoringEffects",false);Check(ev.Count==2&&ev.rows[0].icon.sprite==ItemArtwork.Load("Body/"+ItemArtwork.OmamoriKey(shiva)),"unique omamori art");
   PlayerData.EquippedOmamoriIds=new List<int>();Set(g,"roundNumber",2);
   Set(g,"_legendaryDamageHalfPending",true);Set(g,"_legendaryDamageHalfEnemyKey",Call(g,"GetCurrentEnemyKey_ForLegendary"));
   ((List<string>)Get(g,"_legendaryDamageHalfReservedSourceTiles")).Add("Man1");
   Check(Incoming(g,8000,false)==4000&&(bool)Get(g,"_legendaryDamageHalfPending"),"special tile preview consumed");
   Call(g,"ConfigureAppliedScoringEffects",false);Check(ev.Count==2&&ev.rows[1].icon.sprite==Resources.Load<Sprite>("Sprites/Tiles/Man1"),"special tile art");
   Check(Incoming(g,8000,true)==4000&&!(bool)Get(g,"_legendaryDamageHalfPending"),"special tile doubled or not consumed");
   Set(g,"_legendaryDamageHalfTriggeredThisScoring",false);
   Set(g,"playerHP",1);RunConsumables.Save(new RunConsumables.State{effigy=true});Check(Incoming(g,8000,false)==0,"zero rescue preview");Check(Incoming(g,8000,true)==0&&!RunConsumables.Load().effigy,"zero rescue not consumed");Set(g,"playerHP",20000);

   DevilContracts.SaveRun(new DevilContracts.RunState{active=true,version=2,id=0,mercy=2});
   int expected=DevilContracts.Incoming(8000,false);Check(Incoming(g,8000,false)==expected&&DevilContracts.Run().incomingWins==0,"contract preview consumed");Check(Incoming(g,8000,true)==expected&&DevilContracts.Run().incomingWins==1,"contract counted twice");
   Set(g,"_enemySkillLastAppliedAngerMultiplier",1f);Call(g,"ConfigureAppliedScoringEffects",false);Check(ev.Count==1&&ev.rows[0].icon.sprite,"contract row missing");
   DevilContracts.SaveRun(new DevilContracts.RunState());Incoming(g,8000,false);Call(g,"ConfigureAppliedScoringEffects",false);Check(ev.Count==0,"stale effects from earlier win");
   Set(g,"_enemySkillLastAppliedDefenseRate",0f);Call(g,"ConfigureAppliedScoringEffects",true);Check(pv.Count==0,"inactive guard shown");
   Directory.CreateDirectory("Logs/ScoringEffects");File.WriteAllText("Logs/ScoringEffects/Verified.txt","PASS: authored player guard and enemy anger icons; every scoring entry including disabled reveal binds effect rows; actual equipped omamori artwork and reduction; shield, blood pact and effigy separate artwork; repeat preview does not consume; committed damage matches preview once; contract not counted twice; previous effects cleared.");
  }finally{PlayerSettings.companyName=company;}
 }
}
