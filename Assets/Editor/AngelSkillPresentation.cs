using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class AngelSkillPresentation
{
    static void Rect(Transform t,float x0,float y0,float x1,float y1)
    {
        var r=(RectTransform)t;r.anchorMin=new Vector2(x0,y0);r.anchorMax=new Vector2(x1,y1);
        r.offsetMin=r.offsetMax=Vector2.zero;r.localScale=Vector3.one;r.pivot=Vector2.one*.5f;
    }
    static void Label(Transform t,float x0,float y0,float x1,float y1,float max=36)
    {
        Rect(t,x0,y0,x1,y1);var label=t.GetComponent<TMP_Text>();if(!label)return;
        var fitter=t.GetComponent<ContentSizeFitter>();if(fitter)fitter.enabled=false;
        label.enableAutoSizing=true;label.fontSizeMin=22;label.fontSizeMax=max;
        label.textWrappingMode=TextWrappingModes.Normal;label.overflowMode=TextOverflowModes.Truncate;
        label.margin=new Vector4(6,4,6,4);label.alignment=TextAlignmentOptions.MidlineLeft;
        label.color=JanshinPanelTheme.Ivory;
    }
    static void Backup(string path)
    {
        Directory.CreateDirectory("Logs/AngelSkillPresentation/Backup");
        File.Copy(path,"Logs/AngelSkillPresentation/Backup/"+Path.GetFileNameWithoutExtension(path)+"-"+System.DateTime.Now.ToString("yyyyMMdd-HHmmss")+Path.GetExtension(path));
    }
    public static void Install()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SkillSetScene.unity");Backup(scene.path);
        var canvas=Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(c=>c.isRootCanvas).transform;
        var panel=canvas.Find("SkillsetDescriptionBox");
        Rect(panel,.40f,.10f,.96f,.86f);JanshinPanelTheme.Apply(panel.GetComponent<Image>());
        var old=panel.Find("Panel");if(old)old.GetComponent<Image>().enabled=false;
        foreach(var im in panel.GetComponentsInChildren<Image>(true))if(im.sprite&&AssetDatabase.GetAssetPath(im.sprite).Contains("FudeUI"))im.enabled=false;
        Label(panel.Find("SkillName"),.045f,.87f,.95f,.98f,46);
        Label(panel.Find("Text (TMP)"),.04f,.79f,.95f,.87f,30);
        Label(panel.Find("SkillSetDescription"),.05f,.60f,.95f,.79f,32);
        Label(panel.Find("Text (TMP) (1)"),.04f,.51f,.95f,.59f,30);
        string[] labels={"Geki","Yu","Syun"},icons={"ImageGeki","Image Iyu","ImageSyun"};
        for(int i=0;i<3;i++){
            float y=.345f-i*.16f;
            Label(panel.Find(labels[i]),.13f,y,.96f,y+.15f,32);
            Rect(panel.Find(icons[i]),.04f,y+.035f,.105f,y+.11f);
            panel.Find(icons[i]).GetComponent<Image>().preserveAspect=true;
        }
        var portrait=canvas.Find("Portrait");Rect(portrait,.04f,.225f,.375f,.86f);
        var pi=portrait.GetComponent<Image>();pi.preserveAspect=true;pi.raycastTarget=false;
        pi.color=Color.white;
        Rect(canvas.Find("Skillset"),.04f,.10f,.375f,.205f);
        var selector=canvas.Find("Skillset");
        foreach(var layout in selector.GetComponents<LayoutGroup>())layout.enabled=false;
        string[] choices={"Button (4)","Button (3)","Button (1)"};
        for(int i=0;i<choices.Length;i++){
            var b=selector.Find(choices[i]).GetComponent<Button>();
            Rect(b.transform,i/3f+.01f,0,(i+1)/3f-.01f,1);
            var face=b.GetComponent<Image>()??b.gameObject.AddComponent<Image>();face.enabled=true;b.targetGraphic=face;JanshinPanelTheme.Button(b);
            foreach(var im in b.GetComponentsInChildren<Image>(true))if(im.transform!=b.transform&&im.name!="UnifiedGoldBorder"){
                Rect(im.transform,.23f,.16f,.77f,.84f);im.preserveAspect=true;im.raycastTarget=false;
            }
        }
        var back=canvas.Find("Button_Back").GetComponent<Button>();Rect(back.transform,.035f,.885f,.20f,.97f);JanshinPanelTheme.Button(back);
        foreach(var t in back.GetComponentsInChildren<TMP_Text>(true)){Label(t.transform,.05f,.05f,.95f,.95f,40);t.alignment=TextAlignmentOptions.Center;}
        var unequip=canvas.Find("Button_EquipNone");if(unequip){Rect(unequip,.04f,.015f,.375f,.085f);JanshinPanelTheme.Button(unequip.GetComponent<Button>());}
        Label(canvas.Find("Title"),.28f,.875f,.76f,.985f,80);canvas.Find("Title").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Center;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var controller=Object.FindAnyObjectByType<SkillSetSceneController>();
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        typeof(SkillSetSceneController).GetMethod("LoadSkillSets",flags).Invoke(controller,null);
        var update=typeof(SkillSetSceneController).GetMethod("UpdateSkillUIForSelectedSkill_Local",flags);
        foreach(var id in new[]{"RandomMan","RandomHonor","Capitalist"}){
            update.Invoke(controller,new object[]{id,null});
            Canvas.ForceUpdateCanvases();BattleHUDQA.Capture("Logs/AngelSkillPresentation/Skill-"+id+".png",1920,1080);
        }
        // Preview mutations above are deliberately not saved.
        scene=EditorSceneManager.OpenScene("Assets/Scenes/AngelDialogue.unity");Backup(scene.path);
        var angel=Resources.Load<Sprite>("Sprites/Enemies/Dialogue/天使");
        var ac=Object.FindAnyObjectByType<AngelDialogueController>();var so=new SerializedObject(ac);
        var portraitProp=so.FindProperty("portraitImage");var image=portraitProp.objectReferenceValue as Image;
        image.sprite=angel;image.preserveAspect=true;image.raycastTarget=false;
        Rect(image.transform,.26f,.10f,.74f,.98f);
        // The dialogue stays in front; the full head and halo are inside the canvas.
        var sibling=image.transform.GetSiblingIndex();image.transform.SetSiblingIndex(Mathf.Min(sibling,2));
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Canvas.ForceUpdateCanvases();BattleHUDQA.Capture("Logs/AngelSkillPresentation/Angel.png",1920,1080);
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/AngelSkillPresentation/Installed.txt","SkillSetScene + AngelDialogue saved; edition-specific controllers and events preserved.");
    }
    public static void Finish()
    {
        Install();
        ScoringPassivePresentationQA.Run();
    }
    public static void Inspect()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SkillSetScene.unity");
        Directory.CreateDirectory("Logs/AngelSkillPresentation");
        Canvas.ForceUpdateCanvases();
        var all=Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        File.WriteAllLines("Logs/AngelSkillPresentation/hierarchy.txt",all.Select(t=> {
            string path=t.name;for(var p=t.parent;p!=null;p=p.parent)path=p.name+"/"+path;
            var im=t.GetComponent<Image>();var txt=t.GetComponent<TMP_Text>();var b=t.GetComponent<Button>();
            return path+" active="+t.gameObject.activeSelf+" pos="+t.anchoredPosition+" size="+t.rect.size+" scale="+t.localScale+" sprite="+(im&&im.sprite?AssetDatabase.GetAssetPath(im.sprite):"")+" text="+(txt?txt.text:"")+" button="+(bool)b;
        }));
        BattleHUDQA.Capture("Logs/AngelSkillPresentation/SkillBefore.png",1920,1080);
    }
}
