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

        // 1. Pods を削除して強制再インストール
        string podsDir = Path.Combine(buildPath, "Pods");
        string podfileLock = Path.Combine(buildPath, "Podfile.lock");

        if (Directory.Exists(podsDir))
        {
            Directory.Delete(podsDir, true);
            UnityEngine.Debug.Log("[ForceCocoaPodsInstall] Deleted Pods directory");
        }
        if (File.Exists(podfileLock))
        {
            File.Delete(podfileLock);
            UnityEngine.Debug.Log("[ForceCocoaPodsInstall] Deleted Podfile.lock");
        }

        // 2. Podfile に post_install フックを追加して deployment target を強制的に上げる
        string podfilePath = Path.Combine(buildPath, "Podfile");
        if (File.Exists(podfilePath))
        {
            string podfileContent = File.ReadAllText(podfilePath);

            // 既存の post_install があれば削除
            if (!podfileContent.Contains("force_deployment_target"))
            {
                podfileContent += @"

# force_deployment_target
post_install do |installer|
  installer.pods_project.targets.each do |target|
    target.build_configurations.each do |config|
      config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '15.0'
      config.build_settings['BUILD_LIBRARY_FOR_DISTRIBUTION'] = 'YES'
    end
  end
  installer.pods_project.build_configurations.each do |config|
    config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '15.0'
  end
end
";
                File.WriteAllText(podfilePath, podfileContent);
                UnityEngine.Debug.Log("[ForceCocoaPodsInstall] Added post_install hook to Podfile");
            }
        }

        // 3. Xcode プロジェクトにフレームワークとリンカフラグを追加
        string projPath = PBXProject.GetPBXProjectPath(buildPath);
        PBXProject proj = new PBXProject();
        proj.ReadFromFile(projPath);

        string frameworkGuid = proj.GetUnityFrameworkTargetGuid();
        string mainTargetGuid = proj.GetUnityMainTargetGuid();

        // 必要なフレームワーク
        string[] frameworks = {
            "AdSupport.framework",
            "CoreTelephony.framework",
            "StoreKit.framework",
            "GameController.framework",
            "WebKit.framework",
            "CFNetwork.framework",
            "SystemConfiguration.framework",
            "CoreServices.framework",
            "AppTrackingTransparency.framework"
        };

        foreach (string fw in frameworks)
        {
            proj.AddFrameworkToProject(frameworkGuid, fw, fw == "AppTrackingTransparency.framework");
        }

        // リンカフラグ
        proj.SetBuildProperty(frameworkGuid, "CLANG_ENABLE_MODULES", "YES");
        proj.SetBuildProperty(frameworkGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
        proj.SetBuildProperty(mainTargetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
        proj.AddBuildProperty(frameworkGuid, "OTHER_LDFLAGS", "-ObjC");
        proj.AddBuildProperty(frameworkGuid, "OTHER_LDFLAGS", "-lz");
        proj.AddBuildProperty(frameworkGuid, "OTHER_LDFLAGS", "-lc++");

        // deployment target を上げる
        proj.SetBuildProperty(frameworkGuid, "IPHONEOS_DEPLOYMENT_TARGET", "15.0");
        proj.SetBuildProperty(mainTargetGuid, "IPHONEOS_DEPLOYMENT_TARGET", "15.0");

        proj.WriteToFile(projPath);

        UnityEngine.Debug.Log("[ForceCocoaPodsInstall] Setup complete - frameworks, flags, and deployment target configured");
    }
}