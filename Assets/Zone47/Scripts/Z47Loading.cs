using UnityEngine;
using UnityEngine.UI;

namespace Zone47
{
    /// Match loading screen: map picture, mode / weather, both rosters, a tip and a progress bar.
    /// Stays up at least MinTime so the match never just "snaps" in.
    public class LoadingScreen : MonoBehaviour
    {
        static LoadingScreen I;
        public static bool Visible => I != null;
        const float MinTime = 3.2f;

        float shownAt, target, shown, fade = -1f;
        RectTransform pic, bar; Text pct; CanvasGroup cg;

        static readonly string[][] Tips =
        {
            new[]{"Yatarken tepme ve dağılma azalır, ama yavaş hareket edersin.", "Prone cuts recoil and spread, but you move slowly."},
            new[]{"Şarjörde mermi varsa ateş tuşu şarjör değişimini iptal eder.", "Fire cancels a reload while there is still a round in the mag."},
            new[]{"Sisli havada ve kum fırtınasında botlar da seni uzaktan göremez.", "In fog and sandstorms the bots can't spot you from afar either."},
            new[]{"SOL / SAĞ tuşlarıyla köşeden eğilip bakabilirsin.", "Use the L / R buttons to peek around corners."},
            new[]{"Ayak seslerini dinle: metal, ahşap ve kum farklı ses çıkarır.", "Listen to footsteps: metal, wood and sand all sound different."},
            new[]{"Kafaya isabet iki kattan fazla hasar verir.", "Headshots deal more than double damage."},
            new[]{"Tuşların yerini, boyutunu ve şeffaflığını Ayarlar > Kontroller'den değiştirebilirsin.", "Move, resize and fade every button in Settings > Controls."},
            new[]{"Uzun taramada tepme artar, kısa seriler daha isabetlidir.", "Long sprays kick harder, short bursts stay accurate."},
            new[]{"Takım kartında arkadaşlarının canını ve yeniden doğma süresini görebilirsin.", "The squad card shows your mates' health and respawn timers."},
        };

        public static void Show()
        {
            if (I != null) return;
            var c = UIK.Canvas("Loading", 300);
            DontDestroyOnLoad(c.gameObject);
            I = c.gameObject.AddComponent<LoadingScreen>();
            I.Build(c.transform);
        }

        /// 0..1 from the match setup steps.
        public static void Progress(float p) { if (I != null) I.target = Mathf.Max(I.target, Mathf.Clamp01(p)); }

        /// Called when the match is ready: the screen waits out its minimum time, then fades.
        public static void Done() { if (I != null) { I.target = 1f; if (I.fade < 0f) I.fade = 0f; } }

        void Build(Transform t)
        {
            var R = Z47Refs.I; bool tr = Settings.Turkish;
            shownAt = Time.unscaledTime;
            cg = gameObject.AddComponent<CanvasGroup>();
            UIK.Img(t, "black", Color.black, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            int map = Settings.Map;
            pic = UIK.Rect(t, "pic", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var ri = pic.gameObject.AddComponent<RawImage>(); ri.raycastTarget = false;
            if (R.mapThumbs != null && map < R.mapThumbs.Length && R.mapThumbs[map] != null) ri.texture = R.mapThumbs[map]; else ri.color = new Color(0.12f, 0.12f, 0.11f);
            var arf = pic.gameObject.AddComponent<AspectRatioFitter>(); arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; arf.aspectRatio = 16f / 9f;
            UIK.Img(t, "dim", new Color(0, 0, 0, 0.55f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var low = UIK.Img(t, "low", new Color(0, 0, 0, 0.6f), new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 260)); low.rectTransform.pivot = new Vector2(0.5f, 0);
            UIK.Img(t, "accent", Pal.Accent, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 5)).rectTransform.pivot = new Vector2(0.5f, 1);

            // title block
            string[] mk = { "mapname", "mapname2", "mapname3" };
            UIK.Txt(t, "mode", tr ? "TAKIM ÖLÜM MAÇI" : "TEAM DEATHMATCH", 26, Pal.Accent, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -110), new Vector2(1200, 40), true);
            var mn = UIK.Txt(t, "map", L.S(mk[Mathf.Clamp(map, 0, 2)]), 64, Pal.Text, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -170), new Vector2(1600, 80), true);
            mn.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
            int w = Weather.Pending > 0 ? Weather.Pending : Settings.Weather;
            string wx = L.S("wx" + w);
            UIK.Txt(t, "info", wx + "   ·   5v5   ·   " + (tr ? "HEDEF " : "TARGET ") + MatchManager.KillTarget, 24, Pal.Dim, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -225), new Vector2(1400, 36), true);

