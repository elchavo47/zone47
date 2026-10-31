using UnityEngine;

namespace Zone47
{
    /// Thrown grenade.
    public class Grenade : MonoBehaviour
    {
        public Actor owner;
        public float fuse = 2.6f;
        float t;
        public const float BlastR = 7.5f;
        public static readonly System.Collections.Generic.List<Grenade> Live = new System.Collections.Generic.List<Grenade>();
        public float TimeLeft => fuse - t;
        void OnEnable() { Live.Add(this); }
        void OnDisable() { Live.Remove(this); }
        bool done;

        /// Online: someone else's grenade (it only hurts people on the phone that threw it; see Actor.Damage).
        public static void Spawn(Actor owner, Vector3 pos, Vector3 vel)
        {
            if (Z47Refs.I.grenadePrefab == null) return;
            var go = Instantiate(Z47Refs.I.grenadePrefab, pos, Random.rotation);
            var g = go.GetComponent<Grenade>(); if (g != null) g.owner = owner;
            var rb = go.GetComponent<Rigidbody>(); if (rb != null) { rb.linearVelocity = vel; rb.angularVelocity = Random.insideUnitSphere * 10f; }
        }

        void Update()
        {
            t += Time.deltaTime;
            if (!done && t >= fuse) Explode();
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.relativeVelocity.magnitude > 2f) Sfx.Play3D(Z47Refs.I.bounce, transform.position, Mathf.Clamp01(c.relativeVelocity.magnitude / 10f) * 0.8f, 2f, 30f, 0.12f);
        }

        void Explode()
        {
            done = true;
            Vector3 p = transform.position;
            FX.Explosion(p);
            Sfx.Play3D(Z47Refs.I.explosion, p, 1f, 8f, 160f, 0.05f);
            foreach (var a in Actor.All.ToArray())
            {
                if (!a.Alive) continue;
                float d = Vector3.Distance(a.ChestPos, p);
                if (d > 7.5f) continue;
                // cover check
                if (Physics.Linecast(p + Vector3.up * 0.3f, a.ChestPos, out var h, ~0, QueryTriggerInteraction.Ignore))
                {
                    var hitA = Actor.FromCollider(h.collider);
                    if (hitA != a) continue;
                }
                float dmg = Mathf.Lerp(150f, 15f, d / 7.5f);
                Actor.ExplosionDamage = true; a.Damage(dmg, owner, p, false); Actor.ExplosionDamage = false;
            }
            var pl = PlayerController.Local;
            if (pl != null) pl.Shake(Mathf.Clamp01(1f - Vector3.Distance(pl.EyePos, p) / 30f) * 1.2f);
            MatchManager.I?.Noise(p, owner);
            Destroy(gameObject);
        }
    }
}
