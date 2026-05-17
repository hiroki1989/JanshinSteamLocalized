using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// iOS / Android 向けレターボックス表示ガード（v4）
/// ──────────────────────────────────────────────────
/// ★ 使い方：このファイルを Assets/ 以下に置くだけ。
///
/// 動作：
///  1. Editor で 1920×1080 (16:9) で作った配置を一切変えず、
///     iPhone の画面に縦幅合わせで縮小表示する
///  2. 横が余った分は黒帯（レターボックス）で埋める
///  3. 全シーンで自動適用（DontDestroyOnLoad）
///
/// 仕組み：
///  - CanvasScaler: ScaleWithScreenSize / 1920×1080 / Match=1（高さ基準）
///    → Canvas論理高さが常に1080になる
///    → iPhone 19.5:9 では Canvas論理幅が ≈2340 になる（1920より広い）
///  - 各Canvas の既存UIを 1920×1080 の中央固定フレームに自動収容
///    → 子要素の anchor/position は一切変わらない
///  - フレームの外側に黒パネルを配置して余白を隠す
///
/// PC ビルドには一切影響しません。
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

    // レターボックス済みCanvas を追跡（二重適用防止）
    private static readonly HashSet<int> _processedCanvasIds = new HashSet<int>();

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

        _processedCanvasIds.Clear();
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
        _processedCanvasIds.Clear();
        FixCurrentScene();
    }

    private void LateUpdate()
    {
        if (Screen.fullScreenMode != FullScreenMode.FullScreenWindow)
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;

        // 動的生成された Canvas も拾う
        CheckForNewCanvases();

        if (SHOW_DEBUG) _debugTimer += Time.unscaledDeltaTime;
    }

    // ============================================================
    //  メイン処理
    // ============================================================

    private void FixCurrentScene()
    {
        DisablePCOnlyScripts();
        ApplyLetterboxToAllCanvases();
        ApplyLetterboxToAllCameras();
    }

    // ───── ① CanvasScaler 設定 + レターボックスフレーム ─────
    private void ApplyLetterboxToAllCanvases()
    {
        foreach (var canvas in FindObjectsOfType<Canvas>(true))
        {
            // ルートCanvasのみ処理（子Canvasはスキップ）
            if (canvas.transform.parent != null)
            {
                var parentCanvas = canvas.transform.parent.GetComponentInParent<Canvas>();
                if (parentCanvas != null) continue;
            }
            ApplyLetterboxToCanvas(canvas);
        }
    }

    private void ApplyLetterboxToCanvas(Canvas canvas)
    {
        if (canvas == null) return;
        int id = canvas.GetInstanceID();
        if (_processedCanvasIds.Contains(id)) return;
        _processedCanvasIds.Add(id);

        // ── CanvasScaler 設定 ──
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution  = new Vector2(REF_WIDTH, REF_HEIGHT);
        // Match=1（高さ基準）→ 論理高さが常に1080、幅は端末に応じて広がる
        scaler.screenMatchMode      = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight   = 1f;

        // ── 現在のアスペクト比チェック ──
        float screenAspect = (float)Screen.width / Mathf.Max(1f, Screen.height);
        float refAspect    = REF_WIDTH / REF_HEIGHT; // 16/9 ≈ 1.778

        // 16:9 以下（縦長）なら横にはみ出る可能性あり → その場合は別対応
        // 16:9 以上（横長）なら高さ基準で横に余白ができる → レターボックス
        // どちらでもフレームを入れれば安全
        if (Mathf.Abs(screenAspect - refAspect) < 0.01f) return; // ほぼ16:9なら不要

        RectTransform canvasRT = canvas.transform as RectTransform;
        if (canvasRT == null) return;

        // ── フレーム作成：1920×1080 の中央固定コンテナ ──
        var frameGO = new GameObject("__LetterboxFrame", typeof(RectTransform));
        var frameRT = frameGO.GetComponent<RectTransform>();
        frameRT.SetParent(canvasRT, false);

        // フレームを中央に固定、サイズ 1920×1080
        frameRT.anchorMin = new Vector2(0.5f, 0.5f);
        frameRT.anchorMax = new Vector2(0.5f, 0.5f);
        frameRT.pivot     = new Vector2(0.5f, 0.5f);
        frameRT.sizeDelta = new Vector2(REF_WIDTH, REF_HEIGHT);
        frameRT.anchoredPosition = Vector2.zero;

        // ── 既存の子を全部フレームの中へ移動 ──
        var children = new List<Transform>();
        for (int i = 0; i < canvasRT.childCount; i++)
        {
            var child = canvasRT.GetChild(i);
            if (child == frameRT) continue;
            children.Add(child);
        }
        foreach (var child in children)
        {
            child.SetParent(frameRT, false);
        }

        // フレームを最初の子にする（描画順を維持）
        frameRT.SetAsFirstSibling();

        // ── 黒帯パネル（フレームの外を覆う） ──
        if (screenAspect > refAspect)
        {
            // 横長端末 → 左右に黒帯
            CreateBlackBar(canvasRT, "BarLeft",
                new Vector2(0f, 0f), new Vector2(0f, 1f),  // 左端 stretch
                new Vector2(1f, 0.5f),
                anchoredPos: Vector2.zero,
                sizeDelta: new Vector2(REF_WIDTH, 0f),      // 幅はフレーム左端まで
                calcSide: true, isLeft: true);

            CreateBlackBar(canvasRT, "BarRight",
                new Vector2(1f, 0f), new Vector2(1f, 1f),  // 右端 stretch
                new Vector2(0f, 0.5f),
                anchoredPos: Vector2.zero,
                sizeDelta: new Vector2(REF_WIDTH, 0f),
                calcSide: true, isLeft: false);
        }
        else
        {
            // 縦長端末 → 上下に黒帯
            CreateBlackBar(canvasRT, "BarTop",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 0f),
                anchoredPos: Vector2.zero,
                sizeDelta: new Vector2(0f, REF_HEIGHT),
                calcSide: false, isLeft: false);

            CreateBlackBar(canvasRT, "BarBottom",
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 1f),
                anchoredPos: Vector2.zero,
                sizeDelta: new Vector2(0f, REF_HEIGHT),
                calcSide: false, isLeft: false);
        }
    }

    private void CreateBlackBar(RectTransform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta,
        bool calcSide, bool isLeft)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;

        if (calcSide)
        {
            // 左右バー：Canvas幅の半分 - フレーム幅の半分 = バーの幅
            // Canvas論理幅 = Screen.width / scaleFactor
            // scaleFactor = Screen.height / REF_HEIGHT (match=1)
            float scaleFactor = Screen.height / REF_HEIGHT;
            float canvasLogicalWidth = Screen.width / scaleFactor;
            float barWidth = (canvasLogicalWidth - REF_WIDTH) / 2f;
            barWidth = Mathf.Max(0f, barWidth) + 2f; // +2 で隙間防止

            if (isLeft)
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot     = new Vector2(0f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(barWidth, 0f);
            }
            else
            {
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot     = new Vector2(1f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(barWidth, 0f);
            }
        }
        else
        {
            // 上下バー
            float scaleFactor = Screen.width / REF_WIDTH;
            float canvasLogicalHeight = Screen.height / scaleFactor;
            float barHeight = (canvasLogicalHeight - REF_HEIGHT) / 2f;
            barHeight = Mathf.Max(0f, barHeight) + 2f;

            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot     = pivot;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, barHeight);
        }

        // 黒帯は最前面に
        rt.SetAsLastSibling();
    }

    // ───── ② Camera にもレターボックス適用 ─────
    //  （3Dオブジェクトやパーティクルがある場合に備えて）
    private void ApplyLetterboxToAllCameras()
    {
        float screenAspect = (float)Screen.width / Mathf.Max(1f, Screen.height);
        float targetAspect = REF_WIDTH / REF_HEIGHT;

        foreach (var cam in FindObjectsOfType<Camera>(true))
        {
            if (cam.targetTexture != null) continue; // RenderTexture用カメラはスキップ

            if (screenAspect > targetAspect)
            {
                // 横長 → 左右に黒帯
                float normalizedWidth = targetAspect / screenAspect;
                float barWidth = (1f - normalizedWidth) / 2f;
                cam.rect = new Rect(barWidth, 0f, normalizedWidth, 1f);
            }
            else if (screenAspect < targetAspect)
            {
                // 縦長 → 上下に黒帯
                float normalizedHeight = screenAspect / targetAspect;
                float barHeight = (1f - normalizedHeight) / 2f;
                cam.rect = new Rect(0f, barHeight, 1f, normalizedHeight);
            }
            else
            {
                cam.rect = new Rect(0f, 0f, 1f, 1f);
            }
        }
    }

    // ───── ③ PC 専用スクリプト無効化 ─────
    private void DisablePCOnlyScripts()
    {
        DisableByTypeName("WindowAspectResizer");
        DisableByTypeName("FixedAspect");
    }

    private static void DisableByTypeName(string typeName)
    {
        try
        {
            System.Type type = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(typeName);
                if (type != null) break;
            }
            if (type == null) return;
            foreach (var obj in FindObjectsOfType(type, true))
            {
                var mb = obj as MonoBehaviour;
                if (mb != null) mb.enabled = false;
            }
        }
        catch { }
    }

    // ───── ④ 動的Canvas検出 ─────
    private void CheckForNewCanvases()
    {
        foreach (var canvas in FindObjectsOfType<Canvas>(true))
        {
            if (canvas.transform.parent != null)
            {
                var parentCanvas = canvas.transform.parent.GetComponentInParent<Canvas>();
                if (parentCanvas != null) continue;
            }
            int id = canvas.GetInstanceID();
            if (!_processedCanvasIds.Contains(id))
            {
                ApplyLetterboxToCanvas(canvas);
            }
        }
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
        float barW = 0f;
        float refAspect = REF_WIDTH / REF_HEIGHT;
        if (aspect > refAspect)
            barW = (sw - sh * refAspect) / 2f;

        string text = string.Format(
            "Screen: {0}x{1} ({2:F2}:1)\n" +
            "Ref: {3}x{4} ({5:F2}:1)\n" +
            "Black bar width: {6:F0}px\n" +
            "Scene: {7}",
            sw, sh, aspect,
            REF_WIDTH, REF_HEIGHT, refAspect,
            barW,
            SceneManager.GetActiveScene().name);

        GUI.color = new Color(0, 0, 0, 0.7f);
        GUI.DrawTexture(new Rect(8, 8, 620, 150), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(16, 12, 600, 142), text, _debugStyle);
    }

#else
    public static void ApplyMobileScaler(CanvasScaler scaler) { }
#endif
}
