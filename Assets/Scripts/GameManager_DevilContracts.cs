using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public partial class GameManager
{
    string contractScoringText="";
    int ContractsTraitLevel(int level,string skill,SkillSetAsset.Trait trait,string yaku)
    {
        if(level>0||!DevilContracts.Active)return level;var s=DevilContracts.Run();
        return s.id==10&&s.temporarySkill==skill&&s.temporaryTrait==(int)trait&&
            NormalizeTraitJudgeYakuName_Local(s.temporaryYaku)==NormalizeTraitJudgeYakuName_Local(yaku)?1:level;
    }
    int ContractsMaxHp(int maximum)=>DevilContracts.Active?Mathf.Max(1,Mathf.RoundToInt(maximum*DevilContracts.HpScale(DevilContracts.Run()))):maximum;
    int ContractsMaxMp(int maximum)=>DevilContracts.Active?Mathf.Max(0,Mathf.RoundToInt(maximum*DevilContracts.MpScale(DevilContracts.Run()))):maximum;
    int ContractsSkillHpCost()=>DevilContracts.Active?Mathf.CeilToInt(playerMaxHP*DevilContracts.SkillHpCost(DevilContracts.Run())):0;
    void ContractsSkillUsed()
    {
        if(!DevilContracts.Active)return;var s=DevilContracts.Run();int hpCost=ContractsSkillHpCost();
        playerHP=Mathf.Max(1,playerHP-hpCost);s.skillUses++;var active=ResolveActiveSkillForMP().ToString();if(active=="RandomMan"||active=="RandomHonor")s.transforms++;DevilContracts.SaveRun(s);UpdateHpUI();
    }
    void ContractsCheckLowHp()
    {
        if(!DevilContracts.Active||playerHP<0)return;var s=DevilContracts.Run();
        if(s.id==7&&s.mercy>=2&&!s.lowHpTriggered&&playerHP<=playerMaxHP*.25f){s.lowHpTriggered=s.lowHpShield=true;DevilContracts.SaveRun(s);}
    }
    IEnumerator ContractsBeginRound()
    {
        if(!DevilContracts.Active)yield break;
        var s=DevilContracts.Run();int enemy=ProgressionFlowController.GetCurrentEnemyIndex();
        bool newEnemy=s.enemy!=enemy;
        if(newEnemy){s.hpCharge=s.mpCharge=0;s.relicStacks=s.playerWins=s.incomingWins=0;s.lowHpShield=s.lowHpTriggered=s.freeHpRelic=false;}
        if(s.enemy==enemy&&s.round==roundNumber)yield break;
        s.enemy=enemy;s.round=roundNumber;s.skillUses=s.transforms=0;
        string previous=s.temporaryYaku+"/"+s.temporaryTrait;
        s.temporaryYaku="";s.temporarySkill="";s.temporaryTrait=0;
        var notice="";
        if(s.id==10){
            var (ge,sh,iy,host)=GetCurrentSkillTraitYakuForScoring();string skill=ResolveActiveSkillForMP().ToString();
            var candidates=new List<(SkillSetAsset.Trait trait,string yaku)>();
            if(host)foreach(var pair in new[]{(SkillSetAsset.Trait.Geki,ge),(SkillSetAsset.Trait.Shun,sh),(SkillSetAsset.Trait.Iyu,iy)})
                foreach(string y in pair.Item2??new List<string>())if(host.GetTraitYakuLevel(skill,pair.Item1,y)<=0)candidates.Add((pair.Item1,y));
            if(candidates.Count>1)candidates.RemoveAll(c=>c.yaku+"/"+(int)c.trait==previous);
            if(candidates.Count>0){
                var pick=candidates[UnityEngine.Random.Range(0,candidates.Count)];s.temporaryYaku=pick.yaku;s.temporarySkill=skill;s.temporaryTrait=(int)pick.trait;
                string kind=pick.trait==SkillSetAsset.Trait.Geki?"撃":pick.trait==SkillSetAsset.Trait.Shun?"瞬":"癒";
                string display=pick.yaku;try {var k=NormalizeTraitJudgeYakuName_Local(pick.yaku);var localized=LocalizationManager.Yaku(pick.yaku);if(!string.IsNullOrEmpty(localized)&&!localized.StartsWith("yaku."))display=localized;}catch{}
                notice=ReplaceTraitWordsWithIcons(kind)+"　"+display+"　Lv.1\n"+GameUIText.Get("この局だけ一時解放","Temporarily unlocked for this hand","仅本局临时解锁");
            }else notice=GameUIText.Get("すべてのパッシブスキルが解放済み","All passive skills are already unlocked","全部被动技能已解锁");
        }
        DevilContracts.SaveRun(s);
        if(newEnemy){
            if(s.pendingHpHeal>0){playerHP=Mathf.Min(playerMaxHP,playerHP+Mathf.CeilToInt(playerMaxHP*s.pendingHpHeal*.01f));s.pendingHpHeal=0;}
            if(s.pendingMpHeal>0){_mp=ClampToEffectiveMaxMP(_mp+Mathf.CeilToInt(EffectiveMaxMP()*s.pendingMpHeal*.01f));s.pendingMpHeal=0;}
            DevilContracts.SaveRun(s);
        }
        RefreshAll();UpdateSkillInfoUI();UpdateHpUI();UpdateMpUI();
        if(!string.IsNullOrEmpty(notice)){
            var prefab=Resources.Load<DevilContractNotice>("DevilContracts/PassiveNotice");
            if(prefab){var view=Instantiate(prefab);ApplyTraitSpriteAssetToTMP(view.body as TextMeshProUGUI);yield return view.Play(GameUIText.Get("ケルベロスの加護","Cerberus's blessing","刻耳柏洛斯的庇护"),notice);Destroy(view.gameObject);}
        }
        if(s.id==1&&hand.Select(t=>StripTileIdForLogic(t)).Any(t=>t.StartsWith("Man"))&&hand.Any(t=>StripTileIdForLogic(t).StartsWith("Pin"))&&hand.Any(t=>StripTileIdForLogic(t).StartsWith("Sou"))){
            float loss=Mathf.Lerp(.10f,0,DevilContracts.Restraint(s));_mp=Mathf.Max(0,_mp-Mathf.CeilToInt(EffectiveMaxMP()*loss));UpdateMpUI();
        }
    }
    float ContractsHpRecoveryScale(bool free=false)
    {
        if(!DevilContracts.Active||free)return 1;var s=DevilContracts.Run();return s.id==4?(Mathf.Lerp(.8f,1,DevilContracts.Restraint(s))):1;
    }
    float ContractsMpRecoveryScale()=>DevilContracts.Active&&DevilContracts.Run().id==8?1+(DevilContracts.Run().power>=1?.5f:.3f)*DevilContracts.Strength(DevilContracts.Run()):1;
    void ContractsRecordHpRecovery(int amount)
    {
        if(!DevilContracts.Active||amount<=0)return;var s=DevilContracts.Run();if(s.id!=4)return;
        int actual=Mathf.Min(amount,Mathf.Max(0,playerMaxHP-playerHP));float eligible=actual+(s.power>=2?Mathf.Max(0,amount-actual)*.5f:0);
        float rate=(s.power>=3?.60f:s.power>=1?.45f:.30f)*DevilContracts.Strength(s);
        s.hpCharge=Mathf.Min(s.hpCharge+eligible*rate,playerMaxHP*(s.power>=3?.25f:.15f)*DevilContracts.Strength(s));DevilContracts.SaveRun(s);
    }
    void ContractsRecordMpRecovery(int amount)
    {
        if(!DevilContracts.Active||amount<=0)return;var s=DevilContracts.Run();if(s.id!=8||s.power<2)return;
        int overflow=Mathf.Max(0,_mp+amount-EffectiveMaxMP());s.mpCharge=Mathf.Min(s.mpCharge+overflow*playerMaxHP*.002f*DevilContracts.Strength(s),playerMaxHP*.10f*DevilContracts.Strength(s));DevilContracts.SaveRun(s);
    }
    void ContractsRecoverHp(int amount,bool free=false)
    {
        int adjusted=Mathf.Max(0,Mathf.RoundToInt(amount*ContractsHpRecoveryScale(free)));ContractsRecordHpRecovery(adjusted);playerHP=Mathf.Min(playerMaxHP,playerHP+adjusted);
    }
    void ContractsRecoverMp(int amount)
    {
        int adjusted=Mathf.Max(0,Mathf.RoundToInt(amount*ContractsMpRecoveryScale()));ContractsRecordMpRecovery(adjusted);_mp=ClampToEffectiveMaxMP(_mp+adjusted);
    }
    void ContractsRelicUsed(int id)
    {
        if(!DevilContracts.Active)return;var s=DevilContracts.Run();
        if(s.id==6){s.relicStacks=Mathf.Min(s.relicStacks+1,s.power>=3?3:2);DevilContracts.SaveRun(s);if(s.power>=2)ContractsRecoverHp(Mathf.CeilToInt(playerMaxHP*.03f));}
    }
    bool ContractsFreeHpRelic(float hp)
    {
        if(hp<=0||!DevilContracts.Active)return false;var s=DevilContracts.Run();if(s.id!=4||s.mercy<2||s.freeHpRelic)return false;s.freeHpRelic=true;DevilContracts.SaveRun(s);return true;
    }
    void ContractsModifyScoring(ref int damage,ref int hpHeal,ref int mpHeal,List<string> yaku,float ge,float sh,float iy)
    {
        contractScoringText="";if(!DevilContracts.Active)return;var s=DevilContracts.Run();
        int passives=(SumTraitPctByYaku(yaku,SkillSetAsset.Trait.Geki,0)>0?1:0)+(SumTraitPctByYaku(yaku,SkillSetAsset.Trait.Shun,0)>0?1:0)+(SumTraitPctByYaku(yaku,SkillSetAsset.Trait.Iyu,0)>0?1:0);var conditions=new HashSet<string>();
        foreach(var id in OfudaRunInventory.LoadList()){var split=id.Split(new[]{"__"},StringSplitOptions.None);if(split.Length==2&&Ofuda_Cond_Passes_Runtime(split[0]))conditions.Add(split[0]);}
        bool colored=yaku!=null&&yaku.Any(y=>{var n=NormalizeTraitJudgeYakuName_Local(y);return n.Contains("混一色")||n.Contains("清一色")||n.Contains("ホンイツ")||n.Contains("チンイツ")||n.Contains("honitsu")||n.Contains("chinitsu")||n.Contains("halfflush")||n.Contains("fullflush");});
        float ofuda=1;int extra=0;ApplyRunOfudaModifiers(yaku,ref ofuda,ref extra,null);
        int original=damage, originalHp=hpHeal, originalMp=mpHeal;
        damage=DevilContracts.Outgoing(damage,new DevilContracts.WinContext{colored=colored,tsumo=_lastScoringIsTsumo,ofudaTypes=conditions.Count,passiveTypes=passives,hp=playerHP,maxHp=playerMaxHP,ofudaMultiplier=ofuda},out var multiplier,out var bonus);
        int extraHp=0,extraMp=0;
        if(s.id==1&&s.mercy>=2&&colored)extraHp=Mathf.CeilToInt(playerMaxHP*.05f);
        if(s.id==2&&s.mercy>=2&&s.skillUses==0)extraMp=Mathf.CeilToInt(EffectiveMaxMP()*.10f);
        if(s.id==7&&s.power>=2&&playerHP<=(s.power>=1?.60f:.50f)*playerMaxHP)extraHp=Mathf.CeilToInt(playerMaxHP*.05f);
        if(s.id==10&&s.power>=2&&passives>=3){hpHeal+=Mathf.RoundToInt(Mathf.Max(0,/* passive only */_pendingContractTraitHp)*.30f);mpHeal+=Mathf.RoundToInt(Mathf.Max(0,_pendingContractTraitMp)*.30f);}
        hpHeal=Mathf.RoundToInt((hpHeal+extraHp)*ContractsHpRecoveryScale());mpHeal=Mathf.RoundToInt((mpHeal+extraMp)*ContractsMpRecoveryScale());
        s.hpCharge=s.mpCharge=0;s.relicStacks=0;s.playerWins++;DevilContracts.SaveRun(s);
        var def=DevilContractCatalog.Get(s.id);
        RecordContractDelta("ダメージ", "Damage", "伤害", original, damage);
        RecordContractDelta("HP回復", "HP recovery", "HP恢复", originalHp, hpHeal);
        RecordContractDelta("MP回復", "MP recovery", "MP恢复", originalMp, mpHeal);
        if(s.id==10&&!string.IsNullOrEmpty(s.temporaryYaku)&&scoringPassiveHits.ContainsKey(NormalizeTraitJudgeYakuName_Local(s.temporaryYaku)))
            contractWinEffects.Add(GameUIText.Get("一時解放パッシブ発動：", "Temporary passive triggered: ", "临时被动发动：")+LocalizeSpecialTileYakuName_Local(s.temporaryYaku));
        if(def!=null&&contractWinEffects.Count>0)contractScoringText=string.Join(" / ",contractWinEffects);
    }
    int _pendingContractTraitHp,_pendingContractTraitMp;
    void ContractsAppendScoring(bool player)
    {
        ConfigureAppliedScoringEffects(player);
    }
}
