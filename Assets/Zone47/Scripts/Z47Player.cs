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
        public static bool jump, crouchToggle, reload, grenade, next;
        public static int select = -1;
        public static void ResetEdges() { jump = crouchToggle = reload = grenade = next = false; select = -1; look = Vector2.zero; }
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
        float recoilPitch, recoilYaw;
        float landDip;

        // weapons
        class Slot { public WeaponDef def; public GameObject go; public int mag, reserve; public Transform mag3d; }
        Slot[] slots;
        int cur;
        float nextFire;
        bool reloading; float reloadT;
        float switchT;        // >0 while lowering/raising
        int pendingSlot = -1;
        float boltT;
        public int grenades = 2;
        float throwT = -1f;
        Vector3 kickPos; float kickRot;
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
        public int Mag => slots[cur].mag;
        public int Reserve => slots[cur].reserve;
        public bool Reloading => reloading;
        public bool Scoped => Aiming && Cur.scope && adsT > 0.85f;

        public override Vector3 EyePos => cam != null ? cam.transform.position : transform.position + Vector3.up * 1.6f;
        public override Vector3 ChestPos => transform.position + Vector3.up * (crouched ? 0.8f : 1.25f);
        public override float HeadHeight => EyePos.y - 0.2f;

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
            isPlayer = true; team = 0; callsign = L.S("you");
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
            data.antialiasing = Settings.Quality >= 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.FastApproximateAntialiasing;

            viewRoot = new GameObject("ViewRoot").transform;
            viewRoot.SetParent(camGo.transform, false);

            var R = Z47Refs.I;
            string[] ids = { "m4", "awp", "glock" };
            slots = new Slot[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                var d = R.Weapon(ids[i]);
                var w = Instantiate(d.prefab, viewRoot);
                w.name = "VM_" + d.id;
                Util.SetShadows(w, ShadowCastingMode.Off);
                foreach (var c in w.GetComponentsInChildren<Collider>()) Destroy(c);
                w.SetActive(i == 0);
                slots[i] = new Slot { def = d, go = w, mag = d.magSize, reserve = d.reserve, mag3d = Util.FindDeep(w.transform, "Mag") };
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
                if (k.rKey.wasPressedThisFrame) PInput.reload = true;
                if (k.gKey.wasPressedThisFrame) PInput.grenade = true;
                if (k.digit1Key.wasPressedThisFrame) PInput.select = 0;
                if (k.digit2Key.wasPressedThisFrame) PInput.select = 1;
                if (k.digit3Key.wasPressedThisFrame) PInput.select = 2;
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
            yaw += d.x;
            pitch = Mathf.Clamp(pitch - d.y, -80f, 80f);

            recoilPitch = Mathf.Lerp(recoilPitch, 0, Time.deltaTime * 9f);
            recoilYaw = Mathf.Lerp(recoilYaw, 0, Time.deltaTime * 9f);
            transform.rotation = Quaternion.Euler(0, yaw, 0);

            shake = Mathf.MoveTowards(shake, 0, Time.deltaTime * 2.2f);
            Vector3 sh = shake > 0 ? new Vector3(Mathf.PerlinNoise(Time.time * 25f, 0) - 0.5f, Mathf.PerlinNoise(0, Time.time * 25f) - 0.5f, 0) * shake * 6f : Vector3.zero;
            cam.transform.localRotation = Quaternion.Euler(pitch - recoilPitch + sh.y, recoilYaw + sh.x, 0);

            float fovT = Cur.scope ? 18f : Cur.adsFov;
            adsT = Mathf.MoveTowards(adsT, Aiming ? 1f : 0f, Time.deltaTime * 6f);
            cam.fieldOfView = Mathf.Lerp(70f, fovT, adsT);
            if (vmCam != null) vmCam.fieldOfView = Mathf.Lerp(56f, 44f, adsT);
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
            if (PInput.crouchToggle) crouched = !crouched;
            Vector2 mv = Vector2.ClampMagnitude(PInput.move, 1f);
            bool sprint = mv.y > 0.85f && Mathf.Abs(mv.x) < 0.5f && !Aiming && !Firing && !reloading;
            if (sprint && crouched) crouched = false;
            float speed = crouched ? 2.2f : Aiming ? 2.7f : sprint ? 6.4f : 4.3f;
            Vector3 wish = transform.TransformDirection(new Vector3(mv.x, 0, mv.y)) * speed;

            grounded = cc.isGrounded;
            if (grounded && velocity.y < 0)
            {
                if (velocity.y < -6f) { landDip = Mathf.Clamp(-velocity.y * 0.02f, 0, 0.18f); Sfx.Play2D(Z47Refs.I.steps[0], 0.6f); }
                velocity.y = -2f;
            }
            float accel = grounded ? 14f : 3f;
            velocity.x = Mathf.Lerp(velocity.x, wish.x, Time.deltaTime * accel);
            velocity.z = Mathf.Lerp(velocity.z, wish.z, Time.deltaTime * accel);
            if (PInput.jump && grounded) { velocity.y = Mathf.Sqrt(2f * 9.81f * 1.05f); crouched = false; }
            velocity.y -= 9.81f * 1.6f * Time.deltaTime;
            cc.Move(velocity * Time.deltaTime);

            float targetH = crouched ? 1.05f : 1.62f;
            cc.height = Mathf.Lerp(cc.height, crouched ? 1.2f : 1.8f, Time.deltaTime * 10f);
            cc.center = new Vector3(0, cc.height * 0.5f, 0);
            camH = Mathf.Lerp(camH, targetH, Time.deltaTime * 10f);
            landDip = Mathf.Lerp(landDip, 0, Time.deltaTime * 8f);

            float hs = new Vector2(cc.velocity.x, cc.velocity.z).magnitude;
            float bob = grounded ? Mathf.Sin(Time.time * (sprint ? 13f : 9f)) * Mathf.Clamp01(hs / 6f) * 0.03f * (1f - adsT * 0.8f) : 0f;
            cam.transform.localPosition = new Vector3(0, camH + bob - landDip, 0);

            if (grounded && hs > 0.5f)
            {
                stepDist += hs * Time.deltaTime;
                float stride = sprint ? 2.6f : crouched ? 1.5f : 2.0f;
                if (stepDist > stride)
                {
                    stepDist = 0;
                    var st = Z47Refs.I.steps;
                    Sfx.Play2D(st[Random.Range(0, st.Length)], crouched ? 0.18f : sprint ? 0.5f : 0.33f, 0.1f);
                }
            }
            sprinting = sprint && hs > 3f;
        }
        bool sprinting;

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

            // reload
            if ((PInput.reload || (s.mag == 0 && Firing && s.reserve > 0)) && !reloading && s.mag < d.magSize && s.reserve > 0)
            {
                reloading = true; reloadT = 0f;
                Sfx.Play2D(d.kind == WeaponKind.Sniper ? Z47Refs.I.awpBolt : Z47Refs.I.reload, 0.8f, 0.02f);
            }
            if (reloading)
            {
                reloadT += Time.deltaTime;
                if (s.mag3d != null) s.mag3d.gameObject.SetActive(reloadT < d.reloadTime * 0.3f || reloadT > d.reloadTime * 0.55f);
                if (reloadT >= d.reloadTime)
                {
                    int need = d.magSize - s.mag; int take = Mathf.Min(need, s.reserve);
                    s.mag += take; s.reserve -= take; reloading = false;
                    if (s.mag3d != null) s.mag3d.gameObject.SetActive(true);
                }
            }

            boltT -= Time.deltaTime;
            bool down = Firing && !sprinting;
            float baseSpread = Mathf.Lerp(d.hipSpread, d.adsSpread, adsT) * (crouched ? 0.75f : 1f);
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
        }

        void Shoot(Slot s)
        {
            var d = s.def;
            s.mag--;
            nextFire = Time.time + d.fireInterval;
            if (d.kind == WeaponKind.Sniper) boltT = 1.15f;

            float spread = Scoped ? 0.05f : spreadNow;
            Vector3 dir = Quaternion.Euler(Random.Range(-spread, spread) * 0.5f, Random.Range(-spread, spread) * 0.5f, 0) * cam.transform.forward;
            Vector3 muzzleWorld = s.go.transform.TransformPoint(d.muzzlePoint);
            Ballistics.Fire(this, cam.transform.position, dir, d.range, d.damage, muzzleWorld, !Scoped && Random.value < 0.6f, out _);

            Sfx.Play2D(d.shot, d.kind == WeaponKind.Sniper ? 1f : 0.85f, 0.05f);
            if (Random.value < 0.5f) StartCoroutine(Delayed(0.35f, () => Sfx.Play2D(Z47Refs.I.casing, 0.12f, 0.2f)));
            if (d.kind == WeaponKind.Sniper) StartCoroutine(Delayed(0.45f, () => Sfx.Play2D(Z47Refs.I.awpBolt, 0.6f)));

            float rk = d.recoil * (Aiming ? 0.6f : 1f) * (crouched ? 0.8f : 1f);
            if (Time.time - lastShotAt > 0.35f) shotIdx = 0;
            shotIdx++; lastShotAt = Time.time;
            float climb = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(shotIdx / 12f));      // first shots climb most
            recoilPitch += rk * climb; pitch -= rk * 0.32f * climb;
            float side = Mathf.Sin(shotIdx * 0.75f) * 0.45f + Random.Range(-0.15f, 0.15f); // readable S-pattern
            recoilYaw += rk * side; yaw += rk * side * 0.12f;
            EjectCasing(s);
            kickPos += new Vector3(0, 0.004f, -0.035f) * (d.kind == WeaponKind.Sniper ? 2f : 1f);
            kickRot += d.kind == WeaponKind.Sniper ? 9f : 3.5f;

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
            Vector3 ads = new Vector3(0f, -d.topY + 0.002f, d.kind == WeaponKind.Pistol ? 0.24f : d.kind == WeaponKind.Sniper ? 0.12f : 0.13f);
            Vector3 pos = Vector3.Lerp(hip, ads, adsT);
            Quaternion rot = Quaternion.identity;

            // inertial sway (lags behind the view, springs back)
            float aimK = 1f - adsT * 0.85f;
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
            if (crouched) pos += new Vector3(-0.01f, 0.005f, 0);

            // kick
            kickPos = Vector3.Lerp(kickPos, Vector3.zero, Time.deltaTime * 14f);
            kickRot = Mathf.Lerp(kickRot, 0, Time.deltaTime * 12f);
            pos += kickPos; rot *= Quaternion.Euler(-kickRot, 0, 0);

            // reload / switch / throw
            if (reloading)
            {
                float t = reloadT / d.reloadTime; float w = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
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
            foreach (var r in s.go.GetComponentsInChildren<Renderer>()) r.enabled = !hide;
            if (arms != null)
            {
                arms.Visible = !hide;
                float leftW = 1f;
                if (reloading) { float t = reloadT / d.reloadTime; leftW = (t > 0.2f && t < 0.7f) ? 0f : 1f; }
                if (throwT >= 0) leftW = 0f;
                arms.SetTargets(tr.TransformPoint(Vector3.zero), tr.TransformPoint(d.forePoint), tr.rotation, leftW);
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

        protected override void OnHurt(Actor from, Vector3 fromPos)
        {
            HUD.I?.Damaged(fromPos);
            Shake(0.25f);
        }

        protected override void OnDie(Actor k)
        {
            killer = k;
            cc.enabled = false;
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
            yaw = rot.eulerAngles.y; pitch = 0; velocity = Vector3.zero; crouched = false;
            cam.transform.localPosition = new Vector3(0, 1.62f, 0);
            cam.transform.localRotation = Quaternion.identity;
            hp = maxHp; grenades = 2; reloading = false; throwT = -1; switchT = 0; pendingSlot = -1;
            foreach (var sl in slots) { sl.mag = sl.def.magSize; sl.reserve = sl.def.reserve; sl.go.SetActive(false); if (sl.mag3d) sl.mag3d.gameObject.SetActive(true); }
            cur = 0; slots[cur].go.SetActive(true); AttachFlash();
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

    /// First-person arms: a copy of the soldier model whose hands are IK-pinned to the weapon.
    public class ArmsRig : MonoBehaviour
    {
        Animator anim;
        Transform cam;
        Vector3 rightT, leftT; Quaternion gunRot; float leftW = 1f;
        Renderer[] rends;
        bool visible = true;
        Transform head, lLeg, rLeg;

        public bool Visible
        {
            get => visible;
            set { if (visible == value) return; visible = value; foreach (var r in rends) r.enabled = value; }
        }

        public static ArmsRig Create(Transform cam)
        {
            var R = Z47Refs.I;
            var go = Instantiate(R.swatModel);
            go.name = "Arms";
            var rig = go.AddComponent<ArmsRig>();
            rig.cam = cam;
            rig.anim = go.GetComponent<Animator>();
            if (rig.anim == null) rig.anim = go.AddComponent<Animator>();
            rig.anim.runtimeAnimatorController = R.armsController;
            rig.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            rig.anim.applyRootMotion = false;
            rig.rends = go.GetComponentsInChildren<Renderer>();
            foreach (var r in rig.rends) { r.shadowCastingMode = ShadowCastingMode.Off; if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true; }
            rig.head = rig.anim.GetBoneTransform(HumanBodyBones.Head);
            rig.lLeg = rig.anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rig.rLeg = rig.anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            go.transform.SetParent(cam.parent, true); // under the player
            return rig;
        }

        public void SetTargets(Vector3 grip, Vector3 fore, Quaternion rot, float lw)
        {
            rightT = grip; leftT = fore; gunRot = rot; leftW = Mathf.MoveTowards(leftW, lw, Time.deltaTime * 6f);
            // place the body so its eyes sit at the camera
            transform.rotation = cam.rotation;
            transform.position = cam.position - cam.rotation * new Vector3(0.0f, 1.6f, -0.06f);
        }

        void OnAnimatorIK(int layer)
        {
            if (anim == null) return;
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
            anim.SetIKPosition(AvatarIKGoal.RightHand, rightT + gunRot * new Vector3(0.012f, -0.02f, -0.02f));
            anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            anim.SetIKRotation(AvatarIKGoal.RightHand, gunRot * Quaternion.Euler(0, 0, -90f));
            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftW);
            anim.SetIKPosition(AvatarIKGoal.LeftHand, leftT + gunRot * new Vector3(-0.015f, -0.03f, 0f));
            anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0.6f);
            anim.SetIKHintPosition(AvatarIKHint.RightElbow, cam.position + cam.rotation * new Vector3(0.35f, -0.45f, -0.05f));
            anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0.6f * leftW);
            anim.SetIKHintPosition(AvatarIKHint.LeftElbow, cam.position + cam.rotation * new Vector3(-0.3f, -0.5f, 0.1f));
        }

        void LateUpdate()
        {
            if (head != null) head.localScale = Vector3.one * 0.001f;
            if (lLeg != null) lLeg.localScale = Vector3.one * 0.001f;
            if (rLeg != null) rLeg.localScale = Vector3.one * 0.001f;
        }
    }
}
