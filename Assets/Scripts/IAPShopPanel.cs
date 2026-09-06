using TMPro;
using UnityEngine;
using UnityEngine.UI;
public sealed class IAPShopPanel : MonoBehaviour
{
    [SerializeField] GameObject shopPanelRoot;
    [SerializeField] Button openShopButton, closeShopButton, removeAdsButton, buyGems100Button, buyGems500Button, buyGems1200Button, restorePurchasesButton;
    [SerializeField] TextMeshProUGUI removeAdsLabel, gems100Label, gems500Label, gems1200Label, removeAdsBadge, currentGemsLabel, gemCountTMP;
    [SerializeField] TextMeshProUGUI titleLabel, descriptionLabel, statusLabel;
    [SerializeField] Button retryButton, privacyButton;
    public GameObject PanelRoot => shopPanelRoot;
    private readonly System.Collections.Generic.List<UnityEngine.EventSystems.BaseRaycaster> suspendedRaycasters = new System.Collections.Generic.List<UnityEngine.EventSystems.BaseRaycaster>();
    private GameObject previousSelection;
    private void SuspendBackground()
    {
        if (!Application.isPlaying || suspendedRaycasters.Count > 0) return;
        previousSelection = UnityEngine.EventSystems.EventSystem.current ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
        foreach (var raycaster in FindObjectsByType<UnityEngine.EventSystems.BaseRaycaster>(FindObjectsSortMode.None))
            if (raycaster.enabled && !raycaster.transform.IsChildOf(shopPanelRoot.transform))
            { suspendedRaycasters.Add(raycaster); raycaster.enabled = false; }
        if (UnityEngine.EventSystems.EventSystem.current && closeShopButton)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(closeShopButton.gameObject);
    }
    private void RestoreBackground()
    {
        foreach (var raycaster in suspendedRaycasters) if (raycaster) raycaster.enabled = true;
        suspendedRaycasters.Clear();
        if (Application.isPlaying && UnityEngine.EventSystems.EventSystem.current)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }

