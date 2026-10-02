using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Handles text refreshed by inventory and localized UI; keeps colored semantics.
[DefaultExecutionOrder(32000)]
public sealed class JanshinThemeTextScope : MonoBehaviour
{
    TMP_Text[] labels;
    float nextScan;
    void LateUpdate()
    {
        if(labels==null||Time.unscaledTime>=nextScan){
            labels=GetComponentsInChildren<TMP_Text>(true);nextScan=Time.unscaledTime+.5f;
            foreach(var button in GetComponentsInChildren<Button>(true)){
                if(button.GetComponent<JanshinPanelTheme>()||JanshinPanelTheme.IsTileControl(button))continue;
                var image=button.GetComponent<Image>();
                if(image&&image.sprite&&System.Text.RegularExpressions.Regex.IsMatch(image.sprite.name,@"^(Man|Pin|Sou|man|pin|sou)[1-9]"))continue;
                JanshinPanelTheme.Button(button);
            }
        }
        foreach(var text in labels)if(text)JanshinPanelTheme.Text(text);
    }
}
