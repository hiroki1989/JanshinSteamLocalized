using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AppReviewRequest : MonoBehaviour
{
    static bool pending;
    const string Runs="AppReview.CompletedRuns", Last="AppReview.LastAttempt";
    public static void RewardAccepted()
    {
        if(pending)return;
        pending=true;PlayerPrefs.SetInt(Runs,PlayerPrefs.GetInt(Runs,0)+1);PlayerPrefs.Save();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        pending=false;SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;
    }
    static void Loaded(Scene scene,LoadSceneMode mode)
    {
        if(scene.name!="MenuScene"||!pending)return;
        pending=false;
        new GameObject("AppReviewRequest").AddComponent<AppReviewRequest>().StartCoroutine(Request());
    }
    static IEnumerator Request()
    {
        var settings=Resources.Load<AppReviewSettings>("AppReviewSettings");
        if(!settings||!settings.enabled||PlayerPrefs.GetInt(Runs,0)<settings.minimumCompletedRuns||PlayerPrefs.GetInt("AppReview.ManualOpened",0)!=0)yield break;
        if(long.TryParse(PlayerPrefs.GetString(Last,""),out var ticks)&&(DateTime.UtcNow-new DateTime(ticks,DateTimeKind.Utc)).TotalDays<settings.cooldownDays)yield break;
        if(UnityEngine.Random.value>=settings.probability)yield break;
        yield return new WaitForSecondsRealtime(settings.delaySeconds);
        var menu=UnityEngine.Object.FindAnyObjectByType<MenuController>();
        if(SceneManager.GetActiveScene().name!="MenuScene"||!menu||menu.IsBusyForAppReview)yield break;
        // Apple decides whether to display the dialog; no success/rating callback is available.
#if UNITY_IOS && !UNITY_EDITOR
        PlayerPrefs.SetString(Last,DateTime.UtcNow.Ticks.ToString());PlayerPrefs.Save();
        UnityEngine.iOS.Device.RequestStoreReview();
#else
        Debug.Log("[AppReview] Eligible post-reward menu return (native dialog is iOS-only).");
#endif
    }
}

public partial class MenuController
{
    public bool IsBusyForAppReview => _initialLanguageSelectionShowing || menuTutorial ||
        (skillUnlockPopupRoot && skillUnlockPopupRoot.activeInHierarchy);
}
