#if UNITY_EDITOR && UNITY_IOS
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Zone47.EditorTools
{
    /// Prepares the exported Xcode project for App Store / TestFlight:
    /// export-compliance key, no bitcode, and manual signing taken from CI environment variables.
    public static class Z47iOSPost
    {
        [PostProcessBuild(999)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;

            // Info.plist
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.root.SetString("CFBundleDisplayName", "Zone 47");
            plist.WriteToFile(plistPath);

            // Xcode project
            string projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            string main = proj.GetUnityMainTargetGuid();
            string fw = proj.GetUnityFrameworkTargetGuid();
            foreach (var t in new[] { main, fw }) proj.SetBuildProperty(t, "ENABLE_BITCODE", "NO");

            string team = Environment.GetEnvironmentVariable("IOS_TEAM_ID");
            string profile = Environment.GetEnvironmentVariable("IOS_PROFILE_UUID");
            if (!string.IsNullOrEmpty(team) && !string.IsNullOrEmpty(profile))
            {
                proj.SetBuildProperty(main, "CODE_SIGN_STYLE", "Manual");
                proj.SetBuildProperty(main, "DEVELOPMENT_TEAM", team);
                proj.SetBuildProperty(main, "PROVISIONING_PROFILE_SPECIFIER", profile);
                proj.SetBuildProperty(main, "PROVISIONING_PROFILE", profile);
                proj.SetBuildProperty(main, "CODE_SIGN_IDENTITY", "Apple Distribution");
                proj.SetBuildProperty(main, "CODE_SIGN_IDENTITY[sdk=iphoneos*]", "Apple Distribution");
                proj.SetBuildProperty(fw, "CODE_SIGN_STYLE", "Manual");
                proj.SetBuildProperty(fw, "DEVELOPMENT_TEAM", team);
                proj.SetBuildProperty(fw, "PROVISIONING_PROFILE_SPECIFIER", "");
                proj.SetBuildProperty(fw, "CODE_SIGN_IDENTITY", "");
                proj.SetBuildProperty(fw, "CODE_SIGNING_ALLOWED", "NO");
            }
            proj.WriteToFile(projPath);
        }
    }
}
#endif
