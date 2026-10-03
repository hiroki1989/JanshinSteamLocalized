using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SpecialTilePassiveQA
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static void Check(bool ok,string message){if(!ok)throw new Exception("Special tile passive QA: "+message);}
    static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,F).Invoke(o,args);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,F).SetValue(o,value);
    static SpecialTileSystem.Entry Tile(int seed,string packed)=>new SpecialTileSystem.Entry{baseType=(SpecialTileSystem.BaseType)((seed-1)%3),rarity=SpecialTileSystem.Rarity.Rare,seed=seed,traitBonusPacked=packed};
    public static void Run()
    {
        string company=PlayerSettings.companyName;var language=LocalizationManager.Instance.CurrentLanguage;GameObject owner=null,shopOwner=null;SkillSetAsset host=null;
        GameManager gm=null;UpgradeManager shop=null;
        try{
            PlayerSettings.companyName="JanshinSpecialTilePassiveQA";
            PlayerPrefs.DeleteAll();PlayerPrefs.SetString(SeventeenStepsMode.ModeKey,"Normal");PlayerPrefs.SetString("EquippedActiveSkill","RandomMan");PlayerPrefs.SetInt("SP_EquipSlots",4);
            PlayerPrefs.SetString("PF_TraitUpgradeDelta_Geki","0.10");PlayerPrefs.SetString("PF_TraitUpgradeDelta_Shun","0.05");PlayerPrefs.SetString("PF_TraitUpgradeDelta_Iyu","0.02");
            var a=Tile(1,"三カンツ=1");var b=Tile(2,"SANKANTSU=1;風牌=1");var c=Tile(3,"四カンツ=1;九蓮宝灯=1");
            Check(SpecialTileSystem.TryEquipAppend(a)&&SpecialTileSystem.TryEquipAppend(b)&&SpecialTileSystem.TryEquipAppend(c),"fixture equipment");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            host=ScriptableObject.CreateInstance<SkillSetAsset>();host.id="QA_SPECIAL_PASSIVE";host.displayName="QA";
            host.activeSkills=new List<SkillSetAsset.SkillEntry>{new SkillSetAsset.SkillEntry{activeSkillName="RandomMan",displayName="色寄せ",gekiYaku=new List<string>{"平和","三槓子","風牌"},shunYaku=new List<string>{"タンヤオ","四槓子"},iyuYaku=new List<string>{"七対子","九蓮宝燈"}}};
            host.traitMap=new List<SkillSetAsset.YakuTraitEntry>{
                new SkillSetAsset.YakuTraitEntry{yakuName="三カンツ",trait=SkillSetAsset.Trait.Geki,difficulty=SkillSetAsset.YakuDifficulty.Hard},
                new SkillSetAsset.YakuTraitEntry{yakuName="平和",trait=SkillSetAsset.Trait.Geki,difficulty=SkillSetAsset.YakuDifficulty.Easy},
                new SkillSetAsset.YakuTraitEntry{yakuName="役牌",trait=SkillSetAsset.Trait.Geki,difficulty=SkillSetAsset.YakuDifficulty.Easy},
                new SkillSetAsset.YakuTraitEntry{yakuName="四カンツ",trait=SkillSetAsset.Trait.Shun,difficulty=SkillSetAsset.YakuDifficulty.Hard},
                new SkillSetAsset.YakuTraitEntry{yakuName="九蓮宝灯",trait=SkillSetAsset.Trait.Iyu,difficulty=SkillSetAsset.YakuDifficulty.Yakuman}};
            host.gekiMultiplierByDiff=new[]{1.1f,1.2f,1.3f,1.4f};host.shunMpPctByDiff=new[]{.1f,.2f,.3f,.4f};host.iyuHealMulByDiff=new[]{.1f,.2f,.3f,.4f};
            host.AddTraitYakuLevel("RandomMan",SkillSetAsset.Trait.Geki,"三槓子",2);host.AddTraitYakuLevel("RandomMan",SkillSetAsset.Trait.Shun,"四槓子",1);

            owner=new GameObject("PassiveBattleQA");gm=owner.AddComponent<GameManager>();Set(gm,"_skillSet",host);
            var battleText=new GameObject("BattlePassiveText",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();battleText.transform.SetParent(owner.transform);Set(gm,"_skillTraitGekiTMP",battleText);Call(gm,"OnEnable");
            // A previous scoring cache must never override the equipped loadout.
            var oldCache=(Dictionary<string,int>)typeof(GameManager).GetField("_specialTileTraitLvBonusThisScoring",F).GetValue(gm);oldCache["三カンツ"]=99;
            shopOwner=new GameObject("PassiveShopQA");shop=shopOwner.AddComponent<UpgradeManager>();Set(shop,"_traitHostSet",host);Set(shop,"_traitActiveSkillName","RandomMan");Set(shop,"_upgradeOfferTrait",SkillSetAsset.Trait.Geki);Set(shop,"_upgradeOfferYakuName","SANKANTSU");
            var shopText=new GameObject("ShopPassiveText",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();shopText.transform.SetParent(shopOwner.transform);Set(shop,"traitUpgradeOfferTMP",shopText);Call(shop,"OnEnable");
            foreach(var lang in new[]{LocalizationManager.Language.Japanese,LocalizationManager.Language.English,LocalizationManager.Language.ChineseSimplified}){
                LocalizationManager.Instance.SetLanguage(lang);Set(gm,"_skillSet",host);Set(shop,"_traitHostSet",host);Set(shop,"_traitActiveSkillName","RandomMan");Set(shop,"_upgradeOfferTrait",SkillSetAsset.Trait.Geki);Set(shop,"_upgradeOfferYakuName","SANKANTSU");
                foreach(var key in new[]{"三槓子","三カンツ","三杠子","SANKANTSU","yaku.SANKANTSU","Sankantsu","三槓子(+2)",LocalizationManager.Instance.GetYakuDisplayName("SANKANTSU")})Check(SpecialTileSystem.GetEquippedTraitBonusLv(key)==2,"alias total "+lang+" "+key);
                Check(SpecialTileSystem.GetEquippedTraitBonusLv("役牌")==1&&SpecialTileSystem.GetEquippedTraitBonusLv("風牌")==1,"wind/value alias");
                Check((int)Call(gm,"GetTraitEffectiveLevelForScoring",host,"RandomMan",SkillSetAsset.Trait.Geki,"三槓子")==4,"scoring effective level/cache actual="+Call(gm,"GetTraitEffectiveLevelForScoring",host,"RandomMan",SkillSetAsset.Trait.Geki,"三槓子")+" base="+host.GetTraitYakuLevel("RandomMan",SkillSetAsset.Trait.Geki,"三槓子")+" relic="+RunConsumables.TraitBonus(SkillSetAsset.Trait.Geki));
                Check((int)Call(gm,"GetTraitEffectiveLevelForScoring",host,"RandomMan",SkillSetAsset.Trait.Iyu,"九蓮宝燈")==1,"base-zero passive activated by equipped bonus");
                Check(Mathf.Abs((float)Call(gm,"SumTraitPctByYaku",new List<string>{LocalizationManager.Instance.GetYakuDisplayName("SANKANTSU")+"(+2)"},SkillSetAsset.Trait.Geki,0f)-.6f)<.0001f,"actual damage percentage "+lang);
                Check(Mathf.Abs((float)Call(gm,"SumTraitPctByYaku",new List<string>{"四槓子"},SkillSetAsset.Trait.Shun,0f)-.35f)<.0001f,"actual MP percentage "+lang);
                Check(Mathf.Abs((float)Call(gm,"SumTraitPctByYaku",new List<string>{"九蓮宝燈"},SkillSetAsset.Trait.Iyu,0f)-.4f)<.0001f,"actual HP percentage "+lang);
                Call(gm,"UpdateRightInfoUI_Manual");Check(battleText.text.Contains("Lv.4 60%"),"battle level and percentage "+lang+" "+battleText.text);
                Check(battleText.text.Contains("Lv.1"),"initial first role level before first label");
                Call(shop,"RefreshTraitOfferPresentation");Check(shopText.text.Contains("60%")&&shopText.text.Contains("70%"),"shop current/next percentage "+lang+" "+shopText.text);
                Check((int)Call(shop,"__GetLastSpecialTileTraitBonusForYaku","SANKANTSU")==2,"shop bonus alias");
            }
            var extra=Tile(4,"三槓子=1");Check(SpecialTileSystem.TryEquipAppend(extra),"extra equipment");
            Check(battleText.text.Contains("Lv.5 70%"),"battle label not updated on equip");Check(shopText.text.Contains("70%")&&shopText.text.Contains("80%"),"shop label not updated on equip");
            Check((string)typeof(UpgradeManager).GetField("_upgradeOfferYakuName",F).GetValue(shop)=="SANKANTSU","equipment refresh rerolled offer");
            SpecialTileSystem.UnequipAt(3);Check(battleText.text.Contains("Lv.4 60%")&&shopText.text.Contains("60%"),"unequip refresh");
            var replacement=Tile(5,"三槓子=2");Check(SpecialTileSystem.TryEquipReplaceAt(0,replacement),"replacement");Check(battleText.text.Contains("Lv.5 70%"),"replace refresh");
            SpecialTileSystem.AddOwned(replacement);SpecialTileSystem.DiscardOwned(replacement);Check(battleText.text.Contains("Lv.3 50%"),"discard equipped item refresh");
            while(SpecialTileSystem.GetEquipped().Count>0)SpecialTileSystem.UnequipAt(0);
            Check((int)Call(gm,"GetTraitEffectiveLevelForScoring",host,"RandomMan",SkillSetAsset.Trait.Geki,"三槓子")==2,"removed equipment reused stale score cache");
            Check(battleText.text.Contains("Lv.2 40%")&&shopText.text.Contains("40%"),"removed bonuses remain in labels");
            Directory.CreateDirectory("Logs/SpecialTilePassives");File.WriteAllText("Logs/SpecialTilePassives/Verified.txt","PASS: JA/EN/ZH and Japanese/internal/kan/wind aliases; stacked equipment; live effective levels rather than stale scoring cache; actual damage, MP and HP percentages; initial level timing; battle and shop current/next effects; immediate equip/replace/unequip/discard refresh; shop offer preserved; base purchase level untouched. Isolated QA preferences.");
        }finally{
            if(gm)Call(gm,"OnDisable");if(shop)Call(shop,"OnDisable");if(owner)UnityEngine.Object.DestroyImmediate(owner);if(shopOwner)UnityEngine.Object.DestroyImmediate(shopOwner);if(host)UnityEngine.Object.DestroyImmediate(host);
            LocalizationManager.Instance.SetLanguage(language);PlayerSettings.companyName=company;
        }
    }
}
