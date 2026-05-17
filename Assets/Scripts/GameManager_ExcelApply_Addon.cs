// GameManager_ExcelApply_Addon.cs
using UnityEngine;

public partial class GameManager : MonoBehaviour
{
public void ApplyExcelEnemyConfig(EnemyConfig cfg)
{
    if (cfg == null) return;

    // ★修正：中断復帰中はHP/デッキを上書きしない（スナップショット優先）
    bool isSuspendResume = false;
    try { isSuspendResume = (PlayerPrefs.GetInt("Run_HasSuspend", 0) == 1); } catch {}

    if (isSuspendResume)
    {
        // スナップショット復元済みの状態を壊さない
        // ポートレートと名前UIだけ更新して終了
        try { TryLoadEnemyBattlePortraitByName(cfg.name); } catch {}
        try { RefreshEnemyNameUIFromCurrentConfig(); } catch {}
        return;
    }

    // ★修正：既に Awake() の TryApplyExcelEnemyConfigForCurrentIndex() で
    //         適用済みなら、HP とデッキを二重上書きしない
    if (_excelEnemyApplied)
    {
        try { TryLoadEnemyBattlePortraitByName(cfg.name); } catch {}
        try { RefreshEnemyNameUIFromCurrentConfig(); } catch {}
        return;
    }

    // HP（Tier倍率を適用）
    float tierMult = 1f;
    try { tierMult = GetCurrentTierMultiplier(); } catch { tierMult = 1f; }
    enemyMaxHP = Mathf.Max(1, Mathf.RoundToInt(cfg.maxHP * tierMult));

    if (enemyHP <= 0 || enemyHP > enemyMaxHP) enemyHP = enemyMaxHP;

    // ★重要：敵デッキは GameManager 側の BuildEnemyDeck() が "Excelのみ参照" で生成する
    BuildEnemyDeck();

    // --- バトル用ポートレート（名前で自動ロード） ---
    try { TryLoadEnemyBattlePortraitByName(cfg.name); } catch {}

    // --- 敵名UIは必ずここで最新化 ---
    try { RefreshEnemyNameUIFromCurrentConfig(); } catch {}

    UpdateHpUI(); // HPバー更新
    RefreshTopUI();
}
}
