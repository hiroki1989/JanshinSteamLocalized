using System;
using UnityEngine;
using GoogleMobileAds.Api;

/// <summary>
/// AdMob インタースティシャル広告（全画面広告）マネージャー。
/// 敗北時・強化画面遷移時に表示する。
///
/// ★ showEveryNth を Inspector で設定すると、N 回に 1 回だけ広告を表示します。
///   例: showEveryNth = 3 → 3 回目の遷移で初めて広告が出る。
///
/// AdsInitializer と同じ GameObject にアタッチしてください。
/// </summary>
public class InterstitialAdManager : MonoBehaviour
{
    public static InterstitialAdManager Instance { get; private set; }

    [Header("広告ユニットID (AdMob ダッシュボードで作成したもの)")]
    [Tooltip("テスト用ID が初期値。リリース時に本番IDに差し替えること。")]
#if UNITY_IOS
    [SerializeField] private string _adUnitId = "ca-app-pub-3940256099942544/4411468910"; // iOS テスト用
#elif UNITY_ANDROID
    [SerializeField] private string _adUnitId = "ca-app-pub-3940256099942544/1033173712"; // Android テスト用
#else
    [SerializeField] private string _adUnitId = "unused";
#endif

    [Header("表示頻度: N 回に 1 回だけ広告を表示（1=毎回, 3=3回に1回）")]
    [SerializeField] private int showEveryNth = 3;

    [Header("最低クールダウン（秒）: この時間内は連続表示しない")]
    [SerializeField] private float cooldownSeconds = 30f;

    private InterstitialAd _interstitialAd;
    private bool   _isLoaded = false;
    private Action _onClosed;

    // 表示頻度カウンター
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
    }

    // ========== 広告のロード ==========

    /// <summary>
    /// 広告コンテンツをロード（SDK初期化完了後に呼ぶ）。
    /// </summary>
    public void LoadAd()
    {
        if (AdsInitializer.IsAdFree) return;
        if (!AdsInitializer.IsSDKReady) return;

        // 前の広告を破棄
        if (_interstitialAd != null)
        {
            _interstitialAd.Destroy();
            _interstitialAd = null;
        }
        _isLoaded = false;

        var adRequest = new AdRequest();

        InterstitialAd.Load(_adUnitId, adRequest, (InterstitialAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogWarning($"[InterstitialAd] Load failed: {error}");
                _isLoaded = false;
                return;
            }

            _interstitialAd = ad;
            _isLoaded = true;
            Debug.Log("[InterstitialAd] Ad loaded.");

            // コールバック登録
            _interstitialAd.OnAdFullScreenContentClosed += OnAdClosed;
            _interstitialAd.OnAdFullScreenContentFailed += OnAdShowFailed;
        });
    }

    // ========== 広告の表示 ==========

    /// <summary>
    /// インタースティシャル広告の表示を試みる。
    /// 表示するかどうかは頻度制御（showEveryNth）とクールダウンで決まる。
    /// 広告カット済み / 頻度スキップ / ロード未完了の場合は即座に onClosed を呼ぶ。
    /// </summary>
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
        if (_isLoaded && _interstitialAd != null && _interstitialAd.CanShowAd())
        {
            _isLoaded = false;
            _interstitialAd.Show();
        }
        else
        {
            _onClosed?.Invoke();
        }
    }

    // ========== コールバック ==========

    private void OnAdClosed()
    {
        _lastShowTime = Time.realtimeSinceStartup;
        Debug.Log("[InterstitialAd] Ad closed.");

        // 広告オブジェクトを破棄して次をロード
        if (_interstitialAd != null)
        {
            _interstitialAd.Destroy();
            _interstitialAd = null;
        }
        LoadAd();

        _onClosed?.Invoke();
    }

    private void OnAdShowFailed(AdError error)
    {
        Debug.LogWarning($"[InterstitialAd] Show failed: {error}");

        if (_interstitialAd != null)
        {
            _interstitialAd.Destroy();
            _interstitialAd = null;
        }
        LoadAd();

        _onClosed?.Invoke();
    }

    private void OnDestroy()
    {
        if (_interstitialAd != null)
        {
            _interstitialAd.Destroy();
        }
    }
}
