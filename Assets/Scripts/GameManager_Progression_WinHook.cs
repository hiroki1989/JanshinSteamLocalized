using UnityEngine;

// 現行の勝利遷移は GameManager.cs / ProgressionFlowController.cs 側が本流。
// このファイルは旧フックが残っていても危険な独自遷移をしないように、
// 既存の安全な ProgressionFlowController に委譲するだけにする。
public partial class GameManager : MonoBehaviour
{
    public void __Progression_OnWin_MinFix()
    {
        if (enemyHP > 0)
        {
            AdvanceToNextEnemy();
            return;
        }

        var inst = ProgressionFlowController.Instance;
        if (inst == null)
        {
            inst = Object.FindObjectOfType<ProgressionFlowController>(true);
            if (inst == null)
            {
                var go = new GameObject("ProgressionFlow");
                inst = go.AddComponent<ProgressionFlowController>();
            }
        }

        if (inst != null)
        {
            if (IsCurrentEnemySecretHades())
            {
                inst.GoFromSecretHadesClearToSecretAngelClear();
            }
            else
            {
                inst.GoFromBattleWinToUpgrade();
            }
        }
        else
        {
            Debug.LogError("[Progression] ProgressionFlowController が見つからないため遷移できません。");
        }
    }

    private bool IsCurrentEnemySecretHades()
    {
        try
        {
            if (ProgressionFlowController.GetCurrentEnemyIndex() == 10)
                return true;
        }
        catch { }

        try
        {
            string enemyName = GetCurrentEnemyBaseNameForResources();
            enemyName = (enemyName ?? "").Trim();

            if (enemyName.Contains("ハデス"))
                return true;

            if (enemyName.ToLowerInvariant().Contains("hades"))
                return true;
        }
        catch { }

        return false;
    }

    public void __Progression_ForceSetEnemyIndex(int index)
    {
        SetCurrentEnemyIndex(index);
    }
}