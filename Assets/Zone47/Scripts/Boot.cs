using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Zone47
{
    /// App entry: splash → menu → match. Lives in the only scene.
    public partial class Boot : MonoBehaviour
    {
        public static Boot I;
        GameObject menuRoot, matchRoot;
        Canvas menuCanvas;
        Camera menuCam;
        Light sun;
        public static Light Sun, Fill;
        public static Volume ScopeBlur;
        public static Bloom BloomFx;
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
            sun = sg.AddComponent<Light>(); Sun = sun;
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.86f, 0.7f);
            sun.intensity = 1.55f;
            sun.shadows = Settings.SunShadows;
            sun.shadowStrength = 0.85f;
            sg.transform.rotation = Quaternion.Euler(32f, -38f, 0f);
            RenderSettings.sun = sun;

            // sky bounce: soft cool fill from the opposite side so shaded walls are never black
            var fg = new GameObject("SkyFill"); DontDestroyOnLoad(fg);
            var fill = fg.AddComponent<Light>(); Fill = fill;
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
            var bl = prof.Add<Bloom>(true); BloomFx = bl; bl.active = Settings.Quality >= 1; bl.intensity.Override(0.55f); bl.threshold.Override(1.05f); bl.scatter.Override(0.6f); bl.highQualityFiltering.Override(Settings.Quality >= 2);
            var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(Settings.ExposureValue); ca.contrast.Override(14f); ca.saturation.Override(-8f);
            Exposure = ca;
            ca.colorFilter.Override(new Color(1f, 0.95f, 0.88f));
            var vi = prof.Add<Vignette>(true); vi.intensity.Override(0.28f); vi.smoothness.Override(0.45f);
            var sm = prof.Add<ShadowsMidtonesHighlights>(true);
            sm.shadows.Override(new Vector4(0.95f, 1f, 1.08f, 0f));
            volume.sharedProfile = prof;

            // scope blur: everything outside the scope lens goes soft while looking through it
            var bg = new GameObject("ScopeBlur");
            DontDestroyOnLoad(bg);
            int bl2 = LayerMask.NameToLayer("Viewmodel"); if (bl2 >= 0) bg.layer = bl2;
            ScopeBlur = bg.AddComponent<Volume>();
            ScopeBlur.isGlobal = true; ScopeBlur.priority = 5; ScopeBlur.weight = 0f;
            var bp = ScriptableObject.CreateInstance<VolumeProfile>();
            var dof = bp.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(0f); dof.gaussianEnd.Override(2.5f);
            dof.gaussianMaxRadius.Override(1.5f); dof.highQualitySampling.Override(false);
            ScopeBlur.sharedProfile = bp;
        }

        public static ColorAdjustments Exposure;

        public static void ApplyCameraFx(Camera c)
        {
            var d = c.GetUniversalAdditionalCameraData();
            d.renderPostProcessing = Settings.PostFx;
            d.antialiasing = Settings.AA;
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
            var camGo = new GameObject("MenuCam"); camGo.transform.SetParent(menuRoot.transform);
            menuCam = camGo.AddComponent<Camera>(); menuCam.fieldOfView = 50; menuCam.farClipPlane = 500;
            ApplyCameraFx(menuCam);

            bool video = VideoBg.Available;
            if (video)
            {
                // video lobby: nothing 3D to draw (cheaper, cooler phone); the camera only clears to black
                menuCam.cullingMask = 0; menuCam.clearFlags = CameraClearFlags.SolidColor; menuCam.backgroundColor = Color.black;
                Weather.ApplyLobby(); RenderSettings.fog = false;
                if (R.rain != null) Sfx.Ambience(R.rain, 0.45f);
            }
            else if (R.lobbyPrefab != null) BuildLobbyScene(R, camGo);
            else
            {
                var map = Instantiate(R.mapPrefab, menuRoot.transform);
                camGo.AddComponent<MenuOrbit>().center = MapCenter(map);
            }

            menuCanvas = UIK.Canvas("MenuCanvas", 20);
            menuCanvas.transform.SetParent(menuRoot.transform);
            var full = UIK.Full(menuCanvas.transform, "full");
            if (video) VideoBg.AddTo(full, 0.15f);
            // readability: dark band behind the left menu and the right info column
            // soft side gradients (dark at the screen edge, fading to clear) instead of flat grey slabs
            var gl = UIK.Rect(full, "shadeL", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(1000, 0)); gl.pivot = new Vector2(0, 0.5f); gl.anchoredPosition = Vector2.zero;
            var gli = gl.gameObject.AddComponent<RawImage>(); gli.texture = EdgeGradient(); gli.color = new Color(0, 0, 0, 0.78f); gli.raycastTarget = false;
            var gr = UIK.Rect(full, "shadeR", new Vector2(1, 0), new Vector2(1, 1), Vector2.zero, new Vector2(1000, 0)); gr.pivot = new Vector2(1, 0.5f); gr.anchoredPosition = Vector2.zero;
            var gri = gr.gameObject.AddComponent<RawImage>(); gri.texture = EdgeGradient(); gri.color = new Color(0, 0, 0, 0.72f); gri.raycastTarget = false;
            gri.uvRect = new Rect(1, 0, -1, 1);
            var root = UIK.Full(menuCanvas.transform, "root");
            root.gameObject.AddComponent<SafeArea>();

            BuildLobbyUI(root);

            Sfx.Music(R.menuMusic, R.lobbyPrefab != null && R.rain != null ? 0.4f : 0.7f);
            if (string.IsNullOrWhiteSpace(Settings.PlayerName)) ShowNameDialog(true);
        }

        Text nameTxt, rosterMe;
        static Texture2D edgeGrad;
        static Texture2D EdgeGradient()
        {
            if (edgeGrad != null) return edgeGrad;
            edgeGrad = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[256];
            for (int i = 0; i < 256; i++) { float t = i / 255f; float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.45f, 1f, t)); px[i] = new Color32(255, 255, 255, (byte)(a * 255)); }
            edgeGrad.SetPixels32(px); edgeGrad.Apply();
            return edgeGrad;
        }

        void BuildLobbyScene(Z47Refs R, GameObject camGo)
        {
            var lob = Instantiate(R.lobbyPrefab, menuRoot.transform);
            Transform pc = Util.FindDeep(lob.transform, "PT_cam"), pl = Util.FindDeep(lob.transform, "PT_look"),
                      ps = Util.FindDeep(lob.transform, "PT_soldier"), pf = Util.FindDeep(lob.transform, "PT_face");
            menuCam.fieldOfView = 46f; menuCam.nearClipPlane = 0.05f; menuCam.farClipPlane = 200f;
            menuCam.clearFlags = CameraClearFlags.SolidColor;
            menuCam.backgroundColor = new Color(0.6f, 0.63f, 0.66f);                 // overcast sky, like the scan
            var cd = menuCam.GetUniversalAdditionalCameraData();
            cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;   // menu only: best AA, the scan + character edges shimmer otherwise
            cd.antialiasingQuality = AntialiasingQuality.High;
            Weather.ApplyLobby();
            RenderSettings.fog = false;
            // stormy sky dome (inside-out sphere) behind everything
            if (R.lobbySkyMat != null && pc != null)
            {
                var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(sky.GetComponent<Collider>());
                sky.name = "LobbySky"; sky.transform.SetParent(menuRoot.transform, false);
                sky.transform.position = pc.position; sky.transform.localScale = new Vector3(-180f, 180f, 180f);
                var sr = sky.GetComponent<MeshRenderer>(); sr.sharedMaterial = R.lobbySkyMat;
                sr.shadowCastingMode = ShadowCastingMode.Off; sr.receiveShadows = false;
            }
            if (pc != null && pl != null) BuildRain(R, pc.position, pl.position);
            if (R.rain != null) Sfx.Ambience(R.rain, 0.55f);
            if (pc != null && pl != null) camGo.AddComponent<LobbyCam>().Init(pc.position, pl.position);
            if (ps != null)
            {
                Vector3 to = pf != null ? pf.position - ps.position : -Vector3.forward; to.y = 0;
                SpawnPoser(R.swatModel, ps.position, Quaternion.LookRotation(to).eulerAngles.y, true);
            }
        }

        /// Rain: stretched streak particles in a box in front of the lobby camera (world space, light wind).
        void BuildRain(Z47Refs R, Vector3 camPos, Vector3 look)
        {
            if (R.rainMat == null) return;
            Vector3 fwd = (look - camPos); fwd.y = 0; fwd.Normalize();
            var go = new GameObject("Rain"); go.transform.SetParent(menuRoot.transform, false);
            go.transform.position = camPos + fwd * 7f + Vector3.up * 5f;
            go.transform.rotation = Quaternion.LookRotation(fwd);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.duration = 5f;
            main.startLifetime = 0.75f; main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
            main.startColor = new Color(0.85f, 0.88f, 0.92f, 0.3f);
            main.maxParticles = 4000; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var em = ps.emission; em.rateOverTime = 2600f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(14f, 0.2f, 14f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-1.4f, -0.8f); vel.y = new ParticleSystem.MinMaxCurve(-12f, -10f); vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.04f; r.lengthScale = 1f;
            r.sharedMaterial = R.rainMat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
        }

        // ------------------------------------------------------------ callsign dialog
        void ShowNameDialog(bool first)
        {
            var panel = UIK.Full(menuCanvas.transform, "NameDialog");
            UIK.Img(panel, "bg", new Color(0, 0, 0, 0.8f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            var box = UIK.Img(panel, "box", new Color(0.05f, 0.055f, 0.05f, 0.97f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(820, 430));
            UIK.Img(box.transform, "top", Pal.Accent, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 6)).rectTransform.pivot = new Vector2(0.5f, 1);
            UIK.Txt(box.transform, "t", L.S("callsign_q"), 44, Pal.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(760, 60), true);
            UIK.Txt(box.transform, "h", L.S("callsign_h"), 22, Pal.Dim, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -122), new Vector2(760, 36), false);
            var fieldBg = UIK.Img(box.transform, "field", new Color(1, 1, 1, 0.08f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(640, 86), true);
            UIK.Img(fieldBg.transform, "line", Pal.Accent, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 3)).rectTransform.pivot = new Vector2(0.5f, 0);
            var txt = UIK.Txt(fieldBg.transform, "t", "", 40, Pal.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-40, 0), true);
            txt.supportRichText = false; txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            var ph = UIK.Txt(fieldBg.transform, "ph", L.S("callsign_ph"), 36, new Color(1, 1, 1, 0.25f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-40, 0), true);
            var input = fieldBg.gameObject.AddComponent<InputField>();
            input.textComponent = txt; input.placeholder = ph; input.characterLimit = 14;
            input.lineType = InputField.LineType.SingleLine;
            input.text = Settings.PlayerName;
            UIK.Btn(box.transform, L.S("ok"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(first ? 0 : 150, 50), new Vector2(first ? 420 : 280, 86), () =>
            {
                string n = input.text.Trim();
                if (n.Length < 2) { ph.text = L.S("callsign_short"); input.text = ""; return; }
                Settings.PlayerName = n; PlayerPrefs.Save();
                if (nameTxt != null) nameTxt.text = n.ToUpper();
                if (rosterMe != null) rosterMe.text = n.Substring(0, 1).ToUpper();
                if (Social.Ready) Social.Rename(); else if (Net.Available && Social.Available) Social.Init();
                RefreshLobby();
                Destroy(panel.gameObject);
            }, 36, true);
            if (!first) UIK.Btn(box.transform, L.S("cancel"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-150, 50), new Vector2(280, 86), () => Destroy(panel.gameObject), 32);
            input.ActivateInputField();
        }

        string DiffLabel() => L.S("difficulty") + ":  " + L.S(Settings.Difficulty == 0 ? "easy" : Settings.Difficulty == 1 ? "normal" : "hard");

        void SpawnPoser(GameObject prefab, Vector3 pos, float yaw, bool exact = false)
        {
            var g = Instantiate(prefab, menuRoot.transform);
            g.transform.position = exact ? pos : new Vector3(pos.x, Util.GroundY(pos) + 0.01f, pos.z);
            g.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var a = g.GetComponent<Animator>();
            if (a != null) { a.runtimeAnimatorController = Z47Refs.I.bodyController; a.SetBool("Aim", false); }
            var gun = Instantiate(Z47Refs.I.Weapon(prefab == Z47Refs.I.swatModel ? "m4" : "ak").prefab, g.transform);
            var pg = g.AddComponent<PoserGun>(); pg.gun = gun.transform; pg.def = Z47Refs.I.Weapon(prefab == Z47Refs.I.swatModel ? "m4" : "ak");
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
            if (!Net.InMatch) { MatchManager.EnsureNames(); Weather.PrepareMatch(); }      // online: line-up and weather come from the host
            lobbyRoot = null; lobbyUI = null; stage = null; inviteBox = null;      // the lobby goes away with the menu
            LoadingScreen.Show();
            if (menuRoot != null) Destroy(menuRoot);
            menuRoot = null;
            if (matchRoot != null) Destroy(matchRoot);
            StartCoroutine(StartMatchNextFrame());
        }

        IEnumerator StartMatchNextFrame()
        {
            yield return null; // let destroyed objects go
            Sfx.Music(null, 0);
            RenderSettings.fog = true;
            matchRoot = new GameObject("Match");
            var mm = matchRoot.AddComponent<MatchManager>();
            mm.Begin();
        }

        public void ToMenu() => ToMenu(null);
        /// Back to the lobby; leaves any online room. `reason` (e.g. "host left") is shown as a toast.
        public void ToMenu(string reason)
        {
            Net.Leave();
            pendingToast = reason;
            Time.timeScale = 1f; AudioListener.pause = false;
            Sfx.Ambience(null, 0);
            if (matchRoot != null) Destroy(matchRoot);
            matchRoot = null;
            StartCoroutine(MenuNextFrame());
        }

        IEnumerator MenuNextFrame() { yield return null; BuildMenu(); if (!string.IsNullOrEmpty(pendingToast)) { Toast(pendingToast); pendingToast = null; } reopenRoom = false; }
        string pendingToast; bool reopenRoom;

        /// Online match over: back to the room screen, still connected.
        public void BackToRoom()
        {
            Net.EndMatchLocal();
            Time.timeScale = 1f; AudioListener.pause = false;
            Sfx.Ambience(null, 0);
            if (matchRoot != null) Destroy(matchRoot);
            matchRoot = null;
            reopenRoom = true;
            StartCoroutine(MenuNextFrame());
        }

        // ------------------------------------------------------------ settings
        public void ShowSettings(Transform parent, Action onClose, bool inMatch = false) => SettingsUI.Show(parent, onClose, inMatch);

        // ------------------------------------------------------------ credits
        public void ShowCredits(Transform parent)
        {
            var panel = UIK.Full(parent, "Credits");
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
            "\"fps arms\" by DJMaesen — CC BY 4.0 (skfb.ly/6R6yX)\n" +
            "\"Liman / Port\" map, textures & procedural audio: AYD Works Games\n" +
            "\"Savas Bolgesi\" map: AYD Works Games · textures: Poly Haven (CC0)\n" +
            "\"Conteiner\" by pbk (Rafael_Oliveira) — CC BY 4.0 (sketchfab.com/3d-models/conteiner-8b5a55e3059d41699e4e3aa9c0c1baa4)\n" +
            "\"Forklift Truck\" by Kray Ewan — CC BY 4.0 (sketchfab.com/3d-models/forklift-truck-1567f5f71c1a40eebe487b1b8f570250)\n" +
            "\"Fort Worden Gun Battery Ash\" by peter54 — CC BY 4.0 (sketchfab.com/3d-models/fort-worden-gun-battery-ash-a0bc6bcaaa234e94855be78c20fb11c1)\n" +
            "\"fps animated carbine\" by DJMaesen — CC BY 4.0 (sketchfab.com/3d-models/fps-animated-carbine-62977bb4c53047a185b9f3a0cdf56b87)\n" +
            "\"Free - Holographic sight game ready\" by Karnaval — CC BY 4.0 (skfb.ly/oWDxG)\n" +
            "Icons: game-icons.net — Lorc, Delapouite, sbed, Skoll, Guard13007 — CC BY 3.0\n" +
            "Characters & animations: Mixamo (Adobe)\n\n" +
            "Sounds (freesound.org): mnslugger20, 18hiltc, evanboyerman, synth2, gfl7, fastson,\n" +
            "rammbostein, fusionwolf3740, nicktermer, ogsoundfx, qubodup, designerschoice (Nicholas Judy), jaklocke, ceberation,\n" +
            "dirtjm, silverillusionist, baggonotes, patrick_corra, speakwithanimals (rain),\n" +
            "cell31_sound_productions (drone), morganpurkis (bomb), antumdeluge (helicopter), nimlos (siren),\n" +
            "lemudcrab (shotgun), jumbosizedfish (shotgun reload)\n" +
            "Footsteps: \"Footsteps - Essentials\" by Nox Sound (CC0)\n" +
            "Drone: \"MQ9 Ripper military drone\" by Giperione (CC BY 4.0, skfb.ly/oBAH8)\n" +
            "Bomb: \"MK2 Grenade Drone Bomb\" by yousef.hameed (CC BY 4.0, skfb.ly/oUNp7)\n" +
            "Shotgun: \"Shotgun\" by Pieter Ferreira (CC BY 4.0, skfb.ly/6WTnO)\n" +
            "Flags: flag-icons by Panayiotis Lipiridis (MIT)\n" +
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

    /// Lobby camera: fixed shot (any drift makes the photo-scan shimmer; the rain provides the motion).
    public class LobbyCam : MonoBehaviour
    {
        Vector3 basePos, look;
        public void Init(Vector3 p, Vector3 l) { basePos = p; look = l; Update(); }
        void Update()
        {
            float t = Time.time;
            Vector3 fwd = (look - basePos).normalized, right = Vector3.Cross(Vector3.up, fwd).normalized;
            transform.position = basePos;
            transform.rotation = Quaternion.LookRotation(look - basePos);
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

    /// Lobby soldier pose (PUBG style): square to the camera, legs straight, standing tall, rifle held diagonally
    /// across the chest (muzzle up to his left) with both hands on it. Built on top of the rifle idle every frame
    /// (the idle keeps the breathing); every step measures the animated pose and sets an absolute result, so nothing builds up.
    public class PoserGun : MonoBehaviour
    {
        public Transform gun;
        public WeaponDef def;
        Animator a; float baseYaw; bool ok;
        Transform hips, spine, neck, lUp, lLo, lHand, rUp, rLo, rHand, lMid;
        Transform lThigh, lKnee, lFoot, rThigh, rKnee, rFoot, lToes, rToes;

        void Start()
        {
            a = GetComponent<Animator>();
            baseYaw = transform.localEulerAngles.y;
            if (a == null || !a.isHuman) return;
            Transform B(HumanBodyBones b) => a.GetBoneTransform(b);
            hips = B(HumanBodyBones.Hips); spine = B(HumanBodyBones.Spine); neck = B(HumanBodyBones.Neck);
            lUp = B(HumanBodyBones.LeftUpperArm); lLo = B(HumanBodyBones.LeftLowerArm); lHand = B(HumanBodyBones.LeftHand);
            rUp = B(HumanBodyBones.RightUpperArm); rLo = B(HumanBodyBones.RightLowerArm); rHand = B(HumanBodyBones.RightHand);
            lMid = B(HumanBodyBones.LeftMiddleProximal);
            lThigh = B(HumanBodyBones.LeftUpperLeg); lKnee = B(HumanBodyBones.LeftLowerLeg); lFoot = B(HumanBodyBones.LeftFoot);
            rThigh = B(HumanBodyBones.RightUpperLeg); rKnee = B(HumanBodyBones.RightLowerLeg); rFoot = B(HumanBodyBones.RightFoot);
            lToes = B(HumanBodyBones.LeftToes); rToes = B(HumanBodyBones.RightToes);
            ok = hips && spine && neck && lUp && lLo && lHand && rUp && rLo && rHand && lThigh && lKnee && lFoot && rThigh && rKnee && rFoot;
        }

        void LateUpdate()
        {
            if (gun == null) return;
            if (!ok) { OldPlace(); return; }

            // 0) the idle's own grip: where the hands sit on the rifle and how they're turned (measured on the untouched
            //    animated pose, relative to the rifle). Reused below so the hands keep holding it the natural, authored way.
            Vector3 rp = rHand.position, lp = lHand.position, along = lp - rp;
            if (along.sqrMagnitude < 1e-4f) { OldPlace(); return; }
            Vector3 gu = Vector3.Cross(along, transform.right); if (Vector3.Dot(gu, Vector3.up) < 0) gu = -gu;
            Quaternion animGun = Quaternion.LookRotation(along.normalized, gu), inv = Quaternion.Inverse(animGun);
            Vector3 animGrip = rp + animGun * new Vector3(0, 0.04f, 0.03f);
            Quaternion relR = inv * rHand.rotation, relL = inv * lHand.rotation;
            Vector3 offR = inv * (rp - animGrip), offL = inv * (lp - animGrip);

            // 1) square up: the rifle idle stands bladed (body turned sideways). Turn the whole soldier so the chest faces the camera.
            Vector3 sh = rUp.position - lUp.position, hp = rThigh.position - lThigh.position;
            Vector3 facing = Vector3.Cross((sh.normalized * 0.7f + hp.normalized * 0.3f).Flat(), Vector3.up);
            Vector3 fl = transform.InverseTransformDirection(facing);
            float turn = fl.sqrMagnitude > 1e-4f ? Mathf.Clamp(Mathf.Atan2(fl.x, fl.z) * Mathf.Rad2Deg, -75f, 75f) : 0f;
            transform.localRotation = Quaternion.Euler(0f, baseYaw - turn, 0f);

            // 2) straight legs, feet under the hips (the idle has bent knees)
            StraightLegs(turn);

            // 3) stand tall, chin up
            Posture.Straighten(a, transform, 1f);

            // 4) the idle's head looks ahead while its body is turned: after squaring the body the head was left looking
            //    sideways. Turn it back by the same angle (neck + head), so he looks straight at the camera.
            var head = a.GetBoneTransform(HumanBodyBones.Head);
            neck.rotation = Quaternion.AngleAxis(turn * 0.4f, Vector3.up) * neck.rotation;
            if (head != null) head.rotation = Quaternion.AngleAxis(turn * 0.6f, Vector3.up) * head.rotation;

            // 5) rifle at low ready: stock up by the right chest, muzzle diagonally down toward the ground in front-left,
            //    sights on top, its side toward the camera
            Vector3 H = hips.position, N = neck.position;
            Vector3 up = (N - H).normalized;
            Vector3 right = Vector3.ProjectOnPlane(rUp.position - lUp.position, up).normalized;
            Vector3 fwd = Vector3.Cross(right, up);
            Vector3 muzzle = (-up * 0.55f - right * 0.55f + fwd * 0.62f).normalized;
            Vector3 gunUp = Vector3.ProjectOnPlane(up + right * 0.35f, muzzle).normalized;
            Quaternion gr = Quaternion.LookRotation(muzzle, gunUp);
            Vector3 grip = H + up * 0.3f + right * 0.15f + fwd * 0.22f;
            gun.SetPositionAndRotation(grip, gr);

            // 6) hands back on it with the idle's own grip; elbows down, the right one by the side, the left one out
            Arm(rUp, rLo, rHand, grip + gr * offR, gr * relR, -up * 0.5f + right * 0.9f - fwd * 0.25f);
            // left hand: palm right under the handguard (the idle's hand angle, but placed exactly on the gun)
            Vector3 fore = def != null ? gun.TransformPoint(def.forePoint) : grip + muzzle * 0.32f;
            fore -= gunUp * 0.035f;
            Vector3 palm = Vector3.zero;
            for (int pass = 0; pass < 2; pass++)
            {
                Arm(lUp, lLo, lHand, fore - palm, gr * relL, -up * 0.7f - right * 0.6f - fwd * 0.1f);
                palm = lMid != null ? (lMid.position - lHand.position) * 0.5f : Vector3.zero;   // the gun sits in the palm, not at the wrist
            }
        }

        static void Arm(Transform up, Transform lo, Transform hand, Vector3 wrist, Quaternion handRot, Vector3 pole)
        {
            Solve(up, lo, hand, wrist, pole);
            hand.rotation = handRot;
        }

        void StraightLegs(float turn)
        {
            float ankleY = Mathf.Min(lFoot.position.y, rFoot.position.y);
            Quaternion lf = lFoot.rotation, rf = rFoot.rotation;
            // knees straight (a hair of bend so it doesn't look locked)
            Straighten(lThigh, lKnee, lFoot); Straighten(rThigh, rKnee, rFoot);
            float L = ((lKnee.position - lThigh.position).magnitude + (lFoot.position - lKnee.position).magnitude
                     + (rKnee.position - rThigh.position).magnitude + (rFoot.position - rKnee.position).magnitude) * 0.5f;
            // hips up so straight legs reach the ground
            float hipJointY = (lThigh.position.y + rThigh.position.y) * 0.5f;
            hips.position += Vector3.up * (ankleY + L * 0.985f - hipJointY);
            Vector3 side = transform.right;
            Aim(lThigh, lFoot, new Vector3(lThigh.position.x, ankleY, lThigh.position.z) - side * 0.03f);
            Aim(rThigh, rFoot, new Vector3(rThigh.position.x, ankleY, rThigh.position.z) + side * 0.03f);
            // feet: keep the idle's flat foot, then turn each one to point forward (toes slightly out)
            lFoot.rotation = lf; rFoot.rotation = rf;
            Vector3 body = Vector3.Cross((rUp.position - lUp.position).Flat(), Vector3.up).normalized;      // where the chest faces
            if (body.sqrMagnitude < 0.5f) body = transform.forward;
            PointFoot(lFoot, lToes, Quaternion.Euler(0, -5f, 0) * body, turn);
            PointFoot(rFoot, rToes, Quaternion.Euler(0, 5f, 0) * body, turn);
        }

        static void PointFoot(Transform foot, Transform toes, Vector3 dir, float turn)
        {
            Vector3 cur = toes != null ? (toes.position - foot.position).Flat() : Vector3.zero;
            float ang = cur.sqrMagnitude > 0.0025f ? Vector3.SignedAngle(cur, dir.Flat(), Vector3.up) : turn;   // no toe bone: turn back with the body
            foot.rotation = Quaternion.AngleAxis(Mathf.Clamp(ang, -70f, 70f), Vector3.up) * foot.rotation;
        }

        static void Straighten(Transform thigh, Transform knee, Transform foot)
        {
            Vector3 upper = knee.position - thigh.position, lower = foot.position - knee.position;
            Vector3 want = Vector3.Slerp(upper.normalized, lower.normalized, 0.04f);     // ~straight, keeps the knee's bend direction
            knee.rotation = Quaternion.FromToRotation(lower, want) * knee.rotation;
        }

        static void Aim(Transform root, Transform end, Vector3 target)
        {
            Vector3 cur = end.position - root.position, want = target - root.position;
            if (cur.sqrMagnitude > 1e-6f && want.sqrMagnitude > 1e-6f) root.rotation = Quaternion.FromToRotation(cur, want) * root.rotation;
        }

        static void Solve(Transform up, Transform lo, Transform hand, Vector3 target, Vector3 pole)
        {
            Vector3 a = up.position, b = lo.position, c = hand.position;
            float lab = (b - a).magnitude, lbc = (c - b).magnitude;
            Vector3 at = target - a;
            float lat = Mathf.Clamp(at.magnitude, 0.01f, lab + lbc - 0.001f);
            float d1 = (lab * lab - lbc * lbc + lat * lat) / (2f * lat);
            float h = Mathf.Sqrt(Mathf.Max(lab * lab - d1 * d1, 0f));
            Vector3 n = at.normalized, pd = Vector3.ProjectOnPlane(pole, n).normalized;
            Vector3 e = a + n * d1 + pd * h;
            up.rotation = Quaternion.FromToRotation(b - a, e - a) * up.rotation;
            b = lo.position; c = hand.position;
            lo.rotation = Quaternion.FromToRotation(c - b, a + n * lat - b) * lo.rotation;
        }

        /// Fallback (non-humanoid model): gun between the hands, as before.
        void OldPlace()
        {
            if (rHand == null || lHand == null) return;
            Vector3 f = lHand.position - rHand.position; if (f.sqrMagnitude < 1e-4f) return;
            Vector3 u = Vector3.Cross(f, transform.right); if (Vector3.Dot(u, Vector3.up) < 0) u = -u;
            gun.rotation = Quaternion.LookRotation(f.normalized, u);
            gun.position = rHand.position + gun.rotation * new Vector3(0, 0.04f, 0.03f);
        }
    }
}
