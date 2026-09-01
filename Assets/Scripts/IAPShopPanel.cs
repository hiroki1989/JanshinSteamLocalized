using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 課金ショップパネル（広告カット＋宝石購入）。
///
/// 【使い方（2つの方法）】
///
/// ■ 方法A: Inspector で手動レイアウト
///   1. Canvas 内に Panel を作り、このスクリプトをアタッチ
///   2. ボタン・テキストを Inspector で割当
///
/// ■ 方法B: 自動生成
///   1. 任意の GameObject にこのスクリプトをアタッチ
///   2. shopPanelRoot が未割当なら Start() でUIを自動生成
///   3. 「ショップ」ボタンで開閉する
///
/// ■ メニューシーンに配置する場合の推奨:
///   - MenuController がある Canvas の子に空 GameObject を作り、
///     このスクリプトをアタッチするだけでOK
///   - 自動生成UIは画面中央にオーバーレイ表示される
/// </summary>
public class IAPShopPanel : MonoBehaviour
{
    [Header("パネル参照 (未割当なら自動生成)")]
    [SerializeField] private GameObject shopPanelRoot;

    [Header("ボタン参照 (自動生成時は不要)")]
    [SerializeField] private Button openShopButton;
    [SerializeField] private Button closeShopButton;
    [SerializeField] private Button removeAdsButton;
    [SerializeField] private Button buyGems100Button;
    [SerializeField] private Button buyGems500Button;
    [SerializeField] private Button buyGems1200Button;
    [SerializeField] private Button restorePurchasesButton;

    [Header("テキスト参照 (自動生成時は不要)")]
    [SerializeField] private TextMeshProUGUI removeAdsLabel;
    [SerializeField] private TextMeshProUGUI gems100Label;
    [SerializeField] private TextMeshProUGUI gems500Label;
    [SerializeField] private TextMeshProUGUI gems1200Label;
    [SerializeField] private TextMeshProUGUI removeAdsBadge; // 「購入済み」表示
    [SerializeField] private TextMeshProUGUI currentGemsLabel;

    [Header("宝石数テキスト (MenuController の gemCountTMP と同じものを割当可)")]
    [SerializeField] private TextMeshProUGUI gemCountTMP;

    // 自動生成用の参照保持
    private GameObject _autoPanel;

    // ========================================================

    private void Start()
    {
        // パネルが未割当なら自動生成
        if (shopPanelRoot == null)
        {
            CreateShopUI();
        }

        SetupButtons();
        RefreshUI();

        // パネルは初期非表示
        if (shopPanelRoot != null)
            shopPanelRoot.SetActive(false);

        // IAPManager の購入完了イベントを監視
        if (IAPManager.Instance != null)
            IAPManager.Instance.OnPurchaseCompleted += RefreshUI;
    }

    private void OnDestroy()
    {
        if (IAPManager.Instance != null)
            IAPManager.Instance.OnPurchaseCompleted -= RefreshUI;
    }

    private void OnEnable()
    {
        RefreshUI();
    }

    // ========== ボタン設定 ==========

    private void SetupButtons()
    {
        if (openShopButton)
            openShopButton.onClick.AddListener(OpenShop);

        if (closeShopButton)
            closeShopButton.onClick.AddListener(CloseShop);

        if (removeAdsButton)
            removeAdsButton.onClick.AddListener(OnClickRemoveAds);

        if (buyGems100Button)
            buyGems100Button.onClick.AddListener(OnClickBuyGems100);

        if (buyGems500Button)
            buyGems500Button.onClick.AddListener(OnClickBuyGems500);

        if (buyGems1200Button)
            buyGems1200Button.onClick.AddListener(OnClickBuyGems1200);

        if (restorePurchasesButton)
            restorePurchasesButton.onClick.AddListener(OnClickRestore);
    }

    // ========== 開閉 ==========

    public void OpenShop()
    {
        RefreshUI();
        if (shopPanelRoot) shopPanelRoot.SetActive(true);
    }

    public void CloseShop()
    {
        if (shopPanelRoot) shopPanelRoot.SetActive(false);
    }

    // ========== 購入ボタンハンドラ ==========

