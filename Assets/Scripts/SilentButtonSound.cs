using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Complements silent controls after their existing handlers; never replaces events or clips.
[DefaultExecutionOrder(32700)]
public sealed class SilentButtonSound : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, ISubmitHandler
{
    Button button; float started; bool pending, eligible;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install(){var owner=new GameObject("SilentButtonSoundCoverage");DontDestroyOnLoad(owner);owner.AddComponent<SilentButtonSoundScanner>();}
    public void OnPointerDown(PointerEventData e){started=Time.unscaledTime;button=GetComponent<Button>();eligible=button&&button.IsActive()&&button.IsInteractable();}
    public void OnPointerClick(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)Queue();}
    public void OnSubmit(BaseEventData e){started=Time.unscaledTime;Queue();}
    void Queue(){button=GetComponent<Button>();pending=eligible||(button&&button.IsActive()&&button.IsInteractable());eligible=false;}
    void OnDestroy(){if(pending&&AudioManager.Instance&&AudioManager.LastUserSoundTime<started)AudioManager.Instance.PlaySE_Click();}
    void LateUpdate(){if(!pending)return;pending=false;if(AudioManager.LastUserSoundTime<started)AudioManager.Instance?.PlaySE_Click();}
}
public sealed class SilentButtonSoundScanner : MonoBehaviour
{
    float next;
    void Update(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+.15f;
        foreach(var button in FindObjectsByType<Button>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
            if(!button.GetComponent<SilentButtonSound>())button.gameObject.AddComponent<SilentButtonSound>();
    }
}
