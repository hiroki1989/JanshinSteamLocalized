using System;
using UnityEngine;
using UnityEngine.Advertisements;

/// <summary>
/// インタースティシャル広告（全画面広告）マネージャー。
/// 敗北時・強化画面遷移時に表示する。
/// 
/// ★ showEveryNth を Inspector で設定すると、N 回に 1 回だけ広告を表示します。
///   例: showEveryNth = 3 → 3 回目の遷移で初めて広告が出る。
///        1回目=スキップ, 2回目=スキップ, 3回目=表示, 4回目=スキップ, ...
/// 
/// AdsInitializer と同じ GameObject にアタッチしてください。
/// </summary>
public class InterstitialAdManager : MonoBehaviour, IUnityAdsLoadListener, IUnityAdsShowListener
{
    public static InterstitialAdManager Instance { get; private set; }

    [Header("広告ユニットID (Unity Dashboard で作成したもの)")]
    [SerializeField] private string _iOSAdUnitId      = "Interstitial_iOS";
    [SerializeField] private string _androidAdUnitId   = "Interstitial_Android";

    [Header("表示頻度: N 回に 1 回だけ広告を表示（1=毎回, 3=3回に1回）")]
    [SerializeField] private int showEveryNth = 3;

    [Header("最低クールダウン（秒）: この時間内は連続表示しない")]
    [SerializeField] private float cooldownSeconds = 30f;

    private string _adUnitId;
    private bool   _isLoaded = false;
    private Action _onClosed;

    // 表示頻度カウンター（セッション内でのみ保持）
    private int   _requestCount = 0;
    private float _lastShowTime = -999f;

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

    // ========== 広告のロード ==========

    /// <summary>
    /// 広告コンテンツをロード（初期化完了後に呼ぶ）。
    /// </summary>
    public void LoadAd()
    {
        if (AdsInitializer.IsAdFree) return;
        if (!Advertisement.isInitialized) return;
        if (string.IsNullOrEmpty(_adUnitId)) return;

        Advertisement.Load(_adUnitId, this);
    }

    // ========== 広告の表示 ==========

    /// <summary>
    /// インタースティシャル広告の表示を試みる。
    /// 表示するかどうかは頻度制御（showEveryNth）とクールダウンで決まる。
    /// 広告カット済み / 頻度スキップ / ロード未完了の場合は即座に onClosed を呼ぶ。
    /// </summary>
    /// <param name="onClosed">広告終了後（またはスキップ後）に実行するコールバック</param>
    public void ShowAdIfReady(Action onClosed = null)
    {
        _onClosed = onClosed;

        // ① 広告カット課金済み → 即スキップ
        if (AdsInitializer.IsAdFree)
        {
            _onClosed?.Invoke();
            return;
        }

        // ② 頻度制御: N 回に 1 回だけ表示
        _requestCount++;
        int nth = Mathf.Max(1, showEveryNth);
        if ((_requestCount % nth) != 0)
        {
            // 今回はスキップ
            _onClosed?.Invoke();
            return;
        }

        // ③ クールダウン中 → スキップ
        if (Time.realtimeSinceStartup - _lastShowTime < cooldownSeconds)
        {
            _onClosed?.Invoke();
            return;
        }

        // ④ ロード済みなら表示
        if (_isLoaded)
        {
            _isLoaded = false;
            Advertisement.Show(_adUnitId, this);
        }
        else
        {
            // ロードが間に合わなかった場合はスキップ
            _onClosed?.Invoke();
        }
    }

    // ========== IUnityAdsLoadListener ==========

    public void OnUnityAdsAdLoaded(string adUnitId)
    {
        if (adUnitId == _adUnitId)
        {
            _isLoaded = true;
            Debug.Log("[InterstitialAd] Ad loaded.");
        }
    }

    public void OnUnityAdsFailedToLoad(string adUnitId, UnityAdsLoadError error, string message)
    {
        Debug.LogWarning($"[InterstitialAd] Load failed: {error} - {message}");
        _isLoaded = false;
    }

    // ========== IUnityAdsShowListener ==========

    public void OnUnityAdsShowStart(string adUnitId) { }
    public void OnUnityAdsShowClick(string adUnitId) { }

    public void OnUnityAdsShowComplete(string adUnitId, UnityAdsShowCompletionState state)
    {
        _lastShowTime = Time.realtimeSinceStartup;
        Debug.Log("[InterstitialAd] Show complete.");
        LoadAd(); // 次回用にリロード
        _onClosed?.Invoke();
    }

    public void OnUnityAdsShowFailure(string adUnitId, UnityAdsShowError error, string message)
    {
        Debug.LogWarning($"[InterstitialAd] Show failed: {error} - {message}");
        LoadAd(); // 次回用にリロード
        _onClosed?.Invoke();
    }
}
