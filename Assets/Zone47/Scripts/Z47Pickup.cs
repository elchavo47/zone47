using UnityEngine;

namespace Zone47
{
    /// A dead soldier's gun on the ground (30 s). Walking over it takes its ammo if one of your guns uses the same rounds.
    public class WeaponPickup : MonoBehaviour
    {
        public string ammo; public int amount;
        float born; Light glow; Rigidbody rb;
        const float Life = 30f, Reach = 1.7f;

        /// Calibre per weapon: M4 and the carbine share 5.56.
        public static string AmmoOf(string id)
        {
            switch (id)
            {
                case "m4": case "carbine": return "556";
                case "awp": return "338";
                case "glock": return "9mm";
                case "ak": return "762";
                case "shotgun": return "12g";
                default: return id;
            }
        }

        void Start()
        {
            born = Time.time; rb = GetComponent<Rigidbody>();
            var pl = PlayerController.Local;
            if (pl != null && pl.UsesAmmo(ammo))
            {
                // faint glow so usable ammo can be spotted
                var g = new GameObject("glow"); g.transform.SetParent(transform, false); g.transform.localPosition = Vector3.up * 0.15f;
                glow = g.AddComponent<Light>(); glow.type = LightType.Point; glow.range = 1.6f; glow.intensity = 0f; glow.color = new Color(1f, 0.75f, 0.3f); glow.shadows = LightShadows.None;
            }
        }

        void Update()
        {
            float age = Time.time - born;
            if (age > Life) { Destroy(gameObject); return; }
            if (rb != null && age > 3f && !rb.isKinematic && rb.linearVelocity.sqrMagnitude < 0.01f) rb.isKinematic = true;   // settled: stop simulating
            if (glow != null) glow.intensity = (0.6f + 0.4f * Mathf.Sin(Time.time * 4f)) * Mathf.Clamp01((Life - age) / 3f);
            var pl = PlayerController.Local;
            if (pl == null || !pl.Alive || age < 0.6f) return;
            if ((pl.transform.position - transform.position).sqrMagnitude > Reach * Reach) return;
            int got = pl.GiveAmmo(ammo, amount);
            if (got <= 0) return;
            amount -= got;
            HUD.I?.Pickup("+" + got + (Settings.Turkish ? " MERMİ" : " AMMO"));
            Sfx.Play2D(Z47Refs.I.reload, 0.45f, 0.05f);
            if (amount <= 0) Destroy(gameObject);
        }
    }
}
