using System.Collections.Generic;
using UnityEngine;

namespace Zone47
{
    /// Runtime ragdoll built from the humanoid skeleton. Off (kinematic, no colliders) while alive;
    /// on death the animator stops and the body falls with the hit (direction and strength of the killing shot / blast).
    public class Ragdoll : MonoBehaviour
    {
        class Part { public Transform t; public Rigidbody rb; public Collider col; }
        readonly List<Part> parts = new List<Part>();
        Animator anim;
        Rigidbody chestRb, headRb, hipsRb;
        float activeAt = -1f;

        static readonly List<Ragdoll> active = new List<Ragdoll>();
        const int MaxActive = 4;          // older bodies freeze (phones)
        const float SimTime = 3.2f;        // bodies are hidden after this anyway

        public bool Built => parts.Count >= 6;

        public void Build(Animator a)
        {
            anim = a;
            if (a == null || !a.isHuman) return;
            Transform B(HumanBodyBones b) => a.GetBoneTransform(b);
            var hips = B(HumanBodyBones.Hips);
            var chest = B(HumanBodyBones.Chest) ?? B(HumanBodyBones.Spine);
            var head = B(HumanBodyBones.Head);
            if (hips == null || chest == null || head == null) return;

            hipsRb = Box(hips, null, 12f, Vector3.Lerp(hips.position, chest.position, 0.3f), new Vector3(0.3f, 0.24f, 0.22f));
            chestRb = Box(chest, hipsRb, 12f, Vector3.Lerp(chest.position, head.position, 0.35f), new Vector3(0.34f, 0.3f, 0.22f), 20f, 25f);
            headRb = Sphere(head, chestRb, 5f, head.position + Vector3.up * 0.08f, 0.11f);
            Limb(B(HumanBodyBones.LeftUpperArm), B(HumanBodyBones.LeftLowerArm), chestRb, 2.5f, 0.06f, out var lua);
            Limb(B(HumanBodyBones.LeftLowerArm), B(HumanBodyBones.LeftHand), lua, 2f, 0.05f, out _);
            Limb(B(HumanBodyBones.RightUpperArm), B(HumanBodyBones.RightLowerArm), chestRb, 2.5f, 0.06f, out var rua);
            Limb(B(HumanBodyBones.RightLowerArm), B(HumanBodyBones.RightHand), rua, 2f, 0.05f, out _);
            Limb(B(HumanBodyBones.LeftUpperLeg), B(HumanBodyBones.LeftLowerLeg), hipsRb, 7f, 0.08f, out var lul);
            Limb(B(HumanBodyBones.LeftLowerLeg), B(HumanBodyBones.LeftFoot), lul, 4f, 0.065f, out _);
            Limb(B(HumanBodyBones.RightUpperLeg), B(HumanBodyBones.RightLowerLeg), hipsRb, 7f, 0.08f, out var rul);
            Limb(B(HumanBodyBones.RightLowerLeg), B(HumanBodyBones.RightFoot), rul, 4f, 0.065f, out _);
            SetOn(false);
        }

        Rigidbody Body(Transform t, Rigidbody parent, float mass, float twist, float swing)
        {
            var rb = t.gameObject.AddComponent<Rigidbody>();
            rb.mass = mass; rb.isKinematic = true; rb.linearDamping = 0.05f; rb.angularDamping = 0.6f;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            if (parent != null)
            {
                var j = t.gameObject.AddComponent<CharacterJoint>();
                j.connectedBody = parent;
                j.lowTwistLimit = new SoftJointLimit { limit = -twist };
                j.highTwistLimit = new SoftJointLimit { limit = twist };
                j.swing1Limit = new SoftJointLimit { limit = swing };
                j.swing2Limit = new SoftJointLimit { limit = swing * 0.7f };
                j.enableProjection = true; j.projectionDistance = 0.04f; j.projectionAngle = 10f;
            }
            return rb;
        }

        static float Scale(Transform t) => Mathf.Max(0.0001f, t.lossyScale.x);

