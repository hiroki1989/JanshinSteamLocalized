using System;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

/// <summary>
/// App内課金（IAP）マネージャー。
/// 
/// 【商品一覧】
///   - 広告カット（非消耗型 = 一度買えば永久）
///   - 宝石パック 3種（消耗型 = 何度でも購入可能）
///
/// 【セットアップ】
///   1. Unity Editor → Window → Package Manager → "In App Purchasing" をインストール
///   2. Edit → Project Settings → Services → In-App Purchasing を有効化
///   3. AdsInitializer と同じ GameObject にアタッチ
///   4. App Store Connect で同じ Product ID の商品を作成
///
/// 【商品IDの変更】
///   Inspector で Product ID を変更可能。
///   App Store Connect 側の商品IDと完全一致させること。
/// </summary>
public class IAPManager : MonoBehaviour, IDetailedStoreListener
{
    public static IAPManager Instance { get; private set; }

    // ========== 商品 ID（Inspector で変更可能） ==========

    [Header("商品 ID（App Store Connect と一致させること）")]
    [SerializeField] private string removeAdsProductId = "com.yourapp.removeads";
    [SerializeField] private string gems100ProductId   = "com.yourapp.gems_100";
    [SerializeField] private string gems500ProductId   = "com.yourapp.gems_500";
    [SerializeField] private string gems1200ProductId  = "com.yourapp.gems_1200";

    [Header("宝石パックの付与数")]
    [SerializeField] private int gems100Amount  = 100;
    [SerializeField] private int gems500Amount  = 500;
    [SerializeField] private int gems1200Amount = 1200;

    // ========== 内部状態 ==========

    private IStoreController   _controller;
    private IExtensionProvider _extensions;
    private bool _isInitialized = false;

    /// <summary>IAP が初期化済みか</summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>購入完了時に外部から受け取るコールバック（UIリフレッシュ用）</summary>
    public event Action OnPurchaseCompleted;

    // ========== 商品IDの公開（UI側から参照用） ==========

    public string RemoveAdsProductId => removeAdsProductId;
    public string Gems100ProductId   => gems100ProductId;
    public string Gems500ProductId   => gems500ProductId;
    public string Gems1200ProductId  => gems1200ProductId;

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

        InitializePurchasing();
    }

    // ========== 初期化 ==========

    private void InitializePurchasing()
    {
        if (_isInitialized) return;

        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

        // 広告カット（非消耗型：一度購入すれば永久に有効）
        builder.AddProduct(removeAdsProductId, ProductType.NonConsumable);

        // 宝石パック（消耗型：何度でも購入可能）
        builder.AddProduct(gems100ProductId,  ProductType.Consumable);
        builder.AddProduct(gems500ProductId,  ProductType.Consumable);
        builder.AddProduct(gems1200ProductId, ProductType.Consumable);

        UnityPurchasing.Initialize(this, builder);
    }

    // ========== 購入メソッド（UIボタンから呼ぶ） ==========

    public void BuyRemoveAds()  => InitiatePurchase(removeAdsProductId);
    public void BuyGems100()    => InitiatePurchase(gems100ProductId);
    public void BuyGems500()    => InitiatePurchase(gems500ProductId);
    public void BuyGems1200()   => InitiatePurchase(gems1200ProductId);

    private void InitiatePurchase(string productId)
    {
        if (!_isInitialized)
        {
            Debug.LogWarning("[IAPManager] Not initialized yet.");
            return;
        }

        if (_controller == null)
        {
            Debug.LogWarning("[IAPManager] Store controller is null.");
            return;
        }

        _controller.InitiatePurchase(productId);
    }

    // ========== 購入の復元（iOS 必須） ==========

    /// <summary>
    /// 購入を復元する。設定画面の「購入を復元」ボタンから呼ぶ。
    /// iOS では非消耗型の購入復元ボタンが必須（ないとリジェクトされる）。
    /// </summary>
    public void RestorePurchases(Action<bool> onComplete = null)
    {
        if (!_isInitialized)
        {
            onComplete?.Invoke(false);
            return;
        }

#if UNITY_IOS
        var apple = _extensions.GetExtension<IAppleExtensions>();
apple.RestoreTransactions((result, error) =>
{
    Debug.Log($"[IAPManager] Restore result: {result}, error: {error}");
    onComplete?.Invoke(result);
});
#else
        // Android は自動復元されるため不要
        Debug.Log("[IAPManager] Restore not needed on this platform.");
        onComplete?.Invoke(true);
#endif
    }

    // ========== ローカライズ価格の取得 ==========

    /// <summary>
    /// 商品のローカライズ済み価格文字列を取得する（例: "¥480", "$3.99"）。
    /// 未初期化や商品が見つからない場合は fallback を返す。
    /// </summary>
    public string GetLocalizedPrice(string productId, string fallback = "---")
    {
        if (!_isInitialized || _controller == null) return fallback;

        var product = _controller.products.WithID(productId);
        if (product == null || !product.availableToPurchase) return fallback;

        return product.metadata.localizedPriceString;
    }

    /// <summary>広告カットが購入済みかどうか</summary>
    public bool IsRemoveAdsPurchased()
    {
        if (!_isInitialized || _controller == null) return AdsInitializer.IsAdFree;

        var product = _controller.products.WithID(removeAdsProductId);
        if (product != null && product.hasReceipt)
        {
            return true;
        }
        return AdsInitializer.IsAdFree;
    }

    // ========== IDetailedStoreListener コールバック ==========

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        _controller = controller;
        _extensions = extensions;
        _isInitialized = true;
        Debug.Log("[IAPManager] IAP initialization complete.");

        // 広告カットの復元チェック（アプリ再インストール時など）
        var removeAds = controller.products.WithID(removeAdsProductId);
        if (removeAds != null && removeAds.hasReceipt)
        {
            AdsInitializer.IsAdFree = true;
            Debug.Log("[IAPManager] Remove Ads already purchased - restored.");
        }
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.LogError($"[IAPManager] Init failed: {error}");
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.LogError($"[IAPManager] Init failed: {error} - {message}");
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        string productId = args.purchasedProduct.definition.id;
        Debug.Log($"[IAPManager] Purchase success: {productId}");

        // ===== 広告カット =====
        if (productId == removeAdsProductId)
        {
            AdsInitializer.IsAdFree = true;
            Debug.Log("[IAPManager] Ads removed permanently.");
        }
        // ===== 宝石 100 =====
        else if (productId == gems100ProductId)
        {
            try { SpecialTileSystem.AddGems(gems100Amount); } catch { }
            Debug.Log($"[IAPManager] Added {gems100Amount} gems.");
        }
        // ===== 宝石 500 =====
        else if (productId == gems500ProductId)
        {
            try { SpecialTileSystem.AddGems(gems500Amount); } catch { }
            Debug.Log($"[IAPManager] Added {gems500Amount} gems.");
        }
        // ===== 宝石 1200 =====
        else if (productId == gems1200ProductId)
        {
            try { SpecialTileSystem.AddGems(gems1200Amount); } catch { }
            Debug.Log($"[IAPManager] Added {gems1200Amount} gems.");
        }
        else
        {
            Debug.LogWarning($"[IAPManager] Unknown product: {productId}");
        }

        // 購入完了イベントを発火（UI更新用）
        try { OnPurchaseCompleted?.Invoke(); } catch { }

        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.LogWarning($"[IAPManager] Purchase failed: {product.definition.id} - {failureReason}");
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
        Debug.LogWarning($"[IAPManager] Purchase failed: {product.definition.id} - {failureDescription.reason} - {failureDescription.message}");
    }
}
