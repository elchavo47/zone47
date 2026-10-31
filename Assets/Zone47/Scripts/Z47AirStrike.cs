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
        static float Speed => Thick ? 20f : 24f;
        // fog / sandstorm: fly much lower and tighter so the drone is still seen through the haze (at 85 m it was ~110 m away = invisible)
        static bool Thick => Weather.Current == Weather.Fog || Weather.Current == Weather.Sandstorm;
        static float Alt => Thick ? 55f : 80f;
        static float OrbitR => Thick ? 42f : 70f;

        /// Live aim of one strike. The host keeps moving it onto where the people are (and tells the other phones),
        /// then fixes the final target and the exact bomb spots right before the bombing run.
        class Aim { public Vector3 target; public bool final; public Vector3[] drops; }
        readonly Dictionary<int, Aim> aims = new Dictionary<int, Aim>();
        static bool Decides => !Net.IsClient;            // offline or host

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
            // start over the biggest group of soldiers (the caller's enemies); the aim keeps following them afterwards
            if (!Crowd(callTeam, null, out var start)) yield break;
            var plan = new Plan
            {
                target = start, inAng = Random.Range(0f, 360f),
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
            var aim = new Aim { target = plan.target };
            aims[plan.seed] = aim;
            float retargetT = 0f;
            // host / offline: every second, move the aim onto the biggest group right now (and tell the other phones)
            void Retarget(bool force)
            {
                if (!Decides) return;
                retargetT -= Time.deltaTime;
                if (retargetT > 0f && !force) return;
                retargetT = 1f;
                if (Crowd(callTeam, aim.target, out var c)) { aim.target = c; Net.SendTrack(plan.seed, c); }
            }

            // 1) siren + warning, the drone is still far away (heard before it's seen)
            bool tr = Settings.Turkish;
            if (friendly) HUD.I?.ShowToastBig(tr ? "DOST İHA YOLDA" : "FRIENDLY DRONE INBOUND", new Color(0.35f, 0.75f, 1f), 3.5f);
            else
            {
                HUD.I?.ShowToastBig(neutral ? (tr ? "İHA YAKLAŞIYOR!" : "DRONE INBOUND!") : (tr ? "DÜŞMAN İHA'SI YOLDA!" : "ENEMY DRONE INBOUND!"), new Color(1f, 0.25f, 0.18f), 4f);
                if (Z47Refs.I.siren != null) Sfx.Play2D(Z47Refs.I.siren, 0.85f);
            }

            // The drone flies like an aircraft: constant speed, a limited turn rate and banking into turns.
            // No more instant 90° pivots: it joins the circle along a tangent and curves into the bombing run.
            float alt = Alt, R = OrbitR, speed = Speed;
            float orbitTurn = speed / R * 1.3f;                 // rad/s: enough to hold (and correct) the circle
            float runTurn = speed / (R * 0.42f);                // tighter (hard banked) turn into the bombing run
            Vector3 inDir = Quaternion.Euler(0, plan.inAng, 0) * Vector3.forward;
            Vector3 pos = target - inDir * 520f; pos.y = target.y + alt;
            Vector3 head = inDir;
            var drone = MakeDrone();
            drones.Add(drone);
            drone.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(head));
            if (Z47Refs.I.droneLoop != null)
            {
                var hum = drone.AddComponent<AudioSource>(); hum.clip = Z47Refs.I.droneLoop; hum.loop = true; hum.spatialBlend = 1f;
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

            int phase = 0;                     // 0 approach, 1 circling, 2 bombing run, 3 leaving
            float dirSign = plan.dirSign, orbitT = 0f, wait = 0f, leaveT = 0f, bank = 0f;
            Vector3 center = target;
            bool dropped = false;
            while (drone != null)
            {
                float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                if (phase <= 1) { Retarget(false); center = Vector3.MoveTowards(center, aim.target, 10f * dt); }   // the circle drifts over to where the people are
                Vector3 toC = (center - pos).Flat(); float dist = toC.magnitude;
                Vector3 desired = head; float maxTurn = orbitTurn;

                if (phase == 0)
                {
                    // head for the tangent of the circle so it joins it smoothly
                    float off = dist > R ? Mathf.Asin(Mathf.Clamp01(R / dist)) * Mathf.Rad2Deg : 90f;
                    desired = Quaternion.Euler(0, off * dirSign, 0) * (dist > 0.01f ? toC / dist : head);
                    if (dist < R * 1.08f)
                    {
                        phase = 1;
                        if (!friendly) HUD.I?.ShowToastBig(Settings.Turkish ? "İHA TEPEDE! SİPER AL" : "DRONE OVERHEAD! TAKE COVER", new Color(1f, 0.25f, 0.18f), 3f);
                    }
                }
                if (phase == 1)
                {
                    // circle: follow the tangent, steer in/out to hold the radius (the long scary part: you don't know where it'll hit)
                    orbitT += dt;
                    Vector3 radial = dist > 0.01f ? -toC / dist : head;
                    Vector3 tangent = Vector3.Cross(Vector3.up, radial); if (Vector3.Dot(tangent, head) < 0f) tangent = -tangent;
                    float err = (dist - R) / R;
                    desired = (tangent - radial * Mathf.Clamp(err * 2.2f, -0.8f, 0.8f)).normalized;
                    if (orbitT >= plan.orbitTime)
                    {
                        // final aim: where the soldiers are NOW. The host picks the exact bomb spots (on the people there) and sends them,
                        // so every phone drops the same bombs in the same places.
                        if (Decides && !aim.final)
                        {
                            Retarget(true);
                            aim.drops = PickDrops(aim.target, callTeam);
                            aim.final = true;
                            Net.SendDrop(plan.seed, aim.target, aim.drops);
                        }
                        if (aim.final) { phase = 2; target = aim.target; }
                        else if ((wait += dt) > 8f) phase = 3;          // never got the host's data: just fly away
                    }
                }
                if (phase == 2)
                {
                    // curve into the run, then hold the heading over the target and release
                    Vector3 toT = (target - pos).Flat();
                    maxTurn = runTurn;
                    desired = toT.magnitude > 22f ? toT.normalized : head;
                    if (!dropped && (toT.magnitude < 16f || (toT.magnitude < 45f && Vector3.Dot(toT, head) < 0f)))
                    {
                        dropped = true;
                        var pts = aim.drops ?? new Vector3[0];
                        for (int i = 0; i < pts.Length; i++)
                            DropBomb(pos + head * i * 5f - Vector3.up * 1.5f, pts[i], (Thick ? 3.6f : 4.2f) + i * 0.45f, caller);
                        phase = 3;
                    }
                }
                if (phase == 3) { desired = head; if ((leaveT += dt) > 22f) break; }

                // steer: turn toward `desired` at a limited rate, bank into the turn, keep a steady speed and height
                Vector3 want = desired.Flat(); if (want.sqrMagnitude < 1e-6f) want = head; want.Normalize();
                Vector3 newHead = Vector3.RotateTowards(head, want, maxTurn * dt, 0f).Flat().normalized;
                float yawRate = Vector3.SignedAngle(head, newHead, Vector3.up) / dt;          // deg/s
                head = newHead;
                pos += head * speed * dt;
                pos.y = Mathf.MoveTowards(pos.y, target.y + alt, 3f * dt);
                bank = Mathf.Lerp(bank, Mathf.Clamp(-yawRate * 0.9f, -32f, 32f), 1f - Mathf.Exp(-dt * 2.5f));
                drone.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(head) * Quaternion.Euler(0, 0, bank));
                TryGlance();
                yield return null;
            }
            aims.Remove(plan.seed);
            drones.Remove(drone); if (drone != null) Destroy(drone);
            if (neutral) striking = false;
        }

        static bool Target(Actor a, int callTeam) => a != null && a.Alive && (callTeam < 0 || a.team != callTeam);

        /// Centre of the biggest group of targets (feet level). `near`: prefer groups close to the current aim so the drone doesn't zig-zag.
        static bool Crowd(int callTeam, Vector3? near, out Vector3 center)
        {
            center = Vector3.zero; float best = float.MinValue; bool found = false;
            foreach (var a in Actor.All)
            {
                if (!Target(a, callTeam)) continue;
                int n = 0; Vector3 sum = Vector3.zero;
                foreach (var o in Actor.All)
                    if (Target(o, callTeam) && (o.transform.position - a.transform.position).Flat().sqrMagnitude < 20f * 20f) { n++; sum += o.transform.position; }
                float score = n * 10f + Random.Range(0f, 4f) - (near.HasValue ? (a.transform.position - near.Value).Flat().magnitude * 0.06f : 0f);
                if (score > best) { best = score; center = sum / Mathf.Max(1, n); found = true; }
            }
            return found;
        }

        /// 3-5 bomb spots: on the soldiers standing around the target (a couple of metres off), the rest close around it.
        static Vector3[] PickDrops(Vector3 target, int callTeam)
        {
            var near = new List<Actor>();
            foreach (var a in Actor.All) if (Target(a, callTeam) && (a.transform.position - target).Flat().sqrMagnitude < 22f * 22f) near.Add(a);
            near.Sort((x, y) => (x.transform.position - target).sqrMagnitude.CompareTo((y.transform.position - target).sqrMagnitude));
            int n = Random.Range(3, 6);
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 p = i < near.Count ? near[i].transform.position + Random.insideUnitSphere.Flat() * 2.5f
                                           : target + Random.insideUnitSphere.Flat() * 9f;
                if (Physics.Raycast(p + Vector3.up * 80f, Vector3.down, out var g, 160f, ~0, QueryTriggerInteraction.Ignore)) p = g.point;
                pts[i] = p;
            }
            return pts;
        }

        /// Online (client): the host moved a strike's aim.
        public void NetTrack(int id, Vector3 p) { if (aims.TryGetValue(id, out var a) && !a.final) a.target = p; }
        /// Online (client): final target and bomb spots of a strike.
        public void NetDrop(int id, Vector3 target, Vector3[] pts) { if (aims.TryGetValue(id, out var a)) { a.target = target; a.drops = pts; a.final = true; } }

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
                if (!Exposed(p, a, d)) continue;                                // really behind cover
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

        /// Is `a` in the open from a blast at `p`? Several lines (from low and high, to feet, chest and head):
        /// one clear line is enough. Loose things (dropped guns, brass, grenades) and other soldiers don't count as cover,
        /// and right next to the bomb there is no hiding at all. (It used to be one line to the chest: any small object
        /// or a bump in the ground in between meant no damage, even at your feet.)
        static readonly Vector3[] blastFrom = { Vector3.up * 0.6f, Vector3.up * 1.8f };
        static bool Exposed(Vector3 p, Actor a, float dist)
        {
            if (dist < 4f) return true;
            Vector3 feet = a.transform.position + Vector3.up * 0.3f;
            foreach (var f in blastFrom)
                foreach (var to in new[] { a.ChestPos, a.EyePos, feet })
                {
                    Vector3 from = p + f;
                    if (!Physics.Linecast(from, to, out var h, ~0, QueryTriggerInteraction.Ignore)) return true;
                    var who = Actor.FromCollider(h.collider);
                    if (who != null || h.collider.attachedRigidbody != null) return true;
                }
            return false;
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
                if (span > 0.01f) wrap.transform.localScale = Vector3.one * (12f / span);      // ~12 m wingspan (was 17)
                if (droneMat == null && R.concreteMat != null)
                {
                    droneMat = new Material(R.concreteMat) { name = "DronePaint" };
                    droneMat.SetTexture("_BaseMap", null); droneMat.SetColor("_BaseColor", new Color(0.62f, 0.63f, 0.62f));
                    droneMat.SetFloat("_Smoothness", 0.35f);
                }
                foreach (var r in rends) { if (droneMat != null) { var mats = r.sharedMaterials; for (int i = 0; i < mats.Length; i++) mats[i] = droneMat; r.sharedMaterials = mats; } r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                DroneBeacon.Add(wrap);
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
            DroneBeacon.Add(root);
            return root;
        }

        void OnDestroy() { aims.Clear(); if (I == this) I = null; foreach (var b in bombs) if (b.body) Destroy(b.body.gameObject); foreach (var d in drones) if (d) Destroy(d); drones.Clear(); }
    }

    /// Red anti-collision light under the drone, blinking. Drawn with the unlit FX shader, which ignores fog,
    /// so it shows through a sandstorm / fog even when the body is lost in the haze.
    public class DroneBeacon : MonoBehaviour
    {
        static Material mat;
        Transform q; Renderer r; float phase;

        public static void Add(GameObject drone)
        {
            var src = Z47Refs.I.sparkMat != null ? Z47Refs.I.sparkMat : Z47Refs.I.flashMat;
            if (src == null) return;
            if (mat == null) { mat = new Material(src) { name = "DroneBeacon" }; mat.SetFloat("_VC", 0f); mat.SetColor("_BaseColor", new Color(1f, 0.12f, 0.08f, 1f)); }
            var b = drone.AddComponent<DroneBeacon>();
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(g.GetComponent<Collider>());
            g.name = "Beacon"; g.transform.SetParent(drone.transform, false);
            b.r = g.GetComponent<Renderer>(); b.r.sharedMaterial = mat; b.r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; b.r.receiveShadows = false;
            b.q = g.transform; b.phase = Random.value;
        }

        void LateUpdate()
        {
            if (q == null) return;
            var cam = Camera.main; if (cam == null) return;
            q.position = transform.position - Vector3.up * 0.8f;
            q.rotation = Quaternion.LookRotation(q.position - cam.transform.position);
            float d = Vector3.Distance(cam.transform.position, q.position);
            float t = Mathf.Repeat(Time.time * 1.1f + phase, 1f);
            bool flash = t < 0.12f || (t > 0.24f && t < 0.33f);                 // double blink, like a real strobe
            float size = (0.9f + d * 0.035f) * (flash ? 1.6f : 0.75f);           // keeps a readable size on screen at any distance
            q.localScale = Vector3.one * size / Mathf.Max(0.0001f, transform.lossyScale.x);
            r.enabled = flash || t > 0.5f;
        }
    }
}
