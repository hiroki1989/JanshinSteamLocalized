using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// The managed GoogleMobileAds DLLs and the CocoaPod are not sufficient:
// the Unity-to-iOS bridge must also be present in the repository and enabled.
public sealed class GoogleMobileAdsNativeBuildGuard : IPreprocessBuildWithReport
{
    const string Framework = "Assets/Plugins/iOS/unity-plugin-library.xcframework";
    public int callbackOrder => -10000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS) return;
        string deviceBinary = Framework + "/ios-arm64/unity-plugin-library.framework/unity-plugin-library";
        if (!File.Exists(Framework + "/Info.plist") || !File.Exists(deviceBinary) || new FileInfo(deviceBinary).Length < 1024)
            throw new BuildFailedException("Google Mobile Ads iOS native bridge is missing. Commit the entire Assets/Plugins/iOS directory, including the XCFramework binary and .meta files. Do not ignore nested iOS directories.");

        var importer = AssetImporter.GetAtPath(Framework) as PluginImporter;
        if (importer == null)
            throw new BuildFailedException("Unity did not import " + Framework + " as a native plugin. Reimport Google Mobile Ads v11.5.0 and include the iOS build support module.");
        if (importer.GetCompatibleWithAnyPlatform() || importer.GetCompatibleWithEditor() || !importer.GetCompatibleWithPlatform(BuildTarget.iOS))
        {
            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(false);
            importer.SetCompatibleWithPlatform(BuildTarget.iOS, true);
            importer.SaveAndReimport();
        }
        Debug.Log("[GMA Native Check] iOS arm64 Unity bridge present and enabled: " + deviceBinary);
    }
}
