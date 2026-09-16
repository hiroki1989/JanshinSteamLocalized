using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Run-only inventory. IDs are permanent; never use display names as save keys.</summary>
public static class RunConsumables
{
    public const int Capacity = 4;
    const string Key = "RunConsumablesV1";
    [Serializable] public sealed class State
    {
        public List<int> bag = new List<int>();
        public int regeneration, enemySeal, castsBonus;
        public bool geki, shun, iyu, freeCast, shield, effigy, bloodPact, usedThisTurn;
    }
    public sealed class Definition
    {
        public readonly int id, price;
        readonly string[] names, descriptions;
        public Definition(int id, int price, string ja, string en, string zh, string dja, string den, string dzh)
        { this.id=id; this.price=price; names=new[]{ja,en,zh}; descriptions=new[]{dja,den,dzh}; }
        static int Language => LocalizationManager.Instance.CurrentLanguage == LocalizationManager.Language.English ? 1 : LocalizationManager.Instance.CurrentLanguage == LocalizationManager.Language.ChineseSimplified ? 2 : 0;
        public string Name => names[Language];
        public string Description => descriptions[Language];
        public Sprite Icon => Resources.Load<Sprite>("Consumables/item_" + id.ToString("00"));
    }
    public static readonly Definition[] All = {
        new Definition(1,200,"不死鳥の緋露","Phoenix Crimson Dew","不死鸟绯露","最大HPの30％を回復。","Restore 30% of maximum HP.","恢复最大HP的30%。"),
        new Definition(2,200,"月詠の霊泉","Moon Oracle's Spring","月咏灵泉","最大MPの40％を回復。","Restore 40% of maximum MP.","恢复最大MP的40%。"),
        new Definition(3,300,"天の祝福露","Heavenly Blessed Nectar","天赐祝福露","最大HP・MPの20％をそれぞれ回復。","Restore 20% of maximum HP and MP.","分别恢复最大HP与MP的20%。"),
        new Definition(4,200,"命喰いの聖杯","Life-Devouring Chalice","噬命圣杯","現在HPの20％を消費し、最大MPの60％を回復。HPは1未満にならない。","Spend 20% of current HP to restore 60% of maximum MP. HP cannot fall below 1.","消耗当前HP的20%，恢复最大MP的60%。HP不会低于1。"),
        new Definition(5,300,"暁星の秘薬","Dawnstar Elixir","晓星秘药","今すぐ最大HPの8％を回復。その後、自分のツモ番開始時4回も8％ずつ回復。この対局限り。","Restore 8% of maximum HP now and at the start of your next four draw turns. Expires after this opponent.","立即恢复最大HP的8%，此后4次己方摸牌回合开始时各恢复8%。仅本次对战有效。"),
        new Definition(6,300,"戦神の朱印","War God's Vermilion Seal","战神朱印","この対局中、解放済みの「撃」のレベルをすべて＋1。重複不可。","For this opponent, all unlocked Strike passives gain 1 level. Does not stack.","本次对战中，所有已解锁的「击」等级+1。不可叠加。"),
        new Definition(7,300,"月神の蒼印","Moon God's Azure Seal","月神苍印","この対局中、解放済みの「瞬」のレベルをすべて＋1。重複不可。","For this opponent, all unlocked Flash passives gain 1 level. Does not stack.","本次对战中，所有已解锁的「瞬」等级+1。不可叠加。"),
        new Definition(8,300,"命神の翠印","Life God's Jade Seal","命神翠印","この対局中、解放済みの「癒」のレベルをすべて＋1。重複不可。","For this opponent, all unlocked Heal passives gain 1 level. Does not stack.","本次对战中，所有已解锁的「愈」等级+1。不可叠加。"),
        new Definition(9,300,"双刻の砂時計","Hourglass of Twin Moments","双刻沙漏","このターンのスキル使用回数上限＋1。MPは通常通り消費。","Gain one extra skill use this turn. Normal MP costs apply.","本回合技能使用次数上限+1。正常消耗MP。"),
        new Definition(10,300,"無尽の勾玉","Endless Magatama","无尽勾玉","この対局中、次のスキル成功1回のMP消費が0。使用回数は消費する。","Your next successful skill costs no MP during this opponent. It still counts as a use.","本次对战中，下次成功发动技能不消耗MP，但计入使用次数。"),
        new Definition(11,200,"雷公の鉄楔","Thunder Duke's Wedge","雷公铁楔","敵に固定1,000ダメージ。和了扱いにはならず、倍率は乗らない。","Deal exactly 1,000 damage to the enemy. This is not a mahjong win and ignores damage multipliers.","对敌人造成固定1,000伤害。不视为和牌，不受伤害倍率影响。"),
        new Definition(12,400,"封神の五重鎖","Fivefold Godbinding Chain","封神五重锁","敵のターン5回分、スキルの新たな発動を封じる。既存の効果は解除しない。","Prevent new enemy skill activations for five enemy turns. Existing effects remain.","封印敌人5个回合内的新技能发动。不解除已有的效果。"),
        new Definition(13,200,"玄武の護鱗","Black Tortoise's Scale","玄武护鳞","この対局中、次に受ける敵の和了ダメージ1回を50％軽減。攻撃スキルには無効。","Halve the next damage from an enemy mahjong win during this opponent. Does not affect attack skills.","本次对战中，下次受到的敌方和牌伤害减半。不影响攻击技能。"),
        new Definition(14,200,"破呪の銀鈴","Cursebreaking Silver Bell","破咒银铃","現在の毒・麻痺（スキル封印）を解除。失ったHP・MPや変化した牌は戻らない。","Remove current poison and paralysis (skill seal). Does not restore lost HP, MP, or altered tiles.","解除当前中毒与麻痹（技能封印）。不恢复已损失的HP、MP或已改变的牌。"),
        new Definition(15,400,"逆命の身代わり人形","Fate-Defying Effigy","逆命替身偶","この対局中、一度だけ致死ダメージをHP1で耐える。8局終了の敗北は防げない。","Survive one lethal hit at 1 HP during this opponent. Cannot prevent defeat at the round limit.","本次对战中，一次承受致命伤害后保留1HP。不能阻止局数上限导致的失败。"),
        new Definition(16,200,"星巡りの羅針盤","Starwander Compass","巡星罗盘","交換確定前のツモ場4枚を引き直す。ターンは進まない。立直中は使用不可。","Redraw all four offered tiles before confirming an exchange. Does not end the turn. Unavailable in riichi.","交换确认前重新抽取摸牌区的4张牌。不推进回合。立直中不可使用。"),
        new Definition(17,400,"三相の染め筆","Brush of Three Hues","三相染笔","通常の数牌1枚を、数字を保って別の色へ変更。この局のみ。字牌・特別牌・立直中は対象外。","Change one ordinary numbered hand tile to another suit, keeping its rank, for this hand. No honors, special tiles, or riichi.","将一张普通数牌改成其他花色，数字不变，仅本局有效。字牌、特殊牌及立直中不可用。"),
        new Definition(18,400,"運命織りの金糸","Golden Thread of Fate","织命金线","通常の数牌1枚の数字を±1。この局のみ。1と9は循環しない。特別牌・立直中は対象外。","Change one ordinary numbered hand tile's rank by 1 for this hand. No wrapping 1/9, special tiles, or riichi.","将一张普通数牌的数字增减1，仅本局有效。1与9不循环。特殊牌及立直中不可用。"),
        new Definition(19,400,"黄泉渡りの返魂札","Spirit Recall Talisman","渡冥返魂符","自分の捨て牌1枚と手牌1枚を交換。副露などで使われた牌・立直中は対象外。ツモ和了扱いにはならない。","Swap one of your available discards with a hand tile. No committed tiles or riichi. The recalled tile is not a tsumo draw.","交换一张未被使用的己方弃牌与手牌。副露用牌及立直中不可用。取回的牌不视为自摸牌。"),
        new Definition(20,300,"修羅の血盟印","Asura Blood Pact","修罗血盟印","この対局中、次の自分の和了ダメージ＋50％。和了するまでは被ダメージ＋25％。","Your next mahjong win deals 50% more damage this opponent. Until then, take 25% more damage.","本次对战中，下次己方和牌伤害+50%。和牌前受到的伤害+25%。")
    };
    public static Definition Get(int id) => All.FirstOrDefault(d=>d.id==id);
    public static State Load()
    {
        State s;
        try { s=JsonUtility.FromJson<State>(PlayerPrefs.GetString(Key,"")); } catch { s=null; }
        s ??= new State(); s.bag ??= new List<int>(); s.bag=s.bag.Where(id=>Get(id)!=null).Take(Capacity).ToList(); return s;
    }
    public static void Save(State s) { PlayerPrefs.SetString(Key,JsonUtility.ToJson(s)); PlayerPrefs.Save(); }
    public static void ResetRun() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
    public static void EndBattle() { var s=Load(); Save(new State { bag=s.bag }); }
    public static int IncomingDamage(State s,int damage,int hp,bool enemyWin)
    {
        if(damage<=0)return 0;
        int result=s.bloodPact?Mathf.CeilToInt(damage*1.25f):damage;
        if(enemyWin&&s.shield){result=Mathf.CeilToInt(result*.5f);s.shield=false;}
        if(result>=hp&&hp>0&&s.effigy){result=hp-1;s.effigy=false;}
        return Mathf.Max(0,result);
    }
    public static int TraitBonus(SkillSetAsset.Trait trait) { var s=Load(); return (trait==SkillSetAsset.Trait.Geki?s.geki:trait==SkillSetAsset.Trait.Shun?s.shun:trait==SkillSetAsset.Trait.Iyu&&s.iyu)?1:0; }
    public static int[] RollOffers(System.Random random)
    {
        var ids=All.Select(d=>d.id).ToArray();
        for(int i=0;i<3;i++){int j=random.Next(i,ids.Length); int n=ids[i]; ids[i]=ids[j]; ids[j]=n;}
        return ids.Take(3).ToArray();
    }
}
