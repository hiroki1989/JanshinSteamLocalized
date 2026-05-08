using UnityEngine;

public sealed class EnemyToBattleButton : MonoBehaviour
{
    public void GoBattle()
    {
        var inst = ProgressionFlowController.Instance;
        if (inst == null)
        {
            inst = GameObject.FindObjectOfType<ProgressionFlowController>(true);
            if (inst == null)
            {
                var go = new GameObject("ProgressionFlow");
                inst = go.AddComponent<ProgressionFlowController>();
            }
        }
        inst.GoFromEnemyConversationToBattle();
    }
}