    void Awake() {
        Wire(openShopButton, OpenShop); Wire(closeShopButton, CloseShop);
        Wire(removeAdsButton, BuyRemoveAds); Wire(buyGems100Button, Buy100);
        Wire(buyGems500Button, Buy500); Wire(buyGems1200Button, Buy1200);
        Wire(restorePurchasesButton, Restore); Wire(retryButton, Retry); Wire(privacyButton, Privacy);
        if (shopPanelRoot) shopPanelRoot.SetActive(false);
    }
    static void Wire(Button button, UnityEngine.Events.UnityAction action) {
        if (button) { button.onClick.RemoveListener(action); button.onClick.AddListener(action); }
    }
    void OnEnable() {
        IAPManager.StateChanged += RefreshUI; GemWallet.Changed += RefreshUI;
        AdsInitializer.AdFreeChanged += RefreshUI; AdsInitializer.StateChanged += RefreshUI;
        LocalizationManager.LanguageChanged += LanguageChanged; RefreshUI();
    }
    void OnDisable() { RestoreBackground();
        IAPManager.StateChanged -= RefreshUI; GemWallet.Changed -= RefreshUI;
        AdsInitializer.AdFreeChanged -= RefreshUI; AdsInitializer.StateChanged -= RefreshUI;
        LocalizationManager.LanguageChanged -= LanguageChanged;
    }
    void LanguageChanged(LocalizationManager.Language _) => RefreshUI();
    public void OpenShop() {
        if (!shopPanelRoot) return;
        if (Application.isPlaying) shopPanelRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        shopPanelRoot.SetActive(true); shopPanelRoot.transform.SetAsLastSibling(); SuspendBackground(); RefreshUI();
    }
    public void CloseShop() { RestoreBackground(); if (shopPanelRoot) shopPanelRoot.SetActive(false); }
    void BuyRemoveAds() => IAPManager.Instance?.BuyRemoveAds();
    void Buy100() => IAPManager.Instance?.BuyGems100();
    void Buy500() => IAPManager.Instance?.BuyGems500();
    void Buy1200() => IAPManager.Instance?.BuyGems1200();
    void Restore() => IAPManager.Instance?.RestorePurchases();
    void Retry() => IAPManager.Instance?.InitializePurchasing();
    void Privacy() => AdsInitializer.Instance?.ShowPrivacyOptions();
    static void ButtonText(Button button, string text) {
        if (!button) return;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label) { label.text = text; MonetizationText.Font(label); }
    }
    public void RefreshUI() {
        var iap = IAPManager.Instance;
        if (titleLabel) titleLabel.text = MonetizationText.Get("宝石・広告カット","Gems & Ad Removal","宝石与去广告");
        if (descriptionLabel) descriptionLabel.text = MonetizationText.Get(
            "広告カットは自動表示の全画面広告を停止します。任意のリワード広告は引き続き利用できます。",
            "Ad removal stops automatic full-screen ads. Optional rewarded ads remain available.",
            "去广告将停止自动全屏广告，仍可自愿观看奖励广告。");
        int gems = SpecialTileSystem.GetGems();
        if (currentGemsLabel) currentGemsLabel.text = MonetizationText.Get("所持宝石：","Gems: ","持有宝石：") + gems;
        if (gemCountTMP) gemCountTMP.text = gems.ToString();
        bool owned = AdsInitializer.IsAdFree;
        if (removeAdsBadge) { removeAdsBadge.gameObject.SetActive(owned); removeAdsBadge.text = MonetizationText.Get("広告カット購入済み","Ad removal purchased","已购买去广告"); }
        if (removeAdsLabel) removeAdsLabel.text = MonetizationText.Get("広告カット","Remove ads","去广告") + "   " +
            (owned ? MonetizationText.Get("購入済み","Purchased","已购买") : iap?.GetLocalizedPrice(iap.RemoveAdsProductId) ?? "—");
        Pack(gems100Label, iap ? iap.Gems100Amount : 10, iap?.GetLocalizedPrice(iap.Gems100ProductId));
        Pack(gems500Label, iap ? iap.Gems500Amount : 55, iap?.GetLocalizedPrice(iap.Gems500ProductId));
        Pack(gems1200Label, iap ? iap.Gems1200Amount : 130, iap?.GetLocalizedPrice(iap.Gems1200ProductId));
        if (removeAdsButton) removeAdsButton.interactable = iap && iap.CanBuy(iap.RemoveAdsProductId);
        if (buyGems100Button) buyGems100Button.interactable = iap && iap.CanBuy(iap.Gems100ProductId);
        if (buyGems500Button) buyGems500Button.interactable = iap && iap.CanBuy(iap.Gems500ProductId);
        if (buyGems1200Button) buyGems1200Button.interactable = iap && iap.CanBuy(iap.Gems1200ProductId);
        if (restorePurchasesButton) restorePurchasesButton.interactable = iap && iap.IsInitialized && !iap.IsBusy;
        if (retryButton) { retryButton.gameObject.SetActive(!iap || !iap.IsInitialized); retryButton.interactable = iap && !iap.IsBusy; }
        if (privacyButton) privacyButton.gameObject.SetActive(AdsInitializer.PrivacyOptionsRequired);
        if (statusLabel) statusLabel.text = MonetizationText.Status(iap ? iap.Status : "connecting");
        ButtonText(openShopButton, MonetizationText.Get("宝石購入・広告カット","Buy gems / Remove ads","购买宝石／去广告"));
        ButtonText(closeShopButton, MonetizationText.Get("閉じる","Close","关闭"));
        ButtonText(restorePurchasesButton, MonetizationText.Get("購入を復元","Restore purchases","恢复购买"));
        ButtonText(retryButton, MonetizationText.Get("ストアへ再接続","Reconnect","重新连接"));
        ButtonText(privacyButton, MonetizationText.Get("広告のプライバシー設定","Ad privacy options","广告隐私设置"));
        if (shopPanelRoot) foreach (var text in shopPanelRoot.GetComponentsInChildren<TMP_Text>(true)) MonetizationText.Font(text);
    }
    static void Pack(TMP_Text text, int amount, string price) {
        if (text) text.text = MonetizationText.Get("宝石 ×","Gems ×","宝石 ×") + amount + "   " + (price ?? "—");
    }
}
