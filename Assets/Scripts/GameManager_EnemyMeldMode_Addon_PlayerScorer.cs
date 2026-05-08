// GameManager_EnemyMeldMode_Addon_PlayerScorer_REISSUE_8.cs
// 点数計算のみ差し替え（UI は変更しない）。
// - 敵 = 南家（対面）
// - ツモ & リーチ前提
// - 役・符も計算して保持（UI が読み取れる public 値を用意）
// - 既存の呼び出しと互換のため EnemyAddon_ComputeScoreLikePlayer(int) も提供。

using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public partial class GameManager
{
public int EnemyAddon_LastPoints { get; private set; } = 0;
public int EnemyAddon_LastHan    { get; private set; } = 0;
public int EnemyAddon_LastFu     { get; private set; } = 0;
public string EnemyAddon_LastYakuText { get; private set; } = "";
public string EnemyAddon_LastDoraText { get; private set; } = "";

private static string EnemyScoreFixedText_Local(string key)
{
    var lm = LocalizationManager.Instance;
    if (lm == null) return key;
    return lm.GetFixedText(key);
}

public class EnemyScoreDetail
{
    public int points;
    public int han;
    public int fu;
    public bool isDealer;
    public bool isTsumo;
    public bool isRiichi;
    public List<string> yaku = new List<string>();


public string BuildYakuLine()
{
    // 役名の表記ゆれを吸収し、リーチ/ツモを最初に並べる
    var list = new List<string>();
    var seenBase = new HashSet<string>();

string Normalize(string raw)
{
    if (string.IsNullOrEmpty(raw)) return raw;

// 表記揺れを寄せる（部分一致置換を避ける）
var s = raw;
if (s == "門前清自摸和") s = EnemyScoreFixedText_Local("win_tsumo");
if (s == "立直") s = EnemyScoreFixedText_Local("yaku_riichi_short");

    // 全角/半角ゆらぎ吸収
    s = s.Replace("＋", "+").Replace("＋１", "+1").Replace("＋1", "+1");

    return s;
}
    string BaseKey(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var t = s.Replace(" ", "").Replace("　", "");
        // (+1) / +1 を重複判定では無視
        t = t.Replace("(+1)", "").Replace("+1", "");
        return t;
    }

    void AddOnce(string s)
    {
        if (string.IsNullOrEmpty(s)) return;

        var n = Normalize(s);
        var key = BaseKey(n);
        if (string.IsNullOrEmpty(key)) return;

        // 既に同ベースがあるなら追加しない（ツモ と ツモ(+1) を二重にしない）
        if (!seenBase.Add(key)) return;

        list.Add(n);
    }
// 先に「ダブル立直」「一発」を並べたい（かつ、ダブル立直のときは立直を別途出さない）
bool hasDoubleRiichiInList = yaku != null && yaku.Any(v => Normalize(v) == EnemyScoreFixedText_Local("yaku_double_riichi_short"));
bool hasIppatsuInList      = yaku != null && yaku.Any(v => Normalize(v) == EnemyScoreFixedText_Local("yaku_ippatsu_short"));

if (hasDoubleRiichiInList) AddOnce(EnemyScoreFixedText_Local("yaku_double_riichi_short"));
else if (isRiichi)         AddOnce(EnemyScoreFixedText_Local("yaku_riichi_short"));

if (hasIppatsuInList) AddOnce(EnemyScoreFixedText_Local("yaku_ippatsu_short"));

if (isTsumo) AddOnce(EnemyScoreFixedText_Local("win_tsumo"));
    if (yaku != null)
    {
        foreach (var raw in yaku)
        {
            if (string.IsNullOrEmpty(raw)) continue;

            var s = Normalize(raw);
            if (s == "役なし") continue;

            // ドラ系はそのまま（ただし完全一致でなくベース重複排除される）
            AddOnce(s);
        }
    }

if (list.Count == 0)
    return ""; // 「役なし」は絶対に表示しない

return string.Join("・", list);
}

public string BuildFuLine()   => $"{EnemyScoreFixedText_Local("fu_prefix")}{fu}";
    }

    /// <summary>
public int EnemyAddon_ComputeScoreLikePlayer(List<List<string>> committedMelds, List<string> committedPair)
{
    var detail = EnemyAddon_ComputeScoreDetailLikePlayer(committedMelds, committedPair);
    EnemyAddon_LastPoints   = detail.points;
    EnemyAddon_LastHan      = detail.han;
    EnemyAddon_LastFu       = detail.fu;
    EnemyAddon_LastYakuText = detail.BuildYakuLine();
    EnemyAddon_LastDoraText = CalculateDoraText(); // ドラ情報を計算して設定

    // ★実績：敵和了（天和など）をここで拾う
    try { AchievementSystem.NotifyEnemyWin(detail.yaku); } catch { }

    // ★敵のスコア計算メソッド内では、カットインの表示は行わない。
    //   カットイン表示は GameManager_EnemyMeldMode_Addon 側の
    //   FinalizeEnemyWin_ShowScoringAndCleanup →
    //   __EnemyWin_ShowCutinAndScoring_Flow_Co →
    //   __WinCutInThenShowScoring で一括制御する。

    // scoringPanel が開いたタイミングで役・符などを追記
    try { EnemyAddon_MarkAppendYakuFu(); } catch { }

    // ★修正: 敵の点数計算パネルのUIを更新
    UpdateScoringPanelUI();

    return detail.points;
}
// === 敵スコア詳細（UI 表示用） ===
public int EnemyAddon_LastOmamoriPct { get; private set; } = 0;
public int EnemyAddon_LastFinalDamage { get; private set; } = 0;

public EnemyScoreDetail EnemyAddon_ComputeScoreDetailLikePlayer(
        List<List<string>> committedMelds, List<string> committedPair)
{
    return __EnemyAddon_ComputeMahjongScoreDetail_ByYakuEvaluator(committedMelds, committedPair);
}
private EnemyScoreDetail __EnemyAddon_ComputeMahjongScoreDetail_ByYakuEvaluator(
    List<List<string>> committedMelds, List<string> committedPair)
{
    var detail = new EnemyScoreDetail();

    // 直近の和了フラグを反映（EnemyMeldMode_Addon 側で設定される想定）
    detail.isTsumo  = _enemyLastWinWasTsumo;
    detail.isRiichi = _enemyLastWinWasRiichi;

    // committedMelds(4面子) + committedPair(2枚) から 14枚を組み立てる
    var tiles14 = new List<string>(14);

    if (committedMelds != null)
    {
        foreach (var m in committedMelds)
        {
            if (m == null) continue;
            for (int i = 0; i < m.Count; i++)
            {
                if (!string.IsNullOrEmpty(m[i])) tiles14.Add(m[i]);
            }
        }
    }
    if (committedPair != null)
    {
        for (int i = 0; i < committedPair.Count; i++)
        {
            if (!string.IsNullOrEmpty(committedPair[i])) tiles14.Add(committedPair[i]);
        }
    }

    // 異常系は従来フォールバック（デグレ防止）
    if (tiles14.Count != 14)
    {
        return __EnemyAddon_FallbackMahjongScoreDetail(committedMelds, committedPair);
    }

    // 役判定はプレイヤーと同じ：logic 化して YakuEvaluator へ
    var tiles14Logic = tiles14.Select(StripTileIdForLogic).ToList();

    // 敵は（現仕様）副露UIの手ではなく「確定面子を作るAI」なので、open meld 扱いは無し（プレイヤーと同じ evaluator 経路に揃える）
    IList<IList<string>> openMeldsLogic = new List<IList<string>>();

    // winTile は evaluator に必要。
    // ★重要：敵がロンした場合、「最後の1枚」が和了牌とは限らず、ここがズレると
    //  - ロンで完成した刻子が暗刻扱いになる（＝三暗刻/四暗刻判定や符が狂う）
    //  - カンチャン/ペンチャン/単騎など待ちの符が落ちる
    // などの不具合が起きる。
    // EnemyMeldMode_Addon 側で保持している直近和了牌 _enemyLastWinTileId を優先して使う。
    string winTileLogic = null;
    if (!string.IsNullOrEmpty(_enemyLastWinTileId))
    {
        var w = StripTileIdForLogic(_enemyLastWinTileId);
        if (!string.IsNullOrEmpty(w) && tiles14Logic.Contains(w))
            winTileLogic = w;
    }
    if (string.IsNullOrEmpty(winTileLogic))
        winTileLogic = StripTileIdForLogic(tiles14[tiles14.Count - 1]);

    // 自風/場風：プレイヤーの自風から「下家＝敵」を計算（あなたの仕様：敵はプレイヤーの下家）
    string enemySeatWind = __EnemyAddon_GetEnemySeatWind_FromPlayerSeat();
    string roundWind     = GetRoundWind();

detail.isDealer = true;   // ★敵は常に親扱い

    var ev = YakuEvaluator.Evaluate(
        tiles14Logic, openMeldsLogic, winTileLogic,
        isTsumo: detail.isTsumo,
        isClosed: true,
        seatWind: enemySeatWind,
        roundWind: roundWind);

var yakuList = ParseYakuList(ev.breakdown);
yakuList.RemoveAll(y => y.Contains("役なし"));

int yakumanCount = yakuList.Count(__EnemyAddon_IsYakumanName);
bool hasYakuman = yakumanCount > 0;

if (hasYakuman)
{
    // 単独役満（＝数え役満ではない）を含む場合：
    // 立直/ダブル立直/一発/ドラ/裏ドラ/特別牌ドラ は一切加算しない
    ev.han = 13 * yakumanCount;
    ev.fu = 0;

    // 表示役も役満だけに絞る（複数役満はその数だけ残す）
    yakuList = yakuList.Where(__EnemyAddon_IsYakumanName).Distinct().ToList();
}
else
{
    // (1) ダブル立直 / 立直 / 一発
    if (detail.isRiichi)
    {
        bool isDoubleRiichi = (_enemyRiichiDeclaredTurnCounter == 1);

        if (isDoubleRiichi)
        {
            if (!yakuList.Contains("ダブル立直")) yakuList.Add("ダブル立直");
            ev.han += 2;
        }
        else
        {
            if (!yakuList.Contains("立直")) yakuList.Add("立直");
            ev.han += 1;
        }

        bool isIppatsu =
            _enemyRiichiDeclaredTurnCounter >= 0 &&
            (
                (!detail.isTsumo && _enemyWinDeclaredTurnCounter == _enemyRiichiDeclaredTurnCounter) ||
                (detail.isTsumo && _enemyWinDeclaredTurnCounter == _enemyRiichiDeclaredTurnCounter + 1)
            );

        if (isIppatsu)
        {
            if (!yakuList.Contains("一発")) yakuList.Add("一発");
            ev.han += 1;
        }
    }
    // ★敵がリーチ和了なら、裏ドラ表示牌が未展開の可能性があるため、ここで展開を試みる
    __EnemyAddon_TryRevealUraIndicatorsIfNeeded(detail.isRiichi);

    // (2) ドラ（役満でない時だけ加算）
    int normalDora = __EnemyAddon_CountDoraHits_Local_WithIndicatorToDora(tiles14, doraIndicators);

int spBonus = 0;
try { spBonus = SpecialTileRuntime.CountSpecialDoraBonus(tiles14, new List<List<string>>()); } catch { spBonus = 0; }

    if (normalDora > 0)
    {
        ev.han += normalDora;
        yakuList.Add(string.Format(EnemyScoreFixedText_Local("dora_count_format"), normalDora));
    }
    if (spBonus > 0)
    {
        ev.han += spBonus;
        yakuList.Add(string.Format(EnemyScoreFixedText_Local("special_tile_dora_count_format"), spBonus));
    }

    // (3) 裏ドラ（リーチ時だけ）
    if (detail.isRiichi && uraIndicators != null && uraIndicators.Count > 0)
    {
        int uraCount = __EnemyAddon_CountDoraHits_Local(tiles14, uraIndicators);

        if (uraCount > 0)
        {
            ev.han += uraCount;
            yakuList.Add(string.Format(EnemyScoreFixedText_Local("ura_dora_count_format"), uraCount));
        }
    }
}


    // 点数
    var sr = Scoring.TryScoreWin(ev.fu, ev.han, isTsumo: detail.isTsumo, isDealer: detail.isDealer);

    detail.fu     = sr.fu;
    detail.han    = sr.han;
    detail.points = sr.totalPoints;

    detail.yaku = yakuList ?? new List<string>();
    return detail;
}
private static bool __EnemyAddon_IsYakumanName(string y)
{
    if (string.IsNullOrEmpty(y)) return false;

    // YakuEvaluator が返す役満名に合わせる
    return y == "国士無双"
        || y == "九蓮宝燈"
        || y == "大三元"
        || y == "大四喜"
        || y == "小四喜"
        || y == "字一色"
        || y == "清老頭"
        || y == "緑一色"
        || y == "四暗刻";
}

// 追加：ドラ表示牌(インジケータ)を「実ドラ」に変換して数える版
private int __EnemyAddon_CountDoraHits_Local_WithIndicatorToDora(
    List<string> tiles14Raw,
    IList<string> indicatorRaw)
{
    if (tiles14Raw == null || indicatorRaw == null || indicatorRaw.Count == 0) return 0;

    // 手牌も表示牌も「logic 用ID」に寄せる（末尾サフィックス等があっても吸収）
    var tiles = tiles14Raw.Select(StripTileIdForLogic).Where(s => !string.IsNullOrEmpty(s)).ToList();
    var indicators = indicatorRaw.Select(StripTileIdForLogic).Where(s => !string.IsNullOrEmpty(s)).ToList();

    // 表示牌 -> 実ドラ（次牌）へ
    string Next(string ind)
    {
        // ここはあなたのプロジェクト内の既存ID体系に合わせて分岐する。
        // 典型：Man1..Man9 / Pin1..Pin9 / Sou1..Sou9 / East/South/West/North / White/Green/Red
        // もし既に「NextDoraId」や同等が GameManager 側にあるなら、それを呼ぶのが最優先。
        // 無い場合でも、最低限「9->1」「字牌循環」だけはここで保証する。

        // --- 数牌 ---
        if (ind.StartsWith("Man") || ind.StartsWith("Pin") || ind.StartsWith("Sou"))
        {
            var suit = ind.Substring(0, 3);
            if (int.TryParse(ind.Substring(3), out var n))
            {
                var next = (n == 9) ? 1 : (n + 1);
                return suit + next.ToString();
            }
        }

        // --- 風牌 ---
        if (ind == "East")  return "South";
        if (ind == "South") return "West";
        if (ind == "West")  return "North";
        if (ind == "North") return "East";

        // --- 三元牌 ---
        if (ind == "White") return "Green";
        if (ind == "Green") return "Red";
        if (ind == "Red")   return "White";

        // 変換できないIDはそのまま（＝一致したらドラ扱い）
        return ind;
    }

    var doraTiles = indicators.Select(Next).ToList();

    int count = 0;
    foreach (var d in doraTiles)
        count += tiles.Count(t => t == d);

    return count;
}

public void EnemyAddon_SetLastYakuFuDora(string yakuText, int fu, string doraText)
{
    EnemyAddon_LastYakuText = yakuText ?? "";
    EnemyAddon_LastFu = fu;
    EnemyAddon_LastDoraText = doraText ?? "";
}


private string __EnemyAddon_GetEnemySeatWind_FromPlayerSeat()
{
    // 風の並びは GetPlayerSeatWind() と同じ前提：East,South,West,North
    string p = GetPlayerSeatWind();
    string[] winds = { "East", "South", "West", "North" };

    int pi = Array.IndexOf(winds, p);
    if (pi < 0) pi = 0;

    // 敵はプレイヤーの下家：次の席（E->S->W->N->E）
    int ei = (pi + 1) % 4;
    return winds[ei];
}

    /// <summary> 得点=ダメージとしてプレイヤーHPから減算（UIには触れない） </summary>
    public void EnemyAddon_ApplyDamageByScore(int score)
    {
        int dmg = Mathf.Max(1, score);
        playerHP = Mathf.Max(0, playerHP - dmg);
    }

    // ================== 既存プレイヤースコアラー呼び出し（点数のみ） ==================
    private int __EnemyAddon_TryInvokePlayerScorer(List<List<string>> melds, List<string> pair, List<string> tiles14)
    {
        try
        {
            var flags = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;

            bool NameHit(MethodInfo m)
            {
                string mn = m.Name.ToLowerInvariant();
                string tn = m.DeclaringType != null ? m.DeclaringType.Name.ToLowerInvariant() : "";
                bool a = (mn.Contains("score") || mn.Contains("point") || mn.Contains("hanfu") || mn.Contains("agari") || mn.Contains("yaku"));
                bool b = (mn.Contains("calc") || mn.Contains("compute") || mn.Contains("count"));
                bool c = (mn.Contains("player") || mn.Contains("hand") || tn.Contains("player") || tn.Contains("hand"));
                return (a && (b || c)) || (a && tn.Contains("score"));
            }

            var methods = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
                .SelectMany(t => { try { return t.GetMethods(flags); } catch { return new MethodInfo[0]; } })
                .Where(NameHit)
                .ToList();

            // 引数準備
            object argTiles_ListStr  = tiles14;
            object argTiles_ArrayStr = tiles14.ToArray();
            object argMelds          = melds?.Select(mm => new List<string>(mm)).ToList() ?? new List<List<string>>();
            object argPair           = pair  != null ? new List<string>(pair) : new List<string>();
            string winTypeString     = "tsumo";  // ツモ前提

            foreach (var m in methods)
            {
                var ps = m.GetParameters();
                object target = m.IsStatic ? null :
                                (m.DeclaringType == typeof(GameManager) ? (object)this : Activator.CreateInstance(m.DeclaringType));

                // (List<List<string>>, List<string>, ...) 形式
                if (ps.Length >= 2 &&
                    ps[0].ParameterType.IsGenericType &&
                    ps[0].ParameterType.GetGenericTypeDefinition() == typeof(List<>) &&
                    ps[0].ParameterType.GetGenericArguments()[0].IsGenericType &&
                    ps[0].ParameterType.GetGenericArguments()[0].GetGenericTypeDefinition() == typeof(List<>) &&
                    ps[1].ParameterType == typeof(List<string>))
                {
                    try
                    {
                        var args = new object[ps.Length];
                        args[0] = argMelds;
                        args[1] = argPair;
                        for (int i=2;i<ps.Length;i++)
                        {
                            var pt = ps[i].ParameterType;
                            if (pt == typeof(bool)) args[i] = true;        // tsumo
                            else if (pt == typeof(string)) args[i] = winTypeString;
                            else if (pt == typeof(int)) args[i] = 0;
                            else args[i] = Type.Missing;
                        }
                        var r = m.Invoke(target, args);
                        if (r is int iv && iv > 0) return iv;
                    } catch {}
                }

                // (List<string>) / (IEnumerable<string>) / (string[]) 形式
                if (ps.Length >= 1)
                {
                    object arg0 = null;
                    bool ok0 = true;
                    var pt0 = ps[0].ParameterType;
                    if (pt0 == typeof(List<string>)) arg0 = argTiles_ListStr;
                    else if (pt0 == typeof(string[])) arg0 = argTiles_ArrayStr;
                    else if (typeof(IEnumerable<string>).IsAssignableFrom(pt0)) arg0 = argTiles_ListStr;
                    else ok0 = false;
                    if (!ok0) continue;

                    var args = new object[ps.Length];
                    args[0] = arg0;
                    for (int i=1;i<ps.Length;i++)
                    {
                        var pt = ps[i].ParameterType;
                        if (pt == typeof(bool)) args[i] = true;       // tsumo
                        else if (pt == typeof(int)) args[i] = 0;
                        else if (pt == typeof(string)) args[i] = winTypeString;
                        else args[i] = Type.Missing;
                    }
                    try
                    {
                        var r = m.Invoke(target, args);
                        if (r is int iv && iv > 0) return iv;
                    } catch {}
                }
            }
        }
        catch {}
        return 0;
    }
private EnemyScoreDetail __EnemyAddon_FallbackMahjongScoreDetail(List<List<string>> committedMelds, List<string> committedPair)
{
    var detail = new EnemyScoreDetail();
detail.isDealer = true;   // ★敵は常に親扱い

    // ★修正: 「常にリーチ＋ツモ前提」ではなく、直近のフラグを使用する
    //  - _enemyLastWinWasRiichi / _enemyLastWinWasTsumo は
    //    GameManager_EnemyMeldMode_Addon 側で和了時に設定する
    detail.isTsumo  = _enemyLastWinWasTsumo;
    detail.isRiichi = _enemyLastWinWasRiichi;

    // -----------------------------
    // まずは YakuEvaluator で「プレイヤーと同等の役判定」を通す
    // （ロン時の“ロン牌で完成した刻子は暗刻扱いしない”等もここで吸収）
    // -----------------------------
    try
    {
        // committedMelds(4面子) + committedPair(2枚) から 14 枚を再構成
        var tiles14 = new List<string>();
        if (committedMelds != null)
            foreach (var m in committedMelds)
                if (m != null) tiles14.AddRange(m);

        if (committedPair != null) tiles14.AddRange(committedPair);

        // 直近の和了牌（ロン/ツモ共通）
        var win = _enemyLastWinTileId;
        if (!string.IsNullOrEmpty(win))
        {
            // ロジック用（* や _sp 等を落とす）
            string winLogic = StripStar(win);

            // concealed13 を作る（tiles14 から win を 1 枚だけ抜く）
            var concealed13 = new List<string>(tiles14.Select(t => StripStar(t)));
            int rm = concealed13.IndexOf(winLogic);
            if (rm >= 0) concealed13.RemoveAt(rm);

            // 敵は鳴き無し前提（open は空）
            var open = new List<IList<string>>();

            var eval = YakuEvaluator.Evaluate(
                concealed13,
                open,
                winLogic,
                isTsumo: detail.isTsumo,
                isClosed: true,
                seatWind: GetWindSafe("seatWind"),
                roundWind: GetWindSafe("roundWind")
            );

            // 役リストを breakdown から拾う（"|" より左、"+" 区切り）
            detail.yaku.Clear();
            if (!string.IsNullOrEmpty(eval.breakdown))
            {
                string head = eval.breakdown;
                int bar = head.IndexOf('|');
                if (bar >= 0) head = head.Substring(0, bar);

                var parts = head.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in parts)
                {
                    var name = p.Trim();
                    if (string.IsNullOrEmpty(name)) continue;
                    if (name == "役なし") continue;
                    detail.yaku.Add(name);
                }
            }

            int han = Mathf.Max(0, eval.han);
            int fu  = Mathf.Max(0, eval.fu);

            fu = Mathf.Max(fu, 20);

            // ★修正: スコア計算側も detail.isTsumo を使う
            var sr = Scoring.TryScoreWin(fu, han, isTsumo: detail.isTsumo, isDealer: false);

            detail.han    = sr.han;
            detail.fu     = sr.fu;
            detail.points = sr.totalPoints;
            return detail;
        }
    }
    catch
    {
        // 下の旧フォールバックへ
    }

    // -----------------------------
    // 旧フォールバック（保険）
    // -----------------------------
    var melds = committedMelds?.Where(m => m != null && m.Count == 3).Select(m => new List<string>(m)).ToList()
                ?? new List<List<string>>();
    var pair  = committedPair != null ? new List<string>(committedPair) : null;

    // ---------- 役判定（門前前提：リーチ/ツモを加算） ----------
    int hanFallback = 0;
    detail.yaku.Clear();
    if (detail.isRiichi) { hanFallback += 1; detail.yaku.Add("立直"); }
if (detail.isTsumo)
{
    hanFallback += 1;

    // ★二重追加防止：「門前清自摸和」も「ツモ」も既に入っていたら追加しない
    bool already =
        (detail.yaku != null && (detail.yaku.Contains("門前清自摸和") || detail.yaku.Contains("ツモ")));
    if (!already)
        detail.yaku.Add("門前清自摸和");
}


    // 端牌/字牌/数牌などの特徴を集計
    bool hasHonor = false;
    bool allSimples = true;
    var suitSet = new HashSet<int>(); // 0:man 1:pin 2:sou

    foreach (var m in melds)
    {
        bool parsed0 = __EnemyAddon_TryParseSuitNum(m[0], out int s0, out int n0);
        bool parsed1 = __EnemyAddon_TryParseSuitNum(m[1], out int s1, out int n1);
        bool parsed2 = __EnemyAddon_TryParseSuitNum(m[2], out int s2, out int n2);
        if (parsed0) suitSet.Add(s0); else hasHonor = true;
        if (parsed1) suitSet.Add(s1); else hasHonor = true;
        if (parsed2) suitSet.Add(s2); else hasHonor = true;

        if (!(parsed0 && n0 >= 2 && n0 <= 8)) allSimples = false;
        if (!(parsed1 && n1 >= 2 && n1 <= 8)) allSimples = false;
        if (!(parsed2 && n2 >= 2 && n2 <= 8)) allSimples = false;
    }
    if (pair != null)
    {
        foreach (var id in pair)
        {
            if (!__EnemyAddon_TryParseSuitNum(id, out int s, out int n)) { hasHonor = true; allSimples = false; }
            else if (n < 2 || n > 8) allSimples = false;
            else suitSet.Add(s);
        }
    }

    // 対々和
    bool allPon = melds.Count == 4 && melds.All(m => m[0] == m[1] && m[1] == m[2]);
    if (allPon) { hanFallback += 2; detail.yaku.Add("対々和"); }

    // タンヤオ
    if (allSimples && !hasHonor) { hanFallback += 1; detail.yaku.Add("断么九"); }

    // イッツー
    for (int s = 0; s <= 2; s++)
    {
        bool has123 = melds.Any(m => __EnemyAddon_IsChi(m, s, 1));
        bool has456 = melds.Any(m => __EnemyAddon_IsChi(m, s, 4));
        bool has789 = melds.Any(m => __EnemyAddon_IsChi(m, s, 7));
        if (has123 && has456 && has789) { hanFallback += 1; detail.yaku.Add("一気通貫(食い下げ)"); break; }
    }
    // 三色同順
    for (int num = 1; num <= 7; num++)
    {
        bool m0 = melds.Any(m => __EnemyAddon_IsChi(m, 0, num));
        bool m1 = melds.Any(m => __EnemyAddon_IsChi(m, 1, num));
        bool m2 = melds.Any(m => __EnemyAddon_IsChi(m, 2, num));
        if (m0 && m1 && m2) { hanFallback += 1; detail.yaku.Add("三色同順(食い下げ)"); break; }
    }
    // 混一色/清一色
    var suitsOnly = new HashSet<int>(suitSet.Where(x => x >= 0));
    if (suitsOnly.Count == 1)
    {
        if (hasHonor) { hanFallback += 2; detail.yaku.Add("混一色(食い下げ)"); }
        else          { hanFallback += 5; detail.yaku.Add("清一色(食い下げ)"); }
    }
    // チャンタ / 純チャン
    bool allSetsContain19orHonor = melds.All(m => __EnemyAddon_SetHas19orHonor(m)) && pair != null && __EnemyAddon_SetHas19orHonor(pair);
    bool allSetsContain19Only    = melds.All(m => __EnemyAddon_SetHas19(m))        && pair != null && __EnemyAddon_SetHas19(pair);
    if (allSetsContain19orHonor) { hanFallback += 1; detail.yaku.Add("全帯么(食い下げ)"); }
    if (allSetsContain19Only && !hasHonor) { hanFallback += 2; detail.yaku.Add("純全帯么(食い下げ)"); }

    // 混老頭
    bool honroutou = melds.All(m => __EnemyAddon_SetIs19orHonorOnly(m)) && pair != null && __EnemyAddon_SetIs19orHonorOnly(pair);
    if (honroutou) { hanFallback += 2; detail.yaku.Add("混老頭"); }

    // ★四暗刻（簡易判定）
    int pungCount = 0;
    foreach (var m in melds) { if (m[0] == m[1] && m[1] == m[2]) pungCount++; }

    if (melds.Count == 4 && pungCount >= 4)
    {
        hanFallback = 13;
        detail.yaku.Clear();
        detail.yaku.Add("四暗刻");
    }
    else
    {
        if (pungCount >= 3) { hanFallback += 2; detail.yaku.Add("三暗刻"); }
    }

    // ---------- 符計算（旧） ----------
    int fuFallback = 20;
    fuFallback = Mathf.Max(fuFallback, 20);

    var srFallback = Scoring.TryScoreWin(fuFallback, hanFallback, isTsumo: detail.isTsumo, isDealer: false);
    detail.han    = srFallback.han;
    detail.fu     = srFallback.fu;
    detail.points = srFallback.totalPoints;
    return detail;
}

    // ====== 小物関数 ======
    private bool __EnemyAddon_TryParseSuitNum(string id, out int suit, out int num)
    {
        suit = -1; num = -1;
        if (string.IsNullOrEmpty(id) || id.Length < 3) return false;
        string p = id.Substring(0,3).ToLowerInvariant();
        if (p == "man") suit = 0;
        else if (p == "pin") suit = 1;
        else if (p == "sou") suit = 2;
        else return false;
        string ns = id.Substring(3);
        int n;
        if (!int.TryParse(ns, out n)) return false;
        num = n;
        return true;
    }
        // ★敵のリーチ和了時：裏ドラ表示牌が未展開なら、本体に存在する可能性が高い「裏ドラ展開」処理を反射で呼ぶ
    // （GameManager.cs がこの /mnt/data に無いため、ここでは“正確引用での本体修正”ができない。よって安全に反射で呼ぶ）
    private void __EnemyAddon_TryRevealUraIndicatorsIfNeeded(bool isRiichi)
    {
        if (!isRiichi) return;

        try
        {
            // 既に裏ドラ表示牌が入っているなら何もしない
            var fiUra = GetType().GetField("uraIndicators", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fiUra != null)
            {
                var listObj = fiUra.GetValue(this) as System.Collections.ICollection;
                if (listObj != null && listObj.Count > 0) return;
            }

            // 既存の「裏ドラを含める」フラグがあれば true にする
            var fiInclude = GetType().GetField("_includeUraForScoring", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiInclude != null && fiInclude.FieldType == typeof(bool))
                fiInclude.SetValue(this, true);

            // 既存の裏ドラ展開メソッドがあれば呼ぶ
            var miReveal = GetType().GetMethod("RevealUraDoraIfEligible", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (miReveal != null)
                miReveal.Invoke(this, null);
        }
        catch { }
    }

    // ★表示牌 → 実ドラ（次牌）への変換（字牌は正しい循環：東南西北 / 白發中）
    private string __EnemyAddon_NextDoraFromIndicator(string indicatorId)
    {
        if (string.IsNullOrEmpty(indicatorId)) return null;

        // 既存ユーティリティ（別partialにある想定）を使用
        string id = StripTileIdForLogic(StripStar(indicatorId));
        if (string.IsNullOrEmpty(id)) return null;

        // 数牌（Man/Pin/Sou）
        if (__EnemyAddon_TryParseSuitNum(id, out int suit, out int num))
        {
            int next = num + 1;
            if (next > 9) next = 1;
            string prefix = (suit == 0) ? "Man" : (suit == 1) ? "Pin" : "Sou";
            return prefix + next;
        }

        // 風牌：東南西北で循環（北の次は東）
        if (id == "East")  return "South";
        if (id == "South") return "West";
        if (id == "West")  return "North";
        if (id == "North") return "East";

        // 三元牌：白發中で循環（中=Red の次は白）
        if (id == "White") return "Green";
        if (id == "Green") return "Red";
        if (id == "Red")   return "White";

        return null;
    }

    // ★敵スコア計算用：ドラ表示牌リストから実ドラを求め、tiles14 内の一致数を数える
    private int __EnemyAddon_CountDoraHits_Local(List<string> tiles14, IList<string> indicators)
    {
        if (tiles14 == null || indicators == null) return 0;

        // indicator→actual の重み（同じ実ドラが複数表示牌で重なるケースも加算）
        var weightByActual = new Dictionary<string, int>();
        for (int i = 0; i < indicators.Count; i++)
        {
            var actual = __EnemyAddon_NextDoraFromIndicator(indicators[i]);
            if (string.IsNullOrEmpty(actual)) continue;

            if (weightByActual.TryGetValue(actual, out int w)) weightByActual[actual] = w + 1;
            else weightByActual[actual] = 1;
        }
        if (weightByActual.Count == 0) return 0;

        int hits = 0;
        for (int i = 0; i < tiles14.Count; i++)
        {
            var t = tiles14[i];
            if (string.IsNullOrEmpty(t)) continue;

            string tid = StripTileIdForLogic(StripStar(t));
            if (string.IsNullOrEmpty(tid)) continue;

            if (weightByActual.TryGetValue(tid, out int w))
                hits += w;
        }
        return hits;
    }

    private bool __EnemyAddon_IsChi(List<string> meld, int suit, int startNum)
    {
        if (meld.Count != 3) return false;
        return __EnemyAddon_TryParseSuitNum(meld[0], out int s0, out int n0) &&
               __EnemyAddon_TryParseSuitNum(meld[1], out int s1, out int n1) &&
               __EnemyAddon_TryParseSuitNum(meld[2], out int s2, out int n2) &&
               s0==suit && s1==suit && s2==suit &&
               new HashSet<int>{n0,n1,n2}.SetEquals(new[]{startNum, startNum+1, startNum+2});
    }
    private bool __EnemyAddon_SetHas19orHonor(IEnumerable<string> set)
    {
        foreach (var id in set)
        {
            if (!__EnemyAddon_TryParseSuitNum(id, out int s, out int n)) return true; // honor
            if (n==1 || n==9) return true;
        }
        return false;
    }
    private bool __EnemyAddon_SetHas19(IEnumerable<string> set)
    {
        foreach (var id in set)
        {
            if (!__EnemyAddon_TryParseSuitNum(id, out int s, out int n)) return false; // honor含むとfalse
            if (n==1 || n==9) return true;
        }
        return false;
    }
    private bool __EnemyAddon_SetIs19orHonorOnly(IEnumerable<string> set)
    {
        foreach (var id in set)
        {
            if (!__EnemyAddon_TryParseSuitNum(id, out int s, out int n)) continue; // honor OK
            if (!(n==1 || n==9)) return false;
        }
        return true;
    }
    private bool __EnemyAddon_IsDragon(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        string s = id.ToLowerInvariant();
        return s.Contains("chun") || s.Contains("hatsu") || s.Contains("haku") ||
               s.Contains("red")  || s.Contains("green") || s.Contains("white") ||
               s.Contains("中") || s.Contains("發") || s.Contains("発") || s.Contains("白");
    }
    private bool __EnemyAddon_IsRoundWind(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        string s = id.ToLowerInvariant();
        return s.Contains("ton") || s.Contains("east") || s.Contains("東");
    }
    private bool __EnemyAddon_IsSeatWind(string id)
    {
        // 敵＝南家（対面）
        if (string.IsNullOrEmpty(id)) return false;
        string s = id.ToLowerInvariant();
        return s.Contains("nan") || s.Contains("south") || s.Contains("南");
    }
private string CalculateDoraText()
{
    // ドラ表示用のGameObjectを参照
    GameObject doraDisplay = GameObject.Find("DoraDisplay"); // ドラ表示用のオブジェクト名を指定
    if (doraDisplay == null)
    {
        Debug.LogWarning("DoraDisplayオブジェクトが見つかりません。");
        return "";
    }

    // ドラ表示用のImageコンポーネントを取得
    var doraImage = doraDisplay.GetComponent<Image>();
    if (doraImage == null || doraImage.sprite == null)
    {
        Debug.LogWarning("DoraDisplayオブジェクトにImageコンポーネントが存在しないか、スプライトが設定されていません。");
        return "";
    }

    // ドラのスプライト名からドラ情報を取得（裏ドラは除外）
    string doraName = doraImage.sprite.name;
    if (doraName.Contains("裏ドラ")) return ""; // 裏ドラを表示しない
    return string.Format("{0}{1}", EnemyScoreFixedText_Local("dora_label_prefix"), doraName);
}
}
