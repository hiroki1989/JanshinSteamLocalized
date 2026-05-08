using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class AchievementEntryUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TextMeshProUGUI titleTMP;
    [SerializeField] private Toggle claimedToggle;
    [SerializeField] private Image readyIcon;
    [SerializeField] private Button clickButton;

    private AchievementId _id;
    private AchievementPanelController _owner;

    public void Setup(AchievementPanelController owner, AchievementId id)
    {
        _owner = owner;
        _id = id;

        if (clickButton)
        {
            clickButton.onClick.RemoveAllListeners();
            clickButton.onClick.AddListener(OnClick);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (titleTMP) titleTMP.text = AchievementSystem.GetDisplayTitle(_id);

        bool ready = AchievementSystem.IsReady(_id);
        bool claimed = AchievementSystem.IsClaimed(_id);

        if (readyIcon) readyIcon.enabled = (ready && !claimed);

        if (claimedToggle)
        {
            claimedToggle.isOn = claimed;
            claimedToggle.interactable = false;
        }
    }

    private void OnClick()
    {
        if (_owner != null) _owner.OnClickAchievement(_id);
    }
}