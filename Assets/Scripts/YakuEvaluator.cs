using System;
using System.Collections.Generic;
using System.Linq;

public static class YakuEvaluator
{
    private const string YakuKeyKokushi = "KOKUSHI";
    private const string YakuKeyChiitoitsu = "CHIITOITSU";
    private const string YakuKeyMenzenTsumo = "MENZEN_TSUMO";
    private const string YakuKeyTanyao = "TANYAO";
    private const string YakuKeyPinfu = "PINFU";
    private const string YakuKeyYakuhai = "YAKUHAI";
    private const string YakuKeyIipeikou = "IIPEIKOU";
    private const string YakuKeyRyanpeikou = "RYANPEIKOU";
    private const string YakuKeySanshokuDoujun = "SANSHOKU_DOUJUN";
    private const string YakuKeyIttsu = "ITTSU";
    private const string YakuKeyChanta = "CHANTA";
    private const string YakuKeyJunchan = "JUNCHAN";
    private const string YakuKeyToitoi = "TOITOI";
    private const string YakuKeySanankou = "SANANKOU";
    private const string YakuKeySankantsu = "SANKANTSU";
    private const string YakuKeySanshokuDoukou = "SANSHOKU_DOUKOU";
    private const string YakuKeyShousangen = "SHOUSANGEN";
    private const string YakuKeyHonroutou = "HONROUTOU";
    private const string YakuKeyHonitsu = "HONITSU";
    private const string YakuKeyChinitsu = "CHINITSU";

    private const string YakumanKeyChuurenPoutou = "CHUUREN_POUTOU";
    private const string YakumanKeyDaisangen = "DAISANGEN";
    private const string YakumanKeyDaisuushi = "DAISUUSHI";
    private const string YakumanKeyShousuushi = "SHOUSUUSHI";
    private const string YakumanKeyTsuuiisou = "TSUUIISOU";
    private const string YakumanKeyChinroutou = "CHINROUTOU";
    private const string YakumanKeyRyuuiisou = "RYUUIISOU";
    private const string YakumanKeySuuankou = "SUUANKOU";
    private const string YakumanKeySuukantsu = "SUUKANTSU";

    private static string GetYakuDisplayName(string key)
    {
        return LocalizationManager.Yaku(key);
    }
    private static string GetYakumanDisplayName(string key)
    {
        return LocalizationManager.Yakuman(key);
    }
    private static string FormatYaku(string key, int han)
    {
        return $"{GetYakuDisplayName(key)}(+{han})";
    }

    private static string FormatYakuhai(int han)
    {
        return $"{GetYakuDisplayName(YakuKeyYakuhai)}×{han}(+{han})";
    }

    private static string FormatYakuman(string key)
    {
        return $"{GetYakumanDisplayName(key)}(+13)";
    }

    private static string BuildYakumanBreakdown(IEnumerable<string> yakumanKeys)
    {
        var keys = (yakumanKeys ?? Enumerable.Empty<string>()).ToList();
        return string.Join(" + ", keys.Select(FormatYakuman)) + $" | 役満×{keys.Count}";
    }

    private static string BuildStandardBreakdown(List<string> yaku, int han, int fu)
    {
        return (yaku.Count == 0 ? "役なし" : string.Join(" + ", yaku)) + $" | {han}翻 {fu}符";
    }
    private static string BuildNoYakuBreakdown(int fu)
    {
        return $"役なし | 0翻 {fu}符";
    }

    public struct DetailedResult
    {
        public int han;
        public int fu;
        public string breakdown;
        public List<string> yakuKeys;
        public List<string> yakumanKeys;
    }
    // -------------------- 公開API --------------------
    public static (int han, int fu, string breakdown) Evaluate(
        IList<string> concealed,                 // 手牌（鳴きで使われていない14枚中の13枚）
        IList<IList<string>> openMelds,          // 鳴き面子（各3～4枚：最小限の判定に使用）
        string winTile,                          // 和了牌ID（例: "Man7", "Pin2", "Sou9", "East", "White"...）
        bool isTsumo,                            // ツモ和了か
        bool isClosed,                           // 門前（鳴き無し）か
        string seatWind,                         // 自風（"East"/"South"/"West"/"North"）
        string roundWind                         // 場風（"East"/"South"/"West"/"North"）
    )
    {
        var d = EvaluateDetailed(concealed, openMelds, winTile, isTsumo, isClosed, seatWind, roundWind);
        return (d.han, d.fu, d.breakdown);
    }

