using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Zone47
{
    /// App entry: splash → menu → match. Lives in the only scene.
    public class Boot : MonoBehaviour
    {
        public static Boot I;
        GameObject menuRoot, matchRoot;
        Canvas menuCanvas;
        Camera menuCam;
        Light sun;
        Volume volume;

        void Awake()
        {
            I = this;
            gameObject.AddComponent<AudioListener>();
            gameObject.AddComponent<ListenerFollow>();
            // fallback camera so the screen is never "No cameras rendering" (splash / loading)
            var bc = new GameObject("BootCam").AddComponent<Camera>();
            bc.transform.SetParent(transform);
            bc.clearFlags = CameraClearFlags.SolidColor; bc.backgroundColor = Color.black; bc.cullingMask = 0; bc.depth = -100;
            ListenerFollow.bootCam = bc;
            Settings.Apply();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            SetupEnvironment();
        }

        void Start() { StartCoroutine(Splash()); }

        // ------------------------------------------------------------ environment
        void SetupEnvironment()
        {
            var R = Z47Refs.I;
            var sg = new GameObject("Sun");
            DontDestroyOnLoad(sg);
            sun = sg.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.86f, 0.7f);
            sun.intensity = 1.55f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sg.transform.rotation = Quaternion.Euler(32f, -38f, 0f);
            RenderSettings.sun = sun;

            // sky bounce: soft cool fill from the opposite side so shaded walls are never black
            var fg = new GameObject("SkyFill"); DontDestroyOnLoad(fg);
            var fill = fg.AddComponent<Light>();
            fill.type = LightType.Directional; fill.shadows = LightShadows.None;
            fill.color = new Color(0.7f, 0.76f, 0.9f); fill.intensity = 0.12f;
            fg.transform.rotation = Quaternion.Euler(35f, 142f, 0f);

            if (R.skybox != null) RenderSettings.skybox = R.skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.5f, 0.47f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.38f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.16f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.009f;
            RenderSettings.fogColor = new Color(0.62f, 0.52f, 0.43f);

            var vg = new GameObject("PostFX");
            DontDestroyOnLoad(vg);
            volume = vg.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 1;
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            var tm = prof.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var bl = prof.Add<Bloom>(true); bl.intensity.Override(0.55f); bl.threshold.Override(1.05f); bl.scatter.Override(0.6f);
            var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(Settings.ExposureValue); ca.contrast.Override(14f); ca.saturation.Override(-8f);
            Exposure = ca;
            ca.colorFilter.Override(new Color(1f, 0.95f, 0.88f));
            var vi = prof.Add<Vignette>(true); vi.intensity.Override(0.28f); vi.smoothness.Override(0.45f);
            var sm = prof.Add<ShadowsMidtonesHighlights>(true);
            sm.shadows.Override(new Vector4(0.95f, 1f, 1.08f, 0f));
            volume.sharedProfile = prof;
        }

        public static ColorAdjustments Exposure;

        void ApplyCameraFx(Camera c)
        {
            var d = c.GetUniversalAdditionalCameraData();
            d.renderPostProcessing = Settings.PostFx;
            d.antialiasing = Settings.Quality >= 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.FastApproximateAntialiasing;
        }

        // ------------------------------------------------------------ splash
        IEnumerator Splash()
        {
            var c = UIK.Canvas("Splash", 100);
            var bg = UIK.Img(c.transform, "bg", Color.black, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            var cg = bg.gameObject.AddComponent<CanvasGroup>();
            var t1 = UIK.Txt(bg.transform, "ayd", "AYD WORKS", 150, Pal.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1600, 200), true);
            var line = UIK.Img(bg.transform, "line", Pal.Accent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(0, 6));
            var t2 = UIK.Txt(bg.transform, "games", "G  A  M  E  S", 54, Pal.Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(1000, 80), true);
            t1.color = new Color(1, 1, 1, 0); t2.color = new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0);
            float t = 0;
            while (t < 3.2f)
            {
                t += Time.unscaledDeltaTime;
                float a1 = Mathf.Clamp01(t / 0.6f);
                t1.color = new Color(Pal.Text.r, Pal.Text.g, Pal.Text.b, a1);
                t1.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, Mathf.SmoothStep(0, 1, a1));
                float a2 = Mathf.Clamp01((t - 0.5f) / 0.6f);
                line.rectTransform.sizeDelta = new Vector2(Mathf.SmoothStep(0, 760, a2), 6);
                float a3 = Mathf.Clamp01((t - 0.9f) / 0.5f);
                t2.color = new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, a3);
                if (t > 2.6f) cg.alpha = 1f - (t - 2.6f) / 0.6f;
                if (t > 1.6f && menuRoot == null) BuildMenu();
                yield return null;
            }
            Destroy(c.gameObject);
        }

        // ------------------------------------------------------------ menu
        void BuildMenu()
        {
            var R = Z47Refs.I;
            Time.timeScale = 1f; AudioListener.pause = false;
            FX.ClearHoles();
            menuRoot = new GameObject("Menu");
            var map = Instantiate(R.mapPrefab, menuRoot.transform);
            var camGo = new GameObject("MenuCam"); camGo.transform.SetParent(menuRoot.transform);
            menuCam = camGo.AddComponent<Camera>(); menuCam.fieldOfView = 50; menuCam.farClipPlane = 500;
            ApplyCameraFx(menuCam);
            camGo.AddComponent<MenuOrbit>().center = MapCenter(map);

            menuCanvas = UIK.Canvas("MenuCanvas", 20);
            menuCanvas.transform.SetParent(menuRoot.transform);
            var root = UIK.Full(menuCanvas.transform, "root");
            root.gameObject.AddComponent<SafeArea>();
            var shade = UIK.Img(root, "shade", new Color(0, 0, 0, 0.55f), new Vector2(0, 0), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);

            if (R.logo != null)
            {
                var lg = UIK.Rect(root, "logo", new Vector2(0, 1), new Vector2(0, 1), new Vector2(90, -40), new Vector2(400, 400));
                var ri = lg.gameObject.AddComponent<RawImage>(); ri.texture = R.logo; ri.raycastTarget = false;
            }
            UIK.Txt(root, "mode", L.S("tdm"), 30, Pal.Accent, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(95, -452), new Vector2(700, 40), true);

            UIK.Btn(root, L.S("play"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(90, -100), new Vector2(560, 110), StartMatch, 56, true);
            var diffBtn = UIK.Btn(root, DiffLabel(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(90, -225), new Vector2(560, 84), null, 34);
            diffBtn.onClick.AddListener(() => { Settings.Difficulty = (Settings.Difficulty + 1) % 3; diffBtn.GetComponentInChildren<Text>().text = DiffLabel(); });
            UIK.Btn(root, L.S("settings"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(90, -325), new Vector2(560, 84), () => ShowSettings(menuCanvas.transform, RebuildMenu), 34);
            UIK.Btn(root, L.S("credits"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(90, -425), new Vector2(560, 84), ShowCredits, 34);
            UIK.Txt(root, "foot", "AYD WORKS GAMES  ·  v0.1", 22, Pal.Dim, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 30), new Vector2(420, 30), true);

            Sfx.Music(R.menuMusic, 0.7f);
        }

        string DiffLabel() => L.S("difficulty") + ":  " + L.S(Settings.Difficulty == 0 ? "easy" : Settings.Difficulty == 1 ? "normal" : "hard");

        void SpawnPoser(GameObject prefab, Vector3 pos, float yaw)
        {
            var g = Instantiate(prefab, menuRoot.transform);
            g.transform.position = new Vector3(pos.x, Util.GroundY(pos) + 0.01f, pos.z);
            g.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var a = g.GetComponent<Animator>();
            if (a != null) { a.runtimeAnimatorController = Z47Refs.I.bodyController; a.SetBool("Aim", false); }
            var gun = Instantiate(Z47Refs.I.Weapon(prefab == Z47Refs.I.swatModel ? "m4" : "ak").prefab, g.transform);
            g.AddComponent<PoserGun>().gun = gun.transform;
        }

        void RebuildMenu() { if (menuRoot != null) Destroy(menuRoot); menuRoot = null; BuildMenu(); }

        static Vector3 MapCenter(GameObject map)
        {
            Bounds b = new Bounds(); bool f = true;
            foreach (var r in map.GetComponentsInChildren<Renderer>()) { if (f) { b = r.bounds; f = false; } else b.Encapsulate(r.bounds); }
            return new Vector3(b.center.x, 0, b.center.z);
        }

        public void StartMatch()
        {
            Time.timeScale = 1f; AudioListener.pause = false;
            if (menuRoot != null) Destroy(menuRoot);
            menuRoot = null;
            if (matchRoot != null) Destroy(matchRoot);
            StartCoroutine(StartMatchNextFrame());
        }

        IEnumerator StartMatchNextFrame()
        {
            yield return null; // let destroyed objects go
            Sfx.Music(null, 0);
            matchRoot = new GameObject("Match");
            var mm = matchRoot.AddComponent<MatchManager>();
            mm.Begin();
        }

        public void ToMenu()
        {
            Time.timeScale = 1f; AudioListener.pause = false;
            Sfx.Ambience(null, 0);
            if (matchRoot != null) Destroy(matchRoot);
            matchRoot = null;
            StartCoroutine(MenuNextFrame());
        }

        IEnumerator MenuNextFrame() { yield return null; BuildMenu(); }

        // ------------------------------------------------------------ settings
        public void ShowSettings(Transform parent, Action onClose)
        {
            var panel = UIK.Full(parent, "Settings");
            UIK.Img(panel, "bg", new Color(0.03f, 0.03f, 0.03f, 0.94f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            UIK.Txt(panel, "title", L.S("settings"), 72, Pal.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(1000, 100), true);

            string[] q = { "low", "medium", "high", "ultra" };
            int row = 0;
            Row(panel, row++, L.S("graphics"), () => L.S(q[Settings.Quality]), d => { Settings.Quality = Mathf.Clamp(Settings.Quality + d, 0, 3); });
            Row(panel, row++, L.S("bright"), () => Mathf.RoundToInt(Settings.Brightness * 100) + "%", d => { Settings.Brightness = Mathf.Clamp(Settings.Brightness + d * 0.1f, 0f, 1f); if (Exposure != null) Exposure.postExposure.Override(Settings.ExposureValue); });
            Row(panel, row++, L.S("fps"), () => Settings.Fps.ToString(), d => { int[] f = { 30, 60, 120 }; int i = Array.IndexOf(f, Settings.Fps); Settings.Fps = f[Mathf.Clamp((i < 0 ? 1 : i) + d, 0, 2)]; });
            Row(panel, row++, L.S("sens"), () => Settings.Sens.ToString("0.0"), d => Settings.Sens = Mathf.Clamp(Settings.Sens + d * 0.1f, 0.2f, 3f));
            Row(panel, row++, L.S("adssens"), () => Settings.AdsSens.ToString("0.0"), d => Settings.AdsSens = Mathf.Clamp(Settings.AdsSens + d * 0.1f, 0.2f, 2f));
            Row(panel, row++, L.S("music"), () => Mathf.RoundToInt(Settings.Music * 100) + "%", d => { Settings.Music = Mathf.Clamp01(Settings.Music + d * 0.1f); Sfx.Music(Z47Refs.I.menuMusic, menuRoot != null ? 0.7f : 0f); });
            Row(panel, row++, L.S("sfx"), () => Mathf.RoundToInt(Settings.Sfx * 100) + "%", d => Settings.Sfx = Mathf.Clamp01(Settings.Sfx + d * 0.1f));
            Row(panel, row++, L.S("aimassist"), () => L.S(Settings.AimAssist ? "on" : "off"), d => Settings.AimAssist = !Settings.AimAssist);
            Row(panel, row++, L.S("leftfire"), () => L.S(Settings.LeftFire ? "on" : "off"), d => Settings.LeftFire = !Settings.LeftFire);
            Row(panel, row++, L.S("arms"), () => L.S(Settings.ShowArms ? "on" : "off"), d => Settings.ShowArms = !Settings.ShowArms);
            Row(panel, row++, L.S("lang"), () => Settings.Turkish ? "TÜRKÇE" : "ENGLISH", d => Settings.Turkish = !Settings.Turkish);

            UIK.Btn(panel, L.S("back"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(480, 96), () =>
            {
                Settings.Apply();
                foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) ApplyCameraFx(c);
                Destroy(panel.gameObject);
                onClose?.Invoke();
            }, 44, true);
        }

        void Row(RectTransform panel, int i, string label, Func<string> val, Action<int> change)
        {
            float y = -165 - i * 68;
            UIK.Txt(panel, "l", label, 34, Pal.Dim, TextAnchor.MiddleRight, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-260, y), new Vector2(560, 60), true);
            var v = UIK.Txt(panel, "v", val(), 36, Pal.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(230, y), new Vector2(300, 60), true);
            UIK.Btn(panel, "<", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(40, y), new Vector2(76, 62), () => { change(-1); v.text = val(); }, 40);
            UIK.Btn(panel, ">", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(420, y), new Vector2(76, 62), () => { change(1); v.text = val(); }, 40);
        }

        // ------------------------------------------------------------ credits
        void ShowCredits()
        {
            var panel = UIK.Full(menuCanvas.transform, "Credits");
            UIK.Img(panel, "bg", new Color(0.03f, 0.03f, 0.03f, 0.95f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            UIK.Txt(panel, "title", L.S("credits"), 72, Pal.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(1200, 100), true);
            var t = UIK.Txt(panel, "body", CreditsText, 24, Pal.Text, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(1700, 820));
            t.lineSpacing = 1.05f;
            UIK.Btn(panel, L.S("back"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(480, 90), () => Destroy(panel.gameObject), 42, true);
        }

        const string CreditsText =
            "<b>AYD WORKS GAMES</b>\n\n" +
            "\"Shooting Game Enviornment Map TDM\" by 00amza — CC BY 4.0 (skfb.ly/pF7D6)\n" +
            "\"M4A1 Carbine\" by MrM0lten — CC BY 4.0 (skfb.ly/6RAVW)\n" +
            "\"Ak47\" by wburton — CC BY 4.0 (skfb.ly/6unpx)\n" +
            "\"AWP\" by forestie — CC BY 4.0 (sketchfab.com/3d-models/awp-b7101f0325aa4b0dad8512d0ec67bfa1)\n" +
            "\"Glock Gun 3d Model Free Download\" by thronetools — CC BY 4.0 (skfb.ly/pNyF7)\n" +
            "\"Grenade\" by DJMaesen — CC BY 4.0 (skfb.ly/6AnKC)\n" +
            "Characters & animations: Mixamo (Adobe)\n\n" +
            "Sounds (freesound.org): mnslugger20, 18hiltc, evanboyerman, synth2, gfl7, fastson,\n" +
            "rammbostein, fusionwolf3740, nicktermer, ogsoundfx, qubodup, designerschoice (Nicholas Judy), jaklocke\n" +
            "Music: \"Viking/Medieval Theme\" by Mark_Murray (freesound.org)\n\n" +
            "Fonts: Black Ops One, Rajdhani — SIL Open Font License\n" +
            "Licenses: creativecommons.org/licenses/by/4.0";
    }

    /// One permanent AudioListener that rides on whichever camera is active.
    public class ListenerFollow : MonoBehaviour
    {
        public static Camera bootCam;
        void LateUpdate()
        {
            var c = Camera.main;
            if (c == null) { foreach (var cc in Camera.allCameras) { if (cc != bootCam) { c = cc; break; } } }
            if (bootCam != null) bootCam.enabled = c == null;
            if (c == null) return;
            if (c != null) transform.SetPositionAndRotation(c.transform.position, c.transform.rotation);
            Sfx.ListenerPos = transform.position;
        }
    }

    public class MenuOrbit : MonoBehaviour
    {
        public Vector3 center;
        float a = 200f;
        void Update()
        {
            a += Time.deltaTime * 3.5f;
            float r = 34f;
            Vector3 p = center + Quaternion.Euler(0, a, 0) * new Vector3(0, 0, -r);
            p.y = 15f + Mathf.Sin(Time.time * 0.15f) * 1.5f;
            transform.position = p;
            transform.rotation = Quaternion.LookRotation(center + Vector3.up * 2f - p);
        }
    }

    /// Keeps a rifle in a menu soldier's hands.
    public class PoserGun : MonoBehaviour
    {
        public Transform gun;
        Transform r, l;
        void Start() { var a = GetComponent<Animator>(); r = a.GetBoneTransform(HumanBodyBones.RightHand); l = a.GetBoneTransform(HumanBodyBones.LeftHand); }
        void LateUpdate()
        {
            if (r == null || l == null || gun == null) return;
            Vector3 f = l.position - r.position; if (f.sqrMagnitude < 1e-4f) return;
            Vector3 up = Vector3.Cross(f, transform.right); if (Vector3.Dot(up, Vector3.up) < 0) up = -up;
            gun.rotation = Quaternion.LookRotation(f.normalized, up);
            gun.position = r.position + gun.rotation * new Vector3(0, 0.04f, 0.03f);
        }
    }
}
