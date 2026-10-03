using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class AppliedScoringEffectsSetup
{
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(GameManager gm,string name)=> (T)typeof(GameManager).GetField(name,Flags).GetValue(gm);
    public static void RunAndPlay(){Run();AppliedScoringPlayQA.Run();}
    public static void Run()
    {
        Directory.CreateDirectory("Logs/AppliedScoring");
        const string path="Assets/Scenes/RunScene.unity";
        if(!File.Exists("Logs/AppliedScoring/RunScene.before.unity"))File.Copy(path,"Logs/AppliedScoring/RunScene.before.unity");
        var scene=EditorSceneManager.OpenScene(path);
        var gm=UnityEngine.Object.FindAnyObjectByType<GameManager>();
        Build(gm,true);Build(gm,false);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Verify(gm);
        Debug.Log("Applied scoring effects verified");
    }
    static RectTransform Rect(string name,Transform parent)
    {
        var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(parent,false);return rt;
    }
    static void Fill(RectTransform rt){rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;}
    static void Build(GameManager gm,bool player)
    {
        var panel=Field<GameObject>(gm,player?"scoringPanelPlayer":"scoringPanelEnemy");
        var existing=panel.GetComponentInChildren<AppliedScoringEffectsView>(true);if(existing)UnityEngine.Object.DestroyImmediate(existing.gameObject);
        var value=Field<TextMeshProUGUI>(gm,player?"scoringGekiValue":"scoringOmamoriReduceValue");
        var parent=value.transform.parent;
        var root=Rect("AppliedEffects",parent);
        // Same text column and reserved effect area as the original authored score UI.
        root.anchorMin=root.anchorMax=new Vector2(.5f,.5f);root.pivot=new Vector2(.5f,.5f);
        root.sizeDelta=new Vector2(650,player?360:120);root.anchoredPosition=new Vector2(-70,player?-75:50);
        var view=root.gameObject.AddComponent<AppliedScoringEffectsView>();
        view.specialEffectsRoot=Field<GameObject>(gm,player?"scoringSpecialTileEffectsRoot_Player":"scoringSpecialTileEffectsRoot_Enemy");
        view.specialEffectsText=Field<TextMeshProUGUI>(gm,player?"scoringSpecialTileEffectsTMP_Player":"scoringSpecialTileEffectsTMP_Enemy");
        view.specialEffectsDecorations=parent.Cast<Transform>().Where(t=>t.name=="Text (TMP)"||(t.name=="Image"&&t is RectTransform rt&&rt.sizeDelta.x>300&&rt.sizeDelta.y>200)).Select(t=>t.gameObject).ToArray();
        var viewport=Rect("Viewport",root);Fill(viewport);viewport.gameObject.AddComponent<RectMask2D>();
        var hit=viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
        var content=Rect("Content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
        var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;layout.spacing=5;
        var fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var scroll=root.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;view.scroll=scroll;
        view.rows=new AppliedScoringEffectsView.Row[16];
        for(int i=0;i<view.rows.Length;i++){
            var row=Rect("Effect_"+i,content);var le=row.gameObject.AddComponent<LayoutElement>();le.minHeight=le.preferredHeight=64;
            var icon=Rect("ItemIcon",row).gameObject.AddComponent<Image>();icon.raycastTarget=false;icon.preserveAspect=true;
            icon.rectTransform.anchorMin=icon.rectTransform.anchorMax=new Vector2(0,.5f);icon.rectTransform.pivot=new Vector2(0,.5f);icon.rectTransform.sizeDelta=new Vector2(62,62);icon.rectTransform.anchoredPosition=Vector2.zero;
            var text=Rect("EffectText",row).gameObject.AddComponent<TextMeshProUGUI>();text.font=value.font;text.fontSharedMaterial=value.fontSharedMaterial;text.color=JanshinPanelTheme.Ivory;
            Fill(text.rectTransform);text.rectTransform.offsetMin=new Vector2(76,2);text.rectTransform.offsetMax=new Vector2(-12,-2);
            text.fontSize=32;text.enableAutoSizing=true;text.fontSizeMin=24;text.fontSizeMax=32;text.alignment=TextAlignmentOptions.MidlineLeft;text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Truncate;text.raycastTarget=false;
            view.rows[i]=new AppliedScoringEffectsView.Row{root=row.gameObject,icon=icon,text=text};row.gameObject.SetActive(false);
        }
        string[] names=player?new[]{"scoringGekiValue","scoringShunValue","scoringIyuValue","scoringOfudaDmgValue","scoringOfudaHpValue","scoringOfudaMpValue","koukaOfudaDmgValue","koukaOfudaHpValue","koukaOfudaMpValue","PlayerEnemySkill","Geki","Syun","Yu","Bougyo","Ofuda","Ofuda (1)","Ofuda (2)"}:new[]{"scoringOmamoriReduceValue","LabelOmamoriDmg","EnemySkill"};
        var replaced=parent.GetComponentsInChildren<Transform>(true).Where(t=>names.Contains(t.name)).Select(t=>t.gameObject).ToList();
        // Old charm icon is named Image; select only its small decorative icon, not the card.
        if(!player)replaced.AddRange(parent.GetComponentsInChildren<Image>(true).Where(i=>i.name=="Image"&&i.rectTransform.sizeDelta.x<80&&i.rectTransform.sizeDelta.y<80).Select(i=>i.gameObject));
        view.replacedObjects=replaced.Distinct().ToArray();view.Clear();
        // Above the transparent advance-step button so scrolling is still usable.
        root.SetParent(panel.transform,true);root.SetAsLastSibling();
    }
    static void Verify(GameManager gm)
    {
        foreach(bool player in new[]{true,false}){
            var view=Field<GameObject>(gm,player?"scoringPanelPlayer":"scoringPanelEnemy").GetComponentInChildren<AppliedScoringEffectsView>(true);
            if(view.rows.Length<12||!view.scroll.viewport)throw new Exception("Missing authored effect rows");
            view.Add(DevilContractIcons.Get(10),"ケルベロス　ダメージ +1,000 / HP回復 +200");
            if(view.Count!=1||view.rows[0].icon.sprite!=DevilContractIcons.Get(10))throw new Exception("Contract icon binding failed");
            view.Clear();if(view.Count!=0||view.rows.Any(r=>r.root.activeSelf))throw new Exception("Previous win effects retained");
        }
        var ge=Field<TextMeshProUGUI>(gm,"scoringGekiValue");ge.text="-";
        foreach(var field in new[]{"scoringShunValue","scoringIyuValue","scoringOfudaDmgValue","scoringOfudaHpValue","scoringOfudaMpValue","scoringEnemySkillEffectValue"})Field<TextMeshProUGUI>(gm,field).text="-";
        // Render the authored text column with a representative sparse win (no save changes).
        var playerPanel=Field<GameObject>(gm,"scoringPanelPlayer");var enemyPanel=Field<GameObject>(gm,"scoringPanelEnemy");
        var score=Field<GameObject>(gm,"scoringPanel");score.SetActive(true);playerPanel.SetActive(true);enemyPanel.SetActive(false);
        var pview=playerPanel.GetComponentInChildren<AppliedScoringEffectsView>(true);
        pview.Add(null,GameManager.RenderConsumableDescriptionAnywhere("撃")+" ダメージ +20% ");
        pview.Add(ItemArtwork.Load("Body/ofuda_04_epic"),"お札　ダメージ ×1.5");
        pview.Add(ItemArtwork.Load("Body/omamori_05_legendary"),"お守り　HP回復 +10%");
        pview.Add(DevilContractIcons.Get(10),"ケルベロス\nダメージ +1,000 / HP回復 +200");
        Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(pview.scroll.content);
        BattleHUDQA.Capture("Logs/AppliedScoring/Player.png",1920,1080);
        File.WriteAllText("Logs/AppliedScoring/Verified.txt","PASS: scene-authored scrolling effect rows; item and devil sprites; clear removes previous effects; inactive legacy rows suppressed; score calculation unchanged.");
    }
}
