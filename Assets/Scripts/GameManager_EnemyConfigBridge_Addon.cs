// GameManager_EnemyConfigBridge_Addon.cs
// Excelローダーで取得した設定を GameManager に"後から"適用する（既存と重複させない）

using UnityEngine;

public partial class GameManager : MonoBehaviour
{
public void ApplyEnemyConfigFromExcel(int runtimeIndex)
{
    // ← ランタイム用のインデックス正規化 API を使用
    if (EnemyConfigExcel.TryGetForRuntimeIndex(runtimeIndex, out var cfg))
    {
        float tierMult = 1f;
        try { tierMult = GetCurrentTierMultiplier(); } catch { tierMult = 1f; }

        // Excel最優先で適用（TryApply と同等の反映内容）
        enemyMaxHP = Mathf.Max(1, Mathf.RoundToInt(cfg.maxHP * tierMult));

        // ★修正：キー名を "Run_HasSuspend" に統一（旧: "PF_SUSPEND_FLAG" は存在しないキーだった）
        bool isSuspendedResume = false;
        try { isSuspendedResume = (PlayerPrefs.GetInt("Run_HasSuspend", 0) == 1); } catch { isSuspendedResume = false; }

        if (!isSuspendedResume)
        {
            enemyHP = enemyMaxHP;
        }
        else
        {
            // 既に復元されている enemyHP を保持しつつ、最大HPにだけ丸める
            enemyHP = Mathf.Clamp(enemyHP, 0, enemyMaxHP);
        }

        UpdateHpUI(); // ★ここでもUIを更新

        // ★追加：敵スキル設定を EnemySkills_Addon 側に渡す
        EnemySkills_SetFromConfig(cfg, runtimeIndex);

        try { TryLoadEnemyRunPortraitByName(cfg.name); } catch {}
        try { RefreshEnemyNameUIFromCurrentConfig(); } catch {}

        UpdateHpUI();     // HPバー即反映

        // ★重要：敵デッキは GameManager 側の BuildEnemyDeck() が "Excelのみ参照" で生成する
        BuildEnemyDeck();

        RefreshTopUI();

        _excelEnemyApplied = true; // ★追加：AutoApply による後から上書きを防ぐ
    }
}
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
private static void _EnemyConfigExcelAutoApply()
{
    var gm = Object.FindAnyObjectByType<GameManager>();
    if (!gm) return; // シーンに存在しない場合は無視

    // ★修正：キー名を "Run_HasSuspend" に統一
    try { if (PlayerPrefs.GetInt("Run_HasSuspend", 0) == 1) return; } catch {}

    // ★重要：すでに GameManager 側で Excel 適用済みなら、AutoApply で上書きしない
    try { if (gm._excelEnemyApplied) return; } catch {}

    // ★重要：idx の決め方を GameManager と同じにする（ProgressionFlowController 優先）
    int idx = 0;
    bool idxResolved = false;
    try { idx = ProgressionFlowController.GetCurrentEnemyIndex(); idxResolved = true; } catch {}
    if (!idxResolved)
    {
        try { idx = Mathf.Max(0, PlayerData.CurrentEnemy); idxResolved = true; } catch {}
    }

    gm.ApplyEnemyConfigFromExcel(idx);
}
#if UNITY_EDITOR
[UnityEditor.MenuItem("Tools/EnemyConfig/Reapply Excel Override %#e")]
private static void Menu_ReapplyExcel()
{
    var gm = Object.FindObjectOfType<GameManager>();
    if (!gm) { Debug.LogWarning("[EnemyConfig] GameManager not found in scene."); return; }

    int idx = 0;
    bool idxResolved = false;
    try { idx = ProgressionFlowController.GetCurrentEnemyIndex(); idxResolved = true; } catch {}
    if (!idxResolved)
    {
        try { idx = Mathf.Max(0, PlayerData.CurrentEnemy); idxResolved = true; } catch {}
    }

    gm.ApplyEnemyConfigFromExcel(idx);
    Debug.Log("[EnemyConfig] Reapplied Excel override.");
}
#endif

}
