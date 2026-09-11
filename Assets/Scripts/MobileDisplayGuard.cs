using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Fits each authored UI into the safe area without cropping or stretching.</summary>
[DefaultExecutionOrder(-1000)]
public sealed class MobileDisplayGuard : MonoBehaviour
{
    private static readonly Vector2 SceneSize = new Vector2(1920, 1080);
    private const string FrameName = "__LetterboxFrame";
    private readonly Dictionary<Canvas, RectTransform> frames = new Dictionary<Canvas, RectTransform>();
    private readonly List<Canvas> removed = new List<Canvas>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
#if UNITY_IOS || UNITY_ANDROID
        if (FindFirstObjectByType<MobileDisplayGuard>()) return;
        var go = new GameObject("[MobileDisplayGuard]");
        DontDestroyOnLoad(go);
        go.AddComponent<MobileDisplayGuard>();
#endif
    }

    private void Awake()
    {
#if UNITY_IOS || UNITY_ANDROID
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.AutoRotation;
        PlayerPrefs.SetInt("PF_Option_Fullscreen", 1);
        SceneManager.sceneLoaded += OnSceneLoaded;
#endif
    }
    private void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { DisablePCOnlyScripts(); }
    private void LateUpdate()
    {
#if UNITY_IOS || UNITY_ANDROID
        Refresh(Screen.width, Screen.height, Screen.safeArea);
#endif
    }

    // Also callable by editor device-size checks; does not save changes to scene assets.
    public void Refresh(int width, int height, Rect safeArea)
    {
        if (width <= 0 || height <= 0) return;
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
            if (canvas.GetComponent<FirstMatchTutorialView>() || canvas.GetComponentInChildren<WinTileLightning>(true))
            {
                // Screen-coordinate overlays must not be reparented: focus and impact positions depend on this root.
                ApplyMobileScaler(canvas.GetComponent<CanvasScaler>());
                continue;
            }
            if (!frames.TryGetValue(canvas, out var frame) || !frame)
            {
                frame = PrepareCanvas(canvas);
                frames[canvas] = frame;
            }
            CaptureNewChildren(canvas, frame);
            ApplyViewport(canvas, frame, width, height, safeArea);
        }
        removed.Clear();
        foreach (var pair in frames) if (!pair.Key) removed.Add(pair.Key);
        foreach (var canvas in removed) frames.Remove(canvas);
        Rect viewport = FitViewport(width, height, safeArea, SceneSize);
        foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera.targetTexture) continue;
            camera.rect = new Rect(viewport.x / width, viewport.y / height, viewport.width / width, viewport.height / height);
        }
    }

    public static void ApplyMobileScaler(CanvasScaler scaler)
    {
        if (!scaler) return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // Expand uses the smaller ratio. Height-only scaling clipped the sides on iPad.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
    }

    public static Rect FitViewport(int width, int height, Rect safeArea, Vector2 reference)
    {
        var screen = new Rect(0, 0, Mathf.Max(1, width), Mathf.Max(1, height));
        if (safeArea.width <= 0 || safeArea.height <= 0) safeArea = screen;
        safeArea = Rect.MinMaxRect(Mathf.Clamp(safeArea.xMin, 0, screen.width), Mathf.Clamp(safeArea.yMin, 0, screen.height),
            Mathf.Clamp(safeArea.xMax, 0, screen.width), Mathf.Clamp(safeArea.yMax, 0, screen.height));
        if (safeArea.width <= 0 || safeArea.height <= 0) safeArea = screen;
        float scale = Mathf.Min(safeArea.width / Mathf.Max(1, reference.x), safeArea.height / Mathf.Max(1, reference.y));
        Vector2 size = reference * scale;
        return new Rect(safeArea.center - size * .5f, size);
    }

    public static RectTransform PrepareCanvas(Canvas canvas)
    {
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (!scaler) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        if (scaler.referenceResolution.x <= 0 || scaler.referenceResolution.y <= 0) scaler.referenceResolution = SceneSize;
        ApplyMobileScaler(scaler);
        // Preserve each canvas's design resolution, including the 1600x900 tile-selection modal.
        var parent = (RectTransform)canvas.transform;
        var frame = parent.Find(FrameName) as RectTransform;
        if (!frame)
        {
            frame = new GameObject(FrameName, typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(parent, false);
        }
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);
        frame.sizeDelta = scaler.referenceResolution;
        CaptureNewChildren(canvas, frame);
        frame.SetAsFirstSibling();
        foreach (string name in new[] { "__BarLeft", "__BarRight", "__BarTop", "__BarBottom" })
        {
            if (parent.Find(name)) continue;
            var bar = new GameObject(name, typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(parent, false);
            var image = bar.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
        }
        return frame;
    }

    private static bool IsBar(Transform child)
    {
        return child.name == "__BarLeft" || child.name == "__BarRight" || child.name == "__BarTop" || child.name == "__BarBottom";
    }

    private static void CaptureNewChildren(Canvas canvas, RectTransform frame)
    {
        // Preserve anchors, offsets and ordering, including UI created after scene load.
        for (int i = 0; i < canvas.transform.childCount;)
        {
            var child = canvas.transform.GetChild(i);
            if (child == frame || IsBar(child)) { i++; continue; }
            child.SetParent(frame, false);
        }
    }

    public static void ApplyViewport(Canvas canvas, RectTransform frame, int width, int height, Rect safeArea)
    {
        var scaler = canvas.GetComponent<CanvasScaler>();
        Vector2 reference = scaler.referenceResolution;
        ApplyMobileScaler(scaler);
        var viewport = FitViewport(width, height, safeArea, reference);
        float baseScale = Mathf.Max(.0001f, Mathf.Min(width / reference.x, height / reference.y));
        canvas.scaleFactor = baseScale;
        frame.sizeDelta = reference;
        frame.anchoredPosition = (viewport.center - new Vector2(width, height) * .5f) / baseScale;
        frame.localScale = Vector3.one * (viewport.width / reference.x / baseScale);
        SetBar(canvas.transform, "__BarLeft", new Rect(0, 0, viewport.xMin, height), width, height);
        SetBar(canvas.transform, "__BarRight", new Rect(viewport.xMax, 0, width - viewport.xMax, height), width, height);
        SetBar(canvas.transform, "__BarBottom", new Rect(viewport.xMin, 0, viewport.width, viewport.yMin), width, height);
        SetBar(canvas.transform, "__BarTop", new Rect(viewport.xMin, viewport.yMax, viewport.width, height - viewport.yMax), width, height);
    }

    private static void SetBar(Transform parent, string name, Rect pixels, int width, int height)
    {
        var bar = parent.Find(name) as RectTransform;
        if (!bar) return;
        bar.anchorMin = new Vector2(pixels.xMin / width, pixels.yMin / height);
        bar.anchorMax = new Vector2(pixels.xMax / width, pixels.yMax / height);
        bar.offsetMin = bar.offsetMax = Vector2.zero;
        bar.gameObject.SetActive(pixels.width > .1f && pixels.height > .1f);
    }

    private static void DisablePCOnlyScripts()
    {
        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (behaviour && (behaviour.GetType().Name == "WindowAspectResizer" || behaviour.GetType().Name == "FixedAspect"))
                behaviour.enabled = false;
    }
}
