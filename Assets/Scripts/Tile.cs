using System;

[Serializable]
public enum TileKind {
    // 萬子 1-9
    Man1,Man2,Man3,Man4,Man5,Man6,Man7,Man8,Man9,
    // 筒子 1-9
    Pin1,Pin2,Pin3,Pin4,Pin5,Pin6,Pin7,Pin8,Pin9,
    // 索子 1-9
    Sou1,Sou2,Sou3,Sou4,Sou5,Sou6,Sou7,Sou8,Sou9,
    // 字牌
    East,South,West,North,White,Green,Red
}

[Serializable]
public class Tile {
    public TileKind Kind;
    public float ScoreMultiplier = 1f;
    public int ScoreBonus = 0;

    public Tile(TileKind kind) {
        Kind = kind;
    }

    // 字牌か
    public bool IsHonor() => Kind >= TileKind.East;

    // 1/9 か（端牌）
    public bool IsTerminal() {
        if (IsHonor()) return false;
        int index = (int)Kind; // 0..等
        int posInSuit = index % 9; // 0..8 -> 1..9
        int number = posInSuit + 1;
        return number == 1 || number == 9;
    }
}
