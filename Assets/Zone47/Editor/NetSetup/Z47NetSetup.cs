using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Zone47.EditorTools
{
    /// Online mode setup. Lives in its own editor assembly so it still compiles (and can repair things) even when the game code doesn't.
    ///  - installs Netcode for GameObjects + Multiplayer Services (Relay) once,
    ///  - turns the Z47_NET define on only when those packages are really there (and off again if they disappear).
    [InitializeOnLoad]
    static class Z47NetSetup
    {
        const string Define = "Z47_NET";
        const string FriendsDefine = "Z47_FRIENDS";
        const string OffKey = "z47_net_off";
        static readonly string[] Packages = { "com.unity.netcode.gameobjects", "com.unity.services.multiplayer", "com.unity.services.friends" };
        static AddAndRemoveRequest req;

        static Z47NetSetup() { EditorApplication.delayCall += Check; }

        static bool HasType(string fullName)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { if (a.GetType(fullName, false) != null) return true; } catch { }
            }
            return false;
        }

        static bool FriendsReady => HasType("Unity.Services.Friends.FriendsService");
        static bool PackagesReady => HasType("Unity.Netcode.NetworkManager") && HasType("Unity.Services.Relay.RelayService") && HasType("Unity.Services.Authentication.AuthenticationService");

        static void Check()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Check; return; }
            bool off = EditorPrefs.GetBool(OffKey, false);
            bool ready = PackagesReady;
            SetDefine(Define, ready && !off);
            SetDefine(FriendsDefine, ready && FriendsReady && !off);
            if ((ready && FriendsReady) || off || req != null) return;
            if (SessionState.GetBool("z47_net_tried", false)) return;      // once per editor session
            SessionState.SetBool("z47_net_tried", true);
            Install();
        }

        [MenuItem("Zone47/Online/Paketleri Kur (Install Packages)")]
        static void Install()
        {
            if (req != null) return;
            Debug.Log("[Zone47] Online paketleri kuruluyor: " + string.Join(", ", Packages));
            req = Client.AddAndRemove(Packages, null);
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (req == null || !req.IsCompleted) return;
            EditorApplication.update -= Poll;
            if (req.Status == StatusCode.Success) Debug.Log("[Zone47] Online paketleri kuruldu. Derleme bitince online mod açılır. Sonra: Project Settings > Services > projeyi Unity Cloud'a bağla, Dashboard'da Relay ve Friends servislerini aç.");
            else Debug.LogError("[Zone47] Online paketleri kurulamadı: " + (req.Error != null ? req.Error.message : "?"));
            req = null;
            EditorApplication.delayCall += Check;
        }

        [MenuItem("Zone47/Online/Online Kapat (Disable)")]
        static void Disable() { EditorPrefs.SetBool(OffKey, true); SetDefine(Define, false); SetDefine(FriendsDefine, false); Debug.Log("[Zone47] Online kapatıldı (Z47_NET kaldırıldı)."); }

        [MenuItem("Zone47/Online/Online Aç (Enable)")]
        static void Enable() { EditorPrefs.SetBool(OffKey, false); SessionState.SetBool("z47_net_tried", false); Check(); }

        static void SetDefine(string Define, bool on)
        {
            var targets = new[] { NamedBuildTarget.iOS, NamedBuildTarget.Standalone, NamedBuildTarget.Android };
            foreach (var t in targets)
            {
                string cur = PlayerSettings.GetScriptingDefineSymbols(t) ?? "";
                var list = new List<string>();
                foreach (var d in cur.Split(';')) if (d.Trim().Length > 0) list.Add(d.Trim());
                bool has = list.Contains(Define);
                if (has == on) continue;
                if (on) list.Add(Define); else list.Remove(Define);
                PlayerSettings.SetScriptingDefineSymbols(t, string.Join(";", list));
                Debug.Log("[Zone47] " + Define + (on ? " eklendi" : " kaldırıldı") + " (" + t.TargetName + ")");
            }
        }
    }
}
