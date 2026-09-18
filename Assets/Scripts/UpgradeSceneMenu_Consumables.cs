using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed partial class UpgradeSceneMenu
{
    Button consumableStoreButton;
    ConsumableWindow consumableStore;
    int[] consumableOffers;
    readonly HashSet<int> consumableSold=new HashSet<int>();
    int consumableSelected=-1;
    void BuildConsumableStore()
    {
        consumableOffers=RunConsumables.RollOffers(new System.Random());
        if(!ofudaButton || !menuRoot)return;
        consumableStoreButton=Instantiate(ofudaButton,ofudaButton.transform.parent,false);
        consumableStoreButton.name="Button_Consumables";
        consumableStoreButton.onClick=new Button.ButtonClickedEvent();
        consumableStoreButton.onClick.AddListener(OpenConsumableStore);
        foreach(var localized in consumableStoreButton.GetComponentsInChildren<LocalizedTextUI>(true)) localized.enabled=false;
        // Preserve the original frame and button artwork; make room for the fifth option.
        var buttons=new[]{ofudaButton,statusButton,deckButton,traitYakuButton,consumableStoreButton}.Where(b=>b).ToArray();
        var row=(RectTransform)ofudaButton.transform.parent;
        row.anchorMin=row.anchorMax=new Vector2(.5f,.5f);row.anchoredPosition=Vector2.zero;row.sizeDelta=new Vector2(1780,150);
        var layout=row.GetComponent<HorizontalLayoutGroup>();
        if(layout){layout.childAlignment=TextAnchor.MiddleCenter;layout.spacing=24;layout.childControlWidth=false;layout.childControlHeight=false;layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;}
        for(int i=0;i<buttons.Length;i++)
        {
            var rt=(RectTransform)buttons[i].transform;
            rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.pivot=new Vector2(.5f,.5f);
            rt.anchoredPosition=new Vector2((i-2)*344,0);rt.sizeDelta=new Vector2(320,134);
            foreach(var label in buttons[i].GetComponentsInChildren<TMP_Text>(true)) {label.enableAutoSizing=true;label.fontSizeMin=24;label.fontSizeMax=36;}
        }
        RefreshConsumableStoreLabel();
        LocalizationManager.LanguageChanged+=ConsumableLanguageChanged;
    }
    void ConsumableLanguageChanged(LocalizationManager.Language lang){RefreshConsumableStoreLabel();}
    void RefreshConsumableStoreLabel()
    {
        if(consumableStoreButton)
            foreach(var t in consumableStoreButton.GetComponentsInChildren<TMP_Text>(true)) t.text=ConsumableWindow.T("アイテム購入","Buy items","购买道具");
    }
    void OpenConsumableStore()
    {
        if(consumableStore)return;
        Sprite frame=null;
        if(ofudaStoreRoot)
        {
            var image=ofudaStoreRoot.GetComponentsInChildren<Image>(true).Where(i=>i.sprite && i.rectTransform.rect.width>600).OrderByDescending(i=>i.rectTransform.rect.width*i.rectTransform.rect.height).FirstOrDefault();
            if(image)frame=image.sprite;
        }
        consumableStore=ConsumableWindow.Open(transform,ConsumableWindow.T("アイテム購入","Buy consumables","购买消耗道具"),frame);
        var sceneImages=gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Image>(true));
        var background=sceneImages.Where(i=>i.sprite&& !i.transform.IsChildOf(consumableStore.transform)&&i.sprite.texture.width>=1000).OrderByDescending(i=>i.rectTransform.rect.width*i.rectTransform.rect.height).FirstOrDefault();
     consumableStore.ConfigureStore(backFromDeckButton?backFromDeckButton.GetComponent<Image>().sprite:null,background?background.sprite:null,()=>UpgradeNextButton.Advance());
        consumableStore.Closed=()=>consumableStore=null;
        consumableSelected=-1;
        consumableStore.Confirm.onClick.AddListener(BuyConsumable);
        RefreshConsumableOffers();
    }
    void RefreshConsumableOffers()
    {
        var bag=RunConsumables.Load().bag;
        consumableStore.GoldAmount.text=GameManager.RunCurrency.Get().ToString("N0");
        consumableStore.Summary.text=ConsumableWindow.T("所持","Inventory","持有")+": "+bag.Count+" / "+RunConsumables.Capacity+"    "+ConsumableWindow.T("敗北するとすべて失います","All items are lost on defeat","失败后失去所有道具");
        consumableStore.Items(consumableOffers,i=>{consumableSelected=i;RefreshConsumableOffers();},true,consumableSold,consumableSelected);
        consumableStore.ClearChoices();
        ConsumableWindow.Label("BagHeading",consumableStore.Choices,ConsumableWindow.T("所持アイテム","Your inventory","持有道具"),new Vector2(0,78),new Vector2(1000,34),23);
        for(int i=0;i<RunConsumables.Capacity;i++)
        {
            float x=(i-1.5f)*320;
            var owned=i<bag.Count?RunConsumables.Get(bag[i]):null;
            var tile=ConsumableWindow.Rect("BagSlot"+i,consumableStore.Choices,new Vector2(x,-4),new Vector2(300,125));
            var bg=tile.gameObject.AddComponent<Image>();bg.color=new Color(.83f,.83f,.76f,.5f);bg.raycastTarget=false;
            if(owned!=null){var icon=ConsumableWindow.Rect("Icon",tile,new Vector2(-95,0),new Vector2(88,88)).gameObject.AddComponent<Image>();icon.sprite=owned.Icon;icon.preserveAspect=true;icon.raycastTarget=false;}
            ConsumableWindow.Label("Name",tile,owned!=null?owned.Name:ConsumableWindow.T("空き","Empty","空位"),new Vector2(owned!=null?44:0,0),new Vector2(owned!=null?180:280,105),22);
        }
        consumableStore.Confirm.GetComponentInChildren<TMP_Text>().text=ConsumableWindow.T("購入する","Purchase","购买");
        consumableStore.WhiteStoreText();
        if(consumableSelected<0){consumableStore.Detail.text=ConsumableWindow.T("アイテムを選ぶと説明が表示されます。各商品は1個まで購入できます。","Select an item to see its effect. Each offer can be purchased once.","选择道具查看效果。每项商品只能购买一次。");return;}
var d=RunConsumables.Get(consumableOffers[consumableSelected]);
GameManager.ApplyTraitSpriteAssetToTMPAnywhere(consumableStore.Detail);
consumableStore.Detail.richText=true;
consumableStore.Detail.text=d.Name+"\n"+GameManager.RenderConsumableDescriptionAnywhere(d.Description);
        bool room=bag.Count<RunConsumables.Capacity, money=GameManager.RunCurrency.Get()>=d.price, sold=consumableSold.Contains(consumableSelected);
        consumableStore.Confirm.interactable=room&&money&&!sold;
        consumableStore.Status.text=sold?ConsumableWindow.T("購入済みです","Already purchased","已购买"):!room?ConsumableWindow.T("所持枠がいっぱいです","Inventory is full","持有栏已满"):!money?ConsumableWindow.T("所持金が足りません","Not enough funds","持有金额不足"):ConsumableWindow.T("購入するアイテムを確認してください","Confirm the selected item","请确认所选道具");
    }
    void BuyConsumable()
    {
        if(consumableSelected<0||consumableSold.Contains(consumableSelected))return;
        var s=RunConsumables.Load();var d=RunConsumables.Get(consumableOffers[consumableSelected]);
        if(s.bag.Count>=RunConsumables.Capacity||!GameManager.RunCurrency.Spend(d.price)){RefreshConsumableOffers();return;}
        s.bag.Add(d.id);RunConsumables.Save(s);consumableSold.Add(consumableSelected);RefreshConsumableOffers();
    }
    void OnDestroy(){LocalizationManager.LanguageChanged-=ConsumableLanguageChanged;}
}
