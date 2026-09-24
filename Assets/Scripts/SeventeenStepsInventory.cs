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
    void Build(){
        root=SeventeenStepsUI.CreateCanvas(transform,"遺物");
        SeventeenStepsUI.InfoBacking(root,new Vector2(-320,390),new Vector2(1170,70));SeventeenStepsUI.InfoBacking(root,new Vector2(675,390),new Vector2(490,70));
        SeventeenStepsUI.Label(root,"所持遺物",new Vector2(-320,390),new Vector2(1000,65),36);
        SeventeenStepsUI.Label(root,"装備遺物　1枠",new Vector2(675,390),new Vector2(490,65),36);
        var viewport=SeventeenStepsUI.Rect("OwnedItems",root,new Vector2(-320,-15),new Vector2(1170,710));
        viewport.gameObject.AddComponent<Image>().color=new Color(.97f,.96f,.87f,.95f);viewport.gameObject.AddComponent<RectMask2D>();
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
        rows=SeventeenStepsUI.Rect("Rows",viewport,Vector2.zero,new Vector2(1150,710));rows.anchorMin=new Vector2(.5f,1);rows.anchorMax=new Vector2(.5f,1);rows.pivot=new Vector2(.5f,1);rows.anchoredPosition=Vector2.zero;scroll.content=rows;scroll.scrollSensitivity=45;
        var track=SeventeenStepsUI.Rect("ScrollBar",root,new Vector2(278,-15),new Vector2(18,710));track.gameObject.AddComponent<Image>().color=new Color(0,0,0,.5f);
        var handle=SeventeenStepsUI.Rect("Handle",track,Vector2.zero,new Vector2(18,80));var hi=handle.gameObject.AddComponent<Image>();hi.color=new Color(.8f,.7f,.45f);
        handle.sizeDelta=Vector2.zero;
        var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;bar.handleRect=handle;bar.targetGraphic=hi;
        scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        equipment=SeventeenStepsUI.Rect("EquippedItem",root,new Vector2(670,145),new Vector2(490,460));SeventeenStepsUI.Paper(equipment,equipment.sizeDelta);
        SeventeenStepsUI.InfoBacking(root,new Vector2(670,-145),new Vector2(490,110));
        detail=SeventeenStepsUI.Label(root,"",new Vector2(670,-145),new Vector2(490,105),25);
        equipButton=SeventeenStepsUI.Navigation(root,"装備する",new Vector2(670,-255),new Vector2(370,70),()=>{SeventeenStepsMode.Equip(selected);Refresh();});
        removeButton=SeventeenStepsUI.Navigation(root,"装備を外す",new Vector2(670,-345),new Vector2(370,70),()=>{SeventeenStepsMode.Equip(0);Refresh();});
        SeventeenStepsUI.InfoBacking(root,new Vector2(-240,-430),new Vector2(1330,68));
        SeventeenStepsUI.Label(root,"装備した1個を次の通常ランに持ち込み。そのラン限り有効。",new Vector2(-240,-430),new Vector2(1330,60),26);
        SeventeenStepsUI.Navigation(root,"メニューへ",new Vector2(680,-465),new Vector2(370,70),()=>SceneManager.LoadScene("MenuScene"));Refresh();
    }
    void Refresh(){
        SeventeenStepsUI.Clear(rows);var s=SeventeenStepsMode.LoadStock();var groups=s.items.GroupBy(id=>id).OrderBy(g=>g.Key).ToArray();
        rows.sizeDelta=new Vector2(1150,Mathf.Max(710,Mathf.CeilToInt(groups.Length/3f)*220));
        for(int i=0;i<groups.Length;i++){
            int id=groups[i].Key;var d=RunConsumables.Get(id);if(d==null)continue;
            var b=SeventeenStepsUI.Button(rows,d.Name+"\n×"+groups[i].Count()+(id==s.equipped?"　装備中":""),new Vector2((i%3-1)*375,-115-(i/3)*220),new Vector2(350,205),()=>{selected=id;Refresh();});
            var rowRect=(RectTransform)b.transform;rowRect.anchorMin=rowRect.anchorMax=new Vector2(.5f,1);rowRect.anchoredPosition=new Vector2((i%3-1)*375,-115-(i/3)*220);
            SeventeenStepsUI.Frame(b.transform,new Vector2(350,205));
            var label=b.GetComponentInChildren<TMP_Text>();label.rectTransform.anchoredPosition=new Vector2(53,45);label.rectTransform.sizeDelta=new Vector2(215,85);label.fontSizeMax=25;label.fontSizeMin=21;
            var effect=SeventeenStepsUI.Label(b.transform,d.Description,new Vector2(48,-48),new Vector2(220,100),20);effect.color=Color.black;
            SeventeenStepsUI.Picture(b.transform,d.Icon,new Vector2(-112,0),new Vector2(115,150));
        }
        if(groups.Length==0)SeventeenStepsUI.Label(viewportForEmpty(),"所持遺物なし\n外伝モードで敵を倒すと獲得",new Vector2(0,0),new Vector2(1040,180),34).color=Color.black;
        var equipped=RunConsumables.Get(s.equipped);var chosen=RunConsumables.Get(selected);
        SeventeenStepsUI.Clear(equipment);SeventeenStepsUI.Frame(equipment,equipment.sizeDelta);
        if(equipped!=null){
            SeventeenStepsUI.Picture(equipment,equipped.Icon,new Vector2(0,92),new Vector2(190,180));
            SeventeenStepsUI.Label(equipment,equipped.Name,new Vector2(0,-25),new Vector2(440,65),32).color=Color.black;
            SeventeenStepsUI.Label(equipment,equipped.Description,new Vector2(0,-132),new Vector2(430,140),26).color=Color.black;
        }else SeventeenStepsUI.Label(equipment,"未装備\n持ち込む遺物を選択",Vector2.zero,new Vector2(425,170),30).color=Color.black;
        detail.text=chosen==null?"所持一覧から遺物を選択":"選択中："+chosen.Name;
        equipButton.interactable=chosen!=null&&s.items.Contains(selected)&&s.equipped!=selected;
        removeButton.interactable=equipped!=null;
    }
    Transform viewportForEmpty()=>rows.parent;
}
