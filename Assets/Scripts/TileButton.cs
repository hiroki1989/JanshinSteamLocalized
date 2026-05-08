using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 牌ボタン。必ず Button と Image を持ち、onClick を自分で配線する。
/// 見た目の上下移動は Art（RectTransform）だけを動かすので、LayoutGroup に負けない。
/// 互換メソッド: SetHighlight(bool), SetAura(...), SetGrayed(bool)
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class TileButton : MonoBehaviour
{
    [Header("Optional refs")]
    public RectTransform artRoot;     // 見た目だけを上下させる枠（無指定なら自動）
    public Image image;               // 牌の見た目（無指定なら自動）
    public Image auraOverlay;         // オーラ表示用の薄いオーバーレイ（無ければ自動生成）

    [Header("Raise effect")]
    public float raiseY = 28f;

    // 状態
    public int HandIndex { get; private set; } = -1;
    public string TileId { get; private set; } = "";
    GameManager gm;
    RectTransform targetRt;
    Vector2 basePos;
    bool inited;

void Awake() {
    if (!artRoot) artRoot = transform.Find("Art") as RectTransform;
    if (!image) image = artRoot.GetComponentInChildren<Image>(true);

    // ルート直下に余分な Image がいたら描画もレイキャストも止める
    foreach (var img in GetComponentsInChildren<UnityEngine.UI.Image>(true)) {
        bool isRootImageSiblingOfArt = (img.transform.parent == transform && img.transform != artRoot);
        if (isRootImageSiblingOfArt) {
            img.enabled = false;
            img.raycastTarget = false;
            var cg = img.GetComponent<CanvasGroup>() ?? img.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
        }
    }
}

    void InitIfNeeded()
    {
        if (inited) return;

        if (!artRoot)
        {
            var t = transform.Find("Art") as RectTransform;
            artRoot = t ? t : GetComponent<RectTransform>();
        }
        if (!image)
        {
            if (artRoot)
            {
                var img = artRoot.GetComponentInChildren<Image>();
                if (img) image = img;
            }
            if (!image) image = GetComponent<Image>(); // 最終手段
        }
        // 子の見た目 Image は Raycast しない（親で受ける）
        if (image) image.raycastTarget = false;

        // オーラ用オーバーレイを用意（無ければ生成）
        if (!auraOverlay)
        {
            var aura = transform.Find("Aura") as RectTransform;
            if (!aura)
            {
                var go = new GameObject("Aura", typeof(RectTransform), typeof(Image));
                aura = go.GetComponent<RectTransform>();
                aura.SetParent(artRoot ? artRoot : transform, false);
                aura.anchorMin = Vector2.zero;
                aura.anchorMax = Vector2.one;
                aura.offsetMin = Vector2.zero;
                aura.offsetMax = Vector2.zero;
            }
            auraOverlay = aura.GetComponent<Image>();
            auraOverlay.raycastTarget = false;
            auraOverlay.color = new Color(0.2f, 0.6f, 1f, 0.28f); // 薄い青
            auraOverlay.enabled = false;
        }

        targetRt = artRoot ? artRoot : GetComponent<RectTransform>();
        basePos = targetRt.anchoredPosition;
        inited = true;
    }

    /// <summary>GameManager からの初期化</summary>
    public void SetTile(string tileResId, int index, GameManager manager)
    {
        InitIfNeeded();
        HandIndex = index;
        TileId = tileResId;
        gm = manager;

        if (image)
        {
            var sp = Resources.Load<Sprite>("Sprites/Tiles/" + tileResId);
            image.sprite = sp;
            image.preserveAspect = true;
        }
        SetRaised(false);
        SetGrayed(false);
        SetAura(AuraType.None);
    }

    /// <summary>上に少しずらす（選択演出）</summary>
    public void SetRaised(bool on)
    {
        InitIfNeeded();
        if (!targetRt) return;
        var p = basePos;
        if (on) p.y += raiseY;
        targetRt.anchoredPosition = p;
    }

    /// <summary>グレー化（無効演出）</summary>
    public void SetGrayed(bool on)
    {
        if (!image) return;
        image.color = on ? new Color(1f, 1f, 1f, 0.4f) : Color.white;
    }

    // ===== 互換メソッド =====

    /// <summary>旧API互換: SetHighlight(true/false) → 上にずらす</summary>
    public void SetHighlight(bool on) => SetRaised(on);

    public enum AuraType { None = 0, Blue = 1 }

    /// <summary>旧API互換: SetAura(bool) / SetAura(int) / SetAura(AuraType)</summary>
    public void SetAura(bool on) => SetAura(on ? AuraType.Blue : AuraType.None);
    public void SetAura(int type) => SetAura(type != 0 ? AuraType.Blue : AuraType.None);
    public void SetAura(AuraType type)
    {
        InitIfNeeded();
        if (!auraOverlay) return;
        if (type == AuraType.None)
        {
            auraOverlay.enabled = false;
        }
        else
        {
            auraOverlay.enabled = true;
            // 視認性を上げたい場合は色やアルファを調整
            auraOverlay.color = new Color(0.2f, 0.6f, 1f, 0.28f);
        }
    }

    // ===== クリック =====
    void OnClickSelf()
    {
        if (gm == null || HandIndex < 0) return;
        gm.OnHandTileClicked(HandIndex); // 必ず GameManager に通知
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!artRoot)
        {
            var t = transform.Find("Art") as RectTransform;
            if (t) artRoot = t;
        }
        if (!image && artRoot)
        {
            var img = artRoot.GetComponentInChildren<Image>();
            if (img) image = img;
        }
    }
#endif
}
