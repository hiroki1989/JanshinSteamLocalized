using System;
using System.Collections.Generic;
using UnityEngine;
public static partial class PlayerData
{
    // ===== 通貨 =====
    public static int Coins = 0;   // お守り購入など
    public static int Gold  = 0;   // 強化画面用（敵撃破で+10000）

    // ===== ステージ進行 =====
    public static int CurrentStage = 0;   // 0..2
    public static int CurrentEnemy  = 0;  // ステージ内の敵インデックス

    // 各ステージの目標スコア（敵ごと）
    public static readonly int[][] StageTargets = new int[][]
    {
        new int[]{ 2000, 5000, 8000 }, // stage1
        new int[]{ 15000, 20000, 30000 }, // stage2
        new int[]{ 15000, 20000, 30000 }, // stage3
    };

    public static int StageTarget =>
        StageTargets[Mathf.Clamp(CurrentStage,0,StageTargets.Length-1)]
                    [Mathf.Clamp(CurrentEnemy ,0,StageTargets[CurrentStage].Length-1)];

    // ===== デッキ（山を作るテンプレ） =====
    // 0..33 のタイルIDを複数持つ多重集合
    public static List<int> DeckTemplate = new List<int>();

    public static void EnsureDefaultDeck()
    {
        if (DeckTemplate.Count > 0) return;
        DeckTemplate.Clear();
        // 標準の牌構成（各4枚）
        for (int t = 0; t < 34; t++)
            for (int n = 0; n < 4; n++)
                DeckTemplate.Add(t);
    }
    // ===== スキル（数牌→マンズ） =====
    // GameManager から参照されます
    public static int SkillChargesBase = 3;   // 1局あたり初期回数

    // ===== スキル解放（永続） =====
    private const string SkillKey_DyeMaster = "RandomMan";
    private const string SkillKey_Calligrapher = "EnhanceHand";
    private const string SkillKey_Capitalist = "Capitalist";

    private const string PrefKey_SkillUnlocked_Calligrapher = "PF_SkillUnlocked_Calligrapher";
    private const string PrefKey_SkillUnlocked_Capitalist = "PF_SkillUnlocked_Capitalist";

    private const string PrefKey_SkillRestriction_Calligrapher = "PF_SkillRestriction_Calligrapher";
    private const string PrefKey_SkillRestriction_Capitalist = "PF_SkillRestriction_Capitalist";

    private const string PrefKey_PendingSkillUnlockQueue = "PF_PendingSkillUnlockQueue";
    private const string PrefKey_InitialLanguageSelectionCompleted = "PF_InitialLanguageSelectionCompleted";
    private const string PrefKey_GuideReadPrefix = "PF_GuideRead_";
    private const string PrefKey_HasUnreadGuide = "PF_HasUnreadGuide";

    public static bool HasCompletedInitialLanguageSelection()
    {
        try
        {
            return PlayerPrefs.GetInt(PrefKey_InitialLanguageSelectionCompleted, 0) != 0;
        }
        catch
        {
            return false;
        }
    }

    public static void MarkInitialLanguageSelectionCompleted()
    {
        try
        {
            PlayerPrefs.SetInt(PrefKey_InitialLanguageSelectionCompleted, 1);
            PlayerPrefs.Save();
        }
        catch
        {
        }
    }

    private static string NormalizeGuideReadKey(string rawGuideKey)
    {
        if (string.IsNullOrWhiteSpace(rawGuideKey)) return "";

        string key = rawGuideKey.Trim();
        key = key.Replace("\r", " ");
        key = key.Replace("\n", " ");
        key = key.Replace("\t", " ");

        return key;
    }

    public static bool HasReadGuide(string rawGuideKey)
    {
        string key = NormalizeGuideReadKey(rawGuideKey);
        if (string.IsNullOrEmpty(key))
            return false;

        try
        {
            return PlayerPrefs.GetInt(PrefKey_GuideReadPrefix + key, 0) != 0;
        }
        catch
        {
            return false;
        }
    }

