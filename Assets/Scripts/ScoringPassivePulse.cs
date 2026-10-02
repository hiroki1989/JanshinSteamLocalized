using TMPro;
using UnityEngine;
using System.Collections.Generic;

// Decorates only linked yaku glyphs. The authored layout and scoring values stay intact.
[DisallowMultipleComponent]
public sealed class ScoringPassivePulse : MonoBehaviour
{
    TMP_Text label;
    float began;
    bool armed;
    bool rainbow;
    readonly Dictionary<string,float> appeared=new Dictionary<string,float>();
    void Awake() { label=GetComponent<TMP_Text>(); }
    void OnEnable()
    {
        if(!label)label=GetComponent<TMP_Text>();
        label.OnPreRenderText+=Animate;
        began=Time.unscaledTime;
        appeared.Clear();
    }
    void OnDisable() { if(label)label.OnPreRenderText-=Animate; }
    public void Restart(bool enabledForScore)
    {
        armed=enabledForScore;
        began=Time.unscaledTime;
        appeared.Clear();rainbow=false;
        if(!label)label=GetComponent<TMP_Text>();
        label.SetVerticesDirty();
    }
    void LateUpdate()
    {
        if(armed && (rainbow || Time.unscaledTime-began<4f))label.ForceMeshUpdate();
    }
    void Animate(TMP_TextInfo info)
    {
        if(!armed)return;
        int ordinal=0;
        for(int l=0;l<info.linkCount;l++)
        {
            var link=info.linkInfo[l];
            string id=link.GetLinkID();
            bool yakuman=id.StartsWith("yakuman:");
            if((!id.StartsWith("passive:")&&!yakuman) || id.Length<14 || !ColorUtility.TryParseHtmlString("#"+id.Substring(8,6),out var tint))continue;
            float age=Time.unscaledTime-began-ordinal++*.09f;
            if(yakuman){
                rainbow=true;
                if(!appeared.TryGetValue(id,out var started)){started=Time.unscaledTime;appeared[id]=started;}
                age=Time.unscaledTime-started;
            }
            int first=link.linkTextfirstCharacterIndex,end=first+link.linkTextLength;
            float x0=float.MaxValue,x1=float.MinValue;
            for(int i=first;i<end;i++)
            {
                var c=info.characterInfo[i];if(!c.isVisible)continue;
                x0=Mathf.Min(x0,c.bottomLeft.x);x1=Mathf.Max(x1,c.topRight.x);
            }
            float pulse=age>0 && age<.36f?Mathf.Sin(age/.36f*Mathf.PI):0;
            for(int i=first;i<end;i++)
            {
                var c=info.characterInfo[i];if(!c.isVisible)continue;
                var mesh=info.meshInfo[c.materialReferenceIndex];
                bool icon=c.elementType==TMP_TextElementType.Sprite;
                var center=new Vector3((x0+x1)*.5f,c.baseLine,0);
                float fade=icon?Mathf.Clamp01(age/.12f)*(1-Mathf.Clamp01((age-.32f)/.65f)):1;
                for(int v=0;v<4;v++)
                {
                    int n=c.vertexIndex+v;
                    var p=mesh.vertices[n];
                    float depth=yakuman?.55f*Mathf.Pow(1-Mathf.Clamp01(age/.45f),3):.10f*pulse;
                    p=center+(p-center)*(1+depth);
                    p.y+=c.pointSize*(.14f*pulse+(icon?Mathf.Clamp01(age)*.3f:0));
                    mesh.vertices[n]=p;
                    Color color=mesh.colors32[n];
                    if(!icon){
                        var target=yakuman?Color.HSVToRGB(Mathf.Repeat(Mathf.InverseLerp(x0,x1,p.x)*.8f+Time.unscaledTime*.09f,1),.55f,1):tint;
                        color=Color.Lerp(color,target,Mathf.Clamp01(age/.18f));
                        if(yakuman)color.a*=Mathf.Clamp01(age/.18f);
                    }
                    color.a*=fade;
                    mesh.colors32[n]=color;
                }
            }
        }
    }
}
