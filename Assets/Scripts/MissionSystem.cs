using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ミッションシステム（静的クラス）。
/// 各敵ごとにランダムで役を1つ選び、その役を和了するとGold報酬を得る。
/// 報酬は各敵ごとに1回限り。
///
/// ★役リスト＆難易度は外部（GameManager Inspector）から渡す。
/// </summary>
public static class MissionSystem
{
    // ===== 難易度 → 報酬Gold対応 =====
    public enum Difficulty
    {
        Easy    = 0,   // 500 Gold
        Normal  = 1,   // 1000 Gold
        Hard    = 2,   // 1500 Gold
        VeryHard = 3,  // 2000 Gold
    }

    public static int GetGoldForDifficulty(Difficulty d)
    {
        switch (d)
        {
            case Difficulty.Easy:     return 500;
            case Difficulty.Normal:   return 1000;
            case Difficulty.Hard:     return 1500;
            case Difficulty.VeryHard: return 2000;
            default:                  return 500;
        }
    }

    // ===== ミッション候補エントリ（Inspector から渡される） =====
    [Serializable]
    public class MissionYakuEntry
    {
        [Tooltip("YakuEvaluator のキー（例: PINFU, TANYAO, KOKUSHI）")]
        public string yakuKey;

        [Tooltip("表示名（日本語）。空欄なら LocalizationManager から自動取得。")]
        public string displayNameOverride;

        [Tooltip("この役のミッション難易度（報酬額を決定）")]
        public Difficulty difficulty = Difficulty.Normal;

        [Tooltip("true にするとミッション候補から除外")]
        public bool excluded = false;
    }

    // ===== 現在のミッション状態 =====
    private static int    s_currentPoolIndex    = -1;  // 渡された pool 内のインデックス (-1 = 未設定)
    private static int    s_currentEnemyKey     = -1;  // このミッションの対象敵 excelKey
    private static bool   s_completed           = false;

    // キャッシュ（AssignForEnemy で確定した内容）
    private static string s_cachedYakuKey       = "";
    private static string s_cachedDisplayName   = "";
    private static int    s_cachedGold          = 0;

    // PlayerPrefs キー
    private const string PrefKey_MissionPoolIdx    = "Mission_PoolIndex";
    private const string PrefKey_MissionEnemyKey   = "Mission_CurrentEnemyKey";
    private const string PrefKey_MissionCompleted  = "Mission_CurrentCompleted";
    private const string PrefKey_MissionYakuKey    = "Mission_YakuKey";
    private const string PrefKey_MissionDispName   = "Mission_DispName";
    private const string PrefKey_MissionGold       = "Mission_Gold";

    private static string PrefKey_Claimed(int excelKey) => $"Mission_Claimed_{excelKey}";

    // ===== 公開プロパティ =====
    public static bool HasActiveMission => !string.IsNullOrEmpty(s_cachedYakuKey);
    public static string CurrentYakuKey => s_cachedYakuKey;
    public static string CurrentDisplayName => s_cachedDisplayName;
    public static int CurrentGold => s_cachedGold;
    public static bool IsCompleted => s_completed;
    public static int CurrentEnemyKey => s_currentEnemyKey;

    // ===== 初期化・ロード =====
    public static void ResetForNewRun()
    {
        s_currentPoolIndex = -1;
        s_currentEnemyKey = -1;
        s_completed = false;
        s_cachedYakuKey = "";
        s_cachedDisplayName = "";
        s_cachedGold = 0;
        PlayerPrefs.DeleteKey(PrefKey_MissionPoolIdx);
        PlayerPrefs.DeleteKey(PrefKey_MissionEnemyKey);
        PlayerPrefs.DeleteKey(PrefKey_MissionCompleted);
        PlayerPrefs.DeleteKey(PrefKey_MissionYakuKey);
        PlayerPrefs.DeleteKey(PrefKey_MissionDispName);
        PlayerPrefs.DeleteKey(PrefKey_MissionGold);
        PlayerPrefs.Save();
    }

    public static void Load()
    {
        s_currentPoolIndex = PlayerPrefs.GetInt(PrefKey_MissionPoolIdx, -1);
        s_currentEnemyKey = PlayerPrefs.GetInt(PrefKey_MissionEnemyKey, -1);
        s_completed = PlayerPrefs.GetInt(PrefKey_MissionCompleted, 0) != 0;
        s_cachedYakuKey = PlayerPrefs.GetString(PrefKey_MissionYakuKey, "");
        s_cachedDisplayName = PlayerPrefs.GetString(PrefKey_MissionDispName, "");
        s_cachedGold = PlayerPrefs.GetInt(PrefKey_MissionGold, 0);
    }

