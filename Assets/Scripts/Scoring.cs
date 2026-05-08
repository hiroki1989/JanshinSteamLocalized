using System;

// NOTE: このファサードは GameManager など既存の呼び出し元と型シグネチャを
// 変更せずに動かすため、引数名を isTsumo のままにしています（名前付き引数対応）。
// 実計算は ScoringEngine に一本化し、ロン/ツモの別はここで反転して渡します。
public static class Scoring
{
    /// <summary>
    /// 和了の最終点計算を行います。
    /// fu / han は役・符計算の結果をそのまま渡してください。
    /// isTsumo==true のときはツモ和了、false のときはロン和了。
    /// isDealer は親かどうか。
    /// </summary>
    public static ScoreResult TryScoreWin(int fu, int han, bool isTsumo, bool isDealer)
    {
        // ScoringEngine は isRon を受け取るため、ツモなら false、ロンなら true に変換
        bool isRon = !isTsumo;
        return ScoringEngine.Evaluate(fu, han, isRon, isDealer);
    }
}