    public static DetailedResult EvaluateDetailed(
        IList<string> concealed,                 // 手牌（鳴きで使われていない14枚中の13枚）
        IList<IList<string>> openMelds,          // 鳴き面子（各3～4枚：最小限の判定に使用）
        string winTile,                          // 和了牌ID（例: "Man7", "Pin2", "Sou9", "East", "White"...）
        bool isTsumo,                            // ツモ和了か
        bool isClosed,                           // 門前（鳴き無し）か
        string seatWind,                         // 自風（"East"/"South"/"West"/"North"）
        string roundWind                         // 場風（"East"/"South"/"West"/"North"）
    )
    {
var hand = (concealed ?? Array.Empty<string>()).Where(s => !string.IsNullOrEmpty(s)).ToList();

// ★分解（面子取り）に使う牌列は「和了牌を含んだ枚数」に揃える。
//   呼び出し側が 13 枚で渡すケース（winTile が別渡し）では、ここで 14 枚に寄せる。
//   完成手の総枚数は基本的に 3n+2 になるので、手牌枚数が 3n+1 のときだけ winTile を足す。
var handForDecomp = new List<string>(hand);
if (!string.IsNullOrEmpty(winTile) && (handForDecomp.Count % 3 == 1))
{
    handForDecomp.Add(winTile);
}

var open = ParseOpenMelds(openMelds);
int fixedMelds = open.Count;
int needMelds  = Math.Max(0, 4 - fixedMelds);

// ★暗槓（concealed=true）は「面前を崩さない」扱い。
//   面前判定・食い下がり判定では「開いた副露（concealed=false）」があるかだけを見る。
bool hasOpen   = open.Any(m => !m.concealed);
bool menzen    = !hasOpen && isClosed;

// ★ピンフは「一切の鳴き（暗槓含む）が無い」ことを必須条件にする
bool allowPinfu = menzen && open.Count == 0;

if (!hasOpen && IsKokushi(handForDecomp))
{
    return new DetailedResult
    {
        han = 13,
        fu = 0,
        breakdown = $"{GetYakuDisplayName(YakuKeyKokushi)} | 役満",
        yakuKeys = new List<string>(),
        yakumanKeys = new List<string> { YakuKeyKokushi }
    };
}
if (!hasOpen && IsChiitoitsu(handForDecomp))
{
    bool preferRyanpeikou =
        menzen &&
        GenerateAllDecompositions(handForDecomp, 4).Any(dc => HasRyanpeikou(dc));

if (!preferRyanpeikou)
{
    int han = 2;
    var names = new List<string> { FormatYaku(YakuKeyChiitoitsu, 2) };
    var yakuKeys = new List<string> { YakuKeyChiitoitsu };

    // ★七対子でもツモなら「門前清自摸和」は付く（門前条件）
    if (isTsumo && menzen)
    {
        han += 1;
        names.Add(FormatYaku(YakuKeyMenzenTsumo, 1));
        yakuKeys.Add(YakuKeyMenzenTsumo);
    }
    if (IsTanyaoAll(handForDecomp))
    {
        han += 1;
        names.Add(FormatYaku(YakuKeyTanyao, 1));
        yakuKeys.Add(YakuKeyTanyao);
    }
    if (IsHonitsu(handForDecomp))
    {
        int v = menzen ? 3 : 2;
        han += v;
        names.Add(FormatYaku(YakuKeyHonitsu, v));
        yakuKeys.Add(YakuKeyHonitsu);
    }
    if (IsChinitsu(handForDecomp))
    {
        int v = menzen ? 6 : 5;
        han += v;
        names.Add(FormatYaku(YakuKeyChinitsu, v));
        yakuKeys.Add(YakuKeyChinitsu);
    }

    return new DetailedResult
    {
        han = han,
        fu = 25,
        breakdown = BuildStandardBreakdown(names, han, 25),
        yakuKeys = yakuKeys,
        yakumanKeys = new List<string>()
    };
}

            // preferRyanpeikou==true の場合は return せず、この後の通常分解評価へ
        }
var decomps = GenerateAllDecompositions(handForDecomp, needMelds);

if (decomps.Count == 0)
{
    return new DetailedResult
    {
        han = 0,
        fu = 30,
        breakdown = "",
        yakuKeys = new List<string>(),
        yakumanKeys = new List<string>()
    };
} // 「役なし」を返さない
int bestHan = 0, bestFu = 0;
int bestBase = -1; // ...
string bestText = ""; // 初期値も空に
bool bestHasPinfu = false;
List<string> bestYakuKeys = new List<string>();
List<string> bestYakumanKeys = new List<string>();

foreach (var d in decomps)
{
    var allMelds = new List<Meld>(open);
    allMelds.AddRange(d.melds);

// 役満の先行判定（役満が1つでもあれば通常役はカウントしない）
var yakumanList = DetectYakuman(allMelds, d.pair, winTile, isTsumo, menzen);
if (yakumanList.Count > 0)
{
    int yakumanHan  = 13 * yakumanList.Count;
    int yakumanFu   = 0;
    int yakumanBase = 8000 * yakumanList.Count; // 役満×n は base 8000 を n 倍

    string text = BuildYakumanBreakdown(yakumanList);
if (
    (yakumanHan > bestHan) ||
    (yakumanHan == bestHan && yakumanBase > bestBase) ||
    (yakumanHan == bestHan && yakumanBase == bestBase && yakumanFu > bestFu)
)
{
    bestBase = yakumanBase;
    bestHan = yakumanHan;
    bestFu = yakumanFu;
    bestText = text;
    bestYakuKeys = new List<string>();
    bestYakumanKeys = new List<string>(yakumanList);
}
    continue;
}
// ▼ 通常役の判定（ここを実装）
int localHan = 0; 
var yaku = new List<string>();
var localYakuKeys = new List<string>();
// var flat = FlattenTiles(d.melds, d.pair);   // 面子+雀頭をID列に   // ← 副露が落ちる
var flatAll = FlattenTiles(allMelds, d.pair);   // ← 副露込みに修正

// 門前ツモ
if (isTsumo && menzen)
{
    localHan += 1;
    yaku.Add(FormatYaku(YakuKeyMenzenTsumo, 1));
    localYakuKeys.Add(YakuKeyMenzenTsumo);
}

bool localHasPinfu = allowPinfu && IsPinfu(d, winTile, seatWind, roundWind);

// 平和（ピンフ）：門前 かつ 「鳴き（暗槓含む）なし」 かつ 両面のみ
if (localHasPinfu)
{
    localHan += 1;
    yaku.Add(FormatYaku(YakuKeyPinfu, 1));
    localYakuKeys.Add(YakuKeyPinfu);
}
// タンヤオ（※副露込み）
if (IsTanyaoAll(flatAll))
{
    localHan += 1;
    yaku.Add(FormatYaku(YakuKeyTanyao, 1));
    localYakuKeys.Add(YakuKeyTanyao);
}

// 役牌（自風・場風・三元）…最大3翻ぶん
int yakuhai = CountYakuhaiTriplets(allMelds, d.pair, seatWind, roundWind);
if (yakuhai > 0)
{
    localHan += yakuhai;
    yaku.Add(FormatYakuhai(yakuhai));
    localYakuKeys.Add(YakuKeyYakuhai);
}

    // 二盃口/一盃口（門前のみ）※二盃口を優先し、同時には付けない
    if (menzen)
    {
        int iipeiPairs = CountIipeikouPairs(d); // 同一順子のペア数
        if (iipeiPairs >= 2)
        {
            localHan += 3;
            yaku.Add(FormatYaku(YakuKeyRyanpeikou, 3));
            localYakuKeys.Add(YakuKeyRyanpeikou);
        }
        else if (iipeiPairs >= 1)
        {
            localHan += 1;
            yaku.Add(FormatYaku(YakuKeyIipeikou, 1));
            localYakuKeys.Add(YakuKeyIipeikou);
        }
    }

// 三色同順
if (HasSanshokuDoujun(allMelds))
{
    localHan += 2;
    yaku.Add(FormatYaku(YakuKeySanshokuDoujun, 2));
    localYakuKeys.Add(YakuKeySanshokuDoujun);
}

// 一気通貫（門前2 / 鳴き1）
if (HasIttsu(allMelds))
{
    int v = menzen ? 2 : 1;
    localHan += v;
    yaku.Add(FormatYaku(YakuKeyIttsu, v));
    localYakuKeys.Add(YakuKeyIttsu);
}

// 純チャン/チャンタは重複不可（純チャン優先）
    // ★混老頭が成立する場合はチャンタを排除する（複合しない）
    bool __honroutou = IsHonroutou(flatAll);
    bool __chanta  = IsChanta(allMelds, d.pair);
    bool __junchan = IsJunchan(allMelds, d.pair);
    if (__junchan)
    {
        int v = menzen ? 3 : 2;
        localHan += v;
        yaku.Add(FormatYaku(YakuKeyJunchan, v));
        localYakuKeys.Add(YakuKeyJunchan);
    }
    else if (__chanta && !__honroutou)
    {
        int v = menzen ? 2 : 1;
        localHan += v;
        yaku.Add(FormatYaku(YakuKeyChanta, v));
        localYakuKeys.Add(YakuKeyChanta);
    }

// 対々和（※副露込み）
if (IsToitoi(allMelds))
{
    localHan += 2;
    yaku.Add(FormatYaku(YakuKeyToitoi, 2));
    localYakuKeys.Add(YakuKeyToitoi);
}

// 三暗刻
if (ConcealedTripletCountForYaku(allMelds, d.pair, winTile, isTsumo) >= 3)
{
    localHan += 2;
    yaku.Add(FormatYaku(YakuKeySanankou, 2));
    localYakuKeys.Add(YakuKeySanankou);
}

// 三槓子（暗槓/明槓どちらもOK。Meld.quad=true を3つ数える）
if (allMelds.Count(m => m.quad) >= 3)
{
    localHan += 2;
    yaku.Add(FormatYaku(YakuKeySankantsu, 2));
    localYakuKeys.Add(YakuKeySankantsu);
}

// 三色同刻
if (HasSanshokuDoukou(allMelds))
{
    localHan += 2;
    yaku.Add(FormatYaku(YakuKeySanshokuDoukou, 2));
    localYakuKeys.Add(YakuKeySanshokuDoukou);
}

// 小三元
if (IsShousangen(allMelds, d.pair))
{
    localHan += 2;
    yaku.Add(FormatYaku(YakuKeyShousangen, 2));
    localYakuKeys.Add(YakuKeyShousangen);
}

// 混老頭（※副露込み）
if (__honroutou)
{
    localHan += 2;
    yaku.Add(FormatYaku(YakuKeyHonroutou, 2));
    localYakuKeys.Add(YakuKeyHonroutou);
}

// 混一色（門前3 / 鳴き2）（※副露込み）
if (IsHonitsu(flatAll))
{
    int v = menzen ? 3 : 2;
    localHan += v;
    yaku.Add(FormatYaku(YakuKeyHonitsu, v));
    localYakuKeys.Add(YakuKeyHonitsu);
}

// 清一色（門前6 / 鳴き5）（※副露込み）
if (IsChinitsu(flatAll))
{
    int v = menzen ? 6 : 5;
    localHan += v;
    yaku.Add(FormatYaku(YakuKeyChinitsu, v));
    localYakuKeys.Add(YakuKeyChinitsu);
}
// ▼ 符計算（ピンフ時は 20/30）
var dfu = new Decomp { melds = allMelds, pair = d.pair };   // ← 副露込みで再構成
int localFu = CalcFu(dfu, winTile, isTsumo, menzen, allowPinfu, seatWind, roundWind);
int localBase = BasePointForCompare(localHan, localFu); // 下記ヘルパー
string text2 = BuildStandardBreakdown(yaku, localHan, localFu);

if (
    (localHan > bestHan) ||
    (localHan == bestHan && localBase > bestBase) ||
    (localHan == bestHan && localBase == bestBase && localHasPinfu && !bestHasPinfu) ||
    (localHan == bestHan && localBase == bestBase && localHasPinfu == bestHasPinfu && localFu > bestFu)
)
{
    bestBase = localBase;
    bestHan = localHan;
    bestFu = localFu;
    bestText = text2;
    bestHasPinfu = localHasPinfu;
    bestYakuKeys = new List<string>(localYakuKeys);
    bestYakumanKeys = new List<string>();
}
}
if (bestHan <= 0)
{
    int fuForNoYaku = bestFu;
    if (fuForNoYaku <= 0) fuForNoYaku = 30;   // 念のためフォールバック
    fuForNoYaku = Math.Max(20, fuForNoYaku);  // 最低20符（七対子25は別return済み）
    return new DetailedResult
    {
        han = 0,
        fu = fuForNoYaku,
        breakdown = BuildNoYakuBreakdown(fuForNoYaku),
        yakuKeys = new List<string>(),
        yakumanKeys = new List<string>()
    };
}
return new DetailedResult
{
    han = bestHan,
    fu = bestFu,
    breakdown = bestText,
    yakuKeys = bestYakuKeys,
    yakumanKeys = bestYakumanKeys
};
    }
private static List<string> DetectYakuman(List<Meld> allMelds, int pair, string winTile, bool isTsumo, bool menzen)
{
    var res = new List<string>();
    var flat = FlattenTiles(allMelds, pair);

    // ★九蓮宝燈：他の役と重複しない仕様なので、成立したらこれだけ返す
    if (menzen && IsChuurenPoutou(flat, winTile))
    {
        res.Add(YakumanKeyChuurenPoutou);
        return res;
    }

    if (IsDaisangen(allMelds)) res.Add(YakumanKeyDaisangen);
    var (small4, big4) = DetectFourWinds(allMelds, pair);
    if (big4)   res.Add(YakumanKeyDaisuushi);
    else if (small4) res.Add(YakumanKeyShousuushi);

    if (IsTsuuiisou(flat))  res.Add(YakumanKeyTsuuiisou);
    if (IsChinroutou(flat)) res.Add(YakumanKeyChinroutou);
    if (IsRyuuiisou(flat))  res.Add(YakumanKeyRyuuiisou);

    if (IsSuuAnkou(allMelds, pair, winTile, isTsumo, menzen)) res.Add(YakumanKeySuuankou);

    // ★追加：四槓子（暗槓/明槓どちらもOK）
    if (IsSuuKantsu(allMelds)) res.Add(YakumanKeySuukantsu);

    return res;
}
private static bool IsSuuKantsu(List<Meld> melds)
{
    if (melds == null) return false;

    // Meld.quad が true のものが「槓子」
    int kanCount = melds.Count(m => m.quad);

    // 通常は 4 なら成立。念のため >=4 にしておく（データ異常でも落とさない）
    return kanCount >= 4;
}
private static bool IsChuurenPoutou(List<string> flat, string winTile)
{
    if (flat == null) return false;

    // ※この Evaluator は “concealed が13枚” の呼び出しもあり得るので、
    //    flat が13なら winTile を足して14に寄せる（14ならそのまま）
    var ids = new List<string>(flat.Where(s => !string.IsNullOrEmpty(s)));
    if (!string.IsNullOrEmpty(winTile) && ids.Count == 13) ids.Add(winTile);

    if (ids.Count != 14) return false;

    var idxs = ids.Select(ToIndex).Where(i => i >= 0).ToList();
    if (idxs.Count != 14) return false;

    // 字牌混入は不可
    if (idxs.Any(IsHonorIdx)) return false;

    // 1種類の数牌スートのみ
    var suit = SuitOf(idxs[0]);
    if (suit == Suit.Honor) return false;
    if (idxs.Any(i => SuitOf(i) != suit)) return false;

    // 1112345678999 + 任意1枚（同一スート）
    int[] c = new int[10]; // 1..9
    foreach (var i in idxs) c[NumOf(i)]++;

    if (c[1] < 3) return false;
    if (c[9] < 3) return false;
    for (int n = 2; n <= 8; n++)
        if (c[n] < 1) return false;

    // 合計14枚はすでに保証済みだが念のため
    int sum = 0; for (int n = 1; n <= 9; n++) sum += c[n];
    return sum == 14;
}

private static bool IsDaisangen(List<Meld> melds)
{
    int d = 0;
    foreach (var m in melds)
        if (m.trip && (m.a == ToIndex("White") || m.a == ToIndex("Green") || m.a == ToIndex("Red"))) d++;
    return d == 3;
}

private static (bool small, bool big) DetectFourWinds(List<Meld> melds, int pair)
{
    bool e=false,s=false,w=false,n=false;
    foreach (var m in melds)
    {
        if (!m.trip) continue;
        if (m.a == ToIndex("East"))  e = true;
        if (m.a == ToIndex("South")) s = true;
        if (m.a == ToIndex("West"))  w = true;
        if (m.a == ToIndex("North")) n = true;
    }
    int windPair = (pair==ToIndex("East")||pair==ToIndex("South")||pair==ToIndex("West")||pair==ToIndex("North")) ? 1 : 0;
    bool big  = (e && s && w && n);              // 4風すべて刻子
    bool small= (e && s && w && n==false && windPair==1) ||  // 3風刻子 + 残り1風が対子
                (e && s && w==false && n && windPair==1) ||
                (e && s==false && w && n && windPair==1) ||
                (e==false && s && w && n && windPair==1);
    return (small, big);
}

private static bool IsTsuuiisou(List<string> flat)
{
    // 全て字牌（風・三元）のみ
    foreach (var id in flat)
    {
        int idx = ToIndex(id);
        if (idx < 0 || !IsHonorIdx(idx)) return false;
    }
    return true;
}

private static bool IsChinroutou(List<string> flat)
{
    // 字牌なし ＆ 全て数牌の 1 or 9
    foreach (var id in flat)
    {
        int idx = ToIndex(id);
        if (idx < 0 || IsHonorIdx(idx)) return false;
        int n = NumOf(idx);
        if (n != 1 && n != 9) return false;
    }
    return true;
}

private static bool IsRyuuiisou(List<string> flat)
{
    // 索子のみ、かつ 2,3,4,6,8 と 發 のみ
    foreach (var id in flat)
    {
        if (id == "Green") continue; // 發
        int idx = ToIndex(id);
        if (idx < 0 || IsHonorIdx(idx)) return false;
        if (SuitOf(idx) != Suit.Sou) return false;
        int n = NumOf(idx);
        if (!(n == 2 || n == 3 || n == 4 || n == 6 || n == 8)) return false;
    }
    return true;
}


private static bool IsSuuAnkou(List<Meld> melds, int pair, string winTile, bool isTsumo, bool menzen)
{
    int w = ToIndex(winTile);
    int concealed = 0;

    foreach (var m in melds)
    {
        if (!m.trip && !m.quad) continue;

        bool concealedForYaku = m.concealed;

        // ロン時に「シャボ待ちで和了牌が刻子を完成させた面子」は明刻扱いに落ちる
        // ただし暗槓はここで落としてはいけない
        if (!isTsumo &&
            m.trip &&
            !m.quad &&
            m.a == w &&
            WaitTypeEx(new Decomp{ melds = melds, pair = pair }, w) == "shanpon")
        {
            concealedForYaku = false;
        }

        if (concealedForYaku) concealed++;
    }

    if (concealed < 4) return false;

    // ロンは単騎待ちのみ有効。ツモは無条件にOK。
    if (isTsumo) return true;
    return WaitTypeEx(new Decomp{ melds = melds, pair = pair }, w) == "tanki";
}
    // -------------------- タイル基礎 --------------------
    private enum Suit { Man = 0, Pin = 1, Sou = 2, Honor = 3 }

private static int ToIndex(string id)
{
    if (string.IsNullOrEmpty(id)) return -1;

    // ★正規化：'*' や '_sp' を落としてベースIDで判定する
    if (id.EndsWith("*")) id = id.Substring(0, id.Length - 1);
    int sp = id.IndexOf("_sp", StringComparison.Ordinal);
    if (sp > 0) id = id.Substring(0, sp);

    if (id.StartsWith("Man") && int.TryParse(id.Substring(3), out int nm) && nm >= 1 && nm <= 9) return 0 + (nm - 1);
    if (id.StartsWith("Pin") && int.TryParse(id.Substring(3), out int np) && np >= 1 && np <= 9) return 9 + (np - 1);
    if (id.StartsWith("Sou") && int.TryParse(id.Substring(3), out int ns) && ns >= 1 && ns <= 9) return 18 + (ns - 1);

    switch (id)
    {
        case "East": return 27; case "South": return 28; case "West": return 29; case "North": return 30;
        case "White": return 31; case "Green": return 32; case "Red": return 33;
    }
    return -1;
}

