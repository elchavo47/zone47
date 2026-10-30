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
            sun.shadows = Settings.Mobile && Settings.Quality < 3 ? LightShadows.Hard : LightShadows.Soft;
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
            var bl = prof.Add<Bloom>(true); bl.intensity.Override(0.55f); bl.threshold.Override(1.05f); bl.scatter.Override(0.6f); bl.highQualityFiltering.Override(!Settings.Mobile);
            var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(Settings.ExposureValue); ca.contrast.Override(14f); ca.saturation.Override(-8f);
            Exposure = ca;
            ca.colorFilter.Override(new Color(1f, 0.95f, 0.88f));
            var vi = prof.Add<Vignette>(true); vi.intensity.Override(0.28f); vi.smoothness.Override(0.45f);
            var sm = prof.Add<ShadowsMidtonesHighlights>(true);
            sm.shadows.Override(new Vector4(0.95f, 1f, 1.08f, 0f));
            volume.sharedProfile = prof;
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

            if (R.lobbyPrefab != null) BuildLobbyScene(R, camGo);
            else
            {
                var map = Instantiate(R.mapPrefab, menuRoot.transform);
                camGo.AddComponent<MenuOrbit>().center = MapCenter(map);
            }

            menuCanvas = UIK.Canvas("MenuCanvas", 20);
            menuCanvas.transform.SetParent(menuRoot.transform);
            var full = UIK.Full(menuCanvas.transform, "full");
            // readability: dark band behind the left menu and the right info column
            // soft side gradients (dark at the screen edge, fading to clear) instead of flat grey slabs
            var gl = UIK.Rect(full, "shadeL", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(1000, 0)); gl.pivot = new Vector2(0, 0.5f); gl.anchoredPosition = Vector2.zero;
            var gli = gl.gameObject.AddComponent<RawImage>(); gli.texture = EdgeGradient(); gli.color = new Color(0, 0, 0, 0.78f); gli.raycastTarget = false;
            var gr = UIK.Rect(full, "shadeR", new Vector2(1, 0), new Vector2(1, 1), Vector2.zero, new Vector2(1000, 0)); gr.pivot = new Vector2(1, 0.5f); gr.anchoredPosition = Vector2.zero;
            var gri = gr.gameObject.AddComponent<RawImage>(); gri.texture = EdgeGradient(); gri.color = new Color(0, 0, 0, 0.72f); gri.raycastTarget = false;
            gri.uvRect = new Rect(1, 0, -1, 1);
            var root = UIK.Full(menuCanvas.transform, "root");
            root.gameObject.AddComponent<SafeArea>();

            // ---- left: logo, callsign, menu
            var logoTex = R.lobbyLogo != null ? R.lobbyLogo : R.logo;
            if (logoTex != null)
            {
                // transparent logo, no dark tile behind it: as wide as the menu column (560) and up to 270 tall
                float aspect = logoTex.width / (float)logoTex.height;
                Vector2 sz = aspect >= 560f / 270f ? new Vector2(560f, 560f / aspect) : new Vector2(270f * aspect, 270f);
                var lg = UIK.Rect(root, "logo", new Vector2(0, 1), new Vector2(0, 1), new Vector2(80 + (560f - sz.x) * 0.5f, -12), sz);
                var ri = lg.gameObject.AddComponent<RawImage>(); ri.texture = logoTex; ri.raycastTarget = false;
            }
            var chip = UIK.Img(root, "callsign", new Color(1, 1, 1, 0.07f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -290), new Vector2(560, 92), true);
            UIK.Img(chip.transform, "bar", Pal.Accent, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(6, 0)).rectTransform.pivot = new Vector2(0, 0.5f);
            UIK.Txt(chip.transform, "lbl", L.S("callsign"), 18, Pal.Dim, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(24, -12), new Vector2(-48, -24), true);
            nameTxt = UIK.Txt(chip.transform, "name", Settings.Callsign.ToUpper(), 38, Pal.Text, TextAnchor.LowerLeft, Vector2.zero, Vector2.one, new Vector2(24, 10), new Vector2(-48, -20), true);
            UIK.Txt(chip.transform, "edit", L.S("change"), 18, Pal.Accent, TextAnchor.MiddleRight, Vector2.zero, Vector2.one, new Vector2(-20, 0), new Vector2(-40, 0), true);
            chip.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); ShowNameDialog(false); });
            UIK.Txt(root, "mode", L.S("tdm"), 24, Pal.Accent, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(84, -400), new Vector2(700, 36), true);

            UIK.Btn(root, L.S("deploy"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(80, -30), new Vector2(560, 110), StartMatch, 52, true);
            var diffBtn = UIK.Btn(root, DiffLabel(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(80, -152), new Vector2(560, 80), null, 30);
            diffBtn.onClick.AddListener(() => { Settings.Difficulty = (Settings.Difficulty + 1) % 3; diffBtn.GetComponentInChildren<Text>().text = DiffLabel(); });
            UIK.Btn(root, L.S("settings"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(80, -244), new Vector2(560, 80), () => ShowSettings(menuCanvas.transform, RebuildMenu), 30);

            // ---- right: map + squads
            var col = UIK.Rect(root, "info", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -40), new Vector2(560, 980));
            UIK.Txt(col, "mh", L.S("map"), 20, Pal.Dim, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(0, 30), true);
            // two selectable map cards
            var cards = new System.Collections.Generic.List<(Image bg, Image bar, Text sel, int id)>();
            int nMaps = R.portPrefab != null ? 2 : 1;
            if (Settings.Map >= nMaps) Settings.Map = 0;
            for (int mi = 0; mi < nMaps; mi++)
            {
                int id = mi;
                var card = UIK.Img(col, "card" + mi, new Color(0.05f, 0.055f, 0.05f, 0.82f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -36 - mi * 122), new Vector2(0, 110), true);
                var bar = UIK.Img(card.transform, "bar", Pal.Accent, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(6, 0)); bar.rectTransform.pivot = new Vector2(0, 0.5f);
                UIK.Txt(card.transform, "t", L.S(mi == 0 ? "mapname" : "mapname2"), 30, Pal.Text, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(26, -14), new Vector2(-52, -28), true);
                UIK.Txt(card.transform, "i", L.S(mi == 0 ? "mapinfo" : "mapinfo2"), 17, Pal.Dim, TextAnchor.LowerLeft, Vector2.zero, Vector2.one, new Vector2(26, 14), new Vector2(-52, -28), true);
                var sel = UIK.Txt(card.transform, "s", L.S("selected"), 16, Pal.Accent, TextAnchor.UpperRight, Vector2.zero, Vector2.one, new Vector2(-18, -14), new Vector2(-36, -28), true);
                cards.Add((card, bar, sel, id));
                card.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    Sfx.UI(); Settings.Map = id; PlayerPrefs.Save();
                    foreach (var c in cards) { bool on = c.id == Settings.Map; c.bar.enabled = on; c.sel.enabled = on; c.bg.color = new Color(0.05f, 0.055f, 0.05f, on ? 0.88f : 0.55f); }
                });
            }
            foreach (var c in cards) { bool on = c.id == Settings.Map; c.bar.enabled = on; c.sel.enabled = on; c.bg.color = new Color(0.05f, 0.055f, 0.05f, on ? 0.88f : 0.55f); }

            UIK.Txt(col, "sh", L.S("squads"), 20, Pal.Dim, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -290), new Vector2(0, 30), true);
            for (int t = 0; t < 2; t++)
            {
                var c = t == 0 ? Pal.Ally : Pal.Enemy;
                float x0 = t == 0 ? 0 : 290;
                UIK.Txt(col, "th" + t, L.S(t == 0 ? "allies" : "enemies"), 22, c, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x0, -330), new Vector2(270, 32), true);
                for (int i = 0; i < 5; i++)
                {
                    string n = t == 0 ? (i == 0 ? Settings.Callsign : MatchManager.AllyNames[(i - 1) % MatchManager.AllyNames.Length]) : MatchManager.EnemyNames[i % MatchManager.EnemyNames.Length];
                    var row = UIK.Img(col, "r", t == 0 && i == 0 ? new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.3f) : new Color(1, 1, 1, 0.05f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x0, -370 - i * 46), new Vector2(270, 40));
                    UIK.Img(row.transform, "s", c, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(4, 0)).rectTransform.pivot = new Vector2(0, 0.5f);
                    var tx = UIK.Txt(row.transform, "n", n, 22, Pal.Text, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-24, 0), false);
                    tx.fontStyle = FontStyle.Bold;
                    if (t == 0 && i == 0) rosterMe = tx;
                }
            }
            UIK.Txt(root, "foot", "AYD WORKS GAMES  ·  v0.9", 20, Pal.Dim, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 24), new Vector2(420, 30), true);

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
                if (rosterMe != null) rosterMe.text = n;
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
            RenderSettings.fog = true;
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
            "\"Conteiner\" by pbk (Rafael_Oliveira) — CC BY 4.0 (sketchfab.com/3d-models/conteiner-8b5a55e3059d41699e4e3aa9c0c1baa4)\n" +
            "\"Forklift Truck\" by Kray Ewan — CC BY 4.0 (sketchfab.com/3d-models/forklift-truck-1567f5f71c1a40eebe487b1b8f570250)\n" +
            "\"Fort Worden Gun Battery Ash\" by peter54 — CC BY 4.0 (sketchfab.com/3d-models/fort-worden-gun-battery-ash-a0bc6bcaaa234e94855be78c20fb11c1)\n" +
            "\"fps animated carbine\" by DJMaesen — CC BY 4.0 (sketchfab.com/3d-models/fps-animated-carbine-62977bb4c53047a185b9f3a0cdf56b87)\n" +
            "\"Free - Holographic sight game ready\" by Karnaval — CC BY 4.0 (skfb.ly/oWDxG)\n" +
            "Characters & animations: Mixamo (Adobe)\n\n" +
            "Sounds (freesound.org): mnslugger20, 18hiltc, evanboyerman, synth2, gfl7, fastson,\n" +
            "rammbostein, fusionwolf3740, nicktermer, ogsoundfx, qubodup, designerschoice (Nicholas Judy), jaklocke, ceberation,\n" +
            "dirtjm, silverillusionist, baggonotes, patrick_corra, speakwithanimals (rain)\n" +
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
