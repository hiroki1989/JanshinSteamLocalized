using TMPro;
using UnityEngine;
public static class MonetizationText
{
    public static string Get(string ja, string en, string zh) {
        var language = LocalizationManager.Instance.CurrentLanguage;
        return language == LocalizationManager.Language.English ? en :
            language == LocalizationManager.Language.ChineseSimplified ? zh : ja;
    }
    public static void Font(TMP_Text text) {
        if (!text) return;
        var font = TMP_Settings.defaultFontAsset;
        if (!font) font = LocalizationManager.Instance.GetBodyFont();
        if (font) text.font = font;
    }
    public static string Status(string key) {
        switch (key) {
            case "connecting": return Get("ストアに接続しています…","Connecting to the store…","正在连接商店…");
            case "purchasing": return Get("購入を処理しています…","Processing your purchase…","正在处理购买…");
            case "restoring": return Get("購入を復元しています…","Restoring purchases…","正在恢复购买…");
            case "restored": return Get("広告カットを復元しました。","Ad removal restored.","已恢复去广告。");
            case "restoreEmpty": return Get("復元できる広告カットの購入がありません。宝石は復元対象外です。","No ad-removal purchase to restore. Consumable gems are not restored.","没有可恢复的去广告购买。消耗型宝石不属于恢复对象。");
            case "restoreFailed": return Get("復元できませんでした。接続を確認して再度お試しください。","Restore failed. Check your connection and try again.","恢复失败。请检查网络后重试。");
            case "purchased": return Get("購入内容を反映しました。","Purchase delivered.","已发放购买内容。");
            case "cancelled": return Get("購入をキャンセルしました。","Purchase cancelled.","已取消购买。");
            case "deferred": return Get("購入の承認を待っています。承認後に反映されます。","Awaiting purchase approval. Items will arrive after approval.","正在等待购买批准。批准后将发放商品。");
            case "pending": return Get("購入内容の反映を保留しています。再起動後に再処理します。","Delivery is pending. It will be retried after restarting.","商品发放待处理。重启后将重试。");
            case "failed": return Get("購入できませんでした。接続を確認して再度お試しください。","Purchase failed. Check your connection and try again.","购买失败。请检查网络后重试。");
            case "configuration": return Get("ストアの準備中です。購入はまだ利用できません。","The store is being prepared. Purchases are not available yet.","商店准备中，暂时无法购买。");
            case "unsupported": return Get("購入はiOS版で利用できます。","Purchases are available on iOS.","购买功能仅限iOS版。");
            case "testStore": return Get("Editorテストストア：実際の請求は発生しません。","Editor test store: no real charges.","Editor测试商店：不会产生真实费用。");
            case "ready": return "";
            default: return Get("ストアに接続できません。再接続をお試しください。","Store unavailable. Please reconnect.","无法连接商店，请重新连接。");
        }
    }
}
