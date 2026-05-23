
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// NOTE: Non-invasive add-on: only adds a background watcher for player HP and a small
// Game Over overlay + transition to rewardSceneName. It doesn't touch existing logic.
//
// Drop this alongside your existing GameManager partial files.
//
// ★変更点: __OnClick_GameOverOk() にインタースティシャル広告を挿入。
//   広告表示後（またはスキップ後）に既存の遷移処理を実行する。

public partial class GameManager : MonoBehaviour
{
    // ===== Game Over overlay (generated in code) =====
    private RectTransform _goOverlay;   // full-screen dim
    private RectTransform _goPanel;     // center panel
    private TextMeshProUGUI _goTitle;
    private TextMeshProUGUI _goBody;
    private Button _goOk;
    private bool _goShown = false;
    private int _goPrevHP = 0;

    // Install a lightweight watcher after every scene load (avoids duplicate Awake/Start definitions).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void __InstallGameOverWatcher()
    {
        var gm = Object.FindObjectOfType<GameManager>();
        if (gm != null)
        {
            gm.StartCoroutine(gm.__GameOverWatchLoop());
        }
    }

    private IEnumerator __GameOverWatchLoop()
    {
        // tiny delay to ensure existing UI is created
        yield return null;
        yield return null;

        // Run forever; this is cheap and only reacts on HP <= 0 once.
        while (this && !_goShown)
        {
            // Defensive try/catch so we never break gameplay even if fields change elsewhere.
            int hpNow = 1;
            try { hpNow = Mathf.Max(0, playerHP); } catch { hpNow = 1; }
            if (hpNow <= 0 && !_goShown)
            {
                try { _goPrevHP = playerHP; } catch { _goPrevHP = 0; }
                // Defer auto GameOver if scoring result is being shown; wait for OK on result
bool scoringActive = false;
try { scoringActive = (phase.ToString() == "Scoring") || (scoringPanel && scoringPanel.activeInHierarchy); } catch {}
if (!scoringActive)
{
    __ShowGameOverOverlay();
    yield break;
}
// If scoring is active, do nothing here; OnClickScoreOK will handle transition.
                yield break;
            }
            yield return null;
        }
    }

