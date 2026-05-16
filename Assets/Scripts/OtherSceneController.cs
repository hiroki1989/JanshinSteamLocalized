using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public sealed class OtherSceneController : MonoBehaviour
{
    [Header("Home Root (OtherSceneに入った直後に表示するルート)")]
    [SerializeField] private GameObject homeRoot;

    [Header("Home Buttons")]
    [SerializeField] private Button optionButton;
    [SerializeField] private Button guideButton;
    [SerializeField] private Button achievementsButton;
    [SerializeField] private Button backToPreviousSceneButton;
    [SerializeField] private Button quitApplicationButton;
    [Header("Option Root")]
    [SerializeField] private GameObject optionRoot;
    [SerializeField] private Button optionBackToHomeButton;

    [Header("Guide Root")]
    [SerializeField] private GameObject guideRoot;
    [SerializeField] private Button guideBackToHomeButton;

    [Header("Achievements Root")]
    [SerializeField] private GameObject achievementRoot;
    [SerializeField] private Button achievementBackToHomeButton;
    [SerializeField] private AchievementPanelController achievementPanel;

    [Header("Achievement Reward Notice")]
    [SerializeField] private GameObject homeAchievementRewardNoticeIcon;

    [Header("Guide Unread Notice")]
    [SerializeField] private GameObject homeGuideUnreadNoticeIcon;

    [Header("Option - Fullscreen Toggle")]
    [SerializeField] private Toggle fullscreenToggle;
    [Header("Option - Audio Sliders (0-100)")]
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider seVolumeSlider;

    [Header("Option - Language")]
    [SerializeField] private TMP_Dropdown languageDropdown;
    private bool _ignoreLanguageDropdownCallback = false;

    [Header("Force Home On Enter")]
    [SerializeField] private bool forceShowHomeOnEnable = true;
    [SerializeField] private bool forceShowHomeOnStart = true;
    [SerializeField] private bool forceShowHomeNextFrame = true;
    private const string PF_FULLSCREEN = "PF_Option_Fullscreen";

private void Awake()
{
    AutoAssignRootsIfMissing();
    WireButtons();

    ShowHome();
    RefreshHomeAchievementRewardNoticeIcon();
    RefreshHomeGuideUnreadNoticeIcon();

    // Fullscreenトグル初期化（オプションを開く前でも状態だけ合わせる）
    bool isFullscreen = LoadFullscreenOption_DefaultTrue();
    // ★重要：
    // シーンに入っただけでは Window サイズを再設定しない。
    // ユーザーが自分で変更した Window サイズはそのまま維持する。
    ApplyFullscreen(isFullscreen, applyResolutionWhenWindowed: false);

    if (fullscreenToggle)
    {
        fullscreenToggle.onValueChanged.RemoveAllListeners();
        fullscreenToggle.isOn = isFullscreen;
        fullscreenToggle.onValueChanged.AddListener(OnChangeFullscreenToggle);
    }

    SetupAudioSliders();
    SetupLanguageDropdown();
}
    private void OnEnable()
    {
        if (!forceShowHomeOnEnable) return;

        AutoAssignRootsIfMissing();
        ShowHome();
        RefreshHomeAchievementRewardNoticeIcon();
        RefreshHomeGuideUnreadNoticeIcon();
    }
    private void Start()
    {
        if (forceShowHomeOnStart)
        {
            AutoAssignRootsIfMissing();
            ShowHome();
            RefreshHomeAchievementRewardNoticeIcon();
            RefreshHomeGuideUnreadNoticeIcon();
        }

        if (forceShowHomeNextFrame)
        {
            StartCoroutine(ForceHomeNextFrame());
        }
    }
    private IEnumerator ForceHomeNextFrame()
    {
        yield return null; // 1フレーム待つ（他のStartで上書きされても戻す）
        AutoAssignRootsIfMissing();
        ShowHome();
        RefreshHomeAchievementRewardNoticeIcon();
        RefreshHomeGuideUnreadNoticeIcon();
    }
    private void WireButtons()
    {
        if (optionButton)
        {
            optionButton.onClick.RemoveAllListeners();
            optionButton.onClick.AddListener(OpenOption);
        }

        if (guideButton)
        {
            guideButton.onClick.RemoveAllListeners();
            guideButton.onClick.AddListener(OpenGuide);
        }

        if (achievementsButton)
        {
            achievementsButton.onClick.RemoveAllListeners();
            achievementsButton.onClick.AddListener(OpenAchievements);
        }

        if (backToPreviousSceneButton)
        {
            backToPreviousSceneButton.onClick.RemoveAllListeners();
            backToPreviousSceneButton.onClick.AddListener(BackToPreviousScene);
        }

        if (quitApplicationButton)
        {
            quitApplicationButton.onClick.RemoveAllListeners();
            quitApplicationButton.onClick.AddListener(QuitApplication);
        }

        if (optionBackToHomeButton)
        {
            optionBackToHomeButton.onClick.RemoveAllListeners();
            optionBackToHomeButton.onClick.AddListener(ShowHome);
        }

        if (guideBackToHomeButton)
        {
            guideBackToHomeButton.onClick.RemoveAllListeners();
            guideBackToHomeButton.onClick.AddListener(ShowHome);
        }

        if (achievementBackToHomeButton)
        {
            achievementBackToHomeButton.onClick.RemoveAllListeners();
            achievementBackToHomeButton.onClick.AddListener(ShowHome);
        }
    }

    private void AutoAssignRootsIfMissing()
    {
        // Inspector の紐づけがミスってても動くように、名前で補完する
        // ルート名はこの名前にしておくのが確実：
        // HomeRoot / OptionRoot / GuideRoot / AchievementRoot

        if (!homeRoot)
        {
            var go = GameObject.Find("HomeRoot");
            if (go) homeRoot = go;
        }

        if (!optionRoot)
        {
            var go = GameObject.Find("OptionRoot");
            if (go) optionRoot = go;
        }

        if (!guideRoot)
        {
            var go = GameObject.Find("GuideRoot");
            if (go) guideRoot = go;
        }

        if (!achievementRoot)
        {
            var go = GameObject.Find("AchievementRoot");
            if (go) achievementRoot = go;
        }

        if (!achievementPanel && achievementRoot)
        {
            achievementPanel = achievementRoot.GetComponentInChildren<AchievementPanelController>(true);
        }
    }

    private void RefreshHomeAchievementRewardNoticeIcon()
    {
        if (!homeAchievementRewardNoticeIcon) return;

        try { AchievementSystem.ReconcileFromSaveData(); } catch { }

        bool show = false;
        try
        {
            show = AchievementSystem.HasUnclaimedReadyReward();
        }
        catch
        {
            show = false;
        }

        if (homeAchievementRewardNoticeIcon.activeSelf != show)
        {
            homeAchievementRewardNoticeIcon.SetActive(show);
        }
    }

    private void RefreshHomeGuideUnreadNoticeIcon()
    {
        if (!homeGuideUnreadNoticeIcon) return;

        bool show = true;
        try
        {
            show = PlayerData.HasAnyUnreadGuide();
        }
        catch
        {
            show = true;
        }

        if (homeGuideUnreadNoticeIcon.activeSelf != show)
        {
            homeGuideUnreadNoticeIcon.SetActive(show);
        }
    }
    private void ShowHome()
    {
        if (homeRoot) homeRoot.SetActive(true);
        if (optionRoot) optionRoot.SetActive(false);
        if (guideRoot) guideRoot.SetActive(false);
        if (achievementRoot) achievementRoot.SetActive(false);

        RefreshHomeAchievementRewardNoticeIcon();
        RefreshHomeGuideUnreadNoticeIcon();
    }
    private void OpenOption()
    {
        if (homeRoot) homeRoot.SetActive(false);
        if (optionRoot) optionRoot.SetActive(true);
        if (guideRoot) guideRoot.SetActive(false);
        if (achievementRoot) achievementRoot.SetActive(false);

        SetupAudioSliders();
        SetupLanguageDropdown();
    }
        private void SetupLanguageDropdown()
    {
        if (languageDropdown == null) return;

        var loc = LocalizationManager.Instance;
        if (loc == null) return;

        _ignoreLanguageDropdownCallback = true;

        languageDropdown.onValueChanged.RemoveAllListeners();
        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(new System.Collections.Generic.List<string>
        {
            "日本語",
            "English",
            "简体中文"
        });

        languageDropdown.SetValueWithoutNotify(loc.GetDropdownIndexForCurrentLanguage());
        languageDropdown.onValueChanged.AddListener(OnChangeLanguageDropdown);

        _ignoreLanguageDropdownCallback = false;
    }
private void OnChangeLanguageDropdown(int index)
{
    if (_ignoreLanguageDropdownCallback) return;

    var loc = LocalizationManager.Instance;
    if (loc == null) return;

    loc.SetLanguage(loc.GetLanguageFromDropdownIndex(index));

    try
    {
        var localizedTexts = FindObjectsOfType<LocalizedTextUI>(true);
        if (localizedTexts != null)
        {
            foreach (var x in localizedTexts)
            {
                if (x != null) x.RefreshNow();
            }
        }
    }
    catch { }

    try
    {
        Canvas.ForceUpdateCanvases();
    }
    catch { }
    try
    {
        var panels = FindObjectsOfType<AchievementPanelController>(true);
        if (panels != null)
        {
            foreach (var p in panels)
            {
                if (p != null) p.RefreshAll();
            }
        }
    }
    catch { }

    SetupLanguageDropdown();
}
    private void OpenGuide()
    {
        if (homeRoot) homeRoot.SetActive(false);
        if (optionRoot) optionRoot.SetActive(false);
        if (guideRoot) guideRoot.SetActive(true);
        if (achievementRoot) achievementRoot.SetActive(false);
    }

    private void OpenAchievements()
    {
        if (homeRoot) homeRoot.SetActive(false);
        if (optionRoot) optionRoot.SetActive(false);
        if (guideRoot) guideRoot.SetActive(false);
        if (achievementRoot) achievementRoot.SetActive(true);

        try { AchievementSystem.ReconcileFromSaveData(); } catch { }
        try { if (achievementPanel) achievementPanel.RefreshAll(); } catch { }
        RefreshHomeAchievementRewardNoticeIcon();
    }
    private void BackToPreviousScene()
    {
        string returnScene = "RunScene";
        try { returnScene = PlayerPrefs.GetString("PF_ReturnSceneFromOther", "RunScene"); } catch { returnScene = "RunScene"; }

        if (string.IsNullOrEmpty(returnScene)) returnScene = "RunScene";

        try { UnityEngine.SceneManagement.SceneManager.LoadScene(returnScene); } catch { }
    }

    private void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    private void OnChangeFullscreenToggle(bool on)
    {
        SaveFullscreenOption(on);
        ApplyFullscreen(on, applyResolutionWhenWindowed: true);
    }

    private void SetupAudioSliders()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        if (bgmVolumeSlider)
        {
            bgmVolumeSlider.minValue = 0f;
            bgmVolumeSlider.maxValue = 100f;
            bgmVolumeSlider.wholeNumbers = true;
            bgmVolumeSlider.onValueChanged.RemoveAllListeners();
            bgmVolumeSlider.SetValueWithoutNotify(am.GetBgmVolume100());
            bgmVolumeSlider.onValueChanged.AddListener(OnChangeBgmSlider);
        }

        if (seVolumeSlider)
        {
            seVolumeSlider.minValue = 0f;
            seVolumeSlider.maxValue = 100f;
            seVolumeSlider.wholeNumbers = true;
            seVolumeSlider.onValueChanged.RemoveAllListeners();
            seVolumeSlider.SetValueWithoutNotify(am.GetSeVolume100());
            seVolumeSlider.onValueChanged.AddListener(OnChangeSeSlider);
        }
    }
    private void OnChangeBgmSlider(float value)
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        am.SetBgmVolume100(value, true);
    }

    private void OnChangeSeSlider(float value)
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        am.SetSeVolume100(value, true);
    }

    private bool LoadFullscreenOption_DefaultTrue()
    {
        try
        {
            int v = PlayerPrefs.GetInt(PF_FULLSCREEN, 1);
            return v != 0;
        }
        catch
        {
            return true;
        }
    }
    private void SaveFullscreenOption(bool isFullscreen)
    {
        try
        {
            PlayerPrefs.SetInt(PF_FULLSCREEN, isFullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }
        catch { }
    }
private void ApplyFullscreen(bool isFullscreen, bool applyResolutionWhenWindowed)
{
    try
    {
        if (isFullscreen)
        {
            int desktopW = Screen.currentResolution.width;
            int desktopH = Screen.currentResolution.height;

            Screen.SetResolution(desktopW, desktopH, FullScreenMode.FullScreenWindow);
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.fullScreen = true;
        }
        else
        {
            Screen.fullScreen = false;
            Screen.fullScreenMode = FullScreenMode.Windowed;

            if (applyResolutionWhenWindowed)
            {
                Vector2Int size = new Vector2Int(Screen.width, Screen.height);

                if (size.x <= 0 || size.y <= 0)
                {
                    size = new Vector2Int(1280, 720);
                }

                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
            }
        }
    }
    catch { }
}
private Vector2Int CalcBestWindowedSize(int aspectW, int aspectH, int marginW, int marginH, int minW, int minH)
{
    int desktopW = 1280;
    int desktopH = 720;

    try { desktopW = Mathf.Max(1, Screen.currentResolution.width - marginW); } catch { }
    try { desktopH = Mathf.Max(1, Screen.currentResolution.height - marginH); } catch { }

    float targetAspect = (float)aspectW / (float)aspectH;

    int w = desktopW;
    int h = Mathf.RoundToInt(w / targetAspect);

    if (h > desktopH)
    {
        h = desktopH;
        w = Mathf.RoundToInt(h * targetAspect);
    }

    w = Mathf.Max(1, w);
    h = Mathf.Max(1, h);

    return new Vector2Int(w, h);
}
}