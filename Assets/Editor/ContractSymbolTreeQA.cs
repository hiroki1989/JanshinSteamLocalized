using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ContractSymbolTreeQA
{
    static void Check(bool ok,string message){if(!ok)throw new Exception("Contract symbol tree: "+message);}
    public static void InstallAndVerify()
    {
        DevilContractSetup.BuildTenRankView();DevilContractArtSetup.Run();DevilContractQA.Run();Run();
    }
    public static void Run()
    {
        string company=PlayerSettings.companyName;var language=LocalizationManager.Instance.CurrentLanguage;GameObject root=null;
        const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
        try{
            PlayerSettings.companyName="JanshinSymbolTreeQA";PlayerPrefs.DeleteKey(DevilContracts.SaveKey);PlayerPrefs.SetString(SeventeenStepsMode.ModeKey,"Normal");SpecialTileSystem.SetGems(1000);
            var save=DevilContracts.Load();foreach(var e in save.entries)e.unlocked=1;DevilContracts.Save(save);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            root=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("DevilContracts/ContractView"));var v=root.GetComponent<DevilContractView>();
            typeof(DevilContractView).GetMethod("Start",flags).Invoke(v,null);
            for(int id=0;id<11;id++)Check(DevilContractIcons.Get(id)&&DevilContractIcons.Portrait(id)&&DevilContractIcons.Get(id)!=DevilContractIcons.Portrait(id),"symbol/portrait split "+id);
            Check(v.selectedPortrait.sprite==DevilContractIcons.Portrait(0)&&v.entryIcons[0].sprite==DevilContractIcons.Get(0),"ledger illustration split");
            for(int n=0;n<30;n++)Check(v.nodeIcons[n]&&!v.nodeBodies[n].gameObject.activeSelf&&!v.nodeTitles[n].gameObject.activeSelf&&!v.nodeStates[n].gameObject.activeSelf,"icon-only node "+n);
            v.nodes[2].onClick.Invoke();Check(v.confirmRoot.activeSelf&&!v.confirmYes.interactable&&v.confirmCost.text=="6","future-node prerequisite detail");
            v.confirmNo.onClick.Invoke();v.nodes[0].onClick.Invoke();Check(v.confirmRoot.activeSelf&&v.confirmYes.interactable&&v.confirmCost.text=="2"&&v.confirmGem.sprite,"acquire popup with actual gem icon");
            int before=SpecialTileSystem.GetGems();v.confirmYes.onClick.Invoke();Check(DevilContracts.Load().entries[0].mercy==1&&SpecialTileSystem.GetGems()==before-2,"popup purchase deduction");
            Check(DevilContracts.Load().entries[0].activeMercy==0,"research opt-in preserved");v.confirmYes.onClick.Invoke();Check(DevilContracts.Load().entries[0].activeMercy==1,"enable via popup");
            v.confirmYes.onClick.Invoke();Check(DevilContracts.Load().entries[0].activeMercy==0,"disable via popup");
            v.confirmNo.onClick.Invoke();SpecialTileSystem.SetGems(0);v.nodes[1].onClick.Invoke();Check(!v.confirmYes.interactable&&v.confirmCost.text=="4","insufficient gems shown in popup");
            v.confirmNo.onClick.Invoke();SpecialTileSystem.SetGems(1000);
            foreach(var lang in new[]{LocalizationManager.Language.Japanese,LocalizationManager.Language.English,LocalizationManager.Language.ChineseSimplified}){
                LocalizationManager.Instance.SetLanguage(lang);
                for(int id=0;id<11;id++){
                    typeof(DevilContractView).GetField("selected",flags).SetValue(v,id);v.Refresh();
                    for(int slot=0;slot<30;slot++){
                        v.nodes[slot].onClick.Invoke();Canvas.ForceUpdateCanvases();
                        foreach(var t in v.confirmRoot.GetComponentsInChildren<TMP_Text>()){
                            t.ForceMeshUpdate();Check(!t.isTextOverflowing,"popup overflow "+lang+" "+id+"/"+slot+" "+t.name);
                            foreach(char c in System.Text.RegularExpressions.Regex.Replace(t.text,"<[^>]+>",""))if(!char.IsWhiteSpace(c))Check(t.font.HasCharacter(c,true,true),"missing glyph "+lang+" "+c);
                        }
                        v.confirmNo.onClick.Invoke();
                    }
                }
            }
            LocalizationManager.Instance.SetLanguage(LocalizationManager.Language.Japanese);typeof(DevilContractView).GetField("selected",flags).SetValue(v,0);v.Refresh();
            BattleHUDQA.Capture("Logs/DevilContracts/SymbolTree.png",1920,1080);BattleHUDQA.Capture("Logs/DevilContracts/SymbolTreePhone.png",2340,1080);
            v.nodes[10].onClick.Invoke();BattleHUDQA.Capture("Logs/DevilContracts/SymbolNodePopup.png",1920,1080);BattleHUDQA.Capture("Logs/DevilContracts/SymbolNodePopupPhone.png",2340,1080);
            File.WriteAllText("Logs/DevilContracts/SymbolTreeVerified.txt","PASS: 11 separate flat symbols and original portraits; icon-only 30-node authored tree; future-node details and prerequisites; actual gem sprite; popup purchase/enable/disable; insufficient gems; all 330 nodes in JA/EN/ZH without overflow or missing glyphs; phone/desktop render.");
        }finally{if(root)UnityEngine.Object.DestroyImmediate(root);LocalizationManager.Instance.SetLanguage(language);PlayerSettings.companyName=company;}
    }
}
