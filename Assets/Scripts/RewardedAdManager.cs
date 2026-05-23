using System;
using UnityEngine;
using UnityEngine.Advertisements;

/// <summary>
/// リワード広告マネージャー。
/// 広告を最後まで視聴したプレイヤーに宝石（Gems）を付与する。
/// 
/// AdsInitializer と同じ GameObject にアタッチしてください。
/// </summary>
public class RewardedAdManager : MonoBehaviour, IUnityAdsLoadListener, IUnityAdsShowListener
{
    public static RewardedAdManager Instance { get; private set; }

    [Header("広告ユニットID (Unity Dashboard で作成したもの)")]
    [SerializeField] private string _iOSAdUnitId    = "Rewarded_iOS";
    [SerializeField] private string _androidAdUnitId = "Rewarded_Android";

    [Header("1回の視聴完了で付与する宝石数")]
    [SerializeField] private int gemReward = 5;

    [Header("1日の視聴回数上限 (0 = 無制限)")]
    [SerializeField] private int maxDailyViews = 10;

    private string _adUnitId;
    private bool   _isLoaded = false;
    private Action<bool> _onResult;  // true = 報酬獲得, false = 失敗/スキップ

    // 日次視聴カウンター
    private const string PrefKey_DailyAdDate  = "RewardedAd_Date";
    private const string PrefKey_DailyAdCount = "RewardedAd_Count";

    // ========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_IOS
        _adUnitId = _iOSAdUnitId;
#elif UNITY_ANDROID
        _adUnitId = _androidAdUnitId;
#elif UNITY_EDITOR
        _adUnitId = _androidAdUnitId;
#endif
    }

    // ========== 公開プロパティ ==========

    /// <summary>広告がロード済みで表示可能か</summary>
    public bool IsReady => _isLoaded && !IsDailyLimitReached();

    /// <summary>1回の視聴で貰える宝石数</summary>
    public int GemRewardAmount => gemReward;

    // ========== 広告のロード ==========

    public void LoadAd()
    {
        if (!Advertisement.isInitialized) return;
        if (string.IsNullOrEmpty(_adUnitId)) return;

        Advertisement.Load(_adUnitId, this);
    }

    // ========== 広告の表示 ==========

    /// <summary>
    /// リワード広告を表示する。
    /// 視聴完了で宝石を付与し、onResult(true) を呼ぶ。
    /// スキップ/失敗時は onResult(false)。
    /// </summary>
    public void ShowAd(Action<bool> onResult = null)
    {
        _onResult = onResult;

        if (IsDailyLimitReached())
        {
            Debug.Log("[RewardedAd] Daily limit reached.");
            _onResult?.Invoke(false);
            return;
        }

        if (_isLoaded)
        {
            _isLoaded = false;
            Advertisement.Show(_adUnitId, this);
        }
        else
        {
            Debug.Log("[RewardedAd] Not loaded yet.");
            _onResult?.Invoke(false);
        }
    }

    // ========== 日次制限 ==========

    private bool IsDailyLimitReached()
    {
        if (maxDailyViews <= 0) return false; // 無制限

        string today = DateTime.Now.ToString("yyyyMMdd");
        string savedDate = PlayerPrefs.GetString(PrefKey_DailyAdDate, "");

        if (savedDate != today) return false; // 日付が変わっていればリセット

        int count = PlayerPrefs.GetInt(PrefKey_DailyAdCount, 0);
        return count >= maxDailyViews;
    }

    private void IncrementDailyCount()
    {
        string today = DateTime.Now.ToString("yyyyMMdd");
        string savedDate = PlayerPrefs.GetString(PrefKey_DailyAdDate, "");

        int count = 0;
        if (savedDate == today)
        {
            count = PlayerPrefs.GetInt(PrefKey_DailyAdCount, 0);
        }

        count++;
        PlayerPrefs.SetString(PrefKey_DailyAdDate, today);
        PlayerPrefs.SetInt(PrefKey_DailyAdCount, count);
        PlayerPrefs.Save();
    }

    // ========== IUnityAdsLoadListener ==========

    public void OnUnityAdsAdLoaded(string adUnitId)
    {
        if (adUnitId == _adUnitId)
        {
            _isLoaded = true;
            Debug.Log("[RewardedAd] Ad loaded.");
        }
    }

    public void OnUnityAdsFailedToLoad(string adUnitId, UnityAdsLoadError error, string message)
    {
        Debug.LogWarning($"[RewardedAd] Load failed: {error} - {message}");
        _isLoaded = false;
    }

    // ========== IUnityAdsShowListener ==========

    public void OnUnityAdsShowStart(string adUnitId) { }
    public void OnUnityAdsShowClick(string adUnitId) { }

    public void OnUnityAdsShowComplete(string adUnitId, UnityAdsShowCompletionState state)
    {
        if (state == UnityAdsShowCompletionState.COMPLETED)
        {
            // ★ 宝石を付与
            try { SpecialTileSystem.AddGems(gemReward); } catch { }
            IncrementDailyCount();
            Debug.Log($"[RewardedAd] Reward granted: {gemReward} gems.");
            _onResult?.Invoke(true);
        }
        else
        {
            // スキップされた
            Debug.Log("[RewardedAd] Skipped by user.");
            _onResult?.Invoke(false);
        }

        LoadAd(); // 次回用にリロード
    }

    public void OnUnityAdsShowFailure(string adUnitId, UnityAdsShowError error, string message)
    {
        Debug.LogWarning($"[RewardedAd] Show failed: {error} - {message}");
        _onResult?.Invoke(false);
        LoadAd();
    }
}
