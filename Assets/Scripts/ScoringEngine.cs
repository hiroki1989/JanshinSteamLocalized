using System;

[Serializable]
public struct ScoreResult
{
    public bool validWin;
    public int fu;
    public int han;
    public bool isDealer;     // 親
    public bool isRon;        // ロン

    // 互換プロパティ（UIが参照しても落ちないように広めに定義）
    public string limitName;  // "", 満貫/跳満/倍満/三倍満/役満
    public string limitLabel; // 互換: limitName と同じ値を入れる
    public int basePoint;     // ベース点（2000/3000/4000/6000/8000 or 通常時 fu*2^(han+2)）
    public int totalPoints;   // 合計（表示用）
    public int total;         // 互換: totalPoints と同じ値

    // 支払の内訳（UI用）
    public int ronPoints;         // ロン受取総額
    public int tsumoDealerEach;   // 親ツモの各支払
    public int tsumoFromDealer;   // 子ツモで親から
    public int tsumoNonDealer;    // 子ツモで子から
    public string breakdown;      // 文字列内訳
}

public static class ScoringEngine
{
    // --- utility ---
    private static int Ceil100(int x) => ((x + 99) / 100) * 100;
    private static int Ceil10 (int x) => ((x + 9) / 10) * 10;

    /// <summary>
    /// 日本リーチ麻雀の最終得点を厳密に計算
    /// ・基本点 = fu(切上10) × 2^(han+2)
    /// ・満貫/跳満/倍満/三倍満/役満 はベース点(2000/3000/4000/6000/8000)に置換
    /// ・支払「毎」に100点切り上げ（ロンも切り上げ）
    /// ・戻りは UI 互換の ScoreResult
    /// </summary>
    public static ScoreResult Evaluate(int fuRaw, int han, bool isRon, bool isDealer)
    {
        #if UNITY_EDITOR
    var st = new System.Diagnostics.StackTrace();
    bool viaFacade = false;
    for (int i = 1; i < st.FrameCount; i++)
    {
        var m = st.GetFrame(i).GetMethod();
        if (m != null && m.DeclaringType != null &&
            m.DeclaringType.Name == "Scoring" && m.Name == "TryScoreWin")
        { viaFacade = true; break; }
    }
    if (!viaFacade)
    {
        var caller = st.FrameCount > 1 ? st.GetFrame(1).GetMethod() : null;
        UnityEngine.Debug.LogWarning(
            $"[SCORE][DirectCall] ScoringEngine.Evaluate を直接呼んでいます: " +
            $"{caller?.DeclaringType?.Name}.{caller?.Name}。Scoring.TryScoreWin 経由にしてください。");
    }
#endif
        var r = new ScoreResult
        {
            validWin = han >= 1,
            fu = fuRaw,
            han = han,
            isDealer = isDealer,
            isRon = isRon,
            limitName = "",
            limitLabel = "",
            basePoint = 0,
            totalPoints = 0,
            total = 0,
            ronPoints = 0,
            tsumoDealerEach = 0,
            tsumoFromDealer = 0,
            tsumoNonDealer = 0,
            breakdown = ""
        };
        if (!r.validWin) return r;

// 符は 10 の位に切り上げ。ただし七対子だけは 25 符固定で切り上げしない
int fu = (fuRaw == 25) ? 25 : Math.Max(20, Ceil10(fuRaw));

// 通常ベース点
long basePoint = fu * (1L << (han + 2));


        // 満貫以上は「ベース点置換」で上限適用
        // 役満:8000 / 三倍満:6000 / 倍満:4000 / 跳満:3000 / 満貫:2000
        // 満貫以上は「ベース点置換」で上限適用
        // 役満×n: base 8000 を n 倍 / 以下は従来どおり
        // 満貫以上は「ベース点置換」で上限適用
        // 役満×n: base 8000 を n 倍 / 以下は従来どおり
        if (han >= 13)
        {
            int mult = Math.Max(1, han / 13);
            basePoint = 8000 * mult;
            r.limitName =
                mult == 1 ? "役満" :
                mult == 2 ? "ダブル役満" :
                mult == 3 ? "トリプル役満" :
                $"役満×{mult}";
        }
        else if (han >= 11)
        {
            basePoint = 6000; r.limitName = "三倍満";
        }
        else if (han >= 8)
        {
            basePoint = 4000; r.limitName = "倍満";
        }
        else if (han >= 6)
        {
            basePoint = 3000; r.limitName = "跳満";
        }
        else
        {
            // 満貫条件：5翻以上、4翻40符以上、3翻70符以上
            bool mangan =
                han >= 5 ||
                (han == 4 && fu >= 40) ||
                (han == 3 && fu >= 70);
            if (mangan)
            {
                basePoint = 2000; r.limitName = "満貫";
            }
        }


        r.basePoint = (int)basePoint;
        r.limitLabel = r.limitName; // 互換

        // --- 支払計算（支払“毎”に100点切り上げ） ---
        if (isRon)
        {
            int total = isDealer
                ? Ceil100((int)(basePoint * 6))   // 親ロン
                : Ceil100((int)(basePoint * 4));  // 子ロン

            r.ronPoints = total;
            r.totalPoints = total;
            r.total = total; // 互換
            r.breakdown = total.ToString();
        }
        else
        {
            if (isDealer)
            {
                // 親ツモ：各 2*base
                int each = Ceil100((int)(basePoint * 2));
                r.tsumoDealerEach = each;
                r.totalPoints = each * 3;
                r.total = r.totalPoints;
                r.breakdown = $"(各){each}";
            }
            else
            {
                // 子ツモ：親 2*base、子 1*base
                int fromDealer = Ceil100((int)(basePoint * 2));
                int fromChild  = Ceil100((int)(basePoint * 1));
                r.tsumoFromDealer = fromDealer;
                r.tsumoNonDealer = fromChild;
                r.totalPoints = fromDealer + fromChild * 2;
                r.total = r.totalPoints;
                r.breakdown = $"(子){fromChild} (親){fromDealer}";
            }
        }

        return r;
    }
}
