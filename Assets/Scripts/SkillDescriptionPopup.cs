using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public sealed class SkillDescriptionPopup : MonoBehaviour
{
    System.Action closed;
    float previousTimeScale;
    bool restored;
    public static SkillDescriptionPopup Show(Transform owner, string title, string description, System.Action onClose)
    {
        var go = new GameObject("ActiveSkillDescription", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SkillDescriptionPopup));
        go.transform.SetParent(owner, false);
        var popup = go.GetComponent<SkillDescriptionPopup>();
        popup.closed = onClose; popup.previousTimeScale = Time.timeScale; Time.timeScale = 0;
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 31000;
        var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
        var shade = ItemArtwork.EnsureIcon(go.transform,"DismissBackground");
        ItemArtwork.Rect(shade.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        shade.color = new Color(0,0,0,.72f); shade.raycastTarget = true;
        shade.gameObject.AddComponent<Button>().onClick.AddListener(popup.Close);
        var panel = ItemArtwork.EnsureIcon(go.transform,"Panel"); panel.color = new Color(.07f,.095f,.10f,1); panel.raycastTarget = true;
        ItemArtwork.Rect(panel.rectTransform,new Vector2(.16f,.16f),new Vector2(.84f,.84f),Vector2.zero,Vector2.zero);
        var heading = Label(panel.transform,"Title",title,42);
        ItemArtwork.Rect(heading.rectTransform,new Vector2(0,.78f),new Vector2(1,.97f),new Vector2(32,0),new Vector2(-32,0));
        heading.alignment = TextAlignmentOptions.Center; heading.color = new Color(1,.83f,.48f);
        var viewport = new GameObject("DescriptionViewport",typeof(RectTransform),typeof(RectMask2D),typeof(Image),typeof(ScrollRect));
        viewport.transform.SetParent(panel.transform,false);
        var vr = viewport.GetComponent<RectTransform>();
        ItemArtwork.Rect(vr,new Vector2(0,.22f),new Vector2(1,.77f),new Vector2(38,0),new Vector2(-38,0));
        viewport.GetComponent<Image>().color = Color.clear;
        var body = Label(viewport.transform,"Description",description,32);
        body.enableAutoSizing = false; body.fontSize = 32; body.alignment = TextAlignmentOptions.TopLeft;
        body.overflowMode = TextOverflowModes.Overflow;
        ItemArtwork.Rect(body.rectTransform,new Vector2(0,1),Vector2.one,Vector2.zero,Vector2.zero);
        body.rectTransform.pivot = new Vector2(.5f,1);
        var fitter = body.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.GetComponent<ScrollRect>(); scroll.viewport = vr; scroll.content = body.rectTransform; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        var close = ItemArtwork.EnsureIcon(panel.transform,"CloseButton"); close.color = new Color(.22f,.30f,.34f,1); close.raycastTarget = true;
        ItemArtwork.Rect(close.rectTransform,new Vector2(.32f,.055f),new Vector2(.68f,.18f),Vector2.zero,Vector2.zero);
        close.gameObject.AddComponent<Button>().onClick.AddListener(popup.Close);
        var lang = LocalizationManager.Instance ? LocalizationManager.Instance.CurrentLanguage : LocalizationManager.Language.Japanese;
        var closeText = Label(close.transform,"Label",lang == LocalizationManager.Language.English ? "Close" : lang == LocalizationManager.Language.ChineseSimplified ? "关闭" : "閉じる",30);
        ItemArtwork.Rect(closeText.rectTransform,Vector2.zero,Vector2.one,new Vector2(8,4),new Vector2(-8,-4)); closeText.alignment = TextAlignmentOptions.Center;
        return popup;
    }
    static TextMeshProUGUI Label(Transform parent,string name,string value,float size)
    {
        var go = new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
        var tmp = go.GetComponent<TextMeshProUGUI>(); tmp.font = TMP_Settings.defaultFontAsset; tmp.text = value;
        tmp.color = new Color(.96f,.94f,.87f); tmp.raycastTarget = false; ItemArtwork.Text(tmp,size); return tmp;
    }
    void Update() { if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close(); }
    public void Close() { Restore(); Destroy(gameObject); }
    void OnDisable() { Restore(); }
    void Restore() { if (restored) return; restored=true; Time.timeScale=previousTimeScale; closed?.Invoke(); closed=null; }
}
