using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Resets only the first-match tutorial preference; match progress is untouched.</summary>
public sealed class TutorialResetOption : MonoBehaviour
{
    [SerializeField] private Button resetButton;
    [SerializeField] private TMP_Text buttonLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private string completionKey = "FirstMatchTutorialDoneV1";
    private bool resetRequested;

    private void OnEnable()
    {
        if (resetButton)
        {
            resetButton.onClick.RemoveListener(ResetTutorial);
            resetButton.onClick.AddListener(ResetTutorial);
        }
        LocalizationManager.LanguageChanged += Refresh;
        Refresh(LocalizationManager.Instance.CurrentLanguage);
    }

    private void OnDisable()
    {
        if (resetButton) resetButton.onClick.RemoveListener(ResetTutorial);
        LocalizationManager.LanguageChanged -= Refresh;
    }

    public void ResetTutorial()
    {
        PlayerPrefs.DeleteKey(string.IsNullOrWhiteSpace(completionKey) ? "FirstMatchTutorialDoneV1" : completionKey);
        PlayerPrefs.DeleteKey(MenuController.MenuTutorialDoneKey);
        PlayerPrefs.Save();
        resetRequested = true;
        Refresh(LocalizationManager.Instance.CurrentLanguage);
    }

    public void Refresh(LocalizationManager.Language language)
    {
        bool en = language == LocalizationManager.Language.English;
        bool zh = language == LocalizationManager.Language.ChineseSimplified;
        if (buttonLabel) buttonLabel.text = en ? "Show tutorial again" : zh ? "重新显示教程" : "チュートリアルを再表示";
        if (statusLabel) statusLabel.text = resetRequested
            ? (en ? "The guides will appear when you return to the menu and start a new match."
                : zh ? "返回菜单及开始新对局时，将再次显示教程。" : "メニューに戻ったときと、新しい対局で説明を再表示します。")
            : (en ? "Reset both the menu and match tutorials."
                : zh ? "重置菜单与对局教程。" : "メニューと対局のチュートリアルを再表示します。");
        var font = TMP_Settings.defaultFontAsset;
        if (!font) font = LocalizationManager.Instance.GetBodyFont();
        if (font)
        {
            if (buttonLabel) buttonLabel.font = font;
            if (statusLabel) statusLabel.font = font;
        }
    }
}
