using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SeventeenStepsInventory : MonoBehaviour
{
    RectTransform root,rows,equipment;TextMeshProUGUI detail;Button equipButton,removeButton;int selected;
    public static void AddMenuButton(Transform owner){
        var canvas=FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.gameObject.scene.name=="MenuScene"&&c.isRootCanvas&&c.isActiveAndEnabled).OrderByDescending(c=>c.sortingOrder).FirstOrDefault();
        if(!canvas||canvas.transform.Find("ConsumableInventoryButton"))return;
        var source=canvas.GetComponentsInChildren<Button>(true).FirstOrDefault(x=>x.name=="Button_Equip");if(!source)return;
        var b=SeventeenStepsUI.Navigation(canvas.transform,"遺物",Vector2.zero,new Vector2(340,100),Open);b.name="ConsumableInventoryButton";
        b.onClick.RemoveAllListeners();b.onClick.AddListener(()=>{AudioManager.Instance?.PlayClickSE();Open();});
        var src=source.GetComponent<Image>();if(src){var im=b.GetComponent<Image>();im.sprite=src.sprite;im.type=src.type;im.color=src.color;im.material=src.material;im.preserveAspect=src.preserveAspect;}
        b.transition=source.transition;b.colors=source.colors;b.spriteState=source.spriteState;
        var srcText=source.GetComponentInChildren<TMP_Text>();if(srcText){var text=b.GetComponentInChildren<TMP_Text>();text.font=srcText.font;text.fontSharedMaterial=srcText.fontSharedMaterial;text.fontSizeMax=srcText.fontSize;text.color=srcText.color;}
        string[] names={"Button_SkillSet","Button_Play","Button_Equip","Button_Shop","Button_SpecialTile","ConsumableInventoryButton"};
        for(int i=0;i<names.Length;i++){
            var button=canvas.GetComponentsInChildren<Button>(true).FirstOrDefault(x=>x.name==names[i]);if(!button)continue;
            var rt=(RectTransform)button.transform;rt.SetParent(canvas.transform,false);rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.localScale=Vector3.one;
            rt.sizeDelta=new Vector2(340,100);rt.anchoredPosition=new Vector2(-500+i%3*500,i<3?-335:-455);
        }
        Canvas.ForceUpdateCanvases();
    }
    public static void Open(){
        var old=SceneManager.GetActiveScene();var scene=SceneManager.CreateScene("ConsumableInventoryScene");SceneManager.SetActiveScene(scene);
        new GameObject("ConsumableInventory").AddComponent<SeventeenStepsInventory>().Build();SceneManager.UnloadSceneAsync(old);
    }
    static void ClearContents(Transform parent){
        for(int i=parent.childCount-1;i>=0;i--){
            var child=parent.GetChild(i);if(child.name=="UnifiedGoldBorder")continue;
            child.gameObject.SetActive(false);
            if(Application.isPlaying)Destroy(child.gameObject);else DestroyImmediate(child.gameObject);
        }
    }
    static RectTransform Panel(string name,Transform parent,Vector2 position,Vector2 size){
        var panel=SeventeenStepsUI.Rect(name,parent,position,size);
        JanshinPanelTheme.Apply(panel.gameObject.AddComponent<Image>());
        return panel;
    }
    static TextMeshProUGUI Text(Transform parent,string value,Vector2 position,Vector2 size,float font){
        var label=SeventeenStepsUI.Label(parent,value,position,size,font);
        label.color=JanshinPanelTheme.Ivory;label.margin=new Vector4(10,5,10,5);
        return label;
    }
    void Build(){
        root=SeventeenStepsUI.CreateCanvas(transform,"遺物");
        root.gameObject.AddComponent<JanshinThemeTextScope>();
        var shade=SeventeenStepsUI.Rect("InventoryBackdropShade",root,Vector2.zero,new Vector2(1920,1080)).gameObject.AddComponent<Image>();
        shade.color=new Color(0,0,0,.45f);shade.raycastTarget=false;
        shade.rectTransform.anchorMin=Vector2.zero;shade.rectTransform.anchorMax=Vector2.one;shade.rectTransform.offsetMin=shade.rectTransform.offsetMax=Vector2.zero;
        shade.transform.SetSiblingIndex(2);
        var title=root.GetComponentsInChildren<TextMeshProUGUI>().First();
        title.rectTransform.anchoredPosition=new Vector2(0,455);title.rectTransform.sizeDelta=new Vector2(1200,80);
        title.font=LocalizationManager.Instance.GetTitleFont()??title.font;title.fontSharedMaterial=title.font.material;
        title.fontSizeMax=100;title.fontSizeMin=72;title.color=JanshinPanelTheme.Ivory;SeventeenStepsUI.BlackOutline(title);
        var owned=Panel("OwnedRelicsPanel",root,new Vector2(-310,-10),new Vector2(1180,790));
        var equippedPanel=Panel("EquippedRelicsPanel",root,new Vector2(620,-10),new Vector2(550,790));
        Text(owned,"所持遺物",new Vector2(0,344),new Vector2(1060,68),38).color=JanshinPanelTheme.Gold;
        Text(equippedPanel,"装備遺物　1枠",new Vector2(0,344),new Vector2(490,68),38).color=JanshinPanelTheme.Gold;
        var viewport=SeventeenStepsUI.Rect("OwnedItems",owned,new Vector2(0,-47),new Vector2(1124,670));
        viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
        rows=SeventeenStepsUI.Rect("Rows",viewport,Vector2.zero,new Vector2(1110,670));rows.anchorMin=rows.anchorMax=rows.pivot=new Vector2(.5f,1);rows.anchoredPosition=Vector2.zero;
        scroll.content=rows;scroll.scrollSensitivity=45;
        var track=SeventeenStepsUI.Rect("ScrollBar",owned,new Vector2(568,-47),new Vector2(14,642));track.gameObject.AddComponent<Image>().color=new Color(.02f,.02f,.025f,.8f);
        var handle=SeventeenStepsUI.Rect("Handle",track,Vector2.zero,new Vector2(14,80));var hi=handle.gameObject.AddComponent<Image>();hi.color=JanshinPanelTheme.Gold;handle.sizeDelta=Vector2.zero;
        var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;bar.handleRect=handle;bar.targetGraphic=hi;
        var colors=bar.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.15f,1.1f,.95f);colors.pressedColor=new Color(.8f,.7f,.5f);bar.colors=colors;
        scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        equipment=Panel("EquippedItem",equippedPanel,new Vector2(0,100),new Vector2(500,380));
        detail=Text(equippedPanel,"",new Vector2(0,-145),new Vector2(490,80),28);
        equipButton=SeventeenStepsUI.Navigation(equippedPanel,"装備する",new Vector2(0,-245),new Vector2(420,76),()=>{SeventeenStepsMode.Equip(selected);Refresh();});
        removeButton=SeventeenStepsUI.Navigation(equippedPanel,"装備を外す",new Vector2(0,-335),new Vector2(420,76),()=>{SeventeenStepsMode.Equip(0);Refresh();});
        var note=Panel("CarryInNote",root,new Vector2(-280,-460),new Vector2(1230,78));
        Text(note,"装備した1個を次の通常ランに持ち込み。そのラン限り有効。",Vector2.zero,new Vector2(1190,65),26);
        SeventeenStepsUI.Navigation(root,"メニューへ",new Vector2(620,-460),new Vector2(420,78),()=>SceneManager.LoadScene("MenuScene"));Refresh();
    }
    void Refresh(){
        ClearContents(rows);var s=SeventeenStepsMode.LoadStock();var groups=s.items.GroupBy(id=>id).OrderBy(g=>g.Key).ToArray();
        rows.sizeDelta=new Vector2(1110,Mathf.Max(670,Mathf.CeilToInt(groups.Length/3f)*264+18));
        for(int i=0;i<groups.Length;i++){
            int id=groups[i].Key;var d=RunConsumables.Get(id);if(d==null)continue;
            var b=SeventeenStepsUI.Button(rows,d.Name+"\n×"+groups[i].Count()+(id==s.equipped?"　装備中":""),new Vector2((i%3-1)*368,-135-(i/3)*264),new Vector2(346,250),()=>{selected=id;Refresh();});
            b.name="Relic_"+id;
            var rowRect=(RectTransform)b.transform;rowRect.anchorMin=rowRect.anchorMax=new Vector2(.5f,1);rowRect.anchoredPosition=new Vector2((i%3-1)*368,-135-(i/3)*264);
            b.GetComponent<Image>().color=id==selected?new Color(1.18f,1.05f,.78f):Color.white;
            var label=b.GetComponentInChildren<TMP_Text>();label.rectTransform.anchoredPosition=new Vector2(57,55);label.rectTransform.sizeDelta=new Vector2(212,85);
            label.fontSizeMax=28;label.fontSizeMin=22;label.margin=new Vector4(4,2,4,2);label.color=JanshinPanelTheme.Ivory;
            var effect=Text(b.transform,d.Description,new Vector2(0,-62),new Vector2(315,100),22);effect.margin=new Vector4(4,3,4,3);
            SeventeenStepsUI.Picture(b.transform,d.Icon,new Vector2(-111,53),new Vector2(106,110));
        }
        if(groups.Length==0)Text(rows,"所持遺物なし\n外伝モードで神を倒すと獲得",Vector2.zero,new Vector2(1040,180),34);
        var equipped=RunConsumables.Get(s.equipped);var chosen=RunConsumables.Get(selected);
        ClearContents(equipment);JanshinPanelTheme.Apply(equipment.GetComponent<Image>());
        if(equipped!=null){
            SeventeenStepsUI.Picture(equipment,equipped.Icon,new Vector2(0,95),new Vector2(210,170));
            Text(equipment,equipped.Name,new Vector2(0,-33),new Vector2(450,54),34);
            Text(equipment,equipped.Description,new Vector2(0,-114),new Vector2(450,100),28);
        }else Text(equipment,"未装備\n持ち込む遺物を選択",Vector2.zero,new Vector2(450,170),32);
        detail.text=chosen==null?"所持一覧から遺物を選択":"選択中："+chosen.Name;
        equipButton.interactable=chosen!=null&&s.items.Contains(selected)&&s.equipped!=selected;
        removeButton.interactable=equipped!=null;
    }
}
