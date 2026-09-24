
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
[InitializeOnLoad]
public sealed class GaidenStrikeSettingsSync : IPreprocessBuildWithReport {
 public int callbackOrder=>0;
 static GaidenStrikeSettingsSync(){EditorApplication.delayCall+=Sync;EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode)Sync();};}
 public void OnPreprocessBuild(BuildReport report){Sync();}
 public static void Sync(){
  var settings=AssetDatabase.LoadAssetAtPath<GaidenUISettings>("Assets/Resources/SeventeenSteps/GaidenUISettings.asset");
  const string path="Assets/Scenes/RunScene.unity";if(!settings||!File.Exists(path))return;
  string scene=File.ReadAllText(path);var sound=Regex.Match(scene,@"winStrikeSound:.*guid: ([a-f0-9]+)");
  AudioClip clip=sound.Success?AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(sound.Groups[1].Value)):null;
  var duration=Regex.Match(scene,@"winStrikeDuration: ([\d.]+)");float seconds=duration.Success?float.Parse(duration.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture):1.05f;
  if(settings.normalStrikeSound==clip&&Mathf.Approximately(settings.normalStrikeDuration,seconds))return;
  settings.normalStrikeSound=clip;settings.normalStrikeDuration=seconds;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
 }
}
