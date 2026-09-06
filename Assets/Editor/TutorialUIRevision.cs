using System;
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

internal static class TutorialUIRevision {
    const string Request = "Temp/TutorialQA/ui-revision-request";
    const string MenuPath = "Assets/Scenes/MenuScene.unity", ShopPath = "Assets/Scenes/ShopScene.unity", RunPath = "Assets/Scenes/RunScene.unity";
    [InitializeOnLoadMethod] static void Schedule() { if(File.Exists(Request)) EditorApplication.delayCall += Run; }
    [MenuItem("Tools/Janshin/Apply UI Revision")]
    static void Run() {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Run; return; }
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        var previous=SceneManager.GetActiveScene();
        var opened=new System.Collections.Generic.List<Scene>();
        try {
            foreach(var path in new[]{MenuPath,ShopPath,RunPath}) {
                var current=SceneManager.GetSceneByPath(path);
                if(current.IsValid() && current.isLoaded && current.isDirty) throw new Exception("Unsaved scene: "+path);
            }
            Scene Open(string path) {
                var scene=SceneManager.GetSceneByPath(path);
                if(!scene.IsValid() || !scene.isLoaded) { scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive); opened.Add(scene); }
                return scene;
            }
            var menu=Open(MenuPath); var shop=Open(ShopPath); var run=Open(RunPath);
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/玉ねぎ楷書激無料版v7改 SDF.asset");
            if(!font) throw new Exception("Required font missing");
            foreach(var path in new[]{"Tutorial/Fonts/Japanese","Tutorial/Fonts/ChineseSimplified"}) {
                var fallback=Resources.Load<TMP_FontAsset>(path);
                if(fallback && fallback!=font && !font.fallbackFontAssetTable.Contains(fallback)) font.fallbackFontAssetTable.Add(fallback);
            }
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
            var panel=Components<IAPShopPanel>(menu).FirstOrDefault();
            if(!panel) {
                panel=Components<IAPShopPanel>(shop).Single();
                var so=new SerializedObject(panel);
                var entry=(Button)so.FindProperty("openShopButton").objectReferenceValue;
                entry.transform.SetParent(null,true);
                SceneManager.MoveGameObjectToScene(entry.gameObject,menu);
                SceneManager.MoveGameObjectToScene(panel.PanelRoot,menu);
                SceneManager.MoveGameObjectToScene(panel.gameObject,menu);
                so.FindProperty("gemCountTMP").objectReferenceValue=null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var panelSO=new SerializedObject(panel);
            var open=(Button)panelSO.FindProperty("openShopButton").objectReferenceValue;
            var controller=Components<MenuController>(menu).Single();
            var high=(TMP_Text)new SerializedObject(controller).FindProperty("highScoreTMP").objectReferenceValue;
            var highRect=high.rectTransform;
            var buttonRect=(RectTransform)open.transform;
            buttonRect.SetParent(highRect.parent,false);
            buttonRect.anchorMin=buttonRect.anchorMax=highRect.anchorMin;
            buttonRect.pivot=new Vector2(0,1);
            buttonRect.sizeDelta=new Vector2(Mathf.Clamp(highRect.rect.width,340,480),56);
            var world=new Vector3[4]; highRect.GetWorldCorners(world);
            var parent=(RectTransform)highRect.parent;
            high.ForceMeshUpdate(true);
            var bottomLeft=parent.InverseTransformPoint(highRect.TransformPoint(new Vector3(highRect.rect.xMin, Mathf.Min(highRect.rect.yMin,high.textBounds.min.y),0)));
            var anchor=parent.rect.min+Vector2.Scale(parent.rect.size,buttonRect.anchorMin);
            buttonRect.anchoredPosition=(Vector2)bottomLeft-anchor+new Vector2(0,-14);
            foreach(var text in open.GetComponentsInChildren<TMP_Text>(true)) {
                text.font=font; text.fontSizeMax=26; text.fontSizeMin=18;
                text.rectTransform.anchorMin=Vector2.zero; text.rectTransform.anchorMax=Vector2.one; text.rectTransform.offsetMin=new Vector2(12,6); text.rectTransform.offsetMax=new Vector2(-12,-6);
                text.text="宝石購入・広告カット";
            }
            foreach(var text in panel.PanelRoot.GetComponentsInChildren<TMP_Text>(true)) text.font=font;
            panel.PanelRoot.SetActive(false);
            var gm=Components<GameManager>(run).Single();
            var gmSO=new SerializedObject(gm);
            var trait=(TMP_Text)gmSO.FindProperty("_skillTraitGekiTMP").objectReferenceValue;
            var parentTrait=trait.transform.parent;
            var marker=parentTrait.Find("TutorialPassiveFocus") as RectTransform;
            if(!marker) {
                marker=new GameObject("TutorialPassiveFocus",typeof(RectTransform)).GetComponent<RectTransform>();
                marker.SetParent(parentTrait,false);
            }
            var targets=new[]{"skillTraitGekiTMP","skillTraitShunTMP","skillTraitIyuTMP","Geki","Syun","Yu"};
            bool first=true; Bounds bounds=default;
            foreach(var name in targets) {
                var target=parentTrait.Find(name) as RectTransform;
                if(!target) throw new Exception("Missing passive UI: "+name);
                target.GetWorldCorners(world);
                foreach(var corner in world) {
                    var point=parentTrait.InverseTransformPoint(corner);
                    if(first) { bounds=new Bounds(point,Vector3.zero); first=false; } else bounds.Encapsulate(point);
                }
            }
            marker.anchorMin=marker.anchorMax=marker.pivot=new Vector2(.5f,.5f);
            marker.localScale=Vector3.one; marker.anchoredPosition=bounds.center; marker.sizeDelta=bounds.size;
            gmSO.FindProperty("tutorialPassiveFocus").objectReferenceValue=marker; gmSO.ApplyModifiedPropertiesWithoutUndo();
            var content=Resources.Load<FirstMatchTutorialContent>("Tutorial/FirstMatchTutorialContent");
            content.pages.Single(p=>p.title.japanese=="パッシブスキル").focusTargets=new[]{FirstMatchTutorialContent.FocusTarget.PassiveSkills};
            content.pages.Single(p=>p.title.japanese=="お守り").focusTargets=new[]{FirstMatchTutorialContent.FocusTarget.Omamori};
            content.pages.Single(p=>p.title.japanese=="お札").focusTargets=new[]{FirstMatchTutorialContent.FocusTarget.Ofuda};
            EditorUtility.SetDirty(content); AssetDatabase.SaveAssetIfDirty(content);
            foreach(var path in new[]{"Assets/Resources/Tutorial/FirstMatchTutorial.prefab","Assets/Resources/Commerce/IAPShop.prefab"}) {
                var root=PrefabUtility.LoadPrefabContents(path);
                try {
                    foreach(var text in root.GetComponentsInChildren<TMP_Text>(true)) text.font=font;
                    var view=root.GetComponent<FirstMatchTutorialView>();
                    if(view) { var so=new SerializedObject(view); so.FindProperty("font").objectReferenceValue=font; so.ApplyModifiedPropertiesWithoutUndo(); }
                    foreach(var text in root.GetComponentsInChildren<TMP_Text>(true)) {
                        switch(text.transform.parent.name) {
                            case "RemoveAds": text.text="広告カット   800円"; break;
                            case "Gems10": text.text="宝石 ×10   100円"; break;
                            case "Gems55": text.text="宝石 ×55   400円"; break;
                            case "Gems130": text.text="宝石 ×130   900円"; break;
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                } finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach(var scene in new[]{menu,shop,run}) {
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed: "+scene.path);
            }
            File.WriteAllText("Temp/TutorialQA/ui-revision-results.txt","SUCCESS");
            EditorApplication.delayCall += () => { typeof(FirstMatchTutorialQA).GetMethod("Run",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null); CommerceQA.Run(); };
        } catch(Exception e) {
            File.WriteAllText("Temp/TutorialQA/ui-revision-results.txt","FAIL "+e); Debug.LogException(e);
        } finally {
            if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            foreach(var scene in opened) if(scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene,true);
        }
    }
    static T[] Components<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
}
