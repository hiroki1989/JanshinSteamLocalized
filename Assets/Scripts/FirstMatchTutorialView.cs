using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
/// <summary>A scene-independent, modal uGUI coach. Never changes gameplay tiles or their hierarchy.</summary>
public sealed class FirstMatchTutorialView : MonoBehaviour
{
    public sealed class Page
    {
        public readonly string Title, Body, Hint;
        public readonly Transform[] Targets;
        public string[] Tiles;
        public bool CompactBody;
        public Page(string title, string body, string hint, Transform[] targets)
        { Title = title; Body = body; Hint = hint; Targets = targets; }
    }

    private List<Page> pages;
    private Action completed;
    private LocalizationManager.Language language;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private RectTransform root, card, illustration, safe;
    [SerializeField] private List<RectTransform> shades = new List<RectTransform>();
    [SerializeField] private RectTransform focus;
    [SerializeField] private TextMeshProUGUI titleText, bodyText, bodyExampleText, hintText, countText, nextText, backText, skipText;
    [SerializeField] private Button next, back, skip;
    [SerializeField] private Image progress;
    private int index;
    private bool confirmingSkip, closing;
    private readonly Vector3[] corners = new Vector3[4];
    private readonly Color gold = new Color(.94f, .76f, .4f);
    private readonly Color ink = new Color(.055f, .075f, .11f);

    private string T(string ja, string en, string zh) =>
        language == LocalizationManager.Language.English ? en :
        language == LocalizationManager.Language.ChineseSimplified ? zh : ja;

    [Header("Editable content")]
    public FirstMatchTutorialContent contentAsset;
    [Header("Layout behaviour")]
    [Tooltip("Off: preserve GuideCard's authored RectTransform position. On: place it away from highlighted UI.")]
    [SerializeField] private bool autoPositionCard = true;
    [SerializeField, Min(0)] private float screenPadding = 20;
    [Tooltip("Off: preserve the font assigned to each TMP object in this prefab.")]
    [SerializeField] private bool useLocalizedFonts = true;
    private Vector2 authoredCardSize;
    private bool capturedLayout;
    private float normalBodyMinimum, normalBodySpacing, exampleBodyMinimum, exampleBodySpacing;

    internal void Build(List<Page> content, TMP_FontAsset bodyFont,
        LocalizationManager.Language selectedLanguage, Action onComplete)
    {
        if (!card || !bodyText || !next || !back || !skip || !contentAsset)
            throw new InvalidOperationException("Tutorial prefab is missing its UI references or content asset.");
        if (content == null || content.Count == 0)
            throw new InvalidOperationException("Tutorial content must contain at least one visible page.");
        if (Application.isPlaying) GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        pages = content;
        language = selectedLanguage;
        completed = onComplete;
        index = 0; closing = false; confirmingSkip = false;
        root = (RectTransform)transform;
        if (!capturedLayout)
        {
            authoredCardSize = card.sizeDelta;
            normalBodyMinimum = bodyText.fontSizeMin; normalBodySpacing = bodyText.lineSpacing;
            exampleBodyMinimum = bodyExampleText.fontSizeMin; exampleBodySpacing = bodyExampleText.lineSpacing;
            capturedLayout = true;
        }
        if (useLocalizedFonts)
        {
            font = TMP_Settings.defaultFontAsset;
            if (!font) font = bodyFont ? bodyFont : TMP_Settings.defaultFontAsset;
            foreach (var text in GetComponentsInChildren<TextMeshProUGUI>(true))
                if (font) text.font = font;
        }
        backText.text = contentAsset.back.Get(language);
        skipText.text = contentAsset.skip.Get(language);
        var eyebrow = card.Find("Eyebrow");
        if (eyebrow) eyebrow.GetComponent<TextMeshProUGUI>().text = contentAsset.guideLabel.Get(language);
        next.onClick.RemoveListener(Advance);
        back.onClick.RemoveListener(GoBack);
        skip.onClick.RemoveListener(AskSkip);
        next.onClick.AddListener(Advance);
        back.onClick.AddListener(GoBack);
        skip.onClick.AddListener(AskSkip);
        next.interactable = true;
        ShowPage();
    }

    private void GoBack() { if (confirmingSkip) confirmingSkip = false; else index = Mathf.Max(0, index - 1); ShowPage(); }
    private void AskSkip() { confirmingSkip = true; ShowPage(); }

