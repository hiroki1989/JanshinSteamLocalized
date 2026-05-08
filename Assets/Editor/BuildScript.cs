using UnityEditor;
using System.Linq;

public class BuildScript
{
    public static void BuildIOS()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "ios_build",
            target = BuildTarget.iOS,
            options = BuildOptions.None
        };

        BuildPipeline.BuildPlayer(options);
    }
}