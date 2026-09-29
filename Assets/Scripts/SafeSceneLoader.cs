// SafeSceneLoader.cs
// ── 全てのシーン遷移を非同期ロードに置き換えるユーティリティ ──
//
// 【なぜ必要か】
// SceneManager.LoadScene（同期）はメインスレッドを完全にブロックするため、
// Steam Overlay の D3D フックが描画フレームを処理できなくなり、
// 重いシーン（RunScene 等）への遷移でネイティブクラッシュを引き起こす。
//
// LoadSceneAsync を使えば、ロードが複数フレームに分散されるため
// Overlay に毎フレーム描画の機会が与えられ、クラッシュを防げる。
//
// 【使い方】
// 既存コードの
//   SceneManager.LoadScene("RunScene");
// を
//   SafeSceneLoader.Load("RunScene");
// に置き換えるだけ。

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SafeSceneLoader
{
    private static bool _loading = false;
    public static bool IsLoading => _loading;

    /// <summary>
    /// シーンを非同期で安全にロードする。
    /// どの MonoBehaviour から呼んでも、内部で DontDestroyOnLoad な
    /// 一時オブジェクトを生成してコルーチンを回すため、
    /// 呼び出し元が破棄されても問題ない。
    /// </summary>
    public static void Load(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SafeSceneLoader] Scene name is null or empty.");
            return;
        }

        // 多重ロード防止
        if (_loading)
        {
            Debug.LogWarning($"[SafeSceneLoader] Already loading a scene. Ignoring request for '{sceneName}'.");
            return;
        }
        _loading = true;

        // 一時オブジェクトを作り、DontDestroyOnLoad で保護
        var go = new GameObject("__SafeSceneLoader__");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;

        var runner = go.AddComponent<SafeSceneLoaderRunner>();
        runner.Run(sceneName);
    }

    /// <summary>
    /// コルーチン実行用の内部 MonoBehaviour。
    /// ロード完了後に自動で自身を破棄する。
    /// </summary>
    private class SafeSceneLoaderRunner : MonoBehaviour
    {
        public void Run(string sceneName)
        {
            // Create an opaque persistent cover before the first yield. The previous
            // dialogue/map must not become visible while the next scene loads.
            var canvas=gameObject.AddComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder=32767;
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var cover=new GameObject("SceneTransitionCover",typeof(RectTransform),typeof(UnityEngine.UI.Image));
            cover.transform.SetParent(transform,false);
            var rect=(RectTransform)cover.transform;
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
            rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=cover.GetComponent<UnityEngine.UI.Image>();
            image.color=Color.black;image.raycastTarget=true;
            StartCoroutine(LoadCo(sceneName));
        }

        private IEnumerator LoadCo(string sceneName)
        {
            // ── Step 1: 1フレーム待機 ──
            // Steam Overlay が現在のフレームの描画を完了できるようにする。
            yield return null;

            // ── Step 2: 非同期ロード開始（まだシーンは切り替えない）──
            AsyncOperation op = null;
            try
            {
                op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SafeSceneLoader] LoadSceneAsync failed: {e.Message}");
                SafeSceneLoader._loading = false;
                Destroy(gameObject);
                yield break;
            }

            if (op == null)
            {
                Debug.LogError($"[SafeSceneLoader] LoadSceneAsync returned null for '{sceneName}'.");
                SafeSceneLoader._loading = false;
                Destroy(gameObject);
                yield break;
            }

            // allowSceneActivation = false にすると、progress が 0.9f で止まる。
            // その間 Overlay は通常通りフレームを描画し続けられる。
            op.allowSceneActivation = false;

            // ── Step 3: ロードが 90% になるまで待つ ──
            while (op.progress < 0.9f)
                yield return null;

            // ── Step 4: さらに数フレーム待機（Overlay に猶予を与える）──
            yield return null;
            yield return null;

            // ── Step 5: シーンを有効化 ──
            op.allowSceneActivation = true;

            // isDone になるまで待つ
            while (!op.isDone)
                yield return null;

            // Scene activation precedes Start and the first UI layout rebuild.
            // Keep the cover through those frames, even if timeScale is zero.
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            // 一時オブジェクトを破棄
            SafeSceneLoader._loading = false;
            Destroy(gameObject);
        }
    }
}
