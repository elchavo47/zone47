using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Zone47
{
    /// Lobby (modern layout): nav on the left, player card top-right, big mode title bottom-left,
    /// map card + DEPLOY bottom-right. Plus the Match Settings, Profile and Edit Profile screens.
    public partial class Boot
    {
        static readonly Color Ink = new Color(0.08f, 0.06f, 0.03f);
        static readonly Color Glass = new Color(0.04f, 0.045f, 0.045f, 0.72f);
        static string T(string tr, string en) => Settings.Turkish ? tr : en;

        void Chip(RectTransform root, ref float x, float y, string text, Action onTap)
        {
            var c = UIK.Img(root, "chip", Glass, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(10, 46), onTap != null);
            c.rectTransform.pivot = new Vector2(0, 0); c.rectTransform.anchoredPosition = new Vector2(x, y);
            var ol = c.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(1, 1, 1, 0.2f); ol.effectDistance = new Vector2(2, 2);
            var t = UIK.Txt(c.transform, "t", text, 22, Pal.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
            t.supportRichText = true; t.fontStyle = FontStyle.Bold;
            float w = t.preferredWidth + 40f; c.rectTransform.sizeDelta = new Vector2(w, 46);
            x += w + 12f;
            if (onTap != null) c.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); onTap(); });
        }

        static Texture2D vgrad;
        static Texture2D VGradient()
        {
            if (vgrad != null) return vgrad;
            vgrad = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 64; i++) vgrad.SetPixel(0, i, new Color(1, 1, 1, Mathf.Pow(1f - i / 63f, 1.4f)));
            vgrad.Apply(); return vgrad;
        }

        Text toastTxt; float toastUntil;
        void Toast(string s)
        {
            if (menuCanvas == null) return;
            if (toastTxt == null)
            {
                toastTxt = UIK.Txt(menuCanvas.transform, "toast", "", 28, Pal.Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(1200, 50), true);
                toastTxt.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            }
            toastTxt.text = s; toastTxt.transform.SetAsLastSibling();
            StopCoroutine("ToastFade"); StartCoroutine("ToastFade");
        }
        System.Collections.IEnumerator ToastFade() { toastTxt.color = Pal.Accent; yield return new WaitForSeconds(1.6f); float a = 1; while (a > 0 && toastTxt != null) { a -= Time.deltaTime * 2f; toastTxt.color = new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, a); yield return null; } }

        // ------------------------------------------------------------ shared screen chrome
        RectTransform MakeScreen(string name, string kicker, string title, Action onClose)
        {
            var p = UIK.Full(menuCanvas.transform, name);
            UIK.Img(p, "bg", new Color(0.02f, 0.025f, 0.03f, 0.9f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            var root = UIK.Full(p, "root"); root.gameObject.AddComponent<SafeArea>();
            UIK.Txt(root, "k", kicker, 20, Pal.Accent, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, -46), new Vector2(900, 28), true).rectTransform.pivot = new Vector2(0, 1);
            UIK.Txt(root, "t", title, 50, Color.white, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, -74), new Vector2(1200, 64), true).rectTransform.pivot = new Vector2(0, 1);
            var close = UIK.Btn(root, "X   " + T("KAPAT", "CLOSE"), new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(200, 64), () => { Destroy(p.gameObject); onClose?.Invoke(); }, 24);
            var cr = (RectTransform)close.transform; cr.pivot = new Vector2(1, 1); cr.anchoredPosition = new Vector2(-70, -48);
            return root;
        }

        /// Row of selectable chips; `enabled` can grey out options.
        List<Image> Segment(RectTransform parent, Vector2 pos, string[] labels, Func<int> get, Action<int> set, Func<int, bool> enabled = null, float w = 0f)
        {
            var list = new List<Image>(); float x = pos.x;
            Action refresh = null;
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var b = UIK.Img(parent, "seg" + i, Color.white, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(10, 60), true);
                b.rectTransform.pivot = new Vector2(0, 1);
                var t = UIK.Txt(b.transform, "t", labels[i], 24, Pal.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
                float bw = w > 0 ? w : t.preferredWidth + 44f;
                b.rectTransform.sizeDelta = new Vector2(bw, 60); b.rectTransform.anchoredPosition = new Vector2(x, pos.y); x += bw + 8f;
                b.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (enabled != null && !enabled(idx)) return; Sfx.UI(); set(idx); refresh(); });
                list.Add(b);
            }
            refresh = () =>
            {
                for (int i = 0; i < list.Count; i++)
                {
                    bool on = i == get(), ok = enabled == null || enabled(i);
                    list[i].color = on ? Pal.Accent : new Color(1, 1, 1, ok ? 0.08f : 0.03f);
                    list[i].GetComponentInChildren<Text>().color = on ? Ink : ok ? Pal.Text : new Color(1, 1, 1, 0.25f);
                }
            };
            refresh();
            segRefreshers.Add(refresh);
            return list;
        }
        readonly List<Action> segRefreshers = new List<Action>();

        // ------------------------------------------------------------ match settings
        /// `forRoom`: opened from the online room (closing returns to the room; the button just confirms).
        void ShowMatchSettings(Action forRoom = null)
        {
            var R = Z47Refs.I;
            segRefreshers.Clear();
            RectTransform root = null;
            root = MakeScreen("MatchSettings", Net.Online ? T("PARTİ", "PARTY") : T("OYNA", "PLAY"), T("MAÇ AYARLARI", "MATCH SETTINGS"), forRoom ?? RebuildMenu);
            UIK.Txt(root, "mh", T("HARİTA", "MAP"), 20, Pal.Dim, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, -170), new Vector2(400, 28), true).rectTransform.pivot = new Vector2(0, 1);
            var prefabs = new GameObject[] { R.mapPrefab, R.portPrefab, R.cityPrefab };
            string[] nk = { "mapname", "mapname2", "mapname3" }, ik = { "mapinfo", "mapinfo2", "mapinfo3" };
            var cards = new List<(Image frame, RawImage pic, GameObject tag, int id)>();
            Action refreshMaps = () =>
            {
                foreach (var c in cards)
                {
                    bool on = c.id == Settings.Map;
                    c.frame.color = on ? Pal.Accent : new Color(1, 1, 1, 0.15f);
                    c.pic.color = on ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                    c.tag.SetActive(on);
                }
                foreach (var r in segRefreshers) r();
            };
            float x = 70;
            for (int id = 0; id < prefabs.Length; id++)
            {
                if (prefabs[id] == null) continue;
                int mid = id;
                var frame = UIK.Img(root, "map" + id, Pal.Accent, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(520, 300), true);
                frame.rectTransform.pivot = new Vector2(0, 1); frame.rectTransform.anchoredPosition = new Vector2(x, -206);
                var inner = UIK.Img(frame.transform, "in", Color.black, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-6, -6));
                inner.gameObject.AddComponent<RectMask2D>();
                var pic = UIK.Rect(inner.transform, "pic", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 293)).gameObject.AddComponent<RawImage>();
                pic.raycastTarget = false;
                if (R.mapThumbs != null && id < R.mapThumbs.Length && R.mapThumbs[id] != null) pic.texture = R.mapThumbs[id];
                var sh = UIK.Rect(inner.transform, "sh", new Vector2(0, 0), new Vector2(1, 0.6f), Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
                sh.texture = VGradient(); sh.color = new Color(0, 0, 0, 0.92f); sh.raycastTarget = false;
                UIK.Txt(inner.transform, "d", L.S(ik[id]), 15, Pal.Dim, TextAnchor.LowerLeft, Vector2.zero, Vector2.one, new Vector2(18, 54), new Vector2(-36, -64), true);
                UIK.Txt(inner.transform, "n", L.S(nk[id]), 26, Color.white, TextAnchor.LowerLeft, Vector2.zero, Vector2.one, new Vector2(18, 14), new Vector2(-36, -24), true);
                var tag = UIK.Img(inner.transform, "tag", Pal.Accent, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(110, 30));
                tag.rectTransform.pivot = new Vector2(1, 1); tag.rectTransform.anchoredPosition = new Vector2(-12, -12);
                UIK.Txt(tag.transform, "t", L.S("selected"), 15, Ink, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
                frame.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); Settings.Map = mid; if (!Weather.Valid(Settings.Weather, mid)) Settings.Weather = Weather.Random; PlayerPrefs.Save(); refreshMaps(); });
                cards.Add((frame, pic, tag.gameObject, id));
                x += 540;
            }

            // team size / weather / difficulty
            float y = -560;
            UIK.Txt(root, "th", L.S("mode"), 20, Pal.Dim, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, y), new Vector2(400, 28), true).rectTransform.pivot = new Vector2(0, 1);
            var labels = new string[MatchManager.Presets.Length]; for (int i = 0; i < labels.Length; i++) labels[i] = MatchManager.Presets[i].label;
            var modeInfo = UIK.Txt(root, "mi", "", 18, Pal.Dim, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, y - 112), new Vector2(700, 26), true);
            modeInfo.rectTransform.pivot = new Vector2(0, 1);
            Action mi = () => { var cp = MatchManager.Cur; modeInfo.text = T("HEDEF ", "TARGET ") + cp.target + "  ·  " + Mathf.RoundToInt(cp.time / 60f) + T(" DK", " MIN"); };
            Segment(root, new Vector2(70, y - 40), labels, () => Settings.TeamPreset, v => { Settings.TeamPreset = v; PlayerPrefs.Save(); mi(); }, null, 104);
            mi();
            float wx0 = 70 + 6 * 112 + 60;
            UIK.Txt(root, "wh", L.S("weather"), 20, Pal.Dim, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(wx0, y), new Vector2(400, 28), true).rectTransform.pivot = new Vector2(0, 1);
            Segment(root, new Vector2(wx0, y - 40), new[] { L.S("wx0"), L.S("wx1"), L.S("wx2"), L.S("wx3") }, () => Settings.Weather, v => { Settings.Weather = v; PlayerPrefs.Save(); }, i => Weather.Valid(i, Settings.Map));
            UIK.Txt(root, "dh", L.S("difficulty"), 20, Pal.Dim, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, y - 170), new Vector2(400, 28), true).rectTransform.pivot = new Vector2(0, 1);
            Segment(root, new Vector2(70, y - 210), new[] { L.S("easy"), L.S("normal"), L.S("hard") }, () => Settings.Difficulty, v => { Settings.Difficulty = v; PlayerPrefs.Save(); }, null, 170);

            Action act = StartMatch;
            if (forRoom != null) act = () => { Destroy(root.parent.gameObject); forRoom(); };
            var go = UIK.Btn(root, forRoom != null ? T("TAMAM", "DONE") + "   »" : L.S("deploy") + "   »", new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(560, 110), act, 48, true);
            var gr = (RectTransform)go.transform; gr.pivot = new Vector2(1, 0); gr.anchoredPosition = new Vector2(-70, 62);
            refreshMaps();
        }

        // ------------------------------------------------------------ profile
        void ShowProfile()
        {
            var root = MakeScreen("Profile", T("OYUNCU", "PLAYER"), T("PROFİL", "PROFILE"), RebuildMenu);
            Progress.Split(Progress.XP, out int lv, out int into, out int need);

            // left card
            var card = UIK.Img(root, "card", new Color(1, 1, 1, 0.05f), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(560, 760));
            card.rectTransform.pivot = new Vector2(0, 1); card.rectTransform.anchoredPosition = new Vector2(70, -170);
            var C = card.transform;
            UIK.Img(C, "top", Pal.Accent, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 4)).rectTransform.pivot = new Vector2(0.5f, 1);
            Profile.Avatar(C, "av", new Vector2(0.5f, 1), new Vector2(0, -150), 210, Settings.Callsign, Profile.Flag, Profile.SafeFrame);
            UIK.Txt(C, "n", Settings.Callsign.ToUpper(), 44, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -300), new Vector2(520, 56), true);
            UIK.Txt(C, "ti", Profile.CurrentTitle.ToUpper(), 22, Pal.Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -344), new Vector2(520, 30), true);
            var fl = Countries.Make(C, "fl", Profile.Flag, new Vector2(0.5f, 1), new Vector2(-120, -390), 30);
            var cn = UIK.Txt(C, "cn", Countries.Name(Profile.Flag), 20, Pal.Text, TextAnchor.MiddleLeft, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(80, -390), new Vector2(360, 30), false);
            float cnw = cn.preferredWidth; float tot = 40 + 12 + cnw;
            ((RectTransform)fl.transform).anchoredPosition = new Vector2(-tot / 2f + 20, -390);
            cn.rectTransform.pivot = new Vector2(0, 0.5f); cn.rectTransform.anchoredPosition = new Vector2(-tot / 2f + 52, -390);

            UIK.Txt(C, "lv", L.S("level") + " " + lv, 30, Pal.Accent, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -462), new Vector2(-80, 40), true);
            UIK.Txt(C, "xp", into + " / " + need + " XP", 18, Pal.Dim, TextAnchor.MiddleRight, new Vector2(0, 1), new Vector2(1, 1), new Vector2(-40, -462), new Vector2(-80, 40), false);
            var tr = UIK.Img(C, "trk", new Color(1, 1, 1, 0.1f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -494), new Vector2(-80, 10));
            var fill = UIK.Img(tr.transform, "f", Pal.Accent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).rectTransform;
            fill.pivot = new Vector2(0, 0.5f); fill.anchoredPosition = Vector2.zero; fill.localScale = new Vector3(into / (float)need, 1, 1);
            UIK.Txt(C, "ach", T("BAŞARIMLAR  ", "ACHIEVEMENTS  ") + Profile.UnlockedCount + " / " + Profile.Achievements.Length, 20, Pal.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -540), new Vector2(520, 30), true);
            var eb = UIK.Btn(C, T("PROFİLİ DÜZENLE", "EDIT PROFILE"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(460, 84), () => { Destroy(root.parent.gameObject); ShowProfileEdit(0); }, 30, true);

            // stats grid
            float sx = 680;
            UIK.Txt(root, "sh", T("İSTATİSTİKLER", "STATS"), 20, Pal.Dim, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(sx, -170), new Vector2(600, 28), true).rectTransform.pivot = new Vector2(0, 1);
            (string k, string v)[] st =
            {
                (T("MAÇ", "MATCHES"), Profile.Matches.ToString()),
                (T("GALİBİYET", "WIN RATE"), "%" + Mathf.RoundToInt(Profile.WinRate * 100f)),
                (T("LEŞ", "KILLS"), Profile.Kills.ToString()),
                ("K/D", Profile.KD.ToString("0.00")),
                (T("KAFADAN", "HEADSHOTS"), Profile.Headshots.ToString()),
                (T("İSABET", "ACCURACY"), "%" + Mathf.RoundToInt(Profile.Accuracy * 100f)),
                (T("EN UZUN ATIŞ", "LONGEST KILL"), Mathf.RoundToInt(Profile.Longest) + " m"),
                ("MVP", Profile.Mvps.ToString()),
            };
            for (int i = 0; i < st.Length; i++)
            {
                var b = UIK.Img(root, "st" + i, new Color(1, 1, 1, 0.05f), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(270, 104));
                b.rectTransform.pivot = new Vector2(0, 1); b.rectTransform.anchoredPosition = new Vector2(sx + (i % 4) * 282, -206 - (i / 4) * 116);
                UIK.Txt(b.transform, "v", st[i].v, 38, Color.white, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(20, -12), new Vector2(-40, -20), true);
                UIK.Txt(b.transform, "k", st[i].k, 16, Pal.Dim, TextAnchor.LowerLeft, Vector2.zero, Vector2.one, new Vector2(20, 12), new Vector2(-40, -20), true);
            }

            // achievements (scroll)
            UIK.Txt(root, "ah", T("BAŞARIMLAR", "ACHIEVEMENTS"), 20, Pal.Dim, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(sx, -460), new Vector2(600, 28), true).rectTransform.pivot = new Vector2(0, 1);
            var content = ScrollArea(root, new Vector2(sx, -496), new Vector2(1116, 434), out var setH);
            var A = Profile.Achievements;
            for (int i = 0; i < A.Length; i++)
            {
                var a = A[i]; bool ok = Profile.Unlocked(a); float v = Mathf.Min(a.value(), a.target);
                var it = UIK.Img(content, "a" + i, ok ? new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.14f) : new Color(1, 1, 1, 0.04f), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(552, 98));
                it.rectTransform.pivot = new Vector2(0, 1); it.rectTransform.anchoredPosition = new Vector2((i % 2) * 564, -(i / 2) * 108);
                var badge = UIK.Img(it.transform, "b", ok ? Pal.Accent : new Color(1, 1, 1, 0.12f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(50, 0), new Vector2(60, 60));
                if (Z47Refs.I.avatarHex != null) { badge.enabled = false; var hx = badge.gameObject.AddComponent<RawImage>(); hx.texture = Z47Refs.I.avatarHex; hx.color = ok ? Pal.Accent : new Color(1, 1, 1, 0.12f); hx.raycastTarget = false; }
                UIK.Txt(badge.transform, "i", ok ? (i + 1).ToString() : "?", 26, ok ? Ink : new Color(1, 1, 1, 0.4f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
                UIK.Txt(it.transform, "n", Profile.AName(a), 24, ok ? Color.white : Pal.Text, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(100, -12), new Vector2(-130, -20), true);
                UIK.Txt(it.transform, "d", Profile.ADesc(a), 16, Pal.Dim, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(100, -42), new Vector2(-130, -50), false);
                var pt = UIK.Img(it.transform, "pt", new Color(1, 1, 1, 0.1f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(46, 16), new Vector2(-220, 6));
                var pf = UIK.Img(pt.transform, "pf", ok ? Pal.Accent : new Color(0.75f, 0.73f, 0.68f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).rectTransform;
                pf.pivot = new Vector2(0, 0.5f); pf.anchoredPosition = Vector2.zero; pf.localScale = new Vector3(v / a.target, 1, 1);
                UIK.Txt(it.transform, "pv", Mathf.FloorToInt(v) + " / " + a.target, 15, Pal.Dim, TextAnchor.LowerRight, Vector2.zero, Vector2.one, new Vector2(-14, 8), new Vector2(-28, -16), true);
            }
            setH(((A.Length + 1) / 2) * 108f);
        }

        /// Vertical scroll area; returns the content rect (top-left anchored children).
        RectTransform ScrollArea(RectTransform parent, Vector2 pos, Vector2 size, out Action<float> setHeight)
        {
            var vp = UIK.Img(parent, "scroll", new Color(0, 0, 0, 0.01f), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, size, true);
            vp.rectTransform.pivot = new Vector2(0, 1); vp.rectTransform.anchoredPosition = pos;
            vp.gameObject.AddComponent<RectMask2D>();
            var content = UIK.Rect(vp.transform, "content", new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, size.y));
            content.pivot = new Vector2(0.5f, 1); content.anchoredPosition = Vector2.zero;
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.content = content; sr.viewport = vp.rectTransform; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic; sr.scrollSensitivity = 30f;
            setHeight = h => content.sizeDelta = new Vector2(0, Mathf.Max(h, size.y));
            return content;
        }

        // ------------------------------------------------------------ edit profile
        void ShowProfileEdit(int tab)
        {
            var root = MakeScreen("ProfileEdit", T("PROFİL", "PROFILE"), T("PROFİLİ DÜZENLE", "EDIT PROFILE"), () => ShowProfile());
            string[] tabs = { T("BAYRAK", "FLAG"), T("ÇERÇEVE", "FRAME"), T("UNVAN", "TITLE") };
            for (int i = 0; i < tabs.Length; i++)
            {
                int ti = i;
                var b = UIK.Img(root, "tab" + i, i == tab ? Pal.Accent : new Color(1, 1, 1, 0.08f), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(230, 60), true);
                b.rectTransform.pivot = new Vector2(0, 1); b.rectTransform.anchoredPosition = new Vector2(70 + i * 240, -164);
                UIK.Txt(b.transform, "t", tabs[i], 24, i == tab ? Ink : Pal.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
                b.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); Destroy(root.parent.gameObject); ShowProfileEdit(ti); });
            }
            var nb = UIK.Btn(root, T("İSMİ DEĞİŞTİR", "CHANGE NAME"), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(300, 60), () => ShowNameDialog(false), 22);
            var nr = (RectTransform)nb.transform; nr.pivot = new Vector2(0, 1); nr.anchoredPosition = new Vector2(70 + 3 * 240 + 30, -164);

            var content = ScrollArea(root, new Vector2(70, -250), new Vector2(1780, 740), out var setH);
            if (tab == 0)
            {
                // flags: 10 per row, current one framed
                int cols = 10; float cw = 172, ch = 150;
                var cells = new List<(Image bg, string code)>();
                for (int i = 0; i < Countries.Count; i++)
                {
                    string code = Countries.Codes[i];
                    var c = UIK.Img(content, "f" + i, new Color(1, 1, 1, 0.04f), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(cw - 10, ch - 10), true);
                    c.rectTransform.pivot = new Vector2(0, 1); c.rectTransform.anchoredPosition = new Vector2((i % cols) * cw, -(i / cols) * ch);
                    Countries.Make(c.transform, "fl", code, new Vector2(0.5f, 1), new Vector2(0, -50), 72);
                    var nt = UIK.Txt(c.transform, "n", Countries.Name(i), 14, Pal.Text, TextAnchor.UpperCenter, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 26), new Vector2(-10, 40), false);
                    nt.horizontalOverflow = HorizontalWrapMode.Wrap;
                    cells.Add((c, code));
                    c.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                    {
                        Sfx.UI(); Profile.Flag = code;
                        foreach (var x in cells) x.bg.color = x.code == code ? new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.45f) : new Color(1, 1, 1, 0.04f);
                        Toast(Countries.Name(code));
                    });
                    if (code == Profile.Flag) c.color = new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.45f);
                }
                setH(((Countries.Count + cols - 1) / cols) * ch);
            }
            else if (tab == 1)
            {
                var cells = new List<(Image bg, int i)>();
                for (int i = 0; i < Profile.FrameCount; i++)
                {
                    int fi = i; bool ok = Profile.FrameUnlocked(i);
                    var c = UIK.Img(content, "fr" + i, new Color(1, 1, 1, 0.04f), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(330, 340), true);
                    c.rectTransform.pivot = new Vector2(0, 1); c.rectTransform.anchoredPosition = new Vector2((i % 5) * 350, -(i / 5) * 360);
                    var av = Profile.Avatar(c.transform, "av", new Vector2(0.5f, 1), new Vector2(0, -130), 170, Settings.Callsign, Profile.Flag, i);
                    if (!ok) foreach (var g in av.GetComponentsInChildren<Graphic>()) g.color = new Color(g.color.r * 0.35f, g.color.g * 0.35f, g.color.b * 0.35f, g.color.a);
                    UIK.Txt(c.transform, "n", Profile.FrameName(i), 24, ok ? Color.white : Pal.Dim, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 66), new Vector2(0, 30), true);
                    UIK.Txt(c.transform, "h", ok ? T("AÇIK", "UNLOCKED") : T("KİLİTLİ · ", "LOCKED · ") + Profile.FrameHow(i), 16, ok ? Pal.Accent : Pal.Dim, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 30), new Vector2(-10, 26), false);
                    cells.Add((c, i));
                    c.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                    {
                        if (!Profile.FrameUnlocked(fi)) { Toast(Profile.FrameHow(fi)); return; }
                        Sfx.UI(); Profile.Frame = fi;
                        foreach (var x in cells) x.bg.color = x.i == fi ? new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.35f) : new Color(1, 1, 1, 0.04f);
                    });
                    if (i == Profile.SafeFrame) c.color = new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.35f);
                }
                setH(((Profile.FrameCount + 4) / 5) * 360f);
            }
            else
            {
                var titles = Profile.Titles();
                var cells = new List<(Image bg, int i)>();
                for (int i = -1; i < titles.Count; i++)
                {
                    int ti = i; bool ok = i < 0 || titles[i].unlocked;
                    int slot = i + 1;
                    var c = UIK.Img(content, "t" + slot, new Color(1, 1, 1, 0.04f), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(576, 86), true);
                    c.rectTransform.pivot = new Vector2(0, 1); c.rectTransform.anchoredPosition = new Vector2((slot % 3) * 594, -(slot / 3) * 98);
                    string nm = i < 0 ? T("OTOMATİK (RÜTBEN)", "AUTO (YOUR RANK)") : titles[i].name.ToUpper();
                    UIK.Txt(c.transform, "n", nm, 24, ok ? Color.white : Pal.Dim, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(22, -12), new Vector2(-44, -20), true);
                    UIK.Txt(c.transform, "h", i < 0 ? Profile.RankFor(Progress.Level) : ok ? T("AÇIK", "UNLOCKED") : T("KİLİTLİ · ", "LOCKED · ") + titles[i].how, 15, ok ? Pal.Accent : Pal.Dim, TextAnchor.LowerLeft, Vector2.zero, Vector2.one, new Vector2(22, 12), new Vector2(-44, -20), false);
                    cells.Add((c, i));
                    c.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                    {
                        if (!ok) { Toast(titles[ti].how); return; }
                        Sfx.UI(); Profile.Title = ti;
                        foreach (var x in cells) x.bg.color = x.i == ti ? new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.35f) : new Color(1, 1, 1, 0.04f);
                    });
                    bool cur = i == Profile.Title || (i < 0 && (Profile.Title < 0 || Profile.Title >= titles.Count || !titles[Profile.Title].unlocked));
                    if (cur) c.color = new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.35f);
                }
                setH(((titles.Count + 3) / 3) * 98f);
            }
        }
    }
}