            // rosters
            var vs = UIK.Rect(t, "vs", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1100, 300));
            UIK.Txt(vs, "v", "VS", 54, Pal.Accent, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 80), true);
            for (int team = 0; team < 2; team++)
            {
                bool ally = team == 0; Color col = ally ? Pal.Ally : Pal.Enemy;
                float x = ally ? -300 : 300;
                UIK.Txt(vs, "th" + team, L.S(ally ? "allies" : "enemies"), 22, col, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 130), new Vector2(380, 34), true);
                for (int i = 0; i < MatchManager.TeamSize; i++)
                {
                    string n = ally ? (i == 0 ? Settings.Callsign : MatchManager.AllyNames[i - 1]) : MatchManager.EnemyNames[i];
                    var row = UIK.Img(vs, "r", new Color(col.r, col.g, col.b, ally && i == 0 ? 0.28f : 0.12f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 82 - i * 46), new Vector2(380, 40));
                    UIK.Img(row.transform, "s", col, new Vector2(ally ? 0 : 1, 0), new Vector2(ally ? 0 : 1, 1), Vector2.zero, new Vector2(4, 0)).rectTransform.pivot = new Vector2(ally ? 0 : 1, 0.5f);
                    var tx = UIK.Txt(row.transform, "n", n, 22, ally && i == 0 ? Pal.Accent : Pal.Text, ally ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-36, 0), true);
                }
            }

            // tip + progress
            var tip = Tips[Random.Range(0, Tips.Length)];
            UIK.Txt(t, "tipH", tr ? "İPUCU" : "TIP", 20, Pal.Accent, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(110, 150), new Vector2(200, 30), true);
            UIK.Txt(t, "tip", tip[tr ? 0 : 1], 24, Pal.Text, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(110, 115), new Vector2(1500, 36), false);
            var track = UIK.Img(t, "track", new Color(1, 1, 1, 0.12f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 60), new Vector2(-220, 6));
            bar = UIK.Img(track.transform, "fill", Pal.Accent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).rectTransform;
            bar.pivot = new Vector2(0, 0.5f); bar.anchoredPosition = Vector2.zero; bar.localScale = new Vector3(0, 1, 1);
            pct = UIK.Txt(t, "pct", "", 20, Pal.Dim, TextAnchor.MiddleRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-110, 90), new Vector2(500, 30), true);
        }

        void Update()
        {
            float age = Time.unscaledTime - shownAt;
            // never ahead of the real setup, and spread over at least MinTime
            float goal = Mathf.Min(target, Mathf.Clamp01(age / MinTime));
            shown = Mathf.MoveTowards(shown, goal, Time.unscaledDeltaTime * 1.5f);
            bar.localScale = new Vector3(shown, 1, 1);
            pct.text = (Settings.Turkish ? "YÜKLENİYOR  %" : "LOADING  ") + Mathf.RoundToInt(shown * 100f) + (Settings.Turkish ? "" : "%");
            pic.localScale = Vector3.one * (1f + age * 0.012f);             // slow push-in on the map picture

            if (fade >= 0f && age >= MinTime && shown >= 0.995f)
            {
                fade += Time.unscaledDeltaTime / 0.4f;
                cg.alpha = 1f - fade;
                if (fade >= 1f) { I = null; Destroy(gameObject); }
            }
        }
    }
}
