// Assets/Scripts/GameManager_DragSwap.cs
// Drag & Drop swap between HandArea and OfferArea.
// - Drag a tile and drop onto a tile in the other area to swap.
// - Swaps are allowed any number of times until [捨てる].
// - Hand is auto-sorted (リーパイ) after every swap.
// This file extends the existing GameManager (declared as partial) without modifying other logic.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class GameManager : MonoBehaviour
{
    // Area identification for drag-swap
    private enum SwapArea { Hand, Offer }

    // ---- Public API (kept private within partial class) ----
    // Attach watchers on enable so tiles get drag components whenever UI is rebuilt.
    private void OnEnable()
    {
        TryAttachAreaWatcher(handArea, SwapArea.Hand);
        TryAttachAreaWatcher(offerArea, SwapArea.Offer);
        // Initial attach (in case children already exist)
        AttachDraggers(SwapArea.Hand);
        AttachDraggers(SwapArea.Offer);
    }

    // Watcher to re-wire children when UI refreshes hand/offer.
    private class AreaWatcher : MonoBehaviour
    {
        public GameManager gm;
        public SwapArea area;
        void OnTransformChildrenChanged()
        {
            if (gm) gm.AttachDraggers(area);
        }
        void Start()
        {
            if (gm) gm.AttachDraggers(area);
        }
    }

    private void TryAttachAreaWatcher(RectTransform parent, SwapArea area)
    {
        if (!parent) return;
        var w = parent.GetComponent<AreaWatcher>();
        if (!w) w = parent.gameObject.AddComponent<AreaWatcher>();
        w.gm = this;
        w.area = area;
    }

    // Add (or update) DragSwap to each child tile under the given area
    private void AttachDraggers(SwapArea area)
    {
        RectTransform parent = area == SwapArea.Hand ? handArea : offerArea;
        if (!parent) return;

        int count = Mathf.Min(parent.childCount, area == SwapArea.Hand ? hand.Count : offers.Count);
        for (int i = 0; i < count; i++)
        {
            var child = parent.GetChild(i);
            var drag = child.GetComponent<DragSwap>();
            if (!drag) drag = child.gameObject.AddComponent<DragSwap>();
            drag.Init(this, area, i, GetTileSpriteFrom(child));
        }
    }
    // Helper: obtain the Sprite used on this tile's visible art image
    // 旧バージョンの見た目を確実に出すため、nullのときは強めにフォールバックする
    private Sprite GetTileSpriteFrom(Transform tile)
    {
        if (!tile) return null;

        // 1) 既存の可視アート取得（GameManager側の実装）
        Image img = GetVisibleArtImage(tile);
        if (img && img.sprite) return img.sprite;

        // 2) 子の "Art" を優先
        var artTf = tile.Find("Art");
        if (artTf)
        {
            var artImg = artTf.GetComponent<Image>();
            if (artImg && artImg.sprite) return artImg.sprite;
        }

        // 3) 子階層の Image を総当り（非表示含む）
        var imgs = tile.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < imgs.Length; i++)
        {
            if (imgs[i] && imgs[i].sprite) return imgs[i].sprite;
        }

        return null;
    }
    // Perform the swap logic, respecting the current phase
    private void PerformSwap(SwapArea a1, int index1, SwapArea a2, int index2)
    {
        if (phase != Phase.Offer) return;                         // 入れ替えは自分のオファー番のみ
        if (a1 == a2) return;                                     // 同一エリア間は不可
        if (index1 < 0 || index2 < 0) return;

        // Ensure indices are valid against current lists
        if (a1 == SwapArea.Hand && index1 >= hand.Count) return;
        if (a1 == SwapArea.Offer && index1 >= offers.Count) return;
        if (a2 == SwapArea.Hand && index2 >= hand.Count) return;
        if (a2 == SwapArea.Offer && index2 >= offers.Count) return;

        // Execute swap: hand[i] <-> offers[j]
        if (a1 == SwapArea.Hand && a2 == SwapArea.Offer)
        {
            string tmp = hand[index1];
            hand[index1] = offers[index2];
            offers[index2] = tmp;
        }
        else if (a1 == SwapArea.Offer && a2 == SwapArea.Hand)
        {
            string tmp = offers[index1];
            offers[index1] = hand[index2];
            hand[index2] = tmp;
        }

        // リーパイ & UI再描画（既存フローを維持）
        SortHand();
        UpdateTenpaiBadge();   // テンパイ更新
        RefreshHandUI();
        RefreshOfferUI();
        UpdateButtons();       // リーチボタン表示など既存制御
        EvaluateWinUI_New();   // 勝利UIの事前評価（必要なら）
    }
    private class DragSwap : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private GameManager gm;
        private SwapArea area;
        private int index;

        private GameObject ghost;
        private RectTransform ghostRT;
        private Canvas topCanvas;
        private Sprite sprite;

        private CanvasGroup selfCG;
        private float selfAlphaBefore;

        public void Init(GameManager gm, SwapArea area, int index, Sprite sprite)
        {
            this.gm = gm;
            this.area = area;
            this.index = index;
            this.sprite = sprite;
        }
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!gm || gm.phase != Phase.Offer || gm.isRiichi) return; // drag only during offer & not after riichi

            // ★重要：sprite が null なら見えない DragGhost を作ってしまうので中断する
            if (sprite == null) return;

            topCanvas = GetTopCanvas();
            var parentTf = topCanvas ? topCanvas.transform : gm.transform;

            ghost = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Canvas));
            ghostRT = ghost.transform as RectTransform;
            ghost.transform.SetParent(parentTf, false);

            // ★重要：最前面に出す（潜って見えない対策）
            var ghostCanvas = ghost.GetComponent<Canvas>();
            ghostCanvas.overrideSorting = true;
            ghostCanvas.sortingOrder = 9999;

            ghost.transform.SetAsLastSibling();

            var img = ghost.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.color = new Color(1f, 1f, 1f, 0.9f);
            img.sprite = sprite;

            // サイズは固定ではなく、元タイルのRectに合わせる（旧演出に近づく）
            var srcRT = transform as RectTransform;
            if (srcRT)
            {
                ghostRT.sizeDelta = srcRT.rect.size;
                ghostRT.localScale = srcRT.localScale;
            }
            else
            {
                ghostRT.sizeDelta = new Vector2(64, 88);
            }

            UpdateGhostToPointer(eventData);
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (!ghostRT) return;

            // ★希望どおり：常にカーソルに追従（掴み続けている見た目）
            UpdateGhostToPointer(eventData);
        }
        public void OnEndDrag(PointerEventData eventData)
        {
            if (ghost) GameObject.Destroy(ghost);

            // 元牌を復帰
            if (selfCG)
            {
                selfCG.alpha = selfAlphaBefore;
                selfCG.blocksRaycasts = true;
            }

            if (!gm || gm.phase != Phase.Offer || gm.isRiichi) return;

            // ドロップ先を確定して入れ替え
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            foreach (var r in results)
            {
                var target = r.gameObject.GetComponentInParent<DragSwap>();
                if (target && target.gm == gm)
                {
                    if (target.area != this.area)
                    {
                        gm.PerformSwap(this.area, this.index, target.area, target.index);
                    }
                    break;
                }
            }
        }
        private void UpdateGhostToPointer(PointerEventData eventData)
        {
            if (!ghostRT) return;

            // topCanvas は OnBeginDrag で取得済み。無い場合は従来どおり position を使う
            if (!topCanvas)
            {
                ghostRT.position = eventData.position;
                return;
            }

            // ScreenSpaceOverlay / ScreenSpaceCamera 両対応
            var canvasRT = topCanvas.transform as RectTransform;
            if (!canvasRT)
            {
                ghostRT.position = eventData.position;
                return;
            }

            var cam = (topCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : topCanvas.worldCamera;

            Vector2 localPos;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, eventData.position, cam, out localPos))
            {
                ghostRT.anchoredPosition = localPos;
            }
            else
            {
                ghostRT.position = eventData.position;
            }
        }
        private Canvas GetTopCanvas()
        {
            Canvas c = null;
            Transform t = transform;
            while (t != null)
            {
                c = t.GetComponent<Canvas>();
                if (c && c.isRootCanvas) return c;
                t = t.parent;
            }
            return c;
        }
    }
}
