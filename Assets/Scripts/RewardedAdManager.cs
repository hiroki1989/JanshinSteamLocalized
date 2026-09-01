using System;
using UnityEngine;
using GoogleMobileAds.Api;

/// <summary>
/// AdMob リワード広告マネージャー。
/// 広告を最後まで視聴したプレイヤーに宝石（Gems）を付与する。
///
/// AdsInitializer と同じ GameObject にアタッチしてください。
/// </summary>
public class RewardedAdManager : MonoBehaviour
{
    public static RewardedAdManager Instance { get; private set; }

    [Header("広告ユニットID (AdMob ダッシュボードで作成したもの)")]
    [Tooltip("テスト用ID が初期値。リリース時に本番IDに差し替えること。")]
#if UNITY_IOS
    [SerializeField] private string _adUnitId = "ca-app-pub-3940256099942544/1712485313"; // iOS テスト用
#elif UNITY_ANDROID
    [SerializeField] private string _adUnitId = "ca-app-pub-3940256099942544/5224354917"; // Android テスト用
#else
    [SerializeField] private string _adUnitId = "unused";
#endif

    [Header("1回の視聴完了で付与する宝石数")]
    [SerializeField] private int gemReward = 5;

    [Header("1日の視聴回数上限 (0 = 無制限)")]
    [SerializeField] private int maxDailyViews = 10;

    private RewardedAd _rewardedAd;
    private bool _isLoaded = false;
    private Action<bool> _onResult;

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
    }

    // ========== 公開プロパティ ==========

    /// <summary>広告がロード済みで表示可能か</summary>
    public bool IsReady => _isLoaded && _rewardedAd != null && _rewardedAd.CanShowAd() && !IsDailyLimitReached();

    /// <summary>1回の視聴で貰える宝石数</summary>
    public int GemRewardAmount => gemReward;

    // ========== 広告のロード ==========

    public void LoadAd()
    {
        if (!AdsInitializer.IsSDKReady) return;

        // 前の広告を破棄
        if (_rewardedAd != null)
        {
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }
        _isLoaded = false;

        var adRequest = new AdRequest();

        RewardedAd.Load(_adUnitId, adRequest, (RewardedAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogWarning($"[RewardedAd] Load failed: {error}");
                _isLoaded = false;
                return;
            }

            _rewardedAd = ad;
            _isLoaded = true;
            Debug.Log("[RewardedAd] Ad loaded.");

            // コールバック登録
            _rewardedAd.OnAdFullScreenContentClosed += OnAdClosed;
            _rewardedAd.OnAdFullScreenContentFailed += OnAdShowFailed;
        });
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

        if (_isLoaded && _rewardedAd != null && _rewardedAd.CanShowAd())
        {
            _isLoaded = false;

            _rewardedAd.Show(reward =>
            {
                // ★ 視聴完了 → 宝石を付与
                try { SpecialTileSystem.AddGems(gemReward); } catch { }
                IncrementDailyCount();
                Debug.Log($"[RewardedAd] Reward granted: {gemReward} gems. (type: {reward.Type}, amount: {reward.Amount})");
                _onResult?.Invoke(true);
            });
        }
        else
        {
            Debug.Log("[RewardedAd] Not loaded yet.");
            _onResult?.Invoke(false);
        }
    }

    // ========== コールバック ==========

    private void OnAdClosed()
    {
        Debug.Log("[RewardedAd] Ad closed.");
        if (_rewardedAd != null)
        {
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }
        LoadAd();
    }

    private void OnAdShowFailed(AdError error)
    {
        Debug.LogWarning($"[RewardedAd] Show failed: {error}");
        if (_rewardedAd != null)
        {
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }
        _onResult?.Invoke(false);
        LoadAd();
    }

    private void OnDestroy()
    {
        if (_rewardedAd != null)
        {
            _rewardedAd.Destroy();
        }
    }

    // ========== 日次制限 ==========

    private bool IsDailyLimitReached()
    {
        if (maxDailyViews <= 0) return false;

        string today = DateTime.Now.ToString("yyyyMMdd");
        string savedDate = PlayerPrefs.GetString(PrefKey_DailyAdDate, "");

        if (savedDate != today) return false;

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
}
