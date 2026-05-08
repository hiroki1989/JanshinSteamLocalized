using System.Collections.Generic;

public class Hand {
    public List<Tile> Tiles = new List<Tile>();
    public List<Tile> River = new List<Tile>();

    public void Add(Tile t) { if (t != null) Tiles.Add(t); }

    public void Discard(Tile t) {
        if (t == null) return;
        // 先に見つけて削除（同種が複数あってもインスタンス一致で削除）
        if (Tiles.Remove(t)) {
            River.Add(t);
        } else {
            // もしインスタンス一致しなければ Kind で削除（簡易処理）
            var match = Tiles.Find(x => x.Kind == t.Kind);
            if (match != null) {
                Tiles.Remove(match);
                River.Add(match);
            }
        }
    }
}