    private void OnClickRemoveAds()
    {
        if (IAPManager.Instance != null)
            IAPManager.Instance.BuyRemoveAds();
    }

    private void OnClickBuyGems100()
    {
        if (IAPManager.Instance != null)
            IAPManager.Instance.BuyGems100();
    }

    private void OnClickBuyGems500()
    {
        if (IAPManager.Instance != null)
            IAPManager.Instance.BuyGems500();
    }

    private void OnClickBuyGems1200()
    {
        if (IAPManager.Instance != null)
            IAPManager.Instance.BuyGems1200();
    }

    private void OnClickRestore()
    {
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.RestorePurchases(success =>
            {
                Debug.Log($"[IAPShopPanel] Restore result: {success}");
                RefreshUI();
            });
        }
    }

    // ========== UI更新 ==========

    public void RefreshUI()
    {
        var iap = IAPManager.Instance;

        // --- 宝石数 ---
        int gems = 0;
        try { gems = SpecialTileSystem.GetGems(); } catch { }

        if (currentGemsLabel) currentGemsLabel.text = $"所持宝石: {gems}";
        if (gemCountTMP) gemCountTMP.text = gems.ToString();

        // --- 広告カット ---
        bool adFree = AdsInitializer.IsAdFree;

        if (removeAdsButton)
        {
            removeAdsButton.gameObject.SetActive(!adFree);
            if (!adFree && removeAdsLabel && iap != null)
            {
                string price = iap.GetLocalizedPrice(iap.RemoveAdsProductId, "¥480");
                removeAdsLabel.text = $"広告カット  {price}";
            }
        }

        if (removeAdsBadge)
        {
            removeAdsBadge.gameObject.SetActive(adFree);
            removeAdsBadge.text = "✓ 購入済み";
        }

        // --- 宝石パック ---
        if (gems100Label && iap != null)
        {
            string price = iap.GetLocalizedPrice(iap.Gems100ProductId, "¥160");
            gems100Label.text = $"宝石 ×100  {price}";
        }

        if (gems500Label && iap != null)
        {
            string price = iap.GetLocalizedPrice(iap.Gems500ProductId, "¥650");
            gems500Label.text = $"宝石 ×500  {price}";
        }

        if (gems1200Label && iap != null)
        {
            string price = iap.GetLocalizedPrice(iap.Gems1200ProductId, "¥1,200");
            gems1200Label.text = $"宝石 ×1200  {price}";
        }

        // --- 復元ボタンは iOS のみ表示 ---
        if (restorePurchasesButton)
        {
#if UNITY_IOS
            restorePurchasesButton.gameObject.SetActive(true);
#else
            restorePurchasesButton.gameObject.SetActive(false);
#endif
        }

        // MenuController 側の宝石数も更新
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

    private void CreateShopUI()
    {
        // Canvas を探す
        Canvas canvas = null;
        foreach (var c in FindObjectsOfType<Canvas>())
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay ||
                c.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvas = c;
                break;
            }
        }
        if (canvas == null) return;

        // ===== オーバーレイ（暗幕） =====
        var overlayGO = new GameObject("IAPShopOverlay", typeof(RectTransform), typeof(Image));
        overlayGO.transform.SetParent(canvas.transform, false);
        var overlayRT = overlayGO.GetComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;
        overlayGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.7f);

        shopPanelRoot = overlayGO;

        // ===== 中央パネル =====
        var panelGO = new GameObject("ShopPanel", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(overlayGO.transform, false);
        var panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(600, 520);
        panelRT.anchoredPosition = Vector2.zero;
        panelGO.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.20f, 0.96f);

        float yPos = 220f; // 上から配置していく

        // ===== タイトル =====
        var titleTMP = CreateTMP(panelGO.transform, "ショップ", 28, TextAlignmentOptions.Center);
        titleTMP.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        titleTMP.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        titleTMP.rectTransform.anchoredPosition = new Vector2(0, -30);

        // ===== 所持宝石 =====
        currentGemsLabel = CreateTMP(panelGO.transform, "所持宝石: 0", 20, TextAlignmentOptions.Center);
        currentGemsLabel.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        currentGemsLabel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        currentGemsLabel.rectTransform.anchoredPosition = new Vector2(0, -65);
        currentGemsLabel.color = new Color(1f, 0.85f, 0.2f);

        // ===== 広告カットボタン =====
        yPos = -105f;
        removeAdsButton = CreateButton(panelGO.transform, "RemoveAds", yPos,
            new Color(0.7f, 0.25f, 0.25f, 1f));
        removeAdsLabel = removeAdsButton.GetComponentInChildren<TextMeshProUGUI>();
        removeAdsLabel.text = "広告カット  ¥480";

        // 購入済みバッジ
        removeAdsBadge = CreateTMP(panelGO.transform, "✓ 購入済み", 18, TextAlignmentOptions.Center);
        removeAdsBadge.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        removeAdsBadge.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        removeAdsBadge.rectTransform.anchoredPosition = new Vector2(0, yPos);
        removeAdsBadge.color = new Color(0.5f, 1f, 0.5f);
        removeAdsBadge.gameObject.SetActive(false);

        // ===== 宝石100ボタン =====
        yPos = -165f;
        buyGems100Button = CreateButton(panelGO.transform, "Gems100", yPos,
            new Color(0.2f, 0.45f, 0.7f, 1f));
        gems100Label = buyGems100Button.GetComponentInChildren<TextMeshProUGUI>();
        gems100Label.text = "宝石 ×100  ¥160";

        // ===== 宝石500ボタン =====
        yPos = -225f;
        buyGems500Button = CreateButton(panelGO.transform, "Gems500", yPos,
            new Color(0.2f, 0.45f, 0.7f, 1f));
        gems500Label = buyGems500Button.GetComponentInChildren<TextMeshProUGUI>();
        gems500Label.text = "宝石 ×500  ¥650";

        // ===== 宝石1200ボタン =====
        yPos = -285f;
        buyGems1200Button = CreateButton(panelGO.transform, "Gems1200", yPos,
            new Color(0.2f, 0.55f, 0.45f, 1f));
        gems1200Label = buyGems1200Button.GetComponentInChildren<TextMeshProUGUI>();
        gems1200Label.text = "宝石 ×1200  ¥1,200";

        // ===== 購入を復元ボタン =====
        yPos = -345f;
        var restoreBtn = CreateButton(panelGO.transform, "Restore", yPos,
            new Color(0.35f, 0.35f, 0.4f, 1f), new Vector2(300, 40));
        restorePurchasesButton = restoreBtn;
        var restoreLabel = restoreBtn.GetComponentInChildren<TextMeshProUGUI>();
        restoreLabel.text = "購入を復元";
        restoreLabel.fontSize = 16;

        // ===== 閉じるボタン =====
        yPos = -410f;
        closeShopButton = CreateButton(panelGO.transform, "Close", yPos,
            new Color(0.4f, 0.4f, 0.45f, 1f), new Vector2(200, 44));
        var closeLabel = closeShopButton.GetComponentInChildren<TextMeshProUGUI>();
        closeLabel.text = "閉じる";

        // ===== 暗幕タップで閉じる =====
        var overlayBtn = overlayGO.AddComponent<Button>();
        overlayBtn.onClick.AddListener(CloseShop);
        // パネル内のクリックは伝搬しないようにする
        panelGO.AddComponent<Button>().onClick.AddListener(() => { }); // 空リスナーで止める

        Debug.Log("[IAPShopPanel] Shop UI auto-created.");
    }

    // ========== UI生成ヘルパー ==========

    private Button CreateButton(Transform parent, string name, float yOffset, Color bgColor, Vector2? size = null)
    {
        Vector2 btnSize = size ?? new Vector2(480, 48);

        var btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);

        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.sizeDelta = btnSize;
        rt.anchoredPosition = new Vector2(0, yOffset);

        btnGO.GetComponent<Image>().color = bgColor;

        // ラベル
        var labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(btnGO.transform, false);
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(8, 4);
        labelRT.offsetMax = new Vector2(-8, -4);

        var tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 20;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.text = name;

        return btnGO.GetComponent<Button>();
    }

    private TextMeshProUGUI CreateTMP(Transform parent, string text, int fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject("TMP_" + text, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.text = text;
        tmp.rectTransform.sizeDelta = new Vector2(500, 40);
        return tmp;
    }
}
