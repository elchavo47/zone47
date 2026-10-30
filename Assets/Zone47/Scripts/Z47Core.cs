using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Zone47
{
    // ------------------------------------------------------------------ SETTINGS
    public static class Settings
    {
        public static int Quality { get => PlayerPrefs.GetInt("z_q", 2); set => PlayerPrefs.SetInt("z_q", value); }      // 0..3
        public static int Fps { get => PlayerPrefs.GetInt("z_fps", 60); set => PlayerPrefs.SetInt("z_fps", value); }
        public static float Sens { get => PlayerPrefs.GetFloat("z_sens", 1f); set => PlayerPrefs.SetFloat("z_sens", value); }
        public static float AdsSens { get => PlayerPrefs.GetFloat("z_asens", 0.7f); set => PlayerPrefs.SetFloat("z_asens", value); }
        public static float Music { get => PlayerPrefs.GetFloat("z_mus", 0.6f); set => PlayerPrefs.SetFloat("z_mus", value); }
        public static float Sfx { get => PlayerPrefs.GetFloat("z_sfx", 1f); set => PlayerPrefs.SetFloat("z_sfx", value); }
        public static bool Turkish { get => PlayerPrefs.GetInt("z_tr", Application.systemLanguage == SystemLanguage.Turkish ? 1 : 0) == 1; set => PlayerPrefs.SetInt("z_tr", value ? 1 : 0); }
        public static bool AimAssist { get => PlayerPrefs.GetInt("z_aa", 1) == 1; set => PlayerPrefs.SetInt("z_aa", value ? 1 : 0); }
        public static bool LeftFire { get => PlayerPrefs.GetInt("z_lf", 1) == 1; set => PlayerPrefs.SetInt("z_lf", value ? 1 : 0); }
        public static bool ShowArms { get => PlayerPrefs.GetInt("z_arms3", 1) == 1; set => PlayerPrefs.SetInt("z_arms3", value ? 1 : 0); }
        // phones show the scene darker than the editor: they start brighter (75% here == the old 100%)
        // brighter by default: the old "100%" look is now the starting point, with headroom above it
        public static float Brightness { get => PlayerPrefs.GetFloat("z_bright4", 0.6f); set => PlayerPrefs.SetFloat("z_bright4", value); }
        public static float ExposureValue => Mathf.Lerp(0f, 2.1f, Brightness);
        public static int Difficulty { get => PlayerPrefs.GetInt("z_diff", 1); set => PlayerPrefs.SetInt("z_diff", value); } // 0..2
        // gyroscope: 0 off, 1 only while aiming, 2 always
        public static int Gyro { get => PlayerPrefs.GetInt("z_gyro", 0); set => PlayerPrefs.SetInt("z_gyro", value); }
        public static float GyroSens { get => PlayerPrefs.GetFloat("z_gsens", 1f); set => PlayerPrefs.SetFloat("z_gsens", value); }
        public static bool GyroInvert { get => PlayerPrefs.GetInt("z_ginv", 0) == 1; set => PlayerPrefs.SetInt("z_ginv", value ? 1 : 0); }
        public static bool Mobile => Application.isMobilePlatform;
        public static int Map { get => PlayerPrefs.GetInt("z_map", 0); set => PlayerPrefs.SetInt("z_map", value); }   // 0 desert town, 1 port
        public static string PlayerName { get => PlayerPrefs.GetString("z_name", ""); set => PlayerPrefs.SetString("z_name", value); }
        public static string Callsign => string.IsNullOrWhiteSpace(PlayerName) ? L.S("you") : PlayerName.Trim();

        public static void Apply()
        {
            Application.targetFrameRate = Fps;
            QualitySettings.vSyncCount = 0;
            var urp = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                // phones render at native ~2500px wide; full res + MSAA + SMAA + HDR is what cooked the device.
                // Mobile presets keep the look (same lights/post) but render fewer pixels and cheaper shadows.
                float[] scale = Mobile ? new[] { 0.55f, 0.65f, 0.75f, 0.85f } : new[] { 0.7f, 0.85f, 1f, 1f };
                float[] shadow = Mobile ? new[] { 0f, 22f, 35f, 50f } : new[] { 0f, 35f, 60f, 90f };
                int[] msaa = Mobile ? new[] { 1, 1, 1, 2 } : new[] { 1, 1, 2, 4 };
                urp.renderScale = scale[Quality];
                urp.shadowDistance = shadow[Quality];
                urp.msaaSampleCount = msaa[Quality];
                urp.shadowCascadeCount = Mobile || Quality < 2 ? 1 : 2;
                urp.maxAdditionalLightsCount = Mobile ? 2 : 4;
                // the iOS (Mobile) pipeline ships with HDR off; ACES tonemapping on LDR crushes the image dark
                urp.supportsHDR = true;
            }
            if (RenderSettings.sun != null) RenderSettings.sun.shadows = Mobile && Quality < 3 ? LightShadows.Hard : LightShadows.Soft;
            AudioListener.volume = 1f;
            PlayerPrefs.Save();
        }

        /// SMAA is expensive on phones; FXAA there, SMAA on desktop high+.
        public static AntialiasingMode AA => !Mobile && Quality >= 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.FastApproximateAntialiasing;

        public static bool PostFx => Quality >= 1;
    }

    // ------------------------------------------------------------------ LOCALIZATION
    public static class L
    {
        static readonly Dictionary<string, string[]> T = new Dictionary<string, string[]>
        {
            {"play", new[]{"OYNA","PLAY"}},
            {"settings", new[]{"AYARLAR","SETTINGS"}},
            {"credits", new[]{"EMEĞİ GEÇENLER","CREDITS"}},
            {"back", new[]{"GERİ","BACK"}},
            {"resume", new[]{"DEVAM ET","RESUME"}},
            {"mainmenu", new[]{"ANA MENÜ","MAIN MENU"}},
            {"again", new[]{"TEKRAR OYNA","PLAY AGAIN"}},
            {"victory", new[]{"ZAFER","VICTORY"}},
            {"defeat", new[]{"YENİLGİ","DEFEAT"}},
            {"draw", new[]{"BERABERE","DRAW"}},
            {"graphics", new[]{"GRAFİK","GRAPHICS"}},
            {"low", new[]{"DÜŞÜK","LOW"}},
            {"medium", new[]{"ORTA","MEDIUM"}},
            {"high", new[]{"YÜKSEK","HIGH"}},
            {"ultra", new[]{"ULTRA","ULTRA"}},
            {"fps", new[]{"KARE HIZI","FRAME RATE"}},
            {"bright", new[]{"PARLAKLIK","BRIGHTNESS"}},
            {"sens", new[]{"HASSASİYET","SENSITIVITY"}},
            {"adssens", new[]{"NİŞAN HASSASİYETİ","AIM SENSITIVITY"}},
            {"music", new[]{"MÜZİK","MUSIC"}},
            {"sfx", new[]{"EFEKTLER","EFFECTS"}},
            {"lang", new[]{"DİL","LANGUAGE"}},
            {"arms", new[]{"KOLLAR","ARMS"}},
            {"aimassist", new[]{"NİŞAN YARDIMI","AIM ASSIST"}},
            {"leftfire", new[]{"SOL ATEŞ TUŞU","LEFT FIRE BUTTON"}},
            {"on", new[]{"AÇIK","ON"}},
            {"off", new[]{"KAPALI","OFF"}},
            {"difficulty", new[]{"ZORLUK","DIFFICULTY"}},
            {"easy", new[]{"KOLAY","EASY"}},
            {"normal", new[]{"NORMAL","NORMAL"}},
            {"hard", new[]{"ZOR","HARD"}},
            {"tdm", new[]{"TAKIM ÖLÜM MAÇI · 5v5","TEAM DEATHMATCH · 5v5"}},
            {"respawn", new[]{"YENİDEN DOĞMA","RESPAWN IN"}},
            {"killedby", new[]{"SENİ ÖLDÜREN","KILLED BY"}},
            {"reloading", new[]{"ŞARJÖR DEĞİŞİYOR","RELOADING"}},
            {"noammo", new[]{"MERMİ YOK","NO AMMO"}},
            {"allies", new[]{"BİZİM TİM","ALLIES"}},
            {"enemies", new[]{"DÜŞMAN","ENEMIES"}},
            {"kills", new[]{"L","K"}},
            {"deaths", new[]{"Ö","D"}},
            {"paused", new[]{"DURAKLATILDI","PAUSED"}},
            {"title", new[]{"BÖLGE 47","ZONE 47"}},
            {"firstto", new[]{"İLK 40 LEŞE ULAŞAN KAZANIR","FIRST TO 40 KILLS WINS"}},
            {"headshot", new[]{"KAFADAN","HEADSHOT"}},
            {"you", new[]{"SEN","YOU"}},
            {"deploy", new[]{"SAVAŞA GİR","DEPLOY"}},
            {"callsign", new[]{"ASKER ADI","CALLSIGN"}},
            {"change", new[]{"DEĞİŞTİR","CHANGE"}},
            {"callsign_q", new[]{"ASKER ADIN NE?","YOUR CALLSIGN?"}},
            {"callsign_h", new[]{"Ölüm duyurularında ve skor tablosunda görünür","Shown in the kill feed and scoreboard"}},
            {"callsign_ph", new[]{"Adını yaz","Type your name"}},
            {"callsign_short", new[]{"En az 2 harf","At least 2 letters"}},
            {"ok", new[]{"TAMAM","OK"}},
            {"map", new[]{"HARİTA","MAP"}},
            {"mapname", new[]{"BÖLGE 47 · ÇÖL KASABASI","ZONE 47 · DESERT TOWN"}},
            {"mapinfo", new[]{"5v5 TAKIM ÖLÜM MAÇI · 40 LEŞ · 8 DK","5v5 TEAM DEATHMATCH · 40 KILLS · 8 MIN"}},
            {"newmap", new[]{"YENİ HARİTA","NEW MAP"}},
            {"mapname2", new[]{"LİMAN · KONTEYNER SAHASI","PORT · CONTAINER YARD"}},
            {"mapinfo2", new[]{"YAKIN MESAFE · KONTEYNER ARALARI · DEPOLAR","CLOSE QUARTERS · CONTAINER LANES · WAREHOUSES"}},
            {"selected", new[]{"SEÇİLİ","SELECTED"}},
            {"credits_l", new[]{"Emeği geçenler","Credits"}},
            {"credits_d", new[]{"Kullanılan modeller, sesler ve lisanslar","Models, sounds and licenses used"}},
            {"open", new[]{"AÇ","OPEN"}},
            {"comingsoon", new[]{"YAKINDA","COMING SOON"}},
            {"squads", new[]{"KADROLAR","SQUADS"}},
            {"t_gfx", new[]{"GRAFİK","GRAPHICS"}},
            {"t_ctrl", new[]{"KONTROLLER","CONTROLS"}},
            {"t_audio", new[]{"SES","AUDIO"}},
            {"t_general", new[]{"GENEL","GENERAL"}},
            {"quality", new[]{"Grafik kalitesi","Graphics quality"}},
            {"quality_d", new[]{"Telefon ısınıyorsa ORTA önerilir","Use MEDIUM if your phone runs hot"}},
            {"fps_l", new[]{"Kare hızı","Frame rate"}},
            {"bright_l", new[]{"Parlaklık","Brightness"}},
            {"arms_l", new[]{"FPS kollar","FPS arms"}},
            {"sens_l", new[]{"Bakış hassasiyeti","Look sensitivity"}},
            {"adssens_l", new[]{"Nişan hassasiyeti","Aim sensitivity"}},
            {"gyro_l", new[]{"Jiroskop","Gyroscope"}},
            {"gyro_d", new[]{"Telefonu çevirerek nişan al","Aim by moving your phone"}},
            {"gyrosens_l", new[]{"Jiroskop hassasiyeti","Gyro sensitivity"}},
            {"gyroinv_l", new[]{"Jiroskop yönünü ters çevir","Invert gyroscope"}},
            {"aimassist_l", new[]{"Nişan yardımı","Aim assist"}},
            {"leftfire_l", new[]{"Sol ateş tuşu","Left fire button"}},
            {"layout_l", new[]{"Tuş düzeni","Button layout"}},
            {"layout_d", new[]{"Tuşların yerini ve boyutunu değiştir","Move and resize buttons"}},
            {"edit", new[]{"DÜZENLE","EDIT"}},
            {"music_l", new[]{"Müzik","Music"}},
            {"sfx_l", new[]{"Efektler","Effects"}},
            {"lang_l", new[]{"Dil","Language"}},
            {"diff_l", new[]{"Bot zorluğu","Bot difficulty"}},
            {"aimonly", new[]{"NİŞANDA","AIMING"}},
            {"always", new[]{"HER ZAMAN","ALWAYS"}},
            {"reset", new[]{"SIFIRLA","RESET"}},
            {"save", new[]{"KAYDET","SAVE"}},
            {"cancel", new[]{"İPTAL","CANCEL"}},
            {"size", new[]{"BOYUT","SIZE"}},
            {"layout_hint", new[]{"Tuşu sürükleyip bırak · seçili tuşu BOYUT ile büyüt/küçült","Drag a button to move it · resize the selected one with SIZE"}},
            {"b_fire", new[]{"ATEŞ","FIRE"}},
            {"b_aim", new[]{"NİŞAN","AIM"}},
            {"b_jump", new[]{"ZIPLA","JUMP"}},
            {"b_crouch", new[]{"EĞİL","CROUCH"}},
            {"b_reload", new[]{"ŞARJÖR","RELOAD"}},
            {"b_grenade", new[]{"BOMBA","GRENADE"}},
            {"b_move", new[]{"HAREKET","MOVE"}},
        };
        public static string S(string k) => T.TryGetValue(k, out var v) ? v[Settings.Turkish ? 0 : 1] : k;
    }

    // ------------------------------------------------------------------ COLORS
    public static class Pal
    {
        public static readonly Color Accent = new Color(0.95f, 0.62f, 0.12f);
        public static readonly Color Ally = new Color(0.35f, 0.72f, 1f);
        public static readonly Color Enemy = new Color(1f, 0.32f, 0.25f);
        public static readonly Color Panel = new Color(0.05f, 0.055f, 0.05f, 0.72f);
        public static readonly Color Text = new Color(0.93f, 0.91f, 0.85f);
        public static readonly Color Dim = new Color(0.62f, 0.6f, 0.55f);
    }

    // ------------------------------------------------------------------ UI KIT
    public static class UIK
    {
        public static Canvas Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            UnityEngine.Object.DontDestroyOnLoad(es);
            var t = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (t != null) es.AddComponent(t);
            else es.AddComponent<StandaloneInputModule>();
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = aMin; r.anchorMax = aMax;
            r.pivot = new Vector2((aMin.x + aMax.x) * 0.5f, (aMin.y + aMax.y) * 0.5f);
            r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }

        public static RectTransform Full(Transform parent, string name)
        {
            var r = Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return r;
        }

        public static Image Img(Transform parent, string name, Color c, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, bool raycast = false)
        {
            var r = Rect(parent, name, aMin, aMax, pos, size);
            var i = r.gameObject.AddComponent<Image>();
            i.color = c; i.raycastTarget = raycast;
            return i;
        }

        public static Text Txt(Transform parent, string name, string s, int size, Color c, TextAnchor a, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sz, bool head = false)
        {
            var r = Rect(parent, name, aMin, aMax, pos, sz);
            var t = r.gameObject.AddComponent<Text>();
            t.font = head ? Z47Refs.I.headFont : Z47Refs.I.bodyFont;
            t.text = s; t.fontSize = size; t.color = c; t.alignment = a;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Btn(Transform parent, string label, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Action onClick, int fontSize = 44, bool primary = false)
        {
            var img = Img(parent, "Btn_" + label, primary ? Pal.Accent : new Color(0.08f, 0.085f, 0.08f, 0.85f), aMin, aMax, pos, size, true);
            var b = img.gameObject.AddComponent<Button>();
            var cb = b.colors;
            cb.highlightedColor = new Color(1, 1, 1, 1); cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1);
            b.colors = cb;
            if (!primary)
            {
                var bar = Img(img.transform, "bar", Pal.Accent, new Vector2(0, 0), new Vector2(0, 1), new Vector2(4, 0), new Vector2(8, 0));
                bar.rectTransform.pivot = new Vector2(0, 0.5f);
            }
            Txt(img.transform, "t", label, fontSize, primary ? new Color(0.08f, 0.06f, 0.03f) : Pal.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            b.onClick.AddListener(() => { Sfx.UI(); onClick?.Invoke(); });
            return b;
        }
    }

    // ------------------------------------------------------------------ AUDIO
    public static class Sfx
    {
        static readonly List<AudioSource> pool = new List<AudioSource>();
        static GameObject root;
        static AudioSource music, amb;

        static AudioSource Get()
        {
            if (root == null) { root = new GameObject("SfxPool"); UnityEngine.Object.DontDestroyOnLoad(root); pool.Clear(); }
            foreach (var s in pool) if (s != null && !s.isPlaying) return s;
            if (pool.Count >= 40) return pool[UnityEngine.Random.Range(0, pool.Count)];
            var go = new GameObject("src"); go.transform.SetParent(root.transform);
            var a = go.AddComponent<AudioSource>(); a.playOnAwake = false;
            go.AddComponent<AudioLowPassFilter>().cutoffFrequency = 22000f;
            pool.Add(a);
            return a;
        }

        public static void Play2D(AudioClip c, float vol = 1f, float pitchVar = 0.04f)
        {
            if (c == null) return;
            var s = Get();
            s.transform.position = Vector3.zero;
            s.spatialBlend = 0f; s.clip = c; s.volume = vol * Settings.Sfx;
            s.GetComponent<AudioLowPassFilter>().cutoffFrequency = 22000f;
            s.pitch = 1f + UnityEngine.Random.Range(-pitchVar, pitchVar);
            s.Play();
        }

        public static void Play3D(AudioClip c, Vector3 pos, float vol = 1f, float minD = 4f, float maxD = 110f, float pitchVar = 0.06f)
        {
            if (c == null) return;
            var s = Get();
            s.transform.position = pos;
            s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = minD; s.maxDistance = maxD; s.dopplerLevel = 0f;
            s.clip = c; s.volume = vol * Settings.Sfx;
            // distant sounds lose their highs, like real gunfire across a town
            float dist = Vector3.Distance(pos, ListenerPos);
            s.GetComponent<AudioLowPassFilter>().cutoffFrequency = Mathf.Lerp(22000f, 1800f, Mathf.InverseLerp(12f, 90f, dist));
            s.pitch = 1f + UnityEngine.Random.Range(-pitchVar, pitchVar);
            s.Play();
        }

        public static Vector3 ListenerPos;

        public static void UI() { Play2D(Z47Refs.I.dryFire, 0.35f, 0.1f); }

        public static void Music(AudioClip c, float vol)
        {
            if (root == null) Get();
            if (music == null) { music = root.AddComponent<AudioSource>(); music.loop = true; music.spatialBlend = 0; }
            if (music.clip != c) { music.clip = c; if (c != null) music.Play(); }
            music.volume = vol * Settings.Music;
            if (c == null) music.Stop();
        }

        public static void Ambience(AudioClip c, float vol)
        {
            if (root == null) Get();
            if (amb == null) { amb = root.AddComponent<AudioSource>(); amb.loop = true; amb.spatialBlend = 0; }
            if (amb.clip != c) { amb.clip = c; if (c != null) amb.Play(); }
            amb.volume = vol * Settings.Sfx;
            if (c == null) amb.Stop();
        }
    }

    // ------------------------------------------------------------------ FX
    public static class FX
    {
        static ParticleSystem MakePS(string name, Material mat, Vector3 pos, Quaternion rot, float life)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(pos, rot);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = false; main.duration = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission; em.rateOverTime = 0;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = mat;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            UnityEngine.Object.Destroy(go, life);
            return ps;
        }

        static Gradient Fade(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            return g;
        }

        public static void Impact(Vector3 p, Vector3 n, bool flesh)
        {
            var R = Z47Refs.I;
            var rot = Quaternion.LookRotation(n);
            // sparks / blood
            var ps = MakePS("impact", R.sparkMat, p + n * 0.02f, rot, 1.5f);
            var m = ps.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(flesh ? 1f : 3f, flesh ? 3f : 9f);
            m.startSize = new ParticleSystem.MinMaxCurve(flesh ? 0.05f : 0.02f, flesh ? 0.12f : 0.05f);
            m.startColor = flesh ? new Color(0.45f, 0.03f, 0.02f) : new Color(1f, 0.75f, 0.35f);
            m.gravityModifier = 1.4f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35; sh.radius = 0.01f;
            var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0, (short)(flesh ? 10 : 14)) });
            if (!flesh) { var rr = ps.GetComponent<ParticleSystemRenderer>(); rr.renderMode = ParticleSystemRenderMode.Stretch; rr.velocityScale = 0.04f; rr.lengthScale = 1f; }
            ps.Play();
            // dust puff
            var d = MakePS("dust", R.smokeMat, p + n * 0.05f, rot, 2.5f);
            var dm = d.main;
            dm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            dm.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            dm.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            dm.startColor = flesh ? new Color(0.35f, 0.05f, 0.04f, 0.6f) : new Color(0.62f, 0.6f, 0.55f, 0.55f);
            var ds = d.shape; ds.shapeType = ParticleSystemShapeType.Cone; ds.angle = 25; ds.radius = 0.02f;
            var de = d.emission; de.SetBursts(new[] { new ParticleSystem.Burst(0, 3) });
            var col = d.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, Color.white);
            var sz = d.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.6f));
            d.Play();
        }

        static Transform holeRoot;
        static readonly Queue<GameObject> holes = new Queue<GameObject>();
        public static void ClearHoles() { if (holeRoot != null) UnityEngine.Object.Destroy(holeRoot.gameObject); holeRoot = null; holes.Clear(); }
        public static void Hole(RaycastHit hit)
        {
            if (Settings.Quality == 0) return;
            if (holeRoot == null) holeRoot = new GameObject("BulletHoles").transform;
            GameObject q;
            if (holes.Count >= 60) { q = holes.Dequeue(); if (q == null) return; }
            else
            {
                q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                UnityEngine.Object.Destroy(q.GetComponent<Collider>());
                q.name = "hole"; q.transform.SetParent(holeRoot);
            }
            holes.Enqueue(q);
            q.transform.position = hit.point + hit.normal * 0.006f;
            q.transform.rotation = Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0, 0, UnityEngine.Random.Range(0, 360));
            q.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.07f, 0.11f);
            var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = Z47Refs.I.holeMat; r.shadowCastingMode = ShadowCastingMode.Off;
        }

        public static void Tracer(Vector3 a, Vector3 b)
        {
            var go = new GameObject("tracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Z47Refs.I.tracerMat;
            lr.positionCount = 2;
            Vector3 dir = (b - a);
            float len = Mathf.Min(dir.magnitude, 6f);
            Vector3 start = a + dir.normalized * Mathf.Min(1.5f, dir.magnitude * 0.3f);
            lr.SetPosition(0, start); lr.SetPosition(1, start + dir.normalized * len);
            lr.startWidth = 0.025f; lr.endWidth = 0.008f;
            lr.startColor = new Color(1f, 0.85f, 0.5f, 0.9f); lr.endColor = new Color(1f, 0.6f, 0.2f, 0f);
            lr.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<TracerFly>().Init(start, dir.normalized, dir.magnitude, len);
        }

        static readonly Queue<Transform> flashes = new Queue<Transform>();
        /// Short-lived muzzle flash for third-person weapons (pooled).
        public static void MuzzleFlash(Vector3 p, Vector3 dir, float scale)
        {
            Transform f = null;
            if (flashes.Count >= 12) { f = flashes.Dequeue(); if (f == null) f = null; }
            if (f == null)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                UnityEngine.Object.Destroy(q.GetComponent<Collider>());
                q.name = "mflash";
                var r = q.GetComponent<Renderer>(); r.sharedMaterial = Z47Refs.I.flashMat; r.shadowCastingMode = ShadowCastingMode.Off;
                q.AddComponent<FlashLife>();
                f = q.transform;
            }
            flashes.Enqueue(f);
            var cam = Camera.main;
            f.position = p + dir * 0.05f;
            f.rotation = cam != null ? Quaternion.LookRotation(f.position - cam.transform.position) * Quaternion.Euler(0, 0, UnityEngine.Random.Range(0, 360)) : Quaternion.LookRotation(dir);
            f.localScale = Vector3.one * scale * UnityEngine.Random.Range(0.8f, 1.2f);
            f.gameObject.SetActive(true);
            f.GetComponent<FlashLife>().t = 0.05f;
        }

        public static void Explosion(Vector3 p)
        {
            var R = Z47Refs.I;
            var f = MakePS("fire", R.fireMat, p + Vector3.up * 0.3f, Quaternion.identity, 3f);
            var m = f.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
            m.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            m.startColor = new Color(1f, 0.6f, 0.25f);
            var sh = f.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.5f;
            var em = f.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0, 26) });
            var col = f.colorOverLifetime; col.enabled = true; col.color = Fade(new Color(1f, 0.9f, 0.6f), new Color(0.6f, 0.15f, 0.05f));
            var sz = f.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.5f, 1, 1.4f));
            f.Play();

            var s = MakePS("smoke", R.smokeMat, p + Vector3.up * 0.5f, Quaternion.identity, 8f);
            var sm = s.main;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 3f);
            sm.startSize = new ParticleSystem.MinMaxCurve(2f, 4f);
            sm.startColor = new Color(0.25f, 0.23f, 0.21f, 0.75f);
            sm.gravityModifier = -0.05f;
            var ss = s.shape; ss.shapeType = ParticleSystemShapeType.Hemisphere; ss.radius = 0.8f;
            var se = s.emission; se.SetBursts(new[] { new ParticleSystem.Burst(0, 18) });
            var sc = s.colorOverLifetime; sc.enabled = true; sc.color = Fade(Color.white, Color.white);
            var szz = s.sizeOverLifetime; szz.enabled = true; szz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.8f));
            s.Play();

            var deb = MakePS("debris", R.sparkMat, p + Vector3.up * 0.2f, Quaternion.identity, 3f);
            var dm = deb.main;
            dm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            dm.startSpeed = new ParticleSystem.MinMaxCurve(5f, 14f);
            dm.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            dm.startColor = new Color(1f, 0.7f, 0.3f);
            dm.gravityModifier = 1.5f;
            var dsh = deb.shape; dsh.shapeType = ParticleSystemShapeType.Hemisphere; dsh.radius = 0.3f;
            var dem = deb.emission; dem.SetBursts(new[] { new ParticleSystem.Burst(0, 30) });
            var dr = deb.GetComponent<ParticleSystemRenderer>(); dr.renderMode = ParticleSystemRenderMode.Stretch; dr.velocityScale = 0.05f;
            deb.Play();

            var lg = new GameObject("boomLight");
            lg.transform.position = p + Vector3.up;
            var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.6f, 0.3f); l.range = 14; l.intensity = 12;
            lg.AddComponent<LightFade>().life = 0.35f;
        }
    }

    public class FlashLife : MonoBehaviour
    {
        public float t;
        void Update() { t -= Time.deltaTime; if (t <= 0) gameObject.SetActive(false); }
    }

    public class TracerFly : MonoBehaviour
    {
        Vector3 s, d; float dist, len, t; LineRenderer lr;
        public void Init(Vector3 start, Vector3 dir, float distance, float length) { s = start; d = dir; dist = distance; len = length; lr = GetComponent<LineRenderer>(); }
        void Update()
        {
            t += Time.deltaTime * 320f;
            float a = Mathf.Min(t, dist), b = Mathf.Min(t + len, dist);
            lr.SetPosition(0, s + d * a); lr.SetPosition(1, s + d * b);
            if (t >= dist) Destroy(gameObject);
        }
    }

    public class LightFade : MonoBehaviour
    {
        public float life = 0.05f; float t; Light l; float i0;
        void Start() { l = GetComponent<Light>(); i0 = l.intensity; }
        void Update() { t += Time.deltaTime; l.intensity = Mathf.Lerp(i0, 0, t / life); if (t >= life) Destroy(gameObject); }
    }

    // ------------------------------------------------------------------ UTIL
    public static class Util
    {
        public static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        public static void SetShadows(GameObject g, ShadowCastingMode m)
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = m;
        }

        public static float GroundY(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out var h, 200f, ~0, QueryTriggerInteraction.Ignore)) return h.point.y;
            return 0f;
        }
    }
}
