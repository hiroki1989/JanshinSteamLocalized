using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using System.IO;
using System.Text.RegularExpressions;

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
            Directory.Delete(podsDir, true);
        if (File.Exists(podfileLock))
            File.Delete(podfileLock);

        // 2. Podfile のバージョン固定を解除して最新版を使わせる
        string podfilePath = Path.Combine(buildPath, "Podfile");
        if (File.Exists(podfilePath))
        {
            string content = File.ReadAllText(podfilePath);

            // バージョン指定を削除（例: pod 'UnityAds', '4.19.0' → pod 'UnityAds'）
            content = Regex.Replace(content, @"(pod\s+'[^']+'),\s*'[~>=<!\s]*[\d.]+'", "$1");

            // platform のバージョンを上げる
            content = Regex.Replace(content, @"platform\s*:ios,\s*'[\d.]+'", "platform :ios, '15.0'");

            // post_install フックを追加
            if (!content.Contains("force_xcode26_compat"))
            {
                content += @"

# force_xcode26_compat
post_install do |installer|
  installer.pods_project.targets.each do |target|
    target.build_configurations.each do |config|
      config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '15.0'
      config.build_settings['BUILD_LIBRARY_FOR_DISTRIBUTION'] = 'YES'
      config.build_settings['EXCLUDED_ARCHS[sdk=iphonesimulator*]'] = 'arm64'
    end
  end
end
";
            }

            File.WriteAllText(podfilePath, content);

            // 修正後の Podfile をログに出力（デバッグ用）
            UnityEngine.Debug.Log("[ForceCocoaPodsInstall] Modified Podfile:\n" + content);
        }

        // 3. Xcode プロジェクト設定
        string projPath = PBXProject.GetPBXProjectPath(buildPath);
        PBXProject proj = new PBXProject();
        proj.ReadFromFile(projPath);

        string frameworkGuid = proj.GetUnityFrameworkTargetGuid();
        string mainTargetGuid = proj.GetUnityMainTargetGuid();

        string[] frameworks = {
            "AdSupport.framework", "CoreTelephony.framework",
            "StoreKit.framework", "GameController.framework",
            "WebKit.framework", "CFNetwork.framework",
            "SystemConfiguration.framework", "CoreServices.framework",
            "AppTrackingTransparency.framework"
        };

        foreach (string fw in frameworks)
            proj.AddFrameworkToProject(frameworkGuid, fw, fw == "AppTrackingTransparency.framework");

        proj.SetBuildProperty(frameworkGuid, "CLANG_ENABLE_MODULES", "YES");
        proj.SetBuildProperty(frameworkGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
        proj.SetBuildProperty(mainTargetGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
        proj.AddBuildProperty(frameworkGuid, "OTHER_LDFLAGS", "-ObjC");
        proj.AddBuildProperty(frameworkGuid, "OTHER_LDFLAGS", "-lz");
        proj.AddBuildProperty(frameworkGuid, "OTHER_LDFLAGS", "-lc++");
        proj.SetBuildProperty(frameworkGuid, "IPHONEOS_DEPLOYMENT_TARGET", "15.0");
        proj.SetBuildProperty(mainTargetGuid, "IPHONEOS_DEPLOYMENT_TARGET", "15.0");

        proj.WriteToFile(projPath);

        UnityEngine.Debug.Log("[ForceCocoaPodsInstall] Setup complete");
    }
}