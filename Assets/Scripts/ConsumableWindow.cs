using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Modal inventory/store with isolated input and unscaled close handling.</summary>
public sealed class ConsumableWindow : MonoBehaviour
{
    public RectTransform Card, Choices;
    public TMP_Text Detail, Status, Summary;
    public Button Confirm;
    public bool StoreStyle;
    public TMP_Text GoldAmount;
    public Action Closed;
    readonly List<BaseRaycaster> blocked = new List<BaseRaycaster>();
    float oldTime;
    GameObject oldSelection;
    bool restored;
    public static string T(string ja,string en,string zh) => MonetizationText.Get(ja,en,zh);
    public static ConsumableWindow Open(Transform owner,string title,Sprite frame=null)
    {
        var go=new GameObject("ConsumableWindow",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(ConsumableWindow));
        go.transform.SetParent(owner,false);
        var w=go.GetComponent<ConsumableWindow>();
        var c=go.GetComponent<Canvas>(); c.renderMode=RenderMode.ScreenSpaceOverlay; c.sortingOrder=32600;
        var scaler=go.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        var dim=Rect("Dim",go.transform,Vector2.zero,new Vector2(10000,10000)).gameObject.AddComponent<Image>(); dim.color=new Color(0,0,0,.8f);
        w.Card=Rect("ItemPurchasePanel",go.transform,Vector2.zero,new Vector2(1660,960));
        var bg=w.Card.gameObject.AddComponent<Image>(); bg.color=new Color(.96f,.95f,.85f);
        var border=Rect("OrnateFrame",w.Card,Vector2.zero,w.Card.sizeDelta).gameObject.AddComponent<Image>();
        border.sprite=Resources.Load<Sprite>("Consumables/PanelFrame");border.type=Image.Type.Sliced;border.raycastTarget=false;
        Label("Title",w.Card,title,new Vector2(0,410),new Vector2(1450,70),40);
        w.Summary=Label("Summary",w.Card,"",new Vector2(0,345),new Vector2(1470,50),23);
        w.Detail=Label("Description",w.Card,"",new Vector2(0,-15),new Vector2(1480,105),28);
        w.Detail.alignment=TextAlignmentOptions.TopLeft;
        w.Choices=Rect("TargetChoices",w.Card,new Vector2(0,-185),new Vector2(1500,205));
        w.Status=Label("Status",w.Card,"",new Vector2(0,-325),new Vector2(1470,44),23);
        w.Confirm=Button("Confirm",w.Card,T("使用する","Use","使用"),new Vector2(280,-380),new Vector2(480,64),null);
        w.Confirm.interactable=false;
        Button("Close",w.Card,T("戻る","Back","返回"),new Vector2(-310,-380),new Vector2(360,64),w.Close);
        w.oldTime=Time.timeScale; Time.timeScale=0;
        w.oldSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
        foreach(var r in FindObjectsByType<BaseRaycaster>(FindObjectsSortMode.None))
            if(r.enabled && !r.transform.IsChildOf(go.transform)){w.blocked.Add(r);r.enabled=false;}
        if(EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        return w;
    }
    public void Items(IList<int> ids, Action<int> select, bool prices, ISet<int> sold=null,int selected=-1)
    {
        var existing=Card.Find("Items"); if(existing){existing.gameObject.SetActive(false); Dispose(existing.gameObject);}
        var row=Rect("Items",Card,new Vector2(0,184),new Vector2(1480,270));
        int count=Mathf.Max(1,ids.Count);
        float width=Mathf.Min(350,1460f/count-16);
        for(int i=0;i<ids.Count;i++)
        {
            int slot=i; var d=RunConsumables.Get(ids[i]); if(d==null) continue;
            var b=Button("Item"+i,row,"",new Vector2((i-(count-1)*.5f)*(width+16),0),new Vector2(width,270),()=>select(slot));
            b.GetComponent<Image>().color=new Color(.95f,.95f,.89f);
            if(i==selected){var outline=b.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.68f,.46f,.13f);outline.effectDistance=new Vector2(3,-3);}
            var icon=Rect("Icon",b.transform,new Vector2(0,38),new Vector2(174,174)).gameObject.AddComponent<Image>(); icon.sprite=d.Icon; icon.preserveAspect=true; icon.raycastTarget=false;
            var label=b.GetComponentInChildren<TMP_Text>(); label.rectTransform.anchoredPosition=new Vector2(0,-87); label.rectTransform.sizeDelta=new Vector2(width-20,80); label.fontSize=24; label.fontSizeMin=18;
            label.text=d.Name+(prices&&!StoreStyle?"\n"+d.price+" Gold":"");
            if(StoreStyle){
                label.rectTransform.anchoredPosition=new Vector2(0,-62);label.rectTransform.sizeDelta=new Vector2(width-20,44);
                var frame=Rect("Frame",b.transform,Vector2.zero,new Vector2(width,270)).gameObject.AddComponent<Image>();frame.sprite=Resources.Load<Sprite>("Consumables/PanelFrame");frame.type=Image.Type.Sliced;frame.pixelsPerUnitMultiplier=8;frame.raycastTarget=false;frame.transform.SetAsFirstSibling();
                Currency(b.transform,new Vector2(0,-108),d.price.ToString(),32);
            }
            if(sold!=null && sold.Contains(i)){b.interactable=false;label.text+="\n"+T("購入済み","Sold","已售出");}
        }
    }
    public void ConfigureStore(Sprite brush, Sprite background, Action next)
    {
        StoreStyle=true;
        var dim=transform.Find("Dim").GetComponent<Image>();
        dim.color=Color.white;dim.sprite=background;
        var dr=dim.rectTransform;dr.sizeDelta=new Vector2(1920,1080);
        Card.sizeDelta=new Vector2(1920,1080);Card.GetComponent<Image>().enabled=false;
        Card.Find("OrnateFrame").gameObject.SetActive(false);
        var title=Card.Find("Title").GetComponent<TMP_Text>();title.rectTransform.anchoredPosition=new Vector2(0,385);title.fontSizeMax=80;title.fontSizeMin=56;title.rectTransform.sizeDelta=new Vector2(1100,110);
        Summary.rectTransform.anchoredPosition=new Vector2(0,275);
        var back=Card.Find("Close").GetComponent<Button>();StyleNavigation(back,brush,new Vector2(-740,290));
        var forward=Button("NextEnemy",Card,T("次の敵へ","Next enemy","下一位敌人"),new Vector2(740,290),new Vector2(300,100),()=>{Close();next?.Invoke();});StyleNavigation(forward,brush,new Vector2(740,290));
        // Offers and owned slots retain their layout below the common header.
        Summary.rectTransform.anchoredPosition=new Vector2(0,260);
        Summary.rectTransform.sizeDelta=new Vector2(1180,44);
        GoldAmount=Currency(Card,new Vector2(-740,435),"",60);
        Confirm.GetComponent<Image>().sprite=brush;Confirm.GetComponent<Image>().color=Color.white;
        Confirm.transform.localPosition=new Vector3(0,-440,0);
        Status.rectTransform.anchoredPosition=new Vector2(0,-370);
        Detail.rectTransform.anchoredPosition=new Vector2(0,-95);Detail.rectTransform.sizeDelta=new Vector2(1480,94);
        Choices.anchoredPosition=new Vector2(0,-250);
        WhiteStoreText();
    }
    void StyleNavigation(Button button,Sprite brush,Vector2 position)
    {
        var rt=(RectTransform)button.transform;rt.anchoredPosition=position;rt.sizeDelta=new Vector2(300,100);
        button.GetComponent<Image>().sprite=brush;button.GetComponent<Image>().color=Color.white;
        var text=button.GetComponentInChildren<TMP_Text>();text.rectTransform.sizeDelta=new Vector2(280,70);text.fontSizeMax=44;text.fontSizeMin=28;
    }
    public static TMP_Text Currency(Transform parent,Vector2 position,string amount,float size)
    {
        var root=Rect("Currency",parent,position,new Vector2(200,size+8));
        var icon=Rect("Bag",root,new Vector2(-62,0),new Vector2(size,size)).gameObject.AddComponent<Image>();
        icon.sprite=Resources.Load<Sprite>("Consumables/CurrencyBag");icon.preserveAspect=true;icon.raycastTarget=false;
        return Label("Amount",root,amount,new Vector2(42,0),new Vector2(130,size+8),size*.75f);
    }
    public void WhiteStoreText()
    {
        if(!StoreStyle)return;
        foreach(var text in GetComponentsInChildren<TMP_Text>(true)){
            text.color=Color.white;text.outlineColor=Color.black;text.outlineWidth=.22f;
            var button=text.GetComponentInParent<Button>();
            if(button && button.transform.parent && button.transform.parent.name=="Items"){
                UpgradePanelPresentation.Black40(text);
                var icon=button.transform.Find("Icon")as RectTransform;
                if(icon){icon.anchoredPosition=new Vector2(0,65);icon.sizeDelta=new Vector2(120,120);}
                if(text.name=="Label"){
                    text.rectTransform.anchoredPosition=new Vector2(0,-40);
                    text.rectTransform.sizeDelta=new Vector2(text.rectTransform.sizeDelta.x,86);
                }
            }
        }
        var row=Card.Find("Items");if(row)row.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,100);
    }
    public void ClearChoices()
    {
        for(int i=Choices.childCount-1;i>=0;i--){var g=Choices.GetChild(i).gameObject;g.SetActive(false);Dispose(g);}
    }
    public static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(parent,false);
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=position;rt.sizeDelta=size;return rt;
    }
    public static TMP_Text Label(string name,Transform parent,string value,Vector2 position,Vector2 size,float fontSize)
    {
        var rt=Rect(name,parent,position,size);var t=rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font=TMP_Settings.defaultFontAsset;t.fontSharedMaterial=t.font?t.font.material:null;t.text=value;
        t.fontSize=fontSize;t.enableAutoSizing=true;t.fontSizeMin=fontSize*.8f;t.fontSizeMax=fontSize;
        t.alignment=TextAlignmentOptions.Center;t.color=new Color(.08f,.1f,.12f);t.raycastTarget=false;
        return t;
    }
    public static Button Button(string name,Transform parent,string title,Vector2 position,Vector2 size,Action click)
    {
        var rt=Rect(name,parent,position,size);var image=rt.gameObject.AddComponent<Image>();image.color=new Color(.72f,.76f,.72f);
        var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=image;
        Label("Label",rt,title,Vector2.zero,size-new Vector2(16,8),28);
        b.onClick.AddListener(()=>{if(Application.isPlaying&&AudioManager.Instance)AudioManager.Instance.PlayClickSE();});
        if(click!=null)b.onClick.AddListener(()=>click());return b;
    }
    static void Dispose(GameObject go){if(Application.isPlaying)Destroy(go);else DestroyImmediate(go);}
    public void Close(){Restore();Dispose(gameObject);}
    void OnDisable(){Restore();}
    void Update(){if(UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame==true)Close();}
    void Restore()
    {
        if(restored)return;restored=true;Time.timeScale=oldTime;
        foreach(var r in blocked)if(r)r.enabled=true;blocked.Clear();
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(oldSelection && oldSelection.activeInHierarchy?oldSelection:null);
        Closed?.Invoke();Closed=null;
    }
}
