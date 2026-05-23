using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// iOS / Android 向けレターボックス表示ガード（v5）
/// ──────────────────────────────────────────────────
/// ★ 使い方：このファイルを Assets/ 以下に置くだけ。
///
/// v4 → v5 変更点：
///  - デバッグ表示を削除
///  - 起動直後の画面サイズ未確定問題を修正
///    （数フレーム待ってから適用する）
/// ──────────────────────────────────────────────────
/// </summary>
public sealed class MobileDisplayGuard : MonoBehaviour
{
    private const float REF_WIDTH  = 1920f;
    private const float REF_HEIGHT = 1080f;
    private const string PF_FULLSCREEN = "PF_Option_Fullscreen";

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

    private bool _initialFixDone = false;

    private void Awake()
    {
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.autorotateToPortrait           = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft      = true;
        Screen.autorotateToLandscapeRight     = true;
        Screen.orientation = ScreenOrientation.AutoRotation;

        SceneManager.sceneLoaded += OnSceneLoaded;

        // 最初のシーンは画面サイズが未確定なので、
        // 数フレーム待ってから適用する
        StartCoroutine(DeferredInitialFix());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// 起動直後は Screen.width/height が確定していないため、
    /// 数フレーム待ってから初回適用を行う。
    /// </summary>
    private IEnumerator DeferredInitialFix()
    {
        // 3フレーム待つ（iOSで画面回転＋解像度確定に必要な猶予）
        yield return null;
        yield return null;
        yield return null;

        _processedCanvasIds.Clear();
        FixCurrentScene();
        _initialFixDone = true;
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

        // 動的生成された Canvas も拾う（初回適用完了後のみ）
        if (_initialFixDone)
            CheckForNewCanvases();
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

        // ── 古いレターボックスフレームが残っていたら破棄 ──
        CleanupOldLetterbox(canvas.transform as RectTransform);

        // ── CanvasScaler 設定 ──
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution  = new Vector2(REF_WIDTH, REF_HEIGHT);
        scaler.screenMatchMode      = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight   = 1f;

        // ── 現在のアスペクト比チェック ──
        float screenAspect = (float)Screen.width / Mathf.Max(1f, Screen.height);
        float refAspect    = REF_WIDTH / REF_HEIGHT;

        if (Mathf.Abs(screenAspect - refAspect) < 0.01f) return;

        RectTransform canvasRT = canvas.transform as RectTransform;
        if (canvasRT == null) return;

        // ── フレーム作成：1920×1080 の中央固定コンテナ ──
        var frameGO = new GameObject("__LetterboxFrame", typeof(RectTransform));
        var frameRT = frameGO.GetComponent<RectTransform>();
        frameRT.SetParent(canvasRT, false);

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

        frameRT.SetAsFirstSibling();

        // ── 黒帯パネル ──
        if (screenAspect > refAspect)
        {
            CreateBlackBar(canvasRT, "__BarLeft",  true, true);
            CreateBlackBar(canvasRT, "__BarRight", true, false);
        }
        else
        {
            CreateBlackBar(canvasRT, "__BarTop",    false, false);
            CreateBlackBar(canvasRT, "__BarBottom", false, true);
        }
    }

    /// <summary>
    /// DeferredInitialFix で再適用するとき、
    /// 最初のフレームで作られた壊れたフレーム/黒帯を先に除去する。
    /// </summary>
    private void CleanupOldLetterbox(RectTransform canvasRT)
    {
        if (canvasRT == null) return;
        var toDestroy = new List<GameObject>();
        for (int i = 0; i < canvasRT.childCount; i++)
        {
            var child = canvasRT.GetChild(i);
            if (child.name == "__LetterboxFrame" ||
                child.name == "__BarLeft" ||
                child.name == "__BarRight" ||
                child.name == "__BarTop" ||
                child.name == "__BarBottom")
            {
                // フレーム内の子を Canvas 直下に戻す
                if (child.name == "__LetterboxFrame")
                {
                    var grandchildren = new List<Transform>();
                    for (int j = 0; j < child.childCount; j++)
                        grandchildren.Add(child.GetChild(j));
                    foreach (var gc in grandchildren)
                        gc.SetParent(canvasRT, false);
                }
                toDestroy.Add(child.gameObject);
            }
        }
        foreach (var go in toDestroy)
            Object.DestroyImmediate(go);
    }

    private void CreateBlackBar(RectTransform parent, string name,
        bool isHorizontal, bool isLeftOrBottom)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;

        if (isHorizontal)
        {
            float scaleFactor = Screen.height / REF_HEIGHT;
            float canvasLogicalWidth = Screen.width / scaleFactor;
            float barWidth = (canvasLogicalWidth - REF_WIDTH) / 2f;
            barWidth = Mathf.Max(0f, barWidth) + 2f;

            if (isLeftOrBottom) // left
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot     = new Vector2(0f, 0.5f);
            }
            else // right
            {
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot     = new Vector2(1f, 0.5f);
            }
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(barWidth, 0f);
        }
        else
        {
            float scaleFactor = Screen.width / REF_WIDTH;
            float canvasLogicalHeight = Screen.height / scaleFactor;
            float barHeight = (canvasLogicalHeight - REF_HEIGHT) / 2f;
            barHeight = Mathf.Max(0f, barHeight) + 2f;

            if (isLeftOrBottom) // bottom
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot     = new Vector2(0.5f, 0f);
            }
            else // top
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot     = new Vector2(0.5f, 1f);
            }
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, barHeight);
        }

        rt.SetAsLastSibling();
    }

    // ───── ② Camera にもレターボックス適用 ─────
    private void ApplyLetterboxToAllCameras()
    {
        float screenAspect = (float)Screen.width / Mathf.Max(1f, Screen.height);
        float targetAspect = REF_WIDTH / REF_HEIGHT;

        foreach (var cam in FindObjectsOfType<Camera>(true))
        {
            if (cam.targetTexture != null) continue;

            if (screenAspect > targetAspect)
            {
                float normalizedWidth = targetAspect / screenAspect;
                float barWidth = (1f - normalizedWidth) / 2f;
                cam.rect = new Rect(barWidth, 0f, normalizedWidth, 1f);
            }
            else if (screenAspect < targetAspect)
            {
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

#else
    public static void ApplyMobileScaler(CanvasScaler scaler) { }
#endif
}
