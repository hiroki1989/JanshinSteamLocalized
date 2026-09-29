using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BattleHUDQA
{
    const string Key="BattleHUDQA.Active";
    static double start, journeyStart;
    static bool skipped,captured;
    static BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static BattleHUDQA(){EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=Tick;}
    public static void Run()
    {
        SessionState.SetString("BattleHUDQA.Company",PlayerSettings.companyName);
        PlayerSettings.companyName="JanshinBattleHUDQA";
        EditorSceneManager.OpenScene("Assets/Scenes/RunScene.unity");
        var manager=UnityEngine.Object.FindAnyObjectByType<GameManager>();
        var data=new SerializedObject(manager);data.FindProperty("tutorialEnabled").boolValue=false;data.ApplyModifiedPropertiesWithoutUndo();
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){start=EditorApplication.timeSinceStartup;skipped=captured=false;journeyStart=0;}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            PlayerSettings.companyName=SessionState.GetString("BattleHUDQA.Company","OwlGameStudio");
            SessionState.SetBool(Key,false);
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
    }
    static T Field<T>(GameManager gm,string name)=> (T)typeof(GameManager).GetField(name,flags).GetValue(gm);
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||start<=0)return;
        try
        {
            var gm=UnityEngine.Object.FindAnyObjectByType<GameManager>();
            double elapsed=EditorApplication.timeSinceStartup-start;
            if(!skipped&&elapsed>18&&gm)
            {
                skipped=true;var button=Field<Button>(gm,"btnSkip");
                if(button&&button.gameObject.activeInHierarchy&&button.IsInteractable())button.onClick.Invoke();
            }
            if(!captured&&elapsed>30&&gm)
            {
                captured=true;
                Capture("Logs/BattleHUDPlay.png",1920,1080);
                var hud=UnityEngine.Object.FindAnyObjectByType<TrialBattleHUD>();
                var prefab=Field<GameObject>(gm,"tilePrefab");
                var slots=Field<System.Collections.Generic.List<RectTransform>>(gm,"playerTenpaiWaitSlots");
                foreach(var slot in slots)
                {
                    var tile=UnityEngine.Object.Instantiate(prefab,slot);
                    typeof(GameManager).GetMethod("SetTileSprite",flags).Invoke(gm,new object[]{tile,"Man1"});
                    tile.transform.localPosition=Vector3.zero;
                    typeof(GameManager).GetMethod("ApplyPlayerTenpaiWaitTileSizeIfNeeded",flags).Invoke(gm,new object[]{tile});
                }
                hud.waitsRoot.SetActive(true);hud.waitsCaption.gameObject.SetActive(true);
                var riichi=Field<TMPro.TextMeshProUGUI>(gm,"enemyRiichiStatusTMP");riichi.gameObject.SetActive(true);riichi.text="リーチ";
                Canvas.ForceUpdateCanvases();
                Capture("Logs/BattleHUDWaits.png",1920,1080);
                Capture("Logs/BattleHUDTablet.png",1440,1080);
                var waitsRect=(RectTransform)hud.waitsRoot.transform;
                if(slots.Count!=13||waitsRect.localScale!=Vector3.one)throw new Exception("Wait tile layout invalid");
                if(Field<GameObject>(gm,"enemyRiichiStatusBGObject"))throw new Exception("Old riichi backing remains");
                File.WriteAllText("Logs/BattleHUDQA.txt","PASS runtime HUD, wait slots=13, riichi backing removed\n");
                NormalJourney.Play(NormalJourney.Leg.FirstGod,()=>SafeSceneLoader.Load("EnemyDialogue"));
                journeyStart=EditorApplication.timeSinceStartup;
            }
            if(captured&&journeyStart>0&&EditorApplication.timeSinceStartup-journeyStart>12)
            {
                if(SafeSceneLoader.IsLoading||NormalJourney.IsPlaying)return;
                if(SceneManager.GetActiveScene().name!="EnemyDialogue")throw new Exception("Journey did not reach dialogue");
                File.AppendAllText("Logs/BattleHUDQA.txt","PASS map -> EnemyDialogue transition completed; cover and map released\n");
                EditorApplication.isPlaying=false;
            }
            if(elapsed>100)throw new Exception("QA timed out");
        }
        catch(Exception ex){File.WriteAllText("Logs/BattleHUDQA-error.txt",ex.ToString());Debug.LogException(ex);EditorApplication.isPlaying=false;}
    }
    public static void Capture(string path,int width,int height)
    {
        var owner=new GameObject("HUDCaptureCamera");var camera=owner.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;camera.orthographicSize=540;camera.transform.position=new Vector3(0,0,-10);
        var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;
        var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var modes=new RenderMode[canvases.Length];var cameras=new Camera[canvases.Length];var distances=new float[canvases.Length];
        for(int i=0;i<canvases.Length;i++){var c=canvases[i];modes[i]=c.renderMode;cameras[i]=c.worldCamera;distances[i]=c.planeDistance;if(c.isRootCanvas){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=10;}}
        Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;
        for(int i=0;i<canvases.Length;i++){var c=canvases[i];c.renderMode=modes[i];c.worldCamera=cameras[i];c.planeDistance=distances[i];}
        UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(owner);
    }
}
