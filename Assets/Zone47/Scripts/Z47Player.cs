using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Zone47
{
    /// Input gathered from the touch HUD (and keyboard/mouse in the editor).
    public static class PInput
    {
        public static Vector2 move;
        public static Vector2 look;          // accumulated pixels this frame (consumed by player)
        public static bool fireHeld;
        public static bool aim;
        public static bool jump, crouchToggle, proneToggle, reload, grenade, next;
        public static int lean;              // -1 left, 0 none, 1 right (toggled by the HUD buttons)
        public static int select = -1;
        public static void ResetEdges() { jump = crouchToggle = proneToggle = reload = grenade = next = false; select = -1; look = Vector2.zero; }
    }

    public class PlayerController : Actor
    {
        public static PlayerController Local;

        CharacterController cc;
        public Camera cam;
        Transform viewRoot;
        float yaw, pitch;
        float camH = 1.62f;
        Vector3 velocity;
        bool crouched, grounded;
        float stepDist;
        float shake;
        float recoilPitch, recoilYaw, recoilPitchVel, recoilYawVel;
        float landDip; float camRoll;
        bool prone; float leanT, leanMax = 1f;
        Transform glanceAt; float glanceT, glanceDur, glanceYaw, glancePitch;
        float heat, fovKick, fovKickVel, wallT, breathT;

        // weapons
        class Slot { public WeaponDef def; public GameObject go; public int mag, reserve; public Transform mag3d; public Animation anim; public Renderer[] rends; public float reloadDur; public Renderer adsReticle; public Transform reticle; }
        Slot[] slots;
        int cur;
        float nextFire;
        bool reloading; float reloadT;
        float switchT;        // >0 while lowering/raising
        int pendingSlot = -1;
        float boltT;
        public int grenades = 2;
        float throwT = -1f;
        Vector3 kickPos, kickPosVel; float kickRot, kickRoll, kickYaw, kickRotVel, kickRollVel, kickYawVel;
        static void Spring(ref float x, ref float v, float k, float c, float dt) { v += (-k * x - c * v) * dt; x += v * dt; }
        int shotIdx; float lastShotAt = -9f;
        Vector3 swayPos; Vector3 swayRot; float strafeTilt;
        float adsT;
        bool triggerWasDown;
        bool keyFire;
        bool lockNext = true, wasLocked;
        bool Firing => PInput.fireHeld || keyFire;
        public float spreadNow;
        Transform flash; float flashT;
        ArmsRig arms;
        Actor killer;

        public bool Aiming => PInput.aim && !reloading && switchT <= 0 && throwT < 0;
        public WeaponDef Cur => slots[cur].def;
        public override string WeaponId => slots != null ? slots[cur].def.id : null;
        public int SlotCount => slots.Length;
        public WeaponDef SlotDef(int i) => slots[i].def;
        public int CurSlot => cur;
        public int Mag => slots[cur].mag;
        public int Reserve => slots[cur].reserve;
        public bool Reloading => reloading;
        public float ReloadLeft => reloading ? Mathf.Max(0f, slots[cur].reloadDur - reloadT) : 0f;
        public float ReloadProgress => reloading && slots[cur].reloadDur > 0f ? Mathf.Clamp01(reloadT / slots[cur].reloadDur) : 0f;
        public bool ReloadCancelable => reloading && slots[cur].mag > 0;
        public bool Scoped => Alive && Aiming && Cur.scope && adsT > 0.85f;
        public float AdsT => adsT;
        public float MoveSpeed => cc != null ? new Vector2(cc.velocity.x, cc.velocity.z).magnitude : 0f;

        public override Vector3 EyePos => cam != null ? cam.transform.position : transform.position + Vector3.up * 1.6f;
        public override Vector3 ChestPos => transform.position + Vector3.up * (prone ? 0.3f : crouched ? 0.8f : 1.25f) + transform.right * leanT * 0.22f;
        public override float HeadHeight => EyePos.y - (prone ? 0.12f : 0.2f);

        public static PlayerController Create(Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("Player");
            go.transform.SetPositionAndRotation(pos, rot);
            var p = go.AddComponent<PlayerController>();
            p.Init();
            return p;
        }

        void Init()
        {
            Local = this;
            foreach (var c in casings) if (c != null) Destroy(c);
            casings.Clear();
            isPlayer = true; team = 0; callsign = Settings.Callsign;
            cc = gameObject.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.45f; cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;

            var camGo = new GameObject("Eye");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0, camH, 0);
            cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f; cam.farClipPlane = 400f; cam.fieldOfView = 70f;
            camGo.tag = "MainCamera";
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = Settings.PostFx;
            data.antialiasing = Settings.AA;
            int blurLayer = LayerMask.NameToLayer("Viewmodel");
            if (blurLayer >= 0) data.volumeLayerMask = data.volumeLayerMask | (1 << blurLayer);    // main view also sees the scope-blur volume
            Weather.SetupCamera(cam);

            viewRoot = new GameObject("ViewRoot").transform;
            viewRoot.SetParent(camGo.transform, false);

            var R = Z47Refs.I;
            var idl = new System.Collections.Generic.List<string> { "m4", "awp", "glock" };
            if (R.HasWeapon("carbine")) idl.Add("carbine");
            if (R.HasWeapon("shotgun")) idl.Add("shotgun");
            string[] ids = idl.ToArray();
            slots = new Slot[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                var d = R.Weapon(ids[i]);
                var w = Instantiate(d.prefab, viewRoot);
                w.name = "VM_" + d.id;
                Util.SetShadows(w, ShadowCastingMode.Off);
                foreach (var c in w.GetComponentsInChildren<Collider>()) Destroy(c);
                w.SetActive(i == 0);
                slots[i] = new Slot { def = d, go = w, mag = d.magSize, reserve = d.reserve, mag3d = Util.FindDeep(w.transform, "Mag"),
                                      anim = d.animated ? w.GetComponentInChildren<Animation>() : null, rends = w.GetComponentsInChildren<Renderer>(), reloadDur = d.reloadTime };
                var adsRet = Util.FindDeep(w.transform, "ReticleADS");
                if (adsRet != null) slots[i].adsReticle = adsRet.GetComponent<Renderer>();
                slots[i].reticle = Util.FindDeep(w.transform, "Reticle");     // holo dot: bullets follow what the eye sees
                if (slots[i].anim != null) { var an = slots[i].anim; an.playAutomatically = false; an.cullingType = AnimationCullingType.AlwaysAnimate; foreach (AnimationState st in an) st.wrapMode = st.name == "idle" || st.name == "hold" ? WrapMode.Loop : WrapMode.Once; }
            }

            // muzzle flash quad
            var fq = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(fq.GetComponent<Collider>());
            fq.name = "Flash";
            fq.GetComponent<Renderer>().sharedMaterial = R.flashMat;
            fq.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            flash = fq.transform; flash.gameObject.SetActive(false);

            if (Settings.ShowArms) arms = ArmsRig.Create(cam.transform);
            SetupViewmodelCamera();

            yaw = transform.eulerAngles.y;
            AttachFlash();
        }

        Camera vmCam;
        /// Weapons render on their own layer through an overlay camera: they never clip into walls,
        /// and the stock behind the eye is simply not drawn (no more "looking inside the gun").
        void SetupViewmodelCamera()
        {
            int vm = LayerMask.NameToLayer("Viewmodel");
            if (vm < 0) { cam.nearClipPlane = 0.03f; return; }
            void SetLayer(Transform t) { t.gameObject.layer = vm; foreach (Transform c in t) SetLayer(c); }
            SetLayer(viewRoot); SetLayer(flash);
            if (arms != null) SetLayer(arms.transform);
            cam.cullingMask &= ~(1 << vm);
            var g = new GameObject("ViewmodelCam");
            g.transform.SetParent(cam.transform, false);
            vmCam = g.AddComponent<Camera>();
            vmCam.cullingMask = 1 << vm;
            vmCam.nearClipPlane = 0.01f; vmCam.farClipPlane = 4f; vmCam.fieldOfView = 56f;
            var d = vmCam.GetUniversalAdditionalCameraData();
            d.renderType = CameraRenderType.Overlay;
            d.renderShadows = false;
            d.renderPostProcessing = Settings.PostFx;
            cam.GetUniversalAdditionalCameraData().cameraStack.Add(vmCam);
        }

        void AttachFlash()
        {
            flash.SetParent(slots[cur].go.transform, false);
            flash.localPosition = Cur.muzzlePoint + Vector3.forward * 0.04f;
            flash.localRotation = Quaternion.identity;
            flash.gameObject.SetActive(false);
        }

        void Update()
        {
            if (MatchManager.I == null) return;
            ReadDesktopInput();
            if (!Alive) { DeadCam(); PInput.ResetEdges(); return; }
            bool paused = HUD.I != null && HUD.I.Paused;
            if (paused || MatchManager.I.Finished) { PInput.ResetEdges(); return; }
            if (!MatchManager.I.Running) { Look(); ViewModel(); PInput.ResetEdges(); return; }

            Look();
            Move();
            Weapons();
            ViewModel();
            Regen();
            PInput.ResetEdges();
        }

        // ------------------------------------------------------------ desktop testing
        void ReadDesktopInput()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current; var m = Mouse.current;
            if (k != null)
            {
                Vector2 mv = Vector2.zero;
                if (k.wKey.isPressed) mv.y += 1; if (k.sKey.isPressed) mv.y -= 1;
                if (k.dKey.isPressed) mv.x += 1; if (k.aKey.isPressed) mv.x -= 1;
                if (mv != Vector2.zero) PInput.move = mv.normalized * (k.leftShiftKey.isPressed ? 1f : 0.7f);
                else if (HUD.I != null && !HUD.I.JoystickActive) PInput.move = Vector2.zero;
                if (k.spaceKey.wasPressedThisFrame) PInput.jump = true;
                if (k.cKey.wasPressedThisFrame) PInput.crouchToggle = true;
                if (k.zKey.wasPressedThisFrame) PInput.proneToggle = true;
                if (k.xKey.wasPressedThisFrame) PInput.lean = PInput.lean == -1 ? 0 : -1;
                if (k.vKey.wasPressedThisFrame) PInput.lean = PInput.lean == 1 ? 0 : 1;
                if (k.rKey.wasPressedThisFrame) PInput.reload = true;
                if (k.gKey.wasPressedThisFrame) PInput.grenade = true;
                if (Application.isEditor && k.bKey.wasPressedThisFrame) { if (AirStrikes.I == null && MatchManager.I != null) AirStrikes.Begin(MatchManager.I); AirStrikes.I?.TriggerNow(); }   // test: drone strike now
                if (k.digit1Key.wasPressedThisFrame) PInput.select = 0;
                if (k.digit2Key.wasPressedThisFrame) PInput.select = 1;
                if (k.digit3Key.wasPressedThisFrame) PInput.select = 2;
                if (k.digit4Key.wasPressedThisFrame) PInput.select = 3;
                if (k.digit5Key.wasPressedThisFrame) PInput.select = 4;
                if (k.iKey.wasPressedThisFrame && slots[cur].anim != null && !reloading && switchT <= 0) PlayAnim(slots[cur], "inspect", 0.15f);
                if (k.escapeKey.wasPressedThisFrame) HUD.I?.TogglePause();
                if (Mouse.current != null) { float sc = Mouse.current.scroll.ReadValue().y; if (sc < -0.1f) PInput.next = true; else if (sc > 0.1f) PInput.select = (cur + slots.Length - 1) % slots.Length; }
            }
            if (m != null && !Application.isMobilePlatform)
            {
                bool playing = Alive && MatchManager.I != null && !MatchManager.I.Finished && !(HUD.I != null && HUD.I.Paused);
                if (playing)
                {
                    if (Cursor.lockState != CursorLockMode.Locked && (m.leftButton.wasPressedThisFrame || lockNext)) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; lockNext = false; }
                }
                else if (Cursor.lockState == CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; lockNext = true; }

                bool locked = Cursor.lockState == CursorLockMode.Locked;
                if (locked) PInput.look += m.delta.ReadValue();
                PInput.aim = (locked && m.rightButton.isPressed) || (k != null && k.qKey.isPressed) || (HUD.I != null && HUD.I.AimToggled);
                keyFire = (k != null && k.fKey.isPressed) || (locked && m.leftButton.isPressed && !m.leftButton.wasPressedThisFrame) || (locked && m.leftButton.wasPressedThisFrame && wasLocked);
                wasLocked = locked;
            }
