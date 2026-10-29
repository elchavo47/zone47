using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Zone47
{
    public class BotController : Actor
    {
        NavMeshAgent agent;
        Animator anim;
        CapsuleCollider col;
        WeaponDef weapon;
        Transform gun, rHand, lHand, spine;
        Renderer[] rends;

        Actor target;
        Vector3 lastKnown; float lastKnownT = -99f;
        float reaction;           // time target has been visible
        float thinkT, strafeT, repathT;
        int mag;
        float nextShot, burstLeft, burstPause;
        bool reloading; float reloadT;
        bool crouch;
        float deadT;
        Vector3 roamGoal; bool hasRoam;
        float stepAcc;
        Vector3 aimDir = Vector3.forward;

        float Skill => Settings.Difficulty == 0 ? 0.55f : Settings.Difficulty == 1 ? 0.8f : 1f;

        public override Vector3 EyePos => transform.position + Vector3.up * (crouch ? 1.05f : 1.6f);
        public override Vector3 ChestPos => transform.position + Vector3.up * (crouch ? 0.85f : 1.25f);
        public override float HeadHeight => transform.position.y + (crouch ? 0.95f : 1.5f);

        public static BotController Create(int team, Vector3 pos, Quaternion rot, string weaponId, string name)
        {
            var R = Z47Refs.I;
            var go = Instantiate(team == 0 ? R.swatModel : R.gasModel, pos, rot);
            go.name = "Bot_" + name;
            var b = go.AddComponent<BotController>();
            b.team = team; b.callsign = name;
            b.Init(R.Weapon(weaponId));
            return b;
        }

        void Init(WeaponDef w)
        {
            weapon = w; mag = w.magSize;
            anim = GetComponent<Animator>();
            anim.runtimeAnimatorController = Z47Refs.I.bodyController;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            rends = GetComponentsInChildren<Renderer>();
            foreach (var r in rends) r.shadowCastingMode = Settings.Quality >= 2 ? ShadowCastingMode.On : ShadowCastingMode.Off;

            col = gameObject.AddComponent<CapsuleCollider>();
            col.height = 1.8f; col.radius = 0.32f; col.center = new Vector3(0, 0.9f, 0);

            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f; agent.height = 1.8f; agent.speed = 4.2f; agent.acceleration = 14f; agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.5f; agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.updateRotation = false;
            if (NavMesh.SamplePosition(transform.position, out var h, 5f, NavMesh.AllAreas)) agent.Warp(h.position);

            rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            lHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            spine = anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Spine);
            SpawnGun();
            thinkT = Random.Range(0f, 0.3f);
        }

        void Update()
        {
            if (MatchManager.I == null) return;
            if (!Alive)
            {
                deadT += Time.deltaTime;
                if (deadT > 3.2f) foreach (var r in rends) r.enabled = false;
                return;
            }
            if (!MatchManager.I.Running) { agent.isStopped = true; Animate(); return; }

            thinkT -= Time.deltaTime;
            if (thinkT <= 0f) { thinkT = 0.2f; Think(); }

            Combat();
            Move();
            Animate();
            if (hp < maxHp && Time.time - lastDamageTime > 6f) hp = Mathf.Min(maxHp, hp + 12f * Time.deltaTime);
        }

        // ------------------------------------------------------------ perception
        bool CanSee(Actor a)
        {
            Vector3 from = EyePos, to = a.ChestPos;
            Vector3 d = to - from; float dist = d.magnitude;
            if (dist > 85f) return false;
            float fov = Vector3.Angle(transform.forward, d);
            if (fov > 75f && dist > 6f && Time.time - lastDamageTime > 1.5f) return false;
            if (Ballistics.Cast(from, d / dist, dist + 0.5f, this, out var hit))
                return Actor.FromCollider(hit.collider) == a || Vector3.Distance(hit.point, to) < 0.6f;
            return true;
        }

        void Think()
        {
            Actor best = null; float bd = float.MaxValue;
            foreach (var a in All)
            {
                if (a.team == team || !a.Alive) continue;
                float d = Vector3.Distance(a.ChestPos, ChestPos);
                if (d < bd && CanSee(a)) { bd = d; best = a; }
            }
            if (best != target) reaction = 0f;
            target = best;
            if (target != null) { lastKnown = target.ChestPos; lastKnownT = Time.time; }
        }

        public void Hear(Actor src)
        {
            if (target != null) return;
            lastKnown = src.ChestPos; lastKnownT = Time.time;
        }

        // ------------------------------------------------------------ combat
        void Combat()
        {
            if (reloading)
            {
                reloadT += Time.deltaTime;
                if (reloadT >= weapon.reloadTime) { reloading = false; mag = weapon.magSize; }
                return;
            }
            if (target == null || !target.Alive)
            {
                if (mag < weapon.magSize * 0.5f) StartReload();
                return;
            }
            reaction += Time.deltaTime;
            Vector3 aimAt = target.ChestPos + Vector3.up * (Random.value < 0.15f * Skill ? 0.4f : 0f);
            aimDir = Vector3.Slerp(aimDir, (aimAt - EyePos).normalized, Time.deltaTime * 8f * Skill);
            float react = Mathf.Lerp(0.9f, 0.35f, Skill) + (weapon.kind == WeaponKind.Sniper ? 0.6f : 0f);
            if (reaction < react) return;
            if (mag <= 0) { StartReload(); return; }

            if (burstPause > 0) { burstPause -= Time.deltaTime; return; }
            if (Time.time < nextShot) return;

            float dist = Vector3.Distance(target.ChestPos, EyePos);
            float spread = Mathf.Lerp(4.2f, 1.4f, Skill) * (weapon.kind == WeaponKind.Sniper ? 0.25f : 1f) * (agent.velocity.magnitude > 1f ? 1.4f : 1f);
            spread *= Mathf.Lerp(0.7f, 1.2f, Mathf.InverseLerp(5f, 60f, dist));
            if (target.isPlayer && Time.time - target.lastDamageTime < 0.25f) spread *= 1.3f;
            Vector3 dir = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0) * aimDir;
            if (Ballistics.Cast(EyePos, dir, dist + 1f, this, out var lof))
            {
                var blocker = Actor.FromCollider(lof.collider);
                if (blocker != null && blocker.team == team) { strafeT = 0f; nextShot = Time.time + 0.3f; return; }
            }
            Vector3 muzzle = gun.TransformPoint(weapon.muzzlePoint);
            Ballistics.Fire(this, EyePos, dir, weapon.range, weapon.damage * Mathf.Lerp(0.6f, 0.85f, Skill), muzzle, true, out _);
            Sfx.Play3D(weapon.shot, muzzle, 1f, 6f, 140f, 0.07f);
            FX.MuzzleFlash(muzzle, gun.forward, weapon.kind == WeaponKind.Sniper ? 0.45f : 0.32f);
            mag--;
            nextShot = Time.time + weapon.fireInterval * (weapon.kind == WeaponKind.Sniper ? 1.6f : 1.05f);
            if (weapon.kind != WeaponKind.Sniper)
            {
                if (burstLeft <= 0) burstLeft = Random.Range(3, 8);
                burstLeft--;
                if (burstLeft <= 0) burstPause = Random.Range(0.25f, 0.7f);
            }
            if (anim != null) anim.SetTrigger("Fire");
        }

        void StartReload()
        {
            if (reloading) return;
            reloading = true; reloadT = 0;
            anim.SetTrigger("Reload");
            Sfx.Play3D(Z47Refs.I.reload, transform.position + Vector3.up, 0.6f, 2f, 25f);
        }

        // ------------------------------------------------------------ movement
        void Move()
        {
            if (!agent.isOnNavMesh) return;
            agent.isStopped = false;
            repathT -= Time.deltaTime;
            var mm = MatchManager.I;

            if (target != null)
            {
                float dist = Vector3.Distance(target.ChestPos, ChestPos);
                agent.speed = 2.4f;
                strafeT -= Time.deltaTime;
                if (strafeT <= 0f)
                {
                    strafeT = Random.Range(0.8f, 2f);
                    crouch = weapon.kind == WeaponKind.Sniper ? Random.value < 0.7f : Random.value < 0.3f;
                    Vector3 side = Vector3.Cross(Vector3.up, (target.ChestPos - ChestPos).normalized) * Random.Range(-3.5f, 3.5f);
                    Vector3 fwd = (target.ChestPos - ChestPos).Flat().normalized * (dist > 30f ? 3f : dist < 10f ? -2.5f : 0f);
                    Vector3 p = transform.position + side + fwd;
                    if (hp < 35f) p = transform.position - (target.ChestPos - ChestPos).Flat().normalized * 6f;
                    if ((hp < 70f || Random.value < 0.35f) && FindCover(target.ChestPos, out var cp)) { p = cp; crouch = true; }
                    if (NavMesh.SamplePosition(p, out var h, 2f, NavMesh.AllAreas)) agent.SetDestination(h.position);
                }
                if (crouch) agent.speed = 0.9f;
                Face(target.ChestPos);
            }
            else
            {
                crouch = false;
                agent.speed = 4.4f;
                if (Time.time - lastKnownT < 12f)
                {
                    if (repathT <= 0f) { repathT = 1f; agent.SetDestination(lastKnown); }
                    if (Vector3.Distance(transform.position, lastKnown) < 2f) lastKnownT = -99f;
                }
                else
                {
                    if (!hasRoam || (agent.remainingDistance < 1.5f && !agent.pathPending) || repathT < -15f)
                    {
                        // drift toward the enemy half of the map
                        Vector3 enemySide = mm.spawnCenter[1 - team];
                        Vector3 best = mm.waypoints[Random.Range(0, mm.waypoints.Count)];
                        for (int i = 0; i < 3; i++)
                        {
                            var c = mm.waypoints[Random.Range(0, mm.waypoints.Count)];
                            if (Vector3.Distance(c, enemySide) < Vector3.Distance(best, enemySide)) best = c;
                        }
                        roamGoal = best; hasRoam = true; repathT = 0f;
                        agent.SetDestination(roamGoal);
                    }
                }
                Vector3 v = agent.velocity.Flat();
                if (v.sqrMagnitude > 0.2f) Face(transform.position + v);
                else if (Time.time - lastKnownT < 12f) Face(lastKnown);
                aimDir = Vector3.Slerp(aimDir, transform.forward, Time.deltaTime * 4f);
            }

            float sp = agent.velocity.magnitude;
            if (sp > 0.5f)
            {
                stepAcc += sp * Time.deltaTime;
                if (stepAcc > 2.2f)
                {
                    stepAcc = 0;
                    var st = Z47Refs.I.steps;
                    Sfx.Play3D(st[Random.Range(0, st.Length)], transform.position, 0.55f, 2f, 22f, 0.1f);
                }
            }
        }

        /// A spot behind a nearby crate/barrier, on the far side from the enemy.
        bool FindCover(Vector3 enemy, out Vector3 spot)
        {
            spot = default; float best = 14f; bool found = false;
            foreach (var c in MatchManager.I.covers)
            {
                float d = Vector3.Distance(c.Flat(), transform.position.Flat());
                if (d > best) continue;
                Vector3 away = (c - enemy).Flat().normalized;
                Vector3 p = c + away * 1.4f;
                if (NavMesh.SamplePosition(p, out var h, 1.2f, NavMesh.AllAreas)) { best = d; spot = h.position; found = true; }
            }
            return found;
        }

        void Face(Vector3 p)
        {
            Vector3 d = (p - transform.position).Flat();
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 420f * Time.deltaTime);
        }

        void Animate()
        {
            Vector3 lv = transform.InverseTransformDirection(agent != null ? agent.velocity : Vector3.zero);
            float runN = 4.5f;
            anim.SetFloat("MoveX", lv.x / runN, 0.12f, Time.deltaTime);
            anim.SetFloat("MoveZ", lv.z / runN, 0.12f, Time.deltaTime);
            anim.SetBool("Crouch", crouch);
            anim.SetBool("Aim", target != null);
            anim.SetFloat("Speed", Mathf.Clamp01(lv.magnitude / 1.5f));
        }

        void LateUpdate()
        {
            if (!Alive || rHand == null || lHand == null || gun == null) return;
            // aim the upper body at the target (pitch)
            if (spine != null && target != null)
            {
                Vector3 toT = aimDir;
                float pitchDeg = -Mathf.Asin(Mathf.Clamp(toT.y, -1f, 1f)) * Mathf.Rad2Deg;
                spine.rotation = Quaternion.AngleAxis(pitchDeg * 0.7f, transform.right) * spine.rotation;
            }
            Vector3 fwd = (lHand.position - rHand.position);
            if (fwd.sqrMagnitude < 0.0001f) fwd = transform.forward;
            Vector3 up = Vector3.Cross(fwd, transform.right);
            if (Vector3.Dot(up, Vector3.up) < 0) up = -up;
            gun.rotation = Quaternion.LookRotation(fwd.normalized, up);
            gun.position = rHand.position + gun.rotation * new Vector3(0.0f, 0.04f, 0.03f);
        }

        void SpawnGun()
        {
            var g = Instantiate(weapon.prefab, transform);
            foreach (var c in g.GetComponentsInChildren<Collider>()) Destroy(c);
            Util.SetShadows(g, Settings.Quality >= 2 ? ShadowCastingMode.On : ShadowCastingMode.Off);
            gun = g.transform;
        }

        /// The rifle falls out of the dead soldier's hands and lies on the ground for a while.
        void DropGun()
        {
            if (gun == null) return;
            var g = gun.gameObject; gun = null;
            g.transform.SetParent(null, true);
            var bc = g.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.02f, weapon.muzzlePoint.z * 0.4f);
            bc.size = new Vector3(0.06f, 0.18f, Mathf.Max(0.2f, weapon.muzzlePoint.z * 1.3f));
            var rb = g.AddComponent<Rigidbody>();
            rb.mass = 3.5f;
            rb.linearVelocity = transform.forward * 0.6f + Vector3.up * 0.8f;
            rb.angularVelocity = Random.insideUnitSphere * 3f;
            Destroy(g, 7f);
        }

        protected override void OnHurt(Actor from, Vector3 fromPos)
        {
            if (from != null && target == null) { lastKnown = from.ChestPos; lastKnownT = Time.time; }
            anim.SetTrigger("Hit");
        }

        protected override void OnDie(Actor killer)
        {
            deadT = 0f;
            anim.SetTrigger("Die");
            anim.SetBool("Dead", true);
            col.enabled = false;
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
            target = null;
            DropGun();
            Sfx.Play3D(Z47Refs.I.bodyFall, transform.position, 0.8f, 3f, 30f);
        }

        public override void Respawn(Vector3 pos, Quaternion rot)
        {
            agent.enabled = true;
            agent.Warp(pos);
            transform.rotation = rot;
            col.enabled = true;
            hp = maxHp; mag = weapon.magSize; reloading = false; target = null; lastKnownT = -99f; hasRoam = false;
            foreach (var r in rends) r.enabled = true;
            if (gun == null) SpawnGun();
            anim.SetBool("Dead", false);
            anim.Rebind();
        }
    }
}
