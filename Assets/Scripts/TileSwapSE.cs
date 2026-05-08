using UnityEngine;
using UnityEngine.EventSystems;

public class TileSwapSE : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    private bool _dragStarted = false;

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragStarted = true;
        // 必要ならここで「掴んだときのSE」を鳴らしてもOK
        // if (AudioManager.Instance != null)
        // {
        //     AudioManager.Instance.PlaySelectTileSE();
        // }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragStarted) return;
        _dragStarted = false;

        if (AudioManager.Instance != null)
        {
            // ★修正：AudioManager のメソッド名に合わせる
            AudioManager.Instance.PlaySwapTileSE();
        }
    }
}
