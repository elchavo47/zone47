using System.Collections.Generic;
using UnityEngine;

namespace Zone47
{
    /// Base for anything that can be shot: player and bots.
    public abstract class Actor : MonoBehaviour
    {
        public static readonly List<Actor> All = new List<Actor>();

        public int team;
        public string callsign = "?";
        public float maxHp = 100f;
        public float hp = 100f;
        public int kills, deaths, streak;
        // match stats (end screen)
        public int shotsFired, shotsHit, headshotKills, bestStreak; public float longestKill, damageDealt;
        public readonly Dictionary<string, int> killsBy = new Dictionary<string, int>();
        public string flag = "tr";
        public bool Alive => hp > 0f;
        public bool isPlayer;
        public float lastDamageTime = -99f;
        public Vector3 lastDamageFrom;
        public bool lastHitHead, lastHitExplosive, lastHitDrone; public float lastHitAmount;
        public virtual string WeaponId => null;
        /// set by explosions / air strikes around their Damage() calls (ragdoll force, kill feed)
        public static bool ExplosionDamage, DroneDamage; public static string HazardName;
        public float lastShotTime = -99f;
        public float spawnTime;
        public float respawnAt;       // when a dead actor comes back (squad card countdown)
        public bool Protected => Time.time - spawnTime < 2.5f;
        // online: id shared by every phone; `remote` = simulated on another phone (we only show it)
        public int netId = -1;
        public bool remote;

        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        protected virtual void OnDestroy() { All.Remove(this); }

        public abstract Vector3 EyePos { get; }
        public abstract Vector3 ChestPos { get; }
        public abstract float HeadHeight { get; }   // world y above which a hit counts as headshot

        float lastGrunt = -9f;
        public virtual void Damage(float amount, Actor from, Vector3 fromPos, bool headshot)
        {
            if (!Alive || MatchManager.I == null || !MatchManager.I.Running) return;
            if (from != null && from != this && from.team == team) return; // no friendly fire
            if (Protected) return;
            if (Net.InMatch)
            {
                // the host owns all health. A client only reports what ITS soldier hit; everything else is decided by the host.
                if (Net.IsClient)
                {
                    if (from != null && from == PlayerController.Local) Net.SendDamage(this, amount, from, fromPos, headshot, ExplosionDamage, DroneDamage);
                    return;
                }
                if (from != null && from.remote && !Net.ApplyingRemote) return;   // that phone reports its own hits
            }
            if (from != null && from != this) from.damageDealt += Mathf.Min(amount, hp);
            hp -= amount;
            lastDamageTime = Time.time;
            lastDamageFrom = fromPos; lastHitHead = headshot; lastHitAmount = amount; lastHitExplosive = ExplosionDamage; lastHitDrone = DroneDamage;
            OnHurt(from, fromPos);
            Grunt();
            if (Net.InMatch) Net.SendHp(this, from, fromPos, headshot, amount, ExplosionDamage, DroneDamage);
            if (hp <= 0f)
            {
                hp = 0f;
                deaths++;
                streak = 0;
                if (from != null && from != this) { from.kills++; from.streak++; if (from.streak > from.bestStreak) from.bestStreak = from.streak; }
                MatchManager.I.OnKill(from, this, headshot);
                OnDie(from);
            }
        }

        void Grunt()
        {
            if (Time.time - lastGrunt <= 0.55f) return;
            lastGrunt = Time.time;
            if (isPlayer) Sfx.Play2D(Z47Refs.I.hurt, 0.55f, 0.1f);
            else Sfx.Play3D(Z47Refs.I.hurt, ChestPos, 0.75f, 3f, 45f, 0.18f);
        }

        /// Online (client): the host says this actor got hit.
        public void NetHurt(float newHp, Actor from, Vector3 fromPos, bool head, float amount, bool expl, bool drone)
        {
            if (!Alive) return;
            if (from != null && from == PlayerController.Local && from != this) from.damageDealt += Mathf.Min(amount, hp);
            if (newHp > 0f) hp = newHp;                     // the kill message finishes it
            lastDamageTime = Time.time; lastDamageFrom = fromPos; lastHitHead = head; lastHitAmount = amount; lastHitExplosive = expl; lastHitDrone = drone;
            OnHurt(from, fromPos);
            Grunt();
        }

