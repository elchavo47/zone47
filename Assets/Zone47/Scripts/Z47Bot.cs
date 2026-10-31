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
        Transform rShoulder;
        int ikFrame = -1;
        Vector3 aimErr;                 // first shots at a new target are off, then tighten (human-like)
        bool atCover; float peekT; bool peekHidden;
        float stuckT, holdT;
        float strafeSide = 1f;
        Ragdoll ragdoll;
        int nades = 1; float nextNade;
        static readonly float[] teamNadeAt = { -99f, -99f };        // last grenade per team: one side can't spam several at once
        int NadeCount => Settings.Difficulty >= 2 ? 2 : 1;

        // online puppet: this soldier is simulated on another phone (a remote player, or the host's bots); we only show it
        public bool puppet; public ulong owner; bool netHuman;
        public bool IsHuman => netHuman;
        Vector3 netPos, netVel, pupVel; float netYaw, netPitch, netT = -1f; int netFlags = 1; float lastRemoteFx = -9f;
        bool prone;
        Vector3 Vel => puppet ? pupVel : agent != null && agent.enabled ? agent.velocity : Vector3.zero;
        bool Ready => puppet ? (netFlags & 8) != 0 || Time.time - lastShotTime < 2f : target != null || Time.time - lastKnownT < 12f;

        public override string WeaponId => weapon != null ? weapon.id : null;
        float Skill => Settings.Difficulty == 0 ? 0.55f : Settings.Difficulty == 1 ? 0.8f : 1f;

        public override Vector3 EyePos => transform.position + Vector3.up * (prone ? 0.4f : crouch ? 1.05f : 1.6f);
        public override Vector3 ChestPos => transform.position + Vector3.up * (prone ? 0.3f : crouch ? 0.85f : 1.25f);
        public override float HeadHeight => transform.position.y + (prone ? 0.32f : crouch ? 0.95f : 1.5f);

        public static BotController Create(int team, Vector3 pos, Quaternion rot, string weaponId, string name)
        {
            var R = Z47Refs.I;
            var go = Instantiate(team == 0 ? R.swatModel : R.gasModel, pos, rot);
            go.name = "Bot_" + name;
            var b = go.AddComponent<BotController>();
            b.team = team; b.callsign = name; b.flag = Countries.RandomBot(team == 0);
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
            foreach (var r in rends) r.shadowCastingMode = Settings.Quality >= 1 ? ShadowCastingMode.On : ShadowCastingMode.Off;

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
            rShoulder = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            SpawnGun();
            // ragdoll removed (bodies flopped around): deaths use the Dying animation again
            thinkT = Random.Range(0f, 0.3f);
            nades = NadeCount; nextNade = Time.time + Random.Range(20f, 35f);     // no grenade in the first moments of a life
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
            if (puppet) { PuppetUpdate(); return; }
            if (!MatchManager.I.Running) { agent.isStopped = true; Animate(); return; }

            thinkT -= Time.deltaTime;
            if (thinkT <= 0f) { thinkT = 0.2f; Think(); }

            Combat();
            Move();
            Animate();
            if (hp < maxHp && Time.time - lastDamageTime > 6f) hp = Mathf.Min(maxHp, hp + 12f * Time.deltaTime);
        }

        // ------------------------------------------------------------ perception
        float blindUntil = -99f;
        public bool Blinded => Time.time < blindUntil;
        /// Flashbang: sees nothing for a while (and loses the target).
        public void Blind(float dur)
        {
            blindUntil = Mathf.Max(blindUntil, Time.time + dur);
            target = null; reaction = 0f;
            if (anim != null) anim.SetTrigger("Hit");
        }

        bool CanSee(Actor a)
        {
            if (Blinded) return false;
            Vector3 from = EyePos, to = a.ChestPos;
            if (SmokeCloud.Blocks(from, to)) return false;
            Vector3 d = to - from; float dist = d.magnitude;
            if (dist > Weather.SightRange) return false;
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
            if (best != target)
            {
                reaction = 0f;
                if (best != null)
                {
                    float d = Vector3.Distance(best.ChestPos, ChestPos);
                    aimErr = Random.onUnitSphere * Mathf.Lerp(0.5f, 1.4f, Mathf.InverseLerp(8f, 60f, d)) * Mathf.Lerp(1.3f, 0.7f, Skill);
                    // call it out: nearby teammates turn toward the contact
                    foreach (var a in All)
                        if (a != this && a.team == team && a.Alive && a is BotController mate && Vector3.Distance(a.ChestPos, ChestPos) < 35f) mate.Hear(best, 2f);
                }
            }
            target = best;
            if (target != null) { lastKnown = target.ChestPos; lastKnownT = Time.time; }
            NadeLogic();
        }

        // ------------------------------------------------------------ grenades
        void NadeLogic()
        {
            // get away from any live grenade (theirs or ours)
            foreach (var g in Grenade.Live)
                if (g != null && g.Lethal && (g.transform.position - transform.position).sqrMagnitude < (Grenade.BlastR + 1.5f) * (Grenade.BlastR + 1.5f) && Time.time > fleeUntil)
                    Flee(g.transform.position, Grenade.BlastR + 1.5f, Mathf.Max(0.5f, g.TimeLeft));

            if (nades <= 0 || Time.time < nextNade || reloading || Z47Refs.I.grenadePrefab == null || Time.time < fleeUntil) return;
            if (Time.time - teamNadeAt[team & 1] < 12f) return;           // a teammate just threw one
            Vector3 at; bool want;
            // badly hurt and under fire: smoke between us and them, then fall back behind it
            if (target != null && hp < 45f && Time.time - lastDamageTime < 2f && Random.value < 0.05f * Skill)
            {
                float dd = Vector3.Distance(target.transform.position, transform.position);
                if (dd > 10f && dd < 40f) { ThrowNade(Vector3.Lerp(transform.position, target.transform.position, 0.35f), Throwable.Smoke); return; }
            }
            if (target != null)
            {
                // someone camping behind cover at medium range
                float d = Vector3.Distance(target.transform.position, transform.position);
                want = d > 9f && d < 26f && Random.value < (atCover ? 0.012f : 0.006f) * Skill;     // checked 5x a second
                at = target.transform.position;
            }
            else
            {
                // just lost sight of them: flush them out of where they hid
                float since = Time.time - lastKnownT, d = Vector3.Distance(lastKnown, transform.position);
                want = since > 0.6f && since < 4f && d > 8f && d < 24f && Random.value < 0.03f * Skill;
                at = lastKnown;
            }
            if (!want) return;
            foreach (var a in All) if (a != this && a.team == team && a.Alive && (a.transform.position - at).sqrMagnitude < 7f * 7f) return;   // teammates there
            // up close: sometimes a flashbang instead of a frag
            int kind = Vector3.Distance(at, transform.position) < 18f && Random.value < 0.3f ? Throwable.Flash : Throwable.Frag;
            ThrowNade(at, kind);
        }

        void ThrowNade(Vector3 at, int kind = 0)
        {
            nades--; nextNade = Time.time + Random.Range(35f, 55f); teamNadeAt[team & 1] = Time.time;
            Face(at);
            Vector3 from = EyePos + transform.forward * 0.55f + Vector3.up * 0.1f;
            at += Random.insideUnitSphere.Flat() * Mathf.Lerp(3f, 1f, Skill);
            float T = Mathf.Clamp(Vector3.Distance(from, at) / 13f, 0.7f, 1.6f);
            Vector3 v = (at - from - 0.5f * Physics.gravity * T * T) / T;          // lands at `at` after T seconds
            Grenade.Spawn(this, from, v, kind);
            Net.SendNade(this, from, v, kind);
            if (Z47Refs.I.pin != null) Sfx.Play3D(Z47Refs.I.pin, transform.position + Vector3.up, 0.7f, 2f, 22f);
        }

        /// Something was heard (gunshot, footsteps, teammate callout): go check it out if not already fighting.
        public void Hear(Actor src, float error = 4f)
        {
            if (target != null || src == null) return;
            lastKnown = src.ChestPos + Random.insideUnitSphere.Flat() * error; lastKnownT = Time.time; holdT = 0f;
        }

        float fleeUntil;
        /// Get out of a blast radius (air strike marker).
        public void Flee(Vector3 danger, float radius, float seconds)
        {
            if (!Alive || agent == null || !agent.isOnNavMesh) return;
            Vector3 away = (transform.position - danger).Flat();
            if (away.sqrMagnitude < 0.1f) away = Random.insideUnitSphere.Flat();
            Vector3 p = danger + away.normalized * (radius + 2f);
            for (int i = 0; i < 4; i++)
            {
                if (NavMesh.SamplePosition(p, out var h, 4f, NavMesh.AllAreas)) { agent.SetDestination(h.position); fleeUntil = Time.time + seconds + 0.5f; return; }
                p = danger + Quaternion.Euler(0, 45f * (i + 1), 0) * away.normalized * (radius + 2f);
            }
        }

        /// Gunshots carry far; enemy bots in range react to them.
        public static void Noise(Actor src, float radius)
        {
            if (src == null) return;
            foreach (var a in All)
                if (a.team != src.team && a.Alive && a is BotController b && (a.ChestPos - src.ChestPos).sqrMagnitude < radius * radius) b.Hear(src, radius > 30f ? 6f : 3f);
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
            if (Blinded) return;                                       // flashed: no shooting
            reaction += Time.deltaTime;
            aimErr = Vector3.MoveTowards(aimErr, Vector3.zero, Time.deltaTime * Mathf.Lerp(0.5f, 1.1f, Skill));
            Vector3 aimAt = target.ChestPos + aimErr + Vector3.up * (Random.value < 0.15f * Skill ? 0.4f : 0f);
            aimDir = Vector3.Slerp(aimDir, (aimAt - EyePos).normalized, Time.deltaTime * 8f * Skill);
            float react = Mathf.Lerp(0.9f, 0.35f, Skill) + (weapon.kind == WeaponKind.Sniper ? 0.6f : 0f);
            if (reaction < react) return;
            if (mag <= 0) { StartReload(); return; }
            if (atCover && peekHidden) return;               // ducked behind cover: no shooting

            if (burstPause > 0) { burstPause -= Time.deltaTime; return; }
            if (Time.time < nextShot) return;

            float dist = Vector3.Distance(target.ChestPos, EyePos);
            float spread = MatchManager.HandicapSpread(team) * Mathf.Lerp(3.6f, 1.2f, Skill) * (weapon.kind == WeaponKind.Sniper ? 0.25f : 1f) * (agent.velocity.magnitude > 1f ? 1.4f : 1f);
            spread *= Mathf.Lerp(0.7f, 1.2f, Mathf.InverseLerp(5f, 60f, dist));
            if (target.isPlayer && Time.time - target.lastDamageTime < 0.25f) spread *= 1.3f;
            Vector3 dir = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0) * aimDir;
            if (Ballistics.Cast(EyePos, dir, dist + 1f, this, out var lof))
            {
                var blocker = Actor.FromCollider(lof.collider);
                if (blocker != null && blocker.team == team) { strafeT = 0f; nextShot = Time.time + 0.3f; return; }
            }
            Vector3 muzzle = gun.TransformPoint(weapon.muzzlePoint);
            Ballistics.Fire(this, EyePos, dir, weapon.range, weapon.damage * Mathf.Lerp(0.6f, 0.85f, Skill) * MatchManager.HandicapDamage(team), muzzle, true, out _);
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
            Noise(this, 55f);
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

            if (Time.time < fleeUntil)
            {
                // run from a marked bomb spot, then carry on
                agent.speed = 5.6f; crouch = false; atCover = false;
                Vector3 v0 = agent.velocity.Flat();
                if (v0.sqrMagnitude > 0.2f) Face(transform.position + v0);
                goto Steps;
            }

            // unstick: wants to move, isn't moving
            if (agent.hasPath && !agent.pathPending && agent.remainingDistance > 1f && agent.velocity.sqrMagnitude < 0.09f) stuckT += Time.deltaTime; else stuckT = 0f;
            if (stuckT > 1.5f) { stuckT = 0f; hasRoam = false; strafeT = 0f; repathT = 0f; atCover = false; agent.ResetPath(); }

            if (target != null)
            {
                float dist = Vector3.Distance(target.ChestPos, ChestPos);
                Vector3 toT = (target.ChestPos - ChestPos).Flat().normalized;
                agent.speed = 2.4f;
                strafeT -= Time.deltaTime;
                holdT = 0f;

                if (dist < 11f && !atCover)
                {
                    // close range: side-to-side strafing (ADAD), never stand still
                    if (strafeT <= 0f)
                    {
                        strafeT = Random.Range(0.45f, 0.9f); strafeSide = -strafeSide;
                        crouch = Random.value < 0.15f;
                        Vector3 p = transform.position + Vector3.Cross(Vector3.up, toT) * strafeSide * Random.Range(1.8f, 3f) - toT * (dist < 5f ? 1.5f : 0f);
                        if (NavMesh.SamplePosition(p, out var h, 1.5f, NavMesh.AllAreas)) agent.SetDestination(h.position);
                    }
                    agent.speed = 3.2f;
                }
                else if (strafeT <= 0f && !atCover)
                {
                    strafeT = Random.Range(0.8f, 2f);
                    crouch = weapon.kind == WeaponKind.Sniper ? Random.value < 0.7f : Random.value < 0.25f;
                    Vector3 p = transform.position + Vector3.Cross(Vector3.up, toT) * Random.Range(-3.5f, 3.5f) + toT * (dist > 30f ? 3f : 0f);
                    bool wantCover = hp < 75f || mag < weapon.magSize * 0.35f || weapon.kind == WeaponKind.Sniper || Random.value < 0.55f;
                    if (hp < 35f) p = transform.position - toT * 6f;
                    if (wantCover && FindCover(target.ChestPos, out var cp)) { p = cp; atCover = true; peekHidden = true; peekT = 0.4f; }
                    if (NavMesh.SamplePosition(p, out var h, 2f, NavMesh.AllAreas)) agent.SetDestination(h.position);
                }

                if (atCover)
                {
                    // behind cover: duck to reload / recover, then pop up and shoot
                    bool arrived = !agent.pathPending && agent.remainingDistance < 0.7f;
                    if (arrived)
                    {
                        peekT -= Time.deltaTime;
                        if (peekT <= 0f)
                        {
                            peekHidden = !peekHidden;
                            peekT = peekHidden ? Random.Range(0.6f, 1.4f) : Random.Range(1.4f, weapon.kind == WeaponKind.Sniper ? 4f : 2.6f);
                        }
                        if (reloading || (hp < 45f && Time.time - lastDamageTime < 1.5f)) peekHidden = true;
                        crouch = peekHidden;
                        if (Time.time - lastDamageTime < 0.3f && hp < 60f) { peekHidden = true; peekT = 1.2f; }
                    }
                    else crouch = false;
                    // leave cover now and then (or when the enemy got too close / walked around it)
                    if (dist < 7f || (strafeT <= -6f && Random.value < 0.02f)) { atCover = false; strafeT = 0f; }
                }
                if (crouch) agent.speed = 0.9f;
                Face(target.ChestPos);
            }
            else
            {
                atCover = false;
                crouch = false;
                agent.speed = 4.4f;
                if (mag < weapon.magSize * 0.6f && !reloading) StartReload();
                if (Time.time - lastKnownT < 12f)
                {
                    // investigate: move in, weapon up toward where the enemy was (checks the corner instead of looking at the floor)
                    agent.speed = 3.2f;
                    if (repathT <= 0f) { repathT = 1f; agent.SetDestination(lastKnown); }
                    if (Vector3.Distance(transform.position, lastKnown) < 2f) lastKnownT = -99f;
                    Face(lastKnown);
                    aimDir = Vector3.Slerp(aimDir, (lastKnown + Vector3.up * 1.2f - EyePos).normalized, Time.deltaTime * 5f);
                }
                else
                {
                    bool arrived = hasRoam && agent.remainingDistance < 1.5f && !agent.pathPending;
                    if (arrived && weapon.kind == WeaponKind.Sniper && holdT <= 0f) holdT = Random.Range(6f, 12f);  // snipers hold an angle
                    if (holdT > 0f)
                    {
                        holdT -= Time.deltaTime; crouch = true;
                        Face(mm.SideCenter(1 - team));
                        aimDir = Vector3.Slerp(aimDir, transform.forward, Time.deltaTime * 3f);
                        if (holdT > 0f) goto Steps;
                        hasRoam = false;
                    }
                    if (!hasRoam || arrived || repathT < -15f)
                    {
                        // drift toward the enemy half, but spread out: skip spots a teammate is already near
                        Vector3 enemySide = mm.SideCenter(1 - team);
                        Vector3 best = mm.waypoints[Random.Range(0, mm.waypoints.Count)]; float bs = float.MaxValue;
                        for (int i = 0; i < 6; i++)
                        {
                            var c = mm.waypoints[Random.Range(0, mm.waypoints.Count)];
                            float sc = Vector3.Distance(c, enemySide) + Random.Range(0f, 12f);
                            foreach (var a in All) if (a != this && a.team == team && a.Alive && Vector3.Distance(a.transform.position, c) < 9f) sc += 25f;
                            if (sc < bs) { bs = sc; best = c; }
                        }
                        roamGoal = best; hasRoam = true; repathT = 0f;
                        agent.SetDestination(roamGoal);
                    }
                    Vector3 v = agent.velocity.Flat();
                    if (v.sqrMagnitude > 0.2f) Face(transform.position + v);
                    aimDir = Vector3.Slerp(aimDir, transform.forward, Time.deltaTime * 4f);
                }
            }

            Steps:
            float sp = agent.velocity.magnitude;
            if (sp > 0.5f)
            {
                stepAcc += sp * Time.deltaTime;
                if (stepAcc > 2.2f)
                {
                    stepAcc = 0;
                    Sfx.Play3D(Sfx.StepClip(transform.position, sp > 4f ? 1 : 0), transform.position, 0.7f, 2f, 24f, 0.08f);
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
            Vector3 lv = transform.InverseTransformDirection(Vel);
            float runN = 4.5f;
            anim.SetFloat("MoveX", lv.x / runN, 0.12f, Time.deltaTime);
            anim.SetFloat("MoveZ", lv.z / runN, 0.12f, Time.deltaTime);
            anim.SetBool("Crouch", crouch);
            anim.SetBool("Aim", puppet ? Ready : target != null);
            anim.SetFloat("Speed", Mathf.Clamp01(lv.magnitude / 1.5f));
        }

        /// Rifle held from the shoulder along the aim line; hands are put on the grip and handguard with IK
        /// (instead of the gun following whatever the hands do in each clip, which made it point sideways).
        void OnAnimatorIK(int layer)
        {
            if (layer != 1 || !Alive || gun == null || anim == null) return;
            ikFrame = Time.frameCount;
            PlaceGun();
            float leftW = reloading ? 0f : 1f;
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
            anim.SetIKPosition(AvatarIKGoal.RightHand, gun.position);
            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftW);
            anim.SetIKPosition(AvatarIKGoal.LeftHand, gun.TransformPoint(weapon.forePoint));
            Vector3 sh = rShoulder != null ? rShoulder.position : transform.position + Vector3.up * 1.4f;
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0.6f);
            anim.SetIKHintPosition(AvatarIKHint.RightElbow, sh + transform.right * 0.25f + Vector3.down * 0.35f);
            anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0.6f);
            anim.SetIKHintPosition(AvatarIKHint.LeftElbow, sh - transform.right * 0.55f + Vector3.down * 0.4f + transform.forward * 0.15f);
            anim.SetLookAtWeight(1f, (puppet ? Ready : target != null) ? 0.45f : 0.2f, 0.8f, 0f, 0.5f);
            anim.SetLookAtPosition(EyePos + aimDir * 10f);
        }

        void PlaceGun()
        {
            Vector3 fwd = aimDir.sqrMagnitude > 0.01f ? aimDir.normalized : transform.forward;
            bool ready = Ready;
            if (!ready) fwd = Vector3.Slerp(transform.forward, Vector3.down, 0.3f);         // low ready while walking around
            if (reloading) fwd = Vector3.Slerp(transform.forward, Vector3.down, 0.45f);
            var rot = Quaternion.LookRotation(fwd, Vector3.up);
            if (!ready) rot = rot * Quaternion.Euler(0, -18f, 0);                              // muzzle turned slightly inward
            Vector3 sh = rShoulder != null ? rShoulder.position : transform.position + Vector3.up * (crouch ? 1.0f : 1.42f) + transform.right * 0.18f;
            gun.rotation = rot;
            gun.position = sh + rot * new Vector3(-0.06f, -0.08f, 0.24f);                      // stock in the shoulder pocket, grip in front of it
        }

        void LateUpdate()
        {
            if (!Alive) return;
            // fallback when the animator has no IK pass (old controller): still hold the gun along the aim line
            if (gun != null && ikFrame != Time.frameCount) PlaceGun();
            // the Mixamo rifle idle / walk hunch forward with the head down: stand them up straight.
            // The gun turns with the chest, so the hands stay on it. Not while aiming (that pose is already upright and
            // tilting it would move the muzzle off target), crouched, prone or reloading.
            if (!Ready && !crouch && !prone && !reloading) Posture.Straighten(anim, transform, 0.9f, gun);
            else if (!prone) Posture.LevelHead(anim, transform, 0.6f);
        }

        void SpawnGun()
        {
            var g = Instantiate(weapon.prefab, transform);
            foreach (var c in g.GetComponentsInChildren<Collider>()) Destroy(c);
            Util.SetShadows(g, Settings.Quality >= 1 ? ShadowCastingMode.On : ShadowCastingMode.Off);
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
            // stays on the ground for a while; walk over it to take its ammo if it fits one of your guns
            var pk = g.AddComponent<WeaponPickup>(); pk.ammo = WeaponPickup.AmmoOf(weapon.id); pk.amount = Mathf.Max(weapon.magSize, mag + weapon.magSize / 2); pk.weaponId = weapon.id;
        }

        protected override void OnHurt(Actor from, Vector3 fromPos)
        {
            if (from != null && target == null) { lastKnown = from.ChestPos; lastKnownT = Time.time; }
            anim.SetTrigger("Hit");
        }

        protected override void OnDie(Actor killer)
        {
            deadT = 0f;
            Vector3 carry = agent != null && agent.enabled ? agent.velocity : Vector3.zero;
            col.enabled = false;
            if (ragdoll != null && ragdoll.Built)
            {
                // physical fall: pushed away from the shot (harder for snipers / headshots), or thrown by the blast
                float strength = lastHitExplosive ? 7f : Mathf.Lerp(35f, 95f, Mathf.InverseLerp(20f, 110f, lastHitAmount)) * (lastHitHead ? 1.25f : 1f);
                anim.SetBool("Dead", true);
                ragdoll.Activate(lastDamageFrom, strength, lastHitHead, lastHitExplosive, carry);
            }
            else { anim.SetTrigger("Die"); anim.SetBool("Dead", true); }
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
            target = null;
            DropGun();
            Sfx.Play3D(Z47Refs.I.bodyFall, transform.position, 0.8f, 3f, 30f);
        }

        public override void Respawn(Vector3 pos, Quaternion rot)
        {
            if (puppet) { transform.position = pos; netPos = pos; netVel = pupVel = Vector3.zero; netT = Time.time; netYaw = rot.eulerAngles.y; netFlags = 1; prone = crouch = false; }
            else { agent.enabled = true; agent.Warp(pos); }
            transform.rotation = rot;
            col.enabled = true;
            hp = maxHp; mag = weapon.magSize; reloading = false; target = null; lastKnownT = -99f; hasRoam = false; nades = NadeCount; nextNade = Time.time + Random.Range(20f, 35f); fleeUntil = 0f;
            foreach (var r in rends) r.enabled = true;
            if (gun == null) SpawnGun();
            if (ragdoll != null) ragdoll.Deactivate();
            anim.SetBool("Dead", false);
            anim.Rebind();
        }
    
        // ------------------------------------------------------------ online
        public void MakePuppet(bool human, ulong ownerClient)
        {
            puppet = true; remote = true; netHuman = human; owner = ownerClient;
            if (agent != null) agent.enabled = false;
            netPos = transform.position; netYaw = transform.eulerAngles.y; netT = Time.time;
        }

        /// The player who controlled this soldier left: the host's AI takes over (other phones keep seeing a puppet).
        public void TakeOverByBot()
        {
            if (!puppet) return;
            puppet = false; remote = false; netHuman = false; prone = false;
            target = null; hasRoam = false; lastKnownT = -99f;
            if (Alive) { agent.enabled = true; if (NavMesh.SamplePosition(transform.position, out var h, 4f, NavMesh.AllAreas)) agent.Warp(h.position); }
        }

        public override void GetNetState(out Vector3 pos, out float yaw, out float pitch, out int flags, out int weaponIdx)
        {
            pos = transform.position; yaw = transform.eulerAngles.y;
            Vector3 d = aimDir.sqrMagnitude > 0.01f ? aimDir.normalized : transform.forward;
            pitch = -Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg;
            flags = (Alive ? 1 : 0) | (crouch ? 2 : 0) | (prone ? 4 : 0) | (Ready ? 8 : 0) | (reloading ? 16 : 0);
            weaponIdx = Net.WeaponIndex(weapon != null ? weapon.id : "m4");
            if (puppet) { pos = netPos; yaw = netYaw; pitch = netPitch; flags = netFlags; }
        }

        public void SetNetState(Vector3 p, float yaw, float pitch, int flags, int weaponIdx)
        {
            float now = Time.time;
            if (netT > 0f) { float dt = now - netT; if (dt > 0.02f) netVel = Vector3.ClampMagnitude((p - netPos) / dt, 9f); }
            if ((p - transform.position).sqrMagnitude > 36f) { transform.position = p; netVel = Vector3.zero; }   // teleport (respawn / desync)
            netPos = p; netT = now; netYaw = yaw; netPitch = pitch; netFlags = flags;
            string wid = Net.Weapons[Mathf.Clamp(weaponIdx, 0, Net.Weapons.Length - 1)];
            if (weapon == null || weapon.id != wid) SetWeapon(wid);
        }

        void SetWeapon(string id)
        {
            var w = Z47Refs.I.Weapon(id); if (w == null || w == weapon) return;
            if (gun != null) { Destroy(gun.gameObject); gun = null; }
            weapon = w; mag = w.magSize;
            if (Alive) SpawnGun();
        }

        void PuppetUpdate()
        {
            float age = Mathf.Min(Time.time - netT, 0.25f);
            Vector3 goal = netPos + netVel * age;
            Vector3 before = transform.position;
            transform.position = Vector3.Lerp(before, goal, 1f - Mathf.Exp(-Time.deltaTime * 14f));
            Vector3 v = (transform.position - before) / Mathf.Max(Time.deltaTime, 0.0001f);
            pupVel = Vector3.Lerp(pupVel, v.Flat(), 1f - Mathf.Exp(-Time.deltaTime * 10f));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, netYaw, 0), 1f - Mathf.Exp(-Time.deltaTime * 16f));
            prone = (netFlags & 4) != 0; crouch = (netFlags & 2) != 0 || prone;
            reloading = (netFlags & 16) != 0;
            aimDir = Quaternion.Euler(netPitch, netYaw, 0) * Vector3.forward;
            // hit box follows the stance
            float h = prone ? 0.6f : crouch ? 1.25f : 1.8f;
            if (Mathf.Abs(col.height - h) > 0.01f) { col.height = h; col.center = new Vector3(0, h * 0.5f, 0); }
            // footsteps
            float sp = pupVel.magnitude;
            if (sp > 0.6f && !crouch)
            {
                stepAcc += sp * Time.deltaTime;
                if (stepAcc > 2.2f) { stepAcc = 0; Sfx.Play3D(Sfx.StepClip(transform.position, sp > 4f ? 1 : 0), transform.position, 0.7f, 2f, 24f, 0.08f); }
            }
            Animate();
            // the host keeps remote players' health: same regeneration as the player's own
            if (Net.IsHost && netHuman && hp < maxHp && Time.time - lastDamageTime > 5f) hp = Mathf.Min(maxHp, hp + 18f * Time.deltaTime);
        }

        /// Someone else's shot: sound, flash, tracer and impact here.
        public void RemoteShot(Vector3 end, bool tracer, bool flesh)
        {
            if (!Alive || weapon == null) return;
            Vector3 eye = EyePos, dir = (end - eye); float dist = dir.magnitude; if (dist < 0.01f) return; dir /= dist;
            aimDir = dir; lastShotTime = Time.time;
            Vector3 muzzle = gun != null ? gun.TransformPoint(weapon.muzzlePoint) : eye + dir * 0.6f;
            if (Time.time - lastRemoteFx > 0.045f)
            {
                lastRemoteFx = Time.time;
                Sfx.Play3D(weapon.shot, muzzle, 1f, 6f, 140f, 0.07f);
                FX.MuzzleFlash(muzzle, gun != null ? gun.forward : dir, weapon.kind == WeaponKind.Sniper ? 0.45f : 0.32f);
                if (anim != null) anim.SetTrigger("Fire");
            }
            if (tracer) FX.Tracer(muzzle, end);
            FX.Impact(end, -dir, flesh);
            Ballistics.Flyby(this, eye, dir, end, null);
        }
    }

    /// Fixes the slouch of the Mixamo rifle clips after the animator has run (LateUpdate).
    /// It measures the pose every frame and corrects it to a target, so it never builds up even when the animator skips frames.
    public static class Posture
    {
        const float UprightLean = 4f;        // a soldier standing "straight" still leans a few degrees forward

        /// Turn the spine/chest back so the torso stands up straight, then level the head.
        /// `carry` (the rifle) is turned with the chest so it stays in the hands.
        public static void Straighten(Animator a, Transform root, float amount, Transform carry = null)
        {
            if (a == null || !a.isHuman) return;
            Transform hips = a.GetBoneTransform(HumanBodyBones.Hips), spine = a.GetBoneTransform(HumanBodyBones.Spine),
                      chest = a.GetBoneTransform(HumanBodyBones.Chest), neck = a.GetBoneTransform(HumanBodyBones.Neck);
            if (hips == null || spine == null || neck == null) return;
            Vector3 fwd = root.forward, right = root.right;
            Vector3 torso = neck.position - hips.position;
            float lean = Mathf.Atan2(Vector3.Dot(torso, fwd), Vector3.Dot(torso, Vector3.up)) * Mathf.Rad2Deg;     // + = bent forward
            float fix = -Mathf.Clamp(lean - UprightLean, -10f, 40f) * amount;
            if (Mathf.Abs(fix) > 0.2f)
            {
                // split over the spine bones (a straight back, not a broken one)
                var bones = chest != null ? new[] { spine, chest } : new[] { spine };
                var q = Quaternion.AngleAxis(fix / bones.Length, right);
                foreach (var b in bones)
                {
                    Vector3 pivot = b.position;
                    b.rotation = q * b.rotation;
                    if (carry != null) { carry.position = pivot + q * (carry.position - pivot); carry.rotation = q * carry.rotation; }
                }
            }
            LevelHead(a, root, amount);
        }

        /// Chin up: the idle clips look at the floor.
        public static void LevelHead(Animator a, Transform root, float amount)
        {
            if (a == null || !a.isHuman) return;
            Transform neck = a.GetBoneTransform(HumanBodyBones.Neck), head = a.GetBoneTransform(HumanBodyBones.Head);
            if (neck == null || head == null) return;
            Vector3 n = head.position - neck.position;
            float tilt = Mathf.Atan2(Vector3.Dot(n, root.forward), Vector3.Dot(n, Vector3.up)) * Mathf.Rad2Deg;   // + = head forward/down
            float fix = -Mathf.Clamp(tilt - 6f, -10f, 45f) * amount * 0.8f;
            if (Mathf.Abs(fix) > 0.2f) neck.rotation = Quaternion.AngleAxis(fix, root.right) * neck.rotation;
        }
    }
}
