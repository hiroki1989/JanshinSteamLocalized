using System;
using System.Collections.Generic;
using UnityEngine;

// Presentation journal and claim receipts survive reloads independently of the current mission.
public static class MissionRewardSettlement
{
    public const string NoticeKey="Mission_RewardNotice_V2", QueueKey="Mission_DeferredRewards_V2", WonKey="Mission_LastEnemyWon_V2";
    [Serializable] public class Notice
    {
        public int stage=1, gold, devilId=-1;
        public bool newContract;
        public string missionName,claimKey;
    }
    [Serializable] public class Deferred {public List<Notice> entries=new List<Notice>();}
    public static Notice Read(){try{return JsonUtility.FromJson<Notice>(PlayerPrefs.GetString(NoticeKey,""));}catch{return null;}}
    public static void Save(Notice n){PlayerPrefs.SetString(NoticeKey,JsonUtility.ToJson(n));PlayerPrefs.Save();}
    static Deferred Queue(){try{return JsonUtility.FromJson<Deferred>(PlayerPrefs.GetString(QueueKey,""))??new Deferred();}catch{return new Deferred();}}
    static Notice Current()
    {
        MissionSystem.Load();
        if(!MissionSystem.HasActiveMission||(!MissionSystem.HasPendingCompletion&&!MissionSystem.IsCompleted)||MissionSystem.IsAlreadyClaimed(MissionSystem.CurrentEnemyKey))return null;
        int id=PlayerPrefs.GetInt(MissionSystem.PendingDevilKey,-1);
        return new Notice{gold=MissionSystem.CurrentGold,missionName=MissionSystem.CurrentDisplayName,devilId=id,claimKey=MissionSystem.CurrentClaimKey,
            newContract=id>=0&&id<11&&DevilContracts.Load().entries[id].unlocked==0};
    }
    // Zeus's secret route has no shop. Preserve its earned reward across the next god's assignment.
    public static void DeferCurrent()
    {
        var n=Current();if(n==null)return;var q=Queue();
        if(!q.entries.Exists(x=>x.claimKey==n.claimKey))q.entries.Add(n);
        PlayerPrefs.SetString(QueueKey,JsonUtility.ToJson(q));PlayerPrefs.DeleteKey(MissionSystem.PendingKey);PlayerPrefs.Save();
    }
    public static Notice Begin(bool allowCurrent=true)
    {
        if(SeventeenStepsMode.IsActive)return null;
        MissionSystem.Load();
        var n=Read();
        if(n==null)
        {
            var q=Queue();
            if(q.entries.Count>0){n=q.entries[0];Save(n);q.entries.RemoveAt(0);PlayerPrefs.SetString(QueueKey,JsonUtility.ToJson(q));PlayerPrefs.Save();}
            else {if(!allowCurrent)return null;n=Current();if(n==null)return null;Save(n);}
        }
        if(PlayerPrefs.GetInt(n.claimKey,0)==0)
        {
            PlayerPrefs.SetInt(n.claimKey,1);
            GameManager.RunCurrency.Add(n.gold);
        }
        if(n.claimKey==MissionSystem.CurrentClaimKey)MissionSystem.ConfirmSettlement();
        if(n.newContract)DevilContracts.Grant(n.devilId);
        return n;
    }
    public static void Acknowledge(Notice n)
    {
        if(n.stage==1&&n.newContract){n.stage=2;Save(n);}
        else {PlayerPrefs.DeleteKey(NoticeKey);PlayerPrefs.Save();}
    }
}
