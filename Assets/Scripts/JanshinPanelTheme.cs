using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Saved on authored panel faces. Does not move controls or replace their events.
[DisallowMultipleComponent]
public sealed class JanshinPanelTheme : MonoBehaviour
{
    public bool preserveRarityGradient;
    public Image face;
    public Image border;
    public static readonly Color Ink = new Color(.035f,.047f,.055f,.97f);
    public static readonly Color Ivory = new Color(.96f,.92f,.81f);
    public static readonly Color Gold = new Color(.77f,.62f,.37f);
    static Sprite frame, fill;
    public static Sprite Frame => frame ? frame : (frame=Resources.Load<Sprite>("TrialBattleUI/AntiqueFrame"));
    public static Sprite Fill => fill ? fill : (fill=Resources.Load<Sprite>("UnifiedUI/InkSurface"));
    public static bool IsTileControl(Button b)
    {
        string n=b.name.ToLowerInvariant();
        if(b.transform.Find("Art")||n.StartsWith("tile")||n.StartsWith("equipped_")||n.StartsWith("imageman")||n.StartsWith("imagepin")||n.StartsWith("imagesou")||n=="imagewhite")return true;
        var im=b.GetComponent<Image>();
        return im&&im.sprite&&System.Text.RegularExpressions.Regex.IsMatch(im.sprite.name,@"^(Man|Pin|Sou|man|pin|sou)[1-9]");
    }

    public static void Text(TMP_Text text)
    {
        if(!text)return;
        var c=text.color;
        // Keep rarity, warning, status and rich-text colors intact.
        if(Mathf.Max(c.r,c.g,c.b)-Mathf.Min(c.r,c.g,c.b)<.16f && c.a>.01f)
            {var button=text.GetComponentInParent<Button>();var ink=button&&!button.IsInteractable()?new Color(.53f,.51f,.46f):Ivory;text.color=new Color(ink.r,ink.g,ink.b,c.a);}
    }
    public static JanshinPanelTheme Apply(Image image,bool rarity=false)
    {
        if(!image)return null;
        var theme=image.GetComponent<JanshinPanelTheme>()??image.gameObject.AddComponent<JanshinPanelTheme>();
        theme.face=image;theme.preserveRarityGradient=rarity;
        if(!rarity)image.color=Color.white;
        var existing=image.transform.Find("UnifiedGoldBorder");
        if(existing)theme.border=existing.GetComponent<Image>();
        if(!theme.border){
            var go=new GameObject("UnifiedGoldBorder",typeof(RectTransform),typeof(Image));go.transform.SetParent(image.transform,false);
            theme.border=go.GetComponent<Image>();
        }
        var r=theme.border.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;
        r.offsetMin=r.offsetMax=Vector2.zero;r.localScale=Vector3.one;
        theme.border.sprite=Frame;theme.border.type=Image.Type.Sliced;theme.border.pixelsPerUnitMultiplier=5;
        theme.border.color=Color.white;theme.border.raycastTarget=false;
        theme.border.transform.SetAsLastSibling();
        var le=theme.border.GetComponent<LayoutElement>()??theme.border.gameObject.AddComponent<LayoutElement>();le.ignoreLayout=true;
        theme.Refresh();return theme;
    }
    public static void Button(Button button)
    {
        if(!button||IsTileControl(button))return;
        var image=button.targetGraphic as Image;
        if(!image || image.transform!=button.transform)image=button.GetComponent<Image>();
        if(!image)return;
        Apply(image);button.targetGraphic=image;button.transition=Selectable.Transition.ColorTint;
        var c=button.colors;c.normalColor=Color.white;c.highlightedColor=new Color(1.3f,1.22f,1.06f);
        c.pressedColor=new Color(.68f,.57f,.39f);c.selectedColor=new Color(1.15f,1.08f,.94f);
        c.disabledColor=new Color(.42f,.44f,.47f,1);c.fadeDuration=.12f;button.colors=c;
        foreach(var t in button.GetComponentsInChildren<TMP_Text>(true))Text(t);
    }
    void LateUpdate(){Refresh();}
    void Refresh()
    {
        if(!face)return;
        var legacyGradient=face.GetComponent<UIGradient>();if(legacyGradient)legacyGradient.enabled=false;
        if(!preserveRarityGradient){face.sprite=Fill;face.overrideSprite=null;face.type=Image.Type.Simple;face.preserveAspect=false;}
        // Selection tint belongs to the inventory controller. Never reset it each frame.
        if(border){border.sprite=Frame;border.color=face.color.maxColorComponent<.85f?new Color(1f,.8f,.35f):Color.white;border.raycastTarget=false;}
    }
}
