using TMPro;
using UnityEngine;
using UnityEngine.UI;
public sealed class MenuRewardedAdButton : MonoBehaviour
{
    [SerializeField] Button rewardButton;
    [SerializeField] TextMeshProUGUI buttonLabel, gemCountTMP;
    [SerializeField] bool hideWhenAdFree;
    [SerializeField] TextMeshProUGUI statusLabel;
    string message;
    float nextRefresh;
    void Awake() {
        if (rewardButton) { rewardButton.onClick.RemoveListener(OnClickWatchAd); rewardButton.onClick.AddListener(OnClickWatchAd); }
    }
    void OnEnable() {
        RewardedAdManager.StateChanged += RefreshButtonState;
        AdsInitializer.StateChanged += RefreshButtonState; AdsInitializer.AdFreeChanged += RefreshButtonState;
        GemWallet.Changed += RefreshButtonState; LocalizationManager.LanguageChanged += LanguageChanged;
        RefreshButtonState();
    }
    void OnDisable() {
        RewardedAdManager.StateChanged -= RefreshButtonState;
        AdsInitializer.StateChanged -= RefreshButtonState; AdsInitializer.AdFreeChanged -= RefreshButtonState;
        GemWallet.Changed -= RefreshButtonState; LocalizationManager.LanguageChanged -= LanguageChanged;
    }
    void Update() { if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 1; RefreshButtonState(); } }
    void LanguageChanged(LocalizationManager.Language _) => RefreshButtonState();
    void OnClickWatchAd() {
        var manager = RewardedAdManager.Instance;
        if (!manager) return;
        if (!AdsInitializer.IsSDKReady || !AdsInitializer.CanRequestAds) { AdsInitializer.Instance?.RetryConsent(); return; }
        if (!manager.IsReady) { manager.LoadAd(); return; }
        message = null;
        manager.ShowAd(success => { if (!this) return; message = success ? "earned" : "notEarned"; RefreshButtonState(); });
    }
    public void RefreshButtonState() {
        if (!rewardButton) return;
        var manager = RewardedAdManager.Instance;
        rewardButton.gameObject.SetActive(!(hideWhenAdFree && AdsInitializer.IsAdFree));
        bool ready = manager && manager.IsReady, capped = manager && manager.DailyLimitReached;
        bool showing = manager && manager.IsShowing, loading = manager && manager.IsLoading;
        rewardButton.interactable = AdsInitializer.IsSupported && !capped && !showing && !loading;
        if (buttonLabel) {
            buttonLabel.text = capped ? MonetizationText.Get("本日の広告報酬は上限です","Daily reward limit reached","已达今日奖励上限") :
                showing ? MonetizationText.Get("広告を表示中…","Ad in progress…","广告播放中…") :
                ready ? MonetizationText.Get("広告を見て 宝石 ×","Watch ad: Gems ×","观看广告：宝石 ×") + manager.GemRewardAmount :
                loading ? MonetizationText.Get("広告を準備中…","Loading ad…","正在加载广告…") :
                MonetizationText.Get("広告を再読み込み","Retry loading ad","重新加载广告");
            MonetizationText.Font(buttonLabel);
        }
        if (gemCountTMP) gemCountTMP.text = SpecialTileSystem.GetGems().ToString();
        if (statusLabel) {
            statusLabel.text = message == "earned" ? MonetizationText.Get("宝石を受け取りました。","Gems received.","已获得宝石。") :
                message == "notEarned" ? MonetizationText.Get("視聴が完了しなかったか、広告を表示できませんでした。","Ad not completed or could not be shown.","广告未看完或无法显示。") :
                !ready && !loading && !showing && !capped ? MonetizationText.Get("接続を確認して再度お試しください。","Check your connection and try again.","请检查网络后重试。") : "";
            MonetizationText.Font(statusLabel);
        }
    }
}
