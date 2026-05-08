using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class AchievementPanelController : MonoBehaviour
{
    [Serializable]
    public class AchievementRow
    {
        public AchievementId id;
        public AchievementEntryUI ui;
        public int gemReward;
    }

    [Header("Achievement Rows")]
    [SerializeField] private List<AchievementRow> rows = new List<AchievementRow>();

    [Header("Reward Popup (Panel UI)")]
    [SerializeField] private GameObject rewardPanelRoot;
    [SerializeField] private TextMeshProUGUI rewardTMP;
    [SerializeField] private Button rewardOkButton;

    private AchievementId _pendingClaimId;
    private int _pendingGem;

    private void Awake()
    {
        if (rewardOkButton)
        {
            rewardOkButton.onClick.RemoveAllListeners();
            rewardOkButton.onClick.AddListener(OnClickRewardOk);
        }

        HideRewardPopupImmediate();
    }

    private void Start()
    {
        try { AchievementSystem.ReconcileFromSaveData(); } catch { }

        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] == null) continue;
            if (rows[i].ui == null) continue;
            rows[i].ui.Setup(this, rows[i].id);
        }

        RefreshAll();
    }

    public void RefreshAll()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] == null) continue;
            if (rows[i].ui == null) continue;
            rows[i].ui.Refresh();
        }
    }

    public void OnClickAchievement(AchievementId id)
    {
        int reward = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] == null) continue;
            if (rows[i].id != id) continue;
            reward = Mathf.Max(0, rows[i].gemReward);
            break;
        }

        bool ready = AchievementSystem.IsReady(id);
        bool claimed = AchievementSystem.IsClaimed(id);

        if (!ready || claimed)
        {
            RefreshAll();
            return;
        }

        _pendingClaimId = id;
        _pendingGem = reward;

        ShowRewardPopup(id, reward);
    }
private void ShowRewardPopup(AchievementId id, int gems)
{
    string title = AchievementSystem.GetDisplayTitle(id);
    var lm = LocalizationManager.Instance;

    if (rewardTMP)
    {
        if (gems > 0)
        {
            string line = null;

            if (lm != null)
            {
                line = lm.FormatText("achievement.reward_gems", gems);
                if (string.IsNullOrEmpty(line) || line == "achievement.reward_gems")
                    line = null;
            }

            if (string.IsNullOrEmpty(line))
            {
                line = "宝石を" + gems.ToString() + "個獲得";
            }

            rewardTMP.text = title + "\n" + line;
        }
        else
        {
            string line = null;

            if (lm != null)
            {
                line = lm.GetText("achievement.reward_none");
                if (string.IsNullOrEmpty(line) || line == "achievement.reward_none")
                    line = null;
            }

            if (string.IsNullOrEmpty(line))
            {
                line = "報酬はありません";
            }

            rewardTMP.text = title + "\n" + line;
        }
    }

    if (rewardPanelRoot)
    {
        rewardPanelRoot.SetActive(true);
    }
}

    private void HideRewardPopupImmediate()
    {
        if (rewardPanelRoot)
        {
            rewardPanelRoot.SetActive(false);
        }
    }

    private void OnClickRewardOk()
    {
        bool claimed = false;
        try
        {
            claimed = AchievementSystem.TryClaim(_pendingClaimId, _pendingGem);
        }
        catch { claimed = false; }

        HideRewardPopupImmediate();
        RefreshAll();
    }
}