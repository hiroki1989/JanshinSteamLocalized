using UnityEngine;
public sealed class CommerceBootstrap : MonoBehaviour
{
    void Awake() {
        if (AdsInitializer.Instance) return;
        var prefab = Resources.Load<GameObject>("Commerce/Services");
        if (prefab) Instantiate(prefab);
    }
}
