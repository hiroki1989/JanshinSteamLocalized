using System.Collections.Generic;

public class CheatSystem {
    public int CheatsLeftPerStage = 1;

    public bool CanCheat => CheatsLeftPerStage > 0;

    // selected は hand の中のインスタンス参照を受け取る想定
    public void RerollSelected(Deck deck, Hand hand, List<Tile> selected) {
        if (!CanCheat || selected == null || selected.Count == 0) return;
        foreach (var t in selected) {
            // hand から削除（Hand.Discardではriverに行くのでここは直接削除）
            hand.Tiles.Remove(t);
        }
        var news = deck.DrawMany(selected.Count);
        hand.Tiles.AddRange(news);
        CheatsLeftPerStage--;
    }
}