    public static void MarkGuideAsRead(string rawGuideKey)
    {
        string key = NormalizeGuideReadKey(rawGuideKey);
        if (string.IsNullOrEmpty(key))
            return;

        try
        {
            PlayerPrefs.SetInt(PrefKey_GuideReadPrefix + key, 1);
            PlayerPrefs.Save();
        }
        catch
        {
        }
    }

    public static bool HasAnyUnreadGuide()
    {
        try
        {
            if (!PlayerPrefs.HasKey(PrefKey_HasUnreadGuide))
                return true;

            return PlayerPrefs.GetInt(PrefKey_HasUnreadGuide, 1) != 0;
        }
        catch
        {
            return true;
        }
    }

    public static void SetHasAnyUnreadGuide(bool hasUnread)
    {
        try
        {
            PlayerPrefs.SetInt(PrefKey_HasUnreadGuide, hasUnread ? 1 : 0);
            PlayerPrefs.Save();
        }
        catch
        {
        }
    }

    private static string NormalizeSkillUnlockKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        string s = raw.Trim();

        if (string.Equals(s, "染色師", StringComparison.OrdinalIgnoreCase)) return SkillKey_DyeMaster;
        if (string.Equals(s, "RandomMan", StringComparison.OrdinalIgnoreCase)) return SkillKey_DyeMaster;

        if (string.Equals(s, "書家", StringComparison.OrdinalIgnoreCase)) return SkillKey_Calligrapher;
        if (string.Equals(s, "EnhanceHand", StringComparison.OrdinalIgnoreCase)) return SkillKey_Calligrapher;
        if (string.Equals(s, "RandomHonor", StringComparison.OrdinalIgnoreCase)) return SkillKey_Calligrapher;

        if (string.Equals(s, "資産家", StringComparison.OrdinalIgnoreCase)) return SkillKey_Capitalist;
        if (string.Equals(s, "Capitalist", StringComparison.OrdinalIgnoreCase)) return SkillKey_Capitalist;

