using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zone47
{
    /// UAV (İHA) air strikes: siren, the drone droning in slowly from far away, circling over the area for a while,
    /// then a bombing pass. Where the bombs land is NOT shown — only the whistle. Always on (not a setting).
    public class AirStrikes : MonoBehaviour
    {
        public static AirStrikes I;
        static Material droneMat, bombMat;
        const float BlastR = 11f;
        const float Alt = 85f, Speed = 24f, OrbitR = 70f;

        class Bomb { public Vector3 at, from; public float hitT, dropT; public Transform body; public Actor caller; }
        readonly List<Bomb> bombs = new List<Bomb>();
        readonly List<GameObject> drones = new List<GameObject>();      // cleaned up with the match (no humming in the lobby)
        bool striking;

        public static void Begin(MatchManager mm)
        {
            if (I != null) return;
            I = mm.gameObject.AddComponent<AirStrikes>();
        }

        void Start() { StartCoroutine(Loop()); }

        IEnumerator Loop()
        {
            if (Net.IsClient) yield break;                 // online: only the host decides when drones come
            yield return new WaitForSeconds(Random.Range(30f, 45f));
            while (MatchManager.I != null && !MatchManager.I.Finished)
            {
                if (MatchManager.I.Running && !striking) yield return StartCoroutine(Strike(null));
                yield return new WaitForSeconds(Random.Range(40f, 70f));
            }
        }

        /// Test: start a strike right now (editor key B).
        public void TriggerNow() { if (Net.IsClient) return; if (!striking && MatchManager.I != null && MatchManager.I.Running) StartCoroutine(Strike(null)); }

        /// Kill-streak reward: `caller`'s side calls a drone on the enemy. Their own team takes no damage, kills count for the caller.
        public void Call(Actor caller) { if (caller != null && MatchManager.I != null && MatchManager.I.Running) StartCoroutine(Strike(caller)); }

        /// Everything random about one strike, decided up front (online the host sends it so every phone flies the same drone).
        public struct Plan { public Vector3 target; public float inAng, orbitTime, dirSign; public int seed; }

        // ------------------------------------------------------------ one strike (~35 s)
        IEnumerator Strike(Actor caller)
        {
            if (Net.IsClient) yield break;
            bool neutral = caller == null;
            int callTeam = neutral ? -1 : caller.team;
            // target a crowd (of the caller's enemies)
            Actor best = null; int bestN = -1;
            foreach (var a in Actor.All)
            {
                if (!a.Alive || (!neutral && a.team == callTeam)) continue;
                int n = 0; foreach (var o in Actor.All) if (o != a && o.Alive && (o.ChestPos - a.ChestPos).sqrMagnitude < 25f * 25f) n++;
                n += Random.Range(0, 3);
                if (n > bestN) { bestN = n; best = a; }
            }
            if (best == null) yield break;
            var plan = new Plan
            {
                target = best.transform.position, inAng = Random.Range(0f, 360f),
                orbitTime = neutral ? Random.Range(9f, 13f) : Random.Range(4f, 6f),
                dirSign = Random.value < 0.5f ? 1f : -1f, seed = Random.Range(1, int.MaxValue),
            };
            Net.SendStrike(caller, plan);
            yield return Run(caller, plan);
        }

        /// Online (client): fly the strike the host planned.
        public void RunPlan(Actor caller, Plan p) { StartCoroutine(Run(caller, p)); }

        IEnumerator Run(Actor caller, Plan plan)
        {
            bool neutral = caller == null;
            if (neutral) striking = true;
            int callTeam = neutral ? -1 : caller.team;
            var pl0 = PlayerController.Local;
            bool friendly = !neutral && pl0 != null && callTeam == pl0.team;
            Vector3 target = plan.target;
            var rng = new System.Random(plan.seed);

            // 1) siren + warning, the drone is still far away (heard before it's seen)
            bool tr = Settings.Turkish;
            if (friendly) HUD.I?.ShowToastBig(tr ? "DOST İHA YOLDA" : "FRIENDLY DRONE INBOUND", new Color(0.35f, 0.75f, 1f), 3.5f);
            else
            {
                HUD.I?.ShowToastBig(neutral ? (tr ? "İHA YAKLAŞIYOR!" : "DRONE INBOUND!") : (tr ? "DÜŞMAN İHA'SI YOLDA!" : "ENEMY DRONE INBOUND!"), new Color(1f, 0.25f, 0.18f), 4f);
                if (Z47Refs.I.siren != null) Sfx.Play2D(Z47Refs.I.siren, 0.85f);
            }

            float inAng = plan.inAng;
            Vector3 inDir = Quaternion.Euler(0, inAng, 0) * Vector3.forward;
            Vector3 far = target - inDir * 520f + Vector3.up * Alt;
            Vector3 entry = target - inDir * OrbitR + Vector3.up * Alt;
            var drone = MakeDrone();
            drones.Add(drone);
            drone.transform.SetPositionAndRotation(far, Quaternion.LookRotation(inDir));
            AudioSource hum = null;
            if (Z47Refs.I.droneLoop != null)
            {
                hum = drone.AddComponent<AudioSource>(); hum.clip = Z47Refs.I.droneLoop; hum.loop = true; hum.spatialBlend = 1f;
                hum.minDistance = 30f; hum.maxDistance = 650f; hum.rolloffMode = AudioRolloffMode.Linear; hum.volume = Settings.Sfx; hum.dopplerLevel = 0.4f; hum.Play();
            }
            bool glanced = false;
            void TryGlance()
            {
                var pl = PlayerController.Local;
                if (glanced || pl == null || !pl.Alive || Vector3.Distance(pl.EyePos, drone.transform.position) > 150f) return;
                glanced = true;
                if (!Physics.Raycast(pl.EyePos, (drone.transform.position - pl.EyePos).normalized, 60f, ~0, QueryTriggerInteraction.Ignore))
                    pl.Glance(drone.transform, 1.6f);        // look up at it for a moment (not under a roof)
            }

            // 2) slow approach
            yield return Fly(drone, far, entry, () => TryGlance());
            // 3) circle the area once-ish, banking (the long scary part: it's right above you and you don't know where it'll hit)
            if (!friendly) HUD.I?.ShowToastBig(Settings.Turkish ? "İHA TEPEDE! SİPER AL" : "DRONE OVERHEAD! TAKE COVER", new Color(1f, 0.25f, 0.18f), 3f);
            float orbitTime = plan.orbitTime, t = 0f, a0 = Mathf.Atan2(entry.x - target.x, entry.z - target.z), dirSign = plan.dirSign;
            float w = Speed / OrbitR * dirSign;
            Vector3 last = drone.transform.position;
            while (t < orbitTime)
            {
                t += Time.deltaTime;
                float ang = a0 + w * t;
                Vector3 p = target + new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang)) * OrbitR + Vector3.up * Alt;
                Vector3 v = p - last; last = p;
                drone.transform.position = p;
                if (v.sqrMagnitude > 0.0001f) drone.transform.rotation = Quaternion.LookRotation(v) * Quaternion.Euler(0, 0, -dirSign * 18f);
                TryGlance();
                yield return null;
            }
            // 4) bombing run straight across the area, bombs released over it (impact spots unmarked)
            Vector3 runDir = (target - drone.transform.position).Flat().normalized;
            Vector3 runStart = drone.transform.position, runEnd = target + runDir * 520f + Vector3.up * Alt;
            bool dropped = false;
            yield return Fly(drone, runStart, runEnd, () =>
            {
                TryGlance();
                if (!dropped && Vector3.Distance(drone.transform.position.Flat(), target.Flat()) < 18f)
                {
                    dropped = true;
                    int n = rng.Next(3, 6);
                    for (int i = 0; i < n; i++)
                    {
                        float ba = (float)rng.NextDouble() * Mathf.PI * 2f, br = Mathf.Sqrt((float)rng.NextDouble()) * 24f;
                        Vector3 p = target + new Vector3(Mathf.Cos(ba), 0, Mathf.Sin(ba)) * br;
                        if (Physics.Raycast(p + Vector3.up * 80f, Vector3.down, out var g, 160f, ~0, QueryTriggerInteraction.Ignore)) p = g.point;
                        DropBomb(drone.transform.position + runDir * i * 5f - Vector3.up * 1.5f, p, 4.2f + i * 0.45f, caller);
                    }
                }
            });
            drones.Remove(drone); Destroy(drone);
            if (neutral) striking = false;
        }

        IEnumerator Fly(GameObject drone, Vector3 a, Vector3 b, System.Action tick)
        {
            float dur = Vector3.Distance(a, b) / Speed, t = 0f;
            Quaternion rot = Quaternion.LookRotation((b - a).normalized);
            Quaternion r0 = drone.transform.rotation;
            while (t < dur && drone != null)
            {
                t += Time.deltaTime;
                drone.transform.position = Vector3.Lerp(a, b, t / dur);
                drone.transform.rotation = Quaternion.Slerp(r0, rot, Mathf.Clamp01(t / 2f));
                tick?.Invoke();
                yield return null;
            }
        }

        void DropBomb(Vector3 from, Vector3 at, float fall, Actor caller)
        {
            var b = new Bomb { at = at, from = from, hitT = Time.time + fall, dropT = Time.time, body = MakeBomb().transform, caller = caller };
            b.body.position = from;
            // whistle without direction (2D): you hear it coming, louder the closer it lands, but not where
            var pl = PlayerController.Local;
            if (Z47Refs.I.bombWhistle != null && pl != null)
            {
                float d = Vector3.Distance(pl.transform.position, at);
                if (d < 90f) Sfx.Play2D(Z47Refs.I.bombWhistle, Mathf.Lerp(0.9f, 0.25f, d / 90f), 0.08f);
            }
            bombs.Add(b);
        }

        void Update()
        {
            for (int i = bombs.Count - 1; i >= 0; i--)
            {
                var b = bombs[i];
                float k = Mathf.Clamp01((Time.time - b.dropT) / (b.hitT - b.dropT));
                Vector3 prev = b.body.position;
                b.body.position = Vector3.Lerp(b.from, b.at, k) + Vector3.up * 0f;
                b.body.position = new Vector3(Mathf.Lerp(b.from.x, b.at.x, k), Mathf.Lerp(b.from.y, b.at.y, k * k), Mathf.Lerp(b.from.z, b.at.z, k));   // accelerating fall, forward drift
                Vector3 v = b.body.position - prev;
                if (v.sqrMagnitude > 0.000001f) b.body.rotation = Quaternion.LookRotation(v.normalized);
                if (Time.time >= b.hitT) { Explode(b.at, b.caller); Destroy(b.body.gameObject); bombs.RemoveAt(i); }
            }
        }

        void Explode(Vector3 p, Actor caller)
        {
            FX.BigExplosion(p);
            Sfx.Play3D(Z47Refs.I.explosion, p, 1f, 25f, 320f, 0.04f);
            Sfx.Play3D(Z47Refs.I.explosion, p, 0.8f, 25f, 320f, 0.15f);       // layered: deeper, longer boom
            Actor.ExplosionDamage = true; Actor.DroneDamage = true;
            Actor.HazardName = caller == null ? (Settings.Turkish ? "İHA" : "DRONE") : null;
            foreach (var a in Actor.All.ToArray())
            {
                if (!a.Alive || a.Protected) continue;
                if (caller != null && a.team == caller.team) continue;        // called-in strike: no friendly fire
                float d = Vector3.Distance(a.ChestPos, p);
                if (d > BlastR) continue;
                if (Physics.Linecast(p + Vector3.up * 0.5f, a.ChestPos, out var h, ~0, QueryTriggerInteraction.Ignore) && Actor.FromCollider(h.collider) != a) continue;   // behind cover
                a.Damage(Mathf.Lerp(240f, 25f, d / BlastR), caller, p, false);
            }
            Actor.ExplosionDamage = false; Actor.DroneDamage = false; Actor.HazardName = null;
            var pl = PlayerController.Local;
            if (pl != null)
            {
                float d = Vector3.Distance(pl.EyePos, p);
                pl.Shake(Mathf.Clamp01(1f - d / 60f) * 2.2f);
                if (pl.Alive && d < 28f) { HUD.I?.Flash(Mathf.Clamp01(1f - d / 28f)); StartCoroutine(Deafen(Mathf.Clamp01(1f - d / 28f))); }
            }
        }

        /// Close blast: muffled hearing for a few seconds (ears ringing).
        IEnumerator Deafen(float amount)
        {
            var l = FindListener(); if (l == null) yield break;
            var lp = l.GetComponent<AudioLowPassFilter>() ?? l.gameObject.AddComponent<AudioLowPassFilter>();
            float dur = 2.5f + amount * 2.5f, t = 0f;
            while (t < dur && lp != null)
            {
                t += Time.deltaTime;
                float k = 1f - t / dur;
                lp.cutoffFrequency = Mathf.Lerp(22000f, 600f, amount * k * k);
                yield return null;
            }
            if (lp != null) lp.cutoffFrequency = 22000f;
        }

        static AudioListener FindListener()
        {
            var pl = PlayerController.Local;
            if (pl != null && pl.cam != null) { var l = pl.cam.GetComponent<AudioListener>(); if (l != null) return l; }
            return Object.FindFirstObjectByType<AudioListener>();
        }

        /// Bomb: a real model if one is added, else an olive-drab Mk82-style body with tail fins.
        GameObject MakeBomb()
        {
            var R = Z47Refs.I;
            if (R.bombPrefab != null)
            {
                // "MK2 Grenade Drone Bomb" by yousef.hameed: nose -> +Z, ~0.9 m long, its own albedo
                var wrap = new GameObject("Bomb");
                var model = Instantiate(R.bombPrefab, wrap.transform);
                Transform nose = Util.FindDeep(model.transform, "PT_nose"), top = Util.FindDeep(model.transform, "PT_top");
                var rends = model.GetComponentsInChildren<Renderer>();
                Bounds bb = rends[0].bounds; foreach (var r in rends) bb.Encapsulate(r.bounds);
                if (nose != null && top != null)
                {
                    Vector3 nd = nose.position - bb.center, ud = top.position - bb.center;
                    Vector3.OrthoNormalize(ref nd, ref ud);
                    model.transform.localRotation = Quaternion.Inverse(Quaternion.LookRotation(nd, ud)) * model.transform.localRotation;
                    bb = rends[0].bounds; foreach (var r in rends) bb.Encapsulate(r.bounds);
                }
                float len = Mathf.Max(bb.size.x, Mathf.Max(bb.size.y, bb.size.z));
                if (len > 0.0001f) wrap.transform.localScale = Vector3.one * (0.9f / len);
                if (bombMat == null && R.concreteMat != null)
                {
                    bombMat = new Material(R.concreteMat) { name = "BombPaint" };
                    bombMat.SetTexture("_BaseMap", R.bombTex); bombMat.SetColor("_BaseColor", Color.white); bombMat.SetFloat("_Smoothness", 0.4f);
                }
                foreach (var r in rends) { if (bombMat != null) { var m = r.sharedMaterials; for (int i = 0; i < m.Length; i++) m[i] = bombMat; r.sharedMaterials = m; } r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                return wrap;
            }
            if (bombMat == null && R.concreteMat != null)
            {
                bombMat = new Material(R.concreteMat) { name = "BombPaint" };
                bombMat.SetTexture("_BaseMap", null); bombMat.SetColor("_BaseColor", new Color(0.27f, 0.3f, 0.2f)); bombMat.SetFloat("_Smoothness", 0.45f);
            }
            var root = new GameObject("Bomb");
            void Part(Vector3 pos, Vector3 scale, Vector3 euler, PrimitiveType t)
            {
                var g = GameObject.CreatePrimitive(t); Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(root.transform, false); g.transform.localPosition = pos; g.transform.localScale = scale; g.transform.localEulerAngles = euler;
                var r = g.GetComponent<Renderer>(); if (bombMat != null) r.sharedMaterial = bombMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Part(Vector3.zero, new Vector3(0.32f, 0.95f, 0.32f), new Vector3(90, 0, 0), PrimitiveType.Capsule);      // body (2.2 m, +Z = nose)
            Part(new Vector3(0, 0, 0.98f), new Vector3(0.2f, 0.2f, 0.42f), Vector3.zero, PrimitiveType.Sphere);       // ogive nose
            Part(new Vector3(0, 0, -0.8f), new Vector3(0.22f, 0.22f, 0.5f), new Vector3(90, 0, 0), PrimitiveType.Cylinder); // tail cone
            for (int i = 0; i < 4; i++)
                Part(Quaternion.Euler(0, 0, 45 + i * 90) * new Vector3(0, 0.2f, -0.95f), new Vector3(0.02f, 0.3f, 0.42f), new Vector3(0, 0, 45 + i * 90), PrimitiveType.Cube);   // fins
            return root;
        }

        /// Drone model, or a dark silhouette until a real model is added.
        GameObject MakeDrone()
        {
            var R = Z47Refs.I;
            if (R.dronePrefab != null)
            {
                // wrapper: nose -> +Z, top -> +Y (from the PT_ markers), ~17 m wingspan, flat grey paint
                var wrap = new GameObject("Drone");
                var model = Instantiate(R.dronePrefab, wrap.transform);
                model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
                Transform nose = Util.FindDeep(model.transform, "PT_nose"), top = Util.FindDeep(model.transform, "PT_top");
                var rends = model.GetComponentsInChildren<Renderer>();
                Bounds b = rends.Length > 0 ? rends[0].bounds : new Bounds();
                foreach (var r in rends) b.Encapsulate(r.bounds);
                if (nose != null && top != null)
                {
                    Vector3 nd = (nose.position - b.center), ud = (top.position - b.center);
                    Vector3.OrthoNormalize(ref nd, ref ud);
                    model.transform.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(nd, ud));
                    b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
                }
                float span = Mathf.Max(b.size.x, b.size.z);
                if (span > 0.01f) wrap.transform.localScale = Vector3.one * (17f / span);
                if (droneMat == null && R.concreteMat != null)
                {
                    droneMat = new Material(R.concreteMat) { name = "DronePaint" };
                    droneMat.SetTexture("_BaseMap", null); droneMat.SetColor("_BaseColor", new Color(0.62f, 0.63f, 0.62f));
                    droneMat.SetFloat("_Smoothness", 0.35f);
                }
                foreach (var r in rends) { if (droneMat != null) { var mats = r.sharedMaterials; for (int i = 0; i < mats.Length; i++) mats[i] = droneMat; r.sharedMaterials = mats; } r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                return wrap;
            }
            var root = new GameObject("Drone");
            void Part(Vector3 pos, Vector3 scale, PrimitiveType t)
            {
                var g = GameObject.CreatePrimitive(t); Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(root.transform, false); g.transform.localPosition = pos; g.transform.localScale = scale;
                if (t == PrimitiveType.Capsule) g.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var r = g.GetComponent<Renderer>(); if (R.concreteMat != null) r.sharedMaterial = R.concreteMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Part(Vector3.zero, new Vector3(0.9f, 4.5f, 0.9f), PrimitiveType.Capsule);        // fuselage
            Part(new Vector3(0, 0.15f, 0.3f), new Vector3(12f, 0.12f, 1.1f), PrimitiveType.Cube); // wings
            Part(new Vector3(0, 0.6f, -3.6f), new Vector3(0.1f, 1.2f, 0.9f), PrimitiveType.Cube); // tail fin
            Part(new Vector3(0, 0.1f, -3.8f), new Vector3(3.2f, 0.1f, 0.7f), PrimitiveType.Cube); // tail plane
            return root;
        }

        void OnDestroy() { if (I == this) I = null; foreach (var b in bombs) if (b.body) Destroy(b.body.gameObject); foreach (var d in drones) if (d) Destroy(d); drones.Clear(); }
    }
}
