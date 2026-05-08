using UnityEngine;
using UnityEngine.EventSystems;

public class TileClickSE : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (AudioManager.Instance != null)
        {
            // ★修正：AudioManager のメソッド名に合わせる
            AudioManager.Instance.PlaySelectTileSE();
        }
    }
}
