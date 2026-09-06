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
            ? (en ? "The tutorial will appear at the start of your next new match."
                : zh ? "下次开始新对局时将显示教程。" : "次に新しい対局を開始すると、チュートリアルが表示されます。")
            : (en ? "Reset the tutorial so it appears in your next new match."
                : zh ? "重置教程，下次开始新对局时再次显示。" : "初回フラグをリセットし、次の新しい対局で説明を表示します。");
        var font = TMP_Settings.defaultFontAsset;
        if (!font) font = LocalizationManager.Instance.GetBodyFont();
        if (font)
        {
            if (buttonLabel) buttonLabel.font = font;
            if (statusLabel) statusLabel.font = font;
        }
    }
}
