using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using System.IO;

public class ForceCocoaPodsInstall : IPostprocessBuildWithReport
{
    public int callbackOrder => 99;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS) return;

        string buildPath = report.summary.outputPath;

        // 1. Pods を完全に削除
        string podsDir = Path.Combine(buildPath, "Pods");
        string podfileLock = Path.Combine(buildPath, "Podfile.lock");
        if (Directory.Exists(podsDir)) Directory.Delete(podsDir, true);
        if (File.Exists(podfileLock)) File.Delete(podfileLock);

        // 2. Podfile を完全に書き直す
        string podfilePath = Path.Combine(buildPath, "Podfile");
        string newPodfile = @"source 'https://cdn.cocoapods.org/'

platform :ios, '15.0'
use_frameworks!

target 'UnityFramework' do
  pod 'Google-Mobile-Ads-SDK'
  pod 'GoogleUserMessagingPlatform'
  pod 'UnityAds', '~> 4.19'
end

target 'Unity-iPhone' do
end

post_install do |installer|
  installer.pods_project.targets.each do |target|
    target.build_configurations.each do |config|
      config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '15.0'
      config.build_settings['BUILD_LIBRARY_FOR_DISTRIBUTION'] = 'YES'
    end
  end
end
";
        File.WriteAllText(podfilePath, newPodfile);
        UnityEngine.Debug.Log("[ForceCocoaPodsInstall] Podfile rewritten with dynamic linking");

        // 3. Xcode プロジェクト設定
        string projPath = PBXProject.GetPBXProjectPath(buildPath);
        PBXProject proj = new PBXProject();
        proj.ReadFromFile(projPath);

        string fw = proj.GetUnityFrameworkTargetGuid();
        string main = proj.GetUnityMainTargetGuid();

        string[] frameworks = {
            "AdSupport.framework", "CoreTelephony.framework",
            "StoreKit.framework", "GameController.framework",
            "WebKit.framework", "CFNetwork.framework",
            "SystemConfiguration.framework", "CoreServices.framework",
            "AppTrackingTransparency.framework"
        };
        foreach (string f in frameworks)
            proj.AddFrameworkToProject(fw, f, f == "AppTrackingTransparency.framework");

        proj.SetBuildProperty(fw, "CLANG_ENABLE_MODULES", "YES");
        proj.SetBuildProperty(fw, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
        proj.SetBuildProperty(main, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
        proj.AddBuildProperty(fw, "OTHER_LDFLAGS", "-ObjC");
        proj.AddBuildProperty(fw, "OTHER_LDFLAGS", "-lz");
        proj.AddBuildProperty(fw, "OTHER_LDFLAGS", "-lc++");
        proj.SetBuildProperty(fw, "IPHONEOS_DEPLOYMENT_TARGET", "15.0");
        proj.SetBuildProperty(main, "IPHONEOS_DEPLOYMENT_TARGET", "15.0");

        proj.WriteToFile(projPath);
        UnityEngine.Debug.Log("[ForceCocoaPodsInstall] Xcode project configured");
    }
}