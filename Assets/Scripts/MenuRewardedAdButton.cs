using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// メニューシーンに「広告を見て宝石を獲得」ボタンを追加する。
/// 
/// ★ 使い方（2つの方法）:
/// 
/// 【方法A: Inspector で手動配置】
///   1. Menu シーンの Canvas 内に Button (TextMeshPro) を追加
///   2. このスクリプトをアタッチ
///   3. Inspector で rewardButton / buttonLabel / gemCountTMP を割り当て
///
/// 【方法B: 自動生成（何もアサインしなくてOK）】
///   1. Menu シーンの任意の GameObject にこのスクリプトをアタッチ
///   2. rewardButton が未割当なら、Start() で自動的にボタンを生成する
///   3. 自動生成されたボタンは画面右下に配置される
///
/// 広告カット課金済みでもリワード広告は表示可能にしてあります。
/// （リワードは自発的なので、カットしたいなら hideWhenAdFree を true に）
/// </summary>
public class MenuRewardedAdButton : MonoBehaviour
{
    [Header("UI参照 (未割当なら自動生成)")]
    [SerializeField] private Button rewardButton;
    [SerializeField] private TextMeshProUGUI buttonLabel;

    [Header("宝石数テキスト (更新用。MenuController の gemCountTMP と同じものを割当)")]
    [SerializeField] private TextMeshProUGUI gemCountTMP;

    [Header("設定")]
    [Tooltip("広告カット課金済みのユーザーにもリワードボタンを表示するか")]
    [SerializeField] private bool hideWhenAdFree = false;

    [Header("自動生成ボタンの位置 (Canvas 座標)")]
    [SerializeField] private Vector2 buttonPosition = new Vector2(-40f, 120f);
    [SerializeField] private Vector2 buttonSize     = new Vector2(280f, 56f);

    // ========================================================

    private void Start()
    {
        // 広告カット済み かつ 非表示設定 → 無効化
        if (hideWhenAdFree && AdsInitializer.IsAdFree)
        {
            if (rewardButton) rewardButton.gameObject.SetActive(false);
            return;
        }

        // ボタンが未割当なら自動生成
        if (rewardButton == null)
        {
            CreateButtonUI();
        }

        if (rewardButton != null)
        {
            rewardButton.onClick.RemoveAllListeners();
            rewardButton.onClick.AddListener(OnClickWatchAd);
        }

        RefreshButtonState();
    }

    private void OnEnable()
    {
        RefreshButtonState();
    }

    // ========== ボタン状態の更新 ==========

    private void RefreshButtonState()
    {
        if (rewardButton == null) return;

        var mgr = RewardedAdManager.Instance;
        bool ready = (mgr != null && mgr.IsReady);

        rewardButton.interactable = ready;

        if (buttonLabel != null)
        {
            int amount = (mgr != null) ? mgr.GemRewardAmount : 5;
            buttonLabel.text = ready
                ? $"広告を見て 宝石×{amount}"
                : "広告準備中…";
        }
    }

    // ========== ボタンクリック ==========

    private void OnClickWatchAd()
    {
        if (rewardButton) rewardButton.interactable = false;

        var mgr = RewardedAdManager.Instance;
        if (mgr == null)
        {
            RefreshButtonState();
            return;
        }

        mgr.ShowAd(success =>
        {
            if (success)
            {
                // 宝石数の表示を更新
                RefreshGemCountUI();
            }

            // 少し待ってからボタンを復帰（連打防止）
            Invoke(nameof(RefreshButtonState), 0.5f);
        });
    }

    // ========== 宝石UI更新 ==========

    private void RefreshGemCountUI()
    {
        // ① Inspector で直接割当された gemCountTMP
        if (gemCountTMP != null)
        {
            int gems = 0;
            try { gems = SpecialTileSystem.GetGems(); } catch { }
            gemCountTMP.text = gems.ToString();
        }

        // ② MenuController の RefreshGemCountUI を反射で呼ぶ（より確実）
        try
        {
            var mc = FindObjectOfType<MenuController>();
            if (mc != null)
            {
                var mi = typeof(MenuController).GetMethod("RefreshGemCountUI",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance);
                mi?.Invoke(mc, null);
            }
        }
        catch { }
    }

    // ========== 自動 UI 生成 ==========

    private void CreateButtonUI()
    {
        // Canvas を探す
        Canvas canvas = null;
        var allCanvas = FindObjectsOfType<Canvas>();
        foreach (var c in allCanvas)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay ||
                c.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvas = c;
                break;
            }
        }
        if (canvas == null) return;

        // ===== ボタン本体 =====
        var btnGO = new GameObject("RewardAdButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(canvas.transform, false);

        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);  // 右下基準
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(1f, 0f);
        rt.anchoredPosition = buttonPosition;
        rt.sizeDelta = buttonSize;

        var img = btnGO.GetComponent<Image>();
        img.color = new Color(0.18f, 0.55f, 0.34f, 0.92f); // 緑系

        rewardButton = btnGO.GetComponent<Button>();

        // ===== ラベル =====
        var labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(btnGO.transform, false);

        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(8f, 4f);
        labelRT.offsetMax = new Vector2(-8f, -4f);

        buttonLabel = labelGO.GetComponent<TextMeshProUGUI>();
        buttonLabel.fontSize  = 20;
        buttonLabel.alignment = TextAlignmentOptions.Center;
        buttonLabel.color     = Color.white;
        buttonLabel.text      = "広告準備中…";

        // ===== 宝石アイコンテキスト (簡易) =====
        // 必要に応じて Sprite を差し込んでもOK

        Debug.Log("[MenuRewardedAdButton] Reward ad button auto-created.");
    }
}
