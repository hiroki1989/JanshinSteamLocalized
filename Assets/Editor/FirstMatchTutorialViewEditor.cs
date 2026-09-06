using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(FirstMatchTutorialView))]
public sealed class FirstMatchTutorialViewEditor : Editor
{
    private int previewPage;
    private LocalizationManager.Language previewLanguage;
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("見た目は子ObjectのRectTransform・TMP・Imageで編集します。説明文とボタン名はContent Assetで編集します。Auto Position CardをOFFにすると手動位置を維持します。", MessageType.Info);
        DrawDefaultInspector();
        var view = (FirstMatchTutorialView)target;
        if (!view.contentAsset) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("編集用プレビュー（進行データは変更しません）", EditorStyles.boldLabel);
        previewLanguage = (LocalizationManager.Language)EditorGUILayout.EnumPopup("Language", previewLanguage);
        var names = view.contentAsset.pages.Select((p, i) => (i + 1) + ". " + p.editorLabel).ToArray();
        if (names.Length == 0) return;
        previewPage = Mathf.Clamp(EditorGUILayout.Popup("Page", previewPage, names), 0, names.Length - 1);
        if (GUILayout.Button("選択ページをプレビュー"))
        {
            Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Preview tutorial page");
            view.PreviewPage(previewPage, previewLanguage);
            EditorUtility.SetDirty(view);
            FirstMatchTutorialPrefabSetup.FramePreview(view);
        }
        if (GUILayout.Button("説明文データを選択"))
            Selection.activeObject = view.contentAsset;
    }
}

public static class FirstMatchTutorialPrefabSetup
{
    public const string PrefabPath = "Assets/Resources/Tutorial/FirstMatchTutorial.prefab";
    public const string ContentPath = "Assets/Resources/Tutorial/FirstMatchTutorialContent.asset";
    const string Request = "Temp/TutorialQA/prefab-request";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [InitializeOnLoadMethod]
    static void OnReload()
    {
        if (File.Exists(Request)) EditorApplication.delayCall += Install;
    }

    [MenuItem("Tools/Janshin/Open Tutorial Prefab")]
    public static void Open()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
        {
            var stage = PrefabStageUtility.OpenPrefab(PrefabPath);
            EditorApplication.delayCall += () => FramePreview(stage.prefabContentsRoot.GetComponent<FirstMatchTutorialView>());
        }
        else Install();
    }

    public static void FramePreview(FirstMatchTutorialView view)
    {
        if (!view || Application.isPlaying) return;
        Canvas.ForceUpdateCanvases();
        foreach (var text in view.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
            text.ForceMeshUpdate(true);
        var card = view.transform.Find("SafeArea/GuideCard") as RectTransform;
        if (!card) return;
        var corners = new Vector3[4];
        card.GetWorldCorners(corners);
        var bounds = new Bounds(corners[0], Vector3.zero);
        foreach (var corner in corners) bounds.Encapsulate(corner);
        if (bounds.size.x <= 0 || bounds.size.y <= 0)
        {
            Debug.LogError("Tutorial preview has zero-sized bounds.");
            return;
        }
        var sceneView = EditorWindow.GetWindow<SceneView>();
        sceneView.Show();
        sceneView.in2DMode = true;
        SceneVisibilityManager.instance.Show(view.gameObject, true);
        Tools.visibleLayers |= 1 << view.gameObject.layer;
        Selection.activeGameObject = view.gameObject;
        sceneView.Frame(bounds, true);
        sceneView.Focus();
        SceneView.RepaintAll();
    }
    [MenuItem("Tools/Janshin/Create Missing Tutorial Assets")]
    public static void Install()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += Install; return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(Request)) File.Delete(Request);
        Scene preview = default;
        var singleton = typeof(LocalizationManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
        var originalSingleton = singleton.GetValue(null);
        try
        {
            Directory.CreateDirectory("Assets/Resources/Tutorial");
            var content = AssetDatabase.LoadAssetAtPath<FirstMatchTutorialContent>(ContentPath);
            if (!content)
            {
                preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/RunScene.unity");
                var gm = preview.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameManager>(true)).First();
                typeof(GameManager).GetField("_skillSet", Flags).SetValue(gm, typeof(GameManager).GetField("fallbackSkillSet", Flags).GetValue(gm));
                var localGO = new GameObject("Tutorial content migration");
                SceneManager.MoveGameObjectToScene(localGO, preview);
                var local = localGO.AddComponent<LocalizationManager>();
                singleton.SetValue(null, local);
                var defaults = new List<FirstMatchTutorialView.Page>[3];
                for (int i = 0; i < 3; i++)
                {
                    typeof(LocalizationManager).GetField("currentLanguage", Flags).SetValue(local, (LocalizationManager.Language)i);
                    defaults[i] = (List<FirstMatchTutorialView.Page>)typeof(GameManager).GetMethod("BuildDefaultFirstMatchTutorialPages", Flags).Invoke(gm, null);
                }
                if (defaults.Any(p => p.Count != 10)) throw new Exception("Expected ten tutorial defaults for migration.");
                content = ScriptableObject.CreateInstance<FirstMatchTutorialContent>();
                var targets = new[]
                {
                    Array.Empty<FirstMatchTutorialContent.FocusTarget>(),
                    new[] { FirstMatchTutorialContent.FocusTarget.PlayerHP, FirstMatchTutorialContent.FocusTarget.EnemyHP },
                    new[] { FirstMatchTutorialContent.FocusTarget.Hand, FirstMatchTutorialContent.FocusTarget.Offer },
                    new[] { FirstMatchTutorialContent.FocusTarget.Offer, FirstMatchTutorialContent.FocusTarget.Hand },
                    Array.Empty<FirstMatchTutorialContent.FocusTarget>(),
                    new[] { FirstMatchTutorialContent.FocusTarget.Shanten },
                    new[] { FirstMatchTutorialContent.FocusTarget.EnemyDiscard, FirstMatchTutorialContent.FocusTarget.Discard },
                    new[] { FirstMatchTutorialContent.FocusTarget.MP, FirstMatchTutorialContent.FocusTarget.SkillButton },
                    new[] { FirstMatchTutorialContent.FocusTarget.SkillInfo },
                    Array.Empty<FirstMatchTutorialContent.FocusTarget>()
                };
                for (int i = 0; i < defaults[0].Count; i++)
                {
                    var step = new FirstMatchTutorialContent.Step
                    {
                        editorLabel = defaults[0][i].Title,
                        title = new FirstMatchTutorialContent.Localized(defaults[0][i].Title, defaults[1][i].Title, defaults[2][i].Title),
                        body = new FirstMatchTutorialContent.Localized(defaults[0][i].Body, defaults[1][i].Body, defaults[2][i].Body),
                        hint = new FirstMatchTutorialContent.Localized(defaults[0][i].Hint, defaults[1][i].Hint, defaults[2][i].Hint),
                        requiresEquippedSkill = i == 8,
                        focusTargets = targets[i],
                        exampleTiles = defaults[0][i].Tiles ?? Array.Empty<string>()
                    };
                    if (i == 8)
                    {
                        step.body = new FirstMatchTutorialContent.Localized("{skillName}\n\n{skillDescription}", "{skillName}\n\n{skillDescription}", "{skillName}\n\n{skillDescription}");
                        step.hint = new FirstMatchTutorialContent.Localized("消費MP：{mpCost}", "MP cost: {mpCost}", "消耗MP：{mpCost}");
                    }
                    content.pages.Add(step);
                }
                AssetDatabase.CreateAsset(content, ContentPath);
                AssetDatabase.SaveAssetIfDirty(content);
            }
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
            {
                if (!preview.IsValid()) preview = EditorSceneManager.NewPreviewScene();
                var root = new GameObject("FirstMatchTutorial", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, preview);
                var view = root.AddComponent<FirstMatchTutorialView>();
                view.contentAsset = content;
                view.CreateDefaultHierarchy(content.Resolve(LocalizationManager.Language.Japanese),
                    Resources.Load<TMPro.TMP_FontAsset>("Tutorial/Fonts/Japanese"), LocalizationManager.Language.Japanese, null);
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var rect = (RectTransform)root.transform;
                rect.localScale = Vector3.one;
                rect.sizeDelta = new Vector2(1600, 900);
                rect.pivot = new Vector2(.5f, .5f);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            File.WriteAllText("Temp/TutorialQA/prefab-setup-result.txt", "SUCCESS\n" + PrefabPath + "\n" + ContentPath);
            File.WriteAllText("Temp/TutorialQA/request", "Validate prefab instance");
            EditorApplication.delayCall += () =>
            {
                typeof(FirstMatchTutorialQA).GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                Open();
            };
        }
        catch (Exception e)
        {
            File.WriteAllText("Temp/TutorialQA/prefab-setup-result.txt", e.ToString());
            Debug.LogException(e);
        }
        finally
        {
            singleton.SetValue(null, originalSingleton);
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
