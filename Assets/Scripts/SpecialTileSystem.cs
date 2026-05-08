using System;
using System.Collections.Generic;
using UnityEngine;

public static class SpecialTileSystem
{
    // ===== Base Types (白は廃止：5だけ) =====
    public enum BaseType
    {
        Pin5 = 0,
        Man5 = 1,
        Sou5 = 2,
    }

    // ===== Rarity (5段階) =====
    public enum Rarity
    {
        Normal = 0,
        Common = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
    }
    // ===== Legendary Effect (当面は①～⑥) =====
    // effectId として Entry.effectId に格納する（既存の保存フォーマットを流用）
    public enum LegendaryEffect
    {
        None = 0,
        ExtraOmoteAndUraDora = 1,         // ①
        HalfNextEnemyWinDamage = 2,       // ②
        DoubleGoldThisWin = 3,            // ③
        DoubleTraitsIfUnderMangan = 4,    // ④
        HalfMpCostNextHand = 5,           // ⑤
        FuPlus16OnWin = 6,                // ⑥（和了時 +16符）
    }
    [Serializable]
public struct Entry
    {
        public BaseType baseType;
        public Rarity rarity;
        public int effectId; // LegendaryEffect を int で保持（Normal～Epic は 0）
        public int seed;

        // ★役強化（購入時に確定付与した結果を保存）
        // 形式例： "立直=1;混一色=2"（= の右が加算Lv、; 区切り）
        // Normal は空文字でOK
        public string traitBonusPacked;

        // ★ID仕様：
        //   - Spriteは rarity で出し分け
        //   - legendary effect は ID の末尾に付けるが、Sprite読みは suffix を落として共通化する
        //   例：
        //     Pin5_sp_common
        //     Pin5_sp_legendary_L3
        public string TileId()
        {
            string baseId = BaseIdOf(baseType);
            string r = RarityKey(rarity);

            if (rarity == Rarity.Legendary && effectId > 0)
                return $"{baseId}_sp_{r}_L{effectId}";

            return $"{baseId}_sp_{r}";
        }
    }

    [Serializable]
    public struct RollConfig
    {
        public float basePin5;
        public float baseMan5;
        public float baseSou5;

        public float rarityNormal;
        public float rarityCommon;
        public float rarityRare;
        public float rarityEpic;
        public float rarityLegendary;
    }

    private const string KEY_GEMS = "SP_Gems";
    private const string KEY_OWNED = "SP_Owned";
    private const string KEY_EQUIPPED = "SP_Equipped";
    private const string KEY_SLOTS = "SP_EquipSlots";

    // 所持上限（既存仕様：20）
    private const int OWNED_MAX = 20;

    public static int GetGems() => PlayerPrefs.GetInt(KEY_GEMS, 0);
    public static void SetGems(int v) { PlayerPrefs.SetInt(KEY_GEMS, Mathf.Max(0, v)); PlayerPrefs.Save(); }

    public static bool TryConsumeGems(int cost)
    {
        int g = GetGems();
        if (g < cost) return false;
        SetGems(g - cost);
        return true;
    }
public static void AddGems(int add)
{
    if (add <= 0) return;
    int v = GetGems();
    v = Mathf.Max(0, v + add);
    PlayerPrefs.SetInt("SP_Gems", v);
    PlayerPrefs.Save();
}

    public static int GetEquipSlotsUnlocked()
    {
        // 初期値1、最大4（既存仕様と同じ）
        return Mathf.Clamp(PlayerPrefs.GetInt(KEY_SLOTS, 1), 1, 4);
    }

    public static bool TryIncreaseEquipSlots(int costGems)
    {
        int slots = GetEquipSlotsUnlocked();
        if (slots >= 4) return false;
        if (!TryConsumeGems(costGems)) return false;
        PlayerPrefs.SetInt(KEY_SLOTS, Mathf.Clamp(slots + 1, 1, 4));
        PlayerPrefs.Save();
        return true;
    }

