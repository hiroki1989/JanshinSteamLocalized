// Run from Tools/Janshin/Validate First Match Tutorial. Uses preview scenes and an isolated preference key.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static class FirstMatchTutorialQA
{
    const string Folder = "Temp/TutorialQA";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly List<string> results = new List<string>();
    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
    static void Check(bool value, string name) { if (!value) throw new Exception(name); results.Add("PASS " + name); }

    static TMP_FontAsset EnsureFont(string name, string source)
    {
        string directory = "Assets/Resources/Tutorial/Fonts";
        Directory.CreateDirectory(directory);
        AssetDatabase.Refresh();
        string path = directory + "/" + name + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(source);
        if (asset && asset.sourceFontFile == sourceFont) return asset;
        if (asset) AssetDatabase.DeleteAsset(path);
        if (!sourceFont) throw new Exception("Missing source font: " + source);
        asset = TMP_FontAsset.CreateFontAsset(sourceFont, 64, 7,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        asset.name = "Tutorial " + name;
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        foreach (var atlas in asset.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, asset);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    [InitializeOnLoadMethod]
    static void OnReload()
    {
        if (File.Exists(Folder + "/request"))
            EditorApplication.delayCall += Run;
    }

    [MenuItem("Tools/Janshin/Validate First Match Tutorial")]
    static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += Run; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory(Folder);
        if (File.Exists(Folder + "/request")) File.Delete(Folder + "/request");
        results.Clear();
        var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/RunScene.unity");
        string key = "TutorialQA_" + Guid.NewGuid().ToString("N");
        var singleton = typeof(LocalizationManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        var oldSingleton = singleton.GetValue(null);
        float oldTime = Time.timeScale;
        GameManager gm = null;
        IEnumerator routine = null;
        Camera camera = null;
        RenderTexture texture = null;
        try
        {
            gm = preview.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameManager>(true)).First();
            Set(gm, "tutorialDoneKey", key);
            Set(gm, "_skillSet", Get(gm, "fallbackSkillSet"));
            var localGO = new GameObject("Tutorial QA Localization");
            SceneManager.MoveGameObjectToScene(localGO, preview);
            var local = localGO.AddComponent<LocalizationManager>();
            singleton.SetValue(null, local);
            var japanese = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/meiryo SDF.asset");
            Check(japanese, "Japanese font is available");
            var english = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            var chinese = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/XCDUANZHUANGSONGTI SDF.asset");
            japanese = EnsureFont("Japanese", "Assets/Fonts/meiryo.ttc");
            chinese = EnsureFont("ChineseSimplified", "Assets/Fonts/XCDUANZHUANGSONGTI.ttf");
            Set(local, "japaneseFonts", new LocalizationManager.FontSet { bodyFont = japanese });
            Set(local, "englishFonts", new LocalizationManager.FontSet { bodyFont = english });
            Set(local, "chineseSimplifiedFonts", new LocalizationManager.FontSet { bodyFont = chinese ? chinese : japanese });

            Check((bool)Call(gm, "__ShouldShowFirstMatchTutorial"), "Fresh profile shows tutorial with empty legacy panels");
            Set(gm, "tutorialEnabled", false);
            Check(!(bool)Call(gm, "__ShouldShowFirstMatchTutorial"), "Disabled tutorial does not show");
            Set(gm, "tutorialEnabled", true);

            Time.timeScale = .75f;
            Set(gm, "_freezeProgression", true);
            routine = (IEnumerator)Call(gm, "__RunFirstMatchTutorial_Co");
            Check(routine.MoveNext(), "Tutorial starts and waits");
            Check(Time.timeScale == 0 && (bool)Get(gm, "_freezeProgression"), "Progression and scaled time paused");
            Check(!(bool)Call(gm, "__ShouldShowFirstMatchTutorial"), "Duplicate start prevented");
            var view = (MonoBehaviour)Get(gm, "_tutorialView");
            Check(Resources.Load<FirstMatchTutorialView>("Tutorial/FirstMatchTutorial"), "Prefab is loadable as a component with serialized references");
            var originalContent = Get(gm, "tutorialContent");
            var editedContent = Object.Instantiate(((FirstMatchTutorialView)view).contentAsset);
            try
            {
                editedContent.pages[0].body.japanese = "Edited tutorial copy";
                Set(gm, "tutorialContent", editedContent);
                var editedPages = (List<FirstMatchTutorialView.Page>)Call(gm, "BuildFirstMatchTutorialPages");
                Check(editedPages[0].Body == "Edited tutorial copy", "Content asset edits reach the runtime pages");
            }
            finally { Set(gm, "tutorialContent", originalContent); Object.DestroyImmediate(editedContent); }
            var editedCard = (RectTransform)Get(view, "card");
            Set(view, "autoPositionCard", false);
            var authoredPosition = new Vector2(123, -45);
            var authoredSize = new Vector2(690, 680);
            editedCard.anchoredPosition = authoredPosition;
            editedCard.sizeDelta = authoredSize;
            var authoredColor = new Color(.18f, .27f, .38f);
            editedCard.GetComponent<Image>().color = authoredColor;
            var normalBody = (TextMeshProUGUI)Get(view, "bodyText");
            normalBody.rectTransform.offsetMin = new Vector2(8, 11);
            int beforeBuildCount = view.GetComponentsInChildren<Transform>(true).Length;
            Call(view, "Build", Get(view, "pages"), japanese, LocalizationManager.Language.Japanese, (Action)(() => { }));
            Call(view, "Layout");
            Check(editedCard.anchoredPosition == authoredPosition && editedCard.sizeDelta == authoredSize, "Manual card size and position survive binding");
            Check(editedCard.GetComponent<Image>().color == authoredColor, "Authored colors survive binding");
            Check(normalBody.rectTransform.offsetMin == new Vector2(8, 11), "Authored body layout survives page switching");
            Check(view.GetComponentsInChildren<Transform>(true).Length == beforeBuildCount, "Binding uses existing prefab children without rebuilding");
            ((Button)Get(view, "next")).onClick.Invoke();
            Check((int)Get(view, "index") == 1, "Repeated binding does not duplicate navigation listeners");
            // Restore the coroutine completion callback by restarting this isolated test instance.
            ((IDisposable)routine).Dispose(); routine = null;
            routine = (IEnumerator)Call(gm, "__RunFirstMatchTutorial_Co");
            routine.MoveNext();
            view = (MonoBehaviour)Get(gm, "_tutorialView");

            ((Button)Get(view, "skip")).onClick.Invoke();
            Check(!PlayerPrefs.HasKey(key), "Skip opens confirmation without marking completion");
            ((Button)Get(view, "back")).onClick.Invoke();
            Check(!(bool)Get(view, "confirmingSkip"), "Back cancels skip");
            ((Button)Get(view, "skip")).onClick.Invoke();
            ((Button)Get(view, "next")).onClick.Invoke();
            Check(!routine.MoveNext(), "Confirmed skip finishes");
            routine = null;
            Check(PlayerPrefs.GetInt(key, 0) == 1 && !(bool)Call(gm, "__ShouldShowFirstMatchTutorial"), "Completion persists and suppresses another launch");
            Check(Mathf.Approximately(Time.timeScale, .75f) && (bool)Get(gm, "_freezeProgression"), "Previous pause state restored");

            PlayerPrefs.DeleteKey(key);
            routine = (IEnumerator)Call(gm, "__RunFirstMatchTutorial_Co");
            routine.MoveNext();
            ((IDisposable)routine).Dispose(); routine = null;
            Check(!PlayerPrefs.HasKey(key) && !(bool)Get(gm, "_tutorialRunning"), "Interrupted tutorial remains incomplete and releases input");
            Check(Mathf.Approximately(Time.timeScale, .75f), "Interrupted tutorial restores time");

            var cameraGO = new GameObject("Tutorial QA Camera");
            SceneManager.MoveGameObjectToScene(cameraGO, preview);
            camera = cameraGO.AddComponent<Camera>();
            camera.scene = preview;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.14f, .2f, .23f);
            camera.cullingMask = 1 << 31;
            camera.enabled = false;
            // Populate only the isolated preview, using the same tile prefab and layout groups as gameplay.
            var tilePrefab=(GameObject)Get(gm,"tilePrefab");
            foreach(var pair in new[]{("handArea",13),("offerArea",4),("enemyDiscardArea",6),("discardArea",6)}) {
                var tileArea=(RectTransform)Get(gm,pair.Item1);
                for(int tile=0;tile<pair.Item2;tile++) { var clone=Object.Instantiate(tilePrefab,tileArea,false); Call(gm,"SetupTile",clone,GameManager.IndexToId(tile%9),tile,pair.Item1=="handArea",pair.Item1=="offerArea",true); }
            }
            foreach(var sceneRoot in preview.GetRootGameObjects()) {
                foreach(var tr in sceneRoot.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer=31;
                foreach(var sourceCanvas in sceneRoot.GetComponentsInChildren<Canvas>(true)) {
                    sourceCanvas.renderMode=RenderMode.ScreenSpaceCamera; sourceCanvas.worldCamera=camera; sourceCanvas.planeDistance=10;
                }
            }
            foreach (LocalizationManager.Language lang in Enum.GetValues(typeof(LocalizationManager.Language)))
            {
                Set(local, "currentLanguage", lang);
                routine = (IEnumerator)Call(gm, "__RunFirstMatchTutorial_Co");
                routine.MoveNext();
                view = (MonoBehaviour)Get(gm, "_tutorialView");
                foreach (var tr in view.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 31;
                var canvas = view.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10;
                var pages = (IList)Get(view, "pages");
                foreach (var size in new[] { new Vector2Int(1600, 900), new Vector2Int(1280, 720), new Vector2Int(2560, 1080) })
                {
                    texture = new RenderTexture(size.x, size.y, 24);
                    camera.targetTexture = texture;
                    for (int i = 0; i < pages.Count; i++)
                    {
                        Set(view, "index", i);
                        Call(view, "ShowPage");
                        Canvas.ForceUpdateCanvases();
                        Call(view, "Layout");
                        Canvas.ForceUpdateCanvases();
                        var focusRect=(RectTransform)Get(view,"focus");
                        var cardRect=(RectTransform)Get(view,"card");
                        if(focusRect.gameObject.activeSelf) {
                            Rect ScreenRect(RectTransform rect) {
                                var points=new Vector3[4]; rect.GetWorldCorners(points);
                                var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
                                var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
                                foreach(var point in points) { var screen=(Vector2)camera.WorldToScreenPoint(point); min=Vector2.Min(min,screen); max=Vector2.Max(max,screen); }
                                return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
                            }
                            foreach(var frame in (List<RectTransform>)Get(view,"focusFrames")) if(frame.gameObject.activeSelf) Check(!ScreenRect(cardRect).Overlaps(ScreenRect(frame)),lang+" "+size+" step "+(i+1)+" card clears focus");
                            if(i==1 || i==2 || i==3) Check(((List<Rect>)Get(view,"focusRegions")).Count==2,"Two separate focus windows on page "+(i+1));
                            if(i==1) Check(cardRect.localScale.x>=.95f,"HP page keeps a large card" );
                            Check(cardRect.localScale.x>=.4f,lang+" step "+(i+1)+" readable card scale "+cardRect.localScale.x);
                        }
                        foreach (var text in view.GetComponentsInChildren<TextMeshProUGUI>())
                        {
                            text.ForceMeshUpdate(true);
                            Check(text.font.HasCharacters(text.text, out uint[] missing, true, true), lang + " glyphs " + text.name + ": " + string.Join(",", missing ?? Array.Empty<uint>()));
                            Check(!text.isTextOverflowing, lang + " " + size + " step " + (i + 1) + " " + text.name + " fits");
                        }
                        if (size.x == 1600 && (i == 0 || i == 1 || i == 3 || i == 2 || i == 6 || i == 4 || i == 7 || i == 8 || i == 9 || i == 10 || i == 11 || i == pages.Count - 1))
                        {
                            camera.Render();
                            var old = RenderTexture.active;
                            RenderTexture.active = texture;
                            var png = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                            png.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                            png.Apply();

                            File.WriteAllBytes(Folder + "/" + lang + "-step" + (i + 1) + ".png", png.EncodeToPNG());
                            Object.DestroyImmediate(png);
                            RenderTexture.active = old;
                        }
                    }
                    camera.targetTexture = null;
                    Object.DestroyImmediate(texture); texture = null;
                }
                ((Button)Get(view, "next")).onClick.Invoke();
                Check(!routine.MoveNext(), lang + " final page completes");
                routine = null;
                Check(PlayerPrefs.GetInt(key, 0) == 1, lang + " final completion saved");
                PlayerPrefs.DeleteKey(key);
            }
            results.Add("SUCCESS");
            EditorApplication.delayCall += CommerceQA.Run;
        }
        catch (Exception e) { results.Add("FAIL " + e); Debug.LogException(e); }
        finally
        {
            if (routine is IDisposable disposable) disposable.Dispose();
            if (gm) Call(gm, "__CleanupFirstMatchTutorial");
            if (camera) camera.targetTexture = null;
            if (texture) Object.DestroyImmediate(texture);
            Time.timeScale = oldTime;
            singleton.SetValue(null, oldSingleton);
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            EditorSceneManager.ClosePreviewScene(preview);
            File.WriteAllLines(Folder + "/results.txt", results);
            Debug.Log("[Tutorial QA] " + results.LastOrDefault() + "; " + Folder + "/results.txt");
        }
    }
}