        /// Online (client): the host says this actor died.
        public void NetDie(Actor killer)
        {
            hp = 0f; deaths++; streak = 0;
            if (killer != null && killer != this) { killer.kills++; killer.streak++; if (killer.streak > killer.bestStreak) killer.bestStreak = killer.streak; }
            OnDie(killer);
        }

        /// Snapshot for the network (position at the feet, view yaw/pitch, stance flags, weapon index).
        /// flags: 1 alive, 2 crouch, 4 prone, 8 aiming/alert, 16 reloading
        public virtual void GetNetState(out Vector3 pos, out float yaw, out float pitch, out int flags, out int weapon)
        {
            pos = transform.position; yaw = transform.eulerAngles.y; pitch = 0f; flags = Alive ? 1 : 0; weapon = Net.WeaponIndex(WeaponId);
        }

        protected virtual void OnHurt(Actor from, Vector3 fromPos) { }
        protected abstract void OnDie(Actor killer);
        public abstract void Respawn(Vector3 pos, Quaternion rot);

        public static Actor FromCollider(Collider c)
        {
            if (c == null) return null;
            return c.GetComponentInParent<Actor>();
        }
    }

    /// Shared hitscan logic.
    public static class Ballistics
    {
        static readonly RaycastHit[] hits = new RaycastHit[24];

        public static bool Cast(Vector3 origin, Vector3 dir, float range, Actor shooter, out RaycastHit best)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, hits, range, ~0, QueryTriggerInteraction.Ignore);
            best = default;
            float bd = float.MaxValue; bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (shooter != null && h.collider.transform.IsChildOf(shooter.transform)) continue;
                var a = Actor.FromCollider(h.collider);
                if (a != null && !a.Alive) continue;
                if (h.distance < bd) { bd = h.distance; best = h; found = true; }
            }
            return found;
        }

        /// Fires one bullet. Returns the actor hit (if any).
        public static Actor Fire(Actor shooter, Vector3 origin, Vector3 dir, float range, float damage, Vector3 tracerFrom, bool tracer, out bool killed, float fallStart = 30f, float fallEnd = 120f, float fallMin = 0.7f)
        {
            killed = false;
            Vector3 end = origin + dir * range;
            if (shooter != null) shooter.shotsFired++;
            Actor victim = null;
            if (Cast(origin, dir, range, shooter, out var hit))
            {
                end = hit.point;
                victim = Actor.FromCollider(hit.collider);
                if (victim != null)
                {
                    if (shooter != null && victim.team != shooter.team) shooter.shotsHit++;
                    bool head = hit.point.y >= victim.HeadHeight;
                    float falloff = Mathf.Lerp(1f, fallMin, Mathf.InverseLerp(fallStart, fallEnd, hit.distance));
                    bool wasAlive = victim.Alive;
                    victim.Damage(damage * falloff * (head ? 2.2f : 1f), shooter, origin, head);
                    killed = wasAlive && !victim.Alive;
                    FX.Impact(hit.point, hit.normal, true);
                    if (shooter != null && shooter.isPlayer) HUD.I?.HitMarker(killed, head);
                }
                else
                {
                    FX.Impact(hit.point, hit.normal, false);
                    FX.Hole(hit);
                    if (Random.value < 0.35f) Sfx.Play3D(Z47Refs.I.impact, hit.point, 0.5f, 2f, 30f, 0.15f);
                }
            }
            if (tracer) FX.Tracer(tracerFrom, end);
            if (Net.InMatch && shooter != null && !shooter.remote) Net.SendShot(shooter, end, tracer, victim != null);
            Flyby(shooter, origin, dir, end, victim);
            if (shooter != null) shooter.lastShotTime = Time.time;
            MatchManager.I?.Noise(origin, shooter);
            return victim;
        }

        /// Bullet flyby for the local player.
        public static void Flyby(Actor shooter, Vector3 origin, Vector3 dir, Vector3 end, Actor victim)
        {
            var p = PlayerController.Local;
            if (p != null && shooter != p && p.Alive)
            {
                Vector3 toP = p.EyePos - origin;
                float along = Vector3.Dot(toP, dir);
                if (along > 3f && along < (end - origin).magnitude + 1f)
                {
                    float miss = (toP - dir * along).magnitude;
                    if (miss < 2.2f && victim != p) Sfx.Play3D(Z47Refs.I.flyby, origin + dir * along, 0.8f, 1f, 10f, 0.2f);
                }
            }
        }
    }
}
