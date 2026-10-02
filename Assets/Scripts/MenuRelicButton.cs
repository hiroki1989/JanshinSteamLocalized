using TMPro;
using UnityEngine;

public sealed class MenuRelicButton : MonoBehaviour
{
    public void Open(){AudioManager.Instance?.PlayClickSE();SeventeenStepsInventory.Open();}
    void OnEnable(){Refresh();LocalizationManager.LanguageChanged+=Changed;}
    void OnDisable(){LocalizationManager.LanguageChanged-=Changed;}
    void Changed(LocalizationManager.Language language){Refresh();}
    void Refresh(){foreach(var text in GetComponentsInChildren<TMP_Text>(true))text.text=ConsumableWindow.T("遺物","Relics","遗物");}
}
