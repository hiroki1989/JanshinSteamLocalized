using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DevilContractQA
{
    static void Check(bool condition,string message){if(!condition)throw new Exception("Devil Contracts: "+message);}
    public static void Run()
    {
        if(File.Exists("Logs/DevilContracts/Verified.txt"))File.Delete("Logs/DevilContracts/Verified.txt");
        string[] keys={DevilContracts.SaveKey,DevilContracts.RunKey,SeventeenStepsMode.ModeKey,"RunConsumablesV1",LocalizationManager.PlayerPrefsKeyLanguage};
        bool[] had=keys.Select(PlayerPrefs.HasKey).ToArray();string[] values=keys.Select(k=>PlayerPrefs.GetString(k,"")).ToArray();
        bool hadGems=PlayerPrefs.HasKey("SP_Gems");int gems=SpecialTileSystem.GetGems();
        GameObject owner=null,preview=null;var originalLanguage=LocalizationManager.Instance.CurrentLanguage;
        try{
            PlayerPrefs.DeleteKey(DevilContracts.SaveKey);PlayerPrefs.DeleteKey(DevilContracts.RunKey);PlayerPrefs.SetString(SeventeenStepsMode.ModeKey,"Normal");
            Check(DevilContractCatalog.Shared.contracts.Length==11,"catalog count");
            Check(DevilContractCatalog.Shared.contracts.All(d=>d.mercy.Length==10&&d.power.Length==10&&d.resonance.Length==10),"330-node tree");
            Check(DevilContractCatalog.Shared.contracts.All(d=>d.mercy.Concat(d.power).All(n=>!string.IsNullOrEmpty(n.description.ja)&&!string.IsNullOrEmpty(n.description.en)&&!string.IsNullOrEmpty(n.description.zh))),"localized node content");
            Check(!DevilContracts.Equip(0),"locked contract equip");
            for(int i=0;i<11;i++){Check(DevilContracts.Grant(i),"first mission grant");Check(!DevilContracts.Grant(i),"duplicate grant");}
            SpecialTileSystem.SetGems(110);
            for(int rank=1;rank<=10;rank++){Check(DevilContracts.Research(0,false),"research rank "+rank);Check(DevilContracts.Load().entries[0].mercy==rank,"rank saved");}
            Check(SpecialTileSystem.GetGems()==0&&!DevilContracts.Research(0,false),"ten-rank cap and cost");Check(!DevilContracts.Research(1,false),"insufficient gems");
            Check(DevilContracts.Load().entries[0].activeMercy==0,"research must not activate automatically");
            DevilContracts.SetDepth(0,false,10);DevilContracts.Equip(0);DevilContracts.BeginRun();DevilContracts.Equip(1);Check(DevilContracts.Run().id==0&&DevilContracts.Run().mercy==3&&DevilContracts.Run().mercyRank==10,"run loadout frozen");
            var w=new DevilContracts.WinContext{maxHp=1000,hp=400,colored=true,ofudaTypes=3,passiveTypes=3,ofudaMultiplier=2};
            int[] expected={1050,1100,1083,1000,1000,1000,1000,1133,1000,1167,1167};
            for(int i=0;i<11;i++){DevilContracts.SaveRun(new DevilContracts.RunState{active=true,id=i});Check(DevilContracts.Outgoing(1000,w,out _,out _)==expected[i],"base effect "+i);}
            DevilContracts.SaveRun(new DevilContracts.RunState{active=true,id=3,power=3,destroyed=100});Check(DevilContracts.Outgoing(1000,w,out _,out _)==1420,"destruction cap");
            DevilContracts.SaveRun(new DevilContracts.RunState{active=true,id=6,power=3,relicStacks=3});Check(DevilContracts.Outgoing(1000,w,out _,out _)==1750,"relic stack cap");
            DevilContracts.SaveRun(new DevilContracts.RunState{active=true,id=5,power=3,spent=10000});Check(DevilContracts.Outgoing(1000,w,out _,out _)==1350,"spending cap");
            DevilContracts.SaveRun(new DevilContracts.RunState{active=true,id=10,power=3});Check(DevilContracts.Outgoing(1000,w,out _,out _)==2000,"Cerberus three-type bonus");
            DevilContracts.SaveRun(new DevilContracts.RunState{active=true,id=0,mercy=2});Check(DevilContracts.Incoming(1000,false)==850&&DevilContracts.Run().incomingWins==0,"incoming preview is pure");Check(DevilContracts.Incoming(1000,true)==850&&DevilContracts.Incoming(1000,false)==1000,"first-hit consumed once");
            var suspended=new DevilContracts.RunState{active=true,id=10,enemy=10,round=3,temporarySkill="RandomMan",temporaryTrait=(int)SkillSetAsset.Trait.Geki,temporaryYaku="PINFU",hpCharge=23,mpCharge=12,relicStacks=2,skillUses=3};
            var roundtrip=JsonUtility.FromJson<DevilContracts.RunState>(JsonUtility.ToJson(suspended));DevilContracts.SaveRun(roundtrip);
            Check(roundtrip.temporaryYaku=="PINFU"&&roundtrip.round==3&&roundtrip.skillUses==3&&roundtrip.hpCharge==23,"suspend serialization");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);owner=new GameObject("ContractsTest");var gm=owner.AddComponent<GameManager>();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;var level=typeof(GameManager).GetMethod("ContractsTraitLevel",flags);
            Check((int)level.Invoke(gm,new object[]{0,"RandomMan",SkillSetAsset.Trait.Geki,"PINFU"})==1,"temporary passive participates in scoring");
            Check((int)level.Invoke(gm,new object[]{0,"RandomMan",SkillSetAsset.Trait.Shun,"PINFU"})==0,"temporary passive is trait-specific");
            Check((int)level.Invoke(gm,new object[]{4,"RandomMan",SkillSetAsset.Trait.Geki,"PINFU"})==4,"temporary passive never replaces unlocked level");
            var fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            typeof(GameManager).GetField("playerMaxHP",fields).SetValue(gm,1000);
            typeof(GameManager).GetField("playerHP",fields).SetValue(gm,500);
            DevilContracts.SaveRun(new DevilContracts.RunState{active=true,id=4});
            typeof(GameManager).GetMethod("ContractsRecoverHp",flags).Invoke(gm,new object[]{200,false});
            Check((int)typeof(GameManager).GetField("playerHP",fields).GetValue(gm)==660&&Mathf.Approximately(DevilContracts.Run().hpCharge,16),"actual adjusted HP recovery charges Ammit");
            typeof(GameManager).GetField("playerHP",fields).SetValue(gm,990);
            typeof(GameManager).GetMethod("ContractsRecoverHp",flags).Invoke(gm,new object[]{200,false});
            Check(Mathf.Approximately(DevilContracts.Run().hpCharge,17),"overhealing excluded without upgrade");
            DevilContracts.SaveRun(new DevilContracts.RunState{active=true,id=4,power=2});
            typeof(GameManager).GetField("playerHP",fields).SetValue(gm,1000);
            typeof(GameManager).GetMethod("ContractsRecoverHp",flags).Invoke(gm,new object[]{200,false});
            Check(Mathf.Approximately(DevilContracts.Run().hpCharge,28.8f),"upgraded overheal conversion");
            var savedLoadout=DevilContracts.Load();savedLoadout.equipped=6;savedLoadout.entries[6].mercy=savedLoadout.entries[6].activeMercy=7;DevilContracts.Save(savedLoadout);
            RunConsumables.ResetRun();DevilContracts.BeginRun();RunConsumables.ResetRun();DevilContracts.DeliverStarterRelic();
            Check(RunConsumables.Load().bag.Count==1&&RunConsumables.Get(RunConsumables.Load().bag[0]).price==300,"starter relic survives battle initialization");
            DevilContracts.DeliverStarterRelic();Check(RunConsumables.Load().bag.Count==1,"starter relic delivered once");
            DevilContracts.SaveRun(new DevilContracts.RunState{active=true,id=9,mercy=2});Check(DevilContracts.FreeOfudaReroll(false),"free reroll available");Check(DevilContracts.FreeOfudaReroll(true)&&!DevilContracts.FreeOfudaReroll(false),"free reroll consumed once");
            PlayerPrefs.SetString(SeventeenStepsMode.ModeKey,SeventeenStepsMode.ModeValue);Check(!DevilContracts.Active&&DevilContracts.Outgoing(1000,w,out _,out _)==1000,"gaiden excluded");Check(!DevilContracts.Grant(0),"gaiden cannot grant");
            UnityEngine.Object.DestroyImmediate(owner);owner=null;PlayerPrefs.SetString(SeventeenStepsMode.ModeKey,"Normal");
            SpecialTileSystem.SetGems(22);var save=DevilContracts.Load();save.equipped=10;save.entries[10].mercy=save.entries[10].power=save.entries[10].activeMercy=save.entries[10].activePower=10;DevilContracts.Save(save);
            preview=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("DevilContracts/ContractView"));var view=preview.GetComponent<DevilContractView>();typeof(DevilContractView).GetField("selected",flags).SetValue(view,10);view.Refresh();
            BattleHUDQA.Capture("Logs/DevilContracts/Contract.png",1920,1080);BattleHUDQA.Capture("Logs/DevilContracts/ContractTablet.png",1440,1080);
            foreach(var text in preview.GetComponentsInChildren<TMPro.TMP_Text>(true)){if(!text.gameObject.activeInHierarchy)continue;text.ForceMeshUpdate();Check(!text.isTextOverflowing,"text overflow: "+text.transform.parent.name+"/"+text.name+" "+text.text+" size="+text.rectTransform.rect.size+" point="+text.fontSize+" lines="+text.textInfo.lineCount);}
            foreach(var language in new[]{LocalizationManager.Language.Japanese,LocalizationManager.Language.English,LocalizationManager.Language.ChineseSimplified}){
                LocalizationManager.Instance.SetLanguage(language);
                for(int id=0;id<11;id++){
                    typeof(DevilContractView).GetField("selected",flags).SetValue(view,id);view.Refresh();Canvas.ForceUpdateCanvases();
                    foreach(var t in preview.GetComponentsInChildren<TMPro.TMP_Text>(true)){if(!t.gameObject.activeInHierarchy)continue;t.ForceMeshUpdate();foreach(char c in System.Text.RegularExpressions.Regex.Replace(t.text,"<[^>]+>",""))if(!char.IsWhiteSpace(c))Check(t.font.HasCharacter(c,true,true),"missing glyph: "+language+" "+c+" in "+t.text);Check(!t.isTextOverflowing,"localized overflow: "+language+" "+id+" "+t.transform.parent.name+"/"+t.name+" "+t.text);}
                }
            }
            BattleHUDQA.Capture("Logs/DevilContracts/ContractChinese.png",1920,1080);
            LocalizationManager.Instance.SetLanguage(LocalizationManager.Language.English);view.Refresh();BattleHUDQA.Capture("Logs/DevilContracts/ContractEnglish.png",1920,1080);
            var ownedSave=DevilContracts.Load();var lockedSave=JsonUtility.FromJson<DevilContracts.SaveData>(JsonUtility.ToJson(ownedSave));
            lockedSave.equipped=-1;foreach(var entry in lockedSave.entries)entry.unlocked=0;DevilContracts.Save(lockedSave);
            for(int id=0;id<11;id++){
                typeof(DevilContractView).GetField("selected",flags).SetValue(view,id);view.Refresh();
                Check(view.devilName.text=="？？？"&&view.contractName.text=="？？？","locked names hidden");
                Check(view.benefit.text.EndsWith("？？？")&&view.drawback.text.EndsWith("？？？")&&view.synergy.text.EndsWith("？？？"),"locked effects hidden");
                for(int n=0;n<30;n++)Check(view.nodeTitles[n].text=="？？？"&&view.nodeBodies[n].text=="？？？"&&view.nodeStates[n].text=="？？？"&&!view.nodes[n].interactable,"locked tree hidden");
                Check(view.entryLabels[id].text.StartsWith("？？？\n"),"locked list name hidden");
                Check(view.entryLabels[id].text.Contains(id==10?"？？？</size>":DevilContractCatalog.Get(id).god.Text) || id==10 && !view.entryLabels[id].text.Contains(DevilContractCatalog.Get(id).god.Text),"acquisition condition visibility");
                typeof(DevilContractView).GetMethod("SelectNode",flags).Invoke(view,new object[]{0});Check(!view.confirmRoot.activeSelf,"locked confirmation blocked");
            }
            BattleHUDQA.Capture("Logs/DevilContracts/ContractLocked.png",1920,1080);DevilContracts.Save(ownedSave);
            UnityEngine.Object.DestroyImmediate(preview);preview=null;
            File.WriteAllText("Logs/DevilContracts/Verified.txt","PASS: 11 contracts / 330 nodes; permanent acquisition; duplicate protection; 10 ranks, 2–20 gem research; insufficient-gem handling; opt-in stages; frozen run loadout; all 11 base effects; stack caps; damage preview purity; temporary passive scoring; suspend roundtrip; gaiden exclusion; actual/overflow HP recovery; starter relic delivery after initialization; one-time free reroll; PC/tablet render; all 11 contracts in three languages without text overflow. Test preferences restored.");
        }finally{
            if(owner)UnityEngine.Object.DestroyImmediate(owner);if(preview)UnityEngine.Object.DestroyImmediate(preview);
            LocalizationManager.Instance.SetLanguage(originalLanguage);
            for(int i=0;i<keys.Length;i++){if(had[i])PlayerPrefs.SetString(keys[i],values[i]);else PlayerPrefs.DeleteKey(keys[i]);}
            if(hadGems)SpecialTileSystem.SetGems(gems);else PlayerPrefs.DeleteKey("SP_Gems");PlayerPrefs.Save();
        }
    }
}
