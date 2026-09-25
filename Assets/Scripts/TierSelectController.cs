using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class TierSelectController : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string menuSceneName = "MenuScene";
    [SerializeField] private string angelDialogueScene = "AngelDialogue";
    [SerializeField] private string enemyDialogueScene = "EnemyDialogue";
    [SerializeField] private string battleSceneName = "RunScene";

    [Header("Resume Panel (shown if suspend exists)")]
    [SerializeField] private GameObject resumeChoicePanel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private TextMeshProUGUI resumeInfoTMP;

    [Header("Tier Select Panel (Dropdown)")]
    [SerializeField] private GameObject tierSelectPanel;
    [SerializeField] private TMP_Dropdown tierDropdown;
    [SerializeField] private TextMeshProUGUI selectedTierTMP;
    [SerializeField] private Button startButton;
    [Header("外伝モード UI設定（未指定時はResourcesの設定を使用）")]
    [SerializeField] private GaidenUISettings gaidenUISettings;
    private Button _seventeenStepsButton;
    private GameObject _seventeenCharacterPanel;

    [Header("Back")]
    [SerializeField] private Button backToMenuButton;

    [Header("Debug (Test Start Enemy)")]
    [SerializeField] private bool debugStartEnemyEnabled = false;
    [SerializeField] private int debugStartEnemyIndex = 0;

    private int _selectedTier = 1;

    private const string KeyCurrentTier = "PF_CurrentTier";
    private const string KeyUnlockedTierMax = "PF_UnlockedTierMax";

    private const string PF_SUSPEND_JSON = "Run_SuspendJSON";
    private const string PF_SUSPEND_FLAG = "Run_HasSuspend";

    private static string TierSelectFixed(string key)
    {
        return LocalizationManager.Instance.GetFixedText(key);
    }

    private static string TierSelectFixedFormat(string key, params object[] args)
    {
        string format = TierSelectFixed(key);
        try
        {
            return string.Format(format, args);
        }
        catch
        {
            return format;
        }
    }

    private void Start()
    {
        Time.timeScale = 1f;

        if (backToMenuButton)
        {
            backToMenuButton.onClick.RemoveAllListeners();
            backToMenuButton.onClick.AddListener(OnClickBackToMenu);
        }

        bool hasSuspend = false;
        try { hasSuspend = PlayerPrefs.GetInt(PF_SUSPEND_FLAG, 0) == 1; } catch { hasSuspend = false; }

        if (hasSuspend)
        {
            ShowResumeChoicePanel();
        }
        else
        {
            ShowTierSelectPanel();
        }
    }

    private void ShowResumeChoicePanel()
    {
        if (resumeChoicePanel) resumeChoicePanel.SetActive(true);
        if (tierSelectPanel) tierSelectPanel.SetActive(false);

        if (resumeInfoTMP)
        {
            resumeInfoTMP.text = TierSelectFixed("tier_select_resume_info");
        }
        if (continueButton)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnClickContinueFromSuspend);
        }

        if (restartButton)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(OnClickRestartFromScratch);
        }
    }

    private void ShowTierSelectPanel()
    {
        if (resumeChoicePanel) resumeChoicePanel.SetActive(false);
        if (tierSelectPanel) tierSelectPanel.SetActive(true);

        int unlocked = Mathf.Max(1, PlayerPrefs.GetInt(KeyUnlockedTierMax, 1));
        int current = Mathf.Max(1, PlayerPrefs.GetInt(KeyCurrentTier, 1));
        _selectedTier = Mathf.Clamp(current, 1, unlocked);

        BuildTierDropdownOptions(unlocked, _selectedTier);

        if (startButton)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnClickStartNewRunWithSelectedTier);
        }

        EnsureSeventeenStepsButton();

        RefreshSelectedTierUI();
    }
    private void BuildTierDropdownOptions(int unlockedTierMax, int initialTier)
    {
        if (!tierDropdown) return;

        var opts = new List<TMP_Dropdown.OptionData>();
        for (int tier = 1; tier <= unlockedTierMax; tier++)
        {
            float mult = 1f + 0.3f * (tier - 1);
            int lvFrom = (tier - 1) * 10 + 1;
            int lvTo = tier * 10;
            string label = TierSelectFixedFormat("tier_dropdown_item_format", tier, lvFrom, lvTo, mult);
            opts.Add(new TMP_Dropdown.OptionData(label));
        }

        tierDropdown.ClearOptions();
        tierDropdown.AddOptions(opts);

        int idx = Mathf.Clamp(initialTier - 1, 0, Mathf.Max(0, unlockedTierMax - 1));
        tierDropdown.SetValueWithoutNotify(idx);

        tierDropdown.onValueChanged.RemoveAllListeners();
        tierDropdown.onValueChanged.AddListener(OnDropdownTierChanged);

        _selectedTier = idx + 1;
    }
    private void OnDropdownTierChanged(int dropdownIndex)
    {
        _selectedTier = Mathf.Max(1, dropdownIndex + 1);
        RefreshSelectedTierUI();
    }
    private void RefreshSelectedTierUI()
    {
        int unlocked = Mathf.Max(1, PlayerPrefs.GetInt(KeyUnlockedTierMax, 1));
        _selectedTier = Mathf.Clamp(_selectedTier, 1, unlocked);

        if (selectedTierTMP)
        {
            float mult = 1f + 0.3f * (_selectedTier - 1);
            int lvFrom = (_selectedTier - 1) * 10 + 1;
            int lvTo = _selectedTier * 10;

            string debugTxt = "";
            if (debugStartEnemyEnabled)
            {
                int idx = Mathf.Clamp(debugStartEnemyIndex, 0, 9);
                debugTxt = "\n" + TierSelectFixedFormat("tier_debug_enemy_on", idx);
            }
            else
            {
                debugTxt = "\n" + TierSelectFixed("tier_debug_enemy_off");
            }

            selectedTierTMP.text = TierSelectFixedFormat("tier_selected_format", _selectedTier, lvFrom, lvTo, mult) + debugTxt;
        }
    }
    private void OnClickContinueFromSuspend()
    {
        Time.timeScale = 1f;
        string targetScene = PlayerPrefs.GetString("PF_ResumeScene", "");
        if (string.IsNullOrEmpty(targetScene)) targetScene = string.IsNullOrEmpty(battleSceneName) ? "RunScene" : battleSceneName;
        PlayerPrefs.SetInt("PF_ResumeDirect", 1);
        PlayerPrefs.SetString("PF_ResumeScene", targetScene);
        PlayerPrefs.Save();
        SceneManager.LoadScene(targetScene, LoadSceneMode.Single);
    }

    private void OnClickRestartFromScratch()
    {
        Time.timeScale = 1f;
        try { PlayerPrefs.DeleteKey(PF_SUSPEND_FLAG); } catch {}
        try { PlayerPrefs.DeleteKey(PF_SUSPEND_JSON); } catch {}
        PlayerPrefs.SetInt("PF_ResumeDirect", 0);
        PlayerPrefs.DeleteKey("PF_ResumeScene");
        PlayerPrefs.Save();

        try { StageClearManager.ResetEnemyProgressionNow(); } catch {}
        ShowTierSelectPanel();
    }

    private void EnsureSeventeenStepsButton()
    {
        if (_seventeenStepsButton || !startButton) return;
        var settings=gaidenUISettings?gaidenUISettings:GaidenUISettings.Current;
        var go = new GameObject("StartSeventeenSteps", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(startButton.GetComponentInParent<Canvas>().transform, false);
        var source = startButton.GetComponent<RectTransform>();
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f,.5f);
        rt.pivot = new Vector2(1f,.5f);
        rt.anchoredPosition = settings.buttonPosition;
        rt.sizeDelta = settings.buttonSize;
        var image = go.GetComponent<Image>();
        image.color=settings.buttonColor;image.sprite=settings.buttonSprite;image.type=settings.buttonImageType;
        var frame=SeventeenStepsUI.Picture(go.transform,settings.frameSprite?settings.frameSprite:Resources.Load<Sprite>("Consumables/PanelFrame"),Vector2.zero,rt.sizeDelta);
        frame.type=Image.Type.Sliced;frame.preserveAspect=false;frame.pixelsPerUnitMultiplier=5;
        frame.color=settings.frameColor;frame.gameObject.SetActive(settings.showFrame);
        for(int side=-1;settings.showSeals&&side<=1;side+=2){
            var seal=SeventeenStepsUI.Rect("GoldSeal",go.transform,new Vector2(side*(rt.sizeDelta.x*.5f-25),0),new Vector2(8,8));
            seal.localRotation=Quaternion.Euler(0,0,45);var mark=seal.gameObject.AddComponent<Image>();mark.color=settings.sealColor;mark.raycastTarget=false;
        }
        _seventeenStepsButton = go.GetComponent<Button>();
        _seventeenStepsButton.targetGraphic = image;
        _seventeenStepsButton.onClick.AddListener(OnClickStartSeventeenSteps);
        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        var label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = settings.buttonLabel;
        var sourceLabel=startButton.GetComponentInChildren<TMP_Text>();if(sourceLabel){label.font=sourceLabel.font;label.fontSharedMaterial=sourceLabel.fontSharedMaterial;}
        label.alignment = TextAlignmentOptions.Center;
        label.color = settings.labelColor;
        SeventeenStepsUI.BlackOutline(label);
        label.fontSize = settings.buttonFontSize;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = settings.buttonFontSize;
        var labelRt = (RectTransform)labelGo.transform;
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(12f, 6f);
        labelRt.offsetMax = new Vector2(-12f, -6f);
    }

    private void OnClickStartSeventeenSteps()
    {
        if (_seventeenCharacterPanel) return;
        var canvas=GetComponentInParent<Canvas>();
        if(!canvas)canvas=FindFirstObjectByType<Canvas>();
        var panel=SeventeenStepsUI.Rect("SeventeenCharacterSelect",canvas.transform,Vector2.zero,new Vector2(1760,940));
        _seventeenCharacterPanel=panel.gameObject;
        SeventeenStepsUI.Paper(panel,panel.sizeDelta);
        SeventeenStepsUI.Label(panel,"外伝モード　キャラクター選択",new Vector2(0,385),new Vector2(1500,95),60).color=Color.black;
        var choices=new[]{SeventeenStepsMode.Character.DyeMaster,SeventeenStepsMode.Character.Calligrapher,SeventeenStepsMode.Character.Capitalist};
        var names=new[]{"染色師","書家","資産家"};
        var portraits=new[]{"RandomMan_victory","RandomHonor_victory","Capitalist_victory"};
        var descriptions=new[]{"指定した色の数牌へ変換\n1局に2回使用できます","ランダムな字牌へ変換\n1局に2回使用できます","各敵の開始時に\nお札を2枚選べます"};
        for(int i=0;i<3;i++){
            int index=i;
            var button=SeventeenStepsUI.Button(panel,"",new Vector2((i-1)*530,5),new Vector2(485,610),()=>StartSeventeenStepsWithCharacter(choices[index]));
            SeventeenStepsUI.Frame(button.transform,new Vector2(485,610));
            SeventeenStepsUI.Picture(button.transform,SeventeenStepsUI.CharacterArt(choices[i]),new Vector2(0,65),new Vector2(390,365));
            SeventeenStepsUI.Label(button.transform,names[i],new Vector2(0,-155),new Vector2(420,65),46).color=Color.black;
            SeventeenStepsUI.Label(button.transform,descriptions[i],new Vector2(0,-238),new Vector2(425,95),28).color=Color.black;
        }
        SeventeenStepsUI.Navigation(panel,"戻る",new Vector2(-650,-395),new Vector2(300,85),()=>{Destroy(_seventeenCharacterPanel);_seventeenCharacterPanel=null;});
    }

    private void StartSeventeenStepsWithCharacter(SeventeenStepsMode.Character character)
    {
        Time.timeScale = 1f;
        SeventeenStepsMode.StartNewRun(character);
        SeventeenStepsController.Open();
    }

    private void OnClickStartNewRunWithSelectedTier()
    {
        if(NormalJourney.IsPlaying)return;
                // ★追加：中断の有無に関係なく、「最初から開始」は必ず敗北時相当のリセットを先に実行する
        try { StageClearManager.ResetEnemyProgressionNow(); } catch {}
        try { PlayerPrefs.DeleteKey(PF_SUSPEND_FLAG); } catch {}
        try { PlayerPrefs.DeleteKey(PF_SUSPEND_JSON); } catch {}
        PlayerPrefs.SetInt("PF_ResumeDirect", 0);
        PlayerPrefs.DeleteKey("PF_ResumeScene");
        PlayerPrefs.Save();
        // 対局フロー用シーン名を共有（MenuControllerと同じキー）
        if (!string.IsNullOrEmpty(angelDialogueScene))
            PlayerPrefs.SetString("AngelDialogueScene", angelDialogueScene);
        else
            PlayerPrefs.DeleteKey("AngelDialogueScene");

        if (!string.IsNullOrEmpty(enemyDialogueScene))
            PlayerPrefs.SetString("EnemyDialogueScene", enemyDialogueScene);
        else
            PlayerPrefs.DeleteKey("EnemyDialogueScene");

        PlayerPrefs.Save();

        // Tier確定
        PlayerPrefs.SetInt(KeyCurrentTier, Mathf.Max(1, _selectedTier));
        PlayerPrefs.Save();

        // 新規ラン開始の完全初期化
        SeventeenStepsMode.LeaveMode();
        MissionSystem.ResetForNewRun();
        RunConsumables.ResetRun();
        MissionSystem.ClearRunSeed();
        PlayerPrefs.SetInt("PF_ResumeDirect", 0);
        PlayerPrefs.DeleteKey("PF_ResumeScene");

        // 中断データも念のため破棄（「最初から」で来ていれば既に消えているが保険）
        try { PlayerPrefs.DeleteKey(PF_SUSPEND_FLAG); } catch {}
        try { PlayerPrefs.DeleteKey(PF_SUSPEND_JSON); } catch {}
        int startEnemyIndex = 0;
        if (debugStartEnemyEnabled)
        {
            startEnemyIndex = Mathf.Clamp(debugStartEnemyIndex, 0, 9);
        }

        GameManager.SetCurrentEnemyIndex(startEnemyIndex);
        GameManager.SetLoopCount(0);

        try
        {
            if (debugStartEnemyEnabled) ProgressionFlowController.ForceSetCurrentEnemyIndex(startEnemyIndex);
            else ProgressionFlowController.ForceResetToFirstEnemy();
        }
        catch {}
        try
        {
            string startEnemyName = "";
            try { startEnemyName = ProgressionFlowController.GetCurrentEnemyName(); } catch {}
            PlayerPrefs.SetInt("PF_CurrentEnemyIndex", startEnemyIndex);
            PlayerPrefs.SetString("PF_CurrentEnemyName", startEnemyName ?? "");
            PlayerPrefs.SetInt("CurrentEnemyIndex", startEnemyIndex);
            PlayerPrefs.SetString("CurrentEnemyName", startEnemyName ?? "");
            PlayerPrefs.Save();
        }
        catch {}

        PlayerPrefs.SetInt("PF_ResetRunOnLoad", 1);
        PlayerPrefs.SetInt("PF_PendingFullHeal", 1);

        try { PlayerPrefs.DeleteKey("Run_PlayerHP"); } catch {}
        try { PlayerPrefs.DeleteKey("Run_PlayerMP"); } catch {}
        try { PlayerPrefs.DeleteKey("Run_HPBonus"); } catch {}
        try { PlayerPrefs.DeleteKey("Run_MPBonus"); } catch {}
        try { PlayerPrefs.DeleteKey("Run_SkillCastsBonus"); } catch {}
        try { PlayerPrefs.DeleteKey("EnemiesDefeated"); } catch {}
        try { PlayerPrefs.DeleteKey("RunCleared"); } catch {}
        try { PlayerPrefs.Save(); } catch {}

        SeventeenStepsMode.ReserveStartingItem();

        // Angel会話へ
        if (!string.IsNullOrEmpty(angelDialogueScene))
        {
            NormalJourney.Play(NormalJourney.Leg.Angel, () => SceneManager.LoadScene(angelDialogueScene, LoadSceneMode.Single));
            return;
        }

        // 会話無しなら敵会話→なければRun
        if (!string.IsNullOrEmpty(enemyDialogueScene))
        {
            SceneManager.LoadScene(enemyDialogueScene, LoadSceneMode.Single);
            return;
        }

        SceneManager.LoadScene(string.IsNullOrEmpty(battleSceneName) ? "RunScene" : battleSceneName, LoadSceneMode.Single);
    }

    private void OnClickBackToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(string.IsNullOrEmpty(menuSceneName) ? "MenuScene" : menuSceneName, LoadSceneMode.Single);
    }
}
