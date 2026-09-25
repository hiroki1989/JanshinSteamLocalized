using UnityEngine;
[CreateAssetMenu(menuName="Janshin/App Review Settings")]
public sealed class AppReviewSettings : ScriptableObject
{
    [Header("報酬受け取り後、メニュー帰還時の評価依頼")]
    public bool enabled = true;
    [Tooltip("この回数のランを終了するまでは依頼しません。勝敗は問いません。")]
    [Min(1)] public int minimumCompletedRuns = 2;
    [Tooltip("条件を満たしたメニュー帰還時の抽選確率。0.25 は25％です。")]
    [Range(0,1)] public float probability = .25f;
    [Tooltip("前回の依頼から最低限空ける日数。Appleが表示しなかった場合も間隔を空けます。")]
    [Min(30)] public int cooldownDays = 90;
    [Min(0)] public float delaySeconds = 4;
    [Tooltip("Manual review link. The native rating dialog identifies the app by its bundle ID.")]
    public string appStoreUrl = "https://apps.apple.com/app/id6809131087";
    // May also be bound to an explicit, user-operated review button in the Inspector.
    [ContextMenu("設定したApp Storeの評価ページを開く")]
    public void OpenReviewPage()
    {
        if(!System.Uri.TryCreate(appStoreUrl,System.UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="apps.apple.com")return;
        PlayerPrefs.SetInt("AppReview.ManualOpened",1);PlayerPrefs.Save();
        Application.OpenURL(appStoreUrl+(appStoreUrl.Contains("?")?"&":"?")+"action=write-review");
    }
}
