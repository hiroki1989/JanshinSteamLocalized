using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
// Menu -> AngelDialogue -> EnemyDialogue -> Battle
public class MenuController : MonoBehaviour
{
[SerializeField] private string tierSelectSceneName = "TierSelectScene";
[SerializeField] private string angelDialogueScene = "AngelDialogue";
[SerializeField] private string enemyDialogueScene = "EnemyDialogue";
[SerializeField] private string battleSceneName    = "RunScene";

    [SerializeField] private string skillSetSceneName = "SkillSetScene"; // ★ 追加
    [SerializeField] private string shopSceneName  = "ShopScene";
    [SerializeField] private string equipSceneName = "EquipScene";
    [SerializeField] private string specialTileSceneName = "SpecialTileScene"; // ★ 追加：特別牌シーン

    [SerializeField] private string otherSceneName = "OtherScene"; // ★ 追加：その他シーン

    // ★追加：ハイスコア表示
    [SerializeField] private TextMeshProUGUI highScoreTMP;
    // Optional: show current enemy name on menu
    [SerializeField] private TextMeshProUGUI currentEnemyTMP;

    // ★追加：宝石所持数（数値のみ）
    [SerializeField] private TextMeshProUGUI gemCountTMP;

    [Header("Achievement Reward Notice")]
    [SerializeField] private GameObject achievementRewardNoticeIcon;

    [Header("Guide Unread Notice")]
    [SerializeField] private GameObject guideUnreadNoticeIcon;
    [Header("Skill Unlock Popup")]
    [SerializeField] private GameObject skillUnlockPopupRoot;
    [SerializeField] private CanvasGroup skillUnlockPopupCanvasGroup;
    [SerializeField] private TextMeshProUGUI skillUnlockPopupTMP;
    [SerializeField] private Button skillUnlockPopupOkButton;

    [Header("Initial Language Selection")]
    [SerializeField] private GameObject initialLanguagePanelRoot;
    [SerializeField] private CanvasGroup initialLanguagePanelCanvasGroup;
    [SerializeField] private Button initialLanguageJapaneseButton;
    [SerializeField] private Button initialLanguageEnglishButton;
    [SerializeField] private Button initialLanguageChineseSimplifiedButton;

    [Header("Demo Wishlist / Full Game Link")]
    [SerializeField] private Button demoWishlistButton;
    [SerializeField] private TextMeshProUGUI demoWishlistButtonTMP;
    [SerializeField] private uint fullGameSteamAppId = 0;
    [SerializeField] private string fullGameStoreUrl = "";

    private bool _initialLanguageSelectionShowing = false;
private void Awake()
{
    // 戻ってきた時に UI 入力が止まらないように強制リセット
    Time.timeScale = 1f;

    ApplySavedDisplayMode(applyResolutionWhenWindowed: false);

    EnsureSingleEventSystem();      // ★ 競合EventSystemを排除
    EnsureInputModuleEnabled();     // ★ InputModuleを有効化
    EnableAllButtonsInThisCanvas(); // ★ ボタンのinteractableを強制復旧
    ClearPotentialUIBlockers();     // ★ 画面を覆う余計なRaycastターゲットを無効化
    SetupInitialLanguageSelectionPanelUI();
}
private void Start()
{
    ApplySavedDisplayMode(applyResolutionWhenWindowed: false);

    RefreshCurrentEnemyUI();
    RefreshHighScoreUI();
    RefreshGemCountUI();
    RefreshAchievementRewardNoticeIcon();
    RefreshGuideUnreadNoticeIcon();
    RefreshDemoWishlistButtonUI_Local();

    if (TryShowInitialLanguageSelectionPanel())
    {
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        return;
    }

    TryShowPendingSkillUnlockPopup();

    // 以降は元の処理（選択解除など）
    if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
}
private static string LocalizeUnlockedSkillName_Local(string skillKey)
{
    var lang = LocalizationManager.Language.Japanese;
    try
    {
        if (LocalizationManager.Instance != null)
            lang = LocalizationManager.Instance.CurrentLanguage;
    }
    catch { }

    switch (skillKey)
    {
        case "RandomMan":
            switch (lang)
            {
                case LocalizationManager.Language.English: return "Dye Master";
                case LocalizationManager.Language.ChineseSimplified: return "染色师";
                default: return "染色師";
            }

        case "EnhanceHand":
            switch (lang)
            {
                case LocalizationManager.Language.English: return "Calligrapher";
                case LocalizationManager.Language.ChineseSimplified: return "书家";
                default: return "書家";
            }

        case "Capitalist":
            switch (lang)
            {
                case LocalizationManager.Language.English: return "Capitalist";
                case LocalizationManager.Language.ChineseSimplified: return "资本家";
                default: return "資産家";
            }
    }
    return skillKey ?? "";
}

private static LocalizationManager.Language GetMenuLanguage_Local()
{
    try
    {
        if (LocalizationManager.Instance != null)
            return LocalizationManager.Instance.CurrentLanguage;
    }
    catch
    {
    }

    return LocalizationManager.Language.Japanese;
}

private void SetupInitialLanguageSelectionPanelUI()
{
    HideInitialLanguageSelectionPanel();

    if (initialLanguageJapaneseButton)
    {
        initialLanguageJapaneseButton.onClick.RemoveAllListeners();
        initialLanguageJapaneseButton.onClick.AddListener(OnClickSelectInitialLanguageJapanese);
    }

    if (initialLanguageEnglishButton)
    {
        initialLanguageEnglishButton.onClick.RemoveAllListeners();
        initialLanguageEnglishButton.onClick.AddListener(OnClickSelectInitialLanguageEnglish);
    }

    if (initialLanguageChineseSimplifiedButton)
    {
        initialLanguageChineseSimplifiedButton.onClick.RemoveAllListeners();
        initialLanguageChineseSimplifiedButton.onClick.AddListener(OnClickSelectInitialLanguageChineseSimplified);
    }
}

private bool TryShowInitialLanguageSelectionPanel()
{
    if (PlayerData.HasCompletedInitialLanguageSelection())
    {
        HideInitialLanguageSelectionPanel();
        return false;
    }

    if (initialLanguagePanelRoot == null ||
        initialLanguageJapaneseButton == null ||
        initialLanguageEnglishButton == null ||
        initialLanguageChineseSimplifiedButton == null)
    {
        return false;
    }

    ShowInitialLanguageSelectionPanel();
    return true;
}

private void ShowInitialLanguageSelectionPanel()
{
    _initialLanguageSelectionShowing = true;

    if (skillUnlockPopupRoot)
        skillUnlockPopupRoot.SetActive(false);

    if (initialLanguagePanelRoot)
        initialLanguagePanelRoot.SetActive(true);

    if (initialLanguagePanelCanvasGroup)
    {
        initialLanguagePanelCanvasGroup.alpha = 1f;
        initialLanguagePanelCanvasGroup.interactable = true;
        initialLanguagePanelCanvasGroup.blocksRaycasts = true;
    }

    if (EventSystem.current)
        EventSystem.current.SetSelectedGameObject(null);
}

private void HideInitialLanguageSelectionPanel()
{
    _initialLanguageSelectionShowing = false;

    if (initialLanguagePanelRoot)
        initialLanguagePanelRoot.SetActive(false);

    if (initialLanguagePanelCanvasGroup)
    {
        initialLanguagePanelCanvasGroup.alpha = 0f;
        initialLanguagePanelCanvasGroup.interactable = false;
        initialLanguagePanelCanvasGroup.blocksRaycasts = false;
    }
}

private void OnClickSelectInitialLanguageJapanese()
{
    ApplyInitialLanguageSelection(LocalizationManager.Language.Japanese);
}

private void OnClickSelectInitialLanguageEnglish()
{
    ApplyInitialLanguageSelection(LocalizationManager.Language.English);
}

private void OnClickSelectInitialLanguageChineseSimplified()
{
    ApplyInitialLanguageSelection(LocalizationManager.Language.ChineseSimplified);
}

private void ApplyInitialLanguageSelection(LocalizationManager.Language language)
{
    var loc = LocalizationManager.Instance;
    if (loc != null)
    {
        loc.SetLanguage(language);
    }

    PlayerData.MarkInitialLanguageSelectionCompleted();
    RefreshMenuTextsAfterLanguageChange();
    HideInitialLanguageSelectionPanel();
    TryShowPendingSkillUnlockPopup();
}

private void RefreshMenuTextsAfterLanguageChange()
{
    RefreshCurrentEnemyUI();
    RefreshHighScoreUI();
    RefreshGemCountUI();
    RefreshAchievementRewardNoticeIcon();
    RefreshGuideUnreadNoticeIcon();
    RefreshDemoWishlistButtonUI_Local();

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
    catch
    {
    }

    try
    {
        Canvas.ForceUpdateCanvases();
    }
    catch
    {
    }

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
    catch
    {
    }
}
private void RefreshCurrentEnemyUI()
{
    if (!currentEnemyTMP)
        return;

    int idxAbs = GameManager.GetCurrentEnemyIndex();
    string shown = "";

    if (EnemyConfigExcel.TryGetForRuntimeIndex(idxAbs, out var cfg) && !string.IsNullOrEmpty(cfg.name))
    {
        int tier = 1;
        int lv = 1;
        float mult = 1f;

        try { tier = GameManager.GetCurrentTier(); } catch { tier = 1; }
        try { lv = GameManager.GetCurrentGlobalLevelNumber(); } catch { lv = idxAbs + 1; }
        try { mult = GameManager.GetCurrentTierMultiplier(); } catch { mult = 1f; }

        string enemyName = cfg.name;

        try
        {
            var lm = LocalizationManager.Instance;
            if (lm != null)
            {
                string localizedEnemyName = lm.GetEnemyDisplayName(cfg.name);
                if (!string.IsNullOrEmpty(localizedEnemyName))
                    enemyName = localizedEnemyName;
            }
        }
        catch
        {
        }

        switch (GetMenuLanguage_Local())
        {
            case LocalizationManager.Language.English:
                shown = $"Next Opponent: {enemyName} (Tier {tier} Lv {lv}, Multiplier {mult:0.0}x)";
                break;

            case LocalizationManager.Language.ChineseSimplified:
                shown = $"下一位对手：{enemyName}（Tier{tier} Lv{lv}，倍率 {mult:0.0}x）";
                break;

            default:
                shown = $"次の対戦相手：{enemyName}  (Tier{tier} Lv{lv}, 倍率 {mult:0.0}x)";
                break;
        }
    }

    currentEnemyTMP.text = shown;
}

private void RefreshHighScoreUI()
{
    try
    {
        int hi = PlayerPrefs.GetInt("HighScore", 0);

        switch (GetMenuLanguage_Local())
        {
            case LocalizationManager.Language.English:
                if (highScoreTMP) highScoreTMP.text = $"High Score: {hi:N0}";
                break;

            case LocalizationManager.Language.ChineseSimplified:
                if (highScoreTMP) highScoreTMP.text = $"最高分：{hi:N0}";
                break;

            default:
                if (highScoreTMP) highScoreTMP.text = $"ハイスコア：{hi:N0}";
                break;
        }
    }
    catch
    {
        switch (GetMenuLanguage_Local())
        {
            case LocalizationManager.Language.English:
                if (highScoreTMP) highScoreTMP.text = "High Score: 0";
                break;

            case LocalizationManager.Language.ChineseSimplified:
                if (highScoreTMP) highScoreTMP.text = "最高分：0";
                break;

            default:
                if (highScoreTMP) highScoreTMP.text = "ハイスコア：0";
                break;
        }
    }
}
private static string BuildSkillUnlockedMessage_Local(string skillKey)
{
    string name = LocalizeUnlockedSkillName_Local(skillKey);

    var lang = LocalizationManager.Language.Japanese;
    try
    {
        if (LocalizationManager.Instance != null)
            lang = LocalizationManager.Instance.CurrentLanguage;
    }
    catch { }

    switch (lang)
    {
        case LocalizationManager.Language.English:
            return $"{name} has been unlocked.";

        case LocalizationManager.Language.ChineseSimplified:
            return $"已解锁{name}。";

        default:
            return $"{name}が解放されました";
    }
}

private void HideSkillUnlockPopup()
{
    if (skillUnlockPopupRoot)
        skillUnlockPopupRoot.SetActive(false);

    if (skillUnlockPopupCanvasGroup)
    {
        skillUnlockPopupCanvasGroup.alpha = 0f;
        skillUnlockPopupCanvasGroup.interactable = false;
        skillUnlockPopupCanvasGroup.blocksRaycasts = false;
    }

    if (skillUnlockPopupOkButton)
        skillUnlockPopupOkButton.onClick.RemoveAllListeners();

    TryShowPendingSkillUnlockPopup();
}

private void TryShowPendingSkillUnlockPopup()
{
    if (skillUnlockPopupRoot == null || skillUnlockPopupTMP == null || skillUnlockPopupOkButton == null)
        return;

    string skillKey;
    if (!PlayerData.TryConsumePendingSkillUnlockNotice(out skillKey))
    {
        skillUnlockPopupRoot.SetActive(false);
        return;
    }

    skillUnlockPopupTMP.text = BuildSkillUnlockedMessage_Local(skillKey);
    skillUnlockPopupRoot.SetActive(true);

    if (skillUnlockPopupCanvasGroup)
    {
        skillUnlockPopupCanvasGroup.alpha = 1f;
        skillUnlockPopupCanvasGroup.interactable = true;
        skillUnlockPopupCanvasGroup.blocksRaycasts = true;
    }

    skillUnlockPopupOkButton.onClick.RemoveAllListeners();
    skillUnlockPopupOkButton.onClick.AddListener(HideSkillUnlockPopup);
}
private const string PF_FULLSCREEN = "PF_Option_Fullscreen";

private void ApplySavedDisplayMode(bool applyResolutionWhenWindowed)
{
    bool isFullscreen = LoadFullscreenOption_DefaultTrue();
    ApplyFullscreen(isFullscreen, applyResolutionWhenWindowed);
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
private void ApplyFullscreen(bool isFullscreen, bool applyResolutionWhenWindowed)
{
    try
    {
        if (isFullscreen)
        {
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
                    size = CalcBestWindowedSize(16, 9, 160, 160, 960, 540);
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

    try { desktopW = Mathf.Max(1, Screen.currentResolution.width); } catch { }
    try { desktopH = Mathf.Max(1, Screen.currentResolution.height); } catch { }

    int maxW = Mathf.Max(minW, desktopW - marginW);
    int maxH = Mathf.Max(minH, desktopH - marginH);

    float targetAspect = (float)aspectW / (float)aspectH;

    int w = maxW;
    int h = Mathf.RoundToInt(w / targetAspect);

    if (h > maxH)
    {
        h = maxH;
        w = Mathf.RoundToInt(h * targetAspect);
    }

    w = Mathf.Max(minW, w);
    h = Mathf.Max(minH, h);

    return new Vector2Int(w, h);
}
public void OnClickStartBattleFlow()
{
    if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    Debug.Log("[MenuController] OnClickStartBattleFlow invoked");

    // TierSelect 側が開始時に読むので、会話シーン名はここで共有しておく
    if (!string.IsNullOrEmpty(angelDialogueScene))
        PlayerPrefs.SetString("AngelDialogueScene", angelDialogueScene);
    else
        PlayerPrefs.DeleteKey("AngelDialogueScene");

    if (!string.IsNullOrEmpty(enemyDialogueScene))
        PlayerPrefs.SetString("EnemyDialogueScene", enemyDialogueScene);
    else
        PlayerPrefs.DeleteKey("EnemyDialogueScene");

    PlayerPrefs.Save();

    ForceCleanStateBeforeTransition();

    // Tier選択へ（中断があれば TierSelect 側で「続き/最初から」を出す）
    if (!string.IsNullOrEmpty(tierSelectSceneName))
    {
        SceneManager.LoadScene(tierSelectSceneName, LoadSceneMode.Single);
        return;
    }

    // 保険：TierSelectが未設定なら従来通り
    if (!string.IsNullOrEmpty(angelDialogueScene))
    {
        SceneManager.LoadScene(angelDialogueScene, LoadSceneMode.Single);
        return;
    }

    if (!string.IsNullOrEmpty(enemyDialogueScene))
    {
        SceneManager.LoadScene(enemyDialogueScene, LoadSceneMode.Single);
        return;
    }

    SceneManager.LoadScene(battleSceneName, LoadSceneMode.Single);
}

    // 会話シーン側から呼ばれる進行用ユーティリティ
    public static void ContinueToEnemyDialogueOrBattle(string enemyDialogueScene, string battleSceneName)
    {
        if (!string.IsNullOrEmpty(enemyDialogueScene))
        {
            SceneManager.LoadScene(enemyDialogueScene);
        }
        else
        {
            SceneManager.LoadScene(string.IsNullOrEmpty(battleSceneName) ? "RunScene" : battleSceneName);
        }
    }

    // スキルセット画面へ（メニューのボタンから呼ぶ）
    public void OnClickOpenSkillSetScene()
    {
        if (!string.IsNullOrEmpty(skillSetSceneName))
        {
            // この時点では何も壊さず、単純に遷移だけ
            UnityEngine.SceneManagement.SceneManager.LoadScene(skillSetSceneName);
        }
    }

    public void OnClickOpenShopScene()
    {
        if (!string.IsNullOrEmpty(shopSceneName))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(shopSceneName);
        }
        else
        {
            Debug.LogWarning("[MenuController] shopSceneName is empty.");
        }
    }

    public void OnClickOpenEquipScene()
    {
        if (!string.IsNullOrEmpty(equipSceneName))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(equipSceneName);
        }
        else
        {
            Debug.LogWarning("[MenuController] equipSceneName is empty.");
        }
    }

    public void OnClickOpenSpecialTileScene()
    {
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);

        if (!string.IsNullOrEmpty(specialTileSceneName))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(specialTileSceneName);
        }
        else
        {
            Debug.LogWarning("[MenuController] specialTileSceneName is empty.");
        }
    }

    // ★追加：その他シーンへ（メニューの「その他」ボタンから呼ぶ）
    public void OnClickOpenOtherScene()
    {
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);

        // 将来RunSceneからも使う前提で「戻り先」を保存しておく
        try
        {
            string fromScene = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(fromScene)) fromScene = "MenuScene";
            PlayerPrefs.SetString("PF_ReturnSceneFromOther", fromScene);
            PlayerPrefs.Save();
        }
        catch { }

        ForceCleanStateBeforeTransition();

        if (!string.IsNullOrEmpty(otherSceneName))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(otherSceneName);
        }
        else
        {
            Debug.LogWarning("[MenuController] otherSceneName is empty.");
        }
    }
private void OnEnable()
{
    RefreshCurrentEnemyUI();
    RefreshHighScoreUI();
    RefreshGemCountUI();
    RefreshAchievementRewardNoticeIcon();
    RefreshGuideUnreadNoticeIcon();
    RefreshDemoWishlistButtonUI_Local();
}
    private void RefreshAchievementRewardNoticeIcon()
    {
        if (!achievementRewardNoticeIcon) return;

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

        if (achievementRewardNoticeIcon.activeSelf != show)
        {
            achievementRewardNoticeIcon.SetActive(show);
        }
    }

    private void RefreshGuideUnreadNoticeIcon()
    {
        if (!guideUnreadNoticeIcon) return;

        bool show = true;
        try
        {
            show = PlayerData.HasAnyUnreadGuide();
        }
        catch
        {
            show = true;
        }

        if (guideUnreadNoticeIcon.activeSelf != show)
        {
            guideUnreadNoticeIcon.SetActive(show);
        }
    }
    private void RefreshGemCountUI()
    {
        if (!gemCountTMP) return;

        int gems = 0;
        try
        {
            // SpecialTileSystem が存在する前提（既に他ファイルで参照されている）
            var t = typeof(SpecialTileSystem);

            // よくある候補：GetGems() / GetGemCount() / Gems / GemCount
            var mi = t.GetMethod("GetGems", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (mi != null && mi.ReturnType == typeof(int))
            {
                gems = (int)mi.Invoke(null, null);
            }
            else
            {
                var mi2 = t.GetMethod("GetGemCount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (mi2 != null && mi2.ReturnType == typeof(int))
                {
                    gems = (int)mi2.Invoke(null, null);
                }
                else
                {
                    var prop = t.GetProperty("Gems", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (prop != null && prop.PropertyType == typeof(int))
                    {
                        gems = (int)prop.GetValue(null, null);
                    }
                    else
                    {
                        var prop2 = t.GetProperty("GemCount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (prop2 != null && prop2.PropertyType == typeof(int))
                        {
                            gems = (int)prop2.GetValue(null, null);
                        }
                    }
                }
            }
        }
        catch { gems = 0; }

        gemCountTMP.text = gems.ToString();
    }

    private void RefreshDemoWishlistButtonUI_Local()
    {
        if (demoWishlistButtonTMP)
        {
            switch (GetMenuLanguage_Local())
            {
                case LocalizationManager.Language.English:
                    demoWishlistButtonTMP.text = "Wishlist Now";
                    break;

                case LocalizationManager.Language.ChineseSimplified:
                    demoWishlistButtonTMP.text = "Wishlist Now";
                    break;

                default:
                    demoWishlistButtonTMP.text = "Wishlist Now";
                    break;
            }
        }

        bool canOpen = fullGameSteamAppId != 0 || !string.IsNullOrEmpty(GetFullGameStoreUrl_Local());

        if (demoWishlistButton)
            demoWishlistButton.interactable = canOpen;
    }
    private string GetFullGameStoreUrl_Local()
    {
        if (!string.IsNullOrEmpty(fullGameStoreUrl))
            return fullGameStoreUrl;

        if (fullGameSteamAppId != 0)
            return $"https://store.steampowered.com/app/{fullGameSteamAppId}/";

        return "";
    }

    public void OnClickOpenFullGameStorePage()
    {
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);

        bool opened = TryOpenSteamStoreOverlay_Local();
        if (opened)
            return;

        string url = GetFullGameStoreUrl_Local();
        if (!string.IsNullOrEmpty(url))
        {
            Application.OpenURL(url);
            return;
        }

        Debug.LogWarning("[MenuController] Full game store destination is not configured.");
    }

    private bool TryOpenSteamStoreOverlay_Local()
    {
        if (fullGameSteamAppId == 0)
            return false;

        try
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

            System.Type steamFriendsType = null;
            System.Type appIdType = null;
            System.Type overlayFlagType = null;

            for (int i = 0; i < assemblies.Length; i++)
            {
                var asm = assemblies[i];
                if (steamFriendsType == null)
                    steamFriendsType = asm.GetType("Steamworks.SteamFriends");
                if (appIdType == null)
                    appIdType = asm.GetType("Steamworks.AppId_t");
                if (overlayFlagType == null)
                    overlayFlagType = asm.GetType("Steamworks.EOverlayToStoreFlag");
            }

            if (steamFriendsType == null || appIdType == null || overlayFlagType == null)
                return false;

            var method = steamFriendsType.GetMethod(
                "ActivateGameOverlayToStore",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            if (method == null)
                return false;

            object appIdValue = System.Activator.CreateInstance(appIdType, new object[] { fullGameSteamAppId });
            object flagValue = System.Enum.ToObject(overlayFlagType, 0);

            method.Invoke(null, new object[] { appIdValue, flagValue });
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[MenuController] Steam overlay open failed. Fallback to URL. " + e.Message);
            return false;
        }
    }

    // --- ここから下を MenuController の末尾に追記 ---
    private void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go); // シーンを跨いでも欠けないよう保険（重複は起きないように上で判定）
        }
        else
        {
            // 無効化されていたら有効化
            if (!EventSystem.current.enabled) EventSystem.current.enabled = true;
            if (!EventSystem.current.gameObject.activeInHierarchy)
                EventSystem.current.gameObject.SetActive(true);
        }
    }

    // 競合EventSystemがあれば1つだけ残して他は破棄
    private void EnsureSingleEventSystem()
    {
        var all = GameObject.FindObjectsOfType<EventSystem>(true);
        if (all.Length == 0)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            // DontDestroyOnLoad は付けない：シーンごとに1つを原則にする
            return;
        }
        // 先頭だけ残し、他は削除（重複で入力が死ぬのを防止）
        for (int i = 1; i < all.Length; i++)
        {
            if (all[i] && all[i].gameObject) Destroy(all[i].gameObject);
        }
        // 念のため有効化
        var es = all[0];
        if (!es.enabled) es.enabled = true;
        if (!es.gameObject.activeInHierarchy) es.gameObject.SetActive(true);
    }

    // StandaloneInputModule / InputSystemUIInputModule をいずれか必ず有効に
    private void EnsureInputModuleEnabled()
    {
        var es = EventSystem.current;
        if (!es) return;

        // 旧InputSystem
        var sim = es.GetComponent<StandaloneInputModule>();
        // 新InputSystem（使用していないなら null のままでOK）
    #if ENABLE_INPUT_SYSTEM
        var uim = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    #else
        UnityEngine.InputSystem.UI.InputSystemUIInputModule uim = null;
    #endif
        if (!sim && !uim)
        {
            sim = es.gameObject.AddComponent<StandaloneInputModule>();
        }
        if (sim) { sim.enabled = false; sim.enabled = true; }
        if (uim) { uim.enabled = false; uim.enabled = true; }
    }

    private void ClearPotentialUIBlockers()
    {
        // タグ指定
        try
        {
            var tagged = GameObject.FindGameObjectsWithTag("UIBlocker");
            foreach (var go in tagged)
            {
                if (IsInitialLanguagePanelObject_Local(go)) continue;
                SafeDisableRaycast(go);
            }
        }
        catch (UnityException) { /* タグ未定義なら無視 */ }

        // 画面ほぼ全体を覆う Image を無効化（Block/Overlay/Modal などの名前）
        var imgs = GameObject.FindObjectsOfType<UnityEngine.UI.Image>(true);
        foreach (var img in imgs)
        {
            if (!img.raycastTarget) continue;
            if (IsInitialLanguagePanelObject_Local(img.gameObject)) continue;

            var rt = img.rectTransform;
            if (!rt) continue;
            if (rt.rect.width >= Screen.width * 0.95f && rt.rect.height >= Screen.height * 0.95f)
            {
                string n = img.name.ToLower();
                if (n.Contains("block") || n.Contains("overlay") || n.Contains("modal"))
                    SafeDisableRaycast(img.gameObject);
            }
        }

        // ルート Canvas が CanvasGroup でブロックしていたら解除
        var rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas)
        {
            var cg = rootCanvas.GetComponent<CanvasGroup>();
            if (cg)
            {
                cg.blocksRaycasts = false;
                cg.interactable = true;
                cg.ignoreParentGroups = true;
            }
        }
    }

    private bool IsInitialLanguagePanelObject_Local(GameObject go)
    {
        if (go == null || initialLanguagePanelRoot == null) return false;

        Transform t = go.transform;
        Transform root = initialLanguagePanelRoot.transform;

        while (t != null)
        {
            if (t == root) return true;
            t = t.parent;
        }

        return false;
    }

    private void SafeDisableRaycast(GameObject go)
    {
        var g = go.GetComponent<UnityEngine.UI.Graphic>();
        if (g) g.raycastTarget = false;
        var cg = go.GetComponent<CanvasGroup>();
        if (cg) cg.blocksRaycasts = false;
    }

    private void EnableAllButtonsInThisCanvas()
    {
        var root = GetComponentInParent<Canvas>();
        if (!root) return;
        var buttons = root.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        foreach (var b in buttons) b.interactable = true;
    }

    /// <summary>
    /// 他シーンを経由したあとに残る Additive シーン/ブロッカー/ES を強制的に掃除してから遷移する
    /// </summary>
    private void ForceCleanStateBeforeTransition()
    {
        // 1) 非アクティブ（＝今のメニューではない）ロード済みシーンを全部アンロード
        var active = SceneManager.GetActiveScene();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.IsValid() && s.isLoaded && s != active)
            {
                try { SceneManager.UnloadSceneAsync(s); } catch {}
            }
        }

        // 2) UIブロッカー/CanvasGroup を無効化（透明全画面など）
        ClearPotentialUIBlockers();

        // 3) EventSystem を現在シーンに1つだけ残す（競合排除＆InputModule有効化）
        EnsureSingleEventSystem();
        EnsureInputModuleEnabled();

        // 4) 念のため全ボタンを有効化＆選択解除
        EnableAllButtonsInThisCanvas();
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);

        // 5) Timescale を正常化
        Time.timeScale = 1f;
    }
}