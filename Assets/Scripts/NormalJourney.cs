using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// A scene-independent journey screen. Progression remains owned by ProgressionFlowController.
public sealed class NormalJourney : MonoBehaviour
{
    public enum Leg { Angel, FirstGod, Shop, NextGod }
    public static bool IsPlaying { get; private set; }
    static NormalJourney activeJourney;
    public static void Play(Leg leg, Action arrive) {
        if(IsPlaying)return;
        IsPlaying=true;
        var go=new GameObject("NormalJourney");DontDestroyOnLoad(go);
        activeJourney=go.AddComponent<NormalJourney>();activeJourney.StartCoroutine(activeJourney.Travel(leg,arrive));
    }
    void OnDestroy(){if(activeJourney==this){IsPlaying=false;activeJourney=null;}}
    static string Text(string ja,string en,string zh)=>ConsumableWindow.T(ja,en,zh);
    static Sprite Art(string name)=>Resources.Load<Sprite>("EnemyCutins/"+name);
    IEnumerator Travel(Leg leg,Action arrive){
        Time.timeScale=1;
        var root=SeventeenStepsUI.Rect("PilgrimageCanvas",transform,Vector2.zero,new Vector2(1920,1080));
        var canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32765;
        var scaler=root.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        root.gameObject.AddComponent<GraphicRaycaster>();
        var curtain=SeventeenStepsUI.Rect("Blocker",root,Vector2.zero,new Vector2(10000,10000)).gameObject.AddComponent<Image>();curtain.color=Color.black;curtain.raycastTarget=true;
        var background=SeventeenStepsUI.Picture(root,Resources.Load<Sprite>("Journey/PilgrimageMap"),Vector2.zero,new Vector2(1920,1080));background.preserveAspect=false;
        var shade=SeventeenStepsUI.Rect("Shade",root,Vector2.zero,new Vector2(1920,1080)).gameObject.AddComponent<Image>();shade.color=new Color(0,0,0,.22f);shade.raycastTarget=false;
        var title=SeventeenStepsUI.Label(root,Text("神々への道","Pilgrimage of the Gods","众神之路"),new Vector2(0,438),new Vector2(1200,90),55);SeventeenStepsUI.BlackOutline(title);
        int enemy=Mathf.Max(0,ProgressionFlowController.CurrentEnemyIndex);
        int destination=leg==Leg.Angel?0:leg==Leg.Shop?enemy*2+2:enemy*2+1;
        string[] names=ProgressionFlowController.Instance?ProgressionFlowController.Instance.GetJourneyEnemyNames():EnemyConfigExcel.LoadAll().Where(p=>p.Key>=0&&p.Key<10).OrderBy(p=>p.Key).Select(p=>p.Value.name).ToArray();
        var viewport=SeventeenStepsUI.Rect("RouteViewport",root,new Vector2(0,-50),new Vector2(1700,690));viewport.gameObject.AddComponent<RectMask2D>();
        var route=SeventeenStepsUI.Rect("Route",viewport,Vector2.zero,new Vector2(10000,690));
        int total=1+names.Length*2; // The final shop node is not shown or entered.
        for(int i=Mathf.Max(0,destination-3);i<Mathf.Min(total-1,destination+4);i++){
            Vector2 pos=new Vector2((i-destination)*470+235,Mathf.Sin(i*.9f)*65-130);
            bool angel=i==0,shop=i>0&&i%2==0;
            string label=angel?Text("天使","Angel","天使"):shop?Text("ショップ","Shop","商店"):names[(i-1)/2];
            if(!angel&&!shop&&EnemyConfigExcel.TryGetByName(label,out var enemyConfig))label=enemyConfig.GetLocalizedDisplayName();
            var node=SeventeenStepsUI.Rect("RouteNode"+i,route,pos,new Vector2(60,12));node.gameObject.AddComponent<Image>().color=new Color(.86f,.7f,.4f);
            Sprite sprite=angel?Art("天使"):shop?Resources.Load<Sprite>("Consumables/CurrencyBag"):Art(names[(i-1)/2]);
            var marker=SeventeenStepsUI.Picture(route,sprite,pos+new Vector2(0,115),shop?new Vector2(72,72):new Vector2(175,215));marker.color=!sprite?Color.clear:i>destination?new Color(.7f,.7f,.7f,.65f):Color.white;
            SeventeenStepsUI.InfoBacking(route,pos+new Vector2(0,-50),new Vector2(330,58));
            var text=SeventeenStepsUI.Label(route,label,pos+new Vector2(0,-50),new Vector2(310,52),31);SeventeenStepsUI.BlackOutline(text);
            if(i<total-2){Vector2 next=new Vector2(pos.x+470,Mathf.Sin((i+1)*.9f)*65-130);for(int j=1;j<24;j++){var dot=SeventeenStepsUI.Rect("Path",route,Vector2.Lerp(pos,next,j/24f),new Vector2(10,3));dot.gameObject.AddComponent<Image>().color=new Color(.9f,.74f,.44f,.8f);}}
        }
        string skill=PlayerPrefs.GetString("EquippedActiveSkill","RandomMan");
        string player=skill=="Capitalist"?"Capitalist":skill=="RandomHonor"||skill=="EnhanceHand"?"RandomHonor":"RandomMan";
        var walker=SeventeenStepsUI.Picture(route,Resources.Load<Sprite>("PlayerCutins/"+player+"_victory"),Vector2.zero,new Vector2(150,225));
        var caption=SeventeenStepsUI.Label(root,leg==Leg.Angel?Text("天使のもとへ","To the angel","前往天使处"):leg==Leg.Shop?Text("ショップへ","To the shop","前往商店"):Text("次の神のもとへ","To the next god","前往下一位神明处"),new Vector2(0,-442),new Vector2(1200,65),35);
        Vector2 from=new Vector2(-235,Mathf.Sin((destination-1)*.9f)*65-130),to=new Vector2(150,Mathf.Sin(destination*.9f)*65-130);
        float elapsed=0;
        while(elapsed<6.8f){elapsed+=Time.unscaledDeltaTime;float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-.6f)/5.2f));walker.rectTransform.anchoredPosition=Vector2.Lerp(from,to,t)+new Vector2(0,112+(t>0&&t<1?Mathf.Sin(elapsed*5)*1.5f:0));yield return null;}
        // Keep the opaque screen until the synchronous scene load has completed.
        try {IsPlaying=false;arrive?.Invoke();} finally {Destroy(gameObject);}
    }
}
