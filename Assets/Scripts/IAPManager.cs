using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

// Compatibility API from the installed Unity IAP 5.4.2; iOS uses StoreKit 2.
public sealed class IAPManager : MonoBehaviour, IDetailedStoreListener
{
    public static IAPManager Instance { get; private set; }
    [SerializeField] string removeAdsProductId = "com.yourapp.removeads";
    [SerializeField] string gems100ProductId = "com.yourapp.gems_100";
    [SerializeField] string gems500ProductId = "com.yourapp.gems_500";
    [SerializeField] string gems1200ProductId = "com.yourapp.gems_1200";
    [SerializeField, Min(1)] int gems100Amount = 10;
    [SerializeField, Min(1)] int gems500Amount = 55;
    [SerializeField, Min(1)] int gems1200Amount = 130;
    IStoreController controller;
    IExtensionProvider extensions;
    bool initializing, busy, restoring;
    public event Action OnPurchaseCompleted;
    public static event Action StateChanged;
    public bool IsInitialized => controller != null;
    public bool IsBusy => busy || initializing || restoring;
    public string Status { get; private set; } = "connecting";
    public string RemoveAdsProductId => removeAdsProductId;
    public string Gems100ProductId => gems100ProductId;
    public string Gems500ProductId => gems500ProductId;
    public string Gems1200ProductId => gems1200ProductId;
    public int Gems100Amount => gems100Amount;
    public int Gems500Amount => gems500Amount;
    public int Gems1200Amount => gems1200Amount;
    public bool HasProductionIds => new[] {removeAdsProductId,gems100ProductId,gems500ProductId,gems1200ProductId}
        .All(id => !string.IsNullOrWhiteSpace(id) && !id.StartsWith("com.yourapp.")) &&
        new[] {removeAdsProductId,gems100ProductId,gems500ProductId,gems1200ProductId}.Distinct().Count() == 4;
    void Awake() {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
    }
    void Start() => InitializePurchasing();
    void UpdateState(string state) {
        Status = state;
        if (StateChanged != null) foreach (Action callback in StateChanged.GetInvocationList())
            try { callback(); } catch (Exception e) { Debug.LogException(e); }
    }
    public void InitializePurchasing() {
        if (initializing || IsInitialized) return;
        if (!Application.isEditor && Application.platform != RuntimePlatform.IPhonePlayer) { UpdateState("unsupported"); return; }
        if (!Application.isEditor && !HasProductionIds) { UpdateState("configuration"); return; }
        initializing = true; UpdateState("connecting");
        try {
            var module = StandardPurchasingModule.Instance();
#if UNITY_EDITOR
            module.useFakeStoreUIMode = FakeStoreUIMode.StandardUser;
#endif
            var builder = ConfigurationBuilder.Instance(module);
            builder.AddProduct(removeAdsProductId, ProductType.NonConsumable);
            builder.AddProduct(gems100ProductId, ProductType.Consumable);
            builder.AddProduct(gems500ProductId, ProductType.Consumable);
            builder.AddProduct(gems1200ProductId, ProductType.Consumable);
            UnityPurchasing.Initialize(this, builder);
        } catch (Exception e) { initializing = false; UpdateState("unavailable"); Debug.LogException(e); }
    }
    public bool CanBuy(string id) => !IsBusy && IsInitialized && controller.products.WithID(id)?.availableToPurchase == true &&
        (id != removeAdsProductId || !AdsInitializer.IsAdFree);
    public void BuyRemoveAds() => Buy(removeAdsProductId);
    public void BuyGems100() => Buy(gems100ProductId);
    public void BuyGems500() => Buy(gems500ProductId);
    public void BuyGems1200() => Buy(gems1200ProductId);
    void Buy(string id) {
        if (!CanBuy(id)) { if (!IsBusy) UpdateState("unavailable"); return; }
        busy = true; UpdateState("purchasing");
        try { controller.InitiatePurchase(id); }
        catch (Exception e) { busy = false; UpdateState("failed"); Debug.LogException(e); }
    }
    // Planned Japanese prices; App Store metadata remains authoritative in a live store.
    public string GetLocalizedPrice(string id, string fallback = "—") {
#if !UNITY_EDITOR
        if (IsInitialized && controller.products.WithID(id)?.availableToPurchase == true)
            return controller.products.WithID(id).metadata.isoCurrencyCode == "JPY" ? controller.products.WithID(id).metadata.localizedPrice.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "円" : controller.products.WithID(id).metadata.localizedPriceString;
#endif
        if (id == removeAdsProductId) return "800円";
        if (id == gems100ProductId) return "100円";
        if (id == gems500ProductId) return "400円";
        if (id == gems1200ProductId) return "900円";
        return fallback;
    }
    public bool IsRemoveAdsPurchased() => AdsInitializer.IsAdFree;
    public void RestorePurchases(Action<bool> onComplete = null) {
        if (!IsInitialized || IsBusy) { onComplete?.Invoke(false); return; }
        restoring = true; UpdateState("restoring");
#if UNITY_IOS && !UNITY_EDITOR
        try {
            extensions.GetExtension<IAppleExtensions>().RestoreTransactions((success, error) => {
                restoring = false;
                UpdateState(success ? (AdsInitializer.IsAdFree ? "restored" : "restoreEmpty") : "restoreFailed");
                onComplete?.Invoke(success);
            });
        } catch (Exception e) { restoring = false; UpdateState("restoreFailed"); Debug.LogException(e); onComplete?.Invoke(false); }
#else
        restoring = false; UpdateState(AdsInitializer.IsAdFree ? "restored" : "restoreEmpty"); onComplete?.Invoke(true);
#endif
    }
    public void OnInitialized(IStoreController store, IExtensionProvider provider) {
        controller = store; extensions = provider; initializing = false;
        var item = store.products.WithID(removeAdsProductId);
        if (item != null && item.hasReceipt) AdsInitializer.IsAdFree = true;
#if UNITY_IOS && !UNITY_EDITOR
        provider.GetExtension<IAppleExtensions>().RegisterPurchaseDeferredListener(_ => {
            busy = false; UpdateState("deferred");
        });
#endif
        UpdateState(Application.isEditor ? "testStore" : "ready");
    }
    public void OnInitializeFailed(InitializationFailureReason reason) => OnInitializeFailed(reason, "");
    public void OnInitializeFailed(InitializationFailureReason reason, string message) {
        initializing = false; UpdateState("unavailable"); Debug.LogWarning("[IAP] " + reason + ": " + message);
    }
    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args) {
        var product = args.purchasedProduct;
        int amount = product.definition.id == gems100ProductId ? gems100Amount :
            product.definition.id == gems500ProductId ? gems500Amount :
            product.definition.id == gems1200ProductId ? gems1200Amount : 0;
        try {
            if (product.definition.id == removeAdsProductId) AdsInitializer.IsAdFree = true;
            else if (amount > 0 && !string.IsNullOrWhiteSpace(product.transactionID))
                GemWallet.GrantPurchase(product.definition.id + ":" + product.transactionID, amount);
            else { busy = false; UpdateState("pending"); return PurchaseProcessingResult.Pending; }
        } catch (Exception e) {
            busy = false; UpdateState("pending"); Debug.LogException(e); return PurchaseProcessingResult.Pending;
        }
        busy = false; UpdateState(restoring ? "restoring" : "purchased");
        if (OnPurchaseCompleted != null) foreach (Action callback in OnPurchaseCompleted.GetInvocationList())
            try { callback(); } catch (Exception e) { Debug.LogException(e); }
        return PurchaseProcessingResult.Complete;
    }
    public void OnPurchaseFailed(Product product, PurchaseFailureReason reason) {
        busy = false; UpdateState(reason == PurchaseFailureReason.UserCancelled ? "cancelled" : "failed");
    }
    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failure) => OnPurchaseFailed(product, failure.reason);
    void OnDestroy() { if (Instance == this) Instance = null; }
}
