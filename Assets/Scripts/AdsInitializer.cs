using UnityEngine;
using UnityEngine.Advertisements;

/// <summary>
/// Unity Ads SDK の初期化を行うシングルトン。
/// 最初のシーン（Menu等）の空 GameObject にアタッチしてください。
/// DontDestroyOnLoad で全シーンに常駐します。
/// </summary>
public class AdsInitializer : MonoBehaviour, IUnityAdsInitializationListener
{
    public static AdsInitializer Instance { get; private set; }

    [Header("Game IDs (Unity Dashboard で発行されたもの)")]
    [SerializeField] private string _iOSGameId     = "6119488";
    [SerializeField] private string _androidGameId  = "6119489";

    [Header("テストモード（リリース時に OFF にする）")]
    [SerializeField] private bool _testMode = true;

    // ========== 広告カット課金フラグ ==========
    private const string PrefKey_AdFree = "IAP_AdFree";

    /// <summary>
    /// 広告カット課金済みかどうか。
    /// true にすると InterstitialAdManager が広告を一切表示しなくなる。
    /// </summary>
    public static bool IsAdFree
    {
        get => PlayerPrefs.GetInt(PrefKey_AdFree, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(PrefKey_AdFree, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

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

        InitializeAds();
    }

    private void InitializeAds()
    {
        string gameId = "";
#if UNITY_IOS
        gameId = _iOSGameId;
#elif UNITY_ANDROID
        gameId = _androidGameId;
#elif UNITY_EDITOR
        gameId = _androidGameId; // Editor ではテスト用に Android ID を使用
#endif

        if (!Advertisement.isInitialized && Advertisement.isSupported)
        {
            Advertisement.Initialize(gameId, _testMode, this);
        }
    }

    // ========== IUnityAdsInitializationListener ==========

    public void OnInitializationComplete()
    {
        Debug.Log("[AdsInitializer] Unity Ads initialization complete.");

        // SDK 初期化完了後に広告をプリロード
        try { InterstitialAdManager.Instance?.LoadAd(); } catch { }
        try { RewardedAdManager.Instance?.LoadAd(); }     catch { }
    }

    public void OnInitializationFailed(UnityAdsInitializationError error, string message)
    {
        Debug.LogError($"[AdsInitializer] Unity Ads init failed: {error} - {message}");
    }
}
