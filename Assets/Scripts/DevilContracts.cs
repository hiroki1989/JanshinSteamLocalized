using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Permanent ownership and research are deliberately separate from the frozen run loadout.
public static class DevilContracts
{
    public const string SaveKey = "DevilContracts_Permanent_V1", RunKey = "DevilContracts_Run_V1";
    [Serializable] public class Progress { public int unlocked, mercy, power, activeMercy, activePower, resonance, activeResonance; }
    [Serializable] public class SaveData { public int version; public int equipped=-1; public Progress[] entries=new Progress[11]; }
    [Serializable] public class RunState
    {
        public bool active; public int version, mercyRank, powerRank, resonanceRank; public int id=-1, mercy, power, enemy=-1, round=-1;
        public int destroyed, spent, relicStacks, skillUses, transforms, playerWins, incomingWins, pendingHpHeal, pendingMpHeal;
        public float hpCharge, mpCharge;
        public bool lowHpShield, lowHpTriggered, shopPurchase, shopReroll, shopDestroy, freeOfudaUsed, freeHpRelic, startingRelicPending;
        public string temporaryYaku="", temporarySkill=""; public int temporaryTrait;
    }
    public static event Action Changed;
    public static event Action<int> Acquired;
    public static SaveData Load()
    {
        SaveData s=null; try {s=JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey,""));} catch {}
        if(s==null)s=new SaveData();
        if(s.entries==null||s.entries.Length!=11)Array.Resize(ref s.entries,11);
        for(int i=0;i<11;i++)if(s.entries[i]==null)s.entries[i]=new Progress();
        if(s.version<2){foreach(var e in s.entries){e.mercy=OldRank(e.mercy);e.power=OldRank(e.power);e.activeMercy=OldRank(e.activeMercy);e.activePower=OldRank(e.activePower);}s.version=2;PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(s));PlayerPrefs.Save();}
        s.equipped=Mathf.Clamp(s.equipped,-1,10); return s;
    }
    public static void Save(SaveData s){PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(s));PlayerPrefs.Save();Changed?.Invoke();}
    public static RunState Run()
    {
        try {var s=JsonUtility.FromJson<RunState>(PlayerPrefs.GetString(RunKey,""));if(s==null)s=new RunState();if(s.version<2){s.mercyRank=OldRank(s.mercy);s.powerRank=OldRank(s.power);s.version=2;}return s;}catch{return new RunState();}
    }
    public static void SaveRun(RunState s){PlayerPrefs.SetString(RunKey,JsonUtility.ToJson(s));PlayerPrefs.Save();}
    public static bool Active => Run().active && !SeventeenStepsMode.IsActive;
    public static bool Grant(int id)
    {
        if(id<0||id>=11||SeventeenStepsMode.IsActive)return false;
        var s=Load();if(s.entries[id].unlocked!=0)return false;
        s.entries[id].unlocked=1;Save(s);Acquired?.Invoke(id);return true;
    }
    public static bool Equip(int id)
    {
        var s=Load();if(id < -1 || id>10 || (id>=0&&s.entries[id].unlocked==0))return false;
        s.equipped=id;Save(s);return true;
    }
    static int OldRank(int rank)=>rank==1?4:rank==2?7:rank==3?10:Mathf.Clamp(rank,0,10);
    public static int Tier(int rank)=>rank>=10?3:rank>=7?2:rank>=4?1:0;
    public static float Strength(RunState s)=>Mathf.Lerp(1f/3f,1,Mathf.Clamp01(s.powerRank/10f));
    public static float Restraint(RunState s)=>Mathf.Clamp01(s.mercyRank/10f);
    public static int Cost(int depth)=>Mathf.Clamp(depth,1,10)*2;
    public static bool ResearchBranch(int id,int branch){
        if(branch<2)return Research(id,branch==1);
        if(id<0||id>10)return false;var s=Load();var e=s.entries[id];
        if(e.unlocked==0||e.resonance>=10||!SpecialTileSystem.TryConsumeGems(Cost(e.resonance+1)))return false;
        e.resonance++;Save(s);return true;
    }
    public static void SetBranchDepth(int id,int branch,int depth){
        if(branch<2){SetDepth(id,branch==1,depth);return;}
        var s=Load();if(id<0||id>10||s.entries[id].unlocked==0)return;
        s.entries[id].activeResonance=Mathf.Clamp(depth,0,s.entries[id].resonance);Save(s);
    }
    public static bool Research(int id,bool power)
    {
        if(id<0||id>10)return false;var s=Load();var e=s.entries[id];int n=power?e.power:e.mercy;
        if(e.unlocked==0||n>=10||!SpecialTileSystem.TryConsumeGems(Cost(n+1)))return false;
        if(power)e.power++;else e.mercy++;
        // Research does not automatically enable a new drawback.
        Save(s);return true;
    }
    public static void SetDepth(int id,bool power,int depth)
    {
        var s=Load();if(id<0||id>10||s.entries[id].unlocked==0)return;var e=s.entries[id];
        if(power)e.activePower=Mathf.Clamp(depth,0,e.power);else e.activeMercy=Mathf.Clamp(depth,0,e.mercy);Save(s);
    }
    public static void BeginRun()
    {
        var data=Load();int id=data.equipped;var s=new RunState{active=true,id=id,version=2};
        if(id>=0&&data.entries[id].unlocked!=0){s.mercyRank=Mathf.Min(data.entries[id].activeMercy,data.entries[id].mercy);s.powerRank=Mathf.Min(data.entries[id].activePower,data.entries[id].power);s.resonanceRank=Mathf.Min(data.entries[id].activeResonance,data.entries[id].resonance);s.mercy=Tier(s.mercyRank);s.power=Tier(s.powerRank);}else s.id=-1;
        SaveRun(s);
        s.startingRelicPending=s.id==6&&s.mercy>=2;SaveRun(s);
    }
    public static void DeliverStarterRelic()
    {
        if(!Active)return;var s=Run();if(!s.startingRelicPending)return;
        s.startingRelicPending=false;SaveRun(s);
        var bag=RunConsumables.Load();if(bag.bag.Count>=RunConsumables.Capacity)return;
        var ids=new List<int>();for(int i=1;i<=20;i++)if(RunConsumables.Get(i).price==300)ids.Add(i);
        if(ids.Count>0){bag.bag.Add(ids[UnityEngine.Random.Range(0,ids.Count)]);RunConsumables.Save(bag);}
    }
    public static void EndRun(){PlayerPrefs.DeleteKey(RunKey);PlayerPrefs.Save();}

    public static float HpScale(RunState s)
    {
        float down=0;
        if(s.id==0)down=Mathf.Lerp(.10f,.04f,Restraint(s));
        if(s.id==0&&s.power>=3)down+=.05f;
        if(s.id==3&&s.power>=3)down+=.10f;
        if(s.id==9&&s.power>=3)down+=.15f;
        if(s.id==10&&s.power>=3)down+=.10f;
        return 1-down;
    }
    public static float MpScale(RunState s)=>s.power>=3?(s.id==6?.85f:s.id==8?.80f:s.id==10?.90f:1):1;
    public static float MpCostScale(RunState s)=>s.id==2?(Mathf.Lerp(1.2f,1,Restraint(s))):1;
    public static float SkillHpCost(RunState s)=>s.id==8?(s.mercy>=2&&s.skillUses==0?0:Mathf.Lerp(.03f,.01f,Restraint(s))):0;
    public static int TilePrice(int price)=>Active&&Run().id==3?Mathf.CeilToInt(price*(Mathf.Lerp(1.2f,1,Restraint(Run())))):price;
    public static int RelicPrice(int price)=>Active&&Run().id==6?Mathf.CeilToInt(price*(Mathf.Lerp(1.2f,1,Restraint(Run())))):price;
    public static void EnterShop()
    {
        if(!Active)return;var s=Run();s.shopPurchase=s.shopReroll=s.shopDestroy=s.freeOfudaUsed=false;SaveRun(s);
    }
    public static void Spent(int amount,bool reroll=false)
    {
        if(!Active||SceneManager.GetActiveScene().name!="UpgradeScene")return;var s=Run();s.spent+=Mathf.Max(0,amount);
        int refund=0;if(s.id==5){if(!reroll&&!s.shopPurchase&&s.mercy>=2)refund=50;if(reroll&&!s.shopReroll&&s.power>=2)s.pendingMpHeal+=10;}
        if(reroll)s.shopReroll=true;else s.shopPurchase=true;SaveRun(s);if(refund>0)GameManager.RunCurrency.Add(refund);
    }
    public static bool FreeOfudaReroll(bool consume)
    {
        if(!Active)return false;var s=Run();if(s.id!=9||s.mercy<2||s.freeOfudaUsed)return false;
        if(consume){s.freeOfudaUsed=true;SaveRun(s);}return true;
    }
    public static void Destroyed(int count)
    {
        if(!Active||count<=0)return;var s=Run();s.destroyed+=count;int refund=0;
        if(s.id==3){if(s.power>=2)s.pendingHpHeal+=3*count;if(s.mercy>=2&&!s.shopDestroy)refund=50;}
        s.shopDestroy=true;SaveRun(s);if(refund>0)GameManager.RunCurrency.Add(refund);
    }
    public static int Gold(int amount)
    {
        if(!Active)return amount;var s=Run();if(s.id!=5)return amount;
        float reduction=Mathf.Lerp(.15f,.05f,Restraint(s));if(s.power>=3)reduction+=.10f;
        return Mathf.Max(0,Mathf.RoundToInt(amount*(1-reduction)));
    }
    public static int Incoming(int damage,bool consume)
    {
        if(!Active)return damage;var s=Run();float rate=1;
        if(s.id==0&&s.mercy>=2&&s.incomingWins==0)rate-=.15f;
        if(s.id==4&&s.power>=3)rate+=.10f;
        if(s.id==7){rate+=Mathf.Lerp(.15f,.05f,Restraint(s));if(s.power>=3)rate+=.10f;if(s.lowHpShield)rate-=.30f;}
        if(consume){s.incomingWins++;s.lowHpShield=false;SaveRun(s);}return Mathf.Max(0,Mathf.RoundToInt(damage*rate));
    }
    public struct WinContext {public bool colored,tsumo;public int ofudaTypes,passiveTypes,hp,maxHp;public float ofudaMultiplier;}
    public static int Outgoing(int damage,WinContext w,out float multiplier,out int bonus)
    {
        multiplier=1;bonus=0;if(!Active)return damage;var s=Run();float pct=0;
        switch(s.id){
            case 0:pct=s.power>=3?.30f:s.power>=1?.20f:.15f;if(s.power>=2&&w.ofudaTypes>0)pct+=.10f;break;
            case 1:if(w.colored){pct=s.power>=3?.60f:s.power>=1?.40f:.30f;if(s.power>=2&&s.skillUses>0)pct+=.15f;}else if(s.power>=3)pct=-.20f;break;
            case 2:if(s.skillUses==0){pct=s.power>=3?.55f:s.power>=1?.35f:.25f;if(s.power>=2&&!w.tsumo)pct+=.15f;}else if(s.power>=3)pct=-.15f;break;
            case 3:pct=Mathf.Min(s.destroyed*(s.power>=3?.07f:s.power>=1?.05f:.04f),s.power>=3?.42f:s.power>=1?.30f:.24f);break;
            case 4:bonus=Mathf.RoundToInt(s.hpCharge);break;
            case 5:pct=Mathf.Min((s.spent/(s.power>=1?250:300))*(s.power>=3?.07f:.05f),s.power>=3?.35f:.25f);break;
            case 6:pct=s.relicStacks*(s.power>=1?.25f:.20f);break;
            case 7:if(w.hp<=(s.power>=1?.60f:.50f)*w.maxHp)pct=s.power>=3?.70f:.40f;break;
            case 8:bonus=Mathf.RoundToInt(s.mpCharge);break;
            case 9:if(w.ofudaTypes>=2){pct=s.power>=1?.65f:.50f;if(s.power>=2&&w.ofudaTypes>=3)pct+=.25f;if(s.power>=3&&w.ofudaMultiplier>1)pct+=((1+(w.ofudaMultiplier-1)*1.2f)/w.ofudaMultiplier)-1;}else if(w.ofudaTypes==0)pct=-(Mathf.Lerp(.25f,.05f,Restraint(s)));break;
            case 10:if(w.passiveTypes>=2)pct=s.power>=3&&w.passiveTypes>=3?1:s.power>=1?.65f:.50f;else if(!(s.mercy>=2&&s.playerWins==0))pct=-(Mathf.Lerp(.20f,0,Restraint(s)));break;
        }
        if(pct>0)pct*=Strength(s);if(w.ofudaTypes>=2||w.passiveTypes>=2)pct+=s.resonanceRank*.01f;
        multiplier=Mathf.Max(0,1+pct);return Mathf.Max(0,Mathf.RoundToInt(damage*multiplier)+bonus);
    }
}
