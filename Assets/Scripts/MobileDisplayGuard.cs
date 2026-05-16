using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// iOS / Android 向け画面表示の一括ガード。
/// ─────────────────────────────────────────────
/// ★ 使い方：このファイルを Assets/ 以下に置くだけ。
///   [RuntimeInitializeOnLoadMethod] で自動起動するため、
///   シーンにアタッチする必要はありません。
///
/// やっていること：
///  1. 全シーンの CanvasScaler を ScaleWithScreenSize (1920×1080) に統一
///  2. WindowAspectResizer / FixedAspect をモバイルで無効化
///  3. Screen.SetResolution の誤使用を防ぐラッパー提供
///  4. 画面向きを横固定
/// ─────────────────────────────────────────────
/// PC ビルドには一切影響しません（#if ガードで分岐済み）。
/// </summary>
public sealed class MobileDisplayGuard : MonoBehaviour
{
    // ===== 設定（必要に応じて変更） =====
    private const float REF_WIDTH  = 1920f;
    private const float REF_HEIGHT = 1080f;
    private const float MATCH      = 0.5f;   // 0=幅基準, 1=高さ基準, 0.5=中間

    // ===== 自動起動 =====
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
#if UNITY_IOS || UNITY_ANDROID
        // 既に存在していたら二重生成しない
        if (FindObjectOfType<MobileDisplayGuard>() != null) return;

        var go = new GameObject("[MobileDisplayGuard]");
        go.AddComponent<MobileDisplayGuard>();
        DontDestroyOnLoad(go);
#endif
    }

    // ===== 初期化 =====
    private void Awake()
    {
#if UNITY_IOS || UNITY_ANDROID
        // (A) 画面向きを横固定
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.autorotateToPortrait           = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft      = true;
        Screen.autorotateToLandscapeRight     = true;
        Screen.orientation = ScreenOrientation.AutoRotation;

        // (B) 最初のシーンを修正
        FixCurrentScene();

        // (C) 以後のシーン遷移にもフック
        SceneManager.sceneLoaded += OnSceneLoaded;
#endif
    }

    private void OnDestroy()
    {
#if UNITY_IOS || UNITY_ANDROID
        SceneManager.sceneLoaded -= OnSceneLoaded;
#endif
    }

    // ===== シーン読み込みごとに自動修正 =====
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FixCurrentScene();
    }

    private void FixCurrentScene()
    {
        FixAllCanvasScalers();
        DisablePCOnlyScripts();
    }

    // ───── ① 全 CanvasScaler を統一 ─────
    private void FixAllCanvasScalers()
    {
        // includeInactive = true で非アクティブの Canvas も拾う
        foreach (var scaler in FindObjectsOfType<CanvasScaler>(true))
        {
            ApplyStandardScaler(scaler);
        }
    }

    /// <summary>
    /// CanvasScaler に標準設定を適用する。
    /// 動的に Canvas を生成するコードからも呼べるよう public static にしてある。
    /// 使い方: MobileDisplayGuard.ApplyStandardScaler(myScaler);
    /// </summary>
    public static void ApplyStandardScaler(CanvasScaler scaler)
    {
        if (scaler == null) return;

#if UNITY_IOS || UNITY_ANDROID
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(REF_WIDTH, REF_HEIGHT);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = MATCH;
#endif
    }

    // ───── ② PC 専用スクリプトを無効化 ─────
    private void DisablePCOnlyScripts()
    {
        // WindowAspectResizer（毎フレーム Screen.SetResolution を呼んでしまう）
        foreach (var w in FindObjectsOfType<WindowAspectResizer>(true))
        {
            w.enabled = false;
        }

        // FixedAspect（カメラ rect を毎フレーム触る）
        foreach (var f in FindObjectsOfType<FixedAspect>(true))
        {
            f.enabled = false;
        }
    }

    // ───── ③ Screen.SetResolution ラッパー ─────
    /// <summary>
    /// モバイルでは何もしない安全な SetResolution。
    /// 既存コードの Screen.SetResolution(...) を
    /// MobileDisplayGuard.SafeSetResolution(...) に置換するだけで対応完了。
    /// </summary>
    public static void SafeSetResolution(int w, int h, FullScreenMode mode)
    {
#if UNITY_IOS || UNITY_ANDROID
        // モバイルでは解像度変更しない（OSに任せる）
        return;
#else
        Screen.SetResolution(w, h, mode);
#endif
    }

    public static void SafeSetResolution(int w, int h, bool fullscreen)
    {
#if UNITY_IOS || UNITY_ANDROID
        return;
#else
        Screen.SetResolution(w, h, fullscreen);
#endif
    }
}
