using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WebGLBuildAutomation
{
    public static void Build()
    {
        PlayerSettings.productName = "민첩한 하루 되세요";
        PlayerSettings.WebGL.template = "PROJECT:AgileDay";
        PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);

        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
            throw new System.Exception("No enabled scenes are configured in Build Settings.");

        string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/WebGL"));
        Directory.CreateDirectory(outputPath);
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("WEBGL_BUILD_RESULT=" + report.summary.result + "; OUTPUT=" + outputPath);
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}