    public static List<Entry> GetOwned() => DeserializeList(PlayerPrefs.GetString(KEY_OWNED, ""));
    public static List<Entry> GetEquipped() => DeserializeList(PlayerPrefs.GetString(KEY_EQUIPPED, ""));

    public static bool CanAddOwned(int addCount)
    {
        var owned = GetOwned();
        int c = (owned != null) ? owned.Count : 0;
        return (c + addCount) <= OWNED_MAX;
    }
public static void AddOwned(Entry e)
{
    var owned = GetOwned() ?? new List<Entry>();
    if (owned.Count >= OWNED_MAX) return;

    owned.Add(e);
    PlayerPrefs.SetString(KEY_OWNED, SerializeList(owned));
    PlayerPrefs.Save();

    // ★実績：レジェンダリー特別牌入手
    try
    {
        if (e.rarity == Rarity.Legendary)
        {
            AchievementSystem.NotifyLegendarySpecialTileObtained();
        }
    }
    catch { }
}
    public static void DiscardOwned(Entry e)
    {
        var owned = GetOwned() ?? new List<Entry>();
        for (int i = owned.Count - 1; i >= 0; i--)
        {
            if (SameEntry(owned[i], e))
            {
                owned.RemoveAt(i);
                break;
            }
        }
        PlayerPrefs.SetString(KEY_OWNED, SerializeList(owned));
        PlayerPrefs.Save();

        // 装備中に同一個体があればそれも外す（複数装備対応）
        var eq = GetEquipped() ?? new List<Entry>();
        bool changed = false;
        for (int i = eq.Count - 1; i >= 0; i--)
        {
            if (SameEntry(eq[i], e))
            {
                eq.RemoveAt(i);
                changed = true;
                break;
            }
        }
        if (changed)
        {
            PlayerPrefs.SetString(KEY_EQUIPPED, SerializeList(eq));
            PlayerPrefs.Save();
        }
    }
public static bool TryEquipAppend(Entry e)
    {
        var eq = GetEquipped() ?? new List<Entry>();

        // ★同一個体の多重装備禁止
        for (int i = 0; i < eq.Count; i++)
        {
            if (SameEntry(eq[i], e)) return false;
        }

        int slots = GetEquipSlotsUnlocked();
        if (eq.Count >= slots) return false;

        eq.Add(e);
        PlayerPrefs.SetString(KEY_EQUIPPED, SerializeList(eq));
        PlayerPrefs.Save();
        return true;
    }
public static bool TryEquipReplaceAt(int slotIndex, Entry e)
    {
        var eq = GetEquipped() ?? new List<Entry>();
        int slots = GetEquipSlotsUnlocked();

        if (slotIndex < 0 || slotIndex >= slots) return false;

        // ★同一個体の多重装備禁止（slotIndex以外に同一があれば拒否）
        for (int i = 0; i < eq.Count; i++)
        {
            if (i == slotIndex) continue;
            if (SameEntry(eq[i], e)) return false;
        }

        // まだ枠が埋まっていない場合、末尾までを許容
        if (slotIndex >= eq.Count)
        {
            if (eq.Count >= slots) return false;
            eq.Add(e);
        }
        else
        {
            eq[slotIndex] = e;
        }

        PlayerPrefs.SetString(KEY_EQUIPPED, SerializeList(eq));
        PlayerPrefs.Save();
        return true;
    }

public static bool TryEquipReplace(Entry e)
    {
        var eq = GetEquipped() ?? new List<Entry>();
        int slots = GetEquipSlotsUnlocked();

        // ★同一個体の多重装備禁止（どのスロットにも同一があれば拒否）
        for (int i = 0; i < eq.Count; i++)
        {
            if (SameEntry(eq[i], e)) return false;
        }

        if (eq.Count < slots)
        {
            eq.Add(e);
        }
        else
        {
            eq[slots - 1] = e;
        }

        PlayerPrefs.SetString(KEY_EQUIPPED, SerializeList(eq));
        PlayerPrefs.Save();
        return true;
    }
    public static void UnequipAt(int slotIndex)
    {
        var eq = GetEquipped() ?? new List<Entry>();
        if (slotIndex < 0 || slotIndex >= eq.Count) return;
        eq.RemoveAt(slotIndex);
        PlayerPrefs.SetString(KEY_EQUIPPED, SerializeList(eq));
        PlayerPrefs.Save();
    }

