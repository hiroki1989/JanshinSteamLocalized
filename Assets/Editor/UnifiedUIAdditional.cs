using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class UnifiedUIAdditional
{
    public static void InstallAndPreview()
    {
        Run();
        var menu=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.name=="MenuPanel");
        menu.gameObject.SetActive(true);
        var group=menu.GetComponent<CanvasGroup>();if(group)group.alpha=1;
        foreach(var t in menu.GetComponentsInChildren<TMP_Text>(true)){t.color=JanshinPanelTheme.Ivory;t.ForceMeshUpdate();}
        Canvas.ForceUpdateCanvases();
        BattleHUDQA.Capture("Logs/UnifiedUIPreviews/RunScene_MenuFinal.png",1920,1080);
    }
    public static void Run()
    {
        foreach(var name in new[]{"OtherScene","RunScene"}){
            string path="Assets/Scenes/"+name+".unity",backup="Logs/UnifiedUIBackup/"+name+".unity";
            if(!File.Exists(backup))File.Copy(path,backup);
            var scene=EditorSceneManager.OpenScene(path);
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var roots=name=="RunScene"?all.Where(t=>t.name=="MenuPanel").ToArray():all.Where(t=>t.GetComponent<Canvas>()&&t.GetComponent<Canvas>().isRootCanvas).ToArray();
            foreach(var root in roots){
                if(!root.GetComponent<JanshinThemeTextScope>())root.gameObject.AddComponent<JanshinThemeTextScope>();
                foreach(var image in root.GetComponentsInChildren<Image>(true).ToArray()){
                    if(image.name=="UnifiedGoldBorder")continue;
                    var asset=image.sprite?AssetDatabase.GetAssetPath(image.sprite):"";
                    if(asset.Contains("GUI_Parts/Gui_parts/Frame")||asset.Contains("Consumables/PanelFrame")||image.name=="OptionPanel")JanshinPanelTheme.Apply(image);
                }
                foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))JanshinPanelTheme.Text(text);
                foreach(var button in root.GetComponentsInChildren<Button>(true)){
                    JanshinPanelTheme.Button(button);
                    foreach(var text in button.GetComponentsInChildren<TMP_Text>(true))text.margin=new Vector4(14,8,14,8);
                }
                if(name=="RunScene"){
                    var theme=root.GetComponent<JanshinPanelTheme>();if(theme)Object.DestroyImmediate(theme);
                    var old=root.Find("UnifiedGoldBorder");if(old)Object.DestroyImmediate(old.gameObject);
                    var shade=root.GetComponent<Image>();shade.sprite=null;shade.color=new Color(0,0,0,.78f);
                    var card=root.Find("PauseCard") as RectTransform;
                    if(!card){card=new GameObject("PauseCard",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();card.SetParent(root,false);}
                    card.anchorMin=card.anchorMax=card.pivot=Vector2.one*.5f;card.anchoredPosition=Vector2.zero;card.sizeDelta=new Vector2(740,640);card.localScale=Vector3.one;
                    JanshinPanelTheme.Apply(card.GetComponent<Image>());
                    var names=new[]{"Button_MenuOption","Button_Suspend","Button_Exit","Button_MenuClose"};
                    var buttons=names.Select(n=>root.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name==n)).Where(b=>b).ToArray();
                    for(int i=0;i<buttons.Length;i++){
                        foreach(var oldFace in buttons[i].GetComponentsInChildren<Image>(true))if(oldFace.transform!=buttons[i].transform&&oldFace.name!="UnifiedGoldBorder")oldFace.enabled=false;
                        var face=buttons[i].GetComponent<Image>()??buttons[i].gameObject.AddComponent<Image>();buttons[i].targetGraphic=face;JanshinPanelTheme.Button(buttons[i]);
                        var rt=(RectTransform)buttons[i].transform;rt.SetParent(card,false);rt.anchorMin=rt.anchorMax=rt.pivot=Vector2.one*.5f;rt.localScale=Vector3.one;rt.sizeDelta=new Vector2(560,96);rt.anchoredPosition=new Vector2(0,(buttons.Length-1)*60-i*120);
                        foreach(var t in buttons[i].GetComponentsInChildren<TMP_Text>(true)){t.rectTransform.anchorMin=Vector2.zero;t.rectTransform.anchorMax=Vector2.one;t.rectTransform.offsetMin=new Vector2(16,8);t.rectTransform.offsetMax=new Vector2(-16,-8);t.enableAutoSizing=true;t.fontSizeMin=24;t.fontSizeMax=40;t.alignment=TextAlignmentOptions.Center;}
                    }
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
    }
}