    // ========== UI creation ==========
    private void __EnsureGameOverUI()
    {
        if (_goOverlay != null) return;

        Canvas canvas = null;
        // Try to reuse the same Canvas used by other runtime UI.
        var allCanvas = Object.FindObjectsOfType<Canvas>();
        if (allCanvas != null && allCanvas.Length > 0) canvas = allCanvas[0];
        if (canvas == null)
        {
            var go = new GameObject("UICanvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
        }

        // Overlay
        var overlayGO = new GameObject("GameOverOverlay", typeof(RectTransform), typeof(Image));
        overlayGO.transform.SetParent(canvas.transform, false);
        _goOverlay = overlayGO.GetComponent<RectTransform>();
        _goOverlay.anchorMin = Vector2.zero;
        _goOverlay.anchorMax = Vector2.one;
        _goOverlay.offsetMin = Vector2.zero;
        _goOverlay.offsetMax = Vector2.zero;
        var dim = overlayGO.GetComponent<Image>();
        dim.color = new Color(0, 0, 0, 0.6f);
        overlayGO.SetActive(false);

        // Panel
        var panelGO = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(_goOverlay, false);
        _goPanel = panelGO.GetComponent<RectTransform>();
        _goPanel.sizeDelta = new Vector2(720, 320);
        _goPanel.anchorMin = new Vector2(0.5f, 0.5f);
        _goPanel.anchorMax = new Vector2(0.5f, 0.5f);
        _goPanel.anchoredPosition = Vector2.zero;
        panelGO.GetComponent<Image>().color = new Color(0.09f, 0.21f, 0.28f, 0.95f); // same tone as existing

        // Title
        _goTitle = CreateTmp(panelGO.transform, "タイトル", 28, TextAlignmentOptions.Center);
        var tRect = _goTitle.rectTransform;
        tRect.anchorMin = new Vector2(0.5f, 1f);
        tRect.anchorMax = new Vector2(0.5f, 1f);
        tRect.anchoredPosition = new Vector2(0, -28);
        _goTitle.text = "ゲームオーバー";

        // Body
        _goBody = CreateTmp(panelGO.transform, "本文", 22, TextAlignmentOptions.Left);
        var bRect = _goBody.rectTransform;
        bRect.anchorMin = new Vector2(0f, 1f);
        bRect.anchorMax = new Vector2(1f, 1f);
        bRect.pivot = new Vector2(0.5f, 1f);
        bRect.sizeDelta = new Vector2(-48, 160);
        bRect.anchoredPosition = new Vector2(0, -74);
        _goBody.enableWordWrapping = false;
        _goBody.text = "";

        // OK button
        var btnGO = new GameObject("OK", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(panelGO.transform, false);
        var br = btnGO.GetComponent<RectTransform>();
        br.sizeDelta = new Vector2(180, 44);
        br.anchorMin = new Vector2(0.5f, 0f);
        br.anchorMax = new Vector2(0.5f, 0f);
        br.anchoredPosition = new Vector2(0, 28);
        var btnImg = btnGO.GetComponent<Image>();
        btnImg.color = new Color(0.8f, 0.85f, 0.9f, 1f);
        _goOk = btnGO.GetComponent<Button>();

        var okLabel = CreateTmp(btnGO.transform, "OKLabel", 20, TextAlignmentOptions.Center);
        okLabel.text = "OK";
        okLabel.rectTransform.anchorMin = Vector2.zero;
        okLabel.rectTransform.anchorMax = Vector2.one;
        okLabel.rectTransform.offsetMin = Vector2.zero;
        okLabel.rectTransform.offsetMax = Vector2.zero;

        _goOk.onClick.RemoveAllListeners();
        _goOk.onClick.AddListener(__OnClick_GameOverOk);
    }

    private TextMeshProUGUI CreateTmp(Transform parent, string name, int size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.outlineWidth = 0.15f;
        return tmp;
    }

    private void __ShowGameOverOverlay()
    {
        _goShown = true;
        try { if (scoringPanel && scoringPanel.activeInHierarchy) { StartCoroutine(__Go_WaitScoringAndTransition()); return; } } catch {}
        __EnsureGameOverUI();

        // Compose body text
        int pHP = 0, eHP = 0;
        try { pHP = playerHP; } catch {}
        try { eHP = enemyHP; } catch {}
        _goBody.text = $"プレイヤーHP: {_goPrevHP} → {Mathf.Max(0, pHP)}\n敵HP: {eHP}\n戦闘は終了しました。報酬画面に遷移します。";

        _goOverlay.gameObject.SetActive(true);
        _goOverlay.SetAsLastSibling();
    }

    // ★★★ 変更: 広告を挟んでから遷移処理へ ★★★
    private void __OnClick_GameOverOk()
    {
        // OKボタン連打防止
        if (_goOk != null) _goOk.interactable = false;

        // インタースティシャル広告を試みる（頻度制御は InterstitialAdManager 側で行う）
        var adMgr = InterstitialAdManager.Instance;
        if (adMgr != null)
        {
            adMgr.ShowAdIfReady(() => __OnClick_GameOverOk_Body());
        }
        else
        {
            __OnClick_GameOverOk_Body();
        }
    }

    /// <summary>
    /// 広告表示後（またはスキップ後）に実行される、既存の敗北遷移処理。
    /// </summary>
    private void __OnClick_GameOverOk_Body()
    {
// 次回開始時に全回復させる
try { PlayerPrefs.SetInt("PF_PendingFullHeal", 1); PlayerPrefs.Save(); } catch {}
// 古い持ち越しHPは無効化
try { PlayerPrefs.DeleteKey("Run_PlayerHP"); PlayerPrefs.Save(); } catch {}

        try
        {
            // Clear carried HP when run ends (game over)
            try { PlayerPrefs.DeleteKey("Run_PlayerHP"); PlayerPrefs.Save(); } catch {}
try { PlayerPrefs.DeleteKey("RunOfuda"); PlayerPrefs.DeleteKey("RunOfuda_LastJSON"); PlayerPrefs.Save(); } catch {}
     // Save run result as "途中敗退" 
            PlayerPrefs.SetInt("RunCleared", 0);
            int defeated = 0;
            try { defeated = Mathf.Max(0, PlayerData.CurrentEnemy); } catch { defeated = 0; }
            PlayerPrefs.SetInt("EnemiesDefeated", defeated);
            PlayerPrefs.Save();
        } catch { /* ignore */ }

 try { ClearRunEphemeral(); } catch { /* ignore */ }

        // Fallback scene name if not assigned
// Fallback scene name if not assigned
string scene = null;
try { scene = string.IsNullOrEmpty(rewardSceneName) ? null : rewardSceneName; } catch { scene = null; }
if (string.IsNullOrEmpty(scene)) scene = "RewardScene";

// ★追加：敗北時の報酬をここで"付与"してから遷移する
try
{
    int defeated = 0;
    try { defeated = Mathf.Max(0, PlayerPrefs.GetInt("EnemiesDefeated", 0)); } catch { defeated = 0; }
    PlayerData.GrantRandomOmamori(defeated);
}
catch { /* ignore */ }

// NEW: 最終スコアの保存（GameManager の非publicメソッドを反射で呼ぶ）
try
{
    var gm = GameObject.FindObjectOfType<GameManager>();
    if (gm != null)
    {
        var mi = typeof(GameManager).GetMethod("SaveLastRunScore",
            System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        if (mi != null) mi.Invoke(gm, null);
    }
}
catch {}
// NEW: 最終スコアの保存 ... 略 ...
try { StageClearManager.ResetEnemyProgressionNow(); } catch {}

// デッキ構築はローグライト：次の対局へは引き継がない
try { PlayerData.ResetDeckToDefault(); } catch {}

/* 既存 */ 
SceneManager.LoadScene(scene);


    }

    private System.Collections.IEnumerator __Go_WaitScoringAndTransition()
    {
        float t = 0f;
        while (t < 6f)
        {
            bool active = false;
            try { active = (scoringPanel && scoringPanel.activeInHierarchy); } catch { active = false; }
            if (!active) break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return null;
        __OnClick_GameOverOk();
    }
}
