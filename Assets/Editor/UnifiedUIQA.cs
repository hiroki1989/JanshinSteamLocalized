using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class UnifiedUIQA
{
    const string Key="UnifiedUIQA.Running";
    static readonly string[] Scenes={"MenuScene","TierSelectScene","EnemyDialogue","StageClearScene","SpecialTileScene","EquipScene","ShopScene","UpgradeScene","OtherScene","RunScene"};
    static int index,step;static double next;
    static UnifiedUIQA(){EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=Tick;}
    public static void RefineAndRun(){UnifiedUIRefinement.Run();UnifiedUIAdditional.Run();Run();}
    public static void InstallAndRun(){UnifiedUIPresentation.Install();Run();}
    public static void Run(){
        Directory.CreateDirectory("Logs/UnifiedUIPreviews");File.WriteAllText("Logs/UnifiedUIPreviews/QA.txt", "");if(File.Exists("Logs/UnifiedUIPreviews/Error.txt"))File.Delete("Logs/UnifiedUIPreviews/Error.txt");
        SessionState.SetString(Key+"Company",PlayerSettings.companyName);PlayerSettings.companyName="JanshinUnifiedUIQA";
        EditorSceneManager.OpenScene("Assets/Scenes/MenuScene.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state){
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){
            PlayerPrefs.SetInt("FirstMatchTutorialDoneV1",1);PlayerPrefs.SetInt(MenuController.MenuTutorialDoneKey,1);PlayerData.MarkInitialLanguageSelectionCompleted();
            SpecialTileSystem.SetGems(100);PlayerPrefs.SetInt("SP_EquipSlots",4);GameManager.RunCurrency.Set(5000);
            if(SpecialTileSystem.GetOwned().Count==0){for(int i=0;i<6;i++){var e=new SpecialTileSystem.Entry{baseType=(SpecialTileSystem.BaseType)(i%3),rarity=(SpecialTileSystem.Rarity)(i%5),seed=i+400,traitBonusPacked="平和=1;混一色=1;三暗刻=1",effectId=i%5==4?3:0};SpecialTileSystem.AddOwned(e);if(i<4)SpecialTileSystem.TryEquipAppend(e);}}
            if(PlayerData.OwnedOmamori.Count==0)for(int i=0;i<6;i++)PlayerData.GrantRandomOmamori(10);
            if(!EnemySkillNamesSO.TryName("attack",out var skillName)||skillName=="attack"||skillName=="攻撃")throw new Exception("Enemy skill name SO mapping missing");
            index=0;step=0;next=EditorApplication.timeSinceStartup+8;
        }
        if(state==PlayModeStateChange.EnteredEditMode){PlayerSettings.companyName=SessionState.GetString(Key+"Company","OwlGameStudio");SessionState.SetBool(Key,false);EditorApplication.Exit(0);}
    }
    static void Shot(string suffix){Canvas.ForceUpdateCanvases();BattleHUDQA.Capture("Logs/UnifiedUIPreviews/"+Scenes[index]+suffix+".png",1920,1080);}
    static void Tick(){
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+3;
        try{
            var scene=SceneManager.GetActiveScene();
            if(scene.name!=Scenes[index]){File.AppendAllText("Logs/UnifiedUIPreviews/QA.txt","Redirected: "+Scenes[index]+" -> "+scene.name+"\n");index++;if(index>=Scenes.Length){EditorApplication.isPlaying=false;return;}SceneManager.LoadScene(Scenes[index]);next=EditorApplication.timeSinceStartup+7;return;}
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            if(index==0){
                var menuController=UnityEngine.Object.FindAnyObjectByType<MenuController>();
                typeof(MenuController).GetMethod("HideInitialLanguageSelectionPanel",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.Invoke(menuController,null);
                foreach(var t in all.Where(t=>t.name=="SkillUnlockPopupRoot"||t.name.Contains("InitialLanguage")))t.gameObject.SetActive(false);
            }
            if(scene.name=="UpgradeScene" && step<6){
                if(step==0){int balance=GameManager.RunCurrency.Get();GameManager.RunCurrency.Spend(1);foreach(var manager in UnityEngine.Object.FindObjectsByType<UpgradeManager>(FindObjectsSortMode.None)){var gold=new SerializedObject(manager).FindProperty("goldTMP").objectReferenceValue as TMPro.TMP_Text;if(gold&&gold.text.Replace(",","").Trim()!=(balance-1).ToString())throw new Exception("Gold display did not update after external purchase");}GameManager.RunCurrency.Set(balance);File.AppendAllText("Logs/UnifiedUIPreviews/QA.txt","Gold update after external Spend: PASS\n");Shot("_Menu");}
                string[] methods={"OnChooseOfuda","OnChooseStatus","OnChooseDeck","OnChooseTraitYaku","OpenConsumableStore"};
                var menu=UnityEngine.Object.FindAnyObjectByType<UpgradeSceneMenu>();
                if(step>0)Shot("_"+methods[step-1]);
                if(step<5){typeof(UpgradeSceneMenu).GetMethod(methods[step],System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(menu,null);step++;return;}
            }else if(scene.name=="RunScene"){
                var hud=all.FirstOrDefault(t=>t.name=="BattleHUD_Authored");
                foreach(var t in all.Where(t=>t.name=="WinCutinRoot"||t.name=="MatchStartCutinRoot"||t.name=="RiichiCutinRoot"))t.gameObject.SetActive(false);
                string[] names={"MenuPanel","ScoringPanel","EnemyScoringPanel"};
                if(step<3){
                    foreach(var t in all.Where(t=>names.Contains(t.name))){t.gameObject.SetActive(t.name==names[step]);if(t.gameObject.activeSelf){var p=t.parent;while(p&&p.GetComponent<RectTransform>()){p.gameObject.SetActive(true);p=p.parent;}}}
                    foreach(var group in all.Where(t=>t.name==names[step]).Select(t=>t.GetComponent<CanvasGroup>()).Where(g=>g)){group.alpha=1;group.interactable=true;group.blocksRaycasts=true;}
                    Shot("_"+names[step]);step++;return;
                }
            }else Shot("");
            File.AppendAllText("Logs/UnifiedUIPreviews/QA.txt",scene.name+": captured; theme components="+all.Count(t=>t.GetComponent<JanshinPanelTheme>())+"\n");
            index++;step=0;if(index>=Scenes.Length){EditorApplication.isPlaying=false;return;}
            SceneManager.LoadScene(Scenes[index]);next=EditorApplication.timeSinceStartup+(Scenes[index]=="EnemyDialogue"?.5:7);
        }catch(Exception e){File.WriteAllText("Logs/UnifiedUIPreviews/Error.txt",e.ToString());Debug.LogException(e);EditorApplication.isPlaying=false;}
    }
}
