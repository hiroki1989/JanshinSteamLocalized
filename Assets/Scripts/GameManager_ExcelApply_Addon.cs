// GameManager_ExcelApply_Addon.cs
using UnityEngine;

public partial class GameManager : MonoBehaviour
{
public void ApplyExcelEnemyConfig(EnemyConfig cfg)
{
    if (cfg == null) return;

    // HP
    enemyMaxHP = Mathf.Max(1, cfg.maxHP);
    if (enemyHP <= 0 || enemyHP > enemyMaxHP) enemyHP = enemyMaxHP;

    // ★重要：敵デッキは GameManager 側の BuildEnemyDeck() が “Excelのみ参照” で生成する
    BuildEnemyDeck();

    // --- バトル用ポートレート（名前で自動ロード） ---
    try { TryLoadEnemyBattlePortraitByName(cfg.name); } catch {}

    // --- 敵名UIは必ずここで最新化 ---
    try { RefreshEnemyNameUIFromCurrentConfig(); } catch {}

    UpdateHpUI(); // HPバー更新
    RefreshTopUI();
}
}