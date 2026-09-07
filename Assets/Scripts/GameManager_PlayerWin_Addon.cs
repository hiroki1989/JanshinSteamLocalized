using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public partial class GameManager : MonoBehaviour
{
private IEnumerator __PlayerWin_ShowCutinAndScoring_Flow_Co(
        string reason,         // 「ツモ」「ロン」
        string winningTile,    // 和了牌ID（例: "m5" 等）
        int fu,
        int han,
        List<string> yaku,
        int totalPoints)
    {
        // ★実績：プレイヤー和了（役満/スコア達成）をここで拾う
        try { AchievementSystem.NotifyPlayerWin(yaku, totalPoints); } catch { }

        // ★追加：直前局の勝者フラグを「敵ではない」にしておく（次局開始時の手牌リセット方式に使用）
        _addonLastHandWinnerWasEnemy = false;

        // 自動進行を止めて「点数計算フェーズ」に固定
        _autoSkipPending = false;
        phase = Phase.Scoring;
        // yaku / used を WinCutin コルーチン用に整形
        var yakuLines = (yaku != null) ? new List<string>(yaku) : new List<string>();
        var usedList  = new List<string>();
        if (!string.IsNullOrEmpty(winningTile))
            usedList.Add(winningTile);

        // ★プレイヤー側として汎用カットイン＋スコア表示コルーチンを呼ぶ
        //   finalDamage / finalBasePoints はとりあえず totalPoints を流用
        yield return StartCoroutine(
            __WinCutInThenShowScoring(
                label:          reason,      // 「ツモ」「ロン」
                isPlayer:       true,        // ★プレイヤー側
                finalDamage:    totalPoints,
                finalBasePoints:totalPoints,
                geKi:           0f,
                shun:           0f,
                iyu:            0f,
                finalMpHeal:    0,
                finalHpHeal:    0,
                yakuLines:      yakuLines,
                used:           usedList,
                fu:             fu,
                han:            han,
                baseWinKind:    reason,
                usedTileLabel:  winningTile  // スコア描画用に保持
            )
        );

        // 以降の「OKボタンで次局へ」「敵HP減少」などは、
        // 既存の ShowScoring → WireScoringOK → 既存処理に任せます。
    }
    // ===============================
    //  プレイヤーのカットイン画像：スキルごとに差し替え
    // ===============================

    [System.Serializable]
    private class PlayerCutinEntry
    {
        public string skillId;  // 例: "RandomMan"
        public Sprite sprite;   // そのスキル用のカットイン画像
    }

    [Header("Player cutin (skill-based)")]
    [SerializeField] private Sprite defaultPlayerCutinSprite;
    [SerializeField] private List<PlayerCutinEntry> playerCutinSprites = new List<PlayerCutinEntry>();

    /// <summary>
    /// 現在装備しているスキルに応じてカットイン用スプライトを返す
    /// </summary>
    private Sprite GetPlayerCutinSpriteForCurrentSkill()
    {
        // ★既存のメソッド（添付コード内にある）をそのまま使う想定です
        var skill = GetEquippedSkill();   // ActiveSkill 等の enum

        // 1) Inspector で設定されたテーブルを優先
        if (playerCutinSprites != null)
        {
            string key = skill.ToString();    // 例: "RandomMan"
            foreach (var e in playerCutinSprites)
            {
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.skillId)) continue;
                if (e.skillId == key && e.sprite != null)
                    return e.sprite;
            }
        }

        // 2) Inspector 未設定なら Resources から探す
        //    優先順位:
        //    ① PlayerCutins/<SkillName>_cutin
        //    ② PlayerCutins/<SkillName>_Cutin  （大文字C対応）
        //    ③ PlayerCutins/<SkillName>
        try
        {
            string baseName = skill.ToString();

            // ① xxx_cutin
            string resPath1 = "PlayerCutins/" + baseName + "_cutin";
            var res1 = Resources.Load<Sprite>(resPath1);
            if (res1 != null)
            {
                return res1;
            }

            // ② xxx_Cutin（ファイル名が RandomMan_Cutin などの場合）
            string resPath2 = "PlayerCutins/" + baseName + "_Cutin";
            var res2 = Resources.Load<Sprite>(resPath2);
            if (res2 != null)
            {
                return res2;
            }

            // ③ 従来どおり素の名前
            string resPath3 = "PlayerCutins/" + baseName;
            var res3 = Resources.Load<Sprite>(resPath3);
            if (res3 != null)
            {
                return res3;
            }
        }
        catch { }

        // 3) それでもダメならデフォルト → 旧フィールドにもフォールバック
        if (defaultPlayerCutinSprite != null)
            return defaultPlayerCutinSprite;

        // ★本体 GameManager.cs 側の playerCutinSprite もフォールバックとして参照
        if (playerCutinSprite != null)
            return playerCutinSprite;

        return null;
    }
}
