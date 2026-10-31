using System.Collections.Generic;
using UnityEngine;

namespace Zone47
{
    /// Throwables: 0 frag, 1 smoke, 2 flashbang.
    public static class Throwable
    {
        public const int Frag = 0, Smoke = 1, Flash = 2, Count = 3;
        public static readonly int[] Start = { 2, 1, 1 };          // carried per life
        public static float Fuse(int k) => k == Smoke ? 1.8f : k == Flash ? 1.7f : 2.6f;
        public static string Name(int k) => Settings.Turkish ? (k == Smoke ? "SİS" : k == Flash ? "FLAŞ" : "BOMBA") : (k == Smoke ? "SMOKE" : k == Flash ? "FLASH" : "FRAG");
        public static Texture2D Icon(int k) => k == Smoke ? Gfx.SmokeIcon : k == Flash ? Gfx.FlashIcon : Z47Refs.I.icGrenade;
        /// Tint of the grenade body so the three read apart in the hand / in flight.
        public static Color Tint(int k) => k == Smoke ? new Color(0.55f, 0.62f, 0.5f) : k == Flash ? new Color(0.35f, 0.37f, 0.4f) : Color.white;
    }

    /// Thrown grenade (frag / smoke / flash).
    public class Grenade : MonoBehaviour
    {
        public Actor owner;
        public int kind;
        public float fuse = 2.6f;
        float t;
        public const float BlastR = 7.5f;
        public static readonly List<Grenade> Live = new List<Grenade>();
        public float TimeLeft => fuse - t;
        public bool Lethal => kind == Throwable.Frag;
        void OnEnable() { Live.Add(this); }
        void OnDisable() { Live.Remove(this); }
        bool done;

        /// Creates a thrown grenade of the given kind (also used for other phones' throws online).
        public static Grenade Spawn(Actor owner, Vector3 pos, Vector3 vel, int kind = 0)
        {
            if (Z47Refs.I.grenadePrefab == null) return null;
            var go = Instantiate(Z47Refs.I.grenadePrefab, pos, Random.rotation);
            var g = go.GetComponent<Grenade>();
            if (g != null) { g.owner = owner; g.kind = Mathf.Clamp(kind, 0, Throwable.Count - 1); g.fuse = Throwable.Fuse(g.kind); }
            if (kind != Throwable.Frag) Paint(go, Throwable.Tint(kind));
            var rb = go.GetComponent<Rigidbody>(); if (rb != null) { rb.linearVelocity = vel; rb.angularVelocity = Random.insideUnitSphere * 10f; }
            return g;
        }