    // 既存互換：BaseType指定で最初の1個だけ外す（複数装備がある場合は先頭のみ）
    public static void Unequip(BaseType baseType)
    {
        var eq = GetEquipped() ?? new List<Entry>();
        for (int i = 0; i < eq.Count; i++)
        {
            if (eq[i].baseType == baseType)
            {
                eq.RemoveAt(i);
                PlayerPrefs.SetString(KEY_EQUIPPED, SerializeList(eq));
                PlayerPrefs.Save();
                return;
            }
        }
    }

    public static bool TryGetEquipped(BaseType baseType, out Entry e)
    {
        var eq = GetEquipped();
        if (eq != null)
        {
            for (int i = 0; i < eq.Count; i++)
            {
                if (eq[i].baseType == baseType)
                {
                    e = eq[i];
                    return true;
                }
            }
        }
        e = default;
        return false;
    }
public static Entry Roll(RollConfig cfg, System.Random rng)
    {
        if (rng == null) rng = new System.Random();

        BaseType baseType = RollBase(cfg, rng);
        Rarity rarity = RollRarity(cfg, rng);

        int effectId = 0;
        if (rarity == Rarity.Legendary)
        {
            // ★仕様変更：①～⑥のどれか
            effectId = rng.Next(1, 7);
        }

        // ★仕様変更：役強化は「購入時に回数ぶん抽選して確定付与」
        //   Common=1, Rare=2, Epic=3, Legendary=3
        string packed = "";
        try
        {
            int rollCount = TraitRollCountByRarity(rarity);
            if (rollCount > 0)
                packed = RollTraitBonusPacked(rng, rollCount);
        }
        catch { packed = ""; }

        return new Entry
        {
            baseType = baseType,
            rarity = rarity,
            effectId = effectId,
            seed = rng.Next(),
            traitBonusPacked = packed
        };
    }
    // rarity → Lv+N
    public static int TraitLvAddOfRarity(Rarity r)
    {
        if (r == Rarity.Common) return 1;
        if (r == Rarity.Rare) return 2;
        if (r == Rarity.Epic) return 3;
        if (r == Rarity.Legendary) return 3;
        return 0;
    }

    private static BaseType RollBase(RollConfig cfg, System.Random rng)
    {
        float a = Mathf.Max(0f, cfg.basePin5);
        float b = Mathf.Max(0f, cfg.baseMan5);
        float c = Mathf.Max(0f, cfg.baseSou5);
        float sum = a + b + c;
        if (sum <= 0f) return BaseType.Pin5;

        float r = (float)(rng.NextDouble() * sum);
        if (r < a) return BaseType.Pin5;
        r -= a;
        if (r < b) return BaseType.Man5;
        return BaseType.Sou5;
    }

    private static Rarity RollRarity(RollConfig cfg, System.Random rng)
    {
        float n = Mathf.Max(0f, cfg.rarityNormal);
        float c = Mathf.Max(0f, cfg.rarityCommon);
        float r = Mathf.Max(0f, cfg.rarityRare);
        float e = Mathf.Max(0f, cfg.rarityEpic);
        float l = Mathf.Max(0f, cfg.rarityLegendary);

        float sum = n + c + r + e + l;
        if (sum <= 0f) return Rarity.Normal;

        float x = (float)(rng.NextDouble() * sum);
        if (x < n) return Rarity.Normal;
        x -= n;
        if (x < c) return Rarity.Common;
        x -= c;
        if (x < r) return Rarity.Rare;
        x -= r;
        if (x < e) return Rarity.Epic;
        return Rarity.Legendary;
    }

