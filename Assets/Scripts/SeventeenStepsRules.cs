using System;
using System.Collections.Generic;
using System.Linq;

// Pure rules. Enemy decisions receive only their own deck and public discards.
public static class SeventeenStepsRules
{
    public static readonly string[] Tiles = Enumerable.Range(1,9).Select(n=>"Man"+n)
        .Concat(Enumerable.Range(1,9).Select(n=>"Pin"+n)).Concat(Enumerable.Range(1,9).Select(n=>"Sou"+n))
        .Concat(new[]{"East","South","West","North","White","Green","Red"}).ToArray();
    public static readonly int[] Terminals = {0,8,9,17,18,26,27,28,29,30,31,32,33};
    public static int[] Counts(IEnumerable<int> tiles) { var c=new int[34];foreach(int t in tiles)c[t]++;return c; }
    public static List<int> Expand(int[] counts) {var r=new List<int>();for(int i=0;i<34;i++)for(int n=0;n<counts[i];n++)r.Add(i);return r;}
    public static void Shuffle<T>(IList<T> list,Random random) {for(int i=list.Count-1;i>0;i--){int j=random.Next(i+1);var a=list[i];list[i]=list[j];list[j]=a;}}
    public static void Deal(Random random,out List<int> player,out List<int> enemy)
    {
        Deal(random,out player,out enemy,out _,out _);
    }
    public static void Deal(Random random,out List<int> player,out List<int> enemy,out int doraIndicator,out int uraIndicator)
    {
        var wall=Enumerable.Range(0,136).Select(n=>n/4).ToList();Shuffle(wall,random);
        player=wall.Take(34).OrderBy(t=>t).ToList();enemy=wall.Skip(34).Take(34).OrderBy(t=>t).ToList();doraIndicator=wall[68];uraIndicator=wall[69];
    }
    public static bool IsComplete(IEnumerable<int> tiles)
    {
        var c=Counts(tiles);if(c.Sum()!=14 || c.Any(n=>n>4))return false;
        if(c.Count(n=>n==2)==7)return true;
        if(Terminals.All(t=>c[t]>=1)&&Terminals.Sum(t=>c[t])==14)return true;
        for(int p=0;p<34;p++)if(c[p]>=2){c[p]-=2;bool ok=Melds(c);c[p]+=2;if(ok)return true;}return false;
    }
    static bool Melds(int[] c)
    {
        int i=Array.FindIndex(c,n=>n>0);if(i<0)return true;
        if(c[i]>=3){c[i]-=3;bool ok=Melds(c);c[i]+=3;if(ok)return true;}
        if(i<27&&i%9<=6&&c[i+1]>0&&c[i+2]>0){c[i]--;c[i+1]--;c[i+2]--;bool ok=Melds(c);c[i]++;c[i+1]++;c[i+2]++;if(ok)return true;}return false;
    }
    public static HashSet<int> Waits(IList<int> hand)
    {
        var result=new HashSet<int>();if(hand.Count!=13)return result;var c=Counts(hand);
        for(int i=0;i<34;i++)if(c[i]<4&&IsComplete(hand.Concat(new[]{i})))result.Add(i);return result;
    }
    public sealed class Win
    {
        public int points,han,fu;public string detail;public List<string> keys=new List<string>();
    }
    public static Win Evaluate(IList<int> hand,int tile,bool rawKeys=false,bool dealer=false)
    {
        if(hand.Count!=13||!IsComplete(hand.Concat(new[]{tile})))return new Win();
        var d=rawKeys?YakuEvaluator.EvaluateDetailedKeys(hand.Select(t=>Tiles[t]).ToList(),Tiles[tile],dealer?"East":new[]{"North","West","South","East"}[(Math.Max(1,SeventeenStepsMode.Round)-1)%4],"East"):YakuEvaluator.EvaluateDetailed(hand.Select(t=>Tiles[t]).ToList(),new List<IList<string>>(),Tiles[tile],false,true,dealer?"East":new[]{"North","West","South","East"}[(Math.Max(1,SeventeenStepsMode.Round)-1)%4],"East");
        // Dealer scoring stays fixed; player seat wind rotates as in normal mode.
        return new Win{points=d.han>0?Scoring.TryScoreWin(d.fu,d.han,false,dealer).totalPoints:0,han=d.han,fu=d.fu,
            detail=d.breakdown,keys=(d.yakuKeys??new List<string>()).Concat(d.yakumanKeys??new List<string>()).Distinct().ToList()};
    }
    public static bool CanRiichi(IList<int> hand,bool dealer=false)=>Waits(hand).Any(t=>Evaluate(hand,t,true,dealer).points>0);
    public static bool MeetsMinimum(IList<int> hand,int tile,bool riichi,int dora,bool dealer=false,int penalty=0,bool ippatsu=false,bool houtei=false){
        var score=ApplyDesignatedPenalty(EvaluateRound(hand,tile,riichi,dora,-1,true,dealer,ippatsu,houtei),penalty,dealer);
        return score.points >= (dealer?12000:8000);
    }
    public static bool CanDeclare(IList<int> hand,int dora,bool dealer=false,int penalty=0)=>CanRiichi(hand,dealer)&&Waits(hand).Any(t=>MeetsMinimum(hand,t,true,dora,dealer,penalty));
    public static int NextDora(int indicator)=>indicator<27?indicator/9*9+(indicator%9+1)%9:indicator<31?27+(indicator-27+1)%4:31+(indicator-31+1)%3;
    public static Win EvaluateRound(IList<int> hand,int tile,bool riichi,int dora,int ura,bool rawKeys=false,bool dealer=false,bool ippatsu=false,bool houtei=false){
        var win=Evaluate(hand,tile,rawKeys,dealer);if(hand.Count!=13||!IsComplete(hand.Concat(new[]{tile})))return win;
        // This mode permits riichi only for a hand with a non-riichi, non-dora yaku wait.
        if(win.han<=0&&(!riichi||!CanRiichi(hand,dealer))&&!houtei)return win;
        bool yakuman=win.keys.Any(k=>new[]{"KOKUSHI","CHUUREN_POUTOU","DAISANGEN","DAISUUSHI","SHOUSUUSHI","TSUUIISOU","CHINROUTOU","RYUUIISOU","SUUANKOU"}.Contains(k));
        if(yakuman)return win;
        if(riichi){win.han++;win.keys.Add("RIICHI");win.detail+="\nリーチ　1翻";}
        if(riichi&&ippatsu){win.han++;win.keys.Add("IPPATSU");win.detail+="\n一発　1翻";}
        if(houtei){win.han++;win.keys.Add("HOUTEI");win.detail+="\n河底撈魚　1翻";}
        var tiles=hand.Concat(new[]{tile}).ToList();int visible=tiles.Count(t=>t==NextDora(dora));int hidden=riichi&&ura>=0?tiles.Count(t=>t==NextDora(ura)):0;
        win.han+=visible+hidden;if(visible>0)win.detail+="\nドラ　"+visible+"翻";if(hidden>0)win.detail+="\n裏ドラ　"+hidden+"翻";
        win.points=Scoring.TryScoreWin(win.fu,win.han,false,dealer).totalPoints;return win;
    }
    public static Win ApplyDesignatedPenalty(Win win,int unused,bool dealer=false){
        if(win.points<=0||unused<=0)return win;
        win.han=Math.Max(1,win.han-unused);win.points=Scoring.TryScoreWin(win.fu,win.han,false,dealer).totalPoints;
        win.detail+="\n指定牌未使用　−"+unused+"翻（最低1翻）";return win;
    }
    public static int SafeDiscard(IList<int> candidates,IList<int> ownDeck,IList<int> ownDiscards,IList<int> opponentDiscards,Random random)
    {
        var safe=Enumerable.Range(0,candidates.Count).Where(i=>opponentDiscards.Contains(candidates[i])).ToList();
        if(safe.Count==0){var visible=Counts(ownDeck.Concat(opponentDiscards));safe=Enumerable.Range(0,candidates.Count).Where(i=>candidates[i]>=27&&visible[candidates[i]]>=2).ToList();}
        return safe.Count>0?safe[random.Next(safe.Count)]:random.Next(candidates.Count);
    }
    // Enumerate every constructible tenpai shape, not random thirteen-tile samples.
    // Every standard completion is four sorted melds + a pair, with at most one
    // tile missing from the 34-tile deck. Seven pairs and kokushi are enumerated separately.
    public static List<int[]> TenpaiShapes(IList<int> deck)
    {
        var available=Counts(deck);var used=new int[34];var unique=new Dictionary<string,int[]>();
        var melds=new List<int[]>();for(int i=0;i<34;i++)melds.Add(new[]{i,i,i});
        for(int i=0;i<27;i++)if(i%9<=6)melds.Add(new[]{i,i+1,i+2});
        bool Fits(){int missing=0;for(int i=0;i<34;i++){if(used[i]>4)return false;missing+=Math.Max(0,used[i]-available[i]);if(missing>1)return false;}return true;}
        void Emit(){for(int t=0;t<34;t++)if(used[t]>0){used[t]--;bool fits=true;for(int j=0;j<34;j++)if(used[j]>available[j]){fits=false;break;}
            if(fits){string key=string.Join("",used);if(!unique.ContainsKey(key))unique[key]=(int[])used.Clone();}used[t]++;}}
        void Search(int start,int depth){if(depth==4){Emit();return;}for(int m=start;m<melds.Count;m++){foreach(int t in melds[m])used[t]++;if(Fits())Search(m,depth+1);foreach(int t in melds[m])used[t]--;}}
        for(int p=0;p<34;p++)if(available[p]>=1){used[p]=2;Search(0,0);used[p]=0;}
        void Pairs(int start,int depth){if(depth==7){Emit();return;}for(int t=start;t<34;t++)if(available[t]>=1){used[t]=2;if(Fits())Pairs(t+1,depth+1);used[t]=0;}}
        Pairs(0,0);
        foreach(int p in Terminals){Array.Clear(used,0,34);foreach(int t in Terminals)used[t]++;used[p]++;if(Fits())Emit();}
        return unique.Values.ToList();
    }
    public sealed class RankedHand {public List<int> hand;public int points;}
    public static RankedHand Rank(int[] shape)
    {
        var hand=Expand(shape);int best=0;foreach(int t in Waits(hand))best=Math.Max(best,Evaluate(hand,t,true).points);
        return new RankedHand{hand=hand,points=best};
    }
    public static RankedHand RankWithDora(int[] shape,int indicator){
        var hand=Expand(shape);int best=0;foreach(int tile in Waits(hand))if(MeetsMinimum(hand,tile,true,indicator,true))best=Math.Max(best,EvaluateRound(hand,tile,true,indicator,-1,true,true).points);
        return new RankedHand{hand=hand,points=best};
    }
    public static RankedHand SelectRank(IEnumerable<RankedHand> hands,int rank)
    {
        var sorted=hands.Where(h=>h.points>0).OrderByDescending(h=>h.points).ThenBy(h=>string.Join(",",h.hand)).ToList();
        return sorted.Count==0?null:sorted[Math.Min(rank-1,sorted.Count-1)];
    }
}
