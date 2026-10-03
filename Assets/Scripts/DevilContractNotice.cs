using System.Collections;
using TMPro;
using UnityEngine;

public sealed class DevilContractNotice : MonoBehaviour
{
    public TMP_Text title,body; public CanvasGroup group;public RectTransform card;
    public IEnumerator Play(string heading,string message)
    {
        title.text=heading;body.text=message;
        var gm=Object.FindAnyObjectByType<GameManager>();
        try {AudioManager.Instance?.PlayCutin_PlayerSkill();}catch{}
        float t=0;while(t<.3f){t+=Time.unscaledDeltaTime;float p=Mathf.Clamp01(t/.3f);group.alpha=p;card.localScale=Vector3.one*Mathf.Lerp(.92f,1,p);yield return null;}
        yield return new WaitForSecondsRealtime(2.2f);
        t=0;while(t<.35f){t+=Time.unscaledDeltaTime;group.alpha=1-Mathf.Clamp01(t/.35f);yield return null;}
    }
}
