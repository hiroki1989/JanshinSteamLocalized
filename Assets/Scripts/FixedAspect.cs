using UnityEngine;

[DefaultExecutionOrder(-10000)]
public sealed class FixedAspect : MonoBehaviour
{
    [SerializeField] private int targetWidth = 16;
    [SerializeField] private int targetHeight = 9;
    [SerializeField] private Camera targetCamera;

    private void Awake()
    {
        if (!targetCamera) targetCamera = Camera.main;
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        Apply();
    }

    private void Apply()
    {
        if (!targetCamera) targetCamera = Camera.main;
        if (!targetCamera) return;

        // カメラの切り抜きは一切しない
        if (targetCamera.rect != new Rect(0f, 0f, 1f, 1f))
            targetCamera.rect = new Rect(0f, 0f, 1f, 1f);
    }
public Vector2Int CalcBestWindowedSizeFromDesktop()
{
    int desktopW = 1280;
    int desktopH = 720;

    try { desktopW = Mathf.Max(1, Screen.currentResolution.width); } catch { }
    try { desktopH = Mathf.Max(1, Screen.currentResolution.height); } catch { }

    float targetAspect = (float)targetWidth / (float)targetHeight;

    int preferredW = 1280;
    int preferredH = Mathf.RoundToInt(preferredW / targetAspect);

    if (preferredW > desktopW || preferredH > desktopH)
    {
        preferredW = 960;
        preferredH = Mathf.RoundToInt(preferredW / targetAspect);
    }

    if (preferredW > desktopW || preferredH > desktopH)
    {
        preferredW = desktopW;
        preferredH = Mathf.RoundToInt(preferredW / targetAspect);

        if (preferredH > desktopH)
        {
            preferredH = desktopH;
            preferredW = Mathf.RoundToInt(preferredH * targetAspect);
        }
    }

    preferredW = Mathf.Max(1, preferredW);
    preferredH = Mathf.Max(1, preferredH);

    return new Vector2Int(preferredW, preferredH);
}
    public Vector2Int CalcBestExclusiveFullscreenSize()
    {
        Resolution current = Screen.currentResolution;

        int desktopW = Mathf.Max(1, current.width);
        int desktopH = Mathf.Max(1, current.height);

        float targetAspect = (float)targetWidth / (float)targetHeight;

        int w = desktopW;
        int h = Mathf.RoundToInt(w / targetAspect);

        if (h > desktopH)
        {
            h = desktopH;
            w = Mathf.RoundToInt(h * targetAspect);
        }

        w = Mathf.Max(1, w);
        h = Mathf.Max(1, h);

        return new Vector2Int(w, h);
    }
}