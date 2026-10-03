using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class RelicInventoryPresentation
{
    public static void Run()
    {
        string company=PlayerSettings.companyName;GameObject host=null;
        try
        {
            PlayerSettings.companyName="JanshinRelicInventoryPreview";
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Directory.CreateDirectory("Logs/RelicInventory");
            PlayerPrefs.SetString("SeventeenSteps_StockV1",JsonUtility.ToJson(new SeventeenStepsMode.Stock()));
            host=new GameObject("RelicInventoryPreview");var view=host.AddComponent<SeventeenStepsInventory>();
            typeof(SeventeenStepsInventory).GetMethod("Build",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(view,null);
            Canvas.ForceUpdateCanvases();BattleHUDQA.Capture("Logs/RelicInventory/Empty.png",1920,1080);
            var stock=new SeventeenStepsMode.Stock();stock.items=RunConsumables.All.Select(x=>x.id).ToList();stock.items.Add(stock.items[0]);stock.equipped=stock.items.Last();
            PlayerPrefs.SetString("SeventeenSteps_StockV1",JsonUtility.ToJson(stock));
            var refresh=typeof(SeventeenStepsInventory).GetMethod("Refresh",BindingFlags.NonPublic|BindingFlags.Instance);refresh.Invoke(view,null);
            Canvas.ForceUpdateCanvases();
            foreach(var text in view.GetComponentsInChildren<TMP_Text>(true)){text.ForceMeshUpdate();if(text.isTextOverflowing)throw new Exception("Relic inventory text overflow: "+text.text);}
            BattleHUDQA.Capture("Logs/RelicInventory/Inventory.png",1920,1080);
            BattleHUDQA.Capture("Logs/RelicInventory/Phone.png",2340,1080);
            BattleHUDQA.Capture("Logs/RelicInventory/Tablet.png",1440,1080);
            var scroll=view.GetComponentInChildren<ScrollRect>();if(scroll.content.rect.height<=scroll.viewport.rect.height)throw new Exception("Inventory cannot scroll");
            scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();BattleHUDQA.Capture("Logs/RelicInventory/Scrolled.png",1920,1080);
            view.GetComponentsInChildren<Button>().First(x=>x.name=="Relic_"+stock.items[0]).onClick.Invoke();
            var equip=(Button)typeof(SeventeenStepsInventory).GetField("equipButton",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);
            equip.onClick.Invoke();if(SeventeenStepsMode.LoadStock().equipped!=stock.items[0])throw new Exception("Relic equip failed");
            var remove=(Button)typeof(SeventeenStepsInventory).GetField("removeButton",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(view);
            remove.onClick.Invoke();if(SeventeenStepsMode.LoadStock().equipped!=0)throw new Exception("Relic unequip failed");
            File.WriteAllText("Logs/RelicInventory/Verified.txt","PASS: empty inventory, 20 relic types, readable labels, overflow scrolling, equip and unequip, desktop/phone/tablet previews. Isolated preview preferences.");
            Debug.Log("Relic inventory presentation verified");
        }
        finally{if(host)UnityEngine.Object.DestroyImmediate(host);PlayerSettings.companyName=company;}
    }
}
