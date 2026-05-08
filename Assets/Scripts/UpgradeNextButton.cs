using UnityEngine;

public sealed class UpgradeNextButton : MonoBehaviour
{
    public void GoNext()
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
        inst.ForceAdvanceAndGoToNextEnemyConversation();
    }
}
