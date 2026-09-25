using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public sealed class ShopRerollButton : MonoBehaviour {
    Button button; Func<bool> reroll;
    public static void Ensure(Transform parent,Vector2 position,Func<bool> action){
        if(parent.Find("ShopReroll"))return;
        var button=SeventeenStepsUI.Button(parent,"",position,new Vector2(350,68),null);button.name="ShopReroll";
        var label=button.GetComponentInChildren<TMP_Text>();label.rectTransform.anchoredPosition=new Vector2(-50,0);label.rectTransform.sizeDelta=new Vector2(205,58);
        label.text=ConsumableWindow.T("リロール","Reroll","刷新");label.color=Color.black;
        UpgradePanelPresentation.Bag(button.transform,new Vector2(62,0));
        var price=SeventeenStepsUI.Label(button.transform,"100",new Vector2(123,0),new Vector2(78,58),30);price.color=Color.black;
        var script=button.gameObject.AddComponent<ShopRerollButton>();script.button=button;script.reroll=action;
        button.onClick.AddListener(()=>{if(script.reroll())AudioManager.Instance?.PlayClickSE();});
    }
    void Update(){button.GetComponentInChildren<TMP_Text>().text=ConsumableWindow.T("リロール","Reroll","刷新");button.interactable=GameManager.RunCurrency.Get()>=100;}
}
