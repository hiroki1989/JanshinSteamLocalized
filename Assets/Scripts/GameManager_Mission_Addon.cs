using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GameManager 用ミッション拡張（partial）。
/// ・RunScene 上にミッションテキストを常時表示
/// ・プレイヤー和了時に達成条件を記録
/// ・達成確定・報酬・契約書の表示はショップ入場時
///
/// ★役プール（難易度設定）は MissionPoolSO（ScriptableObject）に集約。
///   Assets/Resources/MissionPoolSO.asset を作成し、そこで全役の難易度を設定する。
///   Inspector から直接参照したい場合は missionPoolAsset に割り当ててもOK。
/// </summary>
public partial class GameManager : MonoBehaviour
{
    // ===============================
    //  Mission: Pool 参照
    // ===============================
    [Header("Mission System - Pool")]
    [Tooltip("ミッション役プール設定。未設定なら Resources/MissionPoolSO を自動ロード。\n" +
             "★難易度の変更はこのアセットの Inspector で行う。")]
    [SerializeField] private MissionPoolSO missionPoolAsset;

    // ===============================
    //  Mission UI (Inspector)
    // ===============================
    [Header("Mission System - UI")]
    [Tooltip("RunScene 上でミッション内容を常時表示するテキスト")]
    [SerializeField] private TextMeshProUGUI missionDisplayTMP;

    [Tooltip("ミッション達成パネル（通常は非表示）")]
    [SerializeField] private GameObject missionCompletePanel;

    [Tooltip("ミッション達成パネル上のテキスト")]
    [SerializeField] private TextMeshProUGUI missionCompleteTMP;

    [Tooltip("ミッション達成パネルの閉じるボタン")]
    [SerializeField] private Button missionCompleteCloseButton;

    [Tooltip("ミッション達成時に鳴らすSE（任意）")]
    [SerializeField] private AudioClip missionCompleteSEClip;

    [Tooltip("上のSEを鳴らすAudioSource（任意）")]
    [SerializeField] private AudioSource missionCompleteSESource;

    [Header("Mission System - Colors")]
    [SerializeField] private Color missionCompletedColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
    [SerializeField] private Color missionActiveColor    = new Color(1f, 1f, 0.6f, 1f);

    // 内部状態
    private bool _missionJustCompleted = false;
    private string _missionContractAcquired = "";

    // ===============================
    //  Pool 解決
    // ===============================
    private List<MissionSystem.MissionYakuEntry> ResolveMissionPool()
    {
        // 1) Inspector に直接割り当てがあればそれ
        if (missionPoolAsset != null)
            return missionPoolAsset.GetPool();

        // 2) Resources から自動ロード
        var shared = MissionPoolSO.LoadShared();
        if (shared != null)
            return shared.GetPool();

        // 3) 空（ミッション無し）
        return new List<MissionSystem.MissionYakuEntry>();
    }

    // ===============================
    //  初期化
    // ===============================
    public void InitMissionUI()
    {
        try
        {
            MissionSystem.Load();
            int enemyKey = EnemyConfigExcel.MapRuntimeIndexToExcelKey(ProgressionFlowController.GetCurrentEnemyIndex());
            // Keep the dialogue's mission and completion state; repair missing/stale assignments only.
            if (!MissionSystem.HasActiveMission || MissionSystem.CurrentEnemyKey != enemyKey)
                MissionSystem.AssignForEnemy(enemyKey, ResolveMissionPool());


            if (missionCompleteCloseButton)
            {
                missionCompleteCloseButton.onClick.RemoveAllListeners();
                missionCompleteCloseButton.onClick.AddListener(OnClickMissionCompleteClose);
            }

            if (missionCompletePanel)
                missionCompletePanel.SetActive(false);

            RefreshMissionDisplayText();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Mission] InitMissionUI error: " + e.Message);
        }
    }

    // ===============================
    //  表示更新
    // ===============================
    private void RefreshMissionDisplayText()
    {
        if (!missionDisplayTMP) return;

        if (!MissionSystem.HasActiveMission)
        {
            missionDisplayTMP.text = "";
            return;
        }

        string text = MissionSystem.GetMissionDisplayText();
        missionDisplayTMP.richText = true;
        bool claimed = MissionSystem.IsAlreadyClaimed(MissionSystem.CurrentEnemyKey);
        bool completed = MissionSystem.IsCompleted || MissionSystem.HasPendingCompletion;

        if (claimed || completed)
        {
            missionDisplayTMP.text = $"<s>{text}</s>";
            missionDisplayTMP.color = Color.white;
        }
        else
        {
            missionDisplayTMP.text = text;
            missionDisplayTMP.color = Color.white;
        }
    }

    // ===============================
    //  プレイヤー和了時フック
    // ===============================
    public void CheckMissionOnPlayerWin(List<string> yakuNames)
    {
        // Record eligibility only. Completion, Gold and acquisition notices belong to shop entry.
        MissionSystem.CheckCompletion(yakuNames);
        RefreshMissionDisplayText();
    }

    public void TryShowMissionCompletePanel() { }

    private void OnClickMissionCompleteClose()
    {
        if (missionCompletePanel) missionCompletePanel.SetActive(false);
    }
}
