using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public static class SilentButtonSoundQA {
 public static void Run(){
  var previous=AudioManager.Instance;var host=new GameObject("SoundCoverageTest");var audio=host.AddComponent<AudioManager>();
  var source=host.AddComponent<AudioSource>();var clip=AudioClip.Create("TestClick",32,1,44100,false);
  var field=BindingFlags.Instance|BindingFlags.NonPublic;
  typeof(AudioManager).GetField("seSource",field).SetValue(audio,source);typeof(AudioManager).GetField("seClick",field).SetValue(audio,clip);
  typeof(AudioManager).GetProperty("Instance").SetValue(null,audio);
  try{
   var control=new GameObject("TestButton",typeof(RectTransform),typeof(Button),typeof(SilentButtonSound));control.transform.SetParent(host.transform);
   var button=control.GetComponent<Button>();var sound=control.GetComponent<SilentButtonSound>();
   var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
   var late=typeof(SilentButtonSound).GetMethod("LateUpdate",field);
   var time=typeof(AudioManager).GetField("<LastUserSoundTime>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);
   void Test(int kind,int expected){
    button.interactable=true;control.SetActive(true);time.SetValue(null,-1f);int before=AudioManager.UserSoundSequence;
    if(kind==3)button.interactable=false;sound.OnPointerDown(data);
    if(kind==1)audio.PlaySE_Click();if(kind==2)button.interactable=false;
    sound.OnPointerClick(data);late.Invoke(sound,null);
    if(AudioManager.UserSoundSequence-before!=expected)throw new Exception("Sound coverage kind="+kind);
   }
   Test(0,1);Test(1,1);Test(2,1);Test(3,0);
  }finally{typeof(AudioManager).GetProperty("Instance").SetValue(null,previous);UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(clip);}
 }
}
