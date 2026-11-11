#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Zone47.EditorTools
{
    /// GitHub Actions entry point: -executeMethod Zone47.EditorTools.Z47CIBuild.BuildIOS
    /// Writes the Xcode project to build/iOS (Z47iOSPost patches it afterwards).
    public static class Z47CIBuild
    {
        public static void BuildIOS()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = "build/iOS", target = BuildTarget.iOS, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
#endif
