using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonClickSEBinder : MonoBehaviour
{
    private void Awake()
    {
        var btn = GetComponent<Button>();
        btn.onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        Debug.Log("[ButtonClickSEBinder] OnClicked called on " + gameObject.name);

        if (AudioManager.Instance == null)
        {
            Debug.LogError("[ButtonClickSEBinder] AudioManager.Instance is NULL!");
            return;
        }

        AudioManager.Instance.PlayClickSE();
    }
}
