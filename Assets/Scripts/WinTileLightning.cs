using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Canvas-space lightning, drawn onto the actual winning tile before any cut-in.</summary>
public sealed class WinTileLightning : MaskableGraphic
{
    readonly List<Vector2> bolt = new();
    readonly List<Vector2[]> branches = new();
    Vector2 impact;
    Rect tileBounds;
    float progress;
    static AudioClip generatedThunder;
    public static IEnumerator Play(RectTransform tile, Transform owner, float duration, AudioClip sound)
    {
        var go = new GameObject("WinningTileLightning",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        go.transform.SetParent(owner,false);
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=30000;
        var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
        var surface = new GameObject("Lightning",typeof(RectTransform),typeof(CanvasRenderer),typeof(WinTileLightning));
        surface.transform.SetParent(go.transform,false);
        var fx=surface.GetComponent<WinTileLightning>();
        ItemArtwork.Rect(fx.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        fx.raycastTarget=true;
        try
        {
            // Settle any layout rebuild / deferred tile replacement before converting coordinates.
            yield return null;
            if (!tile) yield break;
            Canvas.ForceUpdateCanvases();
            fx.Configure(tile);
            bool sounded=false;
            float elapsed=0;
            while (elapsed < duration)
            {
                fx.progress=elapsed/duration;
                if (!sounded && fx.progress >= .2f)
                {
                    sounded=true;
                    if (AudioManager.Instance) AudioManager.Instance.PlaySE(sound ? sound : Thunder(), .8f);
                }
                fx.SetVerticesDirty();
                elapsed+=Time.unscaledDeltaTime;
                yield return null;
            }
        }
        finally { if (go) Destroy(go); }
    }
    public void Configure(RectTransform target)
    {
        var targetCanvas=target.GetComponentInParent<Canvas>();
        Camera cam=targetCanvas && targetCanvas.renderMode!=RenderMode.ScreenSpaceOverlay ? targetCanvas.worldCamera : null;
        var corners=new Vector3[4]; target.GetWorldCorners(corners);
        Vector2 lo=new(float.MaxValue,float.MaxValue), hi=new(float.MinValue,float.MinValue);
        foreach (var corner in corners)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,RectTransformUtility.WorldToScreenPoint(cam,corner),null,out var p);
            lo=Vector2.Min(lo,p); hi=Vector2.Max(hi,p);
        }
        tileBounds=Rect.MinMaxRect(lo.x,lo.y,hi.x,hi.y); impact=tileBounds.center;
        var random=new System.Random(8137);
        float top=rectTransform.rect.yMax;
        bolt.Clear(); branches.Clear();
        for (int i=0;i<=12;i++)
        {
            float t=i/12f;
            float x=impact.x+(i==12 ? 0 : ((float)random.NextDouble()-.5f)*110*(1-t*.6f));
            bolt.Add(new Vector2(x,Mathf.Lerp(top,impact.y,t)));
        }
        for(int i=3;i<11;i+=2)
        {
            float side=i%4==3 ? -1:1;
            branches.Add(new[]{bolt[i],bolt[i]+new Vector2(side*52,-26),bolt[i]+new Vector2(side*35,-56),bolt[i]+new Vector2(side*100,-96)});
        }
        SetVerticesDirty();
    }
    public void Preview(float value) { progress=value; SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (bolt.Count==0) return;
        float fade=1-Mathf.InverseLerp(.65f,1,progress);
        Quad(vh,rectTransform.rect,new Color(.025f,.025f,.065f,.17f*fade));
        float charge=Mathf.Clamp01(progress/.18f);
        Border(vh,new Rect(tileBounds.x-5,tileBounds.y-5,tileBounds.width+10,tileBounds.height+10),3,new Color(1,.86f,.32f,charge*fade));
        if(progress < .14f) return;
        float reach=Mathf.Clamp01((progress-.14f)/.065f);
        float strength=progress<.26f ? 1 : Mathf.Lerp(.8f,0,Mathf.InverseLerp(.26f,.62f,progress));
        for(int i=1;i<bolt.Count;i++)
        {
            if ((i-1)/(float)(bolt.Count-1)>reach) break;
            Vector2 end=Vector2.Lerp(bolt[i-1],bolt[i],Mathf.Clamp01(reach*(bolt.Count-1)-(i-1)));
            Line(vh,bolt[i-1],end,24,new Color(.4f,.55f,1,.16f*strength));
            Line(vh,bolt[i-1],end,10,new Color(.55f,.75f,1,.65f*strength));
            Line(vh,bolt[i-1],end,3.5f,new Color(1,1,.91f,strength));
        }
        if(reach>=1)
        {
            foreach(var branch in branches)
                for(int i=1;i<branch.Length;i++) Line(vh,branch[i-1],branch[i],2,new Color(.75f,.85f,1,strength*.8f));
            float burst=Mathf.InverseLerp(.2f,.75f,progress);
            float alpha=(1-burst)*fade;
            Ring(vh,impact,18+burst*90,7*(1-burst)+1,new Color(1,.85f,.35f,alpha));
            Ring(vh,impact,10+burst*135,2,new Color(.65f,.8f,1,alpha*.65f));
            for(int i=0;i<10;i++)
            {
                float angle=i*Mathf.PI*.2f;
                Vector2 dir=new(Mathf.Cos(angle),Mathf.Sin(angle));
                Line(vh,impact+dir*(15+burst*80),impact+dir*(35+burst*100),3,new Color(1,.95f,.65f,alpha));
            }
            float flash=Mathf.Clamp01(1-(progress-.2f)/.13f);
            Quad(vh,tileBounds,new Color(1,.96f,.76f,flash*.8f));
        }
    }
    static void Quad(VertexHelper vh,Rect r,Color c)
    {
        int n=vh.currentVertCount;
        vh.AddVert(new Vector3(r.xMin,r.yMin),c,Vector2.zero); vh.AddVert(new Vector3(r.xMin,r.yMax),c,Vector2.zero);
        vh.AddVert(new Vector3(r.xMax,r.yMax),c,Vector2.zero); vh.AddVert(new Vector3(r.xMax,r.yMin),c,Vector2.zero);
        vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
    }
    static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color)
    {
        Vector2 normal=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
        int n=vh.currentVertCount;
        vh.AddVert(a-normal,color,Vector2.zero); vh.AddVert(a+normal,color,Vector2.zero);
        vh.AddVert(b+normal,color,Vector2.zero); vh.AddVert(b-normal,color,Vector2.zero);
        vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
    }
    static void Border(VertexHelper vh,Rect r,float w,Color c)
    {
        Line(vh,new(r.xMin,r.yMin),new(r.xMin,r.yMax),w,c); Line(vh,new(r.xMin,r.yMax),new(r.xMax,r.yMax),w,c);
        Line(vh,new(r.xMax,r.yMax),new(r.xMax,r.yMin),w,c); Line(vh,new(r.xMax,r.yMin),new(r.xMin,r.yMin),w,c);
    }
    static void Ring(VertexHelper vh,Vector2 center,float radius,float width,Color color)
    {
        for(int i=0;i<48;i++) { float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;Line(vh,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,color); }
    }
    static AudioClip Thunder()
    {
        if(generatedThunder) return generatedThunder;
        const int rate=44100; var data=new float[(int)(rate*.65f)];var random=new System.Random(7761);float low=0;
        for(int i=0;i<data.Length;i++)
        {
            float t=i/(float)rate,n=(float)random.NextDouble()*2-1;low=Mathf.Lerp(low,n,.075f);
            // A fast broadband crack followed by a short, weighty impact.
            float crack=(n-low)*Mathf.Exp(-t*42)*1.05f;
            float impact=Mathf.Sin(2*Mathf.PI*(125*t-55*t*t))*.48f*Mathf.Exp(-t*17);
            float rumble=low*.65f*Mathf.Exp(-t*10);
            float echo=t>.035f ? n*.18f*Mathf.Exp(-(t-.035f)*55) : 0;
            float sample=(crack+impact+rumble+echo)*Mathf.Min(1,t/.0005f);
            data[i]=.94f*(float)System.Math.Tanh(sample*1.5f)*Mathf.Clamp01((.65f-t)/.025f);
        }
        generatedThunder=AudioClip.Create("WinningTileThunder",data.Length,1,rate,false);generatedThunder.SetData(data,0);return generatedThunder;
    }
}
