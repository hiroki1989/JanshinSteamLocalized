using UnityEngine;
using UnityEngine.UI;

// A lightweight UI mesh: no post processing, particles, input interception or time-scale changes.
public sealed class LegendaryAcquisition : MaskableGraphic
{
    public float duration=2.35f;
    public Color ember=new Color(.78f,.20f,.035f,1);
    public Color gold=new Color(1f,.58f,.18f,1);
    [System.NonSerialized] public float previewTime=-1;
    float age;
    public static bool IsLegendary(string rarity)=>ItemArtwork.RarityKey(rarity)=="05_legendary";
    public static void Play(RectTransform target)
    {
        if(!target)return;
        var canvas=target.GetComponentInParent<Canvas>();if(!canvas)return;canvas=canvas.rootCanvas;
        var prefab=Resources.Load<LegendaryAcquisition>("UnifiedUI/LegendaryAcquisition");
        if(!prefab){Debug.LogWarning("LegendaryAcquisition prefab is missing");return;}
        var fx=Instantiate(prefab,canvas.transform,false);
        fx.raycastTarget=false;fx.maskable=false;fx.transform.SetAsLastSibling();
        var host=(RectTransform)canvas.transform;
        var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        var point=RectTransformUtility.WorldToScreenPoint(camera,target.TransformPoint(target.rect.center));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(host,point,camera,out var local);
        fx.rectTransform.anchorMin=fx.rectTransform.anchorMax=Vector2.one*.5f;
        float size=Mathf.Min(740,host.rect.height*.8f);
        fx.rectTransform.sizeDelta=Vector2.one*size;
        local.x=Mathf.Clamp(local.x,host.rect.xMin+size*.35f,host.rect.xMax-size*.35f);
        local.y=Mathf.Clamp(local.y,host.rect.yMin+size*.35f,host.rect.yMax-size*.35f);
        fx.rectTransform.anchoredPosition=local;
    }
    void Update()
    {
        if(!Application.isPlaying)return;
        age+=Time.unscaledDeltaTime;SetVerticesDirty();
        if(age>=duration)Destroy(gameObject);
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();float t=previewTime>=0?previewTime:age;
        float impact=Mathf.Max(0,t-.12f),fade=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((t-1.15f)/(duration-1.15f)));
        float rise=Mathf.Clamp01(t/.2f),flash=Mathf.Exp(-impact*7)*Mathf.Clamp01(impact*35);
        float unit=rectTransform.rect.width/700f;
        Color E(float a){var c=ember;c.a=a*fade;return c;}
        Color G(float a){var c=gold;c.a=a*fade;return c;}
        void Vertex(Vector2 p,Color c){vh.AddVert(new Vector3(p.x*unit,p.y*unit,0),c,Vector2.zero);}
        void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color ca,Color cb,Color cc,Color cd){int n=vh.currentVertCount;Vertex(a,ca);Vertex(b,cb);Vertex(c,cc);Vertex(d,cd);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
        void Ring(float radius,float width,Color c,float squash=1,float rotation=0,int count=100){
            for(int i=0;i<count;i++){
                float a=rotation+i*Mathf.PI*2/count,b=rotation+(i+1)*Mathf.PI*2/count;
                var va=new Vector2(Mathf.Cos(a),Mathf.Sin(a)*squash);var vb=new Vector2(Mathf.Cos(b),Mathf.Sin(b)*squash);
                Quad(va*(radius-width),vb*(radius-width),vb*(radius+width),va*(radius+width),c,c,c,c);
            }
        }
        // Broad, dark amber glow; its transparent center preserves the item and description.
        for(int i=0;i<80;i++){
            float a=i*Mathf.PI*2/80,b=(i+1)*Mathf.PI*2/80;
            var va=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var vb=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
            Quad(va*55,vb*55,vb*310,va*310,E(.20f*rise),E(.20f*rise),E(0),E(0));
        }
        // Three tapered shafts converge into a molten, white-gold core at impact.
        for(int k=-1;k<=1;k++){
            float x=k*48,w=k==0?32:14,h=(290-Mathf.Abs(k)*65)*rise;
            Quad(new Vector2(x-w,-70),new Vector2(x+w,-70),new Vector2(x+w*.3f,h),new Vector2(x-w*.3f,h),E(.3f*rise),E(.3f*rise),G(0),G(0));
            Quad(new Vector2(x-3,-35),new Vector2(x+3,-35),new Vector2(x+1,h),new Vector2(x-1,h),G(.7f*flash),G(.7f*flash),G(0),G(0));
        }
        float radius=75+220*(1-Mathf.Exp(-impact*3.5f));
        Ring(radius,1.5f,G(.8f*Mathf.Exp(-impact*1.8f)),.58f);
        Ring(radius+8,.55f,E(.6f*Mathf.Exp(-impact*1.6f)),.58f);
        // A restrained, slowly rotating seal gives the reveal weight rather than confetti.
        Ring(155,1.1f,E(.58f*rise));Ring(162,.6f,G(.46f*rise));
        for(int i=0;i<12;i++){
            float angle=i*Mathf.PI/6+t*.075f;
            var n=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));var p=new Vector2(-n.y,n.x);
            Quad(n*150-p*2,n*169-p*2,n*169+p*2,n*150+p*2,G(.48f*rise),G(.48f*rise),G(.48f*rise),G(.48f*rise));
        }
        for(int i=0;i<26;i++){
            float seed=Mathf.Repeat(i*.618034f,1),delay=seed*.25f;
            float life=Mathf.Max(0,impact-delay);if(life<=0)continue;
            float x=Mathf.Sin(i*17.1f)*(80+seed*160),y=-90+life*(55+seed*75);
            float alpha=Mathf.Clamp01(life*8)*(1-Mathf.Clamp01(life/1.9f));float w=.9f+seed*1.6f;
            Quad(new Vector2(x-w,y-4),new Vector2(x+w,y-4),new Vector2(x+w*.4f,y+7),new Vector2(x-w*.4f,y+7),E(0),E(0),G(alpha*.8f),G(alpha*.8f));
        }
        // A single horizontal glint marks impact, without flashing the entire screen.
        Quad(new Vector2(-260,-2),new Vector2(0,-3),new Vector2(0,3),new Vector2(-260,2),G(0),G(flash),G(flash),G(0));
        Quad(new Vector2(0,-3),new Vector2(260,-2),new Vector2(260,2),new Vector2(0,3),G(flash),G(0),G(0),G(flash));
    }
}