    private static Suit SuitOf(int idx) => (idx < 9 ? Suit.Man : (idx < 18 ? Suit.Pin : (idx < 27 ? Suit.Sou : Suit.Honor)));
    private static int NumOf(int idx)   => (idx < 27) ? (idx % 9) + 1 : 0;
    private static bool IsHonorIdx(int idx) => idx >= 27;
    private static bool IsTerminalOrHonorIdx(int idx) => IsHonorIdx(idx) || NumOf(idx) == 1 || NumOf(idx) == 9;

    private static string FromIndex(int idx)
    {
        if (idx < 0 || idx > 33) return "";
        if (idx < 9) return "Man" + (idx % 9 + 1);
        if (idx < 18) return "Pin" + (idx % 9 + 1);
        if (idx < 27) return "Sou" + (idx % 9 + 1);
        return new[] { "East", "South", "West", "North", "White", "Green", "Red" }[idx - 27];
    }

    private struct Meld
    {
        public bool run;
        public bool trip;

        // ★追加：槓子（カン）かどうか
        public bool quad;

        public int a, b, c;
        public bool concealed;

        public static Meld Run(int a, int b, int c, bool concealed = true)
            => new Meld { run = true, trip = false, quad = false, a = a, b = b, c = c, concealed = concealed };

        // 刻子（ポン/暗刻）: trip=true, quad=false
        public static Meld Trip(int i, bool concealed = true)
            => new Meld { run = false, trip = true, quad = false, a = i, b = i, c = i, concealed = concealed };