    // ===== Helpers =====
    public static string BaseIdOf(BaseType t)
    {
        switch (t)
        {
            case BaseType.Pin5: return "Pin5";
            case BaseType.Man5: return "Man5";
            case BaseType.Sou5: return "Sou5";
        }
        return "Pin5";
    }

    public static string RarityKey(Rarity r)
    {
        switch (r)
        {
            case Rarity.Normal: return "normal";
            case Rarity.Common: return "common";
            case Rarity.Rare: return "rare";
            case Rarity.Epic: return "epic";
            case Rarity.Legendary: return "legendary";
        }
        return "normal";
    }

public static bool SameEntry(Entry a, Entry b)
    {
        return a.baseType == b.baseType
            && a.rarity == b.rarity
            && a.effectId == b.effectId
            && a.seed == b.seed
            && (a.traitBonusPacked ?? "") == (b.traitBonusPacked ?? "");
    }
private static string SerializeList(List<Entry> list)
    {
        if (list == null || list.Count == 0) return "";
        // 形式：base|rarity|effect|seed|traitBonusPacked を , 連結
        // 旧データ互換：traitBonusPacked が無い(4要素)も読めるようにする
        var parts = new List<string>();
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];

            string packed = e.traitBonusPacked ?? "";
            packed = packed.Replace("|", " ").Replace(",", " ");

            parts.Add($"{(int)e.baseType}|{(int)e.rarity}|{e.effectId}|{e.seed}|{packed}");
        }
        return string.Join(",", parts);
    }
private static List<Entry> DeserializeList(string s)
    {
        var list = new List<Entry>();
        if (string.IsNullOrEmpty(s)) return list;

        var items = s.Split(',');
        for (int i = 0; i < items.Length; i++)
        {
            var it = items[i];
            if (string.IsNullOrEmpty(it)) continue;

            var f = it.Split('|');
            if (f.Length < 4) continue;

            int bt;
            int rr;
            int fx;
            int sd;

            if (!int.TryParse(f[0], out bt)) continue;
            if (!int.TryParse(f[1], out rr)) continue;
            if (!int.TryParse(f[2], out fx)) fx = 0;
            if (!int.TryParse(f[3], out sd)) sd = 0;

            string packed = "";
            if (f.Length >= 5) packed = f[4] ?? "";

            // 旧データ互換：
            //  - White(旧3)は廃止なので捨てる
            //  - 旧rarity(1..3) が残っていても、とりあえず Normal/ Common / Rare に丸める
            if (bt < 0 || bt > 2) continue;

            Rarity rarity;
            if (rr >= 0 && rr <= 4)
            {
                rarity = (Rarity)rr;
            }
            else
            {
                // 旧仕様 rr=1..3 が来た想定
                // 旧1=ドラ+1相当 → Normal
                // 旧2/3 は Common/Rare に寄せる（将来的に調整可能）
                if (rr == 1) rarity = Rarity.Normal;
                else if (rr == 2) rarity = Rarity.Common;
                else rarity = Rarity.Rare;
            }

            // Legendary 以外は effectId を無効化
            if (rarity != Rarity.Legendary) fx = 0;

            list.Add(new Entry
            {
                baseType = (BaseType)bt,
                rarity = rarity,
                effectId = fx,
                seed = sd,
                traitBonusPacked = packed
            });
        }

        return list;
    }
// ===== Trait Bonus (Passive) =====

