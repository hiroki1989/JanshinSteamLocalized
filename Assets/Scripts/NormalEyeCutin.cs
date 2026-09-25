using UnityEngine;
using UnityEngine.UI;

// A UI frame rather than a resized illustration: the eye strip stays inside its jagged aperture.
public sealed class NormalEyeCutin : MonoBehaviour
{
    Image source, eyes;
    RectTransform frame;
    void Awake(){source=GetComponent<Image>();}
    void LateUpdate()
    {
        if(!source)source=GetComponent<Image>();
        var sprite=source.sprite;
        bool strip=sprite&&sprite.rect.width/sprite.rect.height>3;
        if(!strip){if(frame)frame.gameObject.SetActive(false);source.canvasRenderer.SetAlpha(1);return;}
        if(!frame)
        {
            frame=new GameObject("JaggedEyeFrame",typeof(RectTransform)).GetComponent<RectTransform>();frame.SetParent(transform,false);
            var edge=Graphic(frame,"CrimsonSpikes",new Color(.83f,.015f,.025f),true);
            var opening=Graphic(frame,"EyeAperture",new Color(.93f,.87f,.68f),false);
            opening.gameObject.AddComponent<Mask>().showMaskGraphic=true;
            eyes=new GameObject("Eyes",typeof(RectTransform),typeof(Image)).GetComponent<Image>();eyes.transform.SetParent(opening.transform,false);
            eyes.raycastTarget=false;eyes.preserveAspect=true;
            eyes.rectTransform.anchorMin=new Vector2(.24f,.25f);eyes.rectTransform.anchorMax=new Vector2(.76f,.75f);eyes.rectTransform.sizeDelta=Vector2.zero;
        }
        frame.gameObject.SetActive(true);
        float width=Mathf.Min(((RectTransform)transform).rect.width,1300);
        frame.sizeDelta=new Vector2(width,width*.27f);
        eyes.sprite=sprite;source.canvasRenderer.SetAlpha(0);
    }
    static JaggedCutinGraphic Graphic(RectTransform parent,string name,Color color,bool spikes)
    {
        var g=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(JaggedCutinGraphic)).GetComponent<JaggedCutinGraphic>();
        g.transform.SetParent(parent,false);g.rectTransform.anchorMin=Vector2.zero;g.rectTransform.anchorMax=Vector2.one;g.rectTransform.sizeDelta=Vector2.zero;
        g.color=color;g.spikes=spikes;g.raycastTarget=false;return g;
    }
}
