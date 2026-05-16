using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// iOS / Android 向け画面表示の一括ガード（v3）
/// ──────────────────────────────────────────────────
/// ★ 使い方：このファイルを Assets/ 以下に置くだけ。
///   シーンにアタッチする必要はありません。
/// ──────────────────────────────────────────────────
/// </summary>
public sealed class MobileDisplayGuard : MonoBehaviour
{
    private const float REF_WIDTH  = 1920f;
    private const float REF_HEIGHT = 1080f;
    private const string PF_FULLSCREEN = "PF_Option_Fullscreen";

    // デバッグ表示（リリース前に false に変更）
    private const bool SHOW_DEBUG = true;
    private const float DEBUG_HIDE_AFTER = 8f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
#if UNITY_IOS || UNITY_ANDROID
        try
        {
            PlayerPrefs.SetInt(PF_FULLSCREEN, 1);
            PlayerPrefs.Save();
        }
        catch { }

        if (FindObjectOfType<MobileDisplayGuard>() != null) return;
        var go = new GameObject("[MobileDisplayGuard]");
        go.AddComponent<MobileDisplayGuard>();
        DontDestroyOnLoad(go);
#endif
    }

#if UNITY_IOS || UNITY_ANDROID

    private float _debugTimer;
    private GUIStyle _debugStyle;

    private void Awake()
    {
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.autorotateToPortrait           = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft      = true;
        Screen.autorotateToLandscapeRight     = true;
        Screen.orientation = ScreenOrientation.AutoRotation;

        FixCurrentScene();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        try { PlayerPrefs.SetInt(PF_FULLSCREEN, 1); } catch { }
        FixCurrentScene();
    }

    private void LateUpdate()
    {
        if (Screen.fullScreenMode != FullScreenMode.FullScreenWindow)
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;

        if (SHOW_DEBUG) _debugTimer += Time.unscaledDeltaTime;
    }

    private void FixCurrentScene()
    {
        FixAllCanvasScalers();
        DisablePCOnlyScripts();
    }

    // ───── ① CanvasScaler → Expand モード ─────
    private void FixAllCanvasScalers()
    {
        foreach (var scaler in FindObjectsOfType<CanvasScaler>(true))
            ApplyMobileScaler(scaler);
    }

    public static void ApplyMobileScaler(CanvasScaler scaler)
    {
        if (scaler == null) return;
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(REF_WIDTH, REF_HEIGHT);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.Expand;
        scaler.matchWidthOrHeight  = 0f;
    }

    // ───── ② PC 専用スクリプトを無効化（型名で検索） ─────
    //  WindowAspectResizer / FixedAspect が削除済みでもエラーにならない
    private void DisablePCOnlyScripts()
    {
        DisableByTypeName("WindowAspectResizer");
        DisableByTypeName("FixedAspect");
    }

    private static void DisableByTypeName(string typeName)
    {
        try
        {
            var type = System.Type.GetType(typeName);
            if (type == null)
            {
                // アセンブリ全体から探す
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(typeName);
                    if (type != null) break;
                }
            }
            if (type == null) return; // クラスが存在しない → 何もしない

            var objects = FindObjectsOfType(type, true);
            foreach (var obj in objects)
            {
                var mb = obj as MonoBehaviour;
                if (mb != null) mb.enabled = false;
            }
        }
        catch { }
    }

    // ───── デバッグ表示 ─────
    private void OnGUI()
    {
        if (!SHOW_DEBUG) return;
        if (_debugTimer > DEBUG_HIDE_AFTER) return;

        if (_debugStyle == null)
        {
            _debugStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
            };
            _debugStyle.normal.textColor = Color.yellow;
        }

        float sw = Screen.width;
        float sh = Screen.height;
        float aspect = sw / Mathf.Max(1f, sh);

        string scalerInfo = "No CanvasScaler";
        var cs = FindObjectOfType<CanvasScaler>();
        if (cs != null)
        {
            scalerInfo = string.Format("Scaler: {0} / {1} / ref={2}x{3}",
                cs.uiScaleMode, cs.screenMatchMode,
                cs.referenceResolution.x, cs.referenceResolution.y);
        }

        string text = string.Format(
            "Screen: {0}x{1} ({2:F2}:1)\n" +
            "Safe: {3:F0}x{4:F0}\n" +
            "{5}\n" +
            "FullScreen: {6}\n" +
            "Scene: {7}",
            sw, sh, aspect,
            Screen.safeArea.width, Screen.safeArea.height,
            scalerInfo,
            Screen.fullScreenMode,
            SceneManager.GetActiveScene().name);

        GUI.color = new Color(0, 0, 0, 0.7f);
        GUI.DrawTexture(new Rect(8, 8, 720, 200), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(16, 12, 700, 192), text, _debugStyle);
    }

#else
    public static void ApplyMobileScaler(CanvasScaler scaler) { }
#endif
}
