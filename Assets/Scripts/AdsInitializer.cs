using System;
using GoogleMobileAds.Common;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;

public sealed class AdsInitializer : MonoBehaviour
{
    public static AdsInitializer Instance { get; private set; }
    public static bool IsSDKReady { get; private set; }
    public static event Action StateChanged, AdFreeChanged;
    const string AdFreeKey = "IAP_AdFree";
    bool consentBusy, initializing;
    public static bool IsSupported => Application.isEditor || Application.platform == RuntimePlatform.IPhonePlayer;
    public static bool CanRequestAds => IsSupported && ConsentInformation.CanRequestAds();
    public static bool PrivacyOptionsRequired => IsSupported &&
        ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
    public static bool IsAdFree {
        get => PlayerPrefs.GetInt(AdFreeKey, 0) == 1;
        set {
            if (IsAdFree == value) return;
            PlayerPrefs.SetInt(AdFreeKey, value ? 1 : 0); PlayerPrefs.Save(); AdFreeChanged?.Invoke();
        }
    }
    void Awake() {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject); MobileAdsEventExecutor.Initialize();
    }
    void Start() => RetryConsent();
    void OnMain(Action action) {
        MobileAdsEventExecutor.ExecuteInUpdate(() => { if (this) action(); });
    }
    public void RetryConsent() {
        if (!IsSupported || consentBusy) return;
        consentBusy = true; StateChanged?.Invoke();
        ConsentInformation.Update(new ConsentRequestParameters(), error => OnMain(() => {
            if (error != null) { FinishConsent(error.Message); return; }
            ConsentForm.LoadAndShowConsentFormIfRequired(e => OnMain(() => FinishConsent(e?.Message)));
        }));
    }
    void FinishConsent(string error) {
        consentBusy = false;
        if (!string.IsNullOrEmpty(error)) Debug.LogWarning("[Ads] Consent: " + error);
        if (CanRequestAds) InitializeAdMob();
        StateChanged?.Invoke();
    }
    void InitializeAdMob() {
        if (IsSDKReady) { Preload(); return; }
        if (initializing) return;
        initializing = true;
        MobileAds.SetiOSAppPauseOnBackground(true);
        MobileAds.Initialize(status => OnMain(() => {
            if (!this) return;
            initializing = false;
            if (status == null) {
                Debug.LogWarning("[Ads] Initialization returned no status. Retry consent to retry initialization.");
                StateChanged?.Invoke(); return;
            }
            IsSDKReady = true; Preload(); StateChanged?.Invoke();
        }));
    }
    void Preload() { InterstitialAdManager.Instance?.LoadAd(); RewardedAdManager.Instance?.LoadAd(); }
    public void ShowPrivacyOptions() {
        if (consentBusy || !PrivacyOptionsRequired) return;
        consentBusy = true;
        ConsentForm.ShowPrivacyOptionsForm(e => OnMain(() => FinishConsent(e?.Message)));
    }
    void OnDestroy() { if (Instance == this) { Instance = null; IsSDKReady = false; } }
}