// ================================================================ party lobby (was Z47Party.cs)
namespace Zone47
{
    /// Party lobby (PUBG style): your party's soldiers stand in the middle of the lobby with name plates,
    /// friends list on the right, invites, ready-up and the PLAY button. Offline you simply stand there alone.
    public partial class Boot
    {
        RectTransform lobbyRoot, lobbyUI;
        PartyStage stage;
        GameObject inviteBox;

        public struct PartyMember { public ulong id; public string name, flag, title; public int level, team; public bool ready, host, me; }

        static readonly Color Green = new Color(0.42f, 0.89f, 0.42f);
        const float RightW = 520f;

        // ------------------------------------------------------------ entry (called by BuildMenu)
        void BuildLobbyUI(RectTransform root)
        {
            MatchManager.RollNames();
            lobbyRoot = root;
            SocialPump.Ensure();
            if (Net.Available && Social.Available && !string.IsNullOrWhiteSpace(Settings.PlayerName)) Social.Init();
            if (menuCam != null) menuCam.cullingMask &= ~(1 << PartyStage.Layer);
            stage = PartyStage.Create(menuRoot.transform);
            var w = root.gameObject.AddComponent<LobbyWatcher>(); w.boot = this;
            RefreshLobby();
        }

        List<PartyMember> PartyMembers()
        {
            var l = new List<PartyMember>();
            if (!Net.Online)
            {
                l.Add(new PartyMember { id = 0, name = Settings.Callsign, flag = Profile.Flag, title = Profile.CurrentTitle, level = Progress.Level, team = 0, ready = true, me = true });
                return l;
            }
            foreach (var m in Net.Room)
                l.Add(new PartyMember { id = m.id, name = m.name, flag = m.flag, title = m.title, level = m.level, team = m.team, ready = m.lobbyReady || m.id == 0, host = m.id == 0, me = m.id == Net.MyId });
            l.Sort((a, b) => a.me != b.me ? (a.me ? -1 : 1) : 0);      // me first (centre)
            return l;
        }

