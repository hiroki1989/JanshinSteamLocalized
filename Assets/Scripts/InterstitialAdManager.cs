using System;
using System.Collections;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
public sealed class InterstitialAdManager : MonoBehaviour
{
    public static InterstitialAdManager Instance { get; private set; }
    [SerializeField] string _adUnitId = "ca-app-pub-3940256099942544/4411468910";
    [SerializeField, Min(1)] int showEveryNth = 3;
    [SerializeField, Min(0)] float cooldownSeconds = 100;
    InterstitialAd ad;
    bool loading, showing;
    int requests, failures, generation;
    float lastShow = -9999;
    Action completion;
    Coroutine retry;
    void Awake() {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject); AdsInitializer.AdFreeChanged += OnAdFreeChanged;
    }
    void Start() { if (AdsInitializer.IsSDKReady) LoadAd(); }
    void OnAdFreeChanged() {
        if (!AdsInitializer.IsAdFree) { LoadAd(); return; }
        ++generation; loading = false;
        if (retry != null) { StopCoroutine(retry); retry = null; }
        if (!showing) { ad?.Destroy(); ad = null; }
    }
    public void LoadAd() {
        if (!showing && ad != null && !ad.CanShowAd()) { ad.Destroy(); ad = null; }
        if (AdsInitializer.IsAdFree || !AdsInitializer.IsSDKReady || !AdsInitializer.CanRequestAds || loading || showing || ad != null) return;
        if (retry != null) { StopCoroutine(retry); retry = null; }
        loading = true; int request = ++generation;
        InterstitialAd.Load(_adUnitId, new AdRequest(), (loaded, error) => MobileAdsEventExecutor.ExecuteInUpdate(() => {
            if (!this || request != generation || AdsInitializer.IsAdFree) { loaded?.Destroy(); return; }
            loading = false;
            if (error != null || loaded == null) { loaded?.Destroy(); retry = StartCoroutine(Retry()); return; }
            ad = loaded; failures = 0;
            loaded.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(() => { if (this && ad == loaded) Finish(); });
            loaded.OnAdFullScreenContentFailed += _ => MobileAdsEventExecutor.ExecuteInUpdate(() => { if (this && ad == loaded) Finish(); });
        }));
    }
    IEnumerator Retry() {
        yield return new WaitForSecondsRealtime(Mathf.Min(60, 2 << Mathf.Min(failures++, 5)));
        retry = null; LoadAd();
    }
    public void ShowAdIfReady(Action onClosed = null) {
        // Duplicate clicks must not replace the original scene-transition callback.
        if (showing) return;
        if (AdsInitializer.IsAdFree || !AdsInitializer.CanRequestAds ||
            ++requests % Mathf.Max(1, showEveryNth) != 0 ||
            Time.realtimeSinceStartup - lastShow < cooldownSeconds || ad == null || !ad.CanShowAd())
        { onClosed?.Invoke(); LoadAd(); return; }
        showing = true; completion = onClosed; lastShow = Time.realtimeSinceStartup;
        try { ad.Show(); } catch (Exception e) { Debug.LogException(e); Finish(); }
    }
    void Finish() {
        if (!showing) return;
        showing = false; lastShow = Time.realtimeSinceStartup; ad?.Destroy(); ad = null;
        var callback = completion; completion = null; LoadAd(); callback?.Invoke();
    }
    void OnDestroy() {
        ++generation; AdsInitializer.AdFreeChanged -= OnAdFreeChanged; ad?.Destroy(); ad = null;
        if (Instance == this) { Instance = null; var callback = completion; completion = null; callback?.Invoke(); }
    }
}