        return s;
    }
    public static bool IsSkillRestrictionEnabled(string rawSkillKey)
    {
        string key = NormalizeSkillUnlockKey(rawSkillKey);

        if (key == SkillKey_Calligrapher)
            return PlayerPrefs.GetInt(PrefKey_SkillRestriction_Calligrapher, 1) != 0;

        if (key == SkillKey_Capitalist)
            return PlayerPrefs.GetInt(PrefKey_SkillRestriction_Capitalist, 1) != 0;

        return false;
    }

    public static void SetSkillRestrictionEnabled(string rawSkillKey, bool enabled)
    {
        string key = NormalizeSkillUnlockKey(rawSkillKey);

        if (key == SkillKey_Calligrapher)
        {
            PlayerPrefs.SetInt(PrefKey_SkillRestriction_Calligrapher, enabled ? 1 : 0);
            PlayerPrefs.Save();
            return;
        }

        if (key == SkillKey_Capitalist)
        {
            PlayerPrefs.SetInt(PrefKey_SkillRestriction_Capitalist, enabled ? 1 : 0);
            PlayerPrefs.Save();
            return;
        }
    }

    public static bool IsSkillUnlocked(string rawSkillKey)
    {
        string key = NormalizeSkillUnlockKey(rawSkillKey);

        if (key == SkillKey_DyeMaster)
            return true;

        if (key == SkillKey_Calligrapher)
            return PlayerPrefs.GetInt(PrefKey_SkillUnlocked_Calligrapher, 0) != 0;

        if (key == SkillKey_Capitalist)
            return PlayerPrefs.GetInt(PrefKey_SkillUnlocked_Capitalist, 0) != 0;

        return true;
    }

    public static bool IsSkillUsable(string rawSkillKey)
    {
        string key = NormalizeSkillUnlockKey(rawSkillKey);

        if (key == SkillKey_DyeMaster)
            return true;

        if (!IsSkillRestrictionEnabled(key))
            return true;

        return IsSkillUnlocked(key);
    }

    private static bool UnlockSkillInternal(string rawSkillKey)
    {
        string key = NormalizeSkillUnlockKey(rawSkillKey);
        if (string.IsNullOrEmpty(key)) return false;

        if (key == SkillKey_DyeMaster)
            return false;

        if (IsSkillUnlocked(key))
            return false;

        if (key == SkillKey_Calligrapher)
            PlayerPrefs.SetInt(PrefKey_SkillUnlocked_Calligrapher, 1);
        else if (key == SkillKey_Capitalist)
            PlayerPrefs.SetInt(PrefKey_SkillUnlocked_Capitalist, 1);
        else
            return false;

        EnqueuePendingSkillUnlockNotice(key);
        PlayerPrefs.Save();
        return true;
    }

    public static bool NotifyEnemyDefeatedForSkillUnlocks(string enemyBaseName)
    {
        if (string.IsNullOrWhiteSpace(enemyBaseName))
            return false;

        string n = enemyBaseName.Replace(" ", "").Replace("　", "").Trim();
        string lower = n.ToLowerInvariant();

        bool changed = false;

        if (n.Contains("フレイヤ") || lower.Contains("freyja"))
            changed |= UnlockSkillInternal(SkillKey_Calligrapher);

        if (n.Contains("ゼウス") || lower.Contains("zeus"))
            changed |= UnlockSkillInternal(SkillKey_Capitalist);

        return changed;
    }

    private static void EnqueuePendingSkillUnlockNotice(string rawSkillKey)
    {
        string key = NormalizeSkillUnlockKey(rawSkillKey);
        if (string.IsNullOrEmpty(key)) return;

        string csv = PlayerPrefs.GetString(PrefKey_PendingSkillUnlockQueue, "");
        var list = new List<string>();

        if (!string.IsNullOrEmpty(csv))
        {
            string[] parts = csv.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string p = NormalizeSkillUnlockKey(parts[i]);
                if (!string.IsNullOrEmpty(p) && !list.Contains(p))
                    list.Add(p);
            }
        }

        if (!list.Contains(key))
            list.Add(key);

        PlayerPrefs.SetString(PrefKey_PendingSkillUnlockQueue, string.Join(",", list));
    }

    public static bool TryConsumePendingSkillUnlockNotice(out string skillKey)
    {
        skillKey = "";

        string csv = PlayerPrefs.GetString(PrefKey_PendingSkillUnlockQueue, "");
        if (string.IsNullOrEmpty(csv))
            return false;

        string[] parts = csv.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts == null || parts.Length <= 0)
            return false;

        skillKey = NormalizeSkillUnlockKey(parts[0]);

        var remain = new List<string>();
        for (int i = 1; i < parts.Length; i++)
        {
            string p = NormalizeSkillUnlockKey(parts[i]);
            if (!string.IsNullOrEmpty(p))
                remain.Add(p);
        }

        PlayerPrefs.SetString(PrefKey_PendingSkillUnlockQueue, string.Join(",", remain));
        PlayerPrefs.Save();

        return !string.IsNullOrEmpty(skillKey);
    }

    // ==== ショップ（メニュー→ShopScene 用） ====
    public static List<int> CurrentShopOfferings = new List<int>(); // 各要素は 1..5

    // 価格（ShopManager から参照）
    public static int OmamoriPrice = 1000;

    // 新規プレイ開始時（メニューから）に呼ぶ想定
    public static void ResetShopForNewPlay()
    {
        CurrentShopOfferings.Clear();
    }

    public static void RollShopOfferingsIfNeeded()
    {
        if (CurrentShopOfferings.Count > 0) return;

        var pool = new List<int>{1,2,3,4,5};
        for (int i = 0; i < 3 && pool.Count > 0; i++)
        {
            int pick = UnityEngine.Random.Range(0, pool.Count);
            CurrentShopOfferings.Add(pool[pick]);
            pool.RemoveAt(pick);
        }
    }
    // 購入処理（成功なら true）
    public static bool BuyOmamori(int id)
    {
        if (Coins < OmamoriPrice) return false;
        Coins -= OmamoriPrice;
        OwnedOmamori.Add(id);
        return true;
    }

    // ===== オーラ（強化画面） =====
    // この集合に入っているタイルIDを使って和了したら +1000（GameManager側で判定）
    public static HashSet<int> AuraTiles = new HashSet<int>();

    // ===== ユーティリティ =====
    public static string TileName(int idx)
    {
        if (idx < 9)  return $"一二三四五六七八九"[idx] + "萬";
        if (idx < 18) return $"一二三四五六七八九"[idx-9] + "筒";
        if (idx < 27) return $"一二三四五六七八九"[idx-18] + "索";
        string[] honors = { "東","南","西","北","白","發","中" };
        return honors[idx-27];
    }
        // ===== デッキ（プレイヤー） =====
    // 0..33: 0-8=Man1..9, 9-17=Pin1..9, 18-26=Sou1..9, 27-33=東南西北白發中
    private static int[] _playerDeckCache;

    public static int[] GetDeckCountsCopy()
    {
        EnsureDeckInitialized();
        var copy = new int[34];
        System.Array.Copy(_playerDeckCache, copy, 34);
        return copy;
    }

    public static int TotalDeckCount()
    {
        EnsureDeckInitialized();
        int sum = 0;
        for (int i = 0; i < 34; i++) sum += Mathf.Max(0, _playerDeckCache[i]);
        return sum;
    }

    public static void AddToDeck(int tileIndex, int delta)
    {
        EnsureDeckInitialized();
        if (tileIndex < 0 || tileIndex >= 34) return;
        int v = Mathf.Max(0, _playerDeckCache[tileIndex] + delta);
        _playerDeckCache[tileIndex] = v;
        SaveDeck();
    }

    /// <summary>GameManagerの山構築用： idx→"Man1"/"Pin9"/"East" 等のIDへ</summary>
    public static string TileIdForIndex(int idx)
    {
        if (idx < 0 || idx >= 34) return null;
        if (idx < 9)   return "Man" + (idx + 1);          // 0..8 -> Man1..9
        if (idx < 18)  return "Pin" + (idx - 8);          // 9..17 -> Pin1..9
        if (idx < 27)  return "Sou" + (idx - 17);         // 18..26 -> Sou1..9
        string[] honors = { "East","South","West","North","White","Green","Red" };
        return honors[idx - 27];                           // 27..33
    }

    /// <summary>UI表示用の和名（既存の TileName は和名）も使えます。TileIdForIndex は英名ID。</summary>

    private static void EnsureDeckInitialized()
    {
        if (_playerDeckCache != null && _playerDeckCache.Length == 34) return;

        _playerDeckCache = new int[34];
        if (PlayerPrefs.GetInt("PD_Deck_Init", 0) == 1)
        {
            for (int i = 0; i < 34; i++)
                _playerDeckCache[i] = PlayerPrefs.GetInt("PD_Deck_" + i, 4);
        }
        else
        {
            for (int i = 0; i < 34; i++) _playerDeckCache[i] = 4; // 初期=4枚
            PlayerPrefs.SetInt("PD_Deck_Init", 1);
            SaveDeck();
        }
    }

// ...中略...

private static void SaveDeck()
{
    for (int i = 0; i < 34; i++) PlayerPrefs.SetInt("PD_Deck_" + i, _playerDeckCache[i]);
    PlayerPrefs.Save();
}

// === RUN終了時に“次の対局へは引き継がない”ためのリセット ===
/// <summary>デッキをデフォルト（各4枚）に戻す。次の対局の山構築は常にこの状態から。</summary>
public static void ResetDeckToDefault()
{
    EnsureDeckInitialized();
    for (int i = 0; i < 34; i++) _playerDeckCache[i] = 4;
    SaveDeck();
}

// } クラス終端

}
