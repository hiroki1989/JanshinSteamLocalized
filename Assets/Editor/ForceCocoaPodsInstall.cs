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

        // Pods を削除して強制再インストール
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

        // Xcodeプロジェクトに不足しているフレームワークを追加
        string projPath = PBXProject.GetPBXProjectPath(buildPath);
        PBXProject proj = new PBXProject();
        proj.ReadFromFile(projPath);

        string frameworkGuid = proj.GetUnityFrameworkTargetGuid();

        // Unity Ads / Purchasing が必要とするフレームワーク
        proj.AddFrameworkToProject(frameworkGuid, "AdSupport.framework", false);
        proj.AddFrameworkToProject(frameworkGuid, "CoreTelephony.framework", false);
        proj.AddFrameworkToProject(frameworkGuid, "StoreKit.framework", false);
        proj.AddFrameworkToProject(frameworkGuid, "GameController.framework", false);
        proj.AddFrameworkToProject(frameworkGuid, "WebKit.framework", false);
        proj.AddFrameworkToProject(frameworkGuid, "CFNetwork.framework", false);
        proj.AddFrameworkToProject(frameworkGuid, "SystemConfiguration.framework", false);
        proj.AddFrameworkToProject(frameworkGuid, "CoreServices.framework", false);

        // ビルド設定
        proj.SetBuildProperty(frameworkGuid, "CLANG_ENABLE_MODULES", "YES");
        proj.SetBuildProperty(frameworkGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
        proj.AddBuildProperty(frameworkGuid, "OTHER_LDFLAGS", "-ObjC");

        proj.WriteToFile(projPath);

        UnityEngine.Debug.Log("[ForceCocoaPodsInstall] Added missing frameworks and linker flags");
    }
}