using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Zone47
{
    /// Generic touch pad: reports down/drag/up.
    public class TouchPad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public Action<PointerEventData> down, up, drag;
        public bool held;
        public int pointerId = -999;
        public void OnPointerDown(PointerEventData e) { if (held) return; held = true; pointerId = e.pointerId; down?.Invoke(e); }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId != pointerId) return; held = false; pointerId = -999; up?.Invoke(e); }
        public void OnDrag(PointerEventData e) { if (e.pointerId != pointerId) return; drag?.Invoke(e); }
        void OnDisable() { if (held) { held = false; up?.Invoke(null); } }
    }

    public static class Gfx
    {
        static Sprite circle, ring;
        public static Sprite Circle => circle ??= Make(false);
        public static Sprite Ring => ring ??= Make(true);
        static Sprite arrow;
        public static Sprite Arrow => arrow ??= MakeArrow();
        static Sprite MakeArrow()
        {
            int n = 64; var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n;      // v: 0 bottom .. 1 top
                float half = (1f - v) * 0.42f;                          // triangle pointing up
                bool notch = v < 0.28f && Mathf.Abs(u) < (0.28f - v) * 1.1f;
                float a = (Mathf.Abs(u) <= half && v > 0.06f && !notch) ? 1f : 0f;
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(px); t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100);
        }
        static Sprite Make(bool isRing)
        {
            int n = 128; var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1; float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((1f - r) * n * 0.5f);
                if (isRing) a *= Mathf.Clamp01((r - 0.86f) * n * 0.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(px); t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100);
        }

        public static Texture2D Vignette()
        {
            int n = 256; var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = x / (n - 1f) * 2 - 1, dy = y / (n - 1f) * 2 - 1;
                float r = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.8f) / 0.6f);
                px[y * n + x] = new Color32(160, 10, 5, (byte)(r * r * 255));
            }
            t.SetPixels32(px); t.Apply(); t.wrapMode = TextureWrapMode.Clamp; return t;
        }

        public static Texture2D Scope()
        {
            int n = 512; var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1; float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((r - 0.97f) * 60f);
                float edge = Mathf.Clamp01(1f - Mathf.Abs(r - 0.97f) * 40f) * 0.6f;
                bool line = (Mathf.Abs(dx) < 0.003f && Mathf.Abs(dy) > 0.04f) || (Mathf.Abs(dy) < 0.003f && Mathf.Abs(dx) > 0.04f);
                bool thick = (Mathf.Abs(dx) < 0.012f && Mathf.Abs(dy) > 0.55f) || (Mathf.Abs(dy) < 0.012f && Mathf.Abs(dx) > 0.55f);
                float v = Mathf.Max(a, Mathf.Max(edge, (line || thick) ? 0.95f : 0f));
                px[y * n + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(v) * 255));
            }
            t.SetPixels32(px); t.Apply(); t.wrapMode = TextureWrapMode.Clamp; return t;
        }
    }

    public class HUD : MonoBehaviour
    {
        public static HUD I;
        MatchManager mm;
        Canvas canvas;
        RectTransform root, controls;

        Image allyBar, enemyBar;
        Text allyScore, enemyScore, timer, ammo, weaponName, grenadeTxt, toast;
        Image hpFill, hpBack;
        RectTransform[] xh = new RectTransform[4];
        Image hitMark; float hitT; bool hitKill;
        RawImage vignette; float dmgT;
        RawImage scopeImg; Image scopeL, scopeR;
        RectTransform feed;
        readonly List<(Text t, float time)> feedItems = new List<(Text, float)>();
        GameObject deathPanel, endPanel, pausePanel;
        Text deathTxt, deathTimer;
        float deathAt;
        Image[] slotImgs = new Image[3];
        Text[] slotTxt = new Text[3];

        // joystick
        RectTransform joyBase, joyKnob; Vector2 joyOrigin;
        TouchPad joyPad;
        public bool JoystickActive => joyPad != null && joyPad.held;
        public bool AimToggled { get; private set; }
        public bool Paused { get; private set; }
        static bool TouchUI => Application.isMobilePlatform;
        Image aimBtnImg;

        public static void Create(MatchManager m)
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(m.transform);
            I = go.AddComponent<HUD>();
            I.mm = m;
            I.Build();
        }

        void OnDestroy() { if (I == this) I = null; Time.timeScale = 1f; }

        void Build()
        {
            canvas = UIK.Canvas("HUDCanvas", 10);
            canvas.transform.SetParent(transform);
            root = UIK.Full(canvas.transform, "root");
            var safe = root.gameObject.AddComponent<SafeArea>();

            // damage vignette & scope (behind everything)
            vignette = UIK.Full(root, "vignette").gameObject.AddComponent<RawImage>();
            vignette.texture = Gfx.Vignette(); vignette.color = new Color(1, 1, 1, 0); vignette.raycastTarget = false;

            var scopeR_ = UIK.Rect(root, "scope", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 1080));
            scopeImg = scopeR_.gameObject.AddComponent<RawImage>(); scopeImg.texture = Gfx.Scope(); scopeImg.raycastTarget = false;
            scopeL = UIK.Img(root, "scopeL", Color.black, new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(-540, 0), Vector2.zero);
            scopeL.rectTransform.pivot = new Vector2(1, 0.5f); scopeL.rectTransform.offsetMax = new Vector2(-540, 0); scopeL.rectTransform.offsetMin = Vector2.zero;
            scopeR = UIK.Img(root, "scopeR", Color.black, new Vector2(0.5f, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            scopeR.rectTransform.offsetMin = new Vector2(540, 0); scopeR.rectTransform.offsetMax = Vector2.zero;
            SetScope(false);

            // crosshair
            for (int i = 0; i < 4; i++)
            {
                var im = UIK.Img(root, "xh" + i, new Color(1, 1, 1, 0.9f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, i < 2 ? new Vector2(3, 16) : new Vector2(16, 3));
                im.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.5f);
                xh[i] = im.rectTransform;
            }
            UIK.Img(root, "dot", new Color(1, 1, 1, 0.9f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3, 3));
            hitMark = UIK.Img(root, "hit", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
            hitMark.color = new Color(1, 1, 1, 0);
            for (int i = 0; i < 4; i++)
            {
                var l = UIK.Img(hitMark.transform, "l", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3, 12));
                l.rectTransform.localRotation = Quaternion.Euler(0, 0, 45 + 90 * i);
                l.rectTransform.anchoredPosition = Quaternion.Euler(0, 0, 45 + 90 * i) * new Vector2(0, 11);
            }
            hitMark.enabled = false;

            // score bar: three separated blocks (allies | clock | enemies) with progress toward the kill target
            var sb = UIK.Rect(root, "scorebar", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(620, 70));
            allyScore = ScoreBlock(sb, true, out allyBar);
            enemyScore = ScoreBlock(sb, false, out enemyBar);
            var clock = UIK.Img(sb, "clock", new Color(0.03f, 0.035f, 0.03f, 0.78f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(118, 70));
            timer = UIK.Txt(clock.transform, "time", "8:00", 34, Pal.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0, 7), Vector2.zero, true);
            UIK.Txt(clock.transform, "goal", (Settings.Turkish ? "HEDEF " : "TARGET ") + MatchManager.KillTarget, 13, Pal.Dim, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(118, 18), true);

            // kill feed
            feed = UIK.Rect(root, "feed", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(380, 200));
            var vl = feed.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childAlignment = TextAnchor.UpperRight; vl.spacing = 6; vl.childControlHeight = false; vl.childControlWidth = false; vl.childForceExpandHeight = false;

            // health
            hpBack = UIK.Img(root, "hpBack", Pal.Panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(380, 10));
            hpFill = UIK.Img(hpBack.transform, "fill", Pal.Text, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, new Vector2(-4, -4));
            hpFill.rectTransform.pivot = new Vector2(0, 0.5f); hpFill.rectTransform.anchoredPosition = new Vector2(2, 0);

            // weapon slots
            string[] names = { "M4A1", "AWP", "GLOCK" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var b = UIK.Img(root, "slot" + i, Pal.Panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2((i - 1) * 128, 78), new Vector2(120, 42), true);
                slotImgs[i] = b;
                slotTxt[i] = UIK.Txt(b.transform, "t", names[i], 20, Pal.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
                var tp = b.gameObject.AddComponent<TouchPad>(); tp.down = e => PInput.select = idx;
            }

            ammo = UIK.Txt(root, "ammo", "30 / 120", 44, Pal.Text, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 30), new Vector2(360, 60), true);
            weaponName = UIK.Txt(root, "wname", "M4A1", 18, Pal.Accent, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 86), new Vector2(360, 30), true);
            toast = UIK.Txt(root, "toast", "", 24, Pal.Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(800, 50), true);

            BuildRadar(); BuildIndicators(); BuildTags();
            bigText = UIK.Txt(root, "big", "", 66, Pal.Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(1400, 160), true);
            bigText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
            killText = UIK.Txt(root, "kill", "", 26, Pal.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -95), new Vector2(900, 60), true);
            killText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
            BuildControls();

            // pause button
            var pb = UIK.Btn(root, "II", new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, -55), new Vector2(84, 70), TogglePause, 40);

            BuildDeath(); BuildPause();
            controls.gameObject.SetActive(TouchUI);
        }

        // ------------------------------------------------------------ touch controls
        void BuildControls()
        {
            controls = UIK.Full(root, "controls");
            controls.SetAsFirstSibling();
            scopeR.transform.SetAsFirstSibling();
            scopeL.transform.SetAsFirstSibling();
            scopeImg.transform.SetAsFirstSibling();
            vignette.transform.SetAsFirstSibling();

            // look area (right 62%)
            var look = UIK.Img(controls, "look", new Color(0, 0, 0, 0), new Vector2(0.38f, 0), new Vector2(1, 0.86f), Vector2.zero, Vector2.zero, true);
            var lp = look.gameObject.AddComponent<TouchPad>();
            lp.drag = e => PInput.look += e.delta * (1080f / Screen.height);

            // joystick area (left 38%)
            var joyArea = UIK.Img(controls, "joyArea", new Color(0, 0, 0, 0), new Vector2(0, 0), new Vector2(0.38f, 0.75f), Vector2.zero, Vector2.zero, true);
            joyBase = UIK.Img(controls, "joyBase", new Color(1, 1, 1, 0.16f), new Vector2(0, 0), new Vector2(0, 0), new Vector2(300, 260), new Vector2(260, 260)).rectTransform;
            joyBase.GetComponent<Image>().sprite = Gfx.Circle;
            var ring = UIK.Img(joyBase, "ring", new Color(1, 1, 1, 0.4f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); ring.sprite = Gfx.Ring;
            joyKnob = UIK.Img(joyBase, "knob", new Color(1, 1, 1, 0.75f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110)).rectTransform;
            joyKnob.GetComponent<Image>().sprite = Gfx.Circle;
            Vector2 home = joyBase.anchoredPosition;
            joyPad = joyArea.gameObject.AddComponent<TouchPad>();
            joyPad.down = e =>
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(controls, e.position, null, out var lp2);
                joyBase.anchoredPosition = lp2 + controls.rect.size * 0.5f; joyOrigin = e.position;
                UpdateJoy(e.position);
            };
            joyPad.drag = e => UpdateJoy(e.position);
            joyPad.up = e => { PInput.move = Vector2.zero; joyKnob.anchoredPosition = Vector2.zero; joyBase.anchoredPosition = home; };

            // buttons
            RoundButton(Settings.Turkish ? "ATEŞ" : "FIRE", new Vector2(-190, 210), 190, Pal.Accent, true, null, fire: true);
            if (Settings.LeftFire) RoundButton(Settings.Turkish ? "ATEŞ " : "FIRE ", new Vector2(0, 0), 130, Pal.Accent, true, null, fire: true, left: true);
            aimBtnImg = RoundButton(Settings.Turkish ? "NİŞAN" : "AIM", new Vector2(-400, 150), 130, Color.white, false, () => { AimToggled = !AimToggled; PInput.aim = AimToggled; });
            RoundButton(Settings.Turkish ? "ZIPLA" : "JUMP", new Vector2(-110, 430), 120, Color.white, false, () => PInput.jump = true);
            RoundButton(Settings.Turkish ? "EĞİL" : "CROUCH", new Vector2(-300, 380), 115, Color.white, false, () => PInput.crouchToggle = true);
            RoundButton(Settings.Turkish ? "ŞARJÖR" : "RELOAD", new Vector2(-440, 330), 105, Color.white, false, () => PInput.reload = true);
            var gb = RoundButton(Settings.Turkish ? "BOMBA" : "GRENADE", new Vector2(-560, 200), 105, Color.white, false, () => PInput.grenade = true);
            grenadeTxt = UIK.Txt(gb.transform, "n", "2", 26, Pal.Accent, TextAnchor.UpperRight, Vector2.one, Vector2.one, new Vector2(-12, -10), new Vector2(40, 30), true);
        }

        Image RoundButton(string label, Vector2 pos, float size, Color c, bool fill, Action onDown, bool fire = false, bool left = false)
        {
            Vector2 anchor = left ? new Vector2(0, 0) : new Vector2(1, 0);
            Vector2 p = left ? new Vector2(150, 520) : pos;
            var img = UIK.Img(controls, label, fill ? new Color(c.r, c.g, c.b, 0.55f) : new Color(0, 0, 0, 0.35f), anchor, anchor, p, new Vector2(size, size), true);
            img.sprite = Gfx.Circle;
            var ring = UIK.Img(img.transform, "ring", new Color(c.r, c.g, c.b, 0.85f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); ring.sprite = Gfx.Ring;
            UIK.Txt(img.transform, "t", label, Mathf.RoundToInt(size * 0.2f), fill ? Color.white : Pal.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            var tp = img.gameObject.AddComponent<TouchPad>();
            if (fire)
            {
                tp.down = e => { PInput.fireHeld = true; img.transform.localScale = Vector3.one * 0.92f; };
                tp.up = e => { PInput.fireHeld = AnyFireHeld(tp); img.transform.localScale = Vector3.one; };
                tp.drag = e => PInput.look += e.delta * (1080f / Screen.height);
                fireButtons.Add(tp);
            }
            else
            {
                tp.down = e => { onDown?.Invoke(); img.transform.localScale = Vector3.one * 0.9f; };
                tp.up = e => img.transform.localScale = Vector3.one;
            }
            return img;
        }
        readonly List<TouchPad> fireButtons = new List<TouchPad>();
        bool AnyFireHeld(TouchPad except) { foreach (var f in fireButtons) if (f != except && f.held) return true; return false; }

        void UpdateJoy(Vector2 screenPos)
        {
            float radius = 110f * Screen.height / 1080f;
            Vector2 d = screenPos - joyOrigin;
            Vector2 v = Vector2.ClampMagnitude(d / radius, 1.25f);
            PInput.move = Vector2.ClampMagnitude(v, 1f) * (v.magnitude > 1.1f ? 1f : 0.84f);
            joyKnob.anchoredPosition = Vector2.ClampMagnitude(v, 1f) * 110f;
        }

        // ------------------------------------------------------------ panels
        void BuildDeath()
        {
            deathPanel = UIK.Full(root, "death").gameObject;
            var bg = UIK.Img(deathPanel.transform, "bg", new Color(0.1f, 0, 0, 0.35f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            deathTxt = UIK.Txt(deathPanel.transform, "t", "", 38, Pal.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1200, 80), true);
            deathTimer = UIK.Txt(deathPanel.transform, "c", "", 26, Pal.Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(800, 60), true);
            deathPanel.SetActive(false);
        }

        void BuildPause()
        {
            pausePanel = UIK.Full(root, "pause").gameObject;
            UIK.Img(pausePanel.transform, "bg", new Color(0, 0, 0, 0.75f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            UIK.Txt(pausePanel.transform, "t", L.S("paused"), 80, Pal.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 240), new Vector2(1000, 120), true);
            UIK.Btn(pausePanel.transform, L.S("resume"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(520, 96), TogglePause, 44, true);
            UIK.Btn(pausePanel.transform, L.S("settings"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(520, 96), () => Boot.I.ShowSettings(canvas.transform, null), 44);
            UIK.Btn(pausePanel.transform, L.S("mainmenu"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -160), new Vector2(520, 96), () => Boot.I.ToMenu(), 44);
            pausePanel.SetActive(false);
        }

        public void TogglePause()
        {
            if (mm.Finished) return;
            Paused = !Paused;
            pausePanel.SetActive(Paused);
            Time.timeScale = Paused ? 0f : 1f;
            AudioListener.pause = Paused;
            PInput.fireHeld = false;
        }

        public void ShowDeath(Actor killer)
        {
            deathPanel.SetActive(true);
            deathAt = Time.time;
            string kn = killer != null && killer != PlayerController.Local ? killer.callsign : "—";
            deathTxt.text = L.S("killedby") + "  <color=#FF5040>" + kn.ToUpper() + "</color>";
            controls.gameObject.SetActive(false);
            PInput.fireHeld = false; PInput.move = Vector2.zero;
        }

        public void HideDeath()
        {
            deathPanel.SetActive(false);
            controls.gameObject.SetActive(TouchUI);
            AimToggled = false; PInput.aim = false;
        }

        public void ShowEnd(int result)
        {
            Time.timeScale = 1f; Paused = false; if (pausePanel) pausePanel.SetActive(false);
            controls.gameObject.SetActive(false);
            deathPanel.SetActive(false);
            PInput.fireHeld = false; PInput.move = Vector2.zero;
            endPanel = UIK.Full(root, "end").gameObject;
            UIK.Img(endPanel.transform, "bg", new Color(0, 0, 0, 0.82f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            string title = result > 0 ? L.S("victory") : result < 0 ? L.S("defeat") : L.S("draw");
            UIK.Txt(endPanel.transform, "title", title, 130, result > 0 ? Pal.Accent : result < 0 ? Pal.Enemy : Pal.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(1400, 160), true);
            UIK.Txt(endPanel.transform, "score", mm.score[0] + "  —  " + mm.score[1], 70, Pal.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -250), new Vector2(800, 90), true);

            for (int t = 0; t < 2; t++)
            {
                var list = new List<Actor>();
                foreach (var a in Actor.All) if (a.team == t) list.Add(a);
                list.Sort((x, y) => y.kills.CompareTo(x.kills));
                float x0 = t == 0 ? -330 : 330;
                UIK.Txt(endPanel.transform, "h" + t, L.S(t == 0 ? "allies" : "enemies") + "      " + L.S("kills") + "   " + L.S("deaths"), 30, t == 0 ? Pal.Ally : Pal.Enemy, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x0, 150), new Vector2(600, 40), true);
                for (int i = 0; i < list.Count; i++)
                {
                    var a = list[i];
                    var row = UIK.Img(endPanel.transform, "r", a.isPlayer ? new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.35f) : new Color(1, 1, 1, 0.06f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x0, 95 - i * 56), new Vector2(600, 50));
                    UIK.Txt(row.transform, "n", a.callsign, 32, Pal.Text, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(20, 0), new Vector2(-40, 0));
                    UIK.Txt(row.transform, "k", a.kills + "      " + a.deaths, 32, Pal.Text, TextAnchor.MiddleRight, Vector2.zero, Vector2.one, new Vector2(-30, 0), new Vector2(-60, 0));
                }
            }
            UIK.Btn(endPanel.transform, L.S("again"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-280, 110), new Vector2(500, 100), () => Boot.I.StartMatch(), 44, true);
            UIK.Btn(endPanel.transform, L.S("mainmenu"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(280, 110), new Vector2(500, 100), () => Boot.I.ToMenu(), 44);
        }

        // ------------------------------------------------------------ events
        public void HitMarker(bool kill, bool head)
        {
            hitT = kill ? 0.45f : 0.18f; hitKill = kill || head;
            hitMark.enabled = true;
            if (kill) Sfx.Play2D(Z47Refs.I.casing, 0.25f, 0.05f);
            else Sfx.Play2D(Z47Refs.I.impact, 0.14f, 0.1f);
        }

        public void Damaged(Vector3 from)
        {
            dmgT = 1f;
            var p = PlayerController.Local; if (p == null) return;
            var ind = indicators[indIdx]; indIdx = (indIdx + 1) % indicators.Length;
            ind.from = from; ind.t = 1.4f;
        }

        public void Fired() { }

        public void KillFeed(Actor killer, Actor victim, bool headshot)
        {
            string kn = killer != null && killer != victim ? Colored(killer) : "";
            string line = (kn != "" ? kn + "  <color=#B0A890>»</color>  " : "") + Colored(victim) + (headshot ? "  <color=#F29E1F>[" + L.S("headshot") + "]</color>" : "");
            var bg = UIK.Img(feed, "k", new Color(0, 0, 0, 0.45f), new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(380, 30));
            var t = UIK.Txt(bg.transform, "t", line, 19, Pal.Text, TextAnchor.MiddleRight, Vector2.zero, Vector2.one, new Vector2(-14, 0), new Vector2(-28, 0));
            t.supportRichText = true;
            bg.transform.SetAsFirstSibling();
            feedItems.Add((t, Time.time));
            if (feedItems.Count > 5) { Destroy(feedItems[0].t.transform.parent.gameObject); feedItems.RemoveAt(0); }
            if (killer != null && killer.isPlayer && victim != killer)
            {
                bool tr = Settings.Turkish;
                killText.text = (headshot ? "<color=#F29E1F>" + L.S("headshot") + "</color>  " : "") + (tr ? "ELİMİNE  " : "ELIMINATED  ") + Colored(victim) + "   <color=#F29E1F>+" + (headshot ? 150 : 100) + "</color>";
                killT = 2f; killText.transform.localScale = Vector3.one * 1.25f;
                string sk = killer.streak == 2 ? (tr ? "ÇİFT LEŞ" : "DOUBLE KILL") : killer.streak == 3 ? (tr ? "ÜÇLÜ LEŞ" : "TRIPLE KILL") : killer.streak == 5 ? (tr ? "DURDURULAMAZ" : "UNSTOPPABLE") : killer.streak == 8 ? (tr ? "EFSANE" : "LEGENDARY") : null;
                if (sk != null) ShowToastBig(sk);
            }
        }

        string Colored(Actor a)
        {
            string c = a.isPlayer ? "F29E1F" : a.team == 0 ? "5AB8FF" : "FF5040";
            return "<color=#" + c + ">" + a.callsign + "</color>";
        }

        Text ScoreBlock(RectTransform parent, bool ally, out Image bar)
        {
            var col = ally ? Pal.Ally : Pal.Enemy;
            float x = ally ? -(59 + 8 + 115) : (59 + 8 + 115);
            var blk = UIK.Img(parent, ally ? "allies" : "enemies", new Color(0.03f, 0.035f, 0.03f, 0.62f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, 0), new Vector2(230, 70));
            var strip = UIK.Img(blk.transform, "strip", col, new Vector2(ally ? 0 : 1, 0), new Vector2(ally ? 0 : 1, 1), Vector2.zero, new Vector2(5, 0));
            UIK.Txt(blk.transform, "lbl", L.S(ally ? "allies" : "enemies"), 15, new Color(col.r, col.g, col.b, 0.9f), ally ? TextAnchor.UpperLeft : TextAnchor.UpperRight,
                Vector2.zero, Vector2.one, new Vector2(ally ? 16 : -16, -9), new Vector2(-32, -18), true);
            var t = UIK.Txt(blk.transform, "score", "0", 40, Color.white, ally ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, new Vector2(ally ? -16 : 16, 2), new Vector2(-32, 0), true);
            var track = UIK.Img(blk.transform, "track", new Color(1, 1, 1, 0.1f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 7), new Vector2(-28, 4));
            bar = UIK.Img(track.transform, "fill", col, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bar.rectTransform.pivot = new Vector2(ally ? 0 : 1, 0.5f);
            bar.rectTransform.localScale = new Vector3(0, 1, 1);
            return t;
        }

        float toastT;
        Text bigText, killText; float bigT, killT;
        public void ShowToastBig(string s) { bigText.text = s; bigT = 1.6f; bigText.transform.localScale = Vector3.one * 1.4f; }

        // ------------------------------------------------------------ radar / minimap
        RectTransform radar, radarRot; RawImage mapImg; Image[] dots = new Image[12];
        const float RadarR = 105f, RadarRange = 45f;          // half-size in px, metres shown from centre to edge
        float Px => RadarR / RadarRange;                       // pixels per metre
        void BuildRadar()
        {
            var bg = UIK.Img(root, "radar", new Color(0.03f, 0.035f, 0.03f, 0.7f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -96), new Vector2(RadarR * 2, RadarR * 2));
            radar = bg.rectTransform;
            bg.gameObject.AddComponent<RectMask2D>();
            radarRot = UIK.Rect(radar, "rot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            radarRot.pivot = new Vector2(0.5f, 0.5f);
            mapImg = UIK.Rect(radarRot, "map", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            mapImg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            mapImg.color = new Color(0.92f, 0.9f, 0.86f, 0.95f); mapImg.raycastTarget = false;
            for (int i = 0; i < dots.Length; i++) { dots[i] = UIK.Img(radarRot, "d", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 10)); dots[i].sprite = Gfx.Circle; dots[i].rectTransform.pivot = new Vector2(0.5f, 0.5f); }
            var frame = UIK.Img(radar, "frame", new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var ol = frame.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(1, 1, 1, 0.25f);
            // border lines
            UIK.Img(radar, "bt", new Color(1, 1, 1, 0.25f), new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 2));
            UIK.Img(radar, "bb", new Color(1, 1, 1, 0.25f), new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 2));
            UIK.Img(radar, "bl", new Color(1, 1, 1, 0.25f), new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(2, 0));
            UIK.Img(radar, "br", new Color(1, 1, 1, 0.25f), new Vector2(1, 0), new Vector2(1, 1), Vector2.zero, new Vector2(2, 0));
            var me = UIK.Img(radar, "me", Pal.Accent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18));
            me.rectTransform.pivot = new Vector2(0.5f, 0.5f); me.rectTransform.anchoredPosition = Vector2.zero;
            me.sprite = Gfx.Arrow;
            me.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
        }

        void UpdateRadar(PlayerController p)
        {
            if (mapImg.texture == null && mm.mapTex != null)
            {
                mapImg.texture = mm.mapTex;
                mapImg.rectTransform.sizeDelta = Vector2.one * mm.mapHalf * 2f * Px;
            }
            float yaw = p.cam.transform.eulerAngles.y;
            radarRot.localRotation = Quaternion.Euler(0, 0, yaw);
            Vector3 me = p.transform.position;
            mapImg.rectTransform.anchoredPosition = new Vector2(mm.mapCenter.x - me.x, mm.mapCenter.z - me.z) * Px;
            int n = 0;
            foreach (var a in Actor.All)
            {
                if (a == p || !a.Alive || n >= dots.Length) continue;
                bool ally = a.team == p.team;
                if (!ally && Time.time - a.lastShotTime >= 2.2f) continue;
                Vector3 rel = a.transform.position - me;
                if (new Vector2(rel.x, rel.z).magnitude > RadarRange * 1.45f) continue;
                var d = dots[n++];
                d.enabled = true;
                d.rectTransform.anchoredPosition = new Vector2(rel.x, rel.z) * Px;
                float fade = ally ? 1f : Mathf.Clamp01((2.2f - (Time.time - a.lastShotTime)) / 0.6f);
                var c = ally ? Pal.Ally : Pal.Enemy; d.color = new Color(c.r, c.g, c.b, fade);
                d.rectTransform.sizeDelta = Vector2.one * (ally ? 10f : 12f);
            }
            for (; n < dots.Length; n++) dots[n].enabled = false;
        }

        // ------------------------------------------------------------ damage direction
        class Ind { public Image img; public Vector3 from; public float t; }
        Ind[] indicators = new Ind[5]; int indIdx;
        void BuildIndicators()
        {
            for (int i = 0; i < indicators.Length; i++)
            {
                var im = UIK.Img(root, "dmgInd", new Color(1f, 0.15f, 0.1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 16));
                indicators[i] = new Ind { img = im, t = 0 };
            }
        }
        void UpdateIndicators(PlayerController p)
        {
            foreach (var ind in indicators)
            {
                if (ind.t <= 0) { ind.img.enabled = false; continue; }
                ind.t -= Time.deltaTime;
                ind.img.enabled = true;
                Vector3 to = (ind.from - p.transform.position).Flat();
                Vector3 f = p.cam.transform.forward.Flat();
                float a = Vector3.SignedAngle(f, to, Vector3.up);
                float r = 190f;
                ind.img.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad)) * r;
                ind.img.rectTransform.localRotation = Quaternion.Euler(0, 0, -a);
                ind.img.color = new Color(1f, 0.15f, 0.1f, Mathf.Clamp01(ind.t) * 0.9f);
            }
        }

        // ------------------------------------------------------------ name tags
        Text[] tags = new Text[10];
        void BuildTags()
        {
            for (int i = 0; i < tags.Length; i++)
            {
                tags[i] = UIK.Txt(root, "tag", "", 18, Pal.Ally, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 30), true);
                tags[i].gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
            }
        }
        void UpdateTags(PlayerController p)
        {
            int n = 0;
            var cam = p.cam;
            foreach (var a in Actor.All)
            {
                if (a == p || !a.Alive || n >= tags.Length || p.Scoped) continue;
                Vector3 head = a.transform.position + Vector3.up * 2.15f;
                Vector3 to = head - cam.transform.position;
                float dist = to.magnitude;
                if (dist > 70f || Vector3.Dot(to, cam.transform.forward) <= 0) continue;
                bool ally = a.team == p.team;
                if (!ally)
                {
                    if (Vector3.Angle(cam.transform.forward, a.ChestPos - cam.transform.position) > 2.5f) continue;
                    if (Ballistics.Cast(cam.transform.position, (a.ChestPos - cam.transform.position).normalized, dist + 1f, p, out var h) && Actor.FromCollider(h.collider) != a) continue;
                }
                Vector3 sp = cam.WorldToScreenPoint(head);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out var lp)) continue;
                var t = tags[n++];
                t.enabled = true;
                t.text = a.callsign;
                t.color = ally ? Pal.Ally : Pal.Enemy;
                t.fontSize = Mathf.RoundToInt(Mathf.Lerp(19, 14, dist / 70f));
                t.rectTransform.anchoredPosition = lp;
            }
            for (; n < tags.Length; n++) tags[n].enabled = false;
        }
        void ShowToast(string s, float t) { toast.text = s; toastT = t; }

        void SetScope(bool on)
        {
            scopeImg.enabled = on; scopeL.enabled = on; scopeR.enabled = on;
        }

        // ------------------------------------------------------------ update
        void Update()
        {
            var p = PlayerController.Local;
            if (p == null) return;

            allyScore.text = mm.score[0].ToString();
            enemyScore.text = mm.score[1].ToString();
            allyBar.rectTransform.localScale = new Vector3(Mathf.Clamp01(mm.score[0] / (float)MatchManager.KillTarget), 1, 1);
            enemyBar.rectTransform.localScale = new Vector3(Mathf.Clamp01(mm.score[1] / (float)MatchManager.KillTarget), 1, 1);
            int s = Mathf.CeilToInt(mm.timeLeft);
            timer.text = (s / 60) + ":" + (s % 60).ToString("00");

            hpFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(p.hp / p.maxHp), 1, 1);
            hpFill.color = p.hp < 35 ? Pal.Enemy : Pal.Text;

            var d = p.Cur;
            ammo.text = p.Mag + "<size=40><color=#9E998C> / " + p.Reserve + "</color></size>";
            ammo.supportRichText = true;
            weaponName.text = d.displayName;
            grenadeTxt.text = p.grenades.ToString();
            for (int i = 0; i < 3; i++)
            {
                bool on = (i == 0 && d.id == "m4") || (i == 1 && d.id == "awp") || (i == 2 && d.id == "glock");
                slotImgs[i].color = on ? new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.8f) : Pal.Panel;
            }
            if (aimBtnImg != null) aimBtnImg.color = AimToggled ? new Color(1, 1, 1, 0.45f) : new Color(0, 0, 0, 0.35f);

            // crosshair
            bool scoped = p.Scoped;
            SetScope(scoped);
            float gap = 8f + p.spreadNow * 9f;
            bool showX = !scoped && !p.Aiming;
            xh[0].anchoredPosition = new Vector2(0, gap + 8); xh[1].anchoredPosition = new Vector2(0, -gap - 8);
            xh[2].anchoredPosition = new Vector2(gap + 8, 0); xh[3].anchoredPosition = new Vector2(-gap - 8, 0);
            foreach (var x in xh) x.gameObject.SetActive(showX && p.Alive);

            if (hitT > 0)
            {
                hitT -= Time.deltaTime;
                foreach (var im in hitMark.GetComponentsInChildren<Image>()) if (im != hitMark) im.color = hitKill ? new Color(1f, 0.3f, 0.2f, hitT * 4f) : new Color(1, 1, 1, hitT * 5f);
                if (hitT <= 0) hitMark.enabled = false;
            }

            dmgT = Mathf.MoveTowards(dmgT, 0, Time.deltaTime * 2.5f);
            float low = p.Alive ? Mathf.Clamp01(1f - p.hp / 40f) * 0.32f : 0f;
            vignette.color = new Color(1, 1, 1, Mathf.Max(dmgT * 0.45f, low));

            if (p.Reloading) toast.text = L.S("reloading");
            else if (toastT > 0) { toastT -= Time.deltaTime; if (toastT <= 0) toast.text = ""; }
            else if (p.Mag == 0 && p.Reserve == 0) toast.text = L.S("noammo");
            else toast.text = "";

            if (deathPanel.activeSelf)
            {
                float left = Mathf.Max(0, 4f - (Time.time - deathAt));
                deathTimer.text = L.S("respawn") + "  " + left.ToString("0.0");
            }

            UpdateRadar(p); UpdateIndicators(p); UpdateTags(p);
            if (!mm.Running && !mm.Finished && mm.countdown > 0f) { bigText.text = Mathf.CeilToInt(mm.countdown).ToString(); bigT = 0.3f; bigText.transform.localScale = Vector3.one * (1f + (mm.countdown % 1f) * 0.4f); }
            if (bigT > 0) { bigT -= Time.deltaTime; bigText.color = new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, Mathf.Clamp01(bigT * 2f)); bigText.transform.localScale = Vector3.Lerp(bigText.transform.localScale, Vector3.one, Time.deltaTime * 8f); }
            else bigText.text = "";
            if (killT > 0) { killT -= Time.deltaTime; killText.color = new Color(1, 1, 1, Mathf.Clamp01(killT * 2f)); killText.transform.localScale = Vector3.Lerp(killText.transform.localScale, Vector3.one, Time.deltaTime * 10f); }
            else killText.text = "";

            // fade kill feed
            for (int i = feedItems.Count - 1; i >= 0; i--)
            {
                if (Time.time - feedItems[i].time > 6f) { Destroy(feedItems[i].t.transform.parent.gameObject); feedItems.RemoveAt(i); }
            }
        }
    }

    /// Keeps HUD inside the iPhone safe area (notch / home bar).
    public class SafeArea : MonoBehaviour
    {
        Rect last;
        void Update()
        {
            var sa = Screen.safeArea;
            if (sa == last) return;
            last = sa;
            var r = (RectTransform)transform;
            Vector2 min = sa.position, max = sa.position + sa.size;
            min.x /= Screen.width; min.y /= Screen.height; max.x /= Screen.width; max.y /= Screen.height;
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero;
        }
    }
}
