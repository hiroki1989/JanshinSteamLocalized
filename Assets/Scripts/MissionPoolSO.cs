using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ミッション役プールの共有設定アセット。
/// Assets/Resources/MissionPool.asset として配置すれば、
/// どのシーンからでも同じ設定を参照できる。
///
/// ★作成方法: Project ウィンドウ右クリック → Create → Mahhan2/MasterData/MissionPoolSO
/// ★配置場所: Assets/Resources/MissionPoolSO.asset（自動ロードに必要）
/// </summary>
[CreateAssetMenu(menuName = "Mahhan2/MasterData/MissionPoolSO", fileName = "MissionPoolSO")]
public sealed class MissionPoolSO : ScriptableObject
{
    [Tooltip("ミッション候補となる役のリスト。難易度で報酬額が決まる。\n" +
             "Easy=500G / Normal=1000G / Hard=1500G / VeryHard=2000G")]
    public List<MissionSystem.MissionYakuEntry> pool = new List<MissionSystem.MissionYakuEntry>
    {
        // ===== 1翻（Easy = 500G）=====
        new MissionSystem.MissionYakuEntry { yakuKey = "MENZEN_TSUMO",    difficulty = MissionSystem.Difficulty.Easy },
        new MissionSystem.MissionYakuEntry { yakuKey = "TANYAO",          difficulty = MissionSystem.Difficulty.Easy },
        new MissionSystem.MissionYakuEntry { yakuKey = "PINFU",           difficulty = MissionSystem.Difficulty.Easy },
        new MissionSystem.MissionYakuEntry { yakuKey = "YAKUHAI",         difficulty = MissionSystem.Difficulty.Easy },
        new MissionSystem.MissionYakuEntry { yakuKey = "IIPEIKOU",        difficulty = MissionSystem.Difficulty.Easy },

        // ===== 2翻（Normal = 1000G）=====
        new MissionSystem.MissionYakuEntry { yakuKey = "CHIITOITSU",      difficulty = MissionSystem.Difficulty.Normal },
        new MissionSystem.MissionYakuEntry { yakuKey = "SANSHOKU_DOUJUN", difficulty = MissionSystem.Difficulty.Normal },
        new MissionSystem.MissionYakuEntry { yakuKey = "ITTSU",           difficulty = MissionSystem.Difficulty.Normal },
        new MissionSystem.MissionYakuEntry { yakuKey = "CHANTA",          difficulty = MissionSystem.Difficulty.Normal },
        new MissionSystem.MissionYakuEntry { yakuKey = "TOITOI",          difficulty = MissionSystem.Difficulty.Normal },
        new MissionSystem.MissionYakuEntry { yakuKey = "SANANKOU",        difficulty = MissionSystem.Difficulty.Normal },
        new MissionSystem.MissionYakuEntry { yakuKey = "SANSHOKU_DOUKOU", difficulty = MissionSystem.Difficulty.Normal },
        new MissionSystem.MissionYakuEntry { yakuKey = "SHOUSANGEN",      difficulty = MissionSystem.Difficulty.Normal },
        new MissionSystem.MissionYakuEntry { yakuKey = "HONROUTOU",       difficulty = MissionSystem.Difficulty.Normal },

        // ===== 3翻（Hard = 1500G）=====
        new MissionSystem.MissionYakuEntry { yakuKey = "HONITSU",         difficulty = MissionSystem.Difficulty.Hard },
        new MissionSystem.MissionYakuEntry { yakuKey = "JUNCHAN",         difficulty = MissionSystem.Difficulty.Hard },
        new MissionSystem.MissionYakuEntry { yakuKey = "RYANPEIKOU",      difficulty = MissionSystem.Difficulty.Hard },
        new MissionSystem.MissionYakuEntry { yakuKey = "SANKANTSU",       difficulty = MissionSystem.Difficulty.Hard },

        // ===== 6翻（Hard = 1500G）=====
        new MissionSystem.MissionYakuEntry { yakuKey = "CHINITSU",        difficulty = MissionSystem.Difficulty.Hard },

        // ===== 役満（VeryHard = 2000G）=====
        new MissionSystem.MissionYakuEntry { yakuKey = "KOKUSHI",         difficulty = MissionSystem.Difficulty.VeryHard },
        new MissionSystem.MissionYakuEntry { yakuKey = "SUUANKOU",        difficulty = MissionSystem.Difficulty.VeryHard },
        new MissionSystem.MissionYakuEntry { yakuKey = "DAISANGEN",       difficulty = MissionSystem.Difficulty.VeryHard },
        new MissionSystem.MissionYakuEntry { yakuKey = "SHOUSUUSHI",      difficulty = MissionSystem.Difficulty.VeryHard },
        new MissionSystem.MissionYakuEntry { yakuKey = "DAISUUSHI",       difficulty = MissionSystem.Difficulty.VeryHard },
        new MissionSystem.MissionYakuEntry { yakuKey = "TSUUIISOU",       difficulty = MissionSystem.Difficulty.VeryHard },
        new MissionSystem.MissionYakuEntry { yakuKey = "CHINROUTOU",      difficulty = MissionSystem.Difficulty.VeryHard },
        new MissionSystem.MissionYakuEntry { yakuKey = "RYUUIISOU",       difficulty = MissionSystem.Difficulty.VeryHard },
        new MissionSystem.MissionYakuEntry { yakuKey = "CHUUREN_POUTOU",  difficulty = MissionSystem.Difficulty.VeryHard },
        new MissionSystem.MissionYakuEntry { yakuKey = "SUUKANTSU",       difficulty = MissionSystem.Difficulty.VeryHard },
    };

    // ===== ランタイム自動ロード =====
    private static MissionPoolSO s_cached;

    /// <summary>
    /// Resources/MissionPoolSO からロードして返す。無ければ null。
    /// </summary>
    public static MissionPoolSO LoadShared()
    {
        if (s_cached != null) return s_cached;

        s_cached = Resources.Load<MissionPoolSO>("MissionPoolSO");
        return s_cached;
    }

    /// <summary>
    /// pool を返す（null安全）。
    /// </summary>
    public List<MissionSystem.MissionYakuEntry> GetPool()
    {
        return pool ?? new List<MissionSystem.MissionYakuEntry>();
    }
}
