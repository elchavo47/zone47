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
        /// Circle split as a 9-slice: any rect drawn with it gets fully rounded ends (pill switches).
        static Sprite pill;
        public static Sprite Pill => pill ??= Sprite.Create(Circle.texture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(63, 63, 63, 63));
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

        /// PUBG-style hit direction marker: a soft triangle pointing up (outwards), brightest at the tip.
        static Sprite dmgTri;
        public static Sprite DmgTri => dmgTri ??= MakeDmgTri();
        static Sprite MakeDmgTri()
        {
            int w = 128, h = 96; var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            Vector2 A = new Vector2(0.5f, 0.94f), B = new Vector2(0.06f, 0.1f), C = new Vector2(0.94f, 0.1f);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float acc = 0f, glow = 0f;
                for (int sy = 0; sy < 3; sy++) for (int sx = 0; sx < 3; sx++)
                {
                    var p = new Vector2((x + (sx + 0.5f) / 3f) / w, (y + (sy + 0.5f) / 3f) / h);
                    float d = Mathf.Min(EdgeDist(p, A, B), Mathf.Min(EdgeDist(p, B, C), EdgeDist(p, C, A)));
                    bool inside = Inside(p, A, B, C);
                    if (inside) acc += 1f;
                    glow += inside ? 1f : Mathf.Clamp01(1f - d / 0.07f) * 0.45f;
                }
                acc /= 9f; glow /= 9f;
                float v = (y + 0.5f) / h;
                float a = Mathf.Max(acc * Mathf.Lerp(0.55f, 1f, v), glow * 0.5f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
            }
            t.SetPixels32(px); t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100);
        }
        static float Cross(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(a, b, p), d2 = Cross(b, c, p), d3 = Cross(c, a, p);
            return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
        }
        static float EdgeDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a; float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * k)).magnitude;
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

        /// Blood on the screen edges: noisy dark-red splatter that thickens toward the corners.
        static Texture2D blood;
        public static Texture2D Blood()
        {
            if (blood != null) return blood;
            int w = 320, h = 180; var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            var rnd = new System.Random(47);
            var spots = new List<Vector3>();
            for (int i = 0; i < 26; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                float k = 1f / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
                Vector2 c = dir * k * (0.88f + (float)rnd.NextDouble() * 0.16f);
                spots.Add(new Vector3(c.x, c.y, 0.06f + (float)rnd.NextDouble() * 0.14f));
            }
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float dx = x / (w - 1f) * 2 - 1, dy = y / (h - 1f) * 2 - 1;
                float edge = Mathf.Max(Mathf.Pow(Mathf.Abs(dx), 2.6f), Mathf.Pow(Mathf.Abs(dy), 2.8f)) * 0.75f + new Vector2(dx * 0.8f, dy).magnitude * 0.25f;
                float n = Mathf.PerlinNoise(x * 0.035f, y * 0.035f) * 0.55f + Mathf.PerlinNoise(x * 0.11f + 30f, y * 0.11f) * 0.3f + Mathf.PerlinNoise(x * 0.3f, y * 0.3f + 70f) * 0.15f;
                float v = edge + (n - 0.5f) * 0.6f;
                foreach (var sp in spots)
                {
                    float d = new Vector2((dx - sp.x) * 1.7f, dy - sp.y).magnitude;
                    v = Mathf.Max(v, (1f - d / sp.z) * 1.1f + (n - 0.5f) * 0.45f);
                }
                // crisp blotches on top of a soft red haze toward the edges
                float crisp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.74f, 0.80f, v));
                float haze = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.95f, edge));
                float a = Mathf.Max(crisp * 0.95f, haze * 0.4f);
                float dark = Mathf.Clamp01((v - 0.82f) * 2.5f);
                byte r = crisp > 0.01f ? (byte)Mathf.Lerp(125, 55, dark) : (byte)95;
                byte g = crisp > 0.01f ? (byte)Mathf.Lerp(4, 0, dark) : (byte)0;
                px[y * w + x] = new Color32(r, g, 0, (byte)(Mathf.Clamp01(a) * 255));
            }
            t.SetPixels32(px); t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return blood = t;
        }

        /// Sniper scope: crisp black reticle with mil-dots and thick posts, red centre dot,
        /// dark tube rim and a faint glass reflection.
        static Texture2D scope;
        public const float ScopeOuterA = 0.32f;
        static Texture2D housing;
        /// Scope body around the lens (drawn 1500px wide, lens edge at r=0.72): matte metal tube with a lit bevel,
        /// a ribbed rubber eyecup and an elevation turret on top. Outside is transparent so the world stays visible.
        public static Texture2D ScopeHousing()
        {
            if (housing != null) return housing;
            int n = 1024; var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n]; float pxw = 2f / n;
            Vector2 light = new Vector2(-0.55f, 0.83f);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1; float r = Mathf.Sqrt(dx * dx + dy * dy);
                Vector2 nrm = r > 0.0001f ? new Vector2(dx, dy) / r : Vector2.up;
                float ang = Mathf.Atan2(dy, dx);
                float a = 0f; float lum = 0f;
                const float rIn = 0.715f, rBody = 0.84f, rCup = 0.93f;
                if (r >= rIn - pxw && r <= rCup + pxw)
                {
                    a = Mathf.Clamp01((r - (rIn - pxw)) / pxw) * Mathf.Clamp01((rCup + pxw - r) / pxw);
                    float lit = Vector2.Dot(nrm, light);
                    if (r < rIn + 0.018f) lum = 0.02f;                                         // black inner lip
                    else if (r < rBody)
                    {
                        float u = (r - rIn) / (rBody - rIn);                                    // body: rounded bevel catching the light
                        float bevel = Mathf.Sin(u * Mathf.PI);
                        lum = 0.07f + 0.09f * bevel * (0.5f + 0.5f * lit) + 0.05f * Mathf.Pow(Mathf.Max(0f, lit), 6f) * bevel;
                    }
                    else
                    {
                        float u = (r - rBody) / (rCup - rBody);                                 // rubber eyecup with ribs
                        float rib = 0.5f + 0.5f * Mathf.Cos(ang * 90f);
                        lum = 0.035f + 0.03f * rib + 0.03f * Mathf.Sin(u * Mathf.PI) * (0.5f + 0.5f * lit);
                        if (u < 0.06f) lum *= 0.6f;
                    }
                }
                // elevation turret on top, windage turret on the right
                float tw = 0.075f;
                bool top = Mathf.Abs(dx) < tw && dy > 0.8f && dy < 0.985f;
                bool side = Mathf.Abs(dy) < tw && dx > 0.8f && dx < 0.97f;
                if ((top || side) && r > rCup - 0.02f)
                {
                    float along = top ? dy : dx, across = top ? dx : dy;
                    float edge = Mathf.Clamp01((tw - Mathf.Abs(across)) / pxw) * Mathf.Clamp01(((top ? 0.985f : 0.97f) - along) / pxw);
                    float knurl = 0.5f + 0.5f * Mathf.Cos(across * 260f);
                    float l2 = 0.06f + 0.05f * knurl + 0.06f * (1f - Mathf.Abs(across) / tw) * (top ? 1f : 0.6f);
                    if (edge > a) { a = edge; lum = l2; }
                }
                // soft contact shadow just outside the eyecup
                if (a < 0.01f && r > rCup && r < rCup + 0.06f) { a = 0.35f * (1f - (r - rCup) / 0.06f); lum = 0f; }
                byte L = (byte)(Mathf.Clamp01(lum) * 255f);
                px[y * n + x] = new Color32(L, L, (byte)Mathf.Min(255, L + 2), (byte)(Mathf.Clamp01(a) * 255f));
            }
            t.SetPixels32(px); t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return housing = t;
        }
        public static Texture2D Scope()
        {
            if (scope != null) return scope;
            int n = 1024; var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            float pxw = 2f / n;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1; float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);
                // tube: solid outside, steep dark falloff on the inner rim
                // outside the tube the world stays visible (blurred by the scope DoF), only dimmed a bit
                float rim = r > 1f ? ScopeOuterA : Mathf.Clamp01((r - 0.965f) / pxw);
                float shade = r < 1f ? Mathf.Pow(Mathf.Clamp01((r - 0.72f) / 0.27f), 2.2f) * 0.9f : 0f;
                // reticle
                float thin = 0.0028f, thick = 0.016f;
                float lv = ay > 0.035f ? Mathf.Clamp01((thin - ax) / pxw + 0.5f) : 0f;
                float lh = ax > 0.035f ? Mathf.Clamp01((thin - ay) / pxw + 0.5f) : 0f;
                float pv = ay > 0.5f ? Mathf.Clamp01((thick - ax) / pxw + 0.5f) : 0f;
                float ph = ax > 0.5f ? Mathf.Clamp01((thick - ay) / pxw + 0.5f) : 0f;
                float dots = 0f;
                if (ax < 0.02f || ay < 0.02f) for (int k = 1; k <= 5; k++)
                {
                    float o = k * 0.085f;
                    dots = Mathf.Max(dots, Mathf.Clamp01((0.0075f - new Vector2(ax, ay - o).magnitude) / pxw + 0.5f));
                    dots = Mathf.Max(dots, Mathf.Clamp01((0.0075f - new Vector2(ax - o, ay).magnitude) / pxw + 0.5f));
                }
                float ret = Mathf.Max(Mathf.Max(lv, lh), Mathf.Max(Mathf.Max(pv, ph), dots));
                float red = Mathf.Clamp01((0.0055f - r) / pxw + 0.5f);
                // faint reflection arc, top-left
                float refl = Mathf.Clamp01(1f - Mathf.Abs(r - 0.8f) / 0.06f) * Mathf.Clamp01((-dx + dy) * 1.2f - 0.4f) * 0.07f;
                float a = Mathf.Max(Mathf.Max(rim, shade), ret);
                Color c = new Color(0, 0, 0, a);
                if (refl > 0f && a < 0.5f) c = new Color(0.8f, 0.9f, 1f, Mathf.Max(a, refl));
                if (red > 0f) c = Color.Lerp(c, new Color(1f, 0.1f, 0.05f, 1f), red);
                px[y * n + x] = c;
            }
            t.SetPixels32(px); t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return scope = t;
        }
    }

    /// Where the touch buttons sit. Saved per button (centre position + size) and editable in the layout editor.
    public static class HudLayout
    {
        public class Item { public string id, label; public bool left, accent; public Vector2 pos; public float size, alpha = 1f; public Vector2 defPos; public float defSize; }

        public static Item[] Defaults() => new[]
        {
            new Item { id = "move",    label = "b_move",    left = true,  pos = new Vector2(430, 390),  size = 260 },
            new Item { id = "fire",    label = "b_fire",    left = false, pos = new Vector2(-285, 305), size = 190, accent = true },
            new Item { id = "fire2",   label = "b_fire",    left = true,  pos = new Vector2(215, 585),  size = 130, accent = true },
            new Item { id = "aim",     label = "b_aim",     left = false, pos = new Vector2(-465, 215), size = 130 },
            new Item { id = "jump",    label = "b_jump",    left = false, pos = new Vector2(-170, 490), size = 120 },
            new Item { id = "crouch",  label = "b_crouch",  left = false, pos = new Vector2(-357, 437), size = 115 },
            new Item { id = "reload",  label = "b_reload",  left = false, pos = new Vector2(-492, 382), size = 105 },
            new Item { id = "grenade", label = "b_grenade", left = false, pos = new Vector2(-612, 252), size = 105 },
            new Item { id = "prone",   label = "b_prone",   left = false, pos = new Vector2(-285, 600), size = 95 },
            new Item { id = "leanl",   label = "b_leanl",   left = true,  pos = new Vector2(340, 600),  size = 90 },
            new Item { id = "leanr",   label = "b_leanr",   left = true,  pos = new Vector2(450, 600),  size = 90 },
        };

        public static Item[] Load()
        {
            var a = Defaults();
            foreach (var it in a)
            {
                it.defPos = it.pos; it.defSize = it.size;
                var s = PlayerPrefs.GetString("z_L_" + it.id, "");
                var p = s.Split(';');
                if (p.Length >= 4 && float.TryParse(p[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var al)) it.alpha = Mathf.Clamp(al, 0.05f, 1f);
                if (p.Length >= 3 && float.TryParse(p[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
                    && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y)
                    && float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z))
                { it.pos = new Vector2(x, y); it.size = z; }
            }
            return a;
        }

        public static void Save(Item[] items)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var it in items) PlayerPrefs.SetString("z_L_" + it.id, it.pos.x.ToString(ci) + ";" + it.pos.y.ToString(ci) + ";" + it.size.ToString(ci) + ";" + it.alpha.ToString(ci));
            PlayerPrefs.Save();
        }

        public static Item Get(Item[] a, string id) { foreach (var it in a) if (it.id == id) return it; return null; }
        public static Vector2 Anchor(Item it) => it.left ? Vector2.zero : new Vector2(1, 0);
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
        RawImage scopeImg, scopeLens, scopeHousing; Image scopeLensMask; Image scopeL, scopeR, scopeBlink; float blinkT; bool wasScoped;
        Image redDot, redGlow;
        RectTransform full, scopeGroup; Image centerDot, scopeT, scopeB; Vector2 scopeDrift; Vector3 lastFwd;
        AudioSource breathSrc, heartSrc; float lowHpT; float lastSat = -999f;
        string cAmmoWep;
        int cMag = -1, cRes = -1, cS0 = -1, cS1 = -1, cTime = -1, cGren = -1; string cWep;
        RectTransform feed;
        readonly List<(Text t, float time)> feedItems = new List<(Text, float)>();
        GameObject deathPanel, endPanel, pausePanel;
        Text deathTxt, deathTimer;
        // squad card (PUBG style), top-left above the kill feed: number, name, health with damage trail, kills, respawn timer
        class SquadRow { public Actor a; public Image bg, stripe, fill, trail; public Text num, name, kills, dead; public RectTransform fillRt, trailRt; public GameObject bar; public bool wasAlive = true; public float shown = 1f; public int cK = -1, cDead = -1; public Color col; }
        readonly List<SquadRow> squad = new List<SquadRow>();
        RectTransform squadRoot; Text squadAlive; int cAlive = -1;
        static readonly Color[] SquadCols = { new Color(0.98f, 0.82f, 0.2f), new Color(0.95f, 0.5f, 0.15f), new Color(0.3f, 0.65f, 1f), new Color(0.35f, 0.85f, 0.4f), new Color(0.85f, 0.4f, 0.95f), new Color(0.9f, 0.9f, 0.9f) };
        public const float SquadW = 300f, SquadHead = 28f, SquadGap = 3f;
        public static float SquadRowH => MatchManager.AllySize > 5 ? 21f : 30f;      // 10-man squads get compact rows
        static int SquadFont => MatchManager.AllySize > 5 ? 12 : 15;
        public static float SquadHeight(int n) => SquadHead + 6f + n * (SquadRowH + SquadGap);
        GameObject reloadBox; Text reloadTxt, reloadHint; RectTransform reloadFill; int cReload = -1;
        float deathAt;
        Image[] slotImgs = new Image[5];
        Text[] slotTxt = new Text[5];
        RawImage[] slotIcons = new RawImage[5];
        int slotN = 3;

        // joystick
        RectTransform joyBase, joyKnob; Vector2 joyOrigin;
        TouchPad joyPad;
        public bool JoystickActive => joyPad != null && joyPad.held;
        public bool AimToggled { get; private set; }
        public bool Paused { get; private set; }
        static bool TouchUI => Application.isMobilePlatform;
        Image aimBtnImg, leanLImg, leanRImg;

        public static void Create(MatchManager m)
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(m.transform);
            I = go.AddComponent<HUD>();
            I.mm = m;
            I.Build();
        }

        void OnDestroy()
        {
            if (I == this) I = null; Time.timeScale = 1f;
            if (Boot.Exposure != null) Boot.Exposure.saturation.Override(-8f);
        }

        void Build()
        {
            canvas = UIK.Canvas("HUDCanvas", 10);
            canvas.transform.SetParent(transform);
            // full-screen layer (ignores the notch safe area) for scope, blood and fades
            full = UIK.Full(canvas.transform, "full");
            root = UIK.Full(canvas.transform, "root");
            var safe = root.gameObject.AddComponent<SafeArea>();

            vignette = UIK.Full(full, "blood").gameObject.AddComponent<RawImage>();
            vignette.texture = Gfx.Blood(); vignette.color = new Color(1, 1, 1, 0); vignette.raycastTarget = false;

            // scope group moves as one piece (drift with look/movement); black bars overshoot the screen so no gaps show
            scopeGroup = UIK.Full(full, "scopeGroup");
            // lens: what the scope camera sees, cut to a circle (PUBG style: world outside stays visible)
            scopeLensMask = UIK.Img(scopeGroup, "lens", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(825, 825));
            scopeLensMask.sprite = Gfx.Circle; scopeLensMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            scopeLens = UIK.Rect(scopeLensMask.transform, "rt", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
            scopeLens.raycastTarget = false;
            var scopeR_ = UIK.Rect(scopeGroup, "scope", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(840, 840));
            scopeImg = scopeR_.gameObject.AddComponent<RawImage>(); scopeImg.texture = Gfx.Scope(); scopeImg.raycastTarget = false;
            scopeHousing = UIK.Rect(scopeGroup, "housing", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1175, 1175)).gameObject.AddComponent<RawImage>();
            scopeHousing.texture = Gfx.ScopeHousing(); scopeHousing.raycastTarget = false;
            Color outer = new Color(0, 0, 0, Gfx.ScopeOuterA);
            scopeL = UIK.Img(scopeGroup, "scopeL", outer, new Vector2(0, 0), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            scopeL.rectTransform.offsetMin = new Vector2(-400, -400); scopeL.rectTransform.offsetMax = new Vector2(-420, 400);
            scopeR = UIK.Img(scopeGroup, "scopeR", outer, new Vector2(0.5f, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            scopeR.rectTransform.offsetMin = new Vector2(420, -400); scopeR.rectTransform.offsetMax = new Vector2(400, 400);
            scopeT = UIK.Img(scopeGroup, "scopeT", outer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 420 + 200), new Vector2(840, 400));
            scopeB = UIK.Img(scopeGroup, "scopeB", outer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -420 - 200), new Vector2(840, 400));
            scopeBlink = UIK.Img(full, "blink", new Color(0, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            SetScope(false);

            // crosshair: on the full-screen layer (not the safe-area one), so its centre is the real screen centre the bullets use
            for (int i = 0; i < 4; i++)
            {
                var im = UIK.Img(full, "xh" + i, new Color(1, 1, 1, 0.9f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, i < 2 ? new Vector2(3, 16) : new Vector2(16, 3));
                im.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.5f);
                xh[i] = im.rectTransform;
            }
            centerDot = UIK.Img(full, "dot", new Color(1, 1, 1, 0.9f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3, 3));
            redGlow = UIK.Img(full, "redGlow", new Color(1f, 0.15f, 0.08f, 0.35f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16)); redGlow.sprite = Gfx.Circle;
            redDot = UIK.Img(full, "redDot", new Color(1f, 0.12f, 0.06f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6)); redDot.sprite = Gfx.Circle;
            redDot.enabled = redGlow.enabled = false;
            hitMark = UIK.Img(full, "hit", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
            hitMark.color = new Color(1, 1, 1, 0);
            for (int i = 0; i < 4; i++)
            {
                var l = UIK.Img(hitMark.transform, "l", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3, 12));
                l.rectTransform.localRotation = Quaternion.Euler(0, 0, 45 + 90 * i);
                l.rectTransform.anchoredPosition = Quaternion.Euler(0, 0, 45 + 90 * i) * new Vector2(0, 11);
            }
            hitMark.gameObject.SetActive(false);   // hides the 4 child lines too (disabling the parent Image alone left them on screen)

            // score bar: three separated blocks (allies | clock | enemies) with progress toward the kill target
            var sb = UIK.Rect(root, "scorebar", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(620, 70));
            allyScore = ScoreBlock(sb, true, out allyBar);
            enemyScore = ScoreBlock(sb, false, out enemyBar);
            var clock = UIK.Img(sb, "clock", new Color(0.03f, 0.035f, 0.03f, 0.78f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(118, 70));
            timer = UIK.Txt(clock.transform, "time", "8:00", 34, Pal.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0, 7), Vector2.zero, true);
            UIK.Txt(clock.transform, "goal", (Settings.Turkish ? "HEDEF " : "TARGET ") + MatchManager.KillTarget, 13, Pal.Dim, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(118, 18), true);

            // kill feed
            feed = UIK.Rect(root, "feed", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24 - SquadHeight(MatchManager.AllySize) - 10), new Vector2(380, 160));
            var vl = feed.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childAlignment = TextAnchor.UpperLeft; vl.spacing = 6; vl.childControlHeight = false; vl.childControlWidth = false; vl.childForceExpandHeight = false;

            // health
            hpBack = UIK.Img(root, "hpBack", Pal.Panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(380, 10));
            hpFill = UIK.Img(hpBack.transform, "fill", Pal.Text, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, new Vector2(-4, -4));
            hpFill.rectTransform.pivot = new Vector2(0, 0.5f); hpFill.rectTransform.anchoredPosition = new Vector2(2, 0);

            // weapon slots
            var lp0 = PlayerController.Local;
            slotN = lp0 != null ? Mathf.Min(lp0.SlotCount, slotImgs.Length) : 3;
            for (int i = 0; i < slotN; i++)
            {
                int idx = i;
                string nm = lp0 != null ? lp0.SlotDef(i).displayName : "";
                if (nm == "GLOCK 17") nm = "GLOCK";
                var b = UIK.Img(root, "slot" + i, Pal.Panel, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2((i - (slotN - 1) * 0.5f) * 128, 78), new Vector2(120, 42), true);
                slotImgs[i] = b;
                // weapon silhouette (same as the kill feed); text only if there's no icon
                var ico = lp0 != null ? Z47Refs.I.KillIcon(lp0.SlotDef(i).id) : null;
                if (ico != null)
                {
                    float ih = 28f, iw = Mathf.Min(108f, ih * ico.width / (float)Mathf.Max(1, ico.height)); ih = iw * ico.height / (float)ico.width;
                    var ir = UIK.Rect(b.transform, "ic", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iw, ih));
                    slotIcons[i] = ir.gameObject.AddComponent<RawImage>(); slotIcons[i].texture = ico; slotIcons[i].raycastTarget = false; slotIcons[i].color = Pal.Text;
                }
                else slotTxt[i] = UIK.Txt(b.transform, "t", nm, 20, Pal.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
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

            BuildPauseButton();

            BuildDeath(); BuildPause(); BuildReload(); BuildGrenadeWarn();
            controls.gameObject.SetActive(TouchUI);
        }

        // ------------------------------------------------------------ touch controls
        public static Texture2D IconFor(string id)
        {
            var R = Z47Refs.I;
            switch (id)
            {
                case "fire": case "fire2": return R.icFire;
                case "aim": return R.icAim;
                case "reload": return R.icReload;
                case "grenade": return R.icGrenade;
                case "crouch": return R.icCrouch;
                case "jump": return R.icJump;
                default: return null;
            }
        }

        void BuildPauseButton()
        {
            var pb = UIK.Img(root, "pause", new Color(0.03f, 0.035f, 0.03f, 0.7f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-(24 + RadarR * 2 + 12), -24), new Vector2(64, 64), true);
            if (Z47Refs.I.icPause != null)
            {
                var pi = UIK.Rect(pb.transform, "icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34)).gameObject.AddComponent<RawImage>();
                pi.texture = Z47Refs.I.icPause; pi.color = Pal.Text; pi.raycastTarget = false;
            }
            else
            {
                UIK.Img(pb.transform, "a", Pal.Text, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-8, 0), new Vector2(7, 24));
                UIK.Img(pb.transform, "b", Pal.Text, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(8, 0), new Vector2(7, 24));
            }
            UIK.Img(pb.transform, "line", Pal.Accent, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 3));
            var b = pb.gameObject.AddComponent<Button>(); b.onClick.AddListener(() => { Sfx.UI(); TogglePause(); });
        }

        /// Rebuild the touch buttons (after the layout editor saved new positions).
        public void RebuildControls()
        {
            if (controls != null) Destroy(controls.gameObject);
            fireButtons.Clear(); PInput.fireHeld = false; PInput.move = Vector2.zero;
            BuildControls();
            controls.gameObject.SetActive(TouchUI && !deathPanel.activeSelf && endPanel == null);
        }

        void BuildControls()
        {
            controls = UIK.Full(root, "controls");
            controls.SetAsFirstSibling();
            var lay = HudLayout.Load();

            // look area (right 62%)
            var look = UIK.Img(controls, "look", new Color(0, 0, 0, 0), new Vector2(0.38f, 0), new Vector2(1, 0.86f), Vector2.zero, Vector2.zero, true);
            var lp = look.gameObject.AddComponent<TouchPad>();
            lp.drag = e => PInput.look += e.delta * (1080f / Screen.height);

            // joystick area (left 38%)
            var joyArea = UIK.Img(controls, "joyArea", new Color(0, 0, 0, 0), new Vector2(0, 0), new Vector2(0.38f, 0.75f), Vector2.zero, Vector2.zero, true);
            var mv = HudLayout.Get(lay, "move");
            joyBase = UIK.Img(controls, "joyBase", new Color(1, 1, 1, 0.16f), new Vector2(0, 0), new Vector2(0, 0), mv.pos, Vector2.one * mv.size).rectTransform;
            joyBase.pivot = new Vector2(0.5f, 0.5f); joyBase.anchoredPosition = mv.pos;
            joyBase.gameObject.AddComponent<CanvasGroup>().alpha = mv.alpha;
            joyScale = mv.size / 260f;
            joyBase.GetComponent<Image>().sprite = Gfx.Circle;
            var ring = UIK.Img(joyBase, "ring", new Color(1, 1, 1, 0.4f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); ring.sprite = Gfx.Ring;
            joyKnob = UIK.Img(joyBase, "knob", new Color(1, 1, 1, 0.75f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110) * joyScale).rectTransform;
            joyKnob.GetComponent<Image>().sprite = Gfx.Circle;
            Vector2 home = joyBase.anchoredPosition;
            joyPad = joyArea.gameObject.AddComponent<TouchPad>();
            joyPad.down = e =>
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(controls, e.position, null, out var lp2);
                joyBase.anchoredPosition = lp2 + controls.rect.size * 0.5f; joyOrigin = e.position;   // pivot is centred: base sits under the thumb
                UpdateJoy(e.position);
            };
            joyPad.drag = e => UpdateJoy(e.position);
            joyPad.up = e => { PInput.move = Vector2.zero; joyKnob.anchoredPosition = Vector2.zero; joyBase.anchoredPosition = home; };

            // buttons
            RoundButton(HudLayout.Get(lay, "fire"), Pal.Accent, true, null, fire: true);
            if (Settings.LeftFire) RoundButton(HudLayout.Get(lay, "fire2"), Pal.Accent, true, null, fire: true);
            aimBtnImg = RoundButton(HudLayout.Get(lay, "aim"), Color.white, false, () => { AimToggled = !AimToggled; PInput.aim = AimToggled; });
            RoundButton(HudLayout.Get(lay, "jump"), Color.white, false, () => PInput.jump = true);
            RoundButton(HudLayout.Get(lay, "crouch"), Color.white, false, () => PInput.crouchToggle = true);
            RoundButton(HudLayout.Get(lay, "reload"), Color.white, false, () => PInput.reload = true);
            RoundButton(HudLayout.Get(lay, "prone"), Color.white, false, () => PInput.proneToggle = true);
            leanLImg = RoundButton(HudLayout.Get(lay, "leanl"), Color.white, false, () => PInput.lean = PInput.lean == -1 ? 0 : -1);
            leanRImg = RoundButton(HudLayout.Get(lay, "leanr"), Color.white, false, () => PInput.lean = PInput.lean == 1 ? 0 : 1);
            var gb = RoundButton(HudLayout.Get(lay, "grenade"), Color.white, false, () => PInput.grenade = true);
            grenadeTxt = UIK.Txt(gb.transform, "n", "2", 26, Pal.Accent, TextAnchor.UpperRight, Vector2.one, Vector2.one, new Vector2(-12, -10), new Vector2(40, 30), true);
            cGren = -1;
        }
        float joyScale = 1f;

        Image RoundButton(HudLayout.Item it, Color c, bool fill, Action onDown, bool fire = false)
        {
            string label = L.S(it.label); float size = it.size;
            Vector2 anchor = HudLayout.Anchor(it);
            var img = UIK.Img(controls, it.id, fill ? new Color(c.r, c.g, c.b, 0.55f) : new Color(0, 0, 0, 0.45f), anchor, anchor, it.pos, new Vector2(size, size), true);
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f); img.rectTransform.anchoredPosition = it.pos;
            img.gameObject.AddComponent<CanvasGroup>().alpha = it.alpha;
            img.sprite = Gfx.Circle;
            var ring = UIK.Img(img.transform, "ring", new Color(c.r, c.g, c.b, 0.85f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); ring.sprite = Gfx.Ring;
            var icon = IconFor(it.id);
            if (icon != null)
            {
                // icon instead of a word (like PUBG / CODM); slight drop shadow keeps it readable on bright sand
                float isz = size * (it.id == "fire" || it.id == "fire2" ? 0.55f : 0.6f);
                var sh = UIK.Rect(img.transform, "ish", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(2, -2), Vector2.one * isz).gameObject.AddComponent<RawImage>();
                sh.texture = icon; sh.color = new Color(0, 0, 0, 0.45f); sh.raycastTarget = false;
                var ic = UIK.Rect(img.transform, "icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * isz).gameObject.AddComponent<RawImage>();
                ic.texture = icon; ic.color = fill ? Color.white : Pal.Text; ic.raycastTarget = false;
            }
            else UIK.Txt(img.transform, "t", label, Mathf.RoundToInt(size * 0.2f), fill ? Color.white : Pal.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            var tp = img.gameObject.AddComponent<TouchPad>();
            if (fire)
            {
                tp.down = e => { PInput.fireHeld = true; img.transform.localScale = Vector3.one * 0.92f; };
                tp.up = e => { PInput.fireHeld = AnyFireHeld(tp); img.transform.localScale = Vector3.one; };
                tp.drag = e => { if (Settings.FireDragLook && e != null) PInput.look += e.delta * (1080f / Screen.height); };   // slide the firing thumb to steer
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
            float radius = 110f * joyScale * Screen.height / 1080f;
            Vector2 d = screenPos - joyOrigin;
            Vector2 v = Vector2.ClampMagnitude(d / radius, 1.25f);
            PInput.move = Vector2.ClampMagnitude(v, 1f) * (v.magnitude > 1.1f ? 1f : 0.84f);
            joyKnob.anchoredPosition = Vector2.ClampMagnitude(v, 1f) * 110f * joyScale;
        }

        // ------------------------------------------------------------ panels
        /// Reload countdown under the crosshair, same style as the respawn timer.
        void BuildReload()
        {
            reloadBox = UIK.Rect(root, "reload", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 165), new Vector2(420, 100)).gameObject;   // just above the weapon slots, clear of kill/toast texts
            reloadTxt = UIK.Txt(reloadBox.transform, "t", "", 22, Pal.Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(420, 30), true);
            reloadTxt.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
            var bg = UIK.Img(reloadBox.transform, "bg", new Color(0, 0, 0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(240, 6));
            var fill = UIK.Img(bg.transform, "fill", Pal.Accent, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, Vector2.zero);
            reloadFill = fill.rectTransform; reloadFill.pivot = new Vector2(0, 0.5f);
            reloadHint = UIK.Txt(reloadBox.transform, "h", L.S("reload_cancel"), 16, new Color(1, 1, 1, 0.65f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -24), new Vector2(420, 24), true);
            reloadBox.SetActive(false);
        }

        void BuildSquad(PlayerController me)
        {
            var mates = new List<Actor>();
            mates.Add(me);
            foreach (var a in Actor.All) if (a != null && a != me && a.team == me.team) mates.Add(a);
            if (mates.Count < MatchManager.AllySize) return;          // bots not spawned yet: try again next frame
            float h = SquadHeight(mates.Count);
            squadRoot = UIK.Rect(root, "squad", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(SquadW, h));
            var card = UIK.Img(squadRoot, "card", new Color(0.03f, 0.035f, 0.03f, 0.55f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            UIK.Img(card.transform, "top", Pal.Ally, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 3)).rectTransform.pivot = new Vector2(0.5f, 1);
            UIK.Txt(card.transform, "h", L.S("sq_title"), 15, Pal.Ally, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -17), new Vector2(160, 24), true);
            squadAlive = UIK.Txt(card.transform, "al", "", 14, Pal.Dim, TextAnchor.MiddleRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -17), new Vector2(160, 24), true);
            for (int i = 0; i < mates.Count; i++)
            {
                var a = mates[i]; var r = new SquadRow { a = a, col = SquadCols[i % SquadCols.Length] };
                float y = -(SquadHead + 3f) - i * (SquadRowH + SquadGap);
                r.bg = UIK.Img(card.transform, "row" + i, a == me ? new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.16f) : new Color(1, 1, 1, 0.04f), new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(-8, SquadRowH));
                r.bg.rectTransform.pivot = new Vector2(0.5f, 1); r.bg.rectTransform.anchoredPosition = new Vector2(0, y);
                r.stripe = UIK.Img(r.bg.transform, "s", r.col, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(4, 0)); r.stripe.rectTransform.pivot = new Vector2(0, 0.5f);
                r.num = UIK.Txt(r.bg.transform, "no", (i + 1).ToString(), SquadFont, r.col, TextAnchor.MiddleCenter, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(20, SquadRowH), true);
                // one line: number · name · health bar · kills (nothing stacked, so nothing can overlap)
                float fh = SquadRowH * 0.5f;
                var rfl = Countries.Make(r.bg.transform, "fl", a.flag, new Vector2(0, 0.5f), Vector2.zero, fh);
                rfl.rectTransform.pivot = new Vector2(0, 0.5f); rfl.rectTransform.anchoredPosition = new Vector2(32, 0);
                float nx = 32 + fh * 4f / 3f + 6f;
                r.name = UIK.Txt(r.bg.transform, "n", a.callsign, SquadFont, a == me ? Pal.Accent : Pal.Text, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(nx, 0), new Vector2(156 - nx, SquadRowH), true);
                r.name.rectTransform.pivot = new Vector2(0, 0.5f); r.name.rectTransform.anchoredPosition = new Vector2(nx, 0);
                r.name.horizontalOverflow = HorizontalWrapMode.Wrap; r.name.verticalOverflow = VerticalWrapMode.Truncate;
                r.name.resizeTextForBestFit = true; r.name.resizeTextMinSize = 9; r.name.resizeTextMaxSize = SquadFont;
                // health: dark track, slow pale "damage trail", then the live fill
                r.bar = UIK.Img(r.bg.transform, "bar", new Color(0, 0, 0, 0.55f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(162, 0), new Vector2(84, 7)).gameObject;
                var barRt = (RectTransform)r.bar.transform; barRt.pivot = new Vector2(0, 0.5f); barRt.anchoredPosition = new Vector2(162, 0);
                r.trail = UIK.Img(r.bar.transform, "tr", new Color(1f, 0.45f, 0.35f, 0.8f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                r.trailRt = r.trail.rectTransform; r.trailRt.pivot = new Vector2(0, 0.5f); r.trailRt.anchoredPosition = Vector2.zero;
                r.fill = UIK.Img(r.bar.transform, "hp", Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                r.fillRt = r.fill.rectTransform; r.fillRt.pivot = new Vector2(0, 0.5f); r.fillRt.anchoredPosition = Vector2.zero;
                r.kills = UIK.Txt(r.bg.transform, "k", "", SquadFont, Pal.Text, TextAnchor.MiddleRight, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(40, SquadRowH), true);
                r.kills.rectTransform.pivot = new Vector2(1, 0.5f); r.kills.rectTransform.anchoredPosition = new Vector2(-8, 0);
                r.dead = UIK.Txt(r.bg.transform, "d", "", 13, new Color(1f, 0.42f, 0.36f), TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(162, 0), new Vector2(90, SquadRowH), true);
                r.dead.rectTransform.pivot = new Vector2(0, 0.5f); r.dead.rectTransform.anchoredPosition = new Vector2(162, 0);
                r.dead.gameObject.SetActive(false);
                squad.Add(r);
            }
            // kill feed sits right under the card
            if (feed != null) feed.anchoredPosition = new Vector2(24, -24 - h - 10);
        }

        void UpdateSquad()
        {
            int alive = 0;
            foreach (var r in squad)
            {
                if (r.a == null) continue;
                bool al = r.a.Alive; if (al) alive++;
                if (al != r.wasAlive)
                {
                    r.wasAlive = al;
                    r.dead.gameObject.SetActive(!al); r.bar.SetActive(al);
                    r.name.color = !al ? Pal.Dim : r.a.isPlayer ? Pal.Accent : Pal.Text;
                    r.stripe.color = al ? r.col : new Color(0.35f, 0.35f, 0.33f);
                    r.num.color = r.stripe.color;
                    if (al) { r.shown = 1f; r.cDead = -1; }
                }
                if (r.a.kills != r.cK) { r.cK = r.a.kills; r.kills.text = r.cK + " <size=11>K</size>"; }
                if (!al)
                {
                    int left = Mathf.CeilToInt(r.a.respawnAt - Time.time);
                    if (left != r.cDead) { r.cDead = left; r.dead.text = left > 0 ? L.S("sq_dead") + " · " + left : L.S("sq_dead"); }
                    continue;
                }
                float f = Mathf.Clamp01(r.a.hp / r.a.maxHp);
                r.shown = f > r.shown ? f : Mathf.MoveTowards(r.shown, f, Time.deltaTime * (Time.time - r.a.lastDamageTime > 0.4f ? 0.9f : 0f));
                r.fillRt.localScale = new Vector3(f, 1, 1);
                r.trailRt.localScale = new Vector3(r.shown, 1, 1);
                Color c = f < 0.3f ? Color.Lerp(new Color(1f, 0.25f, 0.2f), new Color(1f, 0.6f, 0.55f), Mathf.PingPong(Time.time * 3f, 1f))
                        : f < 0.6f ? new Color(1f, 0.85f, 0.4f) : Color.white;
                r.fill.color = c;
            }
            if (alive != cAlive) { cAlive = alive; squadAlive.text = alive + "/" + squad.Count + " " + L.S("sq_alive"); }
        }

        // ------------------------------------------------------------ grenade warning (any grenade, own ones too)
        // CoD style: grenade icon on a ring around the crosshair, arrow pointing toward it, distance underneath.
        readonly List<(RectTransform root, RectTransform arrow, Image bg, Image arrowImg, Text dist)> gWarn = new List<(RectTransform, RectTransform, Image, Image, Text)>();
        const float GrenadeWarnR = 12f;

        void BuildGrenadeWarn()
        {
            for (int i = 0; i < 4; i++)
            {
                var r = UIK.Rect(full, "gwarn" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 80));
                var arrow = UIK.Img(r, "arrow", new Color(1f, 0.2f, 0.1f, 0.95f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
                arrow.sprite = Gfx.Arrow;
                var bg = UIK.Img(r, "bg", new Color(0.8f, 0.1f, 0.05f, 0.85f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52, 52));
                bg.sprite = Gfx.Circle;
                if (Z47Refs.I.icGrenade != null)
                {
                    var ic = UIK.Rect(bg.transform, "ic", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34)).gameObject.AddComponent<RawImage>();
                    ic.texture = Z47Refs.I.icGrenade; ic.raycastTarget = false;
                }
                var d = UIK.Txt(r, "d", "", 17, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(100, 24), true);
                d.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);
                r.gameObject.SetActive(false);
                gWarn.Add((r, arrow.rectTransform, bg, arrow, d));
            }
        }

        void UpdateGrenadeWarn(PlayerController p)
        {
            int n = 0;
            if (p.Alive && p.cam != null)
            {
                foreach (var g in Grenade.Live)
                {
                    if (g == null || n >= gWarn.Count) continue;
                    Vector3 to = g.transform.position - p.transform.position;
                    float dist = to.magnitude;
                    if (dist > GrenadeWarnR) continue;
                    var w = gWarn[n++];
                    w.root.gameObject.SetActive(true);
                    // direction relative to where the player is looking (flat), placed on a ring around the crosshair
                    Vector3 local = p.cam.transform.InverseTransformDirection(to); local.y = 0f;
                    float ang = local.sqrMagnitude > 0.0001f ? Mathf.Atan2(local.x, local.z) : 0f;
                    Vector2 dir = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang));
                    w.root.anchoredPosition = dir * 175f;
                    w.arrow.anchoredPosition = dir * 40f;
                    w.arrow.localRotation = Quaternion.Euler(0, 0, -ang * Mathf.Rad2Deg);
                    bool lethal = dist < Grenade.BlastR;
                    Color c = lethal ? new Color(1f, 0.12f, 0.06f, 0.92f) : new Color(1f, 0.55f, 0.1f, 0.88f);
                    w.bg.color = c; w.arrowImg.color = c;
                    float urgency = Mathf.Clamp01(1f - g.TimeLeft / 2.6f);
                    w.root.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Time.time * (10f + urgency * 14f)));
                    w.dist.text = Mathf.Max(1, Mathf.RoundToInt(dist)) + "m";
                }
            }
            for (; n < gWarn.Count; n++) if (gWarn[n].root.gameObject.activeSelf) gWarn[n].root.gameObject.SetActive(false);
        }

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
            UIK.Btn(pausePanel.transform, L.S("settings"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(520, 96), () => { pausePanel.SetActive(false); Boot.I.ShowSettings(canvas.transform, () => { if (Paused) pausePanel.SetActive(true); }, true); }, 44);
            UIK.Btn(pausePanel.transform, L.S("mainmenu"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -160), new Vector2(520, 96), () => Boot.I.ToMenu(), 44);
            pausePanel.SetActive(false);
        }

        public void TogglePause()
        {
            if (mm.Finished) return;
            Paused = !Paused;
            pausePanel.SetActive(Paused);
            Time.timeScale = Paused && !Net.InMatch ? 0f : 1f;          // online: the match keeps running behind the menu
            AudioListener.pause = Paused && !Net.InMatch;
            PInput.fireHeld = false;
        }

        public void ShowDeath(Actor killer)
        {
            deathPanel.SetActive(true);
            deathAt = Time.time;
            string kn = killer != null && killer != PlayerController.Local ? killer.callsign : Actor.HazardName ?? "—";
            deathTxt.text = L.S("killedby") + "  <color=#FF5040>" + kn.ToUpper() + "</color>";
            controls.gameObject.SetActive(false);
            PInput.fireHeld = false; PInput.move = Vector2.zero;
        }

        /// Drop out of aim/scope (death, match end).
        public void ResetAim() { AimToggled = false; PInput.aim = false; SetScope(false); scopeBlink.color = new Color(0, 0, 0, 0); }

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
            bool tr = Settings.Turkish;
            var me = PlayerController.Local;
            endPanel = UIK.Full(root, "end").gameObject;
            var P = endPanel.transform;
            UIK.Img(P, "bg", new Color(0, 0, 0, 0.86f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            string title = result > 0 ? L.S("victory") : result < 0 ? L.S("defeat") : L.S("draw");
            UIK.Txt(P, "title", title, 96, result > 0 ? Pal.Accent : result < 0 ? Pal.Enemy : Pal.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -95), new Vector2(1400, 120), true);
            UIK.Txt(P, "score", "<color=#5AB8FF>" + mm.score[0] + "</color>  —  <color=#FF5040>" + mm.score[1] + "</color>", 52, Pal.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(800, 64), true).supportRichText = true;

            // MVP: best kills/deaths/headshots across both teams
            Actor mvp = null; float best = float.MinValue;
            foreach (var a in Actor.All) { float sc = a.kills * 3f - a.deaths + a.headshotKills; if (sc > best) { best = sc; mvp = a; } }
            if (mvp != null)
            {
                var mb = UIK.Img(P, "mvp", new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.95f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -238), new Vector2(520, 40));
                UIK.Txt(mb.transform, "t", "MVP  ·  " + mvp.callsign.ToUpper() + "  ·  " + mvp.kills + (tr ? " LEŞ" : " KILLS"), 22, new Color(0.08f, 0.06f, 0.03f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            }

            // ---- left: your match + XP
            var card = UIK.Img(P, "mine", new Color(1, 1, 1, 0.05f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520, -60), new Vector2(620, 560));
            var C = card.transform;
            UIK.Img(C, "top", Pal.Accent, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 4)).rectTransform.pivot = new Vector2(0.5f, 1);
            UIK.Txt(C, "h", L.S("end_mine"), 24, Pal.Accent, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -30), new Vector2(-48, 34), true);
            int kills = me != null ? me.kills : 0, deaths = me != null ? me.deaths : 0, hsk = me != null ? me.headshotKills : 0;
            float acc = me != null && me.shotsFired > 0 ? me.shotsHit / (float)me.shotsFired : 0f;
            string[,] rows =
            {
                { L.S("st_kd"), kills + " / " + deaths + "   <color=#9E998C>(" + (deaths > 0 ? (kills / (float)deaths).ToString("0.00") : kills.ToString("0.00")) + ")</color>" },
                { L.S("st_acc"), "%" + Mathf.RoundToInt(acc * 100f) },
                { L.S("st_hs"), hsk.ToString() },
                { L.S("st_long"), me != null ? Mathf.RoundToInt(me.longestKill) + " m" : "-" },
                { L.S("st_dmg"), me != null ? Mathf.RoundToInt(me.damageDealt).ToString() : "0" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float y = -78 - i * 40;
                UIK.Txt(C, "l" + i, rows[i, 0], 22, Pal.Dim, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, y), new Vector2(-48, 34), false);
                UIK.Txt(C, "v" + i, rows[i, 1], 24, Pal.Text, TextAnchor.MiddleRight, new Vector2(0, 1), new Vector2(1, 1), new Vector2(-24, y), new Vector2(-48, 34), true).supportRichText = true;
            }

            // XP for this match
            bool isMvp = mvp != null && mvp == me;
            int xKills = kills * 100, xHs = hsk * 50, xRes = result > 0 ? 500 : 150, xMvp = isMvp ? 300 : 0;
            int gained = xKills + xHs + xRes + xMvp;
            int before = Progress.XP; Progress.XP = before + gained;
            var newAch = Profile.RecordMatch(me, result > 0, isMvp);
            UIK.Img(C, "sep", new Color(1, 1, 1, 0.12f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -290), new Vector2(-48, 2));
            string xl = L.S("xp_kills") + "  +" + xKills + "     " + L.S("xp_hs") + "  +" + xHs + "\n" + L.S(result > 0 ? "xp_win" : "xp_play") + "  +" + xRes + (isMvp ? "     " + L.S("xp_mvp") + "  +" + xMvp : "");
            UIK.Txt(C, "xpl", xl, 19, Pal.Dim, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -330), new Vector2(-48, 60), false);
            UIK.Txt(C, "xpt", L.S("xp_total") + "  <color=#F29E1F>+" + gained + "</color>", 26, Pal.Text, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -400), new Vector2(-48, 36), true).supportRichText = true;
            var lvl = UIK.Txt(C, "lvl", "", 22, Pal.Accent, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -450), new Vector2(-48, 30), true);
            var lvlNum = UIK.Txt(C, "lvln", "", 18, Pal.Dim, TextAnchor.MiddleRight, new Vector2(0, 1), new Vector2(1, 1), new Vector2(-24, -450), new Vector2(-48, 30), false);
            var track = UIK.Img(C, "track", new Color(1, 1, 1, 0.1f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -484), new Vector2(-48, 12));
            var fill = UIK.Img(track.transform, "fill", Pal.Accent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).rectTransform;
            fill.pivot = new Vector2(0, 0.5f); fill.anchoredPosition = Vector2.zero; fill.localScale = new Vector3(0, 1, 1);
            var up = UIK.Txt(C, "up", "", 30, Pal.Accent, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 30), new Vector2(0, 40), true);
            StartCoroutine(XpBar(before, before + gained, lvl, lvlNum, fill, up));
            // achievements unlocked by this match
            for (int i = 0; i < newAch.Count && i < 2; i++)
            {
                var ab = UIK.Img(P, "ach" + i, new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.92f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-560, 286 - i * 46), new Vector2(540, 40));
                UIK.Txt(ab.transform, "t", (tr ? "BAŞARIM AÇILDI:  " : "ACHIEVEMENT:  ") + Profile.AName(newAch[i]).ToUpper(), 20, new Color(0.08f, 0.06f, 0.03f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            }

            // ---- right: both teams
            for (int t = 0; t < 2; t++)
            {
                var list = new List<Actor>();
                foreach (var a in Actor.All) if (a.team == t) list.Add(a);
                list.Sort((x, y) => y.kills != x.kills ? y.kills.CompareTo(x.kills) : x.deaths.CompareTo(y.deaths));
                float x0 = t == 0 ? 90 : 560, rh = list.Count > 5 ? 34f : 46f;
                UIK.Txt(P, "h" + t, L.S(t == 0 ? "allies" : "enemies"), 24, t == 0 ? Pal.Ally : Pal.Enemy, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x0, 200), new Vector2(440, 36), true);
                UIK.Txt(P, "hk" + t, L.S("kills") + "     " + L.S("deaths"), 20, Pal.Dim, TextAnchor.MiddleRight, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x0, 200), new Vector2(440, 36), true);
                for (int i = 0; i < list.Count; i++)
                {
                    var a = list[i];
                    var row = UIK.Img(P, "r", a.isPlayer ? new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.35f) : new Color(1, 1, 1, 0.06f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x0, 160 - i * (rh + 4)), new Vector2(440, rh));
                    UIK.Img(row.transform, "s", t == 0 ? Pal.Ally : Pal.Enemy, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(4, 0)).rectTransform.pivot = new Vector2(0, 0.5f);
                    var efl = Countries.Make(row.transform, "fl", a.flag, new Vector2(0, 0.5f), Vector2.zero, rh * 0.42f);
                    efl.rectTransform.pivot = new Vector2(0, 0.5f); efl.rectTransform.anchoredPosition = new Vector2(16, 0);
                    float enx = 16 + rh * 0.42f * 4f / 3f + 10f;
                    var ent = UIK.Txt(row.transform, "n", (a == mvp ? "MVP  " : "") + a.callsign, rh > 40 ? 24 : 19, a == mvp ? Pal.Accent : Pal.Text, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
                    ent.rectTransform.offsetMin = new Vector2(enx, 0); ent.rectTransform.offsetMax = new Vector2(-18, 0);
                    UIK.Txt(row.transform, "k", a.kills + "        " + a.deaths, rh > 40 ? 24 : 19, Pal.Text, TextAnchor.MiddleRight, Vector2.zero, Vector2.one, new Vector2(-22, 0), new Vector2(-44, 0), true);
                }
            }
            if (Net.Online) UIK.Btn(P, tr ? "LOBİYE DÖN" : "BACK TO LOBBY", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-280, 80), new Vector2(500, 96), () => Boot.I.BackToRoom(), 42, true);
            else UIK.Btn(P, L.S("again"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-280, 80), new Vector2(500, 96), () => Boot.I.StartMatch(), 42, true);
            UIK.Btn(P, L.S("mainmenu"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(280, 80), new Vector2(500, 96), () => Boot.I.ToMenu(), 42);
        }

        /// XP bar fills from the old total to the new one, wrapping (and celebrating) on each level up.
        System.Collections.IEnumerator XpBar(int from, int to, Text lvl, Text num, RectTransform fill, Text up)
        {
            yield return new WaitForSeconds(0.6f);
            float t = 0f, dur = 1.8f; int lastLevel = -1;
            while (true)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur); k = 1f - (1f - k) * (1f - k);
                int xp = Mathf.RoundToInt(Mathf.Lerp(from, to, k));
                Progress.Split(xp, out int level, out int into, out int need);
                if (lastLevel >= 0 && level > lastLevel) { up.text = L.S("levelup") + "  " + level; Sfx.Play2D(Z47Refs.I.headshot, 0.8f); up.transform.localScale = Vector3.one * 1.4f; }
                lastLevel = level;
                lvl.text = L.S("level") + " " + level;
                num.text = into + " / " + need + " XP";
                fill.localScale = new Vector3(into / (float)need, 1, 1);
                up.transform.localScale = Vector3.Lerp(up.transform.localScale, Vector3.one, Time.unscaledDeltaTime * 6f);
                if (k >= 1f && up.transform.localScale.x < 1.01f) break;
                yield return null;
            }
        }

        // ------------------------------------------------------------ events
        public void HitMarker(bool kill, bool head)
        {
            hitT = kill ? 0.45f : 0.18f; hitKill = kill || head;
            hitMark.gameObject.SetActive(true);
            if (head) Sfx.Play2D(Z47Refs.I.headshot, 0.6f, 0.04f);
            else if (kill) Sfx.Play2D(Z47Refs.I.casing, 0.25f, 0.05f);
            else Sfx.Play2D(Z47Refs.I.impact, 0.14f, 0.1f);
        }

        public void Damaged(Vector3 from)
        {
            dmgT = 1f;
            var p = PlayerController.Local; if (p == null) return;
            // refresh an existing marker pointing the same way instead of stacking them
            Vector3 to = (from - p.transform.position).Flat();
            Ind ind = null;
            foreach (var x in indicators) if (x.t > 0 && Vector3.Angle((x.from - p.transform.position).Flat(), to) < 18f) { ind = x; break; }
            if (ind == null) { ind = indicators[indIdx]; indIdx = (indIdx + 1) % indicators.Length; ind.pop = 1f; }
            ind.from = from; ind.t = 1.6f;
        }

        public void Fired() { }

        /// "killer  [weapon icon]  victim  [headshot]" — the icon of whatever did the kill (gun, grenade, drone).
        public void KillFeed(Actor killer, Actor victim, bool headshot, string hazard = null, string weaponId = null)
        {
            string kn = hazard != null ? "<color=#F29E1F>" + hazard + "</color>" : killer != null && killer != victim ? Colored(killer) : "";
            bool hsIcon = headshot && Z47Refs.I.icHeadshot != null;
            var icon = Z47Refs.I.KillIcon(weaponId);
            var bg = UIK.Img(feed, "k", new Color(0, 0, 0, 0.45f), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(380, 30));
            float x = 12f;
            Text Part(string s)
            {
                var tx = UIK.Txt(bg.transform, "t", s, 19, Pal.Text, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(10, 30));
                tx.supportRichText = true;
                float w = Mathf.Max(8f, tx.preferredWidth);
                tx.rectTransform.pivot = new Vector2(0, 0.5f); tx.rectTransform.sizeDelta = new Vector2(w, 30); tx.rectTransform.anchoredPosition = new Vector2(x, 0);
                x += w + 8f;
                return tx;
            }
            Text t = null;
            if (kn != "") t = Part(kn);
            if (icon != null)
            {
                float ih = 20f, iw = ih * icon.width / (float)Mathf.Max(1, icon.height);
                var ir = UIK.Rect(bg.transform, "wi", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(iw, ih));
                ir.pivot = new Vector2(0, 0.5f); ir.anchoredPosition = new Vector2(x, 0);
                var ri = ir.gameObject.AddComponent<RawImage>(); ri.texture = icon; ri.raycastTarget = false; ri.color = new Color(0.92f, 0.9f, 0.84f);
                x += iw + 8f;
            }
            else if (kn != "") Part("<color=#B0A890>»</color>");
            var vt = Part(Colored(victim) + (headshot && !hsIcon ? "  <color=#F29E1F>[" + L.S("headshot") + "]</color>" : ""));
            if (t == null) t = vt;
            if (hsIcon)
            {
                var hi = UIK.Rect(bg.transform, "hs", new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(22, 22));
                hi.pivot = new Vector2(0, 0.5f); hi.anchoredPosition = new Vector2(x - 2f, 0);
                var hri = hi.gameObject.AddComponent<RawImage>(); hri.texture = Z47Refs.I.icHeadshot; hri.color = Pal.Accent; hri.raycastTarget = false;
                x += 26f;
            }
            bg.rectTransform.sizeDelta = new Vector2(Mathf.Max(120f, x + 4f), 30f);
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
        Text bigText, killText; float bigT, killT; Color bigCol = Pal.Accent;
        public void ShowToastBig(string s) => ShowToastBig(s, Pal.Accent, 1.6f);
        Image flashImg; float flashA;
        /// White-out from a close blast (fades in ~0.6 s).
        public void Flash(float amount)
        {
            if (flashImg == null) { flashImg = UIK.Img(full, "flash", new Color(1, 0.97f, 0.9f, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); flashImg.gameObject.AddComponent<FlashFade>().hud = this; }
            flashA = Mathf.Max(flashA, Mathf.Clamp01(amount) * 0.85f);
            flashImg.transform.SetAsLastSibling();
        }
        public void TickFlash() { if (flashImg == null) return; flashA = Mathf.MoveTowards(flashA, 0f, Time.deltaTime * 1.4f); flashImg.color = new Color(1, 0.97f, 0.9f, flashA); }
        public void ShowToastBig(string s, Color c, float dur = 3f) { bigText.text = s; bigT = dur; bigCol = c; bigText.transform.localScale = Vector3.one * 1.4f; }

        // ------------------------------------------------------------ radar / minimap
        /// Kill-streak radar: enemies visible on the minimap until this time.
        public float RadarUntil;
        RectTransform radar, radarRot; RawImage mapImg; Image[] dots = new Image[24];
        const float RadarR = 105f, RadarRange = 45f;          // half-size in px, metres shown from centre to edge
        float Px => RadarR / RadarRange;                       // pixels per metre
        void BuildRadar()
        {
            var bg = UIK.Img(root, "radar", new Color(0.03f, 0.035f, 0.03f, 0.7f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(RadarR * 2, RadarR * 2));
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
                bool radarOn = Time.time < RadarUntil;
                if (!ally && !radarOn && Time.time - a.lastShotTime >= 2.2f) continue;
                Vector3 rel = a.transform.position - me;
                if (new Vector2(rel.x, rel.z).magnitude > RadarRange * 1.45f) continue;
                var d = dots[n++];
                d.enabled = true;
                d.rectTransform.anchoredPosition = new Vector2(rel.x, rel.z) * Px;
                float fade = ally || radarOn ? 1f : Mathf.Clamp01((2.2f - (Time.time - a.lastShotTime)) / 0.6f);
                var c = ally ? Pal.Ally : Pal.Enemy; d.color = new Color(c.r, c.g, c.b, fade);
                d.rectTransform.sizeDelta = Vector2.one * (ally ? 10f : 12f);
            }
            for (; n < dots.Length; n++) dots[n].enabled = false;
        }

        // ------------------------------------------------------------ damage direction
        class Ind { public Image img; public Vector3 from; public float t, pop; }
        Ind[] indicators = new Ind[6]; int indIdx;
        void BuildIndicators()
        {
            for (int i = 0; i < indicators.Length; i++)
            {
                var im = UIK.Img(root, "dmgInd", new Color(1f, 0.15f, 0.1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 48));
                im.sprite = Gfx.DmgTri; im.rectTransform.pivot = new Vector2(0.5f, 0.5f);
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
                ind.pop = Mathf.MoveTowards(ind.pop, 0f, Time.deltaTime * 5f);
                float r = 165f + ind.pop * 18f;
                ind.img.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad)) * r;
                ind.img.rectTransform.localRotation = Quaternion.Euler(0, 0, -a);
                ind.img.rectTransform.localScale = Vector3.one * (1f + ind.pop * 0.35f);
                ind.img.color = new Color(1f, 0.12f, 0.08f, Mathf.Clamp01(ind.t * 1.4f) * 0.95f);
            }
        }

        // ------------------------------------------------------------ name tags
        Text[] tags = new Text[20];
        void BuildTags()
        {
            for (int i = 0; i < tags.Length; i++)
            {
                tags[i] = UIK.Txt(full, "tag", "", 18, Pal.Ally, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 30), true);
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
                if (!ally && Vector3.Angle(cam.transform.forward, a.ChestPos - cam.transform.position) > 2.5f) continue;
                if (!ally && dist > Weather.SightRange * 0.85f) continue;      // fog / dust: no enemy name through the haze
                // only show a tag when the soldier is actually visible (tags through containers looked like people stuck inside them)
                Vector3 eyeTo = a.ChestPos + Vector3.up * 0.5f - cam.transform.position;
                if (Ballistics.Cast(cam.transform.position, eyeTo.normalized, eyeTo.magnitude + 0.5f, p, out var h) && Actor.FromCollider(h.collider) != a) continue;
                Vector3 sp = cam.WorldToScreenPoint(head);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(full, sp, null, out var lp)) continue;
                var t = tags[n++];
                t.enabled = true;
                t.text = a.callsign;
                t.color = ally ? Pal.Ally : Pal.Enemy;
                t.fontSize = Mathf.RoundToInt(Mathf.Lerp(19, 14, dist / 70f));
                t.rectTransform.anchoredPosition = lp;
            }
            for (; n < tags.Length; n++) tags[n].enabled = false;
        }
        public void ShowToast(string s, float t = 2.5f) { toast.text = s; toastT = t; }
        public void Pickup(string s) { ShowToast(s, 1.4f); }

        // ------------------------------------------------------------ low health: blood, heavy breathing, heartbeat, colour drain
        void LowHealth(PlayerController p)
        {
            if (breathSrc == null)
            {
                breathSrc = MakeLoop(Z47Refs.I.breath); heartSrc = MakeLoop(Z47Refs.I.heartbeat);
            }
            // starts below 75 hp, full at 25; fades out slowly while health regenerates
            float target = p.Alive ? Mathf.Clamp01((75f - p.hp) / 50f) : 0f;
            lowHpT = Mathf.MoveTowards(lowHpT, target, Time.deltaTime * (target > lowHpT ? 3f : (p.Alive ? 0.45f : 2f)));
            float vol = Settings.Sfx * lowHpT;
            if (breathSrc != null) { breathSrc.volume = vol * 0.9f; breathSrc.pitch = 1f + lowHpT * 0.1f; }
            if (heartSrc != null) { heartSrc.volume = vol * vol * 0.8f; heartSrc.pitch = 1f + lowHpT * 0.2f; }

            dmgT = Mathf.MoveTowards(dmgT, 0, Time.deltaTime * 1.8f);
            float beat = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * 1.3f * (1f + lowHpT * 0.2f)), 6f);
            float low = lowHpT * (0.55f + 0.25f * beat);
            vignette.color = new Color(1, 1, 1, Mathf.Clamp01(Mathf.Max(dmgT * 0.85f, low)));

            if (Boot.Exposure != null)
            {
                float sat = Mathf.Lerp(-8f, -62f, lowHpT);
                if (Mathf.Abs(sat - lastSat) > 0.5f) { lastSat = sat; Boot.Exposure.saturation.Override(sat); }
            }
        }

        AudioSource MakeLoop(AudioClip c)
        {
            if (c == null) return null;
            var a = gameObject.AddComponent<AudioSource>();
            a.clip = c; a.loop = true; a.spatialBlend = 0f; a.volume = 0f; a.playOnAwake = false; a.Play();
            return a;
        }

        void SetScope(bool on)
        {
            scopeLensMask.enabled = on; scopeLens.enabled = on;
            if (on) { var pl = PlayerController.Local; scopeLens.texture = pl != null ? pl.ScopeRT : null; }
            scopeHousing.enabled = on;
            scopeImg.enabled = on; scopeL.enabled = on; scopeR.enabled = on; scopeT.enabled = on; scopeB.enabled = on;
        }

        // ------------------------------------------------------------ update
        void Update()
        {
            var p = PlayerController.Local;
            if (p == null) return;

            // only touch Text when the value changed: no per-frame string garbage / canvas rebuilds
            if (mm.score[0] != cS0) { cS0 = mm.score[0]; allyScore.text = cS0.ToString(); allyBar.rectTransform.localScale = new Vector3(Mathf.Clamp01(cS0 / (float)MatchManager.KillTarget), 1, 1); }
            if (mm.score[1] != cS1) { cS1 = mm.score[1]; enemyScore.text = cS1.ToString(); enemyBar.rectTransform.localScale = new Vector3(Mathf.Clamp01(cS1 / (float)MatchManager.KillTarget), 1, 1); }
            if (squadRoot == null) BuildSquad(p); else UpdateSquad();
            UpdateGrenadeWarn(p);
            bool rl = p.Alive && p.Reloading;
            if (reloadBox.activeSelf != rl) { reloadBox.SetActive(rl); cReload = -1; }
            if (rl)
            {
                int tenths = Mathf.CeilToInt(p.ReloadLeft * 10f);
                if (tenths != cReload) { cReload = tenths; reloadTxt.text = L.S("rl_timer") + "  " + (tenths / 10f).ToString("0.0"); }
                reloadFill.sizeDelta = new Vector2(240f * p.ReloadProgress, 0f);
                if (reloadHint.gameObject.activeSelf != p.ReloadCancelable) reloadHint.gameObject.SetActive(p.ReloadCancelable);
            }

            int s = Mathf.CeilToInt(mm.timeLeft);
            if (s != cTime) { cTime = s; timer.text = (s / 60) + ":" + (s % 60).ToString("00"); }

            hpFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(p.hp / p.maxHp), 1, 1);
            hpFill.color = p.hp < 35 ? Pal.Enemy : Pal.Text;

            var d = p.Cur;
            if (p.Mag != cMag || p.Reserve != cRes || p.Cur.id != cAmmoWep)
            {
                cMag = p.Mag; cRes = p.Reserve; cAmmoWep = p.Cur.id; ammo.supportRichText = true;
                int ms = Mathf.Max(1, p.Cur.magSize);
                // mag: red when empty, orange when low (<=30%); reserve: red when empty, orange when less than one mag left
                string mc = cMag == 0 ? "#FF4A3D" : cMag <= Mathf.CeilToInt(ms * 0.3f) ? "#FFA028" : "#EDE8D9";
                string rc = cRes == 0 ? "#FF4A3D" : cRes < ms ? "#FFA028" : "#9E998C";
                ammo.text = "<color=" + mc + ">" + cMag + "</color><size=40><color=#9E998C> / </color><color=" + rc + ">" + cRes + "</color></size>";
            }
            if (d.id != cWep)
            {
                cWep = d.id; weaponName.text = d.displayName;
                for (int i = 0; i < slotN; i++)
                {
                    bool on = i == p.CurSlot;
                    slotImgs[i].color = on ? new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.8f) : Pal.Panel;
                    if (slotIcons[i] != null) slotIcons[i].color = on ? new Color(0.08f, 0.06f, 0.03f) : Pal.Text;
                }
            }
            if (p.grenades != cGren) { cGren = p.grenades; grenadeTxt.text = cGren.ToString(); }
            if (aimBtnImg != null) aimBtnImg.color = AimToggled ? new Color(1, 1, 1, 0.45f) : new Color(0, 0, 0, 0.35f);
            if (leanLImg != null) leanLImg.color = PInput.lean == -1 ? new Color(1, 1, 1, 0.45f) : new Color(0, 0, 0, 0.35f);
            if (leanRImg != null) leanRImg.color = PInput.lean == 1 ? new Color(1, 1, 1, 0.45f) : new Color(0, 0, 0, 0.35f);

            // crosshair
            bool scoped = p.Scoped;
            SetScope(scoped);
            if (scoped != wasScoped) { blinkT = scoped ? 1f : 0.6f; wasScoped = scoped; }   // short black blink like putting the eye to the glass
            blinkT = Mathf.MoveTowards(blinkT, 0f, Time.deltaTime * 6f);
            scopeBlink.color = new Color(0, 0, 0, blinkT * 0.9f);
            // scope drift: the tube lags behind fast turns and bobs while walking, like a real eye-to-glass
            Vector3 fwd = p.cam.transform.forward;
            if (scoped && Time.deltaTime > 0f)
            {
                Vector3 lf = p.cam.transform.InverseTransformDirection(lastFwd);
                Vector2 turn = new Vector2(-lf.x, -lf.y) / Time.deltaTime;           // rad/s
                float spd = p.MoveSpeed;
                Vector2 bob = new Vector2(Mathf.Sin(Time.time * 7f), Mathf.Abs(Mathf.Cos(Time.time * 7f)) - 0.5f) * Mathf.Clamp01(spd / 4f) * 26f;
                Vector2 target = Vector2.ClampMagnitude(-turn * 70f, 90f) + bob;
                scopeDrift = Vector2.Lerp(scopeDrift, target, Time.deltaTime * 8f);
            }
            else scopeDrift = Vector2.zero;
            scopeGroup.anchoredPosition = scopeDrift;
            lastFwd = fwd;
            float gap = 8f + p.spreadNow * 9f;
            bool showX = !scoped && !p.Aiming;
            xh[0].anchoredPosition = new Vector2(0, gap + 8); xh[1].anchoredPosition = new Vector2(0, -gap - 8);
            xh[2].anchoredPosition = new Vector2(gap + 8, 0); xh[3].anchoredPosition = new Vector2(-gap - 8, 0);
            foreach (var x in xh) x.gameObject.SetActive(showX && p.Alive);
            centerDot.enabled = showX && p.Alive;
            // carbine red dot: drawn exactly at the aim point while looking through the sight
            bool rd = p.Alive && p.Aiming && p.Cur.id == "carbine" && p.AdsT > 0.8f;
            redDot.enabled = redGlow.enabled = rd;

            if (hitT > 0)
            {
                hitT -= Time.deltaTime;
                foreach (var im in hitMark.GetComponentsInChildren<Image>()) if (im != hitMark) im.color = hitKill ? new Color(1f, 0.3f, 0.2f, hitT * 4f) : new Color(1, 1, 1, hitT * 5f);
                if (hitT <= 0) hitMark.gameObject.SetActive(false);
            }

            LowHealth(p);

            if (toastT > 0) { toastT -= Time.deltaTime; if (toastT <= 0) toast.text = ""; }
            else if (p.Mag == 0 && p.Reserve == 0) toast.text = L.S("noammo");
            else toast.text = "";

            if (deathPanel.activeSelf)
            {
                float left = Mathf.Max(0, 4f - (Time.time - deathAt));
                deathTimer.text = L.S("respawn") + "  " + left.ToString("0.0");
            }

            UpdateRadar(p); UpdateIndicators(p); UpdateTags(p);
            if (!mm.Running && !mm.Finished && mm.countdown > 0f) { bigText.text = Mathf.CeilToInt(mm.countdown).ToString(); bigT = 0.3f; bigCol = Pal.Accent; bigText.transform.localScale = Vector3.one * (1f + (mm.countdown % 1f) * 0.4f); }
            if (bigT > 0) { bigT -= Time.deltaTime; bigText.color = new Color(bigCol.r, bigCol.g, bigCol.b, Mathf.Clamp01(bigT * 2f)); bigText.transform.localScale = Vector3.Lerp(bigText.transform.localScale, Vector3.one, Time.deltaTime * 8f); }
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

    public class FlashFade : MonoBehaviour { public HUD hud; void Update() { if (hud != null) hud.TickFlash(); } }
}