#endif
        }

        // ------------------------------------------------------------ look & move
        void Look()
        {
            float sens = 0.11f * Settings.Sens * (Aiming ? Settings.AdsSens * (Cur.scope ? 0.45f : 1f) : 1f);
            Vector2 d = PInput.look * sens;
            if (Scoped) d += new Vector2(Mathf.PerlinNoise(Time.time * 0.6f, 3f) - 0.5f, Mathf.PerlinNoise(7f, Time.time * 0.6f) - 0.5f) * Time.deltaTime * (crouched ? 1.2f : 2.5f); // breathing sway
            if (Settings.AimAssist && TargetUnderCrosshair(4f) != null) d *= 0.55f;
            d += GyroDelta() * (Aiming ? (Cur.scope ? 0.5f : 0.8f) : 1f);
            yaw += d.x;
            pitch = Mathf.Clamp(pitch - d.y, -80f, 80f);
            if (glanceT > 0f && glanceAt != null && Alive)
            {
                // short cinematic look at something (the UAV): ease in, track it, then hand the view back where it was
                glanceT -= Time.deltaTime;
                Vector3 to = glanceAt.position - cam.transform.position;
                float gy = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, gp = Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -80f, 80f);
                float el = glanceDur - glanceT;
                float w = el < 0.45f ? el / 0.45f : glanceT < 0.45f ? glanceT / 0.45f : 1f;
                w = w * w * (3f - 2f * w);
                yaw = Mathf.LerpAngle(glanceYaw, gy, w);
                pitch = Mathf.Lerp(glancePitch, gp, w);
                if (glanceT <= 0f) { yaw = glanceYaw; pitch = glancePitch; glanceAt = null; }
            }

            // camera recoil on a spring: sharp kick, slight overshoot, settles (feels like a real stock in the shoulder)
            float sdt = Mathf.Min(Time.deltaTime, 0.033f);
            Spring(ref recoilPitch, ref recoilPitchVel, 110f, 17f, sdt);
            Spring(ref recoilYaw, ref recoilYawVel, 110f, 17f, sdt);
            transform.rotation = Quaternion.Euler(0, yaw, 0);

            shake = Mathf.MoveTowards(shake, 0, Time.deltaTime * 2.2f);
            Vector3 sh = shake > 0 ? new Vector3(Mathf.PerlinNoise(Time.time * 25f, 0) - 0.5f, Mathf.PerlinNoise(0, Time.time * 25f) - 0.5f, 0) * shake * 6f : Vector3.zero;
            camRoll = Mathf.Lerp(camRoll, -PInput.move.x * 1.2f * (1f - adsT), Time.deltaTime * 5f);
            cam.transform.localRotation = Quaternion.Euler(pitch - recoilPitch + sh.y, recoilYaw + sh.x, -leanT * 11f + camRoll);

            float fovT = Cur.scope ? 52f : Cur.adsFov;      // scoped: the lens does the zooming, the world around it stays wide
            adsT = Mathf.MoveTowards(adsT, Aiming ? 1f : 0f, Time.deltaTime * 6f);
            Spring(ref fovKick, ref fovKickVel, 180f, 16f, sdt);
            cam.fieldOfView = Mathf.Lerp(70f, fovT, adsT) + fovKick * (Cur.scope ? 0.25f : 1f);
            if (vmCam != null) vmCam.fieldOfView = Mathf.Lerp(56f, 44f, adsT);
            UpdateScopeCam();
        }

        Camera scopeCam; RenderTexture scopeRT;
        public RenderTexture ScopeRT => scopeRT;
        const float ScopeFov = 11f;

        void UpdateScopeCam()
        {
            bool on = Scoped && Alive;
            if (on && scopeCam == null)
            {
                int res = Settings.Quality >= 2 ? 1024 : 768;
                scopeRT = new RenderTexture(res, res, 24) { name = "ScopeRT" };
                var g = new GameObject("ScopeCam");
                g.transform.SetParent(cam.transform, false);
                scopeCam = g.AddComponent<Camera>();
                scopeCam.cullingMask = cam.cullingMask;          // no viewmodel
                scopeCam.nearClipPlane = 0.3f; scopeCam.aspect = 1f;
                scopeCam.targetTexture = scopeRT;
                scopeCam.depth = cam.depth - 1;
                var sd = scopeCam.GetUniversalAdditionalCameraData();
                sd.renderPostProcessing = Settings.PostFx;       // same colour grading as the main view, but not the blur volume
                sd.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                sd.renderShadows = Settings.Quality >= 2;
            }
            if (scopeCam != null)
            {
                scopeCam.enabled = on;
                if (on)
                {
                    scopeCam.fieldOfView = ScopeFov;
                    scopeCam.clearFlags = cam.clearFlags; scopeCam.backgroundColor = cam.backgroundColor;
                    scopeCam.farClipPlane = cam.farClipPlane;
                }
            }
            if (Boot.ScopeBlur != null) Boot.ScopeBlur.weight = on ? 1f : 0f;
        }

        /// Gyro aim (landscape): turning the phone left/right yaws, tilting it pitches. Degrees this frame.
        Vector2 GyroDelta()
        {
#if ENABLE_INPUT_SYSTEM
            int mode = Settings.Gyro;
            if (mode == 0 || (mode == 1 && !Aiming)) return Vector2.zero;
            var g = UnityEngine.InputSystem.Gyroscope.current;
            if (g == null) return Vector2.zero;
            if (!g.enabled) { InputSystem.EnableDevice(g); g.samplingFrequency = 60f; return Vector2.zero; }
            // The Input System reports the gyro in screen space (x = screen right, y = screen up, z = out of the screen),
            // already compensated for landscape. Turning the phone left/right spins it about screen-up (y) -> yaw;
            // tilting it toward/away from you spins it about screen-right (x) -> pitch.
            Vector3 w = g.angularVelocity.ReadValue();                     // rad/s
            Vector2 r = new Vector2(-w.y, w.x) * Mathf.Rad2Deg * Time.deltaTime;
            if (Settings.GyroInvert) r = -r;
            if (r.sqrMagnitude < 0.0004f) return Vector2.zero;               // ignore sensor noise when holding still
            return r * Settings.GyroSens;
#else
            return Vector2.zero;
#endif
        }

        Actor TargetUnderCrosshair(float deg)
        {
            foreach (var a in All)
            {
                if (a.team == team || !a.Alive) continue;
                Vector3 to = a.ChestPos - cam.transform.position;
                if (to.magnitude > 80f) continue;
                if (Vector3.Angle(cam.transform.forward, to) < deg) return a;
            }
            return null;
        }

        void Move()
        {
            // stances: stand / crouch / prone. Standing up needs head room.
            if (PInput.proneToggle) { if (prone) { if (CanStand(1.2f)) prone = false; } else if (grounded) { prone = true; crouched = false; } }
            if (PInput.crouchToggle) { if (prone) { if (CanStand(1.2f)) { prone = false; crouched = true; } } else if (!crouched || CanStand(1.8f)) crouched = !crouched; }
            Vector2 mv = Vector2.ClampMagnitude(PInput.move, 1f);
            // sprint anywhere in the forward 60deg cone (diagonal forward no longer drops to walk speed)
            float mag = mv.magnitude;
            bool sprint = mag > 0.88f && mv.y / Mathf.Max(mag, 0.001f) > 0.5f && !Aiming && !Firing && !reloading && !prone;
            if (sprint && crouched) { if (CanStand(1.8f)) crouched = false; else sprint = false; }
            if (sprint) PInput.lean = 0;
            float speed = prone ? 1.1f : crouched ? 2.2f : Aiming ? 2.7f : sprint ? 6.4f : 4.3f;
            // speed effectors: backing up and strafing are slower than walking forward
            if (!sprint && mag > 0.01f) { float fw = mv.y / mag; speed *= fw < 0f ? Mathf.Lerp(1f, 0.72f, -fw) : Mathf.Lerp(0.86f, 1f, fw); }
            Vector3 wish = transform.TransformDirection(new Vector3(mv.x, 0, mv.y)) * speed;

            grounded = cc.isGrounded;
            if (grounded && velocity.y < 0)
            {
                if (velocity.y < -6f) { landDip = Mathf.Clamp(-velocity.y * 0.02f, 0, 0.18f); Sfx.Play2D(Sfx.StepClip(transform.position, 2), 0.75f, 0.06f); }
                velocity.y = -2f;
            }
            float accel = grounded ? 14f : 3f;
            velocity.x = Mathf.Lerp(velocity.x, wish.x, Time.deltaTime * accel);
            velocity.z = Mathf.Lerp(velocity.z, wish.z, Time.deltaTime * accel);
            if (PInput.jump && grounded && prone) { if (CanStand(1.2f)) { prone = false; crouched = true; } }
            else if (PInput.jump && grounded && (!crouched || CanStand(1.8f))) { velocity.y = Mathf.Sqrt(2f * 9.81f * 1.05f); crouched = false; }
            velocity.y -= 9.81f * 1.6f * Time.deltaTime;
            cc.Move(velocity * Time.deltaTime);

            float targetH = prone ? 0.42f : crouched ? 1.05f : 1.62f;
            cc.height = Mathf.Lerp(cc.height, prone ? 0.6f : crouched ? 1.2f : 1.8f, Time.deltaTime * (prone ? 6f : 10f));
            cc.center = new Vector3(0, cc.height * 0.5f, 0);
            camH = Mathf.Lerp(camH, targetH, Time.deltaTime * 10f);
            landDip = Mathf.Lerp(landDip, 0, Time.deltaTime * 8f);

            float hs = new Vector2(cc.velocity.x, cc.velocity.z).magnitude;
            float bob = grounded ? Mathf.Sin(Time.time * (sprint ? 13f : 9f)) * Mathf.Clamp01(hs / 6f) * 0.03f * (1f - adsT * 0.8f) : 0f;
            // lean: peek around corners, stopped early by walls so the camera never clips through
            int lean = (prone || sprint || !Alive) ? 0 : PInput.lean;
            if (lean != 0)
            {
                Vector3 head = transform.position + Vector3.up * camH;
                leanMax = Physics.SphereCast(head, 0.15f, transform.right * lean, out var lh, 0.42f, ~0, QueryTriggerInteraction.Ignore) && !lh.collider.transform.IsChildOf(transform)
                    ? Mathf.Clamp01((lh.distance - 0.05f) / 0.37f) : 1f;
            }
            leanT = Mathf.Lerp(leanT, lean * leanMax, Time.deltaTime * 8f);
            cam.transform.localPosition = new Vector3(leanT * 0.37f, camH + bob - landDip - Mathf.Abs(leanT) * 0.06f, 0);

            if (grounded && hs > 0.5f)
            {
                stepDist += hs * Time.deltaTime;
                float stride = sprint ? 2.6f : prone ? 1.0f : crouched ? 1.5f : 2.0f;
                if (stepDist > stride)
                {
                    stepDist = 0;
                    if (!crouched && !prone) BotController.Noise(this, sprint ? 18f : 10f);      // footsteps give you away (crouch to sneak)
                    Sfx.Play2D(Sfx.StepClip(transform.position, sprint ? 1 : 0), prone ? 0.15f : crouched ? 0.22f : sprint ? 0.6f : 0.42f, 0.08f);
                }
            }
            sprinting = sprint && hs > 3f;
        }
        bool sprinting;

        bool CanStand(float h)
        {
            float r = cc != null ? cc.radius * 0.9f : 0.3f;
            Vector3 a = transform.position + Vector3.up * (r + 0.35f), b = transform.position + Vector3.up * (h - r);
            foreach (var c in Physics.OverlapCapsule(a, b, r, ~0, QueryTriggerInteraction.Ignore))
                if (!c.transform.IsChildOf(transform) && Actor.FromCollider(c) == null) return false;
            return true;
        }

        // ------------------------------------------------------------ weapons
        void Weapons()
        {
            var s = slots[cur];
            var d = s.def;

            // switching
            if (PInput.select >= 0 && PInput.select != cur && PInput.select < slots.Length) BeginSwitch(PInput.select);
            if (PInput.next) BeginSwitch((cur + 1) % slots.Length);
            if (switchT > 0)
            {
                switchT -= Time.deltaTime;
                if (pendingSlot >= 0 && switchT <= 0.2f)
                {
                    slots[cur].go.SetActive(false);
                    cur = pendingSlot; pendingSlot = -1;
                    slots[cur].go.SetActive(true);
                    AttachFlash();
                    PlayAnim(slots[cur], "draw", 0f);
                }
                return;
            }

            // grenade
            if (PInput.grenade && grenades > 0 && throwT < 0 && !reloading) { throwT = 0f; Sfx.Play2D(Z47Refs.I.pin, 0.7f); }
            if (throwT >= 0)
            {
                throwT += Time.deltaTime;
                if (throwT >= 0.45f && throwT - Time.deltaTime < 0.45f) ThrowGrenade();
                if (throwT >= 0.9f) throwT = -1f;
                return;
            }

            // fire cancels a reload as long as there is still a round in the mag (tap fire = shoot right now)
            if (reloading && s.mag > 0 && Firing && !triggerWasDown && reloadT > 0.08f)
            {
                reloading = false; reloadT = 0f;
                if (s.mag3d != null) s.mag3d.gameObject.SetActive(true);
                if (s.anim != null) PlayAnim(s, s.anim["hold"] != null ? "hold" : "idle", 0.1f);
            }

            // reload
            if ((PInput.reload || (s.mag == 0 && Firing && s.reserve > 0)) && !reloading && s.mag < d.magSize && s.reserve > 0)
            {
                reloading = true; reloadT = 0f; s.reloadDur = d.reloadTime;
                if (s.anim != null)
                {
                    string clip = s.mag == 0 && s.anim["reload_empty"] != null ? "reload_empty" : "reload";
                    if (s.anim[clip] != null) { s.reloadDur = s.anim[clip].length; PlayAnim(s, clip, 0.08f); }
                }
                Sfx.Play2D(d.reloadClip != null ? d.reloadClip : d.kind == WeaponKind.Sniper ? Z47Refs.I.awpBolt : Z47Refs.I.reload, 0.8f, 0.02f);
            }
            if (reloading)
            {
                reloadT += Time.deltaTime;
                if (s.mag3d != null) s.mag3d.gameObject.SetActive(reloadT < s.reloadDur * 0.3f || reloadT > s.reloadDur * 0.55f);
                if (reloadT >= s.reloadDur)
                {
                    int need = d.magSize - s.mag; int take = Mathf.Min(need, s.reserve);
                    s.mag += take; s.reserve -= take; reloading = false;
                    if (s.mag3d != null) s.mag3d.gameObject.SetActive(true);
                }
            }

            boltT -= Time.deltaTime;
            heat = Mathf.MoveTowards(heat, 0f, Time.deltaTime * (Firing ? 0.35f : 1.6f));
            bool down = Firing && !sprinting;
            float baseSpread = Mathf.Lerp(d.hipSpread, d.adsSpread, adsT) * (prone ? 0.55f : crouched ? 0.75f : 1f);
            float moveSpread = new Vector2(cc.velocity.x, cc.velocity.z).magnitude * 0.25f * (1f - adsT * 0.7f);
            if (!grounded) moveSpread += 3f;
            spreadNow = Mathf.Lerp(spreadNow, baseSpread + moveSpread, Time.deltaTime * 10f);

            if (down && !reloading && Time.time >= nextFire && boltT <= 0 && (d.automatic || !triggerWasDown))
            {
                if (s.mag > 0) Shoot(s);
                else if (!triggerWasDown) Sfx.Play2D(Z47Refs.I.dryFire, 0.6f);
            }
            triggerWasDown = down;

            if (flashT > 0) { flashT -= Time.deltaTime; if (flashT <= 0) flash.gameObject.SetActive(false); }
        }

        void BeginSwitch(int to)
        {
            if (to == cur && pendingSlot < 0) return;
            reloading = false; if (slots[cur].mag3d != null) slots[cur].mag3d.gameObject.SetActive(true);
            pendingSlot = to; switchT = 0.45f;
            Sfx.Play2D(Z47Refs.I.equip, 0.55f, 0.06f);
        }

        void Shoot(Slot s)
        {
            var d = s.def;
            s.mag--;
            nextFire = Time.time + d.fireInterval;
            if (d.kind == WeaponKind.Sniper) boltT = 1.15f;

            // aimed fire is honest: first round goes where the sight is, follow-ups bloom a little
            bool aimed = Aiming && adsT > 0.85f;
            float spread = Scoped ? 0.05f : aimed ? d.adsSpread * (Time.time - lastShotAt > 0.35f ? 0.2f : 1f) + Mathf.Max(0f, spreadNow - d.adsSpread) * 0.5f : spreadNow;
            // aimed through a holo: shoot exactly where the dot is drawn on screen (viewmodel cam has its own FOV)
            Vector3 aimDir = cam.transform.forward;
            if (aimed && s.reticle != null && vmCam != null)
            {
                Vector3 sp = vmCam.WorldToScreenPoint(s.reticle.position);
                if (sp.z > 0f && Mathf.Abs(sp.x - Screen.width * 0.5f) < Screen.width * 0.1f && Mathf.Abs(sp.y - Screen.height * 0.5f) < Screen.height * 0.1f)
                    aimDir = cam.ScreenPointToRay(sp).direction;
            }
            Vector3 dir = Quaternion.Euler(Random.Range(-spread, spread) * 0.5f, Random.Range(-spread, spread) * 0.5f, 0) * aimDir;
            Vector3 muzzleWorld = s.go.transform.TransformPoint(d.muzzlePoint);
            // no tracer while aiming: a streak from the muzzle (below the eye) to the target read as "bullets going crooked"
            if (d.pellets > 1)
            {
                // shotgun: a fixed cone of pellets (tighter when aimed), each with steep distance falloff
                float cone = aimed ? d.adsSpread : d.hipSpread;
                for (int pi = 0; pi < d.pellets; pi++)
                {
                    Vector2 r = Random.insideUnitCircle * cone * 0.5f;
                    Vector3 pd = Quaternion.Euler(r.y, r.x, 0) * aimDir;
                    Ballistics.Fire(this, cam.transform.position, pd, d.range, d.damage, muzzleWorld, pi < 3, out _, d.falloffStart, d.falloffEnd, d.falloffMin);
                }
                if (d.pumpClip != null) StartCoroutine(Delayed(0.38f, () => Sfx.Play2D(d.pumpClip, 0.55f, 0.04f)));
            }
            else Ballistics.Fire(this, cam.transform.position, dir, d.range, d.damage, muzzleWorld, !aimed && Random.value < 0.5f, out _, d.falloffStart, d.falloffEnd, d.falloffMin);

            Sfx.Play2D(d.shot, d.kind == WeaponKind.Sniper ? 1f : 0.85f, 0.05f);
            BotController.Noise(this, 60f);
            if (Random.value < 0.5f) StartCoroutine(Delayed(0.35f, () => Sfx.Play2D(Z47Refs.I.casing, 0.12f, 0.2f)));
            if (d.kind == WeaponKind.Sniper) StartCoroutine(Delayed(0.45f, () => Sfx.Play2D(Z47Refs.I.awpBolt, 0.6f)));

            // heat: long sprays kick harder, short bursts stay controllable; cools off quickly between bursts
            heat = Mathf.Min(1f, heat + (d.kind == WeaponKind.Sniper ? 0.5f : 0.09f));
            float rk = d.recoil * (aimed ? 0.75f : 1f) * (prone ? 0.6f : crouched ? 0.8f : 1f) * (1f + heat * 0.45f);
            fovKickVel += (d.kind == WeaponKind.Sniper ? 30f : d.kind == WeaponKind.Pistol ? 9f : 7f) * (aimed ? 0.5f : 1f);
            if (Time.time - lastShotAt > 0.35f) shotIdx = 0;
            shotIdx++; lastShotAt = Time.time;
            float climb = Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(shotIdx / 10f));      // first rounds climb most
            // camera: impulse into the spring (visual kick) + a permanent share the player has to pull down
            recoilPitchVel += rk * climb * 22f;
            pitch -= rk * 0.3f * climb;
            float side = Mathf.Sin(shotIdx * 0.8f) * 0.5f + Random.Range(-0.25f, 0.25f);
            recoilYawVel += rk * side * 9f; yaw += rk * side * 0.1f;
            EjectCasing(s);
            if (s.anim != null) PlayAnim(s, "fire", 0f);

            // weapon kick (position snaps, springs return it). Aimed: almost straight back so the sight stays on target.
            Vector3 kp; float kr, kroll, kyaw;
            switch (d.kind)
            {
                case WeaponKind.Sniper:
                    kp = new Vector3(0f, 0.012f, -0.1f); kr = 11f; kroll = Random.Range(-2f, 2f); kyaw = Random.Range(-1f, 1f);
                    shake = Mathf.Max(shake, 0.18f); break;
                case WeaponKind.Shotgun:
                    kp = new Vector3(0f, 0.02f, -0.11f); kr = aimed ? 7f : 13f; kroll = Random.Range(-2.5f, 2.5f); kyaw = Random.Range(-1.5f, 1.5f);
                    shake = Mathf.Max(shake, 0.16f); break;
                case WeaponKind.Pistol:
                    kp = aimed ? new Vector3(0f, 0.008f, -0.035f) : new Vector3(Random.Range(-0.003f, 0.004f), 0.014f, -0.05f);
                    kr = aimed ? Random.Range(5f, 7f) : Random.Range(12f, 16f);
                    kroll = aimed ? Random.Range(-1f, 1f) : Random.Range(2.5f, 6f) * (Random.value < 0.8f ? 1f : -1f);
                    kyaw = aimed ? Random.Range(-0.6f, 0.6f) : Random.Range(-2.5f, 2.5f);
                    if (!aimed) shake = Mathf.Max(shake, 0.1f); break;
                default:
                    float heavy = d.id == "carbine" ? 0.85f : 1f;
                    kp = (aimed ? new Vector3(Random.Range(-0.001f, 0.001f), 0.002f, -0.03f) : new Vector3(Random.Range(-0.004f, 0.004f), 0.007f, -0.05f)) * heavy;
                    kr = (aimed ? Random.Range(0.6f, 1.1f) : Random.Range(3f, 4.5f)) * heavy;
                    kroll = (aimed ? Random.Range(-0.5f, 0.5f) : Random.Range(-2f, 2f)) * heavy;
                    kyaw = (aimed ? Random.Range(-0.3f, 0.3f) : Random.Range(-1.2f, 1.2f)) * heavy;
                    shake = Mathf.Max(shake, aimed ? 0.02f : 0.05f); break;
            }
            if (s.anim != null) { kp *= 0.45f; kr *= 0.45f; }
            kickPos += kp; kickRot += kr; kickRoll += kroll; kickYaw += kyaw;
            kickPosVel += kp * 6f;          // a little follow-through so it doesn't feel like a teleport

            if (!Scoped)
            {
                flash.gameObject.SetActive(true);
                flash.localRotation = Quaternion.Euler(0, 0, Random.Range(0, 360));
                flash.localScale = Vector3.one * Random.Range(0.14f, 0.22f) * (d.kind == WeaponKind.Pistol ? 0.7f : 1f);
                flashT = 0.045f;
                if (Settings.Quality >= 1)
                {
                    var lg = new GameObject("mf");
                    lg.transform.position = muzzleWorld;
                    var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = 6f; l.intensity = 3f; l.color = new Color(1f, 0.75f, 0.4f);
                    lg.AddComponent<LightFade>().life = 0.05f;
                }
            }
            HUD.I?.Fired();
        }

        IEnumerator Delayed(float t, System.Action a) { yield return new WaitForSeconds(t); a(); }

        void ThrowGrenade()
        {
            grenades--;
            var g = Instantiate(Z47Refs.I.grenadePrefab, cam.transform.position + cam.transform.forward * 0.5f + cam.transform.right * 0.15f, Random.rotation);
            g.GetComponent<Grenade>().owner = this;
            var rb = g.GetComponent<Rigidbody>();
            rb.linearVelocity = cam.transform.forward * 16f + Vector3.up * 3.5f + cc.velocity * 0.5f;
            rb.angularVelocity = Random.insideUnitSphere * 10f;
        }

        // ------------------------------------------------------------ viewmodel
        void ViewModel()
        {
            var s = slots[cur]; var d = s.def;
            Vector3 hip = d.hipOffset;
            // aim over the sights: top of the gun sits just under the screen centre, red-dot drawn by the HUD
            // look straight down the sights: the top of the gun lines up with the screen centre
            Vector3 ads = d.adsZ > 0f ? new Vector3(0f, -d.topY, d.adsZ)
                : new Vector3(0f, -d.topY + 0.002f, d.kind == WeaponKind.Pistol ? 0.24f : d.kind == WeaponKind.Sniper ? 0.12f : 0.13f);
            Vector3 pos = Vector3.Lerp(hip, ads, adsT);
            Quaternion rot = Quaternion.identity;

            // inertial sway (lags behind the view, springs back)
            float aimK = 1f - adsT * 0.97f;
            Vector2 lk = Vector2.ClampMagnitude(PInput.look * 0.0006f, 0.035f);
            swayPos = Vector3.Lerp(swayPos, new Vector3(-lk.x, -lk.y, 0), Time.deltaTime * 9f);
            swayRot = Vector3.Lerp(swayRot, new Vector3(lk.y * 140f, -lk.x * 140f, -lk.x * 220f), Time.deltaTime * 9f);
            strafeTilt = Mathf.Lerp(strafeTilt, -PInput.move.x * 4f, Time.deltaTime * 6f);
            pos += swayPos * aimK;
            rot *= Quaternion.Euler(swayRot.x * aimK, swayRot.y * aimK, (swayRot.z + strafeTilt) * aimK);
            if (!grounded) pos += new Vector3(0, -Mathf.Clamp(velocity.y, -6f, 6f) * 0.004f, 0) * aimK;
            pos += new Vector3(0, -landDip * 0.25f, 0);
            float hs = new Vector2(cc.velocity.x, cc.velocity.z).magnitude;
            float bt = Time.time * (sprinting ? 13f : 9f);
            pos += new Vector3(Mathf.Cos(bt * 0.5f) * 0.012f, Mathf.Abs(Mathf.Sin(bt * 0.5f)) * 0.01f, 0) * Mathf.Clamp01(hs / 5f) * (1f - adsT * 0.9f);
            if (sprinting) { pos += new Vector3(-0.04f, -0.04f, -0.03f); rot *= Quaternion.Euler(10f, -28f, 12f); }
            if (crouched) pos += new Vector3(-0.01f, 0.005f, 0) * (1f - adsT);
            if (prone && !Aiming) { pos += new Vector3(0.02f, -0.03f, 0); rot *= Quaternion.Euler(0, 0, -8f); }

            // breathing: slow figure-eight drift, calmer when aiming, stronger when out of breath (sprinting)
            breathT += Time.deltaTime * (sprinting ? 2.2f : 1f);
            float bA = (sprinting ? 0.006f : 0.0028f) * (1f - adsT * 0.8f) * (prone ? 0.5f : crouched ? 0.75f : 1f);
            pos += new Vector3(Mathf.Sin(breathT * 0.9f), Mathf.Sin(breathT * 1.8f + Mathf.PI) * 0.5f, 0) * bA;

            // pull the gun back / up when standing against a wall instead of sticking it through
            float wantWall = 0f;
            if (Physics.SphereCast(cam.transform.position, 0.06f, cam.transform.forward, out var wh, 0.85f, ~0, QueryTriggerInteraction.Ignore)
                && !wh.collider.transform.IsChildOf(transform) && Actor.FromCollider(wh.collider) == null)
                wantWall = Mathf.Clamp01(1f - (wh.distance - 0.3f) / 0.5f);
            wallT = Mathf.Lerp(wallT, wantWall, Time.deltaTime * 10f);
            if (wallT > 0.01f) { pos += new Vector3(0.02f, -0.04f, -0.16f) * wallT * (1f - adsT * 0.6f); rot *= Quaternion.Euler(-28f * wallT * (1f - adsT), 12f * wallT * (1f - adsT), 0); }

            // kick
            // weapon kick springs: stiff and slightly under-damped -> crisp thump with a small settle
            float kdt = Mathf.Min(Time.deltaTime, 0.033f);
            Spring(ref kickPos.x, ref kickPosVel.x, 260f, 24f, kdt);
            Spring(ref kickPos.y, ref kickPosVel.y, 260f, 24f, kdt);
            Spring(ref kickPos.z, ref kickPosVel.z, 200f, 20f, kdt);
            Spring(ref kickRot, ref kickRotVel, 220f, 20f, kdt);
            Spring(ref kickRoll, ref kickRollVel, 180f, 18f, kdt);
            Spring(ref kickYaw, ref kickYawVel, 180f, 18f, kdt);
            pos += kickPos; rot *= Quaternion.Euler(-kickRot, kickYaw, -kickRoll);

            // reload / switch / throw
            if (reloading && s.anim == null)
            {
                float t = reloadT / s.reloadDur; float w = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                pos += new Vector3(-0.03f, -0.07f, -0.02f) * w;
                rot *= Quaternion.Euler(18f * w, 10f * w, 32f * w);
            }
            if (switchT > 0)
            {
                float w = pendingSlot >= 0 ? 1f - Mathf.Clamp01((switchT - 0.2f) / 0.25f) : Mathf.Clamp01(switchT / 0.2f);
                pos += new Vector3(0, -0.25f, 0) * w; rot *= Quaternion.Euler(30f * w, 0, 0);
            }
            if (throwT >= 0)
            {
                float w = Mathf.Sin(Mathf.Clamp01(throwT / 0.9f) * Mathf.PI);
                pos += new Vector3(0, -0.3f, 0) * w; rot *= Quaternion.Euler(40f * w, 0, 0);
            }

            var tr = s.go.transform;
            tr.localPosition = pos;
            tr.localRotation = rot;

            bool hide = Scoped;
            foreach (var r in s.rends) r.enabled = !hide;
            if (s.adsReticle != null) s.adsReticle.enabled = false;      // red dot is drawn by the HUD at the exact aim point
            if (s.anim != null) AnimTick(s);
            if (arms != null)
            {
                arms.Visible = !hide && s.anim == null;          // animated weapons bring their own arms
                float leftW = 1f;
                if (reloading) { float t = reloadT / s.reloadDur; leftW = (t > 0.2f && t < 0.7f) ? 0f : 1f; }
                if (throwT >= 0) leftW = 0f;
                arms.SetTargets(tr.TransformPoint(Vector3.zero), tr.TransformPoint(d.forePoint), tr.rotation, leftW, d.kind == WeaponKind.Pistol);
            }
        }

        // ------------------------------------------------------------ animated weapon (legacy Animation)
        void PlayAnim(Slot s, string clip, float fade)
        {
            if (s.anim == null || s.anim[clip] == null) return;
            s.anim[clip].time = 0f;
            if (fade > 0f) s.anim.CrossFade(clip, fade, PlayMode.StopAll);
            else { s.anim.Stop(); s.anim.Play(clip, PlayMode.StopAll); }
        }

        /// Between one-shot clips: a still "hold" pose as the base, with the authored breathing idle mixed in
        /// at low weight and slowed down (at full strength/speed it read as the gun shaking). Pure hold while aiming.
        void AnimTick(Slot s)
        {
            var a = s.anim;
            if (reloading) return;
            bool busy = (a.IsPlaying("fire") && a["fire"].time < a["fire"].length) || a.IsPlaying("draw") || a.IsPlaying("inspect");
            if (busy && !(Aiming && a.IsPlaying("inspect"))) return;
            if (a["hold"] == null) return;
            if (!a.IsPlaying("hold")) a.CrossFade("hold", 0.15f, PlayMode.StopAll);
            if (a["idle"] != null)
            {
                a["idle"].speed = 0.4f;
                a.Blend("idle", Aiming ? 0f : 0.35f, 0.25f);
            }
        }

        // ------------------------------------------------------------ brass
        static readonly System.Collections.Generic.Queue<GameObject> casings = new System.Collections.Generic.Queue<GameObject>();
        static Material brass;
        void EjectCasing(Slot s)
        {
            if (Settings.Quality == 0 || s.def.kind == WeaponKind.Sniper) return;
            GameObject c;
            if (casings.Count >= 24) { c = casings.Dequeue(); if (c == null) return; }
            else
            {
                c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                c.name = "casing";
                Destroy(c.GetComponent<Collider>());
                var col = c.AddComponent<CapsuleCollider>(); col.radius = 0.5f; col.height = 2f;
                if (brass == null) { brass = new Material(Z47Refs.I.barrelMat); brass.SetTexture("_BaseMap", null); brass.SetColor("_BaseColor", new Color(0.85f, 0.62f, 0.25f)); brass.SetFloat("_Metallic", 0.35f); brass.SetFloat("_Smoothness", 0.55f); }
                c.GetComponent<Renderer>().sharedMaterial = brass;
                c.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                c.transform.localScale = new Vector3(0.008f, 0.012f, 0.008f);
                var rb = c.AddComponent<Rigidbody>(); rb.mass = 0.01f;
                Physics.IgnoreCollision(col, cc);
            }
            casings.Enqueue(c);
            var wt = s.go.transform;
            c.SetActive(true);
            c.transform.position = wt.TransformPoint(new Vector3(0.025f, 0.04f, 0.06f));
            c.transform.rotation = wt.rotation * Quaternion.Euler(0, 0, 90);
            var r = c.GetComponent<Rigidbody>();
            r.linearVelocity = cc.velocity + wt.right * Random.Range(1.6f, 2.4f) + wt.up * Random.Range(1.2f, 1.8f) - wt.forward * 0.3f;
            r.angularVelocity = Random.insideUnitSphere * 25f;
        }

        void Regen()
        {
            if (hp < maxHp && Time.time - lastDamageTime > 5f) hp = Mathf.Min(maxHp, hp + 18f * Time.deltaTime);
        }

        public void Shake(float a) { shake = Mathf.Max(shake, a); }

        /// Ammo from a dropped gun: fills every carried weapon that uses the same rounds (up to 2x its starting reserve).
        public int GiveAmmo(string ammo, int amount)
        {
            int total = 0;
            if (!Alive || slots == null) return 0;
            foreach (var sl in slots)
            {
                if (amount <= 0) break;
                if (WeaponPickup.AmmoOf(sl.def.id) != ammo) continue;
                int add = Mathf.Min(amount, sl.def.reserve * 2 - sl.reserve);
                if (add <= 0) continue;
                sl.reserve += add; amount -= add; total += add;
            }
            return total;
        }

        public bool UsesAmmo(string ammo) { if (slots != null) foreach (var sl in slots) if (WeaponPickup.AmmoOf(sl.def.id) == ammo) return true; return false; }

        /// Briefly turn the view toward `t` and back (skipped while aiming / in a firefight).
        public void Glance(Transform t, float dur)
        {
            if (!Alive || Aiming || Firing || Time.time - lastShotAt < 1f || Time.time - lastDamageTime < 1f) return;
            glanceAt = t; glanceT = glanceDur = dur; glanceYaw = yaw; glancePitch = pitch;
        }

        protected override void OnHurt(Actor from, Vector3 fromPos)
        {
            HUD.I?.Damaged(fromPos);
            Shake(0.25f);
            recoilPitchVel += Random.Range(4f, 8f); recoilYawVel += Random.Range(-5f, 5f);    // flinch
        }

        protected override void OnDie(Actor k)
        {
            killer = k;
            cc.enabled = false;
            // leave the scope / ADS immediately: the death view must not stay zoomed or blurred
            HUD.I?.ResetAim(); PInput.fireHeld = false;
            adsT = 0f; fovKick = fovKickVel = 0f; cam.fieldOfView = 70f;
            if (scopeCam != null) scopeCam.enabled = false;
            if (Boot.ScopeBlur != null) Boot.ScopeBlur.weight = 0f;
            foreach (var sl in slots) sl.go.SetActive(false);
            if (arms != null) arms.Visible = false;
            Sfx.Play2D(Z47Refs.I.bodyFall, 0.8f);
            deathT = 0f;
            HUD.I?.ShowDeath(k);
        }
        float deathT;

        void DeadCam()
        {
            deathT += Time.deltaTime;
            Vector3 target = transform.position + Vector3.up * Mathf.Lerp(camH, 0.4f, Mathf.Clamp01(deathT * 2f));
            cam.transform.position = Vector3.Lerp(cam.transform.position, target, Time.deltaTime * 6f);
            if (killer != null && killer.Alive)
            {
                var q = Quaternion.LookRotation(killer.ChestPos - cam.transform.position);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, q, Time.deltaTime * 3f);
            }
        }

        public override void Respawn(Vector3 pos, Quaternion rot)
        {
            cc.enabled = false;
            transform.SetPositionAndRotation(pos, rot);
            cc.enabled = true;
            yaw = rot.eulerAngles.y; pitch = 0; velocity = Vector3.zero; crouched = false; prone = false; PInput.lean = 0; leanT = 0; heat = 0;
            cam.transform.localPosition = new Vector3(0, 1.62f, 0);
            cam.transform.localRotation = Quaternion.identity;
            hp = maxHp; grenades = 2; reloading = false; throwT = -1; switchT = 0; pendingSlot = -1;
            foreach (var sl in slots) { sl.mag = sl.def.magSize; sl.reserve = sl.def.reserve; sl.go.SetActive(false); if (sl.mag3d) sl.mag3d.gameObject.SetActive(true); }
            slots[cur].go.SetActive(true); AttachFlash();      // keep the weapon you died with
            foreach (var sl in slots) if (sl.anim != null) sl.anim.Stop();
            if (arms != null) arms.Visible = true;
            HUD.I?.HideDeath();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (Local == this) Local = null;
        }
    }

    /// First-person arms (gloved FPS arms rig). Two-bone IK puts each hand on the weapon:
    /// right hand on the pistol grip, left hand under the handguard (or cupping the pistol grip).
    public class ArmsRig : MonoBehaviour
    {
        class Arm
        {
            public Transform a, b, c, knuckle, tip;
        }
        Transform cam;
        Arm R, L;
        Transform[] bones; Quaternion[] restRot;
        Renderer[] rends;
        bool visible = true;
        Vector3 gripT, foreT; Quaternion gunRot; float leftW = 1f; bool pistol; bool hasTarget;

        public bool Visible
        {
            get => visible;
            set { if (visible == value) return; visible = value; foreach (var r in rends) r.enabled = value; }
        }

        public static ArmsRig Create(Transform cam)
        {
            var R = Z47Refs.I;
            if (R.armsPrefab == null) { Debug.LogError("[Zone47] FPS kollar yok: Z47Refs.armsPrefab bos. Zone47 > Kurulumu Yap'i tekrar calistir (Art/Arms/fp_arms.fbx olmali)."); return null; }
            var go = Instantiate(R.armsPrefab, cam);
            go.name = "FPArms";
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            var rig = go.AddComponent<ArmsRig>();
            rig.cam = cam;
            rig.rends = go.GetComponentsInChildren<Renderer>();
            if (go.GetComponentInChildren<SkinnedMeshRenderer>() == null) Debug.LogError("[Zone47] FPS kollar: SkinnedMeshRenderer yok. Zone47 > Kurulumu Yap'i tekrar calistir.");
            else Debug.Log("[Zone47] FPS kollar yuklendi (" + rig.rends.Length + " renderer)");
            foreach (var r in rig.rends) { r.shadowCastingMode = ShadowCastingMode.Off; if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true; }
            rig.R = rig.Find("R"); rig.L = rig.Find("L");
            var list = new System.Collections.Generic.List<Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>()) if (t.name.StartsWith("R_") || t.name.StartsWith("L_")) list.Add(t);
            rig.bones = list.ToArray(); rig.restRot = new Quaternion[rig.bones.Length];
            for (int i = 0; i < rig.bones.Length; i++) rig.restRot[i] = rig.bones[i].localRotation;
            if (rig.R.a == null || rig.R.b == null || rig.R.c == null || rig.L.a == null) { Debug.LogError("[Zone47] FPS kollar: kemikler bulunamadi (R_arm/R_elbow/R_wrist/L_arm)"); Destroy(go); return null; }
            return rig;
        }

        Arm Find(string s)
        {
            var root = transform;
            return new Arm
            {
                a = Util.FindDeep(root, s + "_arm"), b = Util.FindDeep(root, s + "_elbow"), c = Util.FindDeep(root, s + "_wrist"),
                knuckle = Util.FindDeep(root, s + "_middle1"), tip = Util.FindDeep(root, s + "_middle4")
            };
        }

        public void SetTargets(Vector3 grip, Vector3 fore, Quaternion rot, float lw, bool isPistol)
        {
            gripT = grip; foreT = fore; gunRot = rot; pistol = isPistol; hasTarget = true;
            leftW = Mathf.MoveTowards(leftW, lw, Time.deltaTime * 5f);
        }

        void LateUpdate()
        {
            if (!hasTarget || !visible) return;
            for (int i = 0; i < bones.Length; i++) bones[i].localRotation = restRot[i];

            Vector3 gf = gunRot * Vector3.forward, gu = gunRot * Vector3.up, gr = gunRot * Vector3.right;
            Vector3 cu = cam.up, cr = cam.right;

            // right hand wraps the pistol grip from the right side
            // palm centre sits down the pistol grip (gripT = its top-back corner), right of it; fingers wrap forward around it
            Solve(R, gripT + gr * 0.03f - gf * 0.012f - gu * 0.05f, (gf * 0.94f - gu * 0.34f).normalized, -gr, -cu + cr * 0.6f);

            // left hand: under the handguard (rifles) or cupping the grip (pistol)
            Vector3 lp, lfd, lpalm;
            if (pistol) { lp = gripT - gr * 0.03f - gu * 0.035f; lfd = (gf * 0.6f - gu * 0.5f + gr * 0.4f).normalized; lpalm = (gu * 0.4f + gr).normalized; }
            else { lp = foreT - gr * 0.025f - gu * 0.046f;   // hand wraps the left/under side of the handguard instead of sinking into it
                   lfd = (gf * 0.85f + gr * 0.5f).normalized; lpalm = (gu + gr * 0.35f).normalized; }
            Vector3 lowered = cam.TransformPoint(new Vector3(-0.16f, -0.42f, 0.12f));      // hand drops away during reload/throw
            lp = Vector3.Lerp(lowered, lp, leftW);
            Solve(L, lp, lfd, lpalm, -cu - cr * 0.8f);
        }

        void Solve(Arm arm, Vector3 palmTarget, Vector3 fingerDir, Vector3 palmDir, Vector3 pole)
        {
            // hand frame in the (rest) pose
            Vector3 w = arm.c.position, k = arm.knuckle.position, tp = arm.tip.position;
            Vector3 fd = (k - w).normalized;
            Vector3 palm = Vector3.ProjectOnPlane(tp - k, fd).normalized;
            Vector3 pc = (w + k) * 0.5f + palm * 0.018f;
            Quaternion off = Quaternion.Inverse(Quaternion.LookRotation(fd, palm)) * arm.c.rotation;
            Quaternion qt = Quaternion.LookRotation(fingerDir, palmDir) * off;
            Vector3 pcLocal = Quaternion.Inverse(arm.c.rotation) * (pc - w);
            Vector3 wt = palmTarget - qt * pcLocal;

            // analytic two-bone IK with a pole direction
            Vector3 a = arm.a.position, b = arm.b.position, c = arm.c.position;
            float lab = (b - a).magnitude, lcb = (c - b).magnitude;
            Vector3 at = wt - a;
            float lat = Mathf.Clamp(at.magnitude, 1e-4f, lab + lcb - 1e-4f);
            float d1 = (lab * lab - lcb * lcb + lat * lat) / (2f * lat);
            float h = Mathf.Sqrt(Mathf.Max(lab * lab - d1 * d1, 0f));
            Vector3 n = at.normalized;
            Vector3 pd = Vector3.ProjectOnPlane(pole, n).normalized;
            Vector3 e = a + n * d1 + pd * h;
            arm.a.rotation = Quaternion.FromToRotation(b - a, e - a) * arm.a.rotation;
            b = arm.b.position; c = arm.c.position;
            arm.b.rotation = Quaternion.FromToRotation(c - b, a + n * lat - b) * arm.b.rotation;
            arm.c.rotation = qt;
        }
    }
}
