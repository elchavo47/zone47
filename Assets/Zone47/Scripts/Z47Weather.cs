using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Zone47
{
    /// Per-match weather / time of day. 0 = random (picked each match), 1 noon, 2 foggy morning, 3 sandstorm (desert only).
    /// Lobby uses the baseline look (-1).
    public static class Weather
    {
        public const int Random = 0, Noon = 1, Fog = 2, Sandstorm = 3;
        public static int Current { get; private set; } = -1;

        /// How far bots can spot someone (fog/dust hide you from them too, not only from the player).
        public static float SightRange => Current == Fog ? 48f : Current == Sandstorm ? 42f : 85f;
        public static bool Valid(int w, int map) => w != Sandstorm || map == 0;

        static Material sky;
        static GameObject dust;

        public static int Resolve()
        {
            int w = Settings.Weather;
            if (w == Random || !Valid(w, Settings.Map))
            {
                int n = Settings.Map == 0 ? 3 : 2;
                w = 1 + UnityEngine.Random.Range(0, n);
            }
            return w;
        }

        public static int Pending = -1;
        public static void PrepareMatch() { Pending = Resolve(); }
        public static void ApplyMatch() { Apply(Pending > 0 ? Pending : Resolve()); Pending = -1; }
        public static void ApplyLobby() { Apply(-1); }

        public static void Apply(int w)
        {
            Current = w;
            var sun = Boot.Sun; var fill = Boot.Fill; var ca = Boot.Exposure;
            if (sky == null && Z47Refs.I.skybox != null) sky = new Material(Z47Refs.I.skybox);
            if (sky != null) RenderSettings.skybox = sky;

            // values: sun colour, intensity, pitch, shadow strength | ambient sky/equator/ground | fog colour, density | sky tint, thickness, exposure | colour filter, saturation
            switch (w)
            {
                case Noon:
                    Set(sun, new Color(1f, 0.97f, 0.92f), 1.75f, 56f, 0.9f);
                    Fill(fill, new Color(0.62f, 0.72f, 0.95f), 0.18f);
                    Amb(new Color(0.5f, 0.58f, 0.7f), new Color(0.46f, 0.48f, 0.5f), new Color(0.2f, 0.19f, 0.18f));
                    FogSet(new Color(0.7f, 0.78f, 0.88f), 0.0035f);
                    Sky(new Color(0.45f, 0.5f, 0.58f), 0.9f, 1.3f);
                    Grade(ca, new Color(1f, 1f, 1f), 0f, 12f);
                    break;
                case Fog:
                    Set(sun, new Color(0.85f, 0.9f, 1f), 0.8f, 18f, 0.45f);
                    Fill(fill, new Color(0.7f, 0.78f, 0.9f), 0.28f);
                    Amb(new Color(0.52f, 0.58f, 0.66f), new Color(0.45f, 0.5f, 0.55f), new Color(0.22f, 0.24f, 0.26f));
                    FogSet(new Color(0.63f, 0.69f, 0.75f), 0.03f);
                    Sky(new Color(0.55f, 0.6f, 0.68f), 1.6f, 0.9f);
                    Grade(ca, new Color(0.93f, 0.97f, 1.03f), -18f, 8f);
                    break;
                case Sandstorm:
                    Set(sun, new Color(1f, 0.74f, 0.48f), 0.95f, 24f, 0.45f);
                    Fill(fill, new Color(0.9f, 0.7f, 0.5f), 0.2f);
                    Amb(new Color(0.64f, 0.5f, 0.36f), new Color(0.56f, 0.43f, 0.3f), new Color(0.26f, 0.2f, 0.15f));
                    FogSet(new Color(0.74f, 0.54f, 0.34f), 0.026f);
                    Sky(new Color(0.75f, 0.55f, 0.38f), 2.4f, 1f);
                    Grade(ca, new Color(1f, 0.92f, 0.8f), -12f, 10f);
                    break;
                default: // lobby / baseline (original look)
                    Set(sun, new Color(1f, 0.86f, 0.7f), 1.55f, 32f, 0.85f);
                    Fill(fill, new Color(0.7f, 0.76f, 0.9f), 0.12f);
                    Amb(new Color(0.55f, 0.5f, 0.47f), new Color(0.45f, 0.38f, 0.32f), new Color(0.2f, 0.18f, 0.16f));
                    FogSet(new Color(0.62f, 0.52f, 0.43f), 0.009f);
                    Sky(new Color(0.62f, 0.55f, 0.5f), 1.35f, 1.15f);
                    Grade(ca, new Color(1f, 0.95f, 0.88f), -8f, 14f);
                    break;
            }
            if (dust != null) { Object.Destroy(dust); dust = null; }
        }

        /// Called for the player camera: thick weather hides the sky (fog colour behind everything) and adds blowing dust.
        public static void SetupCamera(Camera cam)
        {
            if (cam == null) return;
            bool thick = Current == Fog || Current == Sandstorm;
            cam.clearFlags = thick ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            cam.backgroundColor = RenderSettings.fogColor;
            if (thick) cam.farClipPlane = Current == Fog ? 150f : 130f;     // nothing visible past the fog anyway: cheaper
            if (Current == Sandstorm && dust == null) dust = MakeDust(cam.transform);
        }

        static GameObject MakeDust(Transform cam)
        {
            var R = Z47Refs.I;
            if (R.smokeMat == null) return null;
            var go = new GameObject("Sandstorm");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(-6f, 0f, 8f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.duration = 5f; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f); main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = new Color(0.78f, 0.58f, 0.38f, Settings.Quality == 0 ? 0.1f : 0.14f);
            main.maxParticles = Settings.Quality == 0 ? 60 : 140;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = Settings.Quality == 0 ? 18f : 40f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(4f, 6f, 22f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(7f, 11f); vel.y = new ParticleSystem.MinMaxCurve(-0.4f, 0.6f); vel.z = new ParticleSystem.MinMaxCurve(1f, 3f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = R.smokeMat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            return go;
        }

        static void Set(Light l, Color c, float i, float pitch, float shadow)
        {
            if (l == null) return;
            l.color = c; l.intensity = i; l.shadowStrength = shadow;
            l.transform.rotation = Quaternion.Euler(pitch, -38f, 0f);
        }
        static void Fill(Light l, Color c, float i) { if (l == null) return; l.color = c; l.intensity = i; }
        static void Amb(Color s, Color e, Color g) { RenderSettings.ambientSkyColor = s; RenderSettings.ambientEquatorColor = e; RenderSettings.ambientGroundColor = g; }
        static void FogSet(Color c, float d) { RenderSettings.fogColor = c; RenderSettings.fogDensity = d; }
        static void Sky(Color tint, float thick, float exp)
        {
            if (sky == null) return;
            sky.SetColor("_SkyTint", tint); sky.SetFloat("_AtmosphereThickness", thick); sky.SetFloat("_Exposure", exp);
        }
        static void Grade(ColorAdjustments ca, Color filter, float sat, float contrast)
        {
            if (ca == null) return;
            ca.colorFilter.Override(filter); ca.saturation.Override(sat); ca.contrast.Override(contrast);
        }
    }
}