// rarity → 抽選回数
public static int TraitRollCountByRarity(Rarity r)
{
    if (r == Rarity.Common) return 1;
    if (r == Rarity.Rare) return 2;
    if (r == Rarity.Epic) return 3;
    if (r == Rarity.Legendary) return 3;
    return 0;
}
// 「全役から抽選」用プール
// ※YakuEvaluator が返す役名と一致している必要があります。必要に応じて調整してください。
private static readonly string[] _allYakuPool =
{
    "平和","タンヤオ","一盃口",
    "三色同順","一気通貫","チャンタ","純チャン","対々和","三暗刻","三カンツ","小三元","混一色","清一色",
    "七対子",
    "役牌",
    "国士無双","四暗刻","大三元","字一色","緑一色","清老頭","小四喜","大四喜","四カンツ","九蓮宝燈"
};
private static string RollOneYakuKey(System.Random rng)
{
    if (rng == null) rng = new System.Random();
    if (_allYakuPool == null || _allYakuPool.Length <= 0) return "";
    int idx = rng.Next(0, _allYakuPool.Length);
    return _allYakuPool[idx] ?? "";
}

// rollCount 回ぶん 1個ずつ抽選し、同一役は加算して packed にする
public static string RollTraitBonusPacked(System.Random rng, int rollCount)
{
    if (rollCount <= 0) return "";

    var dict = new Dictionary<string, int>();
    for (int i = 0; i < rollCount; i++)
    {
        string key = (RollOneYakuKey(rng) ?? "").Trim();
        if (string.IsNullOrEmpty(key)) continue;

        int v = 0;
        dict.TryGetValue(key, out v);
        dict[key] = Mathf.Max(0, v + 1);
    }

    // packed: "立直=1;混一色=2"
    var parts = new List<string>();
    foreach (var kv in dict)
    {
        string k = (kv.Key ?? "").Replace(";", " ").Replace("=", " ").Replace("|", " ").Replace(",", " ").Trim();
        int v = Mathf.Max(0, kv.Value);
        if (string.IsNullOrEmpty(k) || v <= 0) continue;
        parts.Add($"{k}={v}");
    }

    return string.Join(";", parts);
}

public static Dictionary<string, int> UnpackTraitBonus(string packed)
{
    var dict = new Dictionary<string, int>();
    if (string.IsNullOrEmpty(packed)) return dict;

    var items = packed.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
    for (int i = 0; i < items.Length; i++)
    {
        var it = items[i];
        if (string.IsNullOrEmpty(it)) continue;

        var kv = it.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
        if (kv.Length < 2) continue;

        string k = (kv[0] ?? "").Trim();
        if (string.IsNullOrEmpty(k)) continue;

        int v = 0;
        if (!int.TryParse(kv[1], out v)) v = 0;
        v = Mathf.Max(0, v);

        if (v <= 0) continue;

        int prev = 0;
        dict.TryGetValue(k, out prev);
        dict[k] = Mathf.Max(0, prev + v);
    }

    return dict;
}

// 装備中の特別牌の役強化（パッシブ）を、役名キーで合算して返す
public static Dictionary<string, int> GetEquippedTraitBonusMap()
{
    var map = new Dictionary<string, int>();
    var eq = GetEquipped();
    if (eq == null || eq.Count <= 0) return map;

    for (int i = 0; i < eq.Count; i++)
    {
        var e = eq[i];
        if (string.IsNullOrEmpty(e.traitBonusPacked)) continue;

        var d = UnpackTraitBonus(e.traitBonusPacked);
        foreach (var kv in d)
        {
            int prev = 0;
            map.TryGetValue(kv.Key, out prev);
            map[kv.Key] = Mathf.Max(0, prev + Mathf.Max(0, kv.Value));
        }
    }

    return map;
}

// 単体取得（GameManager側から呼びやすい）
public static int GetEquippedTraitBonusLv(string yakuKey)
{
    if (string.IsNullOrEmpty(yakuKey)) return 0;

    int sum = 0;
    var eq = GetEquipped();
    if (eq == null || eq.Count <= 0) return 0;

    for (int i = 0; i < eq.Count; i++)
    {
        var e = eq[i];
        if (string.IsNullOrEmpty(e.traitBonusPacked)) continue;

        var d = UnpackTraitBonus(e.traitBonusPacked);
        int v = 0;
        if (d.TryGetValue(yakuKey, out v))
            sum += Mathf.Max(0, v);
    }

    return Mathf.Max(0, sum);
}
}
