using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BattleResumeLifecycleQA
{
 const string Key="BattleResumeLifecycleQA";
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static double next,deadline;static int step;static GameManager gm;
 static string saved,traitKey;static int maxMp;static List<string> wall,enemyWall;
 static BattleResumeLifecycleQA(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
 public static void Run(){SessionState.SetString(Key+"Company",PlayerSettings.companyName);PlayerSettings.companyName="JanshinResumeLifecycleQA";SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Failed",false);EditorSceneManager.OpenScene("Assets/Scenes/RunScene.unity");EditorApplication.isPlaying=true;}
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Init(){if(!SessionState.GetBool(Key,false))return;PlayerPrefs.DeleteAll();PlayerData.ResetDeckToDefault();PlayerPrefs.SetInt("FirstMatchTutorialDoneV1",1);PlayerPrefs.SetInt("FirstMenuTutorialDoneV1",1);PlayerPrefs.SetInt("Run_StartedFlagV1",1);var t=typeof(EnemyConfigExcel);t.GetField("_cache",F).SetValue(null,t.GetMethod("LoadAllFromResources",F).Invoke(null,null));}
 static void Changed(PlayModeStateChange s){if(!SessionState.GetBool(Key,false))return;if(s==PlayModeStateChange.EnteredPlayMode){step=0;next=EditorApplication.timeSinceStartup+12;deadline=next+160;}if(s==PlayModeStateChange.EnteredEditMode){PlayerSettings.companyName=SessionState.GetString(Key+"Company",PlayerSettings.companyName);SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetBool(Key+"Failed",false)?1:0);}}
 static object Get(string n)=>typeof(GameManager).GetField(n,F).GetValue(gm);
 static void Set(string n,object v)=>typeof(GameManager).GetField(n,F).SetValue(gm,v);
 static object Call(string n,params object[] args)=>typeof(GameManager).GetMethods(F).Single(m=>m.Name==n&&m.GetParameters().Length==args.Length).Invoke(gm,args);
 static void Check(bool ok,string m){if(!ok)throw new Exception("Resume lifecycle: "+m);}
 static void Fixture(){
  Time.timeScale=0;gm.StopAllCoroutines();
  foreach(string n in new[]{"_enemyTurnRunning","_beginOfferPhaseInProgress","_playerSkillCutinRunning","_playerSkillTransformRunning","_enemySkillCutinRunning","_enemyRiichiCutinRunning","_playerRiichiCutinRunning","_rinshanDrawRunning","_tutorialDealingFirstDraw","_consumableDealing","_playerWinDamageAnimating","_enemyWinDamageAnimating","_enemySkillDamageAnimating","_mpDecreaseAnimRunning","_tutorialRunning","_freezeProgression","_autoSkipPending","_autoConfirmOfferPending","_suspendEnemyTurnDelayPending"})Set(n,false);
  Set("phase",Enum.Parse(typeof(GameManager).GetField("phase",F).FieldType,"Offer"));Set("roundNumber",6);Set("_observedRoundNumber",6);Set("_observedEnemyIndex",0);
  Set("playerHP",3456);Set("playerMaxHP",9000);Set("enemyHP",4321);Set("enemyMaxHP",18000);Set("_mp",321);Set("_playerTsumoCountThisRound",7);Set("_skillCastsUsedThisTurn",1);Set("scoreThisEnemy",12345);
  var tiles=(List<string>)Get("hand");tiles.Clear();tiles.AddRange(new[]{"Man1","Man2","Man3","Man4","Man5","Man6","Pin1","Pin2","Pin3","Sou1","Sou2","Sou3","East"});
  var offers=(List<string>)Get("offers");offers.Clear();offers.AddRange(new[]{"West","North","Pin5","Sou7"});
  ((HashSet<int>)Get("selOffer")).Clear();((HashSet<int>)Get("selOffer")).Add(2);
  foreach(string n in new[]{"_enemySkillAngerTurnRemaining","_enemySkillDefenseTurnRemaining","_enemySkillPoisonTurnRemaining","_enemySkillParalysisTurnRemaining"})Set(n,4);
  Set("_enemySkillAngerMultiplier",1.3f);Set("_enemySkillPlayerDamageDownRate",.15f);Set("_enemySkillPoisonDamagePerTurn",120);
  var skills=(List<EnemySkillConfig>)Get("_enemySkills");skills.Clear();skills.Add(new EnemySkillConfig("defense",15,4,3));var counters=(List<int>)Get("_enemySkillTurnCounters");counters.Clear();counters.Add(2);Set("_enemySkillsOwnerRuntimeIndex",0);
  RunConsumables.Save(new RunConsumables.State{bag=new List<int>{1,13,15},shield=true,effigy=true,bloodPact=true,enemySeal=3,regeneration=2,geki=true,freeCast=true,castsBonus=1,usedThisTurn=true});
  DevilContracts.SaveRun(new DevilContracts.RunState{version=2,active=true,id=0,mercy=2,incomingWins=1,spent=500,skillUses=3});
  GameManager.RunCurrency.Set(1730);PlayerPrefs.SetInt("Run_HPBonus",900);PlayerPrefs.SetInt("Run_MPBonus",500);PlayerPrefs.SetInt("Run_SkillCastsBonus",2);PlayerPrefs.SetInt("Run_UpgradeCostCount_Buy",3);
  PlayerData.AddToDeck(0,2);PlayerData.AddToDeck(1,-1);OfudaRunInventory.SaveList(new List<string>{"TANYAO__SCORE_120","PINFU__SCORE_140"});
  var set=Resources.LoadAll<SkillSetAsset>("SkillSets").First(s=>s.activeSkills.Count>0);var keys=set.BattleTraitPreferenceKeys();traitKey=keys.First(p=>p.Value).Key;PlayerPrefs.SetInt(traitKey,4);
  maxMp=(int)Call("EffectiveMaxMP");wall=new List<string>((Stack<string>)Get("deck"));enemyWall=new List<string>((Stack<string>)Get("enemyDeck"));
  Call("SaveSuspendSnapshot",true);saved=PlayerPrefs.GetString("Run_SuspendJSON");Check(saved.Contains("\"version\":3"),"checkpoint version");
 }
 static void Verify(){
  Check((int)Get("enemyHP")==4321&&(int)Get("enemyMaxHP")==18000,"enemy HP overwritten after Start/bootstrap");
  Check((int)Get("playerHP")==3456&&(int)Get("playerMaxHP")==9000&&(int)Get("_mp")==321,"HP/MP overwritten");Check((int)Call("EffectiveMaxMP")==maxMp,"max MP changed");
  Check((int)Get("roundNumber")==6&&(int)Get("_playerTsumoCountThisRound")==7,"round/turn reset");Check((int)Get("_skillCastsUsedThisTurn")==1,"skill usage reset");Check((int)Get("scoreThisEnemy")==12345&&GameManager.RunCurrency.Get()==1730,"first enemy gold/score reset");
  Check(((Stack<string>)Get("deck")).SequenceEqual(wall)&&((Stack<string>)Get("enemyDeck")).SequenceEqual(enemyWall),"wall rebuilt");Check(((List<string>)Get("hand")).Count==13&&((List<string>)Get("offers"))[2]=="Pin5"&&((HashSet<int>)Get("selOffer")).Contains(2),"hand/offers/selection reset");
  foreach(string n in new[]{"_enemySkillAngerTurnRemaining","_enemySkillDefenseTurnRemaining","_enemySkillPoisonTurnRemaining","_enemySkillParalysisTurnRemaining"})Check((int)Get(n)==4,n);
  Check(((List<int>)Get("_enemySkillTurnCounters"))[0]==2&&((List<EnemySkillConfig>)Get("_enemySkills"))[0].id=="defense","enemy skill config/countdown reset");
  var r=RunConsumables.Load();Check(r.bag.SequenceEqual(new[]{1,13,15})&&r.shield&&r.effigy&&r.bloodPact&&r.geki&&r.freeCast&&r.enemySeal==3&&r.regeneration==2&&r.castsBonus==1&&r.usedThisTurn,"relic inventory/effects reset");
  Check(DevilContracts.Run().incomingWins==1&&DevilContracts.Run().skillUses==3,"contract reset");
  Check(PlayerData.GetDeckCountsCopy()[0]==6&&PlayerData.GetDeckCountsCopy()[1]==3,"shop deck purchase/destruction lost");Check(PlayerPrefs.GetInt("Run_HPBonus")==900&&PlayerPrefs.GetInt("Run_MPBonus")==500&&PlayerPrefs.GetInt("Run_SkillCastsBonus")==2&&PlayerPrefs.GetInt("Run_UpgradeCostCount_Buy")==3,"shop upgrades/costs lost");Check(PlayerPrefs.GetInt(traitKey)==4,"trait upgrade lost");Check(OfudaRunInventory.LoadList().Count==2,"ofuda lost");
 }
 static void Tick(){
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||next==0||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
  try{
   if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Resume lifecycle timeout");
   gm=UnityEngine.Object.FindAnyObjectByType<GameManager>();if(!gm)return;
   if(step==0){Fixture();Call("__PrepareForSceneUnload");PlayerPrefs.SetInt("RunGold",99);PlayerPrefs.SetInt("Run_HPBonus",0);PlayerPrefs.SetInt(traitKey,0);PlayerData.ResetDeckToDefault();OfudaRunInventory.Clear();RunConsumables.ResetRun();SceneManager.LoadScene("RunScene");step=1;next+=8;return;}
   if(step==1){Verify();gm.ApplyEnemyConfigFromExcel(0);Call("__Progression_InternalInit");Verify();Call("SaveSuspendSnapshot",true);Call("__PrepareForSceneUnload");SceneManager.LoadScene("RunScene");step=2;next+=8;return;}
   Verify();Directory.CreateDirectory("Logs/ResumeLifecycle");File.WriteAllText("Logs/ResumeLifecycle/Verified.txt","PASS: two real RunScene reloads through Awake, Start and deferred initializers; enemy/player HP and max HP, MP and max MP, round/turn, skill use, wall order, hand/offers/selections, all enemy status effects and skill cooldown, relic inventory and all active relic flags, contracts, Gold, shop deck purchase/destruction, permanent-for-run stat upgrades, purchase costs, passive progression and ofuda preserved. Explicit late enemy-config/progression reapply also preserves state.");Time.timeScale=1;EditorApplication.isPlaying=false;
  }catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key+"Failed",true);Time.timeScale=1;EditorApplication.isPlaying=false;}
 }
}
