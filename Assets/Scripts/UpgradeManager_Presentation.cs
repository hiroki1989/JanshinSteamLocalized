using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class UpgradeManager
{
    void RefreshPresentationStatusLabels()
    {
        if(!buyHpButton || !buyHpButton.transform.Find("PresentationFrame"))return;
        var buttons=new[]{buyHpButton,buyMpButton,buyCastButton,buyHealHpButton,buyHealMpButton};
        var labels=new[]{hpUpLabel,mpUpLabel,castUpLabel,healHpLabel,healMpLabel};
        var values=new[]{hpUpValue,mpUpValue,castUpValue,healHpValue,healMpValue};
        var costs=new[]{GetScaledCost(hpUpCost,hpUpCostIncrease,PrefKey_CostCount_HpUp),GetScaledCost(mpUpCost,mpUpCostIncrease,PrefKey_CostCount_MpUp),GetScaledCost(castUpCost,castUpCostIncrease,PrefKey_CostCount_CastUp),GetScaledCost(healHpCost,healHpCostIncrease,PrefKey_CostCount_HealHp),GetScaledCost(healMpCost,healMpCostIncrease,PrefKey_CostCount_HealMp)};
        var keys=new[]{"Run_HPBonus","Run_MPBonus","Run_SkillCastsBonus"};
        for(int i=0;i<buttons.Length;i++)if(buttons[i]){
            if(labels[i])labels[i].text="+"+values[i].ToString("N0")+(i<3?"\n"+ConsumableWindow.T("累計","Total","累计")+" +"+PlayerPrefs.GetInt(keys[i],0).ToString("N0"):"");
            var price=buttons[i].transform.Find("PresentationPrice")?.GetComponent<TMP_Text>();
            if(!price)price=ConsumableWindow.Label("PresentationPrice",buttons[i].transform,"",new Vector2(25,-95),new Vector2(180,40),28);
            UpgradePanelPresentation.MoveText(price,buttons[i].transform,new Vector2(25,-95),new Vector2(180,40),28);price.text=costs[i].ToString("N0");
            UpgradePanelPresentation.Bag(buttons[i].transform,new Vector2(-85,-95));
        }
    }
    void ApplyShopPresentation(UpgradeSectionMode mode)
    {
        if(mode==UpgradeSectionMode.StatusOnly && statusShopRoot)
        {
            var root=statusShopRoot.transform;
            UpgradePanelPresentation.Header(root);
            var buttons=new[]{buyHpButton,buyMpButton,buyCastButton,buyHealHpButton,buyHealMpButton};
            var labels=new[]{hpUpLabel,mpUpLabel,castUpLabel,healHpLabel,healMpLabel};
            for(int i=0;i<buttons.Length;i++)
            {
                if(!buttons[i])continue;
                var old=buttons[i].transform.parent;
                var pos=i<3?new Vector2((i-1)*490,80):new Vector2((i-3.5f)*490,-265);
                UpgradePanelPresentation.Card(buttons[i],root,pos,new Vector2(450,250));
                if(labels[i])UpgradePanelPresentation.MoveText(labels[i],buttons[i].transform,new Vector2(0,0),new Vector2(400,100),28);
                if(i==3&&currentHpTMP)UpgradePanelPresentation.MoveText(currentHpTMP,buttons[i].transform,new Vector2(0,-50),new Vector2(400,48),24);
                if(i==4&&currentMpTMP)UpgradePanelPresentation.MoveText(currentMpTMP,buttons[i].transform,new Vector2(0,-50),new Vector2(400,48),24);
                if(old!=root)old.gameObject.SetActive(false);
            }
            RefreshPresentationStatusLabels();
        }
        if(mode==UpgradeSectionMode.DeckOnly && deckShopRoot)
        {
            var root=deckShopRoot.transform;
            UpgradePanelPresentation.Header(root);
            var buttons=new[]{buyButton,destroyButton,chooseBuyButton,chooseDestroyButton};
            for(int i=0;i<buttons.Length;i++)if(buttons[i])
                UpgradePanelPresentation.Card(buttons[i],root,new Vector2((i-1.5f)*395,110),new Vector2(375,230));
            if(buyCostTMP)UpgradePanelPresentation.MoveText(buyCostTMP,buyButton.transform,new Vector2(25,-72),new Vector2(230,45),28);
            if(destroyCostTMP)UpgradePanelPresentation.MoveText(destroyCostTMP,destroyButton.transform,new Vector2(25,-72),new Vector2(230,45),28);
            UpgradePanelPresentation.Bag(buyButton.transform,new Vector2(-90,-72));
            UpgradePanelPresentation.Bag(destroyButton.transform,new Vector2(-90,-72));
            if(buyGroupRoot)UpgradePanelPresentation.Place(buyGroupRoot,root,new Vector2(-590,-110),new Vector2(355,125));
            if(destroyGroupRoot)UpgradePanelPresentation.Place(destroyGroupRoot,root,new Vector2(-195,-110),new Vector2(355,125));
            if(rerollBuyButton)UpgradePanelPresentation.Card(rerollBuyButton,root,new Vector2(-590,-280),new Vector2(320,115));
            if(rerollDestroyButton)UpgradePanelPresentation.Card(rerollDestroyButton,root,new Vector2(-195,-280),new Vector2(320,115));
            if(rerollBuyCostTMP)UpgradePanelPresentation.MoveText(rerollBuyCostTMP,root,new Vector2(-565,-380),new Vector2(220,45),30);
            if(rerollDestroyCostTMP)UpgradePanelPresentation.MoveText(rerollDestroyCostTMP,root,new Vector2(-170,-380),new Vector2(220,45),30);
            UpgradePanelPresentation.Bag(root,new Vector2(-680,-380),"BuyRerollBag");
            UpgradePanelPresentation.Bag(root,new Vector2(-285,-380),"DestroyRerollBag");
            var deck=root.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name=="Button_Deck");
            if(deck)UpgradePanelPresentation.Card(deck,root,new Vector2(565,-355),new Vector2(420,110));
            // Old cost decorations belong to the replaced rows, not the deck modal.
            foreach(var name in new[]{"Panel","Rerole"}){var old=root.Find(name);if(old)old.gameObject.SetActive(false);}
        }
        if(mode==UpgradeSectionMode.TraitOnly && traitYakuShopRoot)
        {
            var root=traitYakuShopRoot.transform;
            UpgradePanelPresentation.Header(root);
            bool both=traitUnlockButton && traitUnlockButton!=traitUpgradeButton;
            if(traitUpgradeButton)
            {
                UpgradePanelPresentation.Card(traitUpgradeButton,root,new Vector2(both?400:0,-35),new Vector2(700,420));
                if(traitUpgradeOfferTMP)UpgradePanelPresentation.MoveText(traitUpgradeOfferTMP,traitUpgradeButton.transform,new Vector2(35,45),new Vector2(570,190),34);
                if(traitUpgradeTraitIconImage){UpgradePanelPresentation.Place(traitUpgradeTraitIconImage.rectTransform,traitUpgradeButton.transform,new Vector2(-285,45),new Vector2(65,65));traitUpgradeTraitIconImage.enabled=traitUpgradeTraitIconImage.sprite;}
                var action=traitUpgradeButton.GetComponentsInChildren<TMP_Text>().FirstOrDefault(t=>t!=traitUpgradeOfferTMP);if(action)UpgradePanelPresentation.MoveText(action,traitUpgradeButton.transform,new Vector2(0,-150),new Vector2(580,65),34);
                if(traitUpgradeLabelTMP)UpgradePanelPresentation.MoveText(traitUpgradeLabelTMP,traitUpgradeButton.transform,new Vector2(0,155),new Vector2(580,65),38);
            }
            if(both){
                UpgradePanelPresentation.Card(traitUnlockButton,root,new Vector2(-400,-35),new Vector2(700,420));
                if(traitUnlockOfferTMP)UpgradePanelPresentation.MoveText(traitUnlockOfferTMP,traitUnlockButton.transform,new Vector2(0,25),new Vector2(580,170),34);
                if(traitUnlockLabelTMP)UpgradePanelPresentation.MoveText(traitUnlockLabelTMP,traitUnlockButton.transform,new Vector2(0,155),new Vector2(580,65),38);
            }
            var old=root.Find("Panel");if(old)old.gameObject.SetActive(false);
        }
    }
}

public static class UpgradePanelPresentation
{
    // iOS panel text: rich-text rarity colors remain authoritative.
    public static void Black40(TMP_Text text)
    {
        if(!text)return;
        text.color=Color.black;text.outlineWidth=0;
        text.enableAutoSizing=false;text.fontSize=40;
        text.fontSizeMin=40;text.fontSizeMax=40;
        text.richText=true;
    }
    static readonly System.Collections.Generic.Dictionary<Color,Sprite> gradients=new System.Collections.Generic.Dictionary<Color,Sprite>();
    public static Sprite RarityGradient(Color color)
    {
        color=Color.Lerp(color,new Color(1f,.99f,.93f),.55f);color.a=1;
        if(gradients.TryGetValue(color,out var sprite))return sprite;
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.wrapMode=TextureWrapMode.Clamp;
        texture.SetPixels(new[]{color,color,new Color(1f,.99f,.88f),new Color(1f,.99f,.88f)});texture.Apply();
        sprite=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f,1);gradients[color]=sprite;return sprite;
    }

    public static void Place(RectTransform rt,Transform parent,Vector2 pos,Vector2 size)
    {
        rt.SetParent(parent,false);rt.localScale=Vector3.one;rt.anchorMin=rt.anchorMax=rt.pivot=Vector2.one*.5f;rt.anchoredPosition=pos;rt.sizeDelta=size;
        var le=rt.GetComponent<LayoutElement>();if(le)le.ignoreLayout=true;
    }
    public static void MoveText(TMP_Text t,Transform parent,Vector2 pos,Vector2 size,float font)
    {
        Place(t.rectTransform,parent,pos,size);t.color=Color.white;t.outlineColor=Color.black;t.outlineWidth=.22f;
        t.fontSizeMax=font;t.fontSizeMin=20;t.enableAutoSizing=true;t.alignment=TextAlignmentOptions.Center;t.margin=Vector4.zero;
        var button=t.GetComponentInParent<Button>();
        if(button && button.transform.Find("PresentationFrame"))Black40(t);
    }
    public static void Header(Transform root)
    {
        foreach(var child in root.Cast<Transform>().ToArray())
        {
            if(child.name.Trim().StartsWith("Text_Header")){var t=child.GetComponent<TMP_Text>();if(t)MoveText(t,root,new Vector2(0,385),new Vector2(1100,110),80);}
            if(child.name.StartsWith("Button_Back")||child.name=="Button_Next")
            {
                Place((RectTransform)child,root,new Vector2(child.name=="Button_Next"?740:-740,290),new Vector2(300,100));
                var t=child.GetComponentInChildren<TMP_Text>();if(t)MoveText(t,child,Vector2.zero,new Vector2(280,70),44);
            }
        }
        var gold=root.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TMP_Text>(true)).FirstOrDefault(t=>t.name=="TMP_GoldText");
        if(gold){
            MoveText(gold,gold.transform.parent,new Vector2(-698,435),new Vector2(200,70),45);
            var currencySprite=Resources.Load<Sprite>("Consumables/CurrencyBag");
            foreach(var im in root.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Image>(true)))
                if(im.sprite&&im.sprite.name=="T_5_coin_bag2_"&&im.rectTransform.rect.width>55)im.enabled=false;
            Bag(gold.transform.parent,new Vector2(-802,435),"PresentationWallet");
            var wallet=(RectTransform)gold.transform.parent.Find("PresentationWallet");wallet.sizeDelta=new Vector2(60,60);
        }
    }
    public static void Bag(Transform parent,Vector2 position,string name="PresentationBag")
    {
        var rt=parent.Find(name)as RectTransform;
        if(!rt){rt=ConsumableWindow.Rect(name,parent,position,new Vector2(36,36));var im=rt.gameObject.AddComponent<Image>();im.sprite=Resources.Load<Sprite>("Consumables/CurrencyBag");im.preserveAspect=true;im.raycastTarget=false;}
        Place(rt,parent,position,new Vector2(36,36));
    }
    public static void Card(Button button,Transform root,Vector2 pos,Vector2 size)
    {
        if(!button)return;
        Place((RectTransform)button.transform,root,pos,size);
        var oldGradient=button.GetComponent<UIGradient>();if(oldGradient)oldGradient.enabled=false;
        var img=button.GetComponent<Image>();if(img){img.sprite=null;img.color=new Color(.95f,.95f,.89f);button.targetGraphic=img;}
        foreach(var image in button.GetComponentsInChildren<Image>(true))if(image.transform!=button.transform&&!image.name.StartsWith("Presentation"))image.enabled=false;
        var border=button.transform.Find("PresentationFrame")as RectTransform;
        if(!border){border=ConsumableWindow.Rect("PresentationFrame",button.transform,Vector2.zero,size);var im=border.gameObject.AddComponent<Image>();im.sprite=Resources.Load<Sprite>("Consumables/PanelFrame");im.type=Image.Type.Sliced;im.pixelsPerUnitMultiplier=8;im.raycastTarget=false;}
        Place(border,button.transform,Vector2.zero,size);border.SetAsFirstSibling();
        var label=button.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();
        if(label)MoveText(label,button.transform,new Vector2(0,size.y>180?85:0),new Vector2(size.x-40,size.y>180?70:size.y-20),36);
    }
    public static void Ofuda(GameObject panel)
    {
        if(!panel)return;var root=panel.transform;Header(root);
        foreach(var name in new[]{"OfferSlotsRow","SlotsRow"})
        {
            var row=root.Find(name)as RectTransform;if(!row)continue;
            foreach(var layout in row.GetComponents<LayoutGroup>())layout.enabled=false;
            Place(row,root,new Vector2(0,name=="SlotsRow"?-260:70),new Vector2(1470,name=="SlotsRow"?220:300));
            int i=0;
            foreach(var b in row.GetComponentsInChildren<Button>(true).Where(b=>b.transform.parent==row).ToArray())
            {
                Place((RectTransform)b.transform,row,new Vector2((i++-1)*490,0),new Vector2(450,name=="SlotsRow"?220:300));
                // Keep the rarity gradient and item-art layout supplied by the store.
                var frame=b.transform.Find("PresentationFrame")as RectTransform;
                if(!frame){frame=ConsumableWindow.Rect("PresentationFrame",b.transform,Vector2.zero,((RectTransform)b.transform).sizeDelta);var image=frame.gameObject.AddComponent<Image>();image.sprite=Resources.Load<Sprite>("Consumables/PanelFrame");image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=8;image.raycastTarget=false;}
                frame.sizeDelta=((RectTransform)b.transform).sizeDelta;frame.SetAsLastSibling();
                foreach(var text in b.GetComponentsInChildren<TMP_Text>(true)){Black40(text);}
            }
        }
        foreach(var store in panel.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<UpgradeOfudaStore>(true)))store.ApplyShopPresentation();
        var discard=root.Find("DiscardButton")as RectTransform;if(discard)Place(discard,root,new Vector2(0,-455),new Vector2(300,85));
        var cap=root.Find("CapacityTMP");if(cap)Place((RectTransform)cap,root,new Vector2(160,-125),new Vector2(140,45));
        var heading=root.Find("Text (TMP)");if(heading)Place((RectTransform)heading,root,new Vector2(-30,-125),new Vector2(220,45));
        // Replace the old wide decoration behind all offers with individual frames.
        foreach(var image in root.GetComponents<Image>())image.raycastTarget=false;
        foreach(var child in root.Cast<Transform>())if(child.name=="Image")child.gameObject.SetActive(false);
    }
}
