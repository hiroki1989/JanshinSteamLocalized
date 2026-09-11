using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Preview scenes only: no gameplay saves, scene serialization, or purchases are changed.
public static class MobileLayoutQA
{
    private static readonly List<string> Results = new List<string>();
    private const string Output = "Logs/MobileLayoutQA";
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Results.Add("PASS " + description);
    }

    [MenuItem("Tools/Janshin/Validate Mobile Layout")]
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(2360, 1640), new Vector2Int(2048, 1536), new Vector2Int(2796, 1290) })
            {
                foreach (var reference in new[] { new Vector2(1920, 1080), new Vector2(1600, 900) })
                foreach (var safe in new[] { new Rect(0, 0, size.x, size.y), new Rect(80, 24, size.x - 120, size.y - 48) })
                {
                    var fit = MobileDisplayGuard.FitViewport(size.x, size.y, safe, reference);
                    Check(fit.xMin >= safe.xMin - .01f && fit.xMax <= safe.xMax + .01f && fit.yMin >= safe.yMin - .01f && fit.yMax <= safe.yMax + .01f,
                        size + " " + reference + " contained in " + safe);
                    Check(Mathf.Abs(fit.width / fit.height - reference.x / reference.y) < .001f, "No aspect distortion");
                }
            }
            foreach (var setting in EditorBuildSettings.scenes.Where(s => s.enabled)) RenderScene(setting.path);
            RenderScene("Assets/Scenes/UpgradeScene.unity", "Tiles");
            RenderScene("Assets/Scenes/RunScene.unity", "Skill");
            Results.Add("SUCCESS");
        }
        catch (Exception ex) { Results.Add("FAIL " + ex); Debug.LogException(ex); }
        finally { File.WriteAllLines(Output + "/results.txt", Results); }
        if (Application.isBatchMode) EditorApplication.Exit(Results.Last() == "SUCCESS" ? 0 : 1);
    }

    private static void RenderScene(string path, string variant = "Scene")
    {
        var scene = EditorSceneManager.OpenPreviewScene(path);
        RenderTexture target = null;
        Camera camera = null;
        SkillDescriptionPopup popup = null;
        try
        {
            if (variant == "Tiles")
            {
                var manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UpgradeManager>(true)).First();
                manager.BuildSelectedTileShopUI();
                typeof(UpgradeManager).GetMethod("OpenSelectedTileShop", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(manager, new object[] { false });
            }
            if (variant == "Skill")
                popup = SkillDescriptionPopup.Show(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameManager>(true)).First().transform, "スキル説明 / Skill", "スキルを選んで、狙う役に合わせて手牌を整えましょう。\nChoose a skill to help shape your hand.", null);
            var cameraGo = new GameObject("Mobile Layout QA Camera");
            SceneManager.MoveGameObjectToScene(cameraGo, scene);
            camera = cameraGo.AddComponent<Camera>();
            camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var canvases = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true))
                .Where(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace && !c.GetComponent<FirstMatchTutorialView>()).ToArray();
            var frames = new Dictionary<Canvas, RectTransform>();
            foreach (var canvas in canvases)
            {
                frames[canvas] = MobileDisplayGuard.PrepareCanvas(canvas);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10;
                // Test the authored children, including currently inactive panels, for unchanged geometry.
                Check(frames[canvas].sizeDelta == canvas.GetComponent<CanvasScaler>().referenceResolution, path + " keeps design resolution");
                Check(MobileDisplayGuard.PrepareCanvas(canvas) == frames[canvas], path + " repeated setup is idempotent");
            }
            foreach (var size in new[] { new Vector2Int(1180, 820), new Vector2Int(1024, 768), new Vector2Int(1398, 645) })
            {
                target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                var safe = size.x == 1398 ? new Rect(40, 12, size.x - 60, size.y - 24) : new Rect(0, 12, size.x, size.y - 24);
                Canvas.ForceUpdateCanvases();
                foreach (var canvas in canvases) MobileDisplayGuard.ApplyViewport(canvas, frames[canvas], size.x, size.y, safe);
                Canvas.ForceUpdateCanvases();
                foreach (var canvas in canvases.Where(c => c.isActiveAndEnabled))
                {
                    var corners = new Vector3[4]; frames[canvas].GetWorldCorners(corners);
                    foreach (var world in corners)
                    {
                        var point = camera.WorldToScreenPoint(world);
                        Check(point.x >= safe.xMin - 1 && point.x <= safe.xMax + 1 && point.y >= safe.yMin - 1 && point.y <= safe.yMax + 1,
                            path + " " + size + " actual frame corner " + point + " in safe area");
                    }
                    foreach (var bar in canvas.GetComponentsInChildren<Image>(true).Where(i => i.name.StartsWith("__Bar")))
                        Check(!bar.raycastTarget, "Margins do not intercept taps");
                    foreach (var control in frames[canvas].GetComponentsInChildren<Selectable>())
                    {
                        if (!control.IsActive() || control.GetComponentInParent<ScrollRect>()) continue;
                        var rect = (RectTransform)control.transform;
                        rect.GetWorldCorners(corners);
                        bool fits = corners.All(c => { var p = camera.WorldToScreenPoint(c); return p.x >= safe.xMin - 1 && p.x <= safe.xMax + 1 && p.y >= safe.yMin - 1 && p.y <= safe.yMax + 1; });
                        if (!fits) Results.Add("CONTROL OUTSIDE " + path + " " + variant + " " + size + " " + control.name);
                    }
                }
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var png = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                png.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); png.Apply();
                File.WriteAllBytes(Output + "/" + Path.GetFileNameWithoutExtension(path) + "-" + variant + "-" + size.x + ".png", png.EncodeToPNG());
                Object.DestroyImmediate(png); RenderTexture.active = previous;
                camera.targetTexture = null; Object.DestroyImmediate(target); target = null;
            }
            Results.Add("SCENE " + path);
        }
        finally
        {
            if (popup) Object.DestroyImmediate(popup.gameObject);
            if (camera) camera.targetTexture = null;
            if (target) Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
