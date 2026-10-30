using UnityEngine;

namespace Zone47
{
    /// Thrown grenade.
    public class Grenade : MonoBehaviour
    {
        public Actor owner;
        public float fuse = 2.6f;
        float t;
        bool done;

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
                a.Damage(dmg, owner, p, false);
            }
            var pl = PlayerController.Local;
            if (pl != null) pl.Shake(Mathf.Clamp01(1f - Vector3.Distance(pl.EyePos, p) / 30f) * 1.2f);
            MatchManager.I?.Noise(p, owner);
            Destroy(gameObject);
        }
    }
}
