using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Metropolis.GameEditor
{
    public static class BuildScript
    {
        const string Scene = "Assets/_Project/Scenes/GameWorld.unity";
        const string Output = "Builds/Windows/Metropolis.exe";

        [MenuItem("Metropolis/Build Windows")]
        public static void BuildWindows()
        {
            PlayerSettings.companyName = "Metropolis";
            PlayerSettings.productName = "Metropolis";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = Output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            });

            Debug.Log("BuildScript: " + report.summary.result + ", " + report.summary.totalErrors + " errors, "
                + report.summary.totalSize / (1024 * 1024) + " MB.");

            if (report.summary.result != BuildResult.Succeeded && Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
