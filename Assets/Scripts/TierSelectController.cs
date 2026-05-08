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
        // 「続きから」：中断データは残したまま、中断していたシーンへ直行
        // GameManager 側で TryLoadSuspendSnapshot() が走って復帰される
        Time.timeScale = 1f;

        string targetScene = "";
        try
        {
            targetScene = PlayerPrefs.GetString("PF_ResumeScene", "");
        }
        catch
        {
            targetScene = "";
        }

        if (string.IsNullOrEmpty(targetScene))
        {
            targetScene = string.IsNullOrEmpty(battleSceneName) ? "RunScene" : battleSceneName;
        }

        // フラグを立てておく（他シーンが参照する可能性があるため）
        PlayerPrefs.SetInt("PF_ResumeDirect", 1);
        PlayerPrefs.SetString("PF_ResumeScene", targetScene);
        PlayerPrefs.Save();

        SceneManager.LoadScene(targetScene, LoadSceneMode.Single);
    }
    private void OnClickRestartFromScratch()
    {
        // 「最初から」：中断データを破棄し、次の開始は必ず新規ランとしてリセットする
        Time.timeScale = 1f;

        try { PlayerPrefs.DeleteKey(PF_SUSPEND_FLAG); } catch {}
        try { PlayerPrefs.DeleteKey(PF_SUSPEND_JSON); } catch {}

        PlayerPrefs.SetInt("PF_ResumeDirect", 0);
        PlayerPrefs.DeleteKey("PF_ResumeScene");
        PlayerPrefs.Save();

        try { StageClearManager.ResetEnemyProgressionNow(); } catch {}

        ShowTierSelectPanel();
    }

    private void OnClickStartNewRunWithSelectedTier()
    {
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

        // Angel会話へ
        if (!string.IsNullOrEmpty(angelDialogueScene))
        {
            SceneManager.LoadScene(angelDialogueScene, LoadSceneMode.Single);
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
