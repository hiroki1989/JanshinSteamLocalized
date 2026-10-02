using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ScoringPassivePresentationQA
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/RunScene.unity");
        var gm=UnityEngine.Object.FindAnyObjectByType<GameManager>();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var field=typeof(GameManager).GetField("scoringRoleValue",flags);
        var original=(TextMeshProUGUI)field.GetValue(gm);
        var font=original.font;
        foreach(var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(c.isRootCanvas)c.gameObject.SetActive(false);
        var root=new GameObject("PassivePreview",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
        var go=new GameObject("Yaku",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(root.transform,false);
        var label=go.GetComponent<TextMeshProUGUI>();label.font=font;label.fontSize=56;label.color=JanshinPanelTheme.Ivory;
        label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(1650,400);label.richText=true;
        field.SetValue(gm,label);
        var yaku=new List<string>{"清一色","平和","一気通貫"};
        typeof(GameManager).GetField("_lastScoringYaku",flags).SetValue(gm,yaku);
        var record=typeof(GameManager).GetMethod("RecordScoringPassive",flags);
        record.Invoke(gm,new object[]{"清一色",SkillSetAsset.Trait.Geki,.2f});
        record.Invoke(gm,new object[]{"平和",SkillSetAsset.Trait.Iyu,.4f});
        record.Invoke(gm,new object[]{"一気通貫",SkillSetAsset.Trait.Shun,.4f});
        var decorate=typeof(GameManager).GetMethod("DecorateScoringPassives",flags);
        string roles=string.Join("　",yaku);
        label.text=roles;decorate.Invoke(gm,new object[]{roles,true,.2f,.4f,.4f});
        var pulse=label.GetComponent<ScoringPassivePulse>();
        typeof(ScoringPassivePulse).GetMethod("OnDisable",flags).Invoke(pulse,null);
        typeof(ScoringPassivePulse).GetMethod("OnEnable",flags).Invoke(pulse,null);
        var began=typeof(ScoringPassivePulse).GetField("began",flags);
        began.SetValue(pulse,Time.unscaledTime-.30f);label.ForceMeshUpdate();
        if(label.textInfo.linkCount!=3)throw new Exception("Not all applied yaku received links");
        if(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Count(c=>c.elementType==TMP_TextElementType.Sprite)!=3)throw new Exception("Passive icons missing");
        BattleHUDQA.Capture("Logs/AngelSkillPresentation/Passive-Pop.png",1920,1080);
        began.SetValue(pulse,Time.unscaledTime-2f);label.ForceMeshUpdate();
        foreach(var c in label.textInfo.characterInfo.Take(label.textInfo.characterCount).Where(c=>c.elementType==TMP_TextElementType.Sprite))
            if(label.textInfo.meshInfo[c.materialReferenceIndex].colors32[c.vertexIndex].a>2)throw new Exception("Passive icon failed to fade");
        BattleHUDQA.Capture("Logs/AngelSkillPresentation/Passive-Faded.png",1920,1080);
        label.text=roles;decorate.Invoke(gm,new object[]{roles,true,0f,0f,0f});label.ForceMeshUpdate();
        if(label.text.Contains("passive:"))throw new Exception("Inactive passive animated");
        label.text=roles;decorate.Invoke(gm,new object[]{roles,false,.2f,.4f,.4f});
        if(label.text.Contains("passive:"))throw new Exception("Enemy score animated");
        File.WriteAllText("Logs/AngelSkillPresentation/PassiveQA.txt","PASS: three applied traits use three sprite icons; icons fade; zero-effect and enemy scores are excluded. Preview-only scene was not saved.");
    }
}
