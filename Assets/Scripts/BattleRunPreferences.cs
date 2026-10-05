using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// A battle checkpoint includes purchases made earlier in this run, not account progress.
[Serializable]
public sealed class BattleRunPreferences
{
    [Serializable] public sealed class Entry { public string key,text; public int number; public bool integer,exists; }
    public List<Entry> entries=new List<Entry>();
    public int[] deckCounts;
    static readonly string[] Integers=("RunGold Run_Gold Run_HPBonus Run_MPBonus Run_SkillCastsBonus LastRunScore " +
        "Run_UpgradeCostCount_Buy Run_UpgradeCostCount_RerollBuy Run_UpgradeCostCount_Destroy Run_UpgradeCostCount_RerollDestroy " +
        "Run_UpgradeCostCount_HpUp Run_UpgradeCostCount_MpUp Run_UpgradeCostCount_CastUp Run_UpgradeCostCount_HealHp Run_UpgradeCostCount_HealMp " +
        "Run_StartedFlagV1 Run_DefeatedEnemyCount Run_LastCountedEnemyIndex Run_MissionSeed " +
        "Mission_PoolIndex Mission_CurrentEnemyKey Mission_CurrentCompleted Mission_Gold Mission_RunGeneration " +
        "Mission_PendingCompletion_V2 Mission_PendingDevil_V2 PF_CurrentTier").Split(' ');
    static readonly string[] Strings=("RunOfuda RunOfuda_LastJSON LastOfudaJson RunItems " +
        "PF_TraitUpgradeDelta_Geki PF_TraitUpgradeDelta_Shun PF_TraitUpgradeDelta_Iyu PF_LastSpecialTileTraitBonusPairs " +
        "Mission_YakuKey Mission_DispName").Split(' ');
    public static BattleRunPreferences Capture()
    {
        var state=new BattleRunPreferences{deckCounts=PlayerData.GetDeckCountsCopy()};
        var keys=new Dictionary<string,bool>();
        foreach(var key in Integers)keys[key]=true;
        foreach(var key in Strings)keys[key]=false;
        foreach(var set in Resources.LoadAll<SkillSetAsset>("SkillSets"))
            foreach(var key in set.BattleTraitPreferenceKeys())keys[key.Key]=key.Value;
        foreach(var pair in keys)state.entries.Add(new Entry{key=pair.Key,integer=pair.Value,exists=PlayerPrefs.HasKey(pair.Key),number=pair.Value?PlayerPrefs.GetInt(pair.Key,0):0,text=pair.Value?null:PlayerPrefs.GetString(pair.Key,"")});
        return state;
    }
    public void Restore()
    {
        foreach(var e in entries){
            if(!e.exists)PlayerPrefs.DeleteKey(e.key);
            else if(e.integer)PlayerPrefs.SetInt(e.key,e.number);
            else PlayerPrefs.SetString(e.key,e.text??"");
        }
        PlayerData.RestoreBattleDeckCounts(deckCounts);
        MissionSystem.Load();PlayerPrefs.Save();
    }
}
public static partial class PlayerData
{
    public static void RestoreBattleDeckCounts(int[] counts)
    {
        if(counts==null||counts.Length!=34)return;
        _playerDeckCache=counts.Select(n=>Mathf.Max(0,n)).ToArray();
        PlayerPrefs.SetInt("PD_Deck_Init",1);SaveDeck();
    }
}
