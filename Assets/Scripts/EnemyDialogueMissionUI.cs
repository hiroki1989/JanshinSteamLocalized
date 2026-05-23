using UnityEngine;
using TMPro;

/// <summary>
/// EnemyDialogue シーンの敵情報パネルにミッションテキストを表示するコンポーネント。
///
/// ★役プールは MissionPoolSO（ScriptableObject）から取得。
///   GameManager が同シーンに無くても問題なく動作する。
///   Inspector で missionPoolAsset を直接割り当てるか、
///   Assets/Resources/MissionPoolSO.asset を配置すれば自動ロードする。
/// </summary>
public class EnemyDialogueMissionUI : MonoBehaviour
{
    [Header("Mission Pool")]
    [Tooltip("ミッション役プール設定。未設定なら Resources/MissionPoolSO を自動ロード。")]
    [SerializeField] private MissionPoolSO missionPoolAsset;

    [Header("Mission UI")]
    [Tooltip("敵情報パネル内に配置するミッション表示テキスト")]
    [SerializeField] private TextMeshProUGUI enemyInfoMissionTMP;

    [Header("Colors")]
    [SerializeField] private Color activeColor    = new Color(1f, 0.9f, 0.3f, 1f);
    [SerializeField] private Color completedColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);

    private void Start()
    {
        AssignAndDisplayMission();
    }

    private void AssignAndDisplayMission()
    {
        try
        {
            // 現在の敵インデックスを取得
            int idxAbs = 0;
            try { idxAbs = ProgressionFlowController.GetCurrentEnemyIndex(); }
            catch
            {
                try { idxAbs = GameManager.GetCurrentEnemyIndex(); }
                catch { idxAbs = 0; }
            }

            // ExcelKey を取得
            int excelKey = 0;
            try { excelKey = EnemyConfigExcel.MapRuntimeIndexToExcelKey(idxAbs); }
            catch { excelKey = idxAbs; }

            // Pool を取得
            var pool = ResolvePool();

            // ミッションを割り当て
            MissionSystem.AssignForEnemy(excelKey, pool);

            // テキスト表示
            RefreshMissionText();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[EnemyDialogueMissionUI] Error: " + e.Message);
            if (enemyInfoMissionTMP) enemyInfoMissionTMP.text = "";
        }
    }

    private System.Collections.Generic.List<MissionSystem.MissionYakuEntry> ResolvePool()
    {
        // 1) Inspector に直接割り当てがあればそれ
        if (missionPoolAsset != null)
            return missionPoolAsset.GetPool();

        // 2) Resources から自動ロード
        var shared = MissionPoolSO.LoadShared();
        if (shared != null)
            return shared.GetPool();

        // 3) 空
        return new System.Collections.Generic.List<MissionSystem.MissionYakuEntry>();
    }

    private void RefreshMissionText()
    {
        if (!enemyInfoMissionTMP) return;

        if (!MissionSystem.HasActiveMission)
        {
            enemyInfoMissionTMP.text = "";
            return;
        }

        string text = MissionSystem.GetMissionDisplayText();
        bool alreadyClaimed = MissionSystem.IsAlreadyClaimed(MissionSystem.CurrentEnemyKey);

        if (alreadyClaimed)
        {
            enemyInfoMissionTMP.text = $"<s>{text}</s>　（{GetClaimedLabel()}）";
            enemyInfoMissionTMP.color = completedColor;
        }
        else
        {
            enemyInfoMissionTMP.text = text;
            enemyInfoMissionTMP.color = activeColor;
        }
    }

    private static string GetClaimedLabel()
    {
        try
        {
            var lm = LocalizationManager.Instance;
            if (lm != null)
            {
                switch (lm.CurrentLanguage)
                {
                    case LocalizationManager.Language.English:           return "Claimed";
                    case LocalizationManager.Language.ChineseSimplified: return "已领取";
                }
            }
        }
        catch { }
        return "達成済み";
    }
}
