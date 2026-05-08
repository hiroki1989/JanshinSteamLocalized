using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 最小機能の山。34種×4枚をシャッフルして扱う。
/// ワン牌(DeadWall)として末尾を予約し、リンシャンやドラ表示に使用する簡易版。
/// </summary>
public class SimpleDeck
{
    private List<int> wall = new List<int>(); // 0..33 の牌ID
    private int drawIndex = 0;                // 先頭から引く
    private int deadWallReserved = 14;        // ワン牌の残数
    private int revealedIndicators = 1;       // 表になっているドラ表示の枚数

    public int Remaining => Mathf.Max(0, wall.Count - drawIndex - deadWallReserved);
    public int DeadWallRemaining => deadWallReserved;
    public int DoraIndicatorCount => revealedIndicators;

    public SimpleDeck()
    {
        wall.Clear();
        for (int id = 0; id < 34; id++)
            for (int c = 0; c < 4; c++)
                wall.Add(id);

        // 乱数シャッフル
        for (int i = 0; i < wall.Count; i++)
        {
            int j = Random.Range(i, wall.Count);
            (wall[i], wall[j]) = (wall[j], wall[i]);
        }

        drawIndex = 0;
        deadWallReserved = 14;
        revealedIndicators = 1;
    }

    /// <summary>ワン牌として末尾に n 枚を確保（既定14）。</summary>
    public void ReserveDeadWall(int n) { deadWallReserved = Mathf.Max(0, n); }

    /// <summary>通常ツモ（ワン牌を消費しない）。</summary>
    public int Draw()
    {
        if (Remaining <= 0) return -1;
        return wall[drawIndex++];
    }

    /// <summary>複数枚引く。</summary>
    public List<int> DrawMany(int n)
    {
        var res = new List<int>();
        for (int i = 0; i < n; i++)
        {
            int t = Draw();
            if (t < 0) break;
            res.Add(t);
        }
        return res;
    }

    /// <summary>リンシャン牌（ワン牌から1枚）。簡易実装：ワン牌残数を減らし通常Draw。</summary>
    public int DrawRinshan()
    {
        if (deadWallReserved <= 0) return -1;
        deadWallReserved--;
        // 厳密には末尾から取るが、最小実装として通常Drawで代用
        return Draw();
    }

    /// <summary>現在の「表ドラ表示」牌ID（簡易）。</summary>
    public int PeekDoraIndicator()
    {
        // 末尾の手前から revealedIndicators 番目をインジケータとみなす簡易表現
        int idx = wall.Count - deadWallReserved - revealedIndicators;
        idx = Mathf.Clamp(idx, 0, wall.Count - 1);
        return wall[idx];
    }

    /// <summary>カン時に次のドラ表示を1枚増やす。</summary>
    public int RevealNextDoraIndicator()
    {
        revealedIndicators = Mathf.Max(1, revealedIndicators + 1);
        return PeekDoraIndicator();
    }
}
