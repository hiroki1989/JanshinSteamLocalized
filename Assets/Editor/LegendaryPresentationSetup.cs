using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class LegendaryPresentationSetup
{
    public static void Run()
    {
        Directory.CreateDirectory("Assets/Resources/UnifiedUI");
        var go=new GameObject("LegendaryAcquisition",typeof(RectTransform),typeof(CanvasRenderer),typeof(LegendaryAcquisition));
        go.layer=5;
        var fx=go.GetComponent<LegendaryAcquisition>();fx.raycastTarget=false;fx.maskable=false;
        fx.rectTransform.sizeDelta=Vector2.one*700;
        PrefabUtility.SaveAsPrefabAsset(go,"Assets/Resources/UnifiedUI/LegendaryAcquisition.prefab");
        UnityEngine.Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        Verify();
    }
    public static void Verify()
    {
        Directory.CreateDirectory("Logs/LegendaryPresentation");
        var prefab=Resources.Load<LegendaryAcquisition>("UnifiedUI/LegendaryAcquisition");
        if(!prefab||prefab.raycastTarget||prefab.duration<2)throw new Exception("Legendary prefab configuration invalid");
        if(!LegendaryAcquisition.IsLegendary("Legendary")||LegendaryAcquisition.IsLegendary("Epic"))throw new Exception("Rarity gate invalid");
        ScoringPassivePresentationQA.Run();
        var label=UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None).First(t=>t.gameObject.name=="Yaku");
        label.text="大三元　字一色\n立直　ドラ";
        YakumanTextPresentation.Apply(label);label.ForceMeshUpdate();
        if(label.textInfo.linkCount!=2)throw new Exception("Only actual yakuman should be decorated");
        string wrapped=label.text;YakumanTextPresentation.Apply(label);
        if(wrapped!=label.text)throw new Exception("Yakuman wrapping must be idempotent");
        var pulse=label.GetComponent<ScoringPassivePulse>();
        label.ForceMeshUpdate();
        var appeared=(System.Collections.Generic.Dictionary<string,float>)typeof(ScoringPassivePulse).GetField("appeared",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(pulse);
        foreach(var key in appeared.Keys.ToArray())appeared[key]=Time.unscaledTime-.35f;
        label.ForceMeshUpdate();
        BattleHUDQA.Capture("Logs/LegendaryPresentation/Yakuman.png",1920,1080);
        label.gameObject.SetActive(false);
        var canvas=label.GetComponentInParent<Canvas>();
        var backdrop=new GameObject("Backdrop",typeof(RectTransform),typeof(Image));backdrop.transform.SetParent(canvas.transform,false);
        var rt=backdrop.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
        backdrop.GetComponent<Image>().color=new Color(.025f,.035f,.04f,1);
        var item=new GameObject("LegendaryItem",typeof(RectTransform),typeof(Image));item.transform.SetParent(canvas.transform,false);
        item.GetComponent<RectTransform>().sizeDelta=new Vector2(190,230);
        item.GetComponent<Image>().sprite=ItemArtwork.Load("Body/omamori_05_legendary");item.GetComponent<Image>().preserveAspect=true;
        prefab=Resources.Load<LegendaryAcquisition>("UnifiedUI/LegendaryAcquisition");
        var fx=UnityEngine.Object.Instantiate(prefab,canvas.transform,false);fx.rectTransform.sizeDelta=Vector2.one*700;
        foreach(float time in new[]{.25f,.8f,1.6f}){
            fx.previewTime=time;fx.SetVerticesDirty();Canvas.ForceUpdateCanvases();
            BattleHUDQA.Capture("Logs/LegendaryPresentation/Legendary-"+time.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+".png",1920,1080);
        }
        File.WriteAllText("Logs/LegendaryPresentation/Verified.txt","PASS: prefab; rarity gate; existing passive effects; yakuman isolation and idempotence; preview renders. No scene saved.");
        Debug.Log("LegendaryPresentation verification complete");
    }
}
