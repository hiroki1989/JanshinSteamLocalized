using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Scene-authored controls. No UI objects are created during gameplay.
public sealed class BattleInspectionHUD : MonoBehaviour
{
    public Button contractButton, specialTilesButton, enemySkillsButton;
    public Image contractIcon;
    public TMP_Text enemySkillsLabel, specialTilesLabel;
    GameManager manager;
    float nextRefresh;
    void Start()
    {
        manager=FindAnyObjectByType<GameManager>();
        if(!manager)return;
        contractButton.onClick.AddListener(manager.ShowBattleContract);
        specialTilesButton.onClick.AddListener(manager.ShowBattleSpecialTiles);
        enemySkillsButton.onClick.AddListener(manager.ShowBattleEnemySkills);
        Refresh();
    }
    void LateUpdate()
    {
        if(!manager || Time.unscaledTime<nextRefresh)return;
        nextRefresh=Time.unscaledTime+.25f;Refresh();
    }
    void Refresh()=>manager.RefreshBattleInspection(this);
}