        // 槓子（カン）: trip=true を維持（既存役判定のデグレ防止）, quad=true
        public static Meld Quad(int i, bool concealed = true)
            => new Meld { run = false, trip = true, quad = true, a = i, b = i, c = i, concealed = concealed };
    }

    private struct Decomp { public List<Meld> melds; public int pair; }

    private static List<Meld> ParseOpenMelds(IList<IList<string>> openMelds)
    {
        var res = new List<Meld>();
        if (openMelds == null) return res;
        foreach (var raw in openMelds)
        {
        if (raw == null || raw.Count < 3) continue;
        // 明鳴きか（'*'付きがあるか）：ミンカン/ポン/チーの印
        bool hadStar = raw.Any(s => !string.IsNullOrEmpty(s) && s.EndsWith("*"));
        var ids = raw.Where(s => !string.IsNullOrEmpty(s))
                     .Select(s => s.EndsWith("*") ? s.Substring(0, s.Length - 1) : s).ToList();
            var idx = ids.Select(ToIndex).Where(i => i >= 0).ToList();
            if (idx.Count < 3) continue;

            var g = idx.GroupBy(i => i).OrderByDescending(gp => gp.Count()).First();

            // ★4枚同一 → 槓子（カン）
            if (g.Count() == 4)
            {
                res.Add(Meld.Quad(g.Key, concealed: !hadStar));
                continue;
            }

            // ★3枚同一 → 刻子（ポン/暗刻）
            if (g.Count() == 3)
            {
                res.Add(Meld.Trip(g.Key, concealed: !hadStar));
                continue;
            }


            idx.Sort();
            int a = idx[0], b = idx[1], c = idx[2];
            if (!IsHonorIdx(a) && SuitOf(a) == SuitOf(b) && SuitOf(b) == SuitOf(c) &&
                NumOf(a) + 1 == NumOf(b) && NumOf(b) + 1 == NumOf(c))
                res.Add(Meld.Run(a, b, c, concealed: false));
            else
            {
            int k = ToIndex(ids[0]); if (k >= 0) res.Add(Meld.Trip(k, concealed: !hadStar));
            }
        }
        return res;
    }

    private static List<string> FlattenTiles(List<Meld> melds, int pairIdx)
    {
        var list = new List<string>(melds.Count * 3 + 2);
        foreach (var m in melds) { list.Add(FromIndex(m.a)); list.Add(FromIndex(m.b)); list.Add(FromIndex(m.c)); }
        list.Add(FromIndex(pairIdx)); list.Add(FromIndex(pairIdx));
        return list;
    }

    private static bool IsKokushi(List<string> tiles)
    {
        var need = new HashSet<int> {
            ToIndex("Man1"),ToIndex("Man9"),ToIndex("Pin1"),ToIndex("Pin9"),ToIndex("Sou1"),ToIndex("Sou9"),
            ToIndex("East"),ToIndex("South"),ToIndex("West"),ToIndex("North"),ToIndex("White"),ToIndex("Green"),ToIndex("Red")
        };
        var idxs = tiles.Select(ToIndex).Where(i => i >= 0).ToList();
        var set  = new HashSet<int>(idxs);
        if (!need.IsSubsetOf(set)) return false;
        return idxs.GroupBy(i => i).Any(g => need.Contains(g.Key) && g.Count() >= 2);
    }

    private static bool IsChiitoitsu(List<string> tiles)
    {
        var idxs = tiles.Select(ToIndex).Where(i => i >= 0).ToList();
        if (idxs.Count != 14) return false;
        var groups = idxs.GroupBy(i => i).Select(g => g.Count()).OrderBy(x => x).ToArray();
        return groups.Length == 7 && groups.All(c => c == 2);
    }

    private static List<Decomp> GenerateAllDecompositions(List<string> concealed, int needMelds)
    {
        var all = new List<Decomp>();
        var cnt = new int[34];
        foreach (var t in concealed) { int i = ToIndex(t); if (i >= 0) cnt[i]++; }

        for (int p = 0; p < 34; p++)
        {
            if (cnt[p] < 2) continue;
            cnt[p] -= 2;
            var acc = new List<Meld>();
            EnumerateMelds(cnt, needMelds, acc, all, p);
            cnt[p] += 2;
        }
        return all;
    }

    private static void EnumerateMelds(int[] cnt, int need, List<Meld> acc, List<Decomp> outList, int pairIdx)
    {
        if (need == 0)
        {
            for (int i = 0; i < 34; i++) if (cnt[i] != 0) return;
            outList.Add(new Decomp { melds = new List<Meld>(acc), pair = pairIdx });
            return;
        }
        int k = -1; for (int i = 0; i < 34; i++) { if (cnt[i] > 0) { k = i; break; } }
        if (k == -1) return;

        if (!IsHonorIdx(k))
        {
            int n = NumOf(k);
            if (n <= 7)
            {
                int k1 = k + 1, k2 = k + 2;
                if (SuitOf(k) == SuitOf(k1) && SuitOf(k1) == SuitOf(k2) && cnt[k1] > 0 && cnt[k2] > 0)
                {
                    cnt[k]--; cnt[k1]--; cnt[k2]--;
                    acc.Add(Meld.Run(k, k1, k2, concealed: true));
                    EnumerateMelds(cnt, need - 1, acc, outList, pairIdx);
                    acc.RemoveAt(acc.Count - 1);
                    cnt[k]++; cnt[k1]++; cnt[k2]++;
                }
            }
        }
        if (cnt[k] >= 3)
        {
            cnt[k] -= 3;
            acc.Add(Meld.Trip(k, concealed: true));
            EnumerateMelds(cnt, need - 1, acc, outList, pairIdx);
            acc.RemoveAt(acc.Count - 1);
            cnt[k] += 3;
        }
    }
private static bool IsPinfu(Decomp d, string winTile, string seatWind, string roundWind)
{
    // 平和：全て順子 / 役牌頭でない / 両面待ち
    if (d.melds.Any(m => !m.run)) return false;        // 全順子でない

    string pid = FromIndex(d.pair);
    if (pid == seatWind || pid == roundWind || pid == "White" || pid == "Green" || pid == "Red") return false; // 役牌頭

    int w = ToIndex(winTile);
    if (w < 0) return false;
    if (IsHonorIdx(w)) return false;                   // 字牌待ちは不可

    bool winAppearsInSomeRun =
        d.melds.Any(m => m.run && (m.a == w || m.b == w || m.c == w));

    if (!winAppearsInSomeRun) return false;

    string wt = WaitTypeRunsBestForPinfu(d.melds, w);
    return wt == "ryanmen";
}
private static string WaitTypeRunsBestForPinfu(List<Meld> melds, int winIdx)
{
    bool hasRyanmen = false;
    bool hasKanchan = false;
    bool hasPenchan = false;

    foreach (var m in melds)
    {
        if (!m.run) continue;
        if (m.a == winIdx || m.b == winIdx || m.c == winIdx)
        {
            int na = NumOf(m.a), nb = NumOf(m.b), nc = NumOf(m.c);
            int nw = NumOf(winIdx);

            if (nb == nw) hasKanchan = true;
            else if ((na == 1 && nw == 3) || (nc == 9 && nw == 7)) hasPenchan = true;
            else hasRyanmen = true;
        }
    }

    if (hasRyanmen) return "ryanmen";
    if (hasKanchan) return "kanchan";
    if (hasPenchan) return "penchan";
    return "tanki";
}

// YakuEvaluator.cs
private static string WaitType(List<Meld> melds, int winIdx)
{
    if (IsHonorIdx(winIdx)) return "tanki";
    foreach (var m in melds)
    {
        if (!m.run) continue;
        if (m.a == winIdx || m.b == winIdx || m.c == winIdx)
        {
            int na = NumOf(m.a), nb = NumOf(m.b), nc = NumOf(m.c);
            int nw = NumOf(winIdx);
            if (nb == nw) return "kanchan";
            // ★ 正しいペンチャン：1-2-3 は「3待ち」/ 7-8-9 は「7待ち」
            if ((na == 1 && nw == 3) || (nc == 9 && nw == 7)) return "penchan";
            return "ryanmen";
        }
    }
    return "tanki";
}


    private static string WaitTypeEx(Decomp d, int winIdx)
    {
        var t = WaitType(d.melds, winIdx);
        if (t != "tanki") return t;
        if (winIdx == d.pair) return "tanki";
        return "shanpon";
    }

    private static bool HasIipeikou(Decomp d)
    {
        var runs = d.melds.Where(m => m.run).Select(m => $"{(int)SuitOf(m.a)}:{NumOf(m.a)}").ToList();
        return runs.GroupBy(x => x).Any(g => g.Count() >= 2);
    }
    // d内の順子を「スート:開始数」でキー化し、同一順子の“ペア数”を返す
    private static int CountIipeikouPairs(Decomp d)
    {
        var runs = d.melds.Where(m => m.run).Select(m => $"{(int)SuitOf(m.a)}:{NumOf(m.a)}").ToList();
        return runs.GroupBy(x => x).Sum(g => g.Count() / 2);
    }
    // 二盃口判定：ペア数>=2
    private static bool HasRyanpeikou(Decomp d) => CountIipeikouPairs(d) >= 2;

    private static bool IsTanyaoAll(List<string> tiles)
    {
        foreach (var id in tiles)
        {
            int i = ToIndex(id); if (i < 0) return false;
            if (IsHonorIdx(i)) return false;
            int n = NumOf(i); if (n == 1 || n == 9) return false;
        }
        return true;
    }

    private static int CountYakuhaiTriplets(List<Meld> melds, int pair, string seatWind, string roundWind)
    {
        var ys = new HashSet<string> { "White", "Green", "Red", seatWind, roundWind };
        int cnt = 0; foreach (var m in melds) if (m.trip && ys.Contains(FromIndex(m.a))) cnt++;
        return Math.Min(3, cnt);
    }

    private static bool HasSanshokuDoujun(List<Meld> melds)
    {
        bool[,] has = new bool[3, 8];
        foreach (var m in melds.Where(x => x.run)) has[(int)SuitOf(m.a), NumOf(m.a)] = true;
        for (int s = 1; s <= 7; s++) if (has[0, s] && has[1, s] && has[2, s]) return true;
        return false;
    }

    private static bool HasIttsu(List<Meld> melds)
    {
        bool[,] has = new bool[3, 10];
        foreach (var m in melds.Where(x => x.run)) has[(int)SuitOf(m.a), NumOf(m.a)] = true;
        for (int s = 0; s < 3; s++) if (has[s, 1] && has[s, 4] && has[s, 7]) return true;
        return false;
    }

    private static bool IsChanta(List<Meld> melds, int pair)
    {
        if (!IsTerminalOrHonorIdx(pair)) return false;
        foreach (var m in melds)
        {
            if (m.run)
            {
                int st = NumOf(m.a); if (!(st == 1 || st == 7)) return false;
            }
            else
            {
                if (!IsTerminalOrHonorIdx(m.a)) return false;
            }
        }
        return true;
    }

    private static bool IsJunchan(List<Meld> melds, int pair)
    {
        if (IsHonorIdx(pair) || !(NumOf(pair) == 1 || NumOf(pair) == 9)) return false;
        foreach (var m in melds)
        {
            if (IsHonorIdx(m.a) || IsHonorIdx(m.b) || IsHonorIdx(m.c)) return false;
            if (m.run)
            {
                int st = NumOf(m.a); if (!(st == 1 || st == 7)) return false;
            }
            else
            {
                if (!(NumOf(m.a) == 1 || NumOf(m.a) == 9)) return false;
            }
        }
        return true;
    }

