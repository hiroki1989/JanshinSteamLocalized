#if UNITY_IOS
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEngine;

// EDM4U owns CocoaPods installation. This hook only configures and validates it.
public class ForceCocoaPodsInstall
{
    [PostProcessBuild(0)]
    public static void ConfigureResolver(BuildTarget target, string outputPath)
    {
        if (target != BuildTarget.iOS) return;
        Google.IOSResolver.SwiftPackageManagerEnabled = false;
        Google.IOSResolver.PodfileGenerationEnabled = true;
        Google.IOSResolver.CocoapodsIntegrationMethodPref =
            Google.IOSResolver.CocoapodsIntegrationMethod.Workspace;
        Google.IOSResolver.SkipPodInstallWhenUsingWorkspaceIntegration = false;
    }

    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string outputPath)
    {
        if (target != BuildTarget.iOS) return;
        if (Application.platform != RuntimePlatform.OSXEditor) {
            Debug.Log("[iOS Dependencies] Xcode export only: run CocoaPods on the Mac builder.");
            return;
        }
        ValidateInstallation(outputPath);
        Debug.Log("[iOS Dependencies] CocoaPods workspace and installed dependencies verified.");
    }

    public static void ValidateInstallation(string outputPath)
    {
        string podfile = Path.Combine(outputPath, "Podfile");
        string lockfile = Path.Combine(outputPath, "Podfile.lock");
        string manifest = Path.Combine(outputPath, "Pods", "Manifest.lock");
        string workspace = Path.Combine(outputPath, "Unity-iPhone.xcworkspace", "contents.xcworkspacedata");
        foreach (string file in new[] { podfile, lockfile, manifest, workspace }) {
            if (!File.Exists(file))
                throw new BuildFailedException("[iOS Dependencies] Missing " + file +
                    ". EDM4U CocoaPods installation did not complete. Check the earlier resolver log.");
        }
        string installed = File.ReadAllText(lockfile);
        if (installed.Replace("\r\n", "\n") != File.ReadAllText(manifest).Replace("\r\n", "\n"))
            throw new BuildFailedException("[iOS Dependencies] Podfile.lock and Pods/Manifest.lock differ. Run a Clean Build.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(installed,
                @"(?m)^  - Google-Mobile-Ads-SDK \(13\.9\.\d+\)") ||
            !System.Text.RegularExpressions.Regex.IsMatch(installed,
                @"(?m)^  - GoogleUserMessagingPlatform \(3\.1\.0\)"))
            throw new BuildFailedException("[iOS Dependencies] Expected GMA iOS SDK 13.9.x and UMP 3.1.0 for plugin 11.5.0. Check dependency resolution.");
        if (!File.ReadAllText(workspace).Contains("Pods/Pods.xcodeproj"))
            throw new BuildFailedException("[iOS Dependencies] Workspace does not reference the CocoaPods project.");
    }
}
#endif
