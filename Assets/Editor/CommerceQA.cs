using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

internal static class CommerceQA
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static List<string> results=new List<string>();
    static void Check(bool condition,string name) { if(!condition) throw new Exception(name); results.Add("PASS "+name); }
    static void Set(object target,string name,object value) => target.GetType().GetField(name,Flags).SetValue(target,value);
    static object Call(object target,string name,params object[] args) => target.GetType().GetMethod(name,Flags).Invoke(target,args);
    [MenuItem("Tools/Janshin/Validate Commerce")]
    public static void Run() {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        results.Clear();
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/MenuScene.unity");
        var menu=EditorSceneManager.OpenPreviewScene("Assets/Scenes/MenuScene.unity");
        var singleton=typeof(LocalizationManager).GetField("_instance",BindingFlags.Static|BindingFlags.NonPublic);
        var old=singleton.GetValue(null);
        var iapSingleton=typeof(IAPManager).GetField("<Instance>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);
        var oldIap=iapSingleton.GetValue(null);
        Camera camera=null; RenderTexture texture=null;
        try {
            var wallet=new GemWallet.State {balance=7};
            Check(wallet.Grant("tx-a",10)&&wallet.balance==17,"First purchase grants exact amount");
            wallet=JsonUtility.FromJson<GemWallet.State>(JsonUtility.ToJson(wallet));
            Check(!wallet.Grant("tx-a",10)&&wallet.balance==17,"Replayed purchase after reload does not grant twice");
            Check(wallet.Grant("tx-b",55)&&wallet.balance==72,"A new transaction for a consumable is granted");
            bool invalid=false; try { wallet.Grant("",10); } catch(ArgumentException) { invalid=true; }
            Check(invalid&&wallet.balance==72,"Missing transaction ID does not grant");
            var iap=menu.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<IAPManager>(true)).Single();
            iapSingleton.SetValue(null,iap);
            Check(iap.Gems100Amount==10&&iap.Gems500Amount==55&&iap.Gems1200Amount==130,"Authored pack amounts preserved");
            Check(iap.GetLocalizedPrice(iap.RemoveAdsProductId)=="800円" && iap.GetLocalizedPrice(iap.Gems100ProductId)=="100円" && iap.GetLocalizedPrice(iap.Gems500ProductId)=="400円" && iap.GetLocalizedPrice(iap.Gems1200ProductId)=="900円","Requested Japanese prices");
            Check(!iap.HasProductionIds,"Unregistered IDs are identified");
            Check(!iap.CanBuy(iap.Gems100ProductId),"Purchasing disabled before store is ready");
            iap.OnPurchaseFailed(null,UnityEngine.Purchasing.PurchaseFailureReason.UserCancelled);
            Check(!iap.IsBusy&&iap.Status=="cancelled","Cancellation releases purchase lock and provides status");
            int gemsBefore=SpecialTileSystem.GetGems();
            var reward=menu.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RewardedAdManager>(true)).Single();
            int callbacks=0; bool received=true;
            Set(reward,"showing",true); Set(reward,"earned",false);
            Set(reward,"completion",(Action<bool>)(success=>{callbacks++;received=success;}));
            Call(reward,"Finish"); Call(reward,"Finish");
            Check(callbacks==1&&!received,"Rewardless close completes exactly once");
            Check(SpecialTileSystem.GetGems()==gemsBefore,"Closing without a reward preserves gem balance");
            var interstitial=menu.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<InterstitialAdManager>(true)).Single();
            int navigations=0;
            Set(interstitial,"showing",true); Set(interstitial,"completion",(Action)(()=>navigations++));
            interstitial.ShowAdIfReady(()=>navigations+=100);
            Call(interstitial,"Finish"); Call(interstitial,"Finish");
            Check(navigations==1,"Repeated interstitial clicks and close callbacks do not duplicate navigation");
            var localGO=new GameObject("Commerce QA Localization"); SceneManager.MoveGameObjectToScene(localGO,scene);
            var local=localGO.AddComponent<LocalizationManager>(); singleton.SetValue(null,local);
            Set(local,"englishFonts",new LocalizationManager.FontSet {bodyFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset")});
            var panel=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<IAPShopPanel>(true)).Single();
            Call(panel,"Awake");
            panel.OpenShop(); Check(panel.PanelRoot.activeSelf,"Shop opens");
            panel.CloseShop(); Check(!panel.PanelRoot.activeSelf,"Shop closes");
            panel.OpenShop();
            var cameraGO=new GameObject("Commerce QA Camera"); SceneManager.MoveGameObjectToScene(cameraGO,scene);
            camera=cameraGO.AddComponent<Camera>(); camera.scene=scene;
            camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.1f,.13f,.17f); camera.cullingMask=1<<31;
            foreach(var root in scene.GetRootGameObjects()) foreach(var tr in root.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer=31;
            foreach(var canvas in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Canvas>(true))) {
                canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=10;
            }
            foreach(var language in new[]{LocalizationManager.Language.Japanese,LocalizationManager.Language.English,LocalizationManager.Language.ChineseSimplified}) {
                Set(local,"currentLanguage",language);
                foreach(var size in new[]{new Vector2Int(1600,900),new Vector2Int(1280,720),new Vector2Int(2532,1170)}) {
                    texture=new RenderTexture(size.x,size.y,24); camera.targetTexture=texture;
                    foreach(string state in new[]{"configuration","connecting","purchasing","cancelled","failed","restoreEmpty","restoring","restored","deferred","pending","testStore"}) {
                        Set(iap,"<Status>k__BackingField",state); panel.RefreshUI(); Canvas.ForceUpdateCanvases();
                        foreach(var text in panel.PanelRoot.GetComponentsInChildren<TMP_Text>()) {
                            text.ForceMeshUpdate(true);
                            Check(!text.isTextOverflowing,language+" "+size+" "+state+" "+text.name+" fits");
                        }
                    }
                    if(size.x==1600) {
                        Set(iap,"<Status>k__BackingField","configuration"); panel.RefreshUI(); Canvas.ForceUpdateCanvases(); camera.Render();
                        var previous=RenderTexture.active; RenderTexture.active=texture;
                        var png=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                        png.ReadPixels(new Rect(0,0,size.x,size.y),0,0); png.Apply();
                        File.WriteAllBytes("Temp/TutorialQA/Shop-"+language+".png",png.EncodeToPNG());
                        Object.DestroyImmediate(png); RenderTexture.active=previous;
                    }
                    camera.targetTexture=null; Object.DestroyImmediate(texture); texture=null;
                }
            }
            results.Add("SUCCESS");
        } catch(Exception e) { results.Add("FAIL "+e); Debug.LogException(e); }
        finally {
            if(camera) camera.targetTexture=null; if(texture) Object.DestroyImmediate(texture);
            singleton.SetValue(null,old); iapSingleton.SetValue(null,oldIap);
            EditorSceneManager.ClosePreviewScene(scene); EditorSceneManager.ClosePreviewScene(menu);
            File.WriteAllLines("Temp/TutorialQA/commerce-results.txt",results);
        }
    }
}
