using System.Collections.Generic;

public class RoundState {
    public List<TileKind> DoraIndicators = new List<TileKind>();
    public TileKind CurrentDora;

    // deckを覗いてランダムに表示牌を決める（MVP）
    public void InitDora(Deck deck) {
        var p = deck.PeekRandomFromRemaining();
        if (p != null) {
            DoraIndicators.Clear();
            DoraIndicators.Add(p.Kind);
            CurrentDora = p.Kind;
        }
    }

    // MVP: 表示牌と同種なら1枚につき1ドラ
    public int DoraBonus(Tile t) {
        if (t == null) return 0;
        return t.Kind == CurrentDora ? 1 : 0;
    }
}
