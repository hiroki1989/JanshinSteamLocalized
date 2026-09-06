using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class UpgradeManager
{
    [Header("Choose a tile")]
    [SerializeField, Min(1)] private int selectedTileCostMultiplier = 3;
    [SerializeField] private Button chooseBuyButton, chooseDestroyButton;
    [SerializeField] private GameObject selectedTileShopRoot;
    private readonly System.Collections.Generic.List<UnityEngine.EventSystems.BaseRaycaster> selectedShopRaycasters = new System.Collections.Generic.List<UnityEngine.EventSystems.BaseRaycaster>();
    private bool selectedTileDestroyMode;
    private int selectedShopTile = -1;

    private string TileShopText(string ja,string en,string zh) => MonetizationText.Get(ja,en,zh);
    private int SelectedTilePrice => Mathf.Max(1, selectedTileCostMultiplier) *
        GetScaledCost(selectedTileDestroyMode ? destroyCost : buyCost,
            selectedTileDestroyMode ? destroyCostIncrease : buyCostIncrease,
            selectedTileDestroyMode ? PrefKey_CostCount_Destroy : PrefKey_CostCount_Buy);

    public void BuildSelectedTileShopUI()
    {
        if(!buyButton || !destroyButton) return;
        if(!chooseBuyButton) chooseBuyButton=MakeTileShopEntry(buyButton,"ChooseBuy");
        if(!chooseDestroyButton) chooseDestroyButton=MakeTileShopEntry(destroyButton,"ChooseDestroy");
        chooseBuyButton.onClick=new Button.ButtonClickedEvent();
        chooseDestroyButton.onClick=new Button.ButtonClickedEvent();
        chooseBuyButton.onClick.AddListener(()=>OpenSelectedTileShop(false));
        chooseDestroyButton.onClick.AddListener(()=>OpenSelectedTileShop(true));
        chooseBuyButton.GetComponentInChildren<TMP_Text>().text=TileShopText("牌を選んで購入","Choose a tile to buy","选择购买的牌");
        chooseDestroyButton.GetComponentInChildren<TMP_Text>().text=TileShopText("牌を選んで破壊","Choose a tile to remove","选择销毁的牌");
        if(!selectedTileShopRoot) {
            var root=new GameObject("SelectedTileShop",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(Image));
            root.transform.SetParent(transform,false);
            var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=32500;
            var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900); scaler.matchWidthOrHeight=.5f;
            root.GetComponent<Image>().color=new Color(0,0,0,.9f);
            selectedTileShopRoot=root;
            var card=TileShopRect("Card",root.transform,Vector2.zero,new Vector2(1080,800));
            card.gameObject.AddComponent<Image>().color=new Color(.055f,.075f,.1f);
            TileShopLabel("Title",card,new Vector2(0,348),new Vector2(900,48),32);
            TileShopLabel("Hint",card,new Vector2(0,294),new Vector2(1000,52),22);
            for(int tile=0;tile<34;tile++) {
                int row=tile<27 ? tile/9 : 3, column=tile<27 ? tile%9 : tile-27;
                var button=TileShopButton("Tile"+tile,card,new Vector2(-464+116*column,190-112*row),new Vector2(104,104));
                var art=TileShopRect("Art",button.transform,new Vector2(0,14),new Vector2(54,66));
                var image=art.gameObject.AddComponent<Image>(); image.sprite=LoadTileSpriteByIndex(tile); image.preserveAspect=true; image.raycastTarget=false;
                var label=button.GetComponentInChildren<TMP_Text>(); label.name="Count"; label.rectTransform.anchoredPosition=new Vector2(0,-36); label.rectTransform.sizeDelta=new Vector2(98,24);
            }
            TileShopLabel("Selection",card,new Vector2(0,-236),new Vector2(980,40),26);
            TileShopLabel("Status",card,new Vector2(0,-279),new Vector2(990,34),22);
            TileShopButton("Cancel",card,new Vector2(-240,-345),new Vector2(340,58));
            TileShopButton("Confirm",card,new Vector2(240,-345),new Vector2(440,58));
        }
        var modal=selectedTileShopRoot.transform.Find("Card");
        for(int i=0;i<34;i++) {
            int tile=i; var button=modal.Find("Tile"+i).GetComponent<Button>();
            button.onClick=new Button.ButtonClickedEvent();
            button.onClick.AddListener(()=> { selectedShopTile=tile; RefreshSelectedTileShop(); });
        }
        var cancel=modal.Find("Cancel").GetComponent<Button>(); cancel.onClick=new Button.ButtonClickedEvent(); cancel.onClick.AddListener(CloseSelectedTileShop);
        var confirm=modal.Find("Confirm").GetComponent<Button>(); confirm.onClick=new Button.ButtonClickedEvent(); confirm.onClick.AddListener(ConfirmSelectedTilePurchase);
        selectedTileShopRoot.SetActive(false);
    }

    private Button MakeTileShopEntry(Button source,string name) {
        var original=(RectTransform)source.transform;
        var button=TileShopButton(name,source.transform.parent,Vector2.zero,new Vector2(original.rect.width,52));
        var rect=(RectTransform)button.transform;
        rect.anchorMin=original.anchorMin; rect.anchorMax=original.anchorMax;
        rect.anchoredPosition=original.anchoredPosition+new Vector2(0,-original.rect.height*.5f-38);
        return button;
    }
    private void OpenSelectedTileShop(bool destroy) {
        selectedTileDestroyMode=destroy; selectedShopTile=-1;
        selectedTileShopRoot.SetActive(true);
        selectedTileShopRoot.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        RefreshSelectedTileShop();
        if(Application.isPlaying) {
            var own=selectedTileShopRoot.GetComponent<GraphicRaycaster>();
            foreach(var raycaster in FindObjectsByType<UnityEngine.EventSystems.BaseRaycaster>(FindObjectsSortMode.None))
                if(raycaster && raycaster!=own && raycaster.enabled) { selectedShopRaycasters.Add(raycaster); raycaster.enabled=false; }
        }
        if(UnityEngine.EventSystems.EventSystem.current) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }
    private void CloseSelectedTileShop() {
        if(selectedTileShopRoot) selectedTileShopRoot.SetActive(false);
        foreach(var raycaster in selectedShopRaycasters) if(raycaster) raycaster.enabled=true;
        selectedShopRaycasters.Clear();
        if(UnityEngine.EventSystems.EventSystem.current) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }
    internal static bool CanModifySelectedTile(int index, bool remove, int price, int gold, int[] counts, int minimum) {
        if(index<0 || index>=34 || price<1 || gold<price || counts==null || counts.Length<34) return false;
        if(!remove) return true;
        int total=0; foreach(int count in counts) total+=Mathf.Max(0,count);
        return counts[index]>0 && total>Mathf.Max(1,minimum);
    }
    private bool CanPurchaseSelectedTile() => CanModifySelectedTile(selectedShopTile,selectedTileDestroyMode,
        SelectedTilePrice,CurrentGold,PlayerData.GetDeckCountsCopy(),minDeckSize);
    private void ConfirmSelectedTilePurchase() {
        if(!CanPurchaseSelectedTile()) { RefreshSelectedTileShop(); return; }
        int tile=selectedShopTile, price=SelectedTilePrice;
        if(!TrySpendGold(price)) { RefreshSelectedTileShop(); return; }
        PlayerData.AddToDeck(tile,selectedTileDestroyMode ? -1 : 1);
        IncrementPurchaseCount(selectedTileDestroyMode ? PrefKey_CostCount_Destroy : PrefKey_CostCount_Buy);
        selectedShopTile=-1;
        RefreshUI(); RefreshSelectedTileShop();
    }
    private void RefreshSelectedTileShopLabels() {
        if(chooseBuyButton) chooseBuyButton.GetComponentInChildren<TMP_Text>().text=TileShopText("牌を選んで購入","Choose a tile to buy","选择购买的牌");
        if(chooseDestroyButton) chooseDestroyButton.GetComponentInChildren<TMP_Text>().text=TileShopText("牌を選んで破壊","Choose a tile to remove","选择销毁的牌");
        if(selectedTileShopRoot && selectedTileShopRoot.activeSelf) RefreshSelectedTileShop();
    }
    private void RefreshSelectedTileShop() {
        if(!selectedTileShopRoot) return;
        var card=selectedTileShopRoot.transform.Find("Card");
        card.Find("Title").GetComponent<TMP_Text>().text=selectedTileDestroyMode ? TileShopText("指定した牌を1枚破壊","Remove one chosen tile","销毁一张指定的牌") : TileShopText("指定した牌を1枚購入","Buy one chosen tile","购买一张指定的牌");
        card.Find("Hint").GetComponent<TMP_Text>().text=TileShopText("牌を選び、金額を確認して実行してください。","Select a tile, review the price, then confirm.","请选择牌，确认价格后执行。")+"  Gold: "+CurrentGold;
        var counts=PlayerData.GetDeckCountsCopy();
        for(int i=0;i<34;i++) {
            var button=card.Find("Tile"+i).GetComponent<Button>();
            button.interactable=!selectedTileDestroyMode || (counts[i]>0 && PlayerData.TotalDeckCount()>Mathf.Max(1,minDeckSize));
            button.GetComponent<Image>().color=i==selectedShopTile ? new Color(.65f,.46f,.13f) : new Color(.15f,.2f,.26f);
            button.GetComponentInChildren<TMP_Text>().text="×"+counts[i];
        }
        card.Find("Selection").GetComponent<TMP_Text>().text=selectedShopTile<0 ? TileShopText("牌を選択してください","Select a tile","请选择牌") : TileShopText(PlayerData.TileName(selectedShopTile), GameManager.IndexToId(selectedShopTile), PlayerData.TileName(selectedShopTile))+"  ×1";
        card.Find("Status").GetComponent<TMP_Text>().text=selectedTileDestroyMode && PlayerData.TotalDeckCount()<=Mathf.Max(1,minDeckSize) ?
            TileShopText("デッキの最低枚数に達しています。","The deck is at its minimum size.","牌组已达到最低张数。") :
            CurrentGold<SelectedTilePrice ? TileShopText("Goldが足りません。","Not enough Gold.","Gold不足。") : "";
        var confirm=card.Find("Confirm").GetComponent<Button>();
        confirm.interactable=CanPurchaseSelectedTile();
        confirm.GetComponentInChildren<TMP_Text>().text=(selectedTileDestroyMode ? TileShopText("1枚破壊","Remove 1","销毁1张") : TileShopText("1枚購入","Buy 1","购买1张"))+"  "+SelectedTilePrice+" Gold";
        card.Find("Cancel").GetComponentInChildren<TMP_Text>().text=TileShopText("閉じる","Close","关闭");
    }
    private RectTransform TileShopRect(string name,Transform parent,Vector2 position,Vector2 size) {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f); rect.anchoredPosition=position; rect.sizeDelta=size; return rect;
    }
    private TMP_Text TileShopLabel(string name,Transform parent,Vector2 position,Vector2 size,float fontSize) {
        var text=TileShopRect(name,parent,position,size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font=TMP_Settings.defaultFontAsset; text.fontSize=fontSize; text.enableAutoSizing=true; text.fontSizeMin=18; text.fontSizeMax=fontSize;
        text.color=Color.white; text.raycastTarget=false; text.alignment=TextAlignmentOptions.Center; return text;
    }
    private Button TileShopButton(string name,Transform parent,Vector2 position,Vector2 size) {
        var rect=TileShopRect(name,parent,position,size); var image=rect.gameObject.AddComponent<Image>(); image.color=new Color(.15f,.2f,.26f);
        var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image;
        TileShopLabel("Label",rect,Vector2.zero,size-new Vector2(12,8),24); return button;
    }
}
