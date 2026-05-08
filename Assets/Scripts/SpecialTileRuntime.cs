using System.Collections.Generic;
using UnityEngine;

public static class SpecialTileRuntime
{
    // Equipped special tiles replace matching base tiles in wall/deck.
    // Must not break yaku/tenpai logic: the tile id keeps base prefix (Pin5/Man5/Sou5) and uses '_' suffix.
    public static void ApplyEquippedToWallIds(List<string> wallIds)
    {
        if (wallIds == null) return;

        var equipped = SpecialTileSystem.GetEquipped();
        if (equipped == null || equipped.Count == 0) return;

        for (int i = 0; i < equipped.Count; i++)
        {
            var e = equipped[i];
            string baseId = SpecialTileSystem.BaseIdOf(e.baseType);
            string spId = e.TileId();

            int idx = wallIds.FindIndex(x => x == baseId);
            if (idx >= 0)
            {
                wallIds[idx] = spId;
            }
        }
    }

    // Special Dora bonus: any special tile included => +1 dora each.
    // NOTE: tile id may be "Pin5_sp_common_L3" and/or may end with '*'.
    public static int CountSpecialDoraBonus(List<string> concealed14, List<List<string>> openMelds)
    {
        int c = 0;

        if (concealed14 != null)
        {
            for (int i = 0; i < concealed14.Count; i++)
            {
                if (IsSpecialTileId(concealed14[i])) c++;
            }
        }

        if (openMelds != null)
        {
            for (int m = 0; m < openMelds.Count; m++)
            {
                var meld = openMelds[m];
                if (meld == null) continue;
                for (int i = 0; i < meld.Count; i++)
                {
                    if (IsSpecialTileId(meld[i])) c++;
                }
            }
        }

        return c;
    }

public static int CountSpecialFuBonus(List<string> concealed14, List<List<string>> openMelds)
{
    // ★仕様変更：レア度による符ボーナスは廃止
    // （レジェンダリー効果の「符+16」は GameManager 側で effectId=6 として別途加算する）
    return 0;
}


    public static bool ContainsLegendaryEffect(List<string> concealed14, List<List<string>> openMelds, int effectId)
    {
        if (effectId <= 0) return false;

        if (concealed14 != null)
        {
            for (int i = 0; i < concealed14.Count; i++)
            {
                if (TryGetLegendaryEffectId(concealed14[i], out var fx) && fx == effectId) return true;
            }
        }

        if (openMelds != null)
        {
            for (int m = 0; m < openMelds.Count; m++)
            {
                var meld = openMelds[m];
                if (meld == null) continue;
                for (int i = 0; i < meld.Count; i++)
                {
                    if (TryGetLegendaryEffectId(meld[i], out var fx) && fx == effectId) return true;
                }
            }
        }

        return false;
    }

    // Sprite key: removes '*' and legendary suffix "_L{n}" so all legendary effects share same rarity sprite.
    public static string SpriteKeyFromTileId(string tileId)
    {
        if (string.IsNullOrEmpty(tileId)) return tileId;

        string s = tileId;
        if (s.EndsWith("*")) s = s.Substring(0, s.Length - 1);

        int li = s.IndexOf("_L", System.StringComparison.Ordinal);
        if (li >= 0) s = s.Substring(0, li);

        return s;
    }

    public static bool IsSpecialTileId(string tileId)
    {
        if (string.IsNullOrEmpty(tileId)) return false;

        string s = tileId;
        if (s.EndsWith("*")) s = s.Substring(0, s.Length - 1);

        return s.Contains("_sp");
    }

    public static bool TryGetRarity(string tileId, out SpecialTileSystem.Rarity rarity)
    {
        rarity = SpecialTileSystem.Rarity.Normal;
        if (string.IsNullOrEmpty(tileId)) return false;

        string s = tileId;
        if (s.EndsWith("*")) s = s.Substring(0, s.Length - 1);

        // expected: Base_sp_rarity[_Lx]
        int sp = s.IndexOf("_sp_", System.StringComparison.Ordinal);
        if (sp < 0) return false;

        int start = sp + 4;
        int end = s.IndexOf("_", start, System.StringComparison.Ordinal);
        if (end < 0) end = s.Length;

        string r = s.Substring(start, end - start);
        if (r == "normal") { rarity = SpecialTileSystem.Rarity.Normal; return true; }
        if (r == "common") { rarity = SpecialTileSystem.Rarity.Common; return true; }
        if (r == "rare") { rarity = SpecialTileSystem.Rarity.Rare; return true; }
        if (r == "epic") { rarity = SpecialTileSystem.Rarity.Epic; return true; }
        if (r == "legendary") { rarity = SpecialTileSystem.Rarity.Legendary; return true; }

        return false;
    }

    public static bool TryGetLegendaryEffectId(string tileId, out int effectId)
    {
        effectId = 0;
        if (string.IsNullOrEmpty(tileId)) return false;

        string s = tileId;
        if (s.EndsWith("*")) s = s.Substring(0, s.Length - 1);

        int li = s.IndexOf("_L", System.StringComparison.Ordinal);
        if (li < 0) return false;

        string num = s.Substring(li + 2);
        int v;
        if (!int.TryParse(num, out v)) return false;

        effectId = v;
        return true;
    }

private static int FuBonusOf(string tileId)
{
    // ★仕様変更：レア度による符ボーナスは廃止
    return 0;
}

}
