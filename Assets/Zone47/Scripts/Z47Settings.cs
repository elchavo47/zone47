using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zone47
{
    /// Modern tabbed settings screen: sidebar tabs, card rows, segmented pickers, sliders and switches.
    public static class SettingsUI
    {
        static readonly Color Bg = new Color(0.035f, 0.04f, 0.035f, 0.97f);
        static readonly Color Card = new Color(1f, 1f, 1f, 0.045f);
        static readonly Color Chip = new Color(1f, 1f, 1f, 0.07f);
        static readonly Color Dark = new Color(0.08f, 0.06f, 0.03f);
        static int tab;

        public static void Show(Transform parent, Action onClose, bool inMatch)
        {
            var panel = UIK.Full(parent, "Settings");
            UIK.Img(panel, "bg", Bg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            var safe = UIK.Full(panel, "safe");
            safe.gameObject.AddComponent<SafeArea>();

            // header
            var title = UIK.Txt(safe, "title", L.S("settings"), 56, Pal.Text, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -40), new Vector2(800, 80), true);
            UIK.Img(safe, "line", Pal.Accent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(82, -126), new Vector2(84, 5));

            var close = UIK.Img(safe, "close", Pal.Accent, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-80, -46), new Vector2(230, 72), true);
            UIK.Txt(close.transform, "t", L.S("back"), 30, Dark, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            close.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                Sfx.UI();
                Settings.Apply();
                foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Boot.ApplyCameraFx(c);
                UnityEngine.Object.Destroy(panel.gameObject);
                onClose?.Invoke();
            });

            // content area
            var content = UIK.Rect(safe, "content", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            content.offsetMin = new Vector2(470, 50); content.offsetMax = new Vector2(-80, -170);

            // sidebar tabs
            string[] tabs = { "t_gfx", "t_ctrl", "t_audio", "t_general" };
            var tabImgs = new Image[tabs.Length]; var tabTxt = new Text[tabs.Length]; var tabBar = new Image[tabs.Length];
            Action refreshTabs = null;
            for (int i = 0; i < tabs.Length; i++)
            {
                int idx = i;
                var tb = UIK.Img(safe, "tab" + i, Color.clear, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -170 - i * 92), new Vector2(340, 80), true);
                tabBar[i] = UIK.Img(tb.transform, "bar", Pal.Accent, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(6, 0));
                tabTxt[i] = UIK.Txt(tb.transform, "t", L.S(tabs[i]), 30, Pal.Dim, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-32, 0), true);
                tabImgs[i] = tb;
                tb.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); tab = idx; refreshTabs(); BuildTab(content, panel, parent, onClose, inMatch); });
            }
            refreshTabs = () =>
            {
                for (int i = 0; i < tabs.Length; i++)
                {
                    bool on = i == tab;
                    tabImgs[i].color = on ? new Color(1, 1, 1, 0.08f) : Color.clear;
                    tabTxt[i].color = on ? Pal.Text : Pal.Dim;
                    tabBar[i].enabled = on;
                }
            };
            refreshTabs();
            BuildTab(content, panel, parent, onClose, inMatch);
        }

        static void BuildTab(RectTransform content, RectTransform panel, Transform parent, Action onClose, bool inMatch)
        {
            for (int i = content.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(content.GetChild(i).gameObject);
            int row = 0;
            switch (tab)
            {
                case 0:
                    Segmented(Row(content, row++, "quality", "quality_d"), new[] { "low", "medium", "high", "ultra" }, () => Settings.Quality, v =>
                    {
                        Settings.Quality = v; Settings.Apply();
                        foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Boot.ApplyCameraFx(c);
                    }, true);
                    Segmented(Row(content, row++, "fps_l", null), new[] { "30", "60", "120" }, () => Mathf.Max(0, Array.IndexOf(new[] { 30, 60, 120 }, Settings.Fps)),
                        v => { Settings.Fps = new[] { 30, 60, 120 }[v]; Application.targetFrameRate = Settings.Fps; }, false);
                    Slider(Row(content, row++, "bright_l", null), 0f, 1f, () => Settings.Brightness, v =>
                    {
                        Settings.Brightness = v; if (Boot.Exposure != null) Boot.Exposure.postExposure.Override(Settings.ExposureValue);
                    }, v => Mathf.RoundToInt(v * 100) + "%");
                    Toggle(Row(content, row++, "arms_l", null), () => Settings.ShowArms, v => Settings.ShowArms = v);
                    break;
                case 1:
                    Slider(Row(content, row++, "sens_l", null), 0.2f, 3f, () => Settings.Sens, v => Settings.Sens = v, v => v.ToString("0.0"));
                    Slider(Row(content, row++, "adssens_l", null), 0.2f, 2f, () => Settings.AdsSens, v => Settings.AdsSens = v, v => v.ToString("0.0"));
                    Segmented(Row(content, row++, "gyro_l", "gyro_d"), new[] { "off", "aimonly", "always" }, () => Settings.Gyro, v => Settings.Gyro = v, true);
                    Slider(Row(content, row++, "gyrosens_l", null), 0.2f, 3f, () => Settings.GyroSens, v => Settings.GyroSens = v, v => v.ToString("0.0"));
                    Toggle(Row(content, row++, "gyroinv_l", null), () => Settings.GyroInvert, v => Settings.GyroInvert = v);
                    Toggle(Row(content, row++, "aimassist_l", null), () => Settings.AimAssist, v => Settings.AimAssist = v);
                    Toggle(Row(content, row++, "leftfire_l", null), () => Settings.LeftFire, v => { Settings.LeftFire = v; if (HUD.I != null) HUD.I.RebuildControls(); });
                    ActionBtn(Row(content, row++, "layout_l", "layout_d"), L.S("edit"), () =>
                    {
                        panel.gameObject.SetActive(false);
                        LayoutEditor.Show(parent, () => panel.gameObject.SetActive(true));
                    });
                    break;
                case 2:
                    Slider(Row(content, row++, "music_l", null), 0f, 1f, () => Settings.Music, v => { Settings.Music = v; if (!inMatch) Sfx.Music(Z47Refs.I.menuMusic, 0.7f); }, v => Mathf.RoundToInt(v * 100) + "%");
                    Slider(Row(content, row++, "sfx_l", null), 0f, 1f, () => Settings.Sfx, v => Settings.Sfx = v, v => Mathf.RoundToInt(v * 100) + "%");
                    break;
                case 3:
                    SegmentedRaw(Row(content, row++, "lang_l", null), new[] { "TÜRKÇE", "ENGLISH" }, () => Settings.Turkish ? 0 : 1, v =>
                    {
                        Settings.Turkish = v == 0;
                        // rebuild the whole screen so every label switches language
                        UnityEngine.Object.Destroy(panel.gameObject);
                        Show(parent, onClose, inMatch);
                    });
                    Segmented(Row(content, row++, "diff_l", null), new[] { "easy", "normal", "hard" }, () => Settings.Difficulty, v => Settings.Difficulty = v, false);
                    ActionBtn(Row(content, row++, "credits_l", "credits_d"), L.S("open"), () => Boot.I.ShowCredits(panel));
                    break;
            }
        }

        // ------------------------------------------------------------ row & widgets
        static RectTransform Row(RectTransform content, int i, string label, string desc)
        {
            var card = UIK.Img(content, "row", Card, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -i * 94), new Vector2(0, 82));
            card.rectTransform.pivot = new Vector2(0.5f, 1); card.rectTransform.anchoredPosition = new Vector2(0, -i * 94);
            var l = UIK.Txt(card.transform, "l", L.S(label), 32, Pal.Text, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(30, desc != null ? 13 : 0), new Vector2(-30, 0));
            l.fontStyle = FontStyle.Bold;
            if (desc != null) UIK.Txt(card.transform, "d", L.S(desc), 21, Pal.Dim, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(30, -20), new Vector2(-30, 0));
            return card.rectTransform;
        }

        static void Segmented(RectTransform row, string[] keys, Func<int> get, Action<int> set, bool wide)
        {
            var labels = new string[keys.Length];
            for (int i = 0; i < keys.Length; i++) labels[i] = L.S(keys[i]);
            SegmentedRaw(row, labels, get, set, wide ? 170 : 120);
        }

        static void SegmentedRaw(RectTransform row, string[] labels, Func<int> get, Action<int> set, float w = 190)
        {
            int n = labels.Length;
            var bgs = new Image[n]; var txts = new Text[n];
            Action refresh = () =>
            {
                int cur = get();
                for (int i = 0; i < n; i++) { bgs[i].color = i == cur ? Pal.Accent : Chip; txts[i].color = i == cur ? Dark : Pal.Text; }
            };
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                float x = -24 - (n - 1 - i) * (w + 6);
                var b = UIK.Img(row, "seg" + i, Chip, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(x, 0), new Vector2(w, 56), true);
                txts[i] = UIK.Txt(b.transform, "t", labels[i], 24, Pal.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
                bgs[i] = b;
                b.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); set(idx); refresh(); });
            }
            refresh();
        }

        static void Slider(RectTransform row, float min, float max, Func<float> get, Action<float> set, Func<float, string> fmt)
        {
            const float W = 440f;
            var hit = UIK.Img(row, "slider", Color.clear, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(W + 40, 70), true);
            var track = UIK.Img(hit.transform, "track", new Color(1, 1, 1, 0.13f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, 8));
            var fill = UIK.Img(track.transform, "fill", Pal.Accent, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(0, 0));
            fill.rectTransform.pivot = new Vector2(0, 0.5f);
            var knob = UIK.Img(track.transform, "knob", Color.white, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(36, 36));
            knob.sprite = Gfx.Circle; knob.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var val = UIK.Txt(row, "v", "", 30, Pal.Text, TextAnchor.MiddleRight, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24 - W - 60, 0), new Vector2(110, 60), true);
            Action refresh = () =>
            {
                float t = Mathf.InverseLerp(min, max, get());
                fill.rectTransform.sizeDelta = new Vector2(W * t, 0);
                knob.rectTransform.anchoredPosition = new Vector2(W * t, 0);
                val.text = fmt(get());
            };
            var tp = hit.gameObject.AddComponent<TouchPad>();
            Action<UnityEngine.EventSystems.PointerEventData> drag = e =>
            {
                if (e == null) return;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(track.rectTransform, e.position, null, out var lp);
                float t = Mathf.Clamp01(lp.x / W + 0.5f);
                set(Mathf.Lerp(min, max, t)); refresh();
            };
            tp.down = drag; tp.drag = drag;
            refresh();
        }

        static void Toggle(RectTransform row, Func<bool> get, Action<bool> set)
        {
            var pill = UIK.Img(row, "toggle", Chip, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(104, 52), true);
            pill.sprite = Gfx.Pill; pill.type = Image.Type.Sliced;
            var knob = UIK.Img(pill.transform, "k", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
            knob.sprite = Gfx.Circle; knob.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var txt = UIK.Txt(row, "s", "", 24, Pal.Dim, TextAnchor.MiddleRight, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-144, 0), new Vector2(160, 52), true);
            Action refresh = () =>
            {
                bool on = get();
                pill.color = on ? Pal.Accent : new Color(1, 1, 1, 0.14f);
                knob.rectTransform.anchoredPosition = new Vector2(on ? 26 : -26, 0);
                txt.text = L.S(on ? "on" : "off"); txt.color = on ? Pal.Accent : Pal.Dim;
            };
            pill.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); set(!get()); refresh(); });
            refresh();
        }

        static void ActionBtn(RectTransform row, string label, Action click)
        {
            var b = UIK.Img(row, "btn", Pal.Accent, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(230, 56), true);
            UIK.Txt(b.transform, "t", label, 24, Dark, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            b.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); click(); });
        }
    }

    /// Drag the touch buttons where you like them, resize the selected one, save.
    public static class LayoutEditor
    {
        class W { public HudLayout.Item it; public Image img, ring; public RectTransform rt; }

        public static void Show(Transform parent, Action onDone)
        {
            var items = HudLayout.Load();
            var panel = UIK.Full(parent, "LayoutEditor");
            UIK.Img(panel, "bg", new Color(0, 0, 0, 0.45f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            var safe = UIK.Full(panel, "safe");
            safe.gameObject.AddComponent<SafeArea>();

            // reference outlines of fixed HUD parts so buttons are not dropped on top of them
            Ghost(safe, new Vector2(1, 1), new Vector2(-24, -24), new Vector2(210, 210));       // minimap
            Ghost(safe, new Vector2(1, 1), new Vector2(-246, -24), new Vector2(64, 64));        // pause
            Ghost(safe, new Vector2(0, 1), new Vector2(24, -24), new Vector2(380, 140));        // kill feed
            Ghost(safe, new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(620, 70));       // score
            Ghost(safe, new Vector2(0.5f, 0), new Vector2(0, 78), new Vector2(504, 42));        // weapon slots

            var ws = new List<W>();
            W sel = null;
            Text sizeTxt = null;
            Action refresh = () =>
            {
                foreach (var w in ws)
                {
                    w.rt.anchoredPosition = w.it.pos;
                    w.rt.sizeDelta = Vector2.one * w.it.size;
                    w.ring.color = w == sel ? Pal.Accent : new Color(1, 1, 1, 0.7f);
                    w.img.color = w == sel ? new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.35f) : (w.it.accent ? new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.3f) : new Color(0, 0, 0, 0.4f));
                }
                if (sizeTxt != null) sizeTxt.text = sel != null ? Mathf.RoundToInt(sel.it.size / sel.it.defSize * 100) + "%" : "—";
            };

            foreach (var it in items)
            {
                if (it.id == "fire2" && !Settings.LeftFire) continue;
                var a = HudLayout.Anchor(it);
                var img = UIK.Img(safe, it.id, Color.black, a, a, it.pos, Vector2.one * it.size, true);
                img.sprite = Gfx.Circle; img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var ring = UIK.Img(img.transform, "ring", Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); ring.sprite = Gfx.Ring;
                if (it.id == "move")
                {
                    var k = UIK.Img(img.transform, "knob", new Color(1, 1, 1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                    k.sprite = Gfx.Circle; k.rectTransform.anchorMin = new Vector2(0.29f, 0.29f); k.rectTransform.anchorMax = new Vector2(0.71f, 0.71f);
                }
                var ict = HUD.IconFor(it.id);
                if (ict != null)
                {
                    var ic = UIK.Rect(img.transform, "icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                    ic.anchorMin = new Vector2(0.2f, 0.2f); ic.anchorMax = new Vector2(0.8f, 0.8f);
                    var ri = ic.gameObject.AddComponent<RawImage>(); ri.texture = ict; ri.color = Pal.Text; ri.raycastTarget = false;
                }
                else UIK.Txt(img.transform, "t", L.S(it.label), 24, Pal.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
                var w = new W { it = it, img = img, ring = ring, rt = img.rectTransform };
                ws.Add(w);
                var tp = img.gameObject.AddComponent<TouchPad>();
                tp.down = e => { sel = w; w.rt.SetAsLastSibling(); refresh(); };
                tp.drag = e =>
                {
                    if (e == null) return;
                    w.it.pos += e.delta * (1080f / Screen.height);
                    // keep the button on screen
                    var r = safe.rect; float h = w.it.size * 0.5f;
                    float x0 = w.it.left ? h : -r.width + h, x1 = w.it.left ? r.width - h : -h;
                    w.it.pos = new Vector2(Mathf.Clamp(w.it.pos.x, x0, x1), Mathf.Clamp(w.it.pos.y, h, r.height - h));
                    refresh();
                };
            }

            // toolbar
            var bar = UIK.Img(safe, "toolbar", new Color(0.04f, 0.045f, 0.04f, 0.92f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(1180, 86), true);
            float x = -590 + 20;
            Func<string, float, Color, Color, Action, Image> tb = (label, wdt, bg, fg, act) =>
            {
                var b = UIK.Img(bar.transform, label, bg, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x + 590, 0), new Vector2(wdt, 62), true);
                b.rectTransform.pivot = new Vector2(0, 0.5f); b.rectTransform.anchoredPosition = new Vector2(x + 590, 0);
                UIK.Txt(b.transform, "t", label, 26, fg, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
                if (act != null) b.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); act(); });
                x += wdt + 12;
                return b;
            };
            var chip = new Color(1, 1, 1, 0.08f); var dark = new Color(0.08f, 0.06f, 0.03f);
            tb(L.S("size") + "  −", 170, chip, Pal.Text, () => { if (sel != null) { sel.it.size = Mathf.Max(sel.it.defSize * 0.6f, sel.it.size - sel.it.defSize * 0.1f); refresh(); } });
            var st = tb("", 110, Color.clear, Pal.Accent, null); sizeTxt = st.GetComponentInChildren<Text>();
            tb(L.S("size") + "  +", 170, chip, Pal.Text, () => { if (sel != null) { sel.it.size = Mathf.Min(sel.it.defSize * 1.6f, sel.it.size + sel.it.defSize * 0.1f); refresh(); } });
            tb(L.S("reset"), 200, chip, Pal.Text, () => { foreach (var w in ws) { w.it.pos = w.it.defPos; w.it.size = w.it.defSize; } refresh(); });
            tb(L.S("cancel"), 200, chip, Pal.Text, () => { UnityEngine.Object.Destroy(panel.gameObject); onDone?.Invoke(); });
            tb(L.S("save"), 210, Pal.Accent, dark, () =>
            {
                HudLayout.Save(items);
                if (HUD.I != null) HUD.I.RebuildControls();
                UnityEngine.Object.Destroy(panel.gameObject); onDone?.Invoke();
            });
            UIK.Txt(safe, "hint", L.S("layout_hint"), 24, Pal.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -214), new Vector2(1400, 40));
            bar.transform.SetAsLastSibling();
            refresh();
        }

        static void Ghost(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var g = UIK.Img(parent, "ghost", new Color(1, 1, 1, 0.06f), anchor, anchor, pos, size);
            var o = g.gameObject.AddComponent<Outline>(); o.effectColor = new Color(1, 1, 1, 0.25f); o.effectDistance = new Vector2(2, -2);
        }
    }
}
