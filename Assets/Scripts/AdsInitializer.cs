using UnityEngine;
using GoogleMobileAds.Api;

/// <summary>
/// Google AdMob SDK の初期化を行うシングルトン。
/// 最初のシーン（Menu等）の空 GameObject にアタッチしてください。
/// DontDestroyOnLoad で全シーンに常駐します。
///
/// ★ SDK導入手順:
///   1. https://github.com/googleads/googleads-mobile-unity/releases から
///      最新の GoogleMobileAds-vX.X.X.unitypackage をダウンロード
///   2. Assets → Import Package → Custom Package で導入
///   3. Assets → Google Mobile Ads → Settings で App ID を設定
///      テスト用 iOS App ID:  ca-app-pub-3940256099942544~1458002511
///      テスト用 Android App ID: ca-app-pub-3940256099942544~3347511713
///      ※ リリース時に AdMob ダッシュボードで発行した本番 App ID に差し替える
/// </summary>
public class AdsInitializer : MonoBehaviour
{
    public static AdsInitializer Instance { get; private set; }

    /// <summary>SDK 初期化が完了したか</summary>
    public static bool IsSDKReady { get; private set; } = false;

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

        InitializeAdMob();
    }

    private void InitializeAdMob()
    {
        Debug.Log("[AdsInitializer] Initializing AdMob...");

        MobileAds.Initialize(initStatus =>
        {
            IsSDKReady = true;
            Debug.Log("[AdsInitializer] AdMob initialization complete.");

            // SDK 初期化完了後に広告をプリロード
            try { InterstitialAdManager.Instance?.LoadAd(); } catch { }
            try { RewardedAdManager.Instance?.LoadAd(); }     catch { }
        });
    }
}
