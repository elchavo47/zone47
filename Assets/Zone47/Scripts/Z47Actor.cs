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
        public bool Alive => hp > 0f;
        public bool isPlayer;
        public float lastDamageTime = -99f;
        public Vector3 lastDamageFrom;
        public float lastShotTime = -99f;
        public float spawnTime;
        public float respawnAt;       // when a dead actor comes back (squad card countdown)
        public bool Protected => Time.time - spawnTime < 2.5f;

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
            hp -= amount;
            lastDamageTime = Time.time;
            lastDamageFrom = fromPos;
            OnHurt(from, fromPos);
            if (Time.time - lastGrunt > 0.55f)
            {
                lastGrunt = Time.time;
                if (isPlayer) Sfx.Play2D(Z47Refs.I.hurt, 0.55f, 0.1f);
                else Sfx.Play3D(Z47Refs.I.hurt, ChestPos, 0.75f, 3f, 45f, 0.18f);
            }
            if (hp <= 0f)
            {
                hp = 0f;
                deaths++;
                streak = 0;
                if (from != null && from != this) { from.kills++; from.streak++; }
                MatchManager.I.OnKill(from, this, headshot);
                OnDie(from);
            }
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
        public static Actor Fire(Actor shooter, Vector3 origin, Vector3 dir, float range, float damage, Vector3 tracerFrom, bool tracer, out bool killed)
        {
            killed = false;
            Vector3 end = origin + dir * range;
            Actor victim = null;
            if (Cast(origin, dir, range, shooter, out var hit))
            {
                end = hit.point;
                victim = Actor.FromCollider(hit.collider);
                if (victim != null)
                {
                    bool head = hit.point.y >= victim.HeadHeight;
                    float falloff = Mathf.Lerp(1f, 0.7f, Mathf.InverseLerp(30f, 120f, hit.distance));
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

            // bullet flyby for the local player
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
            if (shooter != null) shooter.lastShotTime = Time.time;
            MatchManager.I?.Noise(origin, shooter);
            return victim;
        }
    }
}
