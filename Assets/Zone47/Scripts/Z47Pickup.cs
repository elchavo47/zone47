using UnityEngine;

namespace Zone47
{
    /// A dead soldier's gun on the ground (30 s). Walking over it takes its ammo if one of your guns uses the same rounds;
    /// if it's a main weapon you don't carry, a TAKE button appears to swap it with the one in your hands (PUBG style).
    public class WeaponPickup : MonoBehaviour
    {
        public string ammo; public int amount;
        public string weaponId;                     // which gun this is (for swapping)

        /// Drop a gun on the ground (the one you swapped away).
        public static WeaponPickup Drop(WeaponDef def, Vector3 pos, int rounds)
        {
            if (def == null || def.prefab == null) return null;
            var g = Instantiate(def.prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 90f));
            foreach (var c in g.GetComponentsInChildren<Collider>()) Destroy(c);
            var bc = g.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.02f, def.muzzlePoint.z * 0.4f);
            bc.size = new Vector3(0.06f, 0.18f, Mathf.Max(0.2f, def.muzzlePoint.z * 1.3f));
            var rb = g.AddComponent<Rigidbody>(); rb.mass = 3.5f; rb.linearVelocity = Vector3.up * 0.5f;
            var pk = g.AddComponent<WeaponPickup>(); pk.ammo = AmmoOf(def.id); pk.amount = Mathf.Max(def.magSize, rounds); pk.weaponId = def.id;
            return pk;
        }
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
            if (!string.IsNullOrEmpty(weaponId) && pl.CanTake(weaponId)) HUD.I?.OfferWeapon(this);
            int got = pl.GiveAmmo(ammo, amount);
            if (got <= 0) return;
            amount -= got;
            HUD.I?.Pickup("+" + got + (Settings.Turkish ? " MERMİ" : " AMMO"));
            Sfx.Play2D(Z47Refs.I.reload, 0.45f, 0.05f);
            if (amount <= 0) Destroy(gameObject);
        }
    }
}
