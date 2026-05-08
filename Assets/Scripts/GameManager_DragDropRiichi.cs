using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class GameManager : MonoBehaviour
{
    // リーチ直後 1 回だけ即ツモチェックするためのラッチ
    private bool __riichiLatched = false;

    private void LateUpdate()
    {
        // 旧「テンパイ確定」ボタンは常に非表示
        if (btnTenpaiConfirm != null && btnTenpaiConfirm.gameObject.activeSelf)
            btnTenpaiConfirm.gameObject.SetActive(false);

        // リーチ直後は「そのターンのツモ4枚に和了牌があってもツモらず、自動捨て開始」が正しい仕様
        if (isRiichi && !__riichiLatched)
        {
            __riichiLatched = true;

            // 既存UI再評価
            EvaluateWinUI_New();

            // 黄色ハイライトもこのタイミングで必ず最新化する
            RefreshOfferWinningHighlights();

            if (phase == Phase.Offer && !_autoConfirmOfferPending)
            {
                _autoConfirmOfferPending = true;
                StartCoroutine(_AutoConfirmOfferAfter(0.05f));
            }
        }
        else if (!isRiichi && __riichiLatched)
        {
            // 新しい局などでリセット
            __riichiLatched = false;
        }
    }

    // リーチ宣言直後に、残っているオファー内に上がり牌があるか軽量チェック
    // 成功したら true を返す（現在仕様では自動ツモには使わず、保険用）
    private bool TryAutoTsumoFromOffers()
    {
        try
        {
            if (offers == null || offers.Count == 0) return false;

            foreach (var tile in offers)
            {
                if (string.IsNullOrEmpty(tile)) continue;

                var concealed = new List<string>(hand);
                if (concealed.Count != 13) continue; // 手牌は13枚の想定
                concealed.Add(tile);                 // 14枚にして評価

                if (CanWinByReflection(concealed, tile))
                {
                    // ツモ上がりできる牌がオファー内にある
                    return true;
                }
            }
        }
        catch
        {
            // 例外は握りつぶし（即ツモは行わない）
        }
        return false;
    }

    // --- YakuEvaluator.Evaluate のシグネチャ差異に耐えるための反射呼び出し ---
    // 1) Evaluate(List<string>, IList<IList<string>>, string, bool, bool, string, string)
    // 2) Evaluate(List<string>, IList<IList<string>>, string, bool, bool)
    // いずれかが見つかったら呼び出し、戻り値の .han >= 1 をもって「上がれる」とする。
    // seat/round wind が必要な実装でも、見つかれば GameManager のフィールド/プロパティから取得し、
    // 無ければ既定 "East" を使う（ビルドを通すためのフォールバック）。
    private bool CanWinByReflection(List<string> concealed14, string winTile)
    {
        var t = typeof(YakuEvaluator);
        var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                       .Where(m => m.Name == "Evaluate");

        var open = new List<IList<string>>(); // 副露なし

        foreach (var m in methods)
        {
            var ps = m.GetParameters();
            object[] args = null;

            if (ps.Length == 7)
            {
                args = new object[]
                {
                    concealed14, open, winTile,
                    true,
                    true,
                    GetWindSafe("seatWind"),
                    GetWindSafe("roundWind")
                };
            }
            else if (ps.Length == 5)
            {
                args = new object[]
                {
                    concealed14, open, winTile,
                    true,
                    true
                };
            }
            else
            {
                continue;
            }

            try
            {
                var res = m.Invoke(null, args);
                if (res == null) continue;

                var hanProp = res.GetType().GetProperty("han", BindingFlags.Public | BindingFlags.Instance);
                if (hanProp != null)
                {
                    int han = Convert.ToInt32(hanProp.GetValue(res));
                    if (han >= 1) return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }

    // GameManager 内の風フィールド/プロパティを反射で探す。無ければ "East"
    private string GetWindSafe(string name)
    {
        try
        {
            var tp = this.GetType();
            var f = tp.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null) return f.GetValue(this) as string ?? "East";
            var p = tp.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null) return p.GetValue(this) as string ?? "East";
        }
        catch
        {
        }
        return "East";
    }
}