using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared brush artwork; game logic still owns timing, sprites, sounds and translations.
public sealed class NormalEyeCutin : MonoBehaviour
{
    public enum EventKind { Riichi, Ron, Tsumo, Skill }
    Image source, eyes, accent;
    RectTransform frame;
    TMP_Text originalLabel;
    TextMeshProUGUI label;
    EventKind kind;
    public System.Func<EventKind> resolveKind;
    static Sprite brush;
    Sprite lastSprite;
    Material labelMaterial;

    public void Configure(TMP_Text text, EventKind eventKind)
    {
        if (originalLabel && originalLabel != text) originalLabel.canvasRenderer.SetAlpha(1f);
        originalLabel = text;
        kind = eventKind;
        if (label) label.gameObject.SetActive(text != null);
    }
    void Awake() { source = GetComponent<Image>(); }
    void LateUpdate()
    {
        if (!source) return;
        Sprite sprite = source.sprite;
        bool strip = source.enabled && sprite && sprite.rect.height > 0 && sprite.rect.width / sprite.rect.height > 3f;
        if (!strip)
        {
            if (frame) frame.gameObject.SetActive(false);
            source.canvasRenderer.SetAlpha(1f);
            if (originalLabel) originalLabel.canvasRenderer.SetAlpha(1f);
            return;
        }
        if (!brush)
        {
            var texture = Resources.Load<Texture2D>("CutinPresentation/InkBrush");
            if (!texture) return;
            // Exclude the transparent padding without resampling the generated artwork.
            brush = Sprite.Create(texture, new Rect(0, texture.height * .26f, texture.width, texture.height * .48f), new Vector2(.5f,.5f), 100f);
        }
        if (!frame) Build();
        frame.gameObject.SetActive(true);
        float width = Mathf.Min(((RectTransform)transform).rect.width, 1420f);
        frame.sizeDelta = new Vector2(width, width * .21f);
        if (lastSprite != sprite) { lastSprite = sprite; eyes.sprite = sprite; }
        if (resolveKind != null) kind = resolveKind();
        Color tint = kind == EventKind.Riichi ? new Color(.90f,.69f,.32f) : kind == EventKind.Tsumo ? new Color(.80f,.91f,1f) : new Color(.82f,.13f,.10f);
        if (kind == EventKind.Skill) tint = new Color(.48f,.78f,.90f);
        accent.color = tint;
        if (label) label.fontSizeMin = kind == EventKind.Skill ? 20 : 36;
        if (originalLabel)
        {
            if (!labelMaterial || label.font != originalLabel.font)
            {
                label.font = originalLabel.font;
                if (labelMaterial) Destroy(labelMaterial);
                labelMaterial = new Material(originalLabel.fontSharedMaterial);
                labelMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, .18f);
                labelMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                label.fontSharedMaterial = labelMaterial;
            }
            label.text = originalLabel.text;
            label.color = kind == EventKind.Riichi ? tint : new Color(1f,.96f,.86f);
            originalLabel.canvasRenderer.SetAlpha(0f);
        }
        source.canvasRenderer.SetAlpha(0f);
    }
    void Build()
    {
        frame = new GameObject("InkEyeCutin", typeof(RectTransform)).GetComponent<RectTransform>();
        frame.SetParent(transform, false);
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f,.5f);
        Picture("InkBand", frame, Vector2.zero, Vector2.one, Color.black);
        var mask = Picture("EyeBrushMask", frame, new Vector2(.025f,.14f), new Vector2(.70f,.90f), Color.white);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        eyes = Picture("CharacterEyes", mask.rectTransform, Vector2.zero, Vector2.one, Color.white);
        eyes.sprite = source.sprite;
        eyes.preserveAspect = false;
        accent = Picture("FineAccent", frame, new Vector2(.035f,.12f), new Vector2(.94f,.145f), Color.white);
        label = new GameObject("EventTitle", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(frame, false);
        label.rectTransform.anchorMin = new Vector2(.65f,.03f);
        label.rectTransform.anchorMax = new Vector2(.97f,.97f);
        label.rectTransform.sizeDelta = Vector2.zero;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 36;
        label.fontSizeMax = 150;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.gameObject.SetActive(originalLabel != null);
    }
    static Image Picture(string name, RectTransform parent, Vector2 min, Vector2 max, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.rectTransform.anchorMin = min;
        image.rectTransform.anchorMax = max;
        image.rectTransform.sizeDelta = Vector2.zero;
        image.sprite = brush;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
    void OnDisable()
    {
        if (source) source.canvasRenderer.SetAlpha(1f);
        if (originalLabel) originalLabel.canvasRenderer.SetAlpha(1f);
    }
    void OnDestroy() { if (labelMaterial) Destroy(labelMaterial); }
}
