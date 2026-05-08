using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// お守りの効果をゲーム進行に適用するパーシャル。
/// - Awake() で最大HP上昇を反映
/// - 敵からのダメージに被ダメ軽減を適用するためのヘルパ
/// - 和了スコア表示(ShowScoring)で撃/癒/瞬/レジェ特効を反映するためのヘルパ
/// - カン時のドラ表示牌+1 を呼び出すヘルパ
/// 既存 GameManager の最小改変でフックする前提。
/// </summary>
public partial class GameManager : MonoBehaviour
{
    // レジェンダリー「役満→次の3回ダブル」の残回数
    private int _omamoriDoubleWinsRemain = 0;

    // ====== Awake フック（最大HP上昇） ======
    private void Omamori_ApplyAtAwake()
    {
        var s = PlayerData.GetEquippedStats();
        if (s.maxHpUp > 0f)
        {
            int before = playerMaxHP <= 0 ? 100 : playerMaxHP;
            playerMaxHP = Mathf.RoundToInt(before * (1f + s.maxHpUp));
            if (playerHP < 0) playerHP = playerMaxHP; // 初期化時
        }
        // MP 系は未実装ならNOP（将来対応）
    }

    // ====== 被ダメ軽減（敵ダメージ計算の直前で使う） ======
    public static int Omamori_ModifyIncomingDamage(int rawDamage)
    {
        var s = PlayerData.GetEquippedStats();
        if (rawDamage <= 0) return 0;
        if (s.dmgTakenDown <= 0f) return rawDamage;
        float mult = Mathf.Clamp01(1f - s.dmgTakenDown);
        return Mathf.Max(0, Mathf.RoundToInt(rawDamage * mult));
    }

// お守り：撃/癒/瞬の上乗せ（実効果用）
// ※呼び出し側で「この局で撃/癒/瞬が実際に発生したか」を判定して渡す。
// ====== 撃/癒/瞬の上乗せ（“該当時だけ”） ======
private void Omamori_ApplyTraitBoosts(
    ref int attack,      // （暫定）トレイト最終攻撃値。撃が出た時だけ%を掛ける
    ref int heal,        // 癒が出た時だけ%を掛ける
    ref int shunAdd,     // 瞬が出た時だけ%を掛ける（※加算パーツそのものを増やす）
    bool hasGeki, bool hasIyu, bool hasShun)
{
    var s = PlayerData.GetEquippedStats();
    if (hasGeki && s.gekiDmgUp > 0f)  attack  = Mathf.RoundToInt(attack  * (1f + s.gekiDmgUp));
    if (hasIyu  && s.iyuHealUp > 0f)  heal    = Mathf.RoundToInt(heal    * (1f + s.iyuHealUp));
    if (hasShun && s.shunAddUp > 0f)  shunAdd = Mathf.RoundToInt(shunAdd * (1f + s.shunAddUp));
}



    // ====== ShowScoring 内での補助（ダメージ倍率/加点/特殊） ======
    private void Omamori_ApplyScoringModifiers(List<string> yaku, ref float mult, ref int extra, ref int damage, List<string> lines)
    {
        // 撃/癒/瞬は ComputeGekiShunIyu 側で attack/heal/shunAdd に既に乗せる想定。
        // ここでは最終ダメージへの特殊効果などに集中。

        // [特殊1] 役満 → 次の3回ダブルを付与
        if (PlayerData.EquippedHasSpecial(PlayerData.OmamoriSpecial.YakumanNext3WinsDouble))
        {
            if (ContainsYakuman(yaku))
            {
                _omamoriDoubleWinsRemain = 3;
                lines?.Add("お守り[特殊] 役満達成 → 次の3回の和了ダメージ×2 を付与");
            }
        }

        // [特殊1の適用] 残回数があれば今回も×2（役満直後は次回から、の解釈もあり得るが仕様は“次の3回”なので今回は未適用）
        if (_omamoriDoubleWinsRemain > 0)
        {
            damage = Mathf.RoundToInt(damage * 2f);
            _omamoriDoubleWinsRemain--;
            lines?.Add($"お守り[特殊] 役満ボーナス適用 → ダメージ×2（残 {_omamoriDoubleWinsRemain}）");
        }

        // ここに今後のスコア関連お守りを追加
    }

    private static bool ContainsYakuman(List<string> yaku)
    {
        if (yaku == null) return false;
        foreach (var t in yaku)
        {
            var s = (t ?? "").Trim();
            if (s.Contains("役満")) return true; // 互換的判定（詳細役名は環境依存）
        }
        return false;
    }

    // ====== カン時：ドラ表示牌をさらに+1 ======
    // カン処理の直後（既存で AddKanIndicator() を呼んでいる箇所）でこのメソッドをもう一度呼ぶだけ。
    public void Omamori_TryAddExtraDoraAfterKan()
    {
        if (PlayerData.EquippedHasSpecial(PlayerData.OmamoriSpecial.ExtraDoraOnKan))
        {
            AddKanIndicator(); // 既存の公開/内部メソッド
        }
    }
    // ====== 丸めを最後に1回だけにするための float 版 ======
private void Omamori_ApplyTraitBoostsPrecise(
    ref float attack,    // ← 丸めない
    ref int heal,
    ref int shunAdd,
    bool hasGeki, bool hasIyu, bool hasShun)
{
    var s = PlayerData.GetEquippedStats();
    if (hasGeki && s.gekiDmgUp > 0f)  attack  = attack  * (1f + s.gekiDmgUp);   // 丸めない
    if (hasIyu  && s.iyuHealUp > 0f)  heal    = Mathf.RoundToInt(heal    * (1f + s.iyuHealUp));
    if (hasShun && s.shunAddUp > 0f)  shunAdd = Mathf.RoundToInt(shunAdd * (1f + s.shunAddUp));
}

}
