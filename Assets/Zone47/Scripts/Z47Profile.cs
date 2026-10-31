using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zone47
{
    /// Player profile (PUBG style): flag, avatar frame, title, lifetime stats, achievements. Stored on the device.
    public static class Profile
    {
        // ---------------------------------------------------------------- identity
        public static string Flag { get => PlayerPrefs.GetString("z_flag", "tr"); set { PlayerPrefs.SetString("z_flag", value); PlayerPrefs.Save(); } }
        public static int Frame { get => PlayerPrefs.GetInt("z_frame", 0); set { PlayerPrefs.SetInt("z_frame", value); PlayerPrefs.Save(); } }
        public static int Title { get => PlayerPrefs.GetInt("z_titlesel", -1); set { PlayerPrefs.SetInt("z_titlesel", value); PlayerPrefs.Save(); } }   // -1 = automatic (rank)

        // ---------------------------------------------------------------- lifetime stats
        static int GetI(string k) => PlayerPrefs.GetInt("z_st_" + k, 0);
        static void AddI(string k, int v) { if (v != 0) PlayerPrefs.SetInt("z_st_" + k, GetI(k) + v); }
        public static int Matches => GetI("matches");
        public static int Wins => GetI("wins");
        public static int Kills => GetI("kills");
        public static int Deaths => GetI("deaths");
        public static int Headshots => GetI("hs");
        public static int Mvps => GetI("mvp");
        public static int ShotsFired => GetI("shots");
        public static int ShotsHit => GetI("hits");
        public static int Damage => GetI("dmg");
        public static int BestStreak => GetI("streak");
        public static int GrenadeKills => GetI("k_gren");
        public static int DroneKills => GetI("k_drone");
        public static int ShotgunKills => GetI("k_shotgun");
        public static int AwpKills => GetI("k_awp");
        public static float Longest => PlayerPrefs.GetFloat("z_st_long", 0f);
        public static float KD => Deaths > 0 ? Kills / (float)Deaths : Kills;
        public static float Accuracy => ShotsFired > 0 ? ShotsHit / (float)ShotsFired : 0f;
        public static float WinRate => Matches > 0 ? Wins / (float)Matches : 0f;

        // ---------------------------------------------------------------- achievements
        public class Ach { public string id, tr, en, dtr, den; public int target; public System.Func<float> value; public int frame = -1; public bool title; }
        public static readonly Ach[] Achievements =
        {
            new Ach { id = "firstblood", tr = "İlk Kan", en = "First Blood", dtr = "İlk düşmanını öldür", den = "Get your first kill", target = 1, value = () => Kills, title = true },
            new Ach { id = "hunter", tr = "Avcı", en = "Hunter", dtr = "100 düşman öldür", den = "Kill 100 enemies", target = 100, value = () => Kills, title = true },
            new Ach { id = "machine", tr = "Ölüm Makinesi", en = "Killing Machine", dtr = "1000 düşman öldür", den = "Kill 1000 enemies", target = 1000, value = () => Kills, title = true },
            new Ach { id = "headhunter", tr = "Kafa Avcısı", en = "Headhunter", dtr = "50 kafadan öldürme", den = "50 headshot kills", target = 50, value = () => Headshots, frame = 6, title = true },
            new Ach { id = "eagleeye", tr = "Keskin Göz", en = "Eagle Eye", dtr = "100 metreden öldür", den = "Kill from 100 m", target = 100, value = () => Longest, title = true },
            new Ach { id = "streak5", tr = "Seri Katil", en = "Serial Killer", dtr = "Ölmeden 5 öldürme", den = "5 kills without dying", target = 5, value = () => BestStreak, title = true },
            new Ach { id = "streak10", tr = "Durdurulamaz", en = "Unstoppable", dtr = "Ölmeden 10 öldürme", den = "10 kills without dying", target = 10, value = () => BestStreak, frame = 7, title = true },
            new Ach { id = "firstwin", tr = "İlk Zafer", en = "First Victory", dtr = "İlk maçını kazan", den = "Win your first match", target = 1, value = () => Wins },
            new Ach { id = "commander", tr = "Komutan", en = "Commander", dtr = "25 maç kazan", den = "Win 25 matches", target = 25, value = () => Wins, frame = 8, title = true },
            new Ach { id = "veteran", tr = "Veteran", en = "Veteran", dtr = "50 maç oyna", den = "Play 50 matches", target = 50, value = () => Matches, title = true },
            new Ach { id = "mvp", tr = "Maçın Yıldızı", en = "Star Player", dtr = "10 kez MVP ol", den = "Be MVP 10 times", target = 10, value = () => Mvps, title = true },
            new Ach { id = "grenadier", tr = "Bombacı", en = "Grenadier", dtr = "El bombasıyla 25 öldürme", den = "25 grenade kills", target = 25, value = () => GrenadeKills, title = true },
            new Ach { id = "skylord", tr = "Göklerin Efendisi", en = "Lord of the Sky", dtr = "İHA ile 10 öldürme", den = "10 drone kills", target = 10, value = () => DroneKills, title = true },
            new Ach { id = "breacher", tr = "Kapı Kıran", en = "Breacher", dtr = "Pompalıyla 50 öldürme", den = "50 shotgun kills", target = 50, value = () => ShotgunKills, title = true },
            new Ach { id = "ghost", tr = "Gölge Nişancı", en = "Ghost Sniper", dtr = "AWP ile 100 öldürme", den = "100 AWP kills", target = 100, value = () => AwpKills, title = true },
            new Ach { id = "marksman", tr = "Usta Atıcı", en = "Marksman", dtr = "5000 isabet", den = "5000 hits", target = 5000, value = () => ShotsHit },
        };
        public static bool Unlocked(Ach a) => a.value() >= a.target;
        public static int UnlockedCount { get { int n = 0; foreach (var a in Achievements) if (Unlocked(a)) n++; return n; } }
        public static string AName(Ach a) => Settings.Turkish ? a.tr : a.en;
        public static string ADesc(Ach a) => Settings.Turkish ? a.dtr : a.den;

        // ---------------------------------------------------------------- frames & titles
        public static readonly string[] FrameNamesTR = { "Standart", "Bronz", "Gümüş", "Altın", "Elmas", "Efsane", "Kafa Avcısı", "Durdurulamaz", "Komutan" };
        public static readonly string[] FrameNamesEN = { "Standard", "Bronze", "Silver", "Gold", "Diamond", "Legend", "Headhunter", "Unstoppable", "Commander" };
        static readonly int[] FrameLevel = { 1, 5, 10, 20, 35, 50, 0, 0, 0 };
        public static int FrameCount => FrameNamesTR.Length;
        public static string FrameName(int i) => (Settings.Turkish ? FrameNamesTR : FrameNamesEN)[Mathf.Clamp(i, 0, FrameCount - 1)];
        public static bool FrameUnlocked(int i)
        {
            if (i < 0 || i >= FrameCount) return false;
            if (FrameLevel[i] > 0) return Progress.Level >= FrameLevel[i];
            foreach (var a in Achievements) if (a.frame == i) return Unlocked(a);
            return false;
        }
        public static string FrameHow(int i)
        {
            bool tr = Settings.Turkish;
            if (FrameLevel[i] > 0) return (tr ? "Seviye " : "Level ") + FrameLevel[i];
            foreach (var a in Achievements) if (a.frame == i) return (tr ? "Başarım: " : "Achievement: ") + AName(a);
            return "";
        }
        public static int SafeFrame => FrameUnlocked(Frame) ? Frame : 0;

        // ranks by level, then achievement titles
        static readonly int[] RankLevel = { 1, 3, 6, 10, 15, 20, 28, 36, 45, 55, 70 };
        static readonly string[] RankTR = { "Er", "Onbaşı", "Çavuş", "Başçavuş", "Teğmen", "Üsteğmen", "Yüzbaşı", "Binbaşı", "Yarbay", "Albay", "General" };
        static readonly string[] RankEN = { "Private", "Corporal", "Sergeant", "Master Sergeant", "2nd Lieutenant", "Lieutenant", "Captain", "Major", "Lt. Colonel", "Colonel", "General" };
        public static string RankFor(int level) { int r = 0; for (int i = 0; i < RankLevel.Length; i++) if (level >= RankLevel[i]) r = i; return (Settings.Turkish ? RankTR : RankEN)[r]; }

        public class TitleOpt { public string name, how; public bool unlocked; }
        public static List<TitleOpt> Titles()
        {
            var l = new List<TitleOpt>(); int lv = Progress.Level; bool tr = Settings.Turkish;
            for (int i = 0; i < RankLevel.Length; i++) l.Add(new TitleOpt { name = (tr ? RankTR : RankEN)[i], how = (tr ? "Seviye " : "Level ") + RankLevel[i], unlocked = lv >= RankLevel[i] });
            foreach (var a in Achievements) if (a.title) l.Add(new TitleOpt { name = AName(a), how = (tr ? "Başarım: " : "Achievement: ") + ADesc(a), unlocked = Unlocked(a) });
            return l;
        }
        /// What's shown under the name: the chosen title if still unlocked, else the rank.
        public static string CurrentTitle
        {
            get
            {
                var t = Titles(); int i = Title;
                return i >= 0 && i < t.Count && t[i].unlocked ? t[i].name : RankFor(Progress.Level);
            }
        }

        // ---------------------------------------------------------------- end of match
        /// Adds the match to the lifetime stats; returns achievements that got unlocked by it.
        public static List<Ach> RecordMatch(Actor me, bool win, bool mvp)
        {
            var before = new HashSet<string>(); foreach (var a in Achievements) if (Unlocked(a)) before.Add(a.id);
            AddI("matches", 1); if (win) AddI("wins", 1); if (mvp) AddI("mvp", 1);
            if (me != null)
            {
                AddI("kills", me.kills); AddI("deaths", me.deaths); AddI("hs", me.headshotKills);
                AddI("shots", me.shotsFired); AddI("hits", me.shotsHit); AddI("dmg", Mathf.RoundToInt(me.damageDealt));
                if (me.bestStreak > BestStreak) PlayerPrefs.SetInt("z_st_streak", me.bestStreak);
                if (me.longestKill > Longest) PlayerPrefs.SetFloat("z_st_long", me.longestKill);
                foreach (var kv in me.killsBy) AddI("k_" + kv.Key, kv.Value);
            }
            PlayerPrefs.Save();
            var news = new List<Ach>(); foreach (var a in Achievements) if (Unlocked(a) && !before.Contains(a.id)) news.Add(a);
            return news;
        }

        // ---------------------------------------------------------------- avatar widget
        /// Hexagon avatar: dark hex, initial letter, chosen frame, flag badge.
        public static RectTransform Avatar(Transform parent, string name, Vector2 anchor, Vector2 pos, float size, string callsign, string flag, int frame)
        {
            var R = Z47Refs.I;
            var root = UIK.Rect(parent, name, anchor, anchor, pos, new Vector2(size, size));
            var hex = root.gameObject.AddComponent<RawImage>(); hex.raycastTarget = false;
            hex.texture = R.avatarHex; hex.color = new Color(0.1f, 0.11f, 0.11f, 0.95f);
            string ini = string.IsNullOrEmpty(callsign) ? "?" : callsign.Trim().Substring(0, 1).ToUpper();
            UIK.Txt(root, "ini", ini, Mathf.RoundToInt(size * 0.42f), Pal.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0, size * 0.02f), Vector2.zero, true);
            if (R.frames != null && R.frames.Length > 0)
            {
                var fr = UIK.Rect(root, "frame", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 1.18f, size * 1.18f)).gameObject.AddComponent<RawImage>();
                fr.texture = R.frames[Mathf.Clamp(frame, 0, R.frames.Length - 1)]; fr.raycastTarget = false;
            }
            Countries.Make(root, "flag", flag, new Vector2(1, 0), new Vector2(-size * 0.08f, size * 0.12f), size * 0.26f);
            return root;
        }
    }
}
