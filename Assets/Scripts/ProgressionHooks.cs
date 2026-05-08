using UnityEngine;

public static class ProgressionHooks
{
    public static void GoToRewardOnDefeat()
    {
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
        inst.GoFromBattleLoseToReward();
    }

    public static void GoToNextEnemyConversationFromUpgrade()
    {
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
        inst.ForceAdvanceAndGoToNextEnemyConversation();
    }
}
