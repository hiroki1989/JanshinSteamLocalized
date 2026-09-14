using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using System.IO;

public class LocalizationPostBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 50;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS) return;

        string plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        // 対応言語を追加
        PlistElementArray localizations = plist.root.CreateArray("CFBundleLocalizations");
        localizations.AddString("ja"); // 日本語
        localizations.AddString("en"); // 英語
        localizations.AddString("zh-Hans"); // 簡体字中国語

        plist.WriteToFile(plistPath);
    }
}