        /// Rebuilds the lobby widgets (the 3D soldiers are only re-spawned when someone changed).
        public void RefreshLobby()
        {
            if (lobbyRoot == null) return;
            if (lobbyUI != null) Destroy(lobbyUI.gameObject);
            lobbyUI = UIK.Full(lobbyRoot, "ui");
            lobbyUI.SetAsFirstSibling();
            var R = Z47Refs.I; var U = lobbyUI;
            bool online = Net.Online;
            float W = Screen.height > 0 ? 1080f * Screen.width / Screen.height : 1920f;
            if (lobbyRoot.rect.width > 100f) W = lobbyRoot.rect.width;

            // ---- logo + nav (left)
            var logoTex = R.lobbyLogo != null ? R.lobbyLogo : R.logo;
            if (logoTex != null)
            {
                float h = 130f, w = h * logoTex.width / (float)logoTex.height;
                var lg = UIK.Rect(U, "logo", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(w, h));
                lg.pivot = new Vector2(0, 1); lg.anchoredPosition = new Vector2(70, -16);
                var ri = lg.gameObject.AddComponent<RawImage>(); ri.texture = logoTex; ri.raycastTarget = false;
            }
            (string t, string s, Action a, bool soon)[] nav =
            {
                (T("OYNA", "PLAY"), T("TAKIM ÖLÜM MAÇI", "TEAM DEATHMATCH"), null, false),
                (T("CEPHANELİK", "ARMORY"), T("SİLAHLAR · KAMUFLAJ", "WEAPONS · CAMO"), () => Toast(T("CEPHANELİK YAKINDA", "ARMORY COMING SOON")), false),
                (T("KOSTÜMLER", "OUTFITS"), T("YAKINDA", "COMING SOON"), () => Toast(T("KOSTÜMLER YAKINDA", "OUTFITS COMING SOON")), true),
                (T("PROFİL", "PROFILE"), T("SEVİYE · BAŞARIMLAR", "LEVEL · ACHIEVEMENTS"), () => ShowProfile(), false),
                (T("AYARLAR", "SETTINGS"), T("GRAFİK · KONTROL · SES", "GRAPHICS · CONTROLS · AUDIO"), () => ShowSettings(menuCanvas.transform, RebuildMenu), false),
            };
            for (int i = 0; i < nav.Length; i++)
            {
                bool on = i == 0;
                var item = UIK.Img(U, "nav" + i, on ? new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.2f) : new Color(0, 0, 0, 0.01f), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(360, 64), true);
                item.rectTransform.pivot = new Vector2(0, 1); item.rectTransform.anchoredPosition = new Vector2(70, -180 - i * 70);
                if (on) UIK.Img(item.transform, "bar", Pal.Accent, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(5, 0)).rectTransform.pivot = new Vector2(0, 0.5f);
                UIK.Txt(item.transform, "t", nav[i].t, 26, on ? Color.white : new Color(0.62f, 0.6f, 0.55f), TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(22, -6), new Vector2(-34, -8), true);
                UIK.Txt(item.transform, "s", nav[i].s, 15, on ? Pal.Accent : nav[i].soon ? Pal.Ally : new Color(0.45f, 0.43f, 0.4f), TextAnchor.LowerLeft, Vector2.zero, Vector2.one, new Vector2(22, 7), new Vector2(-34, -12), true);
                var act = nav[i].a;
                if (act != null) item.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); act(); });
            }

            // ---- stage (middle): free space between the nav and the right column
            float leftEdge = 450f, rightEdge = W - 70f - RightW - 20f;
            float free = Mathf.Max(500f, rightEdge - leftEdge);
            float stageW = Mathf.Clamp(free + 160f, 720f, 1400f), stageH = 780f;
            float cx = (leftEdge + rightEdge) * 0.5f - W * 0.5f;          // x offset of the stage centre from the screen centre
            int slots = free >= 1050f ? 5 : 3;
            var members = PartyMembers();
            stage.Sync(members, slots, stageW / stageH, Screen.height / 1080f * stageH);

            var glow = UIK.Rect(U, "glow", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(cx, 70), new Vector2(stageW * 1.05f, 190)).gameObject.AddComponent<RawImage>();
            glow.texture = RadialTex(); glow.color = new Color(Pal.Accent.r, Pal.Accent.g, Pal.Accent.b, 0.32f); glow.raycastTarget = false;
            var stRt = UIK.Rect(U, "stage", new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(stageW, stageH));
            stRt.pivot = new Vector2(0.5f, 0); stRt.anchoredPosition = new Vector2(cx, 120);
            var sImg = stRt.gameObject.AddComponent<RawImage>(); sImg.texture = stage.rt; sImg.raycastTarget = false;

            for (int i = 0; i < slots; i++)
            {
                var head = stage.SlotViewport(i, 2.02f); var feet = stage.SlotViewport(i, -0.02f); var mid = stage.SlotViewport(i, 1.05f);
                Vector2 P(Vector3 v) => new Vector2((v.x - 0.5f) * stageW, v.y * stageH);
                int mi = stage.MemberAt(i);
                if (mi >= 0)
                {
                    var m = members[mi];
                    Color tc = m.me ? Pal.Accent : (m.team == (Net.Me != null ? Net.Me.team : 0) ? Pal.Ally : Pal.Enemy);
                    var plate = UIK.Img(stRt, "plate" + i, new Color(0.03f, 0.04f, 0.04f, 0.8f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(10, m.me ? 74 : 66));
                    plate.rectTransform.pivot = new Vector2(0.5f, 0); plate.rectTransform.anchoredPosition = P(head) + new Vector2(0, 10);
                    UIK.Img(plate.transform, "b", tc, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 4)).rectTransform.pivot = new Vector2(0.5f, 0);
                    int fs = m.me ? 26 : 22;
                    var nm = UIK.Txt(plate.transform, "n", m.name.ToUpper() + (m.host && online ? "  <size=14><color=#F29E1F>" + T("KURUCU", "HOST") + "</color></size>" : ""), fs, Color.white, TextAnchor.UpperCenter, Vector2.zero, Vector2.one, new Vector2(14, -6), new Vector2(-28 - fs * 1.1f, -8), true);
                    nm.supportRichText = true;
                    float nw = Mathf.Max(170f, nm.preferredWidth + 34f + fs * 1.3f);
                    plate.rectTransform.sizeDelta = new Vector2(nw, m.me ? 74 : 66);
                    nm.rectTransform.offsetMin = new Vector2(fs * 1.3f + 18, nm.rectTransform.offsetMin.y);
                    var fl = Countries.Make(plate.transform, "fl", m.flag, new Vector2(0, 1), Vector2.zero, fs * 0.7f);
                    fl.rectTransform.pivot = new Vector2(0, 1); fl.rectTransform.anchoredPosition = new Vector2(14, -10);
                    UIK.Txt(plate.transform, "lv", T("SV ", "LV ") + m.level + "  ·  " + (m.title ?? "").ToUpper(), 15, Pal.Accent, TextAnchor.LowerCenter, Vector2.zero, Vector2.one, new Vector2(0, 9), new Vector2(-16, -12), true);
                    if (online)
                    {
                        var st = UIK.Img(stRt, "st" + i, new Color(0.03f, 0.04f, 0.04f, 0.85f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(170, 38));
                        st.rectTransform.pivot = new Vector2(0.5f, 1); st.rectTransform.anchoredPosition = P(feet) + new Vector2(0, -4);
                        var ol = st.gameObject.AddComponent<Outline>(); ol.effectColor = m.ready ? Green : new Color(0.45f, 0.43f, 0.4f); ol.effectDistance = new Vector2(2, 2);
                        UIK.Txt(st.transform, "t", m.ready ? T("HAZIR", "READY") : T("BEKLİYOR", "NOT READY"), 19, m.ready ? Green : Pal.Dim, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
                    }
                }
                else if (!online || Net.Room.Count < Net.MaxPlayers)
                {
                    // empty slot: + invite (offline it starts a party)
                    var plus = UIK.Img(stRt, "plus" + i, new Color(0.04f, 0.045f, 0.045f, 0.6f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(110, 110), true);
                    plus.rectTransform.anchoredPosition = P(mid) + new Vector2(0, 20);
                    var po = plus.gameObject.AddComponent<Outline>(); po.effectColor = Pal.Accent; po.effectDistance = new Vector2(3, 3);
                    UIK.Txt(plus.transform, "p", "+", 76, Pal.Accent, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0, 4), Vector2.zero, true);
                    UIK.Txt(plus.transform, "l", online ? T("DAVET ET", "INVITE") : T("PARTİ KUR", "PARTY UP"), 20, Pal.Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -26), new Vector2(240, 30), true);
                    plus.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); PlusTapped(); });
                }
            }

            // ---- party header (top, over the stage)
            var ph = UIK.Rect(U, "party", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(cx, -36), new Vector2(900, 64));
            UIK.Txt(ph, "k", T("PARTİ", "PARTY"), 24, Pal.Accent, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(140, 0), true).rectTransform.pivot = new Vector2(0, 0.5f);
            var cnt = UIK.Txt(ph, "n", (online ? Net.Room.Count : 1) + "<size=34><color=#9E998C> / " + Net.MaxPlayers + "</color></size>", 56, Color.white, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0, 1), new Vector2(118, 0), new Vector2(200, 0), true);
            cnt.supportRichText = true; cnt.rectTransform.pivot = new Vector2(0, 0.5f);
            if (online)
            {
                var cb = UIK.Img(ph, "code", new Color(0.04f, 0.045f, 0.045f, 0.8f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(400, 56), true);
                cb.rectTransform.pivot = new Vector2(0, 0.5f); cb.rectTransform.anchoredPosition = new Vector2(300, 0);
                UIK.Txt(cb.transform, "k", T("ODA KODU", "CODE"), 17, Pal.Dim, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-32, 0), true);
                UIK.Txt(cb.transform, "c", Net.Code, 30, Color.white, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(118, 0), new Vector2(-236, 0), true);
                var cp = UIK.Img(cb.transform, "cp", Pal.Accent, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(110, 38));
                cp.rectTransform.pivot = new Vector2(1, 0.5f); cp.rectTransform.anchoredPosition = new Vector2(-9, 0);
                UIK.Txt(cp.transform, "t", T("KOPYALA", "COPY"), 16, Ink, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
                cb.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); GUIUtility.systemCopyBuffer = Net.Code; Toast(T("KOD KOPYALANDI", "CODE COPIED")); });
                var lv = UIK.Btn(ph, T("ÇIK", "LEAVE"), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(110, 56), () => { Net.Leave(); }, 22);
                var lr = (RectTransform)lv.transform; lr.pivot = new Vector2(0, 0.5f); lr.anchoredPosition = new Vector2(712, 0);
            }
            else
            {
                UIK.Txt(ph, "h", Net.Available ? T("Arkadaşlarını davet et ya da tek başına botlara karşı oyna", "Invite friends or play solo against bots") : T("Tek başına botlara karşı", "Solo against bots"),
                    20, Pal.Dim, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(300, 0), new Vector2(-300, 0), false).rectTransform.pivot = new Vector2(0, 0.5f);
            }
            if (online && members.Count > slots)
                UIK.Txt(U, "more", "+" + (members.Count - slots) + T(" OYUNCU DAHA", " MORE"), 22, Pal.Dim, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(cx, 90), new Vector2(400, 30), true);

            // ---- hero (bottom left)
            var pr = online ? MatchManager.Presets[Mathf.Clamp(Net.RoomPreset, 0, MatchManager.Presets.Length - 1)] : MatchManager.Cur;
            var me = Net.Me;
            string kicker = online ? (me != null && me.team == 1 ? T("TAKIM B · KIRMIZI", "TEAM B · RED") : T("TAKIM A · MAVİ", "TEAM A · BLUE")) : T("ŞİMDİ OYNA", "PLAY NOW");
            UIK.Txt(U, "kick", kicker, 20, online && me != null && me.team == 1 ? Pal.Enemy : Pal.Accent, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(72, 196), new Vector2(700, 28), true).rectTransform.pivot = new Vector2(0, 0);
            var ht = UIK.Txt(U, "hero", T("TAKIM ÖLÜM MAÇI", "TEAM DEATHMATCH"), 54, Color.white, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(70, 124), new Vector2(900, 70), true);
            ht.rectTransform.pivot = new Vector2(0, 0); ht.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
            float x = 72;
            Chip(U, ref x, 60, "<color=#F29E1F>" + pr.label + "</color>  ·  " + T("HEDEF ", "TARGET ") + pr.target, null);
            Chip(U, ref x, 60, Mathf.RoundToInt(pr.time / 60f) + T(" DK", " MIN"), null);
            if (online)
            {
                int bots = Mathf.Max(0, pr.a - Net.TeamCount(0)) + Mathf.Max(0, pr.e - Net.TeamCount(1));
                Chip(U, ref x, 60, bots + T(" BOT", " BOTS"), null);
                Chip(U, ref x, 60, "<color=#5AB8FF>" + T("TAKIM DEĞİŞTİR", "SWITCH TEAM") + "</color>", () => Net.SwitchTeam());
            }
            else Chip(U, ref x, 60, T("ZORLUK: ", "DIFFICULTY: ") + "<color=#F29E1F>" + L.S(Settings.Difficulty == 0 ? "easy" : Settings.Difficulty == 1 ? "normal" : "hard") + "</color>",
                () => { Settings.Difficulty = (Settings.Difficulty + 1) % 3; PlayerPrefs.Save(); RefreshLobby(); });

            // ---- right column: player card, friends, settings chips, PLAY
            BuildPlayerCard(U);
            BuildFriends(U);
            string[] mk = { "mapname", "mapname2", "mapname3" };
            int map = online ? Net.RoomMap : Settings.Map, wxi = online ? Net.RoomWeather : Settings.Weather;
            Action openSettings = () =>
            {
                if (!online) ShowMatchSettings(RefreshLobby);
                else if (Net.IsHost) ShowMatchSettings(() => { Net.PullHostSettings(); RefreshLobby(); });
                else Toast(T("AYARLARI KURUCU SEÇER", "THE HOST PICKS THE SETTINGS"));
            };
            string third = online ? "<color=#F29E1F>" + CountReady() + " / " + Net.Room.Count + "</color> " + T("HAZIR", "READY") : T("DEĞİŞTİR  »", "CHANGE  »");
            string[] notes = { T("HARİTA: ", "MAP: ") + "<color=#F29E1F>" + L.S(mk[Mathf.Clamp(map, 0, 2)]) + "</color>", T("HAVA: ", "WEATHER: ") + "<color=#F29E1F>" + L.S("wx" + wxi) + "</color>", third };
            float nw3 = (RightW - 16f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var n = UIK.Img(U, "note" + i, new Color(0.04f, 0.045f, 0.045f, 0.75f), new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(nw3, 48), true);
                n.rectTransform.pivot = new Vector2(1, 0); n.rectTransform.anchoredPosition = new Vector2(-70 - (2 - i) * (nw3 + 8), 190);
                var o = n.gameObject.AddComponent<Outline>(); o.effectColor = new Color(1, 1, 1, 0.14f); o.effectDistance = new Vector2(2, 2);
                var tx = UIK.Txt(n.transform, "t", notes[i], 17, Pal.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-10, 0), true); tx.supportRichText = true;
                tx.resizeTextForBestFit = true; tx.resizeTextMinSize = 11; tx.resizeTextMaxSize = 17;
                n.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); openSettings(); });
            }
            BuildPlayButton(U);
        }

        public void ShowToastMsg(string s) => Toast(s);

        int CountReady() { int n = 0; foreach (var m in Net.Room) if (m.lobbyReady || m.id == 0) n++; return n; }

        void BuildPlayButton(RectTransform U)
        {
            bool online = Net.Online, host = Net.IsHost;
            string big, small; Action act; bool lit = true;
            if (!online) { big = T("OYNA", "PLAY"); small = T("BOTLARA KARŞI", "VS BOTS"); act = StartMatch; }
            else if (host)
            {
                string wait = Net.Waiting;
                big = T("OYNA", "PLAY");
                small = wait == null ? Net.Room.Count + T(" OYUNCU · HAZIR", " PLAYERS · READY") : wait.ToUpper() + T(" BEKLENİYOR", " NOT READY");
                lit = wait == null;
                act = () => { if (Net.AllReady) Net.StartGame(); else Toast(T("HERKES HAZIR OLMALI", "EVERYONE MUST BE READY")); };
            }
            else
            {
                bool r = Net.MyReady;
                big = r ? T("HAZIRSIN", "READY") : T("HAZIRIM", "I'M READY");
                small = r ? T("İPTAL İÇİN DOKUN · KURUCU BAŞLATACAK", "TAP TO CANCEL · HOST STARTS") : T("HAZIR OLUNCA DOKUN", "TAP WHEN READY");
                lit = !r;
                act = () => Net.ToggleReady();
            }
            var b = UIK.Img(U, "play", lit ? Pal.Accent : new Color(0.16f, 0.17f, 0.16f, 0.95f), new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(RightW, 120), true);
            b.rectTransform.pivot = new Vector2(1, 0); b.rectTransform.anchoredPosition = new Vector2(-70, 56);
            if (!lit) { var o = b.gameObject.AddComponent<Outline>(); o.effectColor = online && !host && Net.MyReady ? Green : Pal.Accent; o.effectDistance = new Vector2(3, 3); }
            Color tc = lit ? Ink : online && !host && Net.MyReady ? Green : Pal.Accent;
            UIK.Txt(b.transform, "t", big, 56, tc, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0, 14), new Vector2(0, -30), true);
            UIK.Txt(b.transform, "s", small, 19, lit ? new Color(Ink.r, Ink.g, Ink.b, 0.8f) : Pal.Dim, TextAnchor.LowerCenter, Vector2.zero, Vector2.one, new Vector2(0, 12), new Vector2(-20, -20), true);
            b.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); act(); });
        }

        void BuildPlayerCard(RectTransform U)
        {
            Progress.Split(Progress.XP, out int lv, out int into, out int need);
            var pc = UIK.Img(U, "player", Glass, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(RightW, 112), true);
            pc.rectTransform.pivot = new Vector2(1, 1); pc.rectTransform.anchoredPosition = new Vector2(-70, -24);
            Profile.Avatar(pc.transform, "av", new Vector2(0, 0.5f), new Vector2(58, 0), 84, Settings.Callsign, Profile.Flag, Profile.SafeFrame);
            nameTxt = UIK.Txt(pc.transform, "n", Settings.Callsign.ToUpper(), 30, Color.white, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(120, -10), new Vector2(-140, -16), true);
            UIK.Txt(pc.transform, "t", Profile.CurrentTitle.ToUpper() + "  ·  " + L.S("level") + " " + lv, 16, Pal.Accent, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(120, -48), new Vector2(-140, -54), true);
            string tag = Social.Ready && Social.MyTag.Length > 0 ? T("ETİKETİN: ", "YOUR TAG: ") + Social.MyTag : "";
            if (tag.Length > 0) UIK.Txt(pc.transform, "tag", tag, 16, Pal.Ally, TextAnchor.LowerLeft, Vector2.zero, Vector2.one, new Vector2(120, 12), new Vector2(-140, -20), true);
            var trk = UIK.Img(pc.transform, "xp", new Color(1, 1, 1, 0.12f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(60, 4), new Vector2(-150, 4));
            trk.rectTransform.pivot = new Vector2(0.5f, 0);
            var fill = UIK.Img(trk.transform, "f", Pal.Accent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).rectTransform;
            fill.pivot = new Vector2(0, 0.5f); fill.anchoredPosition = Vector2.zero; fill.localScale = new Vector3(into / (float)need, 1, 1);
            pc.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                Sfx.UI();
                if (Social.Ready && Social.MyTag.Length > 0) { GUIUtility.systemCopyBuffer = Social.MyTag; Toast(T("ETİKET KOPYALANDI · PROFİL İÇİN AVATARA DOKUN", "TAG COPIED · TAP THE AVATAR FOR PROFILE")); }
                else ShowProfile();
            });
            var avBtn = pc.transform.Find("av");
            if (avBtn != null) { var ri = avBtn.GetComponent<RawImage>(); if (ri != null) ri.raycastTarget = true; avBtn.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); ShowProfile(); }); }
        }

        // ------------------------------------------------------------ friends panel
        int friendTab;
        void BuildFriends(RectTransform U)
        {
            float top = -150f, h = 1080f - 150f - 260f;          // down to just above the settings chips
            var p = UIK.Img(U, "friends", new Color(0.03f, 0.04f, 0.04f, 0.8f), new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(RightW, h), true);
            p.rectTransform.pivot = new Vector2(1, 1); p.rectTransform.anchoredPosition = new Vector2(-70, top);
            var P = (RectTransform)p.transform;
            UIK.Img(P, "top", Pal.Accent, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 4)).rectTransform.pivot = new Vector2(0.5f, 1);
            UIK.Txt(P, "h", T("ARKADAŞLAR", "FRIENDS"), 26, Color.white, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -30), new Vector2(-36, 36), true);
            if (Social.Ready) UIK.Txt(P, "on", "<color=#6BE36B>●</color> " + Social.OnlineCount + T(" ÇEVRİMİÇİ", " ONLINE"), 17, Green, TextAnchor.MiddleRight, new Vector2(0, 1), new Vector2(1, 1), new Vector2(-18, -30), new Vector2(-36, 36), true).supportRichText = true;

            float bottomH = 64f;
            if (!Social.Available || !Social.Ready)
            {
                string msg = !Net.Available ? T("Online paketleri kurulu değil.\nUnity'de proje açılınca otomatik kurulur.", "Online packages not installed.")
                           : !Social.Available ? T("Arkadaş sistemi kurulu değil.\nUnity'de proje açılınca otomatik kurulur.", "Friends package not installed.")
                           : Social.Starting ? T("Bağlanıyor...", "Connecting...")
                           : (Social.Error.Length > 0 ? Social.Error : T("Bağlanılamadı", "Not connected"));
                UIK.Txt(P, "msg", msg, 20, Pal.Dim, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 20), new Vector2(-40, -120), false);
                if (Social.Available && !Social.Starting && Net.Available)
                {
                    var rb = UIK.Btn(P, T("TEKRAR DENE", "RETRY"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(260, 56), () => { Social.Init(); RefreshLobby(); }, 22);
                }
            }
            else
            {
                // tabs
                string[] tabs = { T("LİSTE", "LIST"), T("İSTEKLER", "REQUESTS") + (Social.Incoming.Count > 0 ? "  <color=#F29E1F>" + Social.Incoming.Count + "</color>" : "") };
                for (int i = 0; i < tabs.Length; i++)
                {
                    int ti = i;
                    var tb = UIK.Txt(P, "tab" + i, tabs[i], 18, friendTab == i ? Color.white : Pal.Dim, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18 + i * 130, -70), new Vector2(150, 30), true);
                    tb.supportRichText = true; tb.rectTransform.pivot = new Vector2(0, 0.5f); tb.raycastTarget = true;
                    if (friendTab == i) { var ul = UIK.Img(tb.transform, "u", Pal.Accent, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(tb.preferredWidth, 3)); ul.rectTransform.pivot = new Vector2(0, 1); }
                    tb.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); friendTab = ti; RefreshLobby(); });
                }
                UIK.Img(P, "sep", new Color(1, 1, 1, 0.1f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -90), new Vector2(0, 1));
                var content = ScrollArea(P, new Vector2(0, -92), new Vector2(RightW, h - 92 - bottomH - 8), out var setH);
                float y = 0f; const float rh = 62f;
                if (friendTab == 0)
                {
                    if (Social.Friends.Count == 0)
                    {
                        UIK.Txt(content, "e", T("Henüz arkadaşın yok.\nAşağıdan etiketle ekle (İsim#1234).", "No friends yet.\nAdd one by tag below (Name#1234)."), 19, Pal.Dim, TextAnchor.UpperCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -60), new Vector2(-40, 80), false);
                        y = 140f;
                    }
                    foreach (var f in Social.Friends) { FriendRow(content, f, y, rh); y += rh; }
                }
                else
                {
                    if (Social.Incoming.Count == 0 && Social.Outgoing.Count == 0)
                    { UIK.Txt(content, "e", T("Bekleyen istek yok.", "No pending requests."), 19, Pal.Dim, TextAnchor.UpperCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -60), new Vector2(-40, 40), false); y = 100f; }
                    foreach (var f in Social.Incoming) { RequestRow(content, f, y, rh, true); y += rh; }
                    foreach (var f in Social.Outgoing) { RequestRow(content, f, y, rh, false); y += rh; }
                }
                setH(y);
            }

            // bottom buttons: add friend / join by code
            float bw = (RightW - 36f - 10f) / 2f;
            var add = UIK.Img(P, "add", new Color(0, 0, 0, 0.01f), new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(bw, 50), true);
            add.rectTransform.pivot = new Vector2(0, 0); add.rectTransform.anchoredPosition = new Vector2(18, 12);
            var ao = add.gameObject.AddComponent<Outline>(); ao.effectColor = Pal.Accent; ao.effectDistance = new Vector2(2, 2);
            UIK.Txt(add.transform, "t", "+  " + T("ARKADAŞ EKLE", "ADD FRIEND"), 19, Pal.Accent, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            add.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                Sfx.UI();
                if (!Social.Ready) { Toast(T("ARKADAŞ SİSTEMİ HAZIR DEĞİL", "FRIENDS NOT READY")); return; }
                InputDialog(T("ARKADAŞ EKLE", "ADD FRIEND"), T("Arkadaşının etiketini yaz (profil kartında yazar)", "Type your friend's tag (shown on their player card)"), "Isim#1234", 30, false,
                    (v, done) => Social.AddByName(v, err => { done(err); if (err == null) { Toast(T("İSTEK GÖNDERİLDİ", "REQUEST SENT")); friendTab = 1; RefreshLobby(); } }));
            });
            var jc = UIK.Img(P, "join", new Color(0, 0, 0, 0.01f), new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(bw, 50), true);
            jc.rectTransform.pivot = new Vector2(1, 0); jc.rectTransform.anchoredPosition = new Vector2(-18, 12);
            var jo = jc.gameObject.AddComponent<Outline>(); jo.effectColor = new Color(1, 1, 1, 0.3f); jo.effectDistance = new Vector2(2, 2);
            UIK.Txt(jc.transform, "t", T("KODLA KATIL", "JOIN BY CODE"), 19, Pal.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            jc.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                Sfx.UI();
                if (!Net.Available) { Toast(T("ONLINE KURULU DEĞİL", "ONLINE NOT INSTALLED")); return; }
                InputDialog(T("KODLA KATIL", "JOIN BY CODE"), T("Arkadaşının oda kodunu yaz", "Type your friend's room code"), "ABC123", 8, true, (v, done) => JoinParty(v, done));
            });
        }

        void FriendRow(RectTransform content, Social.Friend f, float y, float rh)
        {
            var row = UIK.Img(content, "f", new Color(1, 1, 1, f.status > 0 ? 0.035f : 0.015f), new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(-24, rh - 6));
            row.rectTransform.pivot = new Vector2(0.5f, 1); row.rectTransform.anchoredPosition = new Vector2(0, -y - 3);
            float a = f.status > 0 ? 1f : 0.5f;
            if (!string.IsNullOrEmpty(f.flag)) { var fl = Countries.Make(row.transform, "fl", f.flag, new Vector2(0, 0.5f), Vector2.zero, 20); fl.rectTransform.pivot = new Vector2(0, 0.5f); fl.rectTransform.anchoredPosition = new Vector2(10, 0); fl.color = new Color(1, 1, 1, a); }
            UIK.Txt(row.transform, "n", f.name + (f.level > 0 ? "  <size=15><color=#9E998C>" + T("SV ", "LV ") + f.level + "</color></size>" : ""), 21, new Color(1, 1, 1, a), TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(48, -6), new Vector2(-200, -10), true).supportRichText = true;
            string st; Color sc;
            switch (f.status)
            {
                case 3: st = T("Maçta", "In a match"); sc = Pal.Accent; break;
                case 2: st = T("Partide", "In a party") + (f.size > 1 ? " (" + f.size + ")" : ""); sc = Green; break;
                case 1: st = T("Lobide", "In the lobby"); sc = Green; break;
                default: st = LastSeen(f.lastSeen); sc = new Color(0.5f, 0.5f, 0.48f); break;
            }
            UIK.Txt(row.transform, "s", "<color=#" + ColorUtility.ToHtmlStringRGB(sc) + ">●</color> " + st, 15, Pal.Dim, TextAnchor.LowerLeft, Vector2.zero, Vector2.one, new Vector2(48, 6), new Vector2(-200, -10), false).supportRichText = true;

            // action
            bool inMyParty = Net.Online && f.status == 2 && f.code == Net.Code;
            string label; Color col; Action act = null; bool filled = false;
            if (inMyParty) { label = T("PARTİDE", "IN PARTY"); col = Green; }
            else if (f.status == 3) { label = T("MAÇTA", "IN MATCH"); col = Pal.Dim; }
            else if (f.status == 0) { label = T("ÇEVRİMDIŞI", "OFFLINE"); col = new Color(0.45f, 0.43f, 0.4f); }
            else if (f.status == 2 && !string.IsNullOrEmpty(f.code) && !Net.Online) { label = T("KATIL", "JOIN"); col = Green; filled = true; string code = f.code; act = () => JoinParty(code, null); }
            else
            {
                label = T("DAVET ET", "INVITE"); col = Pal.Accent; filled = true; string id = f.id, nm = f.name;
                act = () => Social.Invite(id, err => Toast(err ?? (nm.ToUpper() + T(" DAVET EDİLDİ", " INVITED"))));
            }
            var b = UIK.Img(row.transform, "b", filled ? col : new Color(0, 0, 0, 0.01f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(150, 40), act != null);
            b.rectTransform.pivot = new Vector2(1, 0.5f); b.rectTransform.anchoredPosition = new Vector2(-10, 0);
            if (!filled) { var o = b.gameObject.AddComponent<Outline>(); o.effectColor = col; o.effectDistance = new Vector2(2, 2); }
            UIK.Txt(b.transform, "t", label, 16, filled ? Ink : col, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            if (act != null) b.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); act(); });
            // long-ish menu: tap the row to remove the friend
            row.raycastTarget = true;
            string fid = f.id, fname = f.name;
            row.gameObject.AddComponent<Button>().onClick.AddListener(() => ConfirmDialog(fname.ToUpper(), T("Arkadaşlıktan çıkarılsın mı?", "Remove from friends?"), T("ÇIKAR", "REMOVE"), () => Social.Remove(fid)));
        }

        void RequestRow(RectTransform content, Social.Friend f, float y, float rh, bool incoming)
        {
            var row = UIK.Img(content, "r", new Color(1, 1, 1, 0.035f), new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(-24, rh - 6));
            row.rectTransform.pivot = new Vector2(0.5f, 1); row.rectTransform.anchoredPosition = new Vector2(0, -y - 3);
            UIK.Txt(row.transform, "n", f.name, 21, Color.white, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(14, -6), new Vector2(-230, -10), true);
            UIK.Txt(row.transform, "s", incoming ? T("Seni eklemek istiyor", "Wants to be friends") : T("İstek gönderildi", "Request sent"), 15, Pal.Dim, TextAnchor.LowerLeft, Vector2.zero, Vector2.one, new Vector2(14, 6), new Vector2(-230, -10), false);
            string id = f.id;
            if (incoming)
            {
                var ok = UIK.Img(row.transform, "ok", Green, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(110, 40), true);
                ok.rectTransform.pivot = new Vector2(1, 0.5f); ok.rectTransform.anchoredPosition = new Vector2(-130, 0);
                UIK.Txt(ok.transform, "t", T("KABUL", "ACCEPT"), 16, Ink, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
                ok.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); Social.Accept(id); });
            }
            var no = UIK.Img(row.transform, "no", new Color(0, 0, 0, 0.01f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(110, 40), true);
            no.rectTransform.pivot = new Vector2(1, 0.5f); no.rectTransform.anchoredPosition = new Vector2(-10, 0);
            var o = no.gameObject.AddComponent<Outline>(); o.effectColor = Pal.Dim; o.effectDistance = new Vector2(2, 2);
            UIK.Txt(no.transform, "t", incoming ? T("REDDET", "DECLINE") : T("İPTAL", "CANCEL"), 16, Pal.Dim, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            no.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); if (incoming) Social.Decline(id); else Social.CancelRequest(id); });
        }

        static string LastSeen(DateTime t)
        {
            if (t.Year < 2000) return Settings.Turkish ? "Çevrimdışı" : "Offline";
            var d = DateTime.UtcNow - t.ToUniversalTime();
            if (d.TotalMinutes < 60) return Mathf.Max(1, (int)d.TotalMinutes) + (Settings.Turkish ? " dk önce" : " min ago");
            if (d.TotalHours < 24) return (int)d.TotalHours + (Settings.Turkish ? " saat önce" : " h ago");
            return (int)d.TotalDays + (Settings.Turkish ? " gün önce" : " d ago");
        }

        // ------------------------------------------------------------ party actions
        void PlusTapped()
        {
            if (!Net.Available) { Toast(T("ONLINE KURULU DEĞİL", "ONLINE NOT INSTALLED")); return; }
            if (!Net.Online)
            {
                if (Net.Busy) return;
                Toast(T("PARTİ KURULUYOR...", "CREATING PARTY..."));
                Net.HostRoom(err => { if (err != null) Toast(err); else { Toast(T("PARTİ KURULDU · ARKADAŞLARINI DAVET ET", "PARTY READY · INVITE FRIENDS")); Social.UpdatePresence(); } RefreshLobby(); });
                return;
            }
            Toast(Social.Ready ? T("SAĞDAKİ LİSTEDEN DAVET ET ya da KODU PAYLAŞ", "INVITE FROM THE LIST or SHARE THE CODE") : T("ODA KODUNU ARKADAŞINLA PAYLAŞ", "SHARE THE ROOM CODE"));
        }

        void JoinParty(string code, Action<string> done)
        {
            Action go = () => Net.JoinRoom(code, err =>
            {
                done?.Invoke(err);
                if (err != null) Toast(err); else Toast(T("PARTİYE KATILDIN", "JOINED THE PARTY"));
                RefreshLobby();
            });
            if (Net.Online) { Net.Leave(); StartCoroutine(Later(0.3f, go)); }       // leave my own party first
            else go();
            Toast(T("BAĞLANILIYOR...", "CONNECTING..."));
        }

        System.Collections.IEnumerator Later(float t, Action a) { yield return new WaitForSecondsRealtime(t); a(); }

        /// A friend invited us: banner with JOIN / DECLINE (auto hides).
        public void ShowInvite(string from, string code)
        {
            if (menuCanvas == null || Net.InMatch) { HUD.I?.ShowToast(from + (Settings.Turkish ? " seni partisine çağırdı" : " invited you"), 4f); return; }
            if (inviteBox != null) Destroy(inviteBox);
            var b = UIK.Img(menuCanvas.transform, "invite", new Color(0.03f, 0.04f, 0.04f, 0.95f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(760, 84), true);
            b.rectTransform.pivot = new Vector2(0.5f, 1); b.rectTransform.anchoredPosition = new Vector2(0, -110);
            inviteBox = b.gameObject;
            UIK.Img(b.transform, "s", Pal.Ally, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(6, 0)).rectTransform.pivot = new Vector2(0, 0.5f);
            UIK.Txt(b.transform, "t", "<color=#5AB8FF>" + from.ToUpper() + "</color> " + T("seni partisine çağırıyor", "invites you to the party"), 24, Pal.Text, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(26, 0), new Vector2(-300, 0), true).supportRichText = true;
            var ok = UIK.Img(b.transform, "ok", Green, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(130, 52), true);
            ok.rectTransform.pivot = new Vector2(1, 0.5f); ok.rectTransform.anchoredPosition = new Vector2(-152, 0);
            UIK.Txt(ok.transform, "t", T("KATIL", "JOIN"), 22, Ink, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            ok.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); Destroy(b.gameObject); JoinParty(code, null); });
            var no = UIK.Img(b.transform, "no", new Color(0, 0, 0, 0.01f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(130, 52), true);
            no.rectTransform.pivot = new Vector2(1, 0.5f); no.rectTransform.anchoredPosition = new Vector2(-12, 0);
            var o = no.gameObject.AddComponent<Outline>(); o.effectColor = Pal.Dim; o.effectDistance = new Vector2(2, 2);
            UIK.Txt(no.transform, "t", T("REDDET", "DECLINE"), 20, Pal.Dim, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            no.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.UI(); Destroy(b.gameObject); });
            Sfx.UI();
            Destroy(b.gameObject, 25f);
        }

        // ------------------------------------------------------------ dialogs
        void InputDialog(string title, string hint, string placeholder, int limit, bool upper, Action<string, Action<string>> submit)
        {
            var panel = UIK.Full(menuCanvas.transform, "Dialog");
            UIK.Img(panel, "bg", new Color(0, 0, 0, 0.8f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            var box = UIK.Img(panel, "box", new Color(0.05f, 0.055f, 0.05f, 0.97f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(820, 430));
            UIK.Img(box.transform, "top", Pal.Accent, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 6)).rectTransform.pivot = new Vector2(0.5f, 1);
            UIK.Txt(box.transform, "t", title, 40, Pal.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -64), new Vector2(760, 56), true);
            var hintT = UIK.Txt(box.transform, "h", hint, 21, Pal.Dim, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(760, 34), false);
            var fieldBg = UIK.Img(box.transform, "field", new Color(1, 1, 1, 0.08f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(640, 84), true);
            UIK.Img(fieldBg.transform, "line", Pal.Accent, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 3)).rectTransform.pivot = new Vector2(0.5f, 0);
            var txt = UIK.Txt(fieldBg.transform, "t", "", 38, Pal.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-30, 0), true); txt.supportRichText = false;
            var ph = UIK.Txt(fieldBg.transform, "ph", placeholder, 34, new Color(1, 1, 1, 0.22f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-30, 0), true);
            var input = fieldBg.gameObject.AddComponent<InputField>();
            input.textComponent = txt; input.placeholder = ph; input.characterLimit = limit; input.lineType = InputField.LineType.SingleLine;
            if (upper) { input.characterValidation = InputField.CharacterValidation.Alphanumeric; input.onValueChanged.AddListener(v => { string u = v.ToUpperInvariant(); if (u != v) input.text = u; }); }
            bool busy = false;
            UIK.Btn(box.transform, L.S("ok"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(150, 50), new Vector2(280, 84), () =>
            {
                if (busy) return; busy = true; hintT.text = Settings.Turkish ? "Bekle..." : "Please wait..."; hintT.color = Pal.Dim;
                submit(input.text.Trim(), err =>
                {
                    busy = false;
                    if (panel == null) return;
                    if (err == null) Destroy(panel.gameObject);
                    else { hintT.text = err; hintT.color = Pal.Enemy; }
                });
            }, 34, true);
            UIK.Btn(box.transform, L.S("cancel"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-150, 50), new Vector2(280, 84), () => Destroy(panel.gameObject), 30);
            input.ActivateInputField();
        }

        void ConfirmDialog(string title, string text, string yes, Action onYes)
        {
            var panel = UIK.Full(menuCanvas.transform, "Confirm");
            UIK.Img(panel, "bg", new Color(0, 0, 0, 0.8f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            var box = UIK.Img(panel, "box", new Color(0.05f, 0.055f, 0.05f, 0.97f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 320));
            UIK.Img(box.transform, "top", Pal.Accent, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 6)).rectTransform.pivot = new Vector2(0.5f, 1);
            UIK.Txt(box.transform, "t", title, 38, Pal.Text, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -64), new Vector2(680, 54), true);
            UIK.Txt(box.transform, "h", text, 24, Pal.Dim, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -124), new Vector2(680, 40), false);
            UIK.Btn(box.transform, yes, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(140, 50), new Vector2(250, 80), () => { Destroy(panel.gameObject); onYes(); }, 30, true);
            UIK.Btn(box.transform, L.S("cancel"), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-140, 50), new Vector2(250, 80), () => Destroy(panel.gameObject), 28);
        }

        static Texture2D radial;
        static Texture2D RadialTex()
        {
            if (radial != null) return radial;
            const int n = 128; radial = new Texture2D(n, n / 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n / 2; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - n * 0.5f) / (n * 0.5f), dy = (y - n * 0.25f) / (n * 0.25f);
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    radial.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(1f - d, 1.6f)));
                }
            radial.Apply(); return radial;
        }
    }

    /// Rebuilds the lobby when the party / friends change; shows incoming invites.
    public class LobbyWatcher : MonoBehaviour
    {
        public Boot boot;
        bool dirty; float pullT;
        void OnEnable() { Net.Changed += Mark; Net.Lost += OnLost; Social.Changed += Mark; Social.Invited += OnInvite; }
        void OnDisable() { Net.Changed -= Mark; Net.Lost -= OnLost; Social.Changed -= Mark; Social.Invited -= OnInvite; }
        void Mark() { dirty = true; }
        void OnLost(string why) { dirty = true; lostMsg = why; }
        void OnInvite(string from, string code) { if (boot != null) boot.ShowInvite(from, code); }
        string lostMsg;
        void Update()
        {
            if (boot == null) return;
            if (dirty) { dirty = false; boot.RefreshLobby(); }
            if (!string.IsNullOrEmpty(lostMsg)) { boot.ShowToastMsg(lostMsg); lostMsg = null; }
            pullT += Time.unscaledDeltaTime;
            if (pullT > 30f) { pullT = 0f; Social.Refresh(); }       // presence safety net
        }
    }

    /// The party's soldiers, rendered by their own camera into a transparent texture that the lobby UI shows
    /// (so it works over the lobby video). Lives far below the world on its own layer.
    public class PartyStage : MonoBehaviour
    {
        public const int Layer = 29;
        static readonly Vector3 Origin = new Vector3(0f, -1000f, 0f);
        public RenderTexture rt;
        Camera cam;
        float aspect = 1.8f;
        readonly string[] keys = new string[5];
        readonly GameObject[] bodies = new GameObject[5];
        readonly int[] memberAt = { -1, -1, -1, -1, -1 };
        int count = 5;

        // slot order: centre first, then alternating sides
        static readonly int[] Fill5 = { 2, 1, 3, 0, 4 };
        static readonly int[] Fill3 = { 1, 0, 2 };
        static readonly float[] X5 = { -2.0f, -1.0f, 0f, 1.0f, 2.0f }, Z5 = { 0.55f, 0.2f, 0f, 0.2f, 0.55f };
        static readonly float[] X3 = { -1.05f, 0f, 1.05f }, Z3 = { 0.25f, 0f, 0.25f };

        public static PartyStage Create(Transform parent)
        {
            var g = new GameObject("PartyStage");
            g.transform.SetParent(parent, false); g.transform.position = Origin;
            var s = g.AddComponent<PartyStage>();
            s.Build();
            return s;
        }

        void Build()
        {
            var cg = new GameObject("PartyCam"); cg.transform.SetParent(transform, false);
            cam = cg.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << Layer; cam.fieldOfView = 30f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 30f;
            cam.depth = -50;
            cam.allowHDR = false; cam.allowMSAA = true;                      // HDR buffers have no alpha: the soldiers must sit on a transparent background
            var d = cam.GetUniversalAdditionalCameraData(); d.renderPostProcessing = false; d.renderShadows = false;
            // lights only for this layer: key, fill, warm rim
            AddLight(new Vector3(2.6f, 3.2f, -3.6f), 14f, new Color(1f, 0.9f, 0.78f), 9f);
            AddLight(new Vector3(0f, 2.8f, 3.6f), 14f, new Color(1f, 0.62f, 0.3f), 8f);
        }

        void AddLight(Vector3 local, float range, Color c, float intensity)
        {
            var lg = new GameObject("PartyLight"); lg.transform.SetParent(transform, false); lg.transform.localPosition = local;
            lg.transform.LookAt(transform.position + Vector3.up * 1f);
            var l = lg.AddComponent<Light>(); l.type = LightType.Spot; l.spotAngle = 80f; l.range = range; l.color = c; l.intensity = intensity;
            l.cullingMask = 1 << Layer; l.shadows = LightShadows.None;
        }

        /// Size the texture and frame the camera for the UI rect it is shown in.
        void Frame(float uiAspect, float pixelH)
        {
            aspect = Mathf.Max(0.8f, uiAspect);
            int h = Mathf.Clamp(Mathf.RoundToInt(pixelH), 256, 1400), w = Mathf.Clamp(Mathf.RoundToInt(h * aspect), 256, 2600);
            if (rt == null || rt.width != w || rt.height != h)
            {
                if (rt != null) { cam.targetTexture = null; rt.Release(); Destroy(rt); }
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "PartyRT", antiAliasing = 4 };
                cam.targetTexture = rt;
            }
            // feet at the bottom, room for name plates above the heads
            float span = count == 5 ? 5.4f : 3.6f;
            float dist = span * 0.5f / Mathf.Tan(Mathf.Atan(Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * aspect));
            dist = Mathf.Max(dist, 4.6f);
            float halfV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * dist;
            float lookY = -0.05f + halfV * 0.92f;
            cam.transform.localPosition = new Vector3(0f, lookY + 0.15f, -dist);
            cam.transform.LookAt(transform.position + new Vector3(0f, lookY, 0f));
        }

        public void Sync(List<Boot.PartyMember> members, int slots, float uiAspect, float pixelH)
        {
            if (slots != count) { count = slots; for (int i = 0; i < 5; i++) { if (bodies[i] != null) Destroy(bodies[i]); bodies[i] = null; keys[i] = null; } }
            Frame(uiAspect, pixelH);
            var fill = count == 5 ? Fill5 : Fill3;
            for (int i = 0; i < 5; i++) memberAt[i] = -1;
            for (int k = 0; k < members.Count && k < fill.Length; k++) memberAt[fill[k]] = k;
            for (int i = 0; i < count; i++)
            {
                int mi = memberAt[i];
                string key = mi >= 0 ? members[mi].id + ":" + members[mi].team + ":" + (members[mi].me ? 1 : 0) : null;
                if (key == keys[i]) continue;
                if (bodies[i] != null) Destroy(bodies[i]);
                bodies[i] = null; keys[i] = key;
                if (mi >= 0) bodies[i] = Spawn(i, members[mi]);
            }
        }

        public int MemberAt(int slot) => slot >= 0 && slot < 5 ? memberAt[slot] : -1;

        Vector3 SlotLocal(int i) => count == 5 ? new Vector3(X5[i], 0f, Z5[i]) : new Vector3(X3[i], 0f, Z3[i]);

        /// Where a point `h` metres above slot i lands in the stage texture (0..1).
        public Vector3 SlotViewport(int i, float h) => cam.WorldToViewportPoint(transform.position + SlotLocal(i) + Vector3.up * h);

        GameObject Spawn(int i, Boot.PartyMember m)
        {
            var R = Z47Refs.I;
            int myTeam = Net.Me != null ? Net.Me.team : 0;
            var prefab = m.team == myTeam ? R.swatModel : R.gasModel;
            var g = Instantiate(prefab, transform);
            g.transform.localPosition = SlotLocal(i);
            float yaw = 180f + SlotLocal(i).x * 9f;                         // face the camera, turned a little toward the centre
            g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var a = g.GetComponent<Animator>();
            if (a != null)
            {
                a.runtimeAnimatorController = R.bodyController; a.SetBool("Aim", false);
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                a.Update(UnityEngine.Random.Range(0f, 2f));                 // not all in lockstep
            }
            var gun = Instantiate(R.Weapon(m.team == myTeam ? "m4" : "ak").prefab, g.transform);
            foreach (var c in gun.GetComponentsInChildren<Collider>()) Destroy(c);
            g.AddComponent<PoserGun>().gun = gun.transform;
            foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            return g;
        }

        void OnDestroy() { if (rt != null) { if (cam != null) cam.targetTexture = null; rt.Release(); Destroy(rt); } }
    }
}
