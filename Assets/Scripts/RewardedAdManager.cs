using System;
using System.Collections;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

public sealed class RewardedAdManager : MonoBehaviour
{
    public static RewardedAdManager Instance { get; private set; }
    public static event Action StateChanged;
    [Header("iOS ad unit (Google test ID by default)")]
    [SerializeField] string _adUnitId = "ca-app-pub-3940256099942544/1712485313";
    [SerializeField, Min(1)] int gemReward = 1;
    [SerializeField, Min(0)] int maxDailyViews = 10;
    const string DateKey = "RewardedAd_Date", CountKey = "RewardedAd_Count";
    RewardedAd ad;
    bool loading, showing, earned, finishing;
    int failures, generation;
    Coroutine retry;
    Action<bool> completion;
    public bool IsLoading => loading;
    public bool IsShowing => showing;
    public bool IsReady => !showing && AdsInitializer.CanRequestAds && ad != null && ad.CanShowAd() && !DailyLimitReached;
    public int GemRewardAmount => Mathf.Max(1, gemReward);
    public bool DailyLimitReached => maxDailyViews > 0 &&
        PlayerPrefs.GetString(DateKey, "") == DateTime.Now.ToString("yyyyMMdd") &&
        PlayerPrefs.GetInt(CountKey, 0) >= maxDailyViews;
    void Awake() {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
    }
    void Start() { if (AdsInitializer.IsSDKReady) LoadAd(); }
    public void LoadAd() {
        if (!showing && ad != null && !ad.CanShowAd()) { ad.Destroy(); ad = null; }
        if (loading || showing || ad != null || !AdsInitializer.IsSDKReady || !AdsInitializer.CanRequestAds) return;
        if (retry != null) { StopCoroutine(retry); retry = null; }
        loading = true; int request = ++generation; StateChanged?.Invoke();
        RewardedAd.Load(_adUnitId, new AdRequest(), (loaded, error) => MobileAdsEventExecutor.ExecuteInUpdate(() => {
            if (!this || request != generation) { loaded?.Destroy(); return; }
            loading = false;
            if (error != null || loaded == null) {
                loaded?.Destroy(); retry = StartCoroutine(Retry()); StateChanged?.Invoke(); return;
            }
            ad = loaded; failures = 0;
            loaded.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(() => { if (this && ad == loaded && !finishing) StartCoroutine(FinishAfterClose()); });
            loaded.OnAdFullScreenContentFailed += _ => MobileAdsEventExecutor.ExecuteInUpdate(() => { if (this && ad == loaded) Finish(); });
            StateChanged?.Invoke();
        }));
    }
    IEnumerator Retry() {
        yield return new WaitForSecondsRealtime(Mathf.Min(60, 2 << Mathf.Min(failures++, 5)));
        retry = null; LoadAd();
    }
    public void ShowAd(Action<bool> onResult = null) {
        if (!IsReady) { onResult?.Invoke(false); if (!showing) LoadAd(); return; }
        completion = onResult; showing = true; earned = false; finishing = false;
        var shown = ad; StateChanged?.Invoke();
        try {
            shown.Show(_ => MobileAdsEventExecutor.ExecuteInUpdate(() => {
                if (!this || ad != shown || !showing || earned) return;
                try {
                    SpecialTileSystem.AddGems(GemRewardAmount);
                    earned = true; IncrementDailyCount();
                } catch (Exception e) { Debug.LogException(e); }
                StateChanged?.Invoke();
            }));
        } catch (Exception e) { Debug.LogException(e); Finish(); }
    }
    IEnumerator FinishAfterClose() { finishing = true; yield return null; Finish(); }
    void Finish() {
        if (!showing) return;
        bool success = earned; var callback = completion; completion = null;
        showing = false; finishing = false; ad?.Destroy(); ad = null;
        StateChanged?.Invoke(); LoadAd(); callback?.Invoke(success);
    }
    void IncrementDailyCount() {
        string today = DateTime.Now.ToString("yyyyMMdd");
        int count = PlayerPrefs.GetString(DateKey, "") == today ? PlayerPrefs.GetInt(CountKey, 0) : 0;
        PlayerPrefs.SetString(DateKey, today); PlayerPrefs.SetInt(CountKey, count + 1); PlayerPrefs.Save();
    }
    void OnDestroy() {
        ++generation; ad?.Destroy(); ad = null;
        if (Instance == this) { Instance = null; var callback = completion; completion = null; callback?.Invoke(earned); }
    }
}
