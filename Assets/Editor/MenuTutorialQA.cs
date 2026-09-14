using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static class MenuTutorialQA
{
    const string Folder = "Logs/MenuTutorialQA";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    static readonly List<string> Results = new List<string>();
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Results.Add("PASS " + message); }
    [InitializeOnLoadMethod] static void Reload()
    {
        if (File.Exists(Folder + "/request")) EditorApplication.delayCall += Run;
    }
    [MenuItem("Tools/Janshin/Validate Menu Tutorial and Descriptions")]
    public static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Run; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory(Folder);
        File.Delete(Folder + "/request");
        Results.Clear();
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/MenuScene.unity");
        RenderTexture target = null;
        float previousTime = Time.timeScale;
        try
        {
            var menu = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MenuController>(true)).Single();
            var pages = (List<FirstMatchTutorialView.Page>)Call(menu, "BuildMenuTutorialPages");
            Check(pages.Count == 7 && pages.All(p => p.Targets.Length == 1 && p.Targets[0]), "Seven menu pages resolve all button targets");
            var cameraObject = new GameObject("QA Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            target = new RenderTexture(1180,820,24); camera.targetTexture = target;
            foreach (var canvas in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).Where(c => c.isRootCanvas))
            {
                var frame = MobileDisplayGuard.PrepareCanvas(canvas);
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
                MobileDisplayGuard.ApplyViewport(canvas, frame, 1180,820,new Rect(0,0,1180,820));
            }
            var prefab = Resources.Load<FirstMatchTutorialView>("Tutorial/FirstMatchTutorial");
            var view = Object.Instantiate(prefab, menu.transform, false);
            var viewCanvas = view.GetComponent<Canvas>();
            viewCanvas.renderMode = RenderMode.ScreenSpaceCamera; viewCanvas.worldCamera = camera; viewCanvas.planeDistance = 5;
            Call(view, "Build", pages, LocalizationManager.Instance.GetBodyFont(), LocalizationManager.Instance.CurrentLanguage, (Action)(() => {}));
            for (int i = 0; i < pages.Count; i++)
            {
                view.GetType().GetField("index", Flags).SetValue(view, i);
                Call(view, "ShowPage"); Canvas.ForceUpdateCanvases(); Call(view,"Layout"); Canvas.ForceUpdateCanvases();
                var body = (TMP_Text)Get(view, "bodyText");
                Check(!string.IsNullOrWhiteSpace(body.text), "Body populated page " + (i+1));
                if (LocalizationManager.Instance.CurrentLanguage == LocalizationManager.Language.Japanese)
                    Check(body.font.name.Contains("Meiryo"), "Meiryo body page " + (i+1));
                camera.Render();
                var previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(1180,820,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1180,820),0,0); image.Apply();
                File.WriteAllBytes(Folder + "/menu-" + (i+1) + ".png", image.EncodeToPNG());
                Object.DestroyImmediate(image); RenderTexture.active = previous;
            }
            camera.targetTexture = null;
            Results.Add("SUCCESS menu previews");
        }
        catch (Exception ex) { Results.Add("FAIL " + ex); Debug.LogException(ex); }
        finally
        {
            if (target) Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(scene);
            Time.timeScale = previousTime;
            File.WriteAllLines(Folder + "/results.txt", Results);
        }
    }
}