    private static void Save()
    {
        PlayerPrefs.SetInt(PrefKey_MissionPoolIdx, s_currentPoolIndex);
        PlayerPrefs.SetInt(PrefKey_MissionEnemyKey, s_currentEnemyKey);
        PlayerPrefs.SetInt(PrefKey_MissionCompleted, s_completed ? 1 : 0);
        PlayerPrefs.SetString(PrefKey_MissionYakuKey, s_cachedYakuKey ?? "");
        PlayerPrefs.SetString(PrefKey_MissionDispName, s_cachedDisplayName ?? "");
        PlayerPrefs.SetInt(PrefKey_MissionGold, s_cachedGold);
        PlayerPrefs.Save();
    }

    // ===== ミッション割り当て =====
    /// <summary>
    /// 次の敵との戦闘前に呼ぶ。pool からランダムでミッションを1つ割り当てる。
    /// pool は GameManager Inspector の missionYakuPool を渡す。
    /// </summary>
    public static void AssignForEnemy(int excelKey, List<MissionYakuEntry> pool)
    {
        s_currentEnemyKey = excelKey;
        s_completed = IsAlreadyClaimed(excelKey);

        // 有効な候補をフィルタ
        var candidates = new List<int>();
        if (pool != null)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                var e = pool[i];
                if (e == null) continue;
                if (e.excluded) continue;
                if (string.IsNullOrEmpty(e.yakuKey)) continue;
                candidates.Add(i);
            }
        }

        if (candidates.Count == 0)
        {
            s_cachedYakuKey = "";
            s_cachedDisplayName = "";
            s_cachedGold = 0;
            s_currentPoolIndex = -1;
            Save();
            return;
        }

        // ランダム選択（同じ敵には同じミッション：Runシード+敵キーでハッシュ）
        int runSeed = PlayerPrefs.GetInt("Run_MissionSeed", 0);
        if (runSeed == 0)
        {
            runSeed = UnityEngine.Random.Range(1, int.MaxValue);
            PlayerPrefs.SetInt("Run_MissionSeed", runSeed);
            PlayerPrefs.Save();
        }

        var rng = new System.Random(runSeed ^ (excelKey * 7919));
        int pick = candidates[rng.Next(0, candidates.Count)];

        s_currentPoolIndex = pick;
        var entry = pool[pick];

        s_cachedYakuKey = entry.yakuKey.Trim();
        s_cachedGold = GetGoldForDifficulty(entry.difficulty);

        // 表示名の解決
        s_cachedDisplayName = ResolveDisplayName(entry);

        Save();
    }

    // ===== 報酬取得済みチェック =====
    public static bool IsAlreadyClaimed(int excelKey)
    {
        return PlayerPrefs.GetInt(PrefKey_Claimed(excelKey), 0) != 0;
    }

    // ===== 達成判定 =====
    public static bool CheckCompletion(List<string> playerYakuList)
    {
        if (!HasActiveMission) return false;
        if (s_completed) return false;

        if (playerYakuList == null || playerYakuList.Count == 0) return false;

        string targetNorm = NormalizeForMatch(s_cachedYakuKey);
        if (string.IsNullOrEmpty(targetNorm)) return false;

        for (int i = 0; i < playerYakuList.Count; i++)
        {
            string raw = playerYakuList[i];
            if (string.IsNullOrEmpty(raw)) continue;

            string norm = NormalizeForMatch(raw);
            if (string.IsNullOrEmpty(norm)) continue;

            if (norm.Contains(targetNorm) || targetNorm.Contains(norm))
            {
                s_completed = true;
                Save();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// ミッション報酬を受け取り、所持Goldに反映する。各敵につき1回だけ。
    /// </summary>
    public static int ClaimReward()
    {
        if (!HasActiveMission) return 0;
        if (!s_completed) return 0;
        if (IsAlreadyClaimed(s_currentEnemyKey)) return 0;

        int reward = s_cachedGold;

        PlayerPrefs.SetInt(PrefKey_Claimed(s_currentEnemyKey), 1);
        PlayerPrefs.Save();

        GameManager.RunCurrency.Add(reward);
        return reward;
    }

    // ===== 表示テキスト生成 =====
    public static string GetMissionDisplayText()
    {
        if (!HasActiveMission) return "";
        string label = GetLocalizedLabel();
        string verb = GetLocalizedVerb();
        return $"{label}　{s_cachedDisplayName}{verb}　{s_cachedGold}";
    }

    public static string GetMissionCompleteText()
    {
        if (!HasActiveMission) return "";
        string completeLabel = GetLocalizedCompleteLabel();
        string earnedLabel = GetLocalizedEarnedLabel();
        return $"{completeLabel}　{s_cachedGold}{earnedLabel}";
    }

    // ===== Run リセット時のシード破棄 =====
    public static void ClearRunSeed()
    {
        PlayerPrefs.DeleteKey("Run_MissionSeed");
        PlayerPrefs.Save();
    }

    // ===== 全報酬クリア（デバッグ/リセット用） =====
    public static void ClearAllClaimed()
    {
        for (int i = 0; i < 100; i++)
            PlayerPrefs.DeleteKey(PrefKey_Claimed(i));
        PlayerPrefs.Save();
    }

    // ===== 内部ヘルパー =====

    /// <summary>
    /// 表示名を解決する。Override が設定されていればそれを使い、
    /// 空欄なら LocalizationManager から自動取得する。
    /// yakuKey が "yakuman." に該当するか判定して適切なメソッドを使う。
    /// </summary>
    private static string ResolveDisplayName(MissionYakuEntry entry)
    {
        if (!string.IsNullOrEmpty(entry.displayNameOverride))
            return entry.displayNameOverride;

        string key = entry.yakuKey.Trim().ToUpperInvariant();

        // 役満キーかどうか判定
        bool isYakuman = IsYakumanKey(key);

        try
        {
            if (isYakuman)
            {
                string name = LocalizationManager.Yakuman(key);
                if (!string.IsNullOrEmpty(name) && name != ("yakuman." + key))
                    return name;
            }

            string yakuName = LocalizationManager.Yaku(key);
            if (!string.IsNullOrEmpty(yakuName) && yakuName != ("yaku." + key))
                return yakuName;
        }
        catch { }

        // フォールバック：キーをそのまま返す
        return entry.yakuKey;
    }

    private static bool IsYakumanKey(string upperKey)
    {
        switch (upperKey)
        {
            case "KOKUSHI":
            case "CHUUREN_POUTOU":
            case "DAISANGEN":
            case "DAISUUSHI":
            case "SHOUSUUSHI":
            case "TSUUIISOU":
            case "CHINROUTOU":
            case "RYUUIISOU":
            case "SUUANKOU":
            case "SUUKANTSU":
            case "TENHOU":
            case "CHIHOU":
            case "RENHOU":
                return true;
            default:
                return false;
        }
    }

    private static string NormalizeForMatch(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        string s = raw.Trim();

        // (+N), (×N), ×N などを除去
        int paren = s.IndexOf('(');
        if (paren >= 0) s = s.Substring(0, paren);
        paren = s.IndexOf('（');
        if (paren >= 0) s = s.Substring(0, paren);
        int times = s.IndexOf('×');
        if (times >= 0) s = s.Substring(0, times);

        s = s.Replace(" ", "").Replace("　", "").Replace("-", "").Replace("_", "");
        return s.Trim().ToLowerInvariant();
    }

    private static string GetLocalizedLabel()
    {
        try
        {
            var lm = LocalizationManager.Instance;
            if (lm != null)
            {
                switch (lm.CurrentLanguage)
                {
                    case LocalizationManager.Language.English:           return "Mission";
                    case LocalizationManager.Language.ChineseSimplified: return "任务";
                }
            }
        }
        catch { }
        return "ミッション";
    }

    private static string GetLocalizedVerb()
    {
        try
        {
            var lm = LocalizationManager.Instance;
            if (lm != null)
            {
                switch (lm.CurrentLanguage)
                {
                    case LocalizationManager.Language.English:           return " - Win with";
                    case LocalizationManager.Language.ChineseSimplified: return "和牌";
                }
            }
        }
        catch { }
        return "を和了しろ";
    }

    private static string GetLocalizedCompleteLabel()
    {
        try
        {
            var lm = LocalizationManager.Instance;
            if (lm != null)
            {
                switch (lm.CurrentLanguage)
                {
                    case LocalizationManager.Language.English:           return "Mission Complete!";
                    case LocalizationManager.Language.ChineseSimplified: return "任务完成！";
                }
            }
        }
        catch { }
        return "ミッション達成！";
    }

    private static string GetLocalizedEarnedLabel()
    {
        try
        {
            var lm = LocalizationManager.Instance;
            if (lm != null)
            {
                switch (lm.CurrentLanguage)
                {
                    case LocalizationManager.Language.English:           return " Gold earned";
                    case LocalizationManager.Language.ChineseSimplified: return " 金币获得";
                }
            }
        }
        catch { }
        return "獲得";
    }
}
