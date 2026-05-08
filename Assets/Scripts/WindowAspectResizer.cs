using UnityEngine;

public sealed class WindowAspectResizer : MonoBehaviour
{
    [Header("Reference Aspect (元の比率)")]
    [SerializeField] private int referenceWidth = 1920;
    [SerializeField] private int referenceHeight = 1080;

    [Header("Clamp (小さくされすぎ防止)")]
    [SerializeField] private int minWidth = 640;
    [SerializeField] private int minHeight = 360;

    [Header("Behavior")]
    [SerializeField] private bool enableOnlyWhenWindowed = true;

    private int _lastW;
    private int _lastH;
    private bool _applying;

    private float Aspect
    {
        get
        {
            if (referenceHeight <= 0) return 16f / 9f;
            return (float)referenceWidth / (float)referenceHeight;
        }
    }

    private void Awake()
    {
        _lastW = Screen.width;
        _lastH = Screen.height;
    }

    private void Update()
    {
        if (enableOnlyWhenWindowed)
        {
            if (Screen.fullScreenMode != FullScreenMode.Windowed) return;
        }

        int w = Screen.width;
        int h = Screen.height;

        if (w == _lastW && h == _lastH) return;
        if (_applying)
        {
            _lastW = w;
            _lastH = h;
            return;
        }

        float aspect = Aspect;

        int targetW = w;
        int targetH = h;

        bool widthChanged = (w != _lastW);
        bool heightChanged = (h != _lastH);

        if (widthChanged && !heightChanged)
        {
            targetH = Mathf.RoundToInt(targetW / aspect);
        }
        else if (!widthChanged && heightChanged)
        {
            targetW = Mathf.RoundToInt(targetH * aspect);
        }
        else
        {
            int dw = Mathf.Abs(w - _lastW);
            int dh = Mathf.Abs(h - _lastH);

            if (dw >= dh)
            {
                targetH = Mathf.RoundToInt(targetW / aspect);
            }
            else
            {
                targetW = Mathf.RoundToInt(targetH * aspect);
            }
        }

        if (targetW < minWidth)
        {
            targetW = minWidth;
            targetH = Mathf.RoundToInt(targetW / aspect);
        }

        if (targetH < minHeight)
        {
            targetH = minHeight;
            targetW = Mathf.RoundToInt(targetH * aspect);
        }

        if (targetW != w || targetH != h)
        {
            _applying = true;
            Screen.SetResolution(targetW, targetH, FullScreenMode.Windowed);
            _applying = false;
        }

        _lastW = Screen.width;
        _lastH = Screen.height;
    }
}