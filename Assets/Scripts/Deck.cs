using System;
using System.Collections.Generic;

public class Deck {
    private List<Tile> wall = new List<Tile>();
    private System.Random rng = new System.Random();

    public Deck() { Reset(); }

    public void Reset() {
        wall.Clear();
        foreach (TileKind k in Enum.GetValues(typeof(TileKind))) {
            for (int i = 0; i < 4; i++) wall.Add(new Tile(k));
        }
        Shuffle();
    }

    private void Shuffle() {
        for (int i = wall.Count - 1; i > 0; i--) {
            int j = rng.Next(i + 1);
            var tmp = wall[i];
            wall[i] = wall[j];
            wall[j] = tmp;
        }
    }

    public Tile Draw() {
        if (wall.Count == 0) return null;
        var t = wall[wall.Count - 1];
        wall.RemoveAt(wall.Count - 1);
        return t;
    }

    public List<Tile> DrawMany(int n) {
        var list = new List<Tile>();
        for (int i = 0; i < n; i++) {
            var t = Draw();
            if (t == null) break;
            list.Add(t);
        }
        return list;
    }

    public int Remaining => wall.Count;

    public Tile PeekRandomFromRemaining() {
        if (wall.Count == 0) return null;
        return wall[rng.Next(wall.Count)];
    }

    // 演出用コピーを返す（消費しないオプションあり）
    public Tile GenerateOppDiscard(bool consumeFromWall = false) {
        if (wall.Count == 0) return null;
        int idx = rng.Next(wall.Count);
        var t = wall[idx];
        if (consumeFromWall) wall.RemoveAt(idx);
        return new Tile(t.Kind);
    }
}
