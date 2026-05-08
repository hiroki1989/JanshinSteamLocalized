using System.Collections;
using UnityEngine;

// Tiny bootstrapper to invoke the progression init without touching base GameManager methods.
// Attach this to any GameObject in the battle scene (e.g., the same object as GameManager).
[DefaultExecutionOrder(10000)]
public class GameManager_Progression_Bootstrap : MonoBehaviour
{
    [SerializeField] private float waitTimeout = 5f;

    private static bool IsAlive(Object o)
    {
        return o != null && !ReferenceEquals(o, null);
    }

    private IEnumerator Start()
    {
        float t = 0f;
        GameManager gm = null;

        while (t < waitTimeout)
        {
            gm = FindObjectOfType<GameManager>();
            if (IsAlive(gm)) break;

            yield return null;
            t += Time.unscaledDeltaTime;
        }

        if (!IsAlive(gm))
            yield break;

        try
        {
            gm.SendMessage("__Progression_InternalInit", SendMessageOptions.DontRequireReceiver);
        }
        catch (MissingReferenceException)
        {
        }
        catch (System.MissingMethodException)
        {
        }
        catch (System.Exception)
        {
        }
    }
}