    public void PreviewPage(int pageIndex, LocalizationManager.Language previewLanguage)
    {
        if (!contentAsset) return;
        Build(contentAsset.Resolve(previewLanguage), font, previewLanguage, null);
        index = Mathf.Clamp(pageIndex, 0, pages.Count - 1);
        ShowPage();
    }

    public void CreateDefaultHierarchy(List<Page> content, TMP_FontAsset bodyFont,
        LocalizationManager.Language selectedLanguage, Action onComplete)
    {
        pages = content;
        font = TMP_Settings.defaultFontAsset;
        if (!font) font = bodyFont ? bodyFont : TMP_Settings.defaultFontAsset;
        language = selectedLanguage;
        completed = onComplete;
        root = (RectTransform)transform;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();
        Box("InputBlocker", root, Color.clear, Vector2.zero, Vector2.one).GetComponent<Image>().raycastTarget = true;
        for (int i = 0; i < 4; i++)
            shades.Add(Box("Dim" + i, root, new Color(0, 0, 0, .73f), Vector2.zero, Vector2.one));
        focus = Box("Focus", root, Color.clear, Vector2.zero, Vector2.one);
        Box("Top", focus, gold, new Vector2(0, 1), Vector2.one, new Vector2(0, -3), Vector2.zero);
        Box("Bottom", focus, gold, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 3));
        Box("Left", focus, gold, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(3, 0));
        Box("Right", focus, gold, new Vector2(1, 0), Vector2.one, new Vector2(-3, 0), Vector2.zero);
        safe = Box("SafeArea", root, Color.clear, Vector2.zero, Vector2.one);
        card = Box("GuideCard", safe, ink, Vector2.zero, Vector2.zero);
        Box("Accent", card, gold, new Vector2(0, 1), Vector2.one, new Vector2(0, -4), Vector2.zero);
        Text("Eyebrow", card, "JANSHIN  /  " + T("対局ガイド", "MATCH GUIDE", "对局指南"), 17, gold,
            new Vector2(.06f, .905f), new Vector2(.72f, .95f));
        countText = Text("Step", card, "", 19, gold, new Vector2(.75f, .905f), new Vector2(.94f, .95f));
        countText.alignment = TextAlignmentOptions.Right;
        titleText = Text("Title", card, "", 34, Color.white, new Vector2(.06f, .78f), new Vector2(.94f, .89f));
        titleText.fontStyle = FontStyles.Bold;
        bodyText = Text("Body", card, "", 25, new Color(.9f, .92f, .95f),
            new Vector2(.06f, .31f), new Vector2(.94f, .76f));
        bodyText.lineSpacing = 8;
        bodyExampleText = Instantiate(bodyText, card);
        bodyExampleText.name = "BodyWithExample";
        bodyExampleText.rectTransform.anchorMin = new Vector2(.06f, .40f);
        illustration = Box("ExampleTiles", card, Color.clear, new Vector2(.06f, .28f), new Vector2(.94f, .37f));
        hintText = Text("Hint", card, "", 19, gold, new Vector2(.06f, .145f), new Vector2(.94f, .255f));
        back = MakeButton("Back", T("戻る", "Back", "返回"), new Vector2(.06f, .04f), new Vector2(.24f, .12f),
            false, () => { if (confirmingSkip) confirmingSkip = false; else index = Mathf.Max(0, index - 1); ShowPage(); }, out backText);
        skip = MakeButton("Skip", T("スキップ", "Skip", "跳过"), new Vector2(.27f, .04f), new Vector2(.49f, .12f),
            false, () => { confirmingSkip = true; ShowPage(); }, out skipText);
        next = MakeButton("Next", "", new Vector2(.53f, .04f), new Vector2(.94f, .12f),
            true, Advance, out nextText);
        var track = Box("ProgressTrack", card, new Color(.18f, .21f, .27f),
            new Vector2(.06f, .015f), new Vector2(.94f, .019f));
        progress = Box("Progress", track, gold, Vector2.zero, Vector2.one).GetComponent<Image>();
        card.anchorMin = card.anchorMax = new Vector2(.5f, .5f);
        card.sizeDelta = authoredCardSize = new Vector2(700, 700);
        capturedLayout = true;
        Canvas.ForceUpdateCanvases();
        Layout();
        ShowPage();
    }

    internal static void Release(UnityEngine.Object target)
    {
        if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
    }

    private void Advance()
    {
        if (closing) return;
        if (confirmingSkip || index == pages.Count - 1)
        {
            closing = true;
            next.interactable = back.interactable = skip.interactable = false;
            completed?.Invoke();
        }
        else { index++; ShowPage(); }
    }

    private void ShowPage()
    {
        var p = pages[index];
        titleText.text = confirmingSkip ? contentAsset.skipTitle.Get(language) : p.Title;
        bodyText.text = confirmingSkip ? contentAsset.skipBody.Get(language) : p.Body;
        bodyExampleText.text = bodyText.text;
        bool compact = !confirmingSkip && p.CompactBody;
        bodyText.fontSizeMin = compact ? Mathf.Min(normalBodyMinimum, 18) : normalBodyMinimum;
        bodyText.lineSpacing = compact ? 0 : normalBodySpacing;
        bodyExampleText.fontSizeMin = compact ? Mathf.Min(exampleBodyMinimum, 18) : exampleBodyMinimum;
        bodyExampleText.lineSpacing = compact ? 0 : exampleBodySpacing;
        hintText.text = confirmingSkip ? "" : p.Hint;
        countText.text = (index + 1) + " / " + pages.Count;
        nextText.text = confirmingSkip ? contentAsset.skipStart.Get(language) :
            index == pages.Count - 1 ? contentAsset.start.Get(language) : contentAsset.next.Get(language);
        back.interactable = confirmingSkip || index > 0;
        skip.gameObject.SetActive(!confirmingSkip && index < pages.Count - 1);
        var available = new List<Button>();
        if (back.interactable) available.Add(back);
        if (skip.gameObject.activeSelf) available.Add(skip);
        available.Add(next);
        for (int i = 0; i < available.Count; i++)
        {
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnLeft = nav.selectOnUp = available[(i + available.Count - 1) % available.Count];
            nav.selectOnRight = nav.selectOnDown = available[(i + 1) % available.Count];
            available[i].navigation = nav;
        }
        for (int i = illustration.childCount - 1; i >= 0; i--)
        {
            illustration.GetChild(i).gameObject.SetActive(false);
            Release(illustration.GetChild(i).gameObject);
        }
        bool example = !confirmingSkip && p.Tiles != null;
        illustration.gameObject.SetActive(example);
        bodyText.gameObject.SetActive(!example);
        bodyExampleText.gameObject.SetActive(example);
        if (example)
        {
            for (int i = 0; i < p.Tiles.Length; i++)
            {
                float x = i / (float)p.Tiles.Length;
                var tile = Box("Tile" + i, illustration, Color.white,
                    new Vector2(x, 0), new Vector2(x + 1f / p.Tiles.Length - .008f, 1)).GetComponent<Image>();
                tile.sprite = Resources.Load<Sprite>("Sprites/Tiles/" + p.Tiles[i]);
                tile.preserveAspect = true;
            }
        }
        progress.rectTransform.anchorMax = new Vector2((index + 1f) / pages.Count, 1);
        Canvas.ForceUpdateCanvases();
        if (Application.isPlaying) Layout();
        if (Application.isPlaying && EventSystem.current) EventSystem.current.SetSelectedGameObject(next.gameObject);
    }

    private void LateUpdate() { if (pages != null) Layout(); }

    private readonly List<RectTransform> focusFrames = new List<RectTransform>();
    private readonly List<Rect> focusRegions = new List<Rect>();
    private void Layout()
    {
        var screenSafe=Screen.safeArea;
        safe.anchorMin=new Vector2(screenSafe.xMin/Mathf.Max(1,Screen.width),screenSafe.yMin/Mathf.Max(1,Screen.height));
        safe.anchorMax=new Vector2(screenSafe.xMax/Mathf.Max(1,Screen.width),screenSafe.yMax/Mathf.Max(1,Screen.height));
        var area=safe.rect; var full=root.rect;
        float width=Mathf.Min(authoredCardSize.x,Mathf.Max(1,area.width-screenPadding*2));
        float height=Mathf.Min(authoredCardSize.y,Mathf.Max(1,area.height-screenPadding*2));
        focusRegions.Clear();
        if(!confirmingSkip) foreach(var target in pages[index].Targets) {
            var rt=target as RectTransform; if(!rt || !rt.gameObject.activeInHierarchy) continue;
            var canvas=rt.GetComponentInParent<Canvas>(); if(canvas) canvas=canvas.rootCanvas;
            var camera=canvas && canvas.renderMode!=RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var rects=new List<RectTransform>();
            if(rt.GetComponent<LayoutGroup>()) foreach(Transform child in rt)
                if(child.gameObject.activeInHierarchy && child is RectTransform childRect && child.GetComponentInChildren<Graphic>())
                    rects.Add(child.Find("Art/Image") as RectTransform ?? childRect);
            if(rects.Count==0) rects.Add(rt);
            bool first=true; Rect bounds=default;
            foreach(var visible in rects) {
                GetVisibleCorners(visible);
                foreach(var corner in corners) {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(root,RectTransformUtility.WorldToScreenPoint(camera,corner),DestinationCamera(),out var point);
                    if(first) { bounds=new Rect(point,Vector2.zero); first=false; }
                    else { bounds.xMin=Mathf.Min(bounds.xMin,point.x); bounds.xMax=Mathf.Max(bounds.xMax,point.x);
                        bounds.yMin=Mathf.Min(bounds.yMin,point.y); bounds.yMax=Mathf.Max(bounds.yMax,point.y); }
                }
            }
            bounds=Rect.MinMaxRect(Mathf.Clamp(bounds.xMin-4,full.xMin,full.xMax),Mathf.Clamp(bounds.yMin-4,full.yMin,full.yMax),
                Mathf.Clamp(bounds.xMax+4,full.xMin,full.xMax),Mathf.Clamp(bounds.yMax+4,full.yMin,full.yMax));
            if(bounds.width>1 && bounds.height>1) focusRegions.Add(bounds);
        }
        if(focusFrames.Count==0) focusFrames.Add(focus);
        while(focusFrames.Count<focusRegions.Count) {
            var frame=Instantiate(focus,root,false); frame.name="Focus"+focusFrames.Count;
            focusFrames.Add(frame);
        }
        for(int i=0;i<focusFrames.Count;i++) {
            focusFrames[i].gameObject.SetActive(i<focusRegions.Count);
            if(i<focusRegions.Count) Position(focusFrames[i],focusRegions[i]);
        }
        // Subtract each window separately: the space between windows stays shaded.
        var dark=new List<Rect>{full};
        foreach(var hole in focusRegions) {
            var next=new List<Rect>();
            foreach(var rectangle in dark) CutRectangle(rectangle,hole,next,false);
            dark=next;
        }
        while(shades.Count<dark.Count) {
            var shade=Instantiate(shades[0],root,false); shade.name="Dim"+shades.Count; shades.Add(shade);
        }
        for(int i=0;i<shades.Count;i++) {
            shades[i].gameObject.SetActive(i<dark.Count);
            if(i<dark.Count) Position(shades[i],dark[i]);
            shades[i].SetAsFirstSibling();
        }
        foreach(var frame in focusFrames) frame.SetAsLastSibling();
        safe.SetAsLastSibling();
        if(!autoPositionCard) return;
        card.sizeDelta=new Vector2(width,height);
        var available=Rect.MinMaxRect(area.xMin+screenPadding,area.yMin+screenPadding,area.xMax-screenPadding,area.yMax-screenPadding);
        var candidates=new List<Rect>{available};
        foreach(var region in focusRegions) {
            Vector2 min=safe.InverseTransformPoint(root.TransformPoint(region.min));
            Vector2 max=safe.InverseTransformPoint(root.TransformPoint(region.max));
            var obstacle=Rect.MinMaxRect(min.x-16,min.y-16,max.x+16,max.y+16);
            var next=new List<Rect>();
            foreach(var candidate in candidates) CutRectangle(candidate,obstacle,next,true);
            candidates=next;
        }
        float best=-1, distance=float.PositiveInfinity; Vector2 position=area.center;
        foreach(var candidate in candidates) {
            float scale=Mathf.Min(1,candidate.width/width,candidate.height/height);
            if(scale<=0) continue;
            var center=new Vector2(Mathf.Clamp(area.center.x,candidate.xMin+width*scale/2,candidate.xMax-width*scale/2),
                Mathf.Clamp(area.center.y,candidate.yMin+height*scale/2,candidate.yMax-height*scale/2));
            float d=(center-area.center).sqrMagnitude;
            if(scale>best+.0001f || (Mathf.Abs(scale-best)<.0001f && d<distance)) { best=scale; position=center; distance=d; }
        }
        card.anchorMin=card.anchorMax=new Vector2(.5f,.5f);
        card.anchoredPosition=position-area.center;
        card.localScale=Vector3.one*Mathf.Max(.01f,best);
    }
    private static void CutRectangle(Rect rect,Rect hole,List<Rect> output,bool maximal)
    {
        if(!rect.Overlaps(hole)) { output.Add(rect); return; }
        float x0=Mathf.Max(rect.xMin,hole.xMin), x1=Mathf.Min(rect.xMax,hole.xMax);
        float y0=Mathf.Max(rect.yMin,hole.yMin), y1=Mathf.Min(rect.yMax,hole.yMax);
        void Add(float left,float bottom,float right,float top) { if(right-left>.01f && top-bottom>.01f) output.Add(Rect.MinMaxRect(left,bottom,right,top)); }
        Add(rect.xMin,rect.yMin,rect.xMax,y0);
        Add(rect.xMin,y1,rect.xMax,rect.yMax);
        Add(rect.xMin,maximal?rect.yMin:y0,x0,maximal?rect.yMax:y1);
        Add(x1,maximal?rect.yMin:y0,rect.xMax,maximal?rect.yMax:y1);
    }

    private void GetVisibleCorners(RectTransform rect) {
        rect.GetWorldCorners(corners);
        var text=rect.GetComponent<TMP_Text>();
        if(text && !string.IsNullOrWhiteSpace(text.text)) {
            text.ForceMeshUpdate();
            var bounds=text.textBounds;
            if(bounds.size.x>0 && bounds.size.y>0) {
                corners[0]=rect.TransformPoint(new Vector3(bounds.min.x,bounds.min.y));
                corners[1]=rect.TransformPoint(new Vector3(bounds.min.x,bounds.max.y));
                corners[2]=rect.TransformPoint(new Vector3(bounds.max.x,bounds.max.y));
                corners[3]=rect.TransformPoint(new Vector3(bounds.max.x,bounds.min.y));
            }
            return;
        }
        var image=rect.GetComponent<Image>();
        var sprite=image ? (image.overrideSprite ? image.overrideSprite : image.sprite) : null;
        if(!image || !sprite || !image.preserveAspect || image.type!=Image.Type.Simple) return;
        var local=image.GetPixelAdjustedRect();
        if(local.width<=0 || local.height<=0 || sprite.rect.height<=0) return;
        float ratio=sprite.rect.width/sprite.rect.height;
        var size=local.size;
        if(ratio>size.x/size.y) size.y=size.x/ratio; else size.x=size.y*ratio;
        var offset=Vector2.Scale(local.size-size,rect.pivot);
        local=new Rect(local.position+offset,size);
        corners[0]=rect.TransformPoint(new Vector3(local.xMin,local.yMin));
        corners[1]=rect.TransformPoint(new Vector3(local.xMin,local.yMax));
        corners[2]=rect.TransformPoint(new Vector3(local.xMax,local.yMax));
        corners[3]=rect.TransformPoint(new Vector3(local.xMax,local.yMin));
    }
    private Camera DestinationCamera() {
        var canvas = root.GetComponentInParent<Canvas>();
        if (canvas) canvas = canvas.rootCanvas;
        return canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }
    private void Position(RectTransform rt, Rect rect)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        rt.anchoredPosition = rect.center;
        rt.sizeDelta = rect.size;
    }

    private RectTransform Box(string name, Transform parent, Color color, Vector2 min, Vector2 max,
        Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rt;
    }

    private TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false); rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = size;
        text.enableAutoSizing = true; text.fontSizeMin = size - 4; text.fontSizeMax = size;
        text.color = color; text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private Button MakeButton(string name, string label, Vector2 min, Vector2 max, bool primary, Action action, out TextMeshProUGUI text)
    {
        var rt = Box(name, card, primary ? gold : new Color(.14f, .18f, .24f), min, max);
        var image = rt.GetComponent<Image>(); image.raycastTarget = true;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, .92f, .73f);
        colors.selectedColor = new Color(1f, .92f, .73f);
        colors.pressedColor = new Color(.75f, .65f, .48f);
        button.colors = colors;
        button.onClick.AddListener(() => action());
        text = Text("Label", rt, label, 21, primary ? ink : Color.white, new Vector2(.04f, .08f), new Vector2(.96f, .92f));
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }
}
