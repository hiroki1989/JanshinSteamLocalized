using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

internal static class GameplayRevisionQA {
    const string Request="Temp/TutorialQA/gameplay-revision-request";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly List<string> Results=new List<string>();
    static object Get(object o,string field)=>o.GetType().GetField(field,Flags).GetValue(o);
    static void Set(object o,string field,object value)=>o.GetType().GetField(field,Flags).SetValue(o,value);
    static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,Flags).Invoke(o,args);
    static void Check(bool value,string message) { if(!value) throw new Exception(message); Results.Add("PASS "+message); }
    static T Component<T>(Scene scene) where T:Component=>scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).First();
    [InitializeOnLoadMethod] static void Schedule() { if(File.Exists(Request)) EditorApplication.delayCall+=Run; }
    [MenuItem("Tools/Janshin/Apply and Validate Gameplay Revision")]
    static void Run() {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating) { EditorApplication.delayCall+=Run; return; }
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request); Results.Clear();
        var previous=SceneManager.GetActiveScene(); var opened=new List<Scene>();
        try {
            foreach(string path in new[]{"Assets/Scenes/MenuScene.unity","Assets/Scenes/UpgradeScene.unity"}) {
                var scene=SceneManager.GetSceneByPath(path);
                if(scene.IsValid()&&scene.isLoaded&&scene.isDirty) throw new Exception("Unsaved scene: "+path);
            }
            foreach(string path in new[]{"Assets/Scenes/MenuScene.unity","Assets/Scenes/UpgradeScene.unity"}) {
                var scene=SceneManager.GetSceneByPath(path);
                if(!scene.IsValid()||!scene.isLoaded) { scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive); opened.Add(scene); }
                if(path.Contains("MenuScene")) {
                    var wiring=Component<MenuSceneWiring>(scene);
                    foreach(string field in new[]{"startButton","otherButton"}) {
                        var button=(Button)Get(wiring,field);
                        for(int i=button.onClick.GetPersistentEventCount()-1;i>=0;i--) {
                            var method=button.onClick.GetPersistentMethodName(i);
                            if(method.StartsWith("GoFromMenu") || method.StartsWith("OnClickStart") || method=="OnClickOpenOtherScene")
                                UnityEventTools.RemovePersistentListener(button.onClick,i);
                        }
                        Check(button.onClick.GetPersistentEventCount()==0,"Navigation button has no competing serialized scene transitions: "+field);
                    }
                } else {
                    var upgrade=Component<UpgradeManager>(scene); upgrade.BuildSelectedTileShopUI();
                    var root=(GameObject)Get(upgrade,"selectedTileShopRoot");
                    root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
                    var rect=(RectTransform)root.transform; rect.localScale=Vector3.one; rect.sizeDelta=new Vector2(1600,900);
                    root.SetActive(false);
                    Check(Get(upgrade,"chooseBuyButton")!=null && Get(upgrade,"chooseDestroyButton")!=null,"Editable tile purchase/removal entries created");
                    EditorUtility.SetDirty(upgrade);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save "+path);
            }
            Results.Add("SCENE SETUP SUCCESS");
        } catch(Exception e) { Results.Add("FAIL "+e); Debug.LogException(e); }
        finally {
            if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous);
            foreach(var scene in opened) EditorSceneManager.CloseScene(scene,true);
        }
        if(!Results.Any(s=>s.StartsWith("FAIL"))) TestGameplay();
        File.WriteAllLines("Temp/TutorialQA/gameplay-revision-results.txt",Results);
        if(!Results.Any(s=>s.StartsWith("FAIL"))) {
            typeof(FirstMatchTutorialQA).GetMethod("Run",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        }
    }
    static void TestGameplay() {
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/RunScene.unity");
        IEnumerator draw=null;
        try {
            var counts=Enumerable.Repeat(3,34).ToArray();
            bool Eligible(int index,bool remove,int gold,int minimum=80) => (bool)typeof(UpgradeManager).GetMethod("CanModifySelectedTile",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{index,remove,900,gold,counts,minimum});
            Check(Eligible(0,false,900)&&!Eligible(0,false,899)&&!Eligible(-1,false,900),"Selected purchase validates exact price and a selected tile");
            Check(Eligible(0,true,900)&&!Eligible(0,true,900,102),"Selected removal protects the minimum deck size");
            counts[0]=0; Check(!Eligible(0,true,900),"An unowned tile cannot be removed");
            var snapshotType=typeof(GameManager).GetNestedType("SuspendSnapshot",BindingFlags.NonPublic);
            var snapshot=Activator.CreateInstance(snapshotType,true);
            snapshotType.GetField("consumedEnemyDiscards").SetValue(snapshot,new List<int>{0,3});
            var loaded=JsonUtility.FromJson(JsonUtility.ToJson(snapshot),snapshotType);
            Check(((List<int>)snapshotType.GetField("consumedEnemyDiscards").GetValue(loaded)).SequenceEqual(new[]{0,3}),"Consumed discard positions survive snapshot serialization");
            var gm=Component<GameManager>(scene);
            var phase=gm.GetType().GetField("phase",Flags).FieldType;
            var mode=gm.GetType().GetField("callMode",Flags).FieldType;
            Set(gm,"_skillSet",Get(gm,"fallbackSkillSet"));
            var hand=(List<string>)Get(gm,"hand"); hand.Clear(); hand.AddRange(new[]{"Man3","Man3","Pin1","Pin2","Pin3","Sou1","Sou2","Sou3","East","East","East","White","White"});
            var river=(List<string>)Get(gm,"enemyDiscards"); river.Clear(); river.AddRange(new[]{"Man3","Man3","Pin2"});
            var turn=(List<string>)Get(gm,"lastEnemyTurnTiles"); turn.Clear(); turn.AddRange(river);
            var used=(HashSet<int>)Get(gm,"enemyUsedIndices"); used.Clear(); used.Add(0);
            var available=((IEnumerable<string>)Call(gm,"AvailableEnemyTurnTiles")).ToArray();
            Check(available.SequenceEqual(new[]{"Man3","Pin2"}),"Consumed discard is excluded by instance while an identical unconsumed tile remains available");
            used.Clear();
            Set(gm,"_enemySkillParalysisTurnRemaining",3);
            Set(gm,"phase",Enum.Parse(phase,"ChoosingCall")); Set(gm,"callMode",Enum.Parse(mode,"Pon"));
            Set(gm,"callBaseTile","Man3"); Set(gm,"selectedEnemyIndex",0);
            var selected=(HashSet<int>)Get(gm,"selHand"); selected.Clear(); selected.Add(0); selected.Add(1);
            var melds=(List<List<string>>)Get(gm,"melds"); melds.Clear();
            Call(gm,"ConfirmCall");
            Check(melds.Count==1 && melds[0].Count==3 && used.Contains(0),"Pon succeeds during skill paralysis and consumes the exact discard");
            Check(Get(gm,"phase").ToString()=="NeedDiscardAfterCall","Pon requires one discard");
            var deck=(Stack<string>)Get(gm,"deck"); deck.Clear(); deck.Push("Man9");
            int count=hand.Count;
            draw=(IEnumerator)Call(gm,"__RinshanToHandFlow",0f,0f,Enum.Parse(phase,"NeedDiscardAfterCall"),"");
            while(draw.MoveNext()) {}
            Check(hand.Count==count+1 && Get(gm,"phase").ToString()=="NeedDiscardAfterCall" && !(bool)Get(gm,"_rinshanDrawRunning"),"Supplementary draw returns to the pending discard and releases its input lock");
            Results.Add("GAMEPLAY SUCCESS");
        } catch(Exception e) { Results.Add("FAIL "+e); Debug.LogException(e); }
        finally { if(draw is IDisposable d) d.Dispose(); EditorSceneManager.ClosePreviewScene(scene); }
        RenderShop();
    }
    static void RenderShop() {
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/UpgradeScene.unity");
        Camera camera=null; RenderTexture target=null;
        try {
            var upgrade=Component<UpgradeManager>(scene); upgrade.BuildSelectedTileShopUI();
            var root=(GameObject)Get(upgrade,"selectedTileShopRoot");
            var cameraGo=new GameObject("Tile Shop QA Camera"); SceneManager.MoveGameObjectToScene(cameraGo,scene);
            camera=cameraGo.AddComponent<Camera>(); camera.scene=scene; camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black; camera.cullingMask=1<<31;
            foreach(var tr in root.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer=31;
            foreach(bool destroy in new[]{false,true}) {
                Call(upgrade,"OpenSelectedTileShop",destroy);
                var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=10;
                target=new RenderTexture(1600,900,24); camera.targetTexture=target; Canvas.ForceUpdateCanvases();
                foreach(var text in root.GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Check(!text.isTextOverflowing,"Tile shop text fits: "+text.name); }
                Check(!(bool)Call(upgrade,"CanPurchaseSelectedTile"),"No purchase can occur before a tile is selected");
                camera.Render(); var old=RenderTexture.active; RenderTexture.active=target;
                var png=new Texture2D(1600,900,TextureFormat.RGB24,false); png.ReadPixels(new Rect(0,0,1600,900),0,0); png.Apply();
                File.WriteAllBytes("Temp/TutorialQA/SelectedTile-"+(destroy?"Remove":"Buy")+".png",png.EncodeToPNG());
                Object.DestroyImmediate(png); RenderTexture.active=old; camera.targetTexture=null; Object.DestroyImmediate(target); target=null;
            }
            Results.Add("TILE SHOP UI SUCCESS");
        } catch(Exception e) { Results.Add("FAIL "+e); Debug.LogException(e); }
        finally { if(camera) camera.targetTexture=null; if(target) Object.DestroyImmediate(target); EditorSceneManager.ClosePreviewScene(scene); }
    }
}
