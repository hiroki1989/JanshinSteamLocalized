using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class SeventeenStepsMode
{
    public const string ModeKey="PF_GameMode",ModeValue="SeventeenSteps";
    const string SaveKey="SeventeenSteps_V2",StockKey="SeventeenSteps_StockV1";
    public const int MaxRoundsPerEnemy=3,NormalEnemyCount=4;
    public enum Character { DyeMaster,Calligrapher,Capitalist }
    public static readonly string[] Enemies={"アマテラス","アヌビス","ポセイドン","ゼウス"};
    [Serializable] public sealed class State {
        public int enemy,round=1,playerScore,enemyScore,character,claimed;
        public bool complete;
        public List<int> ofuda=new List<int>();
    }
    [Serializable] public sealed class Stock {
        public List<int> items=new List<int>();public int equipped,pending;
    }
    public static State Current=new State();
    public static bool IsActive=>PlayerPrefs.GetString(ModeKey,"Normal")==ModeValue;
    public static int EnemyIndex=>Current.enemy;
    public static int Round=>Current.round;
    public static Character SelectedCharacter=>(Character)Current.character;
    public static int EnemyHandRank=>Current.enemy==0?3:Current.enemy==1?2:1;
    public static int RequiredTiles=>Current.enemy==0?0:Current.enemy==1?1:2;
    public static bool IsFinalEnemy=>Current.enemy==3;
    public static bool EnemyMatchFinished=>Current.round>3;
    public static bool PlayerWonEnemy=>Current.playerScore>Current.enemyScore;
    public static void Save(){PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(Current));PlayerPrefs.Save();}
    public static void StartNewRun(Character character){Current=new State{character=(int)character};PlayerPrefs.SetString(ModeKey,ModeValue);Save();}
    public static void LeaveMode(){PlayerPrefs.SetString(ModeKey,"Normal");PlayerPrefs.Save();}
    public static void RecordRound(int playerPoints,int enemyPoints){Current.playerScore+=playerPoints;Current.enemyScore+=enemyPoints;Current.round++;Save();}
    public static void AdvanceEnemy(){Current.enemy++;Current.round=1;Current.playerScore=Current.enemyScore=0;Save();}
    public static Stock LoadStock(){try{var s=JsonUtility.FromJson<Stock>(PlayerPrefs.GetString(StockKey,""))??new Stock();s.items??=new List<int>();return s;}catch{return new Stock();}}
    static void WriteStock(Stock s){PlayerPrefs.SetString(StockKey,JsonUtility.ToJson(s));}
    public static void Equip(int id){var s=LoadStock();if(id!=0&&!s.items.Contains(id))return;s.equipped=id;WriteStock(s);PlayerPrefs.Save();}
    public static int GrantEnemyReward(System.Random random){
        int bit=1<<Current.enemy;if((Current.claimed&bit)!=0)return 0;
        var defs=RunConsumables.All;int Weight(RunConsumables.Definition d)=>d.price<=300?6:d.price<=500?3:1;
        int roll=random.Next(defs.Sum(Weight));var chosen=defs[0];foreach(var d in defs){roll-=Weight(d);if(roll<0){chosen=d;break;}}
        var stock=LoadStock();stock.items.Add(chosen.id);WriteStock(stock);Current.claimed|=bit;
        if(IsFinalEnemy){SpecialTileSystem.AddGems(2);Current.complete=true;}
        Save();return chosen.id;
    }
    // Reserve a single owned item only on a NEW normal run, never on resume.
    public static void ReserveStartingItem(){var s=LoadStock();if(s.equipped>0&&s.items.Remove(s.equipped)){s.pending=s.equipped;s.equipped=0;WriteStock(s);PlayerPrefs.Save();}}
    public static void DeliverStartingItem(){var s=LoadStock();if(s.pending<=0)return;var bag=RunConsumables.Load();if(bag.bag.Count>=RunConsumables.Capacity)return;bag.bag.Add(s.pending);RunConsumables.Save(bag);s.pending=0;WriteStock(s);PlayerPrefs.Save();}
}

public static class SeventeenStepsOfuda
{
    public sealed class Definition {
        public int id,rarity;public string key,name;
        public float Multiplier=>1.2f+rarity*.2f;
        public int Weight=>new[]{10,7,4,2,1}[rarity];
        public string Rarity=>new[]{"ノーマル","コモン","レア","エピック","レジェンダリー"}[rarity];
        public string Description=>name+"で和了した点数 ×"+Multiplier.ToString("0.0");
    }
    // Ron-only reachable yaku from the normal evaluator. Tsumo/kan-only yaku cannot
    // occur in this mode and are intentionally not offered as unusable rewards.
    public static readonly Definition[] All=Build();
    static Definition[] Build(){
        string[] keys={"TANYAO","PINFU","YAKUHAI","IIPEIKOU","CHIITOITSU","TOITOI","SANANKOU","SANSHOKU_DOUJUN","ITTSU","CHANTA","JUNCHAN","HONROUTOU","SHOUSANGEN","SANSHOKU_DOUKOU","HONITSU","CHINITSU","RYANPEIKOU","KOKUSHI","CHUUREN_POUTOU","DAISANGEN","DAISUUSHI","SHOUSUUSHI","TSUUIISOU","CHINROUTOU","RYUUIISOU","SUUANKOU","RIICHI","IPPATSU","HOUTEI"};
        string[] names={"タンヤオ","平和","役牌","一盃口","七対子","対々和","三暗刻","三色同順","一気通貫","チャンタ","純チャン","混老頭","小三元","三色同刻","混一色","清一色","二盃口","国士無双","九蓮宝燈","大三元","大四喜","小四喜","字一色","清老頭","緑一色","四暗刻","立直","一発","河底撈魚"};
        int[] rarity={4,4,4,3,3,3,2,2,2,2,1,1,1,1,3,1,1,0,0,0,0,0,0,0,0,0,4,3,1};
        return keys.Select((k,i)=>new Definition{id=i,key=k,name=names[i],rarity=rarity[i]}).ToArray();
    }
    public static Definition[] Offer(System.Random rng,IEnumerable<int> owned){var pool=All.Where(d=>!owned.Contains(d.id)).ToList();var result=new List<Definition>();while(result.Count<3&&pool.Count>0){int r=rng.Next(pool.Sum(d=>d.Weight));var chosen=pool[0];foreach(var d in pool){r-=d.Weight;if(r<0){chosen=d;break;}}result.Add(chosen);pool.Remove(chosen);}return result.ToArray();}
    public static int Apply(SeventeenStepsRules.Win win,IEnumerable<int> equipped){decimal points=win.points;foreach(int id in equipped){var d=All[id];if(win.keys.Contains(d.key))points*=1.2m+d.rarity*.2m;}return (int)(Math.Ceiling(points/100)*100);}
}
