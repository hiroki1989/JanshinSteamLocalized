using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
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

        // xcpretty を無効化するためにGemfileを修正
        string gemfile = Path.Combine(buildPath, "Gemfile");
        if (File.Exists(gemfile))
        {
            string content = File.ReadAllText(gemfile);
            content = content.Replace("gem 'xcpretty'", "# gem 'xcpretty'");
            File.WriteAllText(gemfile, content);
            UnityEngine.Debug.Log("[ForceCocoaPodsInstall] Disabled xcpretty");
        }
    }
}