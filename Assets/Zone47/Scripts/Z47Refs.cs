using System;
using UnityEngine;

namespace Zone47
{
    public enum WeaponKind { Rifle, Sniper, Pistol, Shotgun }

    [Serializable]
    public class WeaponDef
    {
        public string id;
        public string displayName;
        public WeaponKind kind;
        public GameObject prefab;          // root at grip, +Z = muzzle, +Y = up
        public Vector3 forePoint;          // left hand
        public Vector3 muzzlePoint;
        public float topY;                 // highest point above grip (sight line)
        public float adsZ;                 // optional: weapon z while aiming (optics)
        public bool animated;              // has its own arms + legacy Animation clips (fire/reload/reload_empty/idle/hold/draw/inspect)
        public float damage = 25f;
        public float fireInterval = 0.08f;
        public bool automatic = true;
        public int magSize = 30;
        public int reserve = 120;
        public float reloadTime = 2.4f;
        public float hipSpread = 2.2f;     // degrees
        public float adsSpread = 0.35f;
        public float recoil = 1.1f;        // degrees per shot
        public float range = 160f;
        public float adsFov = 50f;
        public bool scope;
        public int pellets = 1;            // shotgun: several pellets per shot
        public float falloffStart = 30f, falloffEnd = 120f, falloffMin = 0.7f;
        public AudioClip shot, reloadClip, pumpClip;
        public Vector3 hipOffset = new Vector3(0.13f, -0.14f, 0.26f);
    }

    [Serializable]
    public class StepSet { public string name; public AudioClip[] walk, run, land; }

    public class Z47Refs : ScriptableObject
    {
        [Header("Characters")]
        public GameObject swatModel;
        public GameObject gasModel;
        public RuntimeAnimatorController bodyController;
        public RuntimeAnimatorController armsController;
        public GameObject armsPrefab;

        [Header("Weapons")]
        public WeaponDef[] weapons;
        public GameObject grenadePrefab;

        [Header("World")]
        public GameObject mapPrefab;
        public GameObject lobbyPrefab;
        public GameObject portPrefab, cityPrefab;
        public Texture2D[] mapThumbs;
        public Material lobbySkyMat, rainMat;
        public UnityEngine.Video.VideoClip lobbyVideo;      // looping lobby / loading background
        public GameObject dronePrefab, heliPrefab, bombPrefab;
        public Texture2D bombTex;
        public Texture2D[] killIcons; public string[] killIconIds;     // kill feed weapon silhouettes
        public Texture2D KillIcon(string id)
        {
            if (id == null || killIcons == null || killIconIds == null) return null;
            if (id == "m4holo") id = "m4";
            for (int i = 0; i < killIconIds.Length && i < killIcons.Length; i++) if (killIconIds[i] == id) return killIcons[i];
            return null;
        }          // optional: air strike UAV, intro helicopter
        public Material skybox;
        public Material crateMat, concreteMat, sandbagMat, barrelMat;

        [Header("FX")]
        public Material flashMat, smokeMat, sparkMat, holeMat, tracerMat, fireMat;

        [Header("UI")]
        public Font headFont;
        public Font bodyFont;
        public Texture2D logo, lobbyLogo;
        public Texture2D icFire, icAim, icReload, icGrenade, icCrouch, icJump, icHeadshot, icPause;

        [Header("Audio")]
        public AudioClip reload, dryFire, explosion, impact, flyby, pin, casing, bodyFall, awpBolt, breath, heartbeat, hurt, headshot, equip, bounce, rain, siren, droneLoop, bombWhistle, heliLoop;
        public AudioClip[] steps;
        public StepSet[] stepSets;     // Sand, Rock, Metal, Wood, Tile, Gravel, Dirt (Nox Sound, CC0)
        public AudioClip ambience, menuMusic;

        static Z47Refs _i;
        public static Z47Refs I
        {
            get
            {
                if (_i == null) _i = Resources.Load<Z47Refs>("Z47Refs");
                return _i;
            }
        }

        public bool HasWeapon(string id) { if (weapons != null) foreach (var w in weapons) if (w.id == id && w.prefab != null) return true; return false; }

        public WeaponDef Weapon(string id)
        {
            foreach (var w in weapons) if (w.id == id) return w;
            return weapons[0];
        }
    }
}
