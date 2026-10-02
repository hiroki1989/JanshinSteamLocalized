using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class OtherButtonTextLayout
{
    public static void Apply()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/OtherScene.unity");
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var guide=all.First(t=>t.name=="GuideRoot");
        var grid=guide.Find("GuideButtonGrid") as RectTransform;
        if(!grid){grid=new GameObject("GuideButtonGrid",typeof(RectTransform)).GetComponent<RectTransform>();grid.SetParent(guide,false);}
        grid.anchorMin=Vector2.zero;grid.anchorMax=Vector2.one;grid.offsetMin=grid.offsetMax=Vector2.zero;
        foreach(var button in guide.GetComponentsInChildren<Button>(true).Where(b=>System.Text.RegularExpressions.Regex.IsMatch(b.name,@"^\d+Button$")).ToArray()){
            int number=int.Parse(button.name.Replace("Button",""))-1;
            var r=(RectTransform)button.transform;r.SetParent(grid,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.localScale=Vector3.one;r.sizeDelta=new Vector2(520,90);r.anchoredPosition=new Vector2(number<5?-310:310,240-(number%5)*120);
        }
        int count=0;
        foreach(var button in all.Select(t=>t.GetComponent<Button>()).Where(b=>b)){
            var labels=button.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.GetComponentInParent<Button>(true)==button).ToArray();
            foreach(var text in labels){
                if(labels.Length==1){
                    text.rectTransform.SetParent(button.transform,false);
                    var r=text.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=Vector2.one*.5f;r.localScale=Vector3.one;r.offsetMin=new Vector2(22,12);r.offsetMax=new Vector2(-22,-12);
                    var fit=text.GetComponent<ContentSizeFitter>();if(fit)fit.enabled=false;
                    var layout=text.GetComponent<LayoutElement>()??text.gameObject.AddComponent<LayoutElement>();layout.ignoreLayout=true;
                    text.alignment=TextAlignmentOptions.Center;
                }
                text.enableAutoSizing=true;text.fontSizeMax=42;text.fontSizeMin=20;
                text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Truncate;text.margin=Vector4.zero;
                count++;
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Logs/UnifiedUIPreviews");
        File.WriteAllText("Logs/OtherButtonTextLayout.txt","Fitted button labels: "+count+"\n");
        foreach(var name in new[]{"HomeRoot","GuideRoot","OptionRoot"}){
            foreach(var root in all.Where(t=>new[]{"HomeRoot","GuideRoot","OptionRoot","AchievementRoot"}.Contains(t.name)))root.gameObject.SetActive(root.name==name);
            foreach(var text in all.Select(t=>t.GetComponent<TMP_Text>()).Where(t=>t&&t.gameObject.activeInHierarchy))text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();BattleHUDQA.Capture("Logs/UnifiedUIPreviews/OtherScene_"+name+".png",1920,1080);
        }
    }
}