    private static bool IsToitoi(List<Meld> melds) => melds.All(m => m.trip);

private static int ConcealedTripletCountForYaku(List<Meld> melds, int pair, string winTile, bool isTsumo)
{
    if (melds == null) return 0;

    int baseCount = melds.Count(m => (m.trip || m.quad) && m.concealed);

    if (!isTsumo)
    {
        int w = ToIndex(winTile);
        var d = new Decomp { melds = melds, pair = pair };
        string wt = WaitTypeEx(d, w);

        if (wt == "shanpon")
        {
            bool completedTripletByRon = melds.Any(m =>
                m.trip &&
                !m.quad &&
                m.a == w);

            if (completedTripletByRon)
                baseCount -= 1;
        }
    }

    return baseCount;
}
    private static bool HasSanshokuDoukou(List<Meld> melds)
    {
        var nums = new Dictionary<int, HashSet<Suit>>();
        foreach (var m in melds.Where(x => x.trip && !IsHonorIdx(x.a)))
        {
            int num = NumOf(m.a); var s = SuitOf(m.a);
            if (!nums.ContainsKey(num)) nums[num] = new HashSet<Suit>();
            nums[num].Add(s);
        }
        return nums.Values.Any(set => set.Count >= 3);
    }

    private static bool IsShousangen(List<Meld> melds, int pair)
    {
        Func<int, bool> isDragon = (i) => i == ToIndex("White") || i == ToIndex("Green") || i == ToIndex("Red");
        int trip = melds.Count(m => m.trip && isDragon(m.a));
        bool pairD = isDragon(pair);
        return pairD && trip >= 2;
    }

    private static bool IsHonroutou(List<string> tiles) => tiles.All(t => { int i = ToIndex(t); return IsTerminalOrHonorIdx(i); });

    private static bool IsHonitsu(List<string> tiles)
    {
        var suits = new HashSet<Suit>(tiles.Where(t => !IsHonorIdx(ToIndex(t))).Select(t => SuitOf(ToIndex(t))));
        bool hasHonor = tiles.Any(t => IsHonorIdx(ToIndex(t)));
        return suits.Count == 1 && hasHonor;
    }

    private static bool IsChinitsu(List<string> tiles)
    {
        var suits = new HashSet<Suit>(tiles.Select(t => SuitOf(ToIndex(t))));
        return suits.Count == 1 && !suits.Contains(Suit.Honor);
    }

private static int CalcFu(Decomp d, string winTile, bool isTsumo, bool menzen, bool allowPinfu, string seatWind, string roundWind)
{
    // 平和（ピンフ）：門前のときだけ 20/30 符を適用する
    if (menzen && allowPinfu && IsPinfu(d, winTile, seatWind, roundWind))
        return isTsumo ? 20 : 30;

        int fu = 20;
        if (isTsumo) fu += 2;              // ツモ符
        if (menzen && !isTsumo) fu += 10;  // 門前ロン +10

        int w = ToIndex(winTile);
        string wt = WaitTypeEx(d, w);

        // 刻子/槓子の符（ロンのシャボは明刻扱い：刻子のみ対象）
        foreach (var m in d.melds)
        {
            if (!m.trip) continue;

            bool toh = IsTerminalOrHonorIdx(m.a);
            bool concealedForFu = m.concealed;

            // シャボロンで完成した「刻子」は明刻扱い（槓子には通常当てはまらないので除外）
            if (!isTsumo && wt == "shanpon" && !m.quad && m.a == w) concealedForFu = false;

            if (m.quad)
            {
                // 槓子（カン）
                // 明槓：中張 8 / 么九字 16
                // 暗槓：中張 16 / 么九字 32
                if (concealedForFu) fu += toh ? 32 : 16;
                else                fu += toh ? 16 : 8;
            }
            else
            {
                // 刻子（ポン/暗刻）
                // 明刻：中張 2 / 么九字 4
                // 暗刻：中張 4 / 么九字 8
                if (concealedForFu) fu += toh ? 8 : 4;
                else                fu += toh ? 4 : 2;
            }
        }

        // 雀頭が役牌なら +2（自風／場風／三元牌）
        int pair = d.pair;
        bool isDragon = pair == ToIndex("White") || pair == ToIndex("Green") || pair == ToIndex("Red");
        int seatIdx = ToIndex(seatWind);
        int roundIdx = ToIndex(roundWind);
        if (isDragon || pair == seatIdx || pair == roundIdx) fu += 2;

        // 待ちの符（シャボは +0）
        if (wt == "tanki" || wt == "kanchan" || wt == "penchan") fu += 2;

        fu = ((fu + 9) / 10) * 10; // 10の位切り上げ
        return fu;
    }
        // ScoringEngine と同じ“上限置換”で比較用ベース点を返す
    private static int BasePointForCompare(int han, int fuRaw)
    {
        if (han <= 0) return 0;

        if (han >= 13) return 8000 * Math.Max(1, han / 13); // 複合役満
        if (han >= 11) return 6000; // 三倍満
        if (han >= 8)  return 4000; // 倍満
        if (han >= 6)  return 3000; // 跳満

        int fu = Math.Max(20, ((fuRaw + 9) / 10) * 10);
        bool mangan =
            han >= 5 ||
            (han == 4 && fu >= 40) ||
            (han == 3 && fu >= 70);
        if (mangan) return 2000; // 満貫

        long basePoint = (long)fu * (1L << (han + 2));
        return (int)Math.Min(int.MaxValue, basePoint);
    }

}
