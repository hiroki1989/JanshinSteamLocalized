// 2025/11/12 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using System.Collections;
using UnityEngine;
using UnityEngine.UI; // 必要な名前空間をインポート
using System.Collections.Generic;
using TMPro;
public partial class GameManager
{
    // 直近の追記を一度だけ行うためのフラグ
    private bool _enemyAddon_YakuFuAppendPending = false;

    private static string EnemyAddonYakuFu_FixedText_Local(string key)
    {
        var lm = LocalizationManager.Instance;
        if (lm == null) return key;
        return lm.GetFixedText(key);
    }

    /// <summary>
private void EnemyAddon_MarkAppendYakuFu()
{
    _enemyAddon_YakuFuAppendPending = true;
    StartCoroutine(__EnemyAddon_WaitPanelAndAppend());
}
// 2025/11/15 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

private IEnumerator __EnemyAddon_WaitPanelAndAppend()
{
    // 最大2秒ほど待機（フレーム遅延でscoringPanelが開いた後に追記する）
    float t = 0f;
    while (t < 2f)
    {
        if (!_enemyAddon_YakuFuAppendPending) yield break;
        if (scoringPanel && scoringPanel.activeInHierarchy)
        {
            EnemyAddon_AppendYakuFuToScoringUI();

            // ★修正: 点数計算パネルを閉じたタイミングでプレイヤーのHPにダメージを与える
            ApplyDamageToPlayer();

            // ★修正: 敵の点数計算パネルのUIを更新
            UpdateScoringPanelUI();

            yield break;
        }
        t += Time.unscaledDeltaTime;
        yield return null;
    }
    // 念のためタイムアウト後も一回試す
    EnemyAddon_AppendYakuFuToScoringUI();

    // ★修正: 点数計算パネルを閉じたタイミングでプレイヤーのHPにダメージを与える
    ApplyDamageToPlayer();

    // ★修正: 敵の点数計算パネルのUIを更新
    UpdateScoringPanelUI();
}
    private void ApplyDamageToPlayer()
    {
        // ダメージ適用処理は FinalizeEnemyWin_ShowScoringAndCleanup() 内で
        // ApplyDamageToPlayer_WithOmamori(hpDmg, "enemy_win") が呼ばれている。
        // ここで再度呼ぶと二重ダメージになるため、現在は何もしない。
    }
private int CalculateEnemyDamage()
{
    // 敵のダメージを計算するロジックを記述
    // 例: 敵の攻撃力に基づいてダメージを計算
    return Mathf.Clamp(enemyAttackPower, 1, playerHP); // 最低1ダメージ、最大プレイヤーの現在HP
}

private void EnemyAddon_AppendYakuFuToScoringUI()
{
    if (!_enemyAddon_YakuFuAppendPending) return;
    _enemyAddon_YakuFuAppendPending = false;

    try
    {
        if (scoringTMP == null) return;
        if (EnemyAddon_LastFu <= 0 && string.IsNullOrEmpty(EnemyAddon_LastYakuText) && string.IsNullOrEmpty(EnemyAddon_LastDoraText)) return;

        string yakuLine = (EnemyAddon_LastYakuText ?? "");
        string fuPrefix = EnemyAddonYakuFu_FixedText_Local("fu_prefix");
        string yakuNone = EnemyAddonYakuFu_FixedText_Local("yaku_none");
        string yakuLabelPrefix = EnemyAddonYakuFu_FixedText_Local("yaku_label_prefix");
        string hanFuLabel = EnemyAddonYakuFu_FixedText_Local("han_fu_label");

        string fuLine = (EnemyAddon_LastPoints < 12000 && EnemyAddon_LastFu > 0)
            ? (fuPrefix + EnemyAddon_LastFu.ToString())
            : "";

        var baseText = scoringTMP.text ?? string.Empty;

        {
            baseText = baseText
                .Replace(yakuNone, "")
                .Replace(yakuLabelPrefix + yakuNone, "")
                .Replace("役なし", "")
                .Replace("役:なし", "")
                .Replace("役：なし", "")
                .Replace("No Yaku", "")
                .Replace("Yaku: No Yaku", "")
                .Replace("无役", "")
                .Replace("役: 无役", "");

            var rxJaYaku = new System.Text.RegularExpressions.Regex(
                @"^.*役(?:[：:]\s*|[\s　]+)[^\r\n]*\r?\n?",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            var rxJaYakuAny = new System.Text.RegularExpressions.Regex(
                @"^役.*\r?\n?",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            var rxEnYaku = new System.Text.RegularExpressions.Regex(
                @"^Yaku:\s*[^\r\n]*\r?\n?",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            baseText = rxJaYaku.Replace(baseText, "");
            baseText = rxJaYakuAny.Replace(baseText, "");
            baseText = rxEnYaku.Replace(baseText, "");

            if (!string.IsNullOrEmpty(yakuLine))
            {
                baseText = baseText.TrimEnd();
                baseText = baseText + "\n" + yakuLine;
            }
        }

        if (!string.IsNullOrEmpty(fuLine))
        {
            if (!(baseText.Contains(fuPrefix)
                || baseText.Contains(hanFuLabel)
                || baseText.Contains("符:")
                || baseText.Contains("符：")
                || baseText.Contains("Fu: ")))
            {
                baseText = baseText.TrimEnd();
                baseText = baseText + "\n" + fuLine;
            }
        }

        scoringTMP.text = baseText;
    }
    catch
    {
    }
}
private void BindOkButtonEvent()
{
    GameObject okButton = GameObject.Find("ScoringPanelOkButton"); // 点数計算パネルのOKボタンの名前を指定
    if (okButton != null)
    {
        var buttonComponent = okButton.GetComponent<Button>();
        if (buttonComponent != null)
        {
            buttonComponent.onClick.AddListener(() =>
            {
                HideEnemyCutin(); // OKボタン押下時にカットインを非表示
            });
        }
        else
        {
            Debug.LogWarning("ScoringPanelOkButtonにButtonコンポーネントがありません。");
        }
    }
    else
    {
        Debug.LogWarning("ScoringPanelOkButtonオブジェクトが見つかりません。");
    }
}
}