        public static void Paint(GameObject go, Color c)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                foreach (var m in mats) { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); }
                r.materials = mats;
            }
        }

        void Update()
        {
            t += Time.deltaTime;
            if (!done && t >= fuse) { done = true; if (kind == Throwable.Smoke) PopSmoke(); else if (kind == Throwable.Flash) Bang(); else Explode(); }
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.relativeVelocity.magnitude > 2f) Sfx.Play3D(Z47Refs.I.bounce, transform.position, Mathf.Clamp01(c.relativeVelocity.magnitude / 10f) * 0.8f, 2f, 30f, 0.12f);
        }

        void Explode()
        {
            Vector3 p = transform.position;
            FX.Explosion(p);
            Sfx.Play3D(Z47Refs.I.explosion, p, 1f, 8f, 160f, 0.05f);
            foreach (var a in Actor.All.ToArray())
            {
                if (!a.Alive) continue;
                float d = Vector3.Distance(a.ChestPos, p);
                if (d > BlastR) continue;
                // cover check
                if (Physics.Linecast(p + Vector3.up * 0.3f, a.ChestPos, out var h, ~0, QueryTriggerInteraction.Ignore))
                {
                    var hitA = Actor.FromCollider(h.collider);
                    if (hitA != a) continue;
                }
                float dmg = Mathf.Lerp(150f, 15f, d / BlastR);
                Actor.ExplosionDamage = true; a.Damage(dmg, owner, p, false); Actor.ExplosionDamage = false;
            }
            var pl = PlayerController.Local;
            if (pl != null) pl.Shake(Mathf.Clamp01(1f - Vector3.Distance(pl.EyePos, p) / 30f) * 1.2f);
            MatchManager.I?.Noise(p, owner);
            Destroy(gameObject);
        }

        // ------------------------------------------------------------ smoke
        void PopSmoke()
        {
            Vector3 p = transform.position;
            Sfx.Play3D(Z47Refs.I.bounce, p, 0.6f, 3f, 40f, 0.05f);
            SmokeCloud.Create(p + Vector3.up * 0.2f);
            Destroy(gameObject);
        }

        // ------------------------------------------------------------ flashbang
        public const float FlashR = 20f;

        void Bang()
        {
            Vector3 p = transform.position + Vector3.up * 0.15f;
            // white burst + a very bright, very short light
            var lg = new GameObject("flashbang"); lg.transform.position = p + Vector3.up * 0.5f;
            var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.97f, 0.9f); l.range = 22f; l.intensity = 40f;
            lg.AddComponent<LightFade>().life = 0.25f;
            FX.MuzzleFlash(p, Vector3.up, 2.6f);
            if (Z47Refs.I.explosion != null)
            {
                // sharp "bang": the explosion sample plus a metallic crack
                Sfx.Play3D(Z47Refs.I.explosion, p, 0.9f, 6f, 120f, 0f);
                Sfx.Play3D(Z47Refs.I.casing, p, 1f, 3f, 40f, 0.05f);
            }
            foreach (var a in Actor.All.ToArray())
            {
                if (!a.Alive) continue;
                float dur = BlindTime(p, a);
                if (dur <= 0f) continue;
                if (a == PlayerController.Local) HUD.I?.Flashbang(dur);
                else if (a is BotController b && !b.puppet) b.Blind(dur);
            }
            MatchManager.I?.Noise(p, owner);
            Destroy(gameObject);
        }

        /// How long `a` is blinded: ~4 s when it goes off at your feet, less the further away (nothing past 20 m),
        /// less when you look away (at close range that doesn't save you), nothing through a wall.
        public static float BlindTime(Vector3 p, Actor a)
        {
            Vector3 eye = a.EyePos;
            float d = Vector3.Distance(eye, p);
            if (d > FlashR) return 0f;
            if (Physics.Linecast(p, eye, out var h, ~0, QueryTriggerInteraction.Ignore))
            {
                var who = Actor.FromCollider(h.collider);
                if (who != a && h.collider.attachedRigidbody == null) return 0f;             // behind a wall
            }
            float s = Mathf.Pow(1f - d / FlashR, 1.3f);                                      // 1 at the feet .. 0 at 20 m
            Vector3 look = a is PlayerController pc && pc.cam != null ? pc.cam.transform.forward : a.transform.forward;
            float facing = Vector3.Dot(look, (p - eye).normalized);
            float k = d < 3f ? 1f : facing > 0.3f ? 1f : facing > -0.3f ? 0.6f : 0.25f;       // back turned: much less
            if (SmokeCloud.Blocks(p, eye)) k *= 0.5f;
            float dur = 4f * s * k;
            return dur < 0.3f ? 0f : dur;
        }
    }

    /// Smoke screen: a growing cloud that blocks sight (bots can't see through it, aim assist ignores what's behind it).
    public class SmokeCloud : MonoBehaviour
    {
        public static readonly List<SmokeCloud> All = new List<SmokeCloud>();
        const float Life = 16f, MaxR = 5.5f, Grow = 2.5f, FadeT = 3f;
        float t;

        /// Current radius (0 while it hasn't spread yet / once it's gone).
        public float Radius
        {
            get
            {
                float r = Mathf.Min(MaxR, t * MaxR / Grow);
                if (t > Life - FadeT) r *= Mathf.Clamp01((Life - t) / FadeT);
                return r;
            }
        }

        public static void Create(Vector3 p)
        {
            var go = new GameObject("Smoke");
            go.transform.position = p;
            go.AddComponent<SmokeCloud>();
            var R = Z47Refs.I;
            if (R.smokeMat == null) return;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            bool low = Settings.Quality == 0;
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.duration = Life - 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(3.2f, 5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = new Color(0.78f, 0.78f, 0.76f, 0.9f);
            main.gravityModifier = -0.01f;
            main.maxParticles = low ? 70 : 140;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = low ? 6f : 11f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(low ? 10 : 18)) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 1.2f;
            var vel = ps.limitVelocityOverLifetime; vel.enabled = true; vel.limit = 0.6f; vel.dampen = 0.15f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.7f, 1, 1.5f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = R.smokeMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
        void Update() { t += Time.deltaTime; if (t > Life + 4f) Destroy(gameObject); }

        /// Does the line a→b pass through a smoke cloud?
        public static bool Blocks(Vector3 a, Vector3 b)
        {
            foreach (var s in All)
            {
                if (s == null) continue;
                float r = s.Radius * 0.85f; if (r < 0.5f) continue;
                Vector3 c = s.transform.position + Vector3.up * 1.2f;
                Vector3 ab = b - a; float len2 = ab.sqrMagnitude; if (len2 < 1e-6f) continue;
                float k = Mathf.Clamp01(Vector3.Dot(c - a, ab) / len2);
                if ((a + ab * k - c).sqrMagnitude < r * r) return true;
            }
            return false;
        }
    }
}