        Rigidbody Box(Transform t, Rigidbody parent, float mass, Vector3 worldCenter, Vector3 worldSize, float twist = 15f, float swing = 20f)
        {
            var rb = Body(t, parent, mass, twist, swing);
            var c = t.gameObject.AddComponent<BoxCollider>();
            c.center = t.InverseTransformPoint(worldCenter);
            c.size = worldSize / Scale(t);
            parts.Add(new Part { t = t, rb = rb, col = c });
            return rb;
        }

        Rigidbody Sphere(Transform t, Rigidbody parent, float mass, Vector3 worldCenter, float worldR)
        {
            var rb = Body(t, parent, mass, 30f, 35f);
            var c = t.gameObject.AddComponent<SphereCollider>();
            c.center = t.InverseTransformPoint(worldCenter);
            c.radius = worldR / Scale(t);
            parts.Add(new Part { t = t, rb = rb, col = c });
            return rb;
        }

        void Limb(Transform t, Transform child, Rigidbody parent, float mass, float worldR, out Rigidbody rb)
        {
            rb = null;
            if (t == null || child == null || parent == null) return;
            rb = Body(t, parent, mass, 30f, 45f);
            Vector3 local = t.InverseTransformPoint(child.position);
            var c = t.gameObject.AddComponent<CapsuleCollider>();
            float ax = Mathf.Abs(local.x), ay = Mathf.Abs(local.y), az = Mathf.Abs(local.z);
            c.direction = ax > ay && ax > az ? 0 : ay > az ? 1 : 2;
            c.center = local * 0.5f;
            c.radius = worldR / Scale(t);
            c.height = local.magnitude + c.radius;
            parts.Add(new Part { t = t, rb = rb, col = c });
        }

        void SetOn(bool on)
        {
            foreach (var p in parts)
            {
                p.rb.isKinematic = !on;
                p.rb.interpolation = on ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
                p.col.enabled = on;
            }
        }

        /// Death: go limp and take the hit. `from` = where the damage came from.
        public void Activate(Vector3 from, float strength, bool head, bool explosion, Vector3 carryVelocity)
        {
            if (!Built) return;
            if (anim != null) anim.enabled = false;
            SetOn(true);
            // corpses don't trip the player
            var pl = PlayerController.Local;
            var plCol = pl != null ? pl.GetComponent<CharacterController>() : null;
            if (plCol != null) foreach (var p in parts) Physics.IgnoreCollision(p.col, plCol, true);
            foreach (var p in parts) p.rb.linearVelocity = carryVelocity;

            if (explosion)
            {
                foreach (var p in parts) p.rb.AddExplosionForce(strength * p.rb.mass, from, 12f, 1.3f, ForceMode.Impulse);
            }
            else
            {
                Vector3 dir = (chestRb.position - from); dir.y = Mathf.Max(dir.y, 0f); dir = dir.normalized + Vector3.up * 0.15f;
                var hit = head && headRb != null ? headRb : chestRb;
                hit.AddForce(dir * strength, ForceMode.Impulse);
                if (head && headRb != null) chestRb.AddForce(dir * strength * 0.4f, ForceMode.Impulse);
            }

            activeAt = Time.time;
            active.Remove(this); active.Add(this);
            while (active.Count > MaxActive) { var old = active[0]; active.RemoveAt(0); if (old != null) old.Freeze(); }
        }

        void Update()
        {
            if (activeAt > 0f && Time.time - activeAt > SimTime) Freeze();
        }

        void Freeze()
        {
            if (activeAt < 0f) return;
            activeAt = -1f; active.Remove(this);
            foreach (var p in parts) { p.rb.isKinematic = true; p.rb.interpolation = RigidbodyInterpolation.None; p.col.enabled = false; }
        }

        /// Respawn: back to the animator.
        public void Deactivate()
        {
            if (!Built) return;
            activeAt = -1f; active.Remove(this);
            SetOn(false);
            if (anim != null) anim.enabled = true;
        }

        void OnDestroy() { active.Remove(this); }
    }
}
