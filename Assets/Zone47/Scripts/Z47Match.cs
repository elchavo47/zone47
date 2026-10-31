using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

namespace Zone47
{
    public class MatchManager : MonoBehaviour
    {
        public static MatchManager I;

        public const int KillTarget = 40;
        public const float MatchTime = 480f;
        public const int TeamSize = 5;

        public int[] score = new int[2];
        public float timeLeft = MatchTime;
        public bool Running { get; private set; }
        public float countdown = 3.5f;
        public readonly List<Vector3> covers = new List<Vector3>();
        public bool Finished { get; private set; }

        public Vector3[] spawnCenter = new Vector3[2];
        public Vector3 mapCenter;
        public float mapRadius = 50f;
        public readonly List<Vector3> waypoints = new List<Vector3>();
        public Transform worldRoot;

        // callsign pools: every match draws fresh, unique names (allies mostly Turkish, enemies mostly English, some mixing)
        static readonly string[] NamesTR =
        {
            "Bozkurt", "Şahin", "Kartal", "Atmaca", "Pars", "Doğan", "Yıldırım", "Kaplan", "Akbaba", "Çakal", "Sırtlan", "Kurt", "Aslan", "Toros",
            "Tufan", "Kasırga", "Fırtına", "Poyraz", "Rüzgar", "Yıldız", "Kılıç", "Hançer", "Mızrak", "Kalkan", "Zırh", "Demir", "Çelik", "Kaya",
            "Granit", "Volkan", "Lav", "Ateş", "Kor", "Alev", "Duman", "Gölge", "Hayalet", "Sis", "Bora", "Tayfun", "Şimşek", "Gök",
            "Bulut", "Turna", "Kuzgun", "Baykuş", "Puhu", "Delice", "Yaman", "Alp", "Batu", "Tolga", "Kaan", "Oğuz", "Atilla", "Mete",
            "Tarkan", "Cengiz", "Timur", "Bülent", "Serkan", "Emre", "Mert", "Kerem", "Selim", "Burak", "Onur", "Umut", "Barış", "Cesur",
            "Sarp", "Çağan", "Korkut", "Tuğrul", "Ertuğrul", "Alparslan", "Bilge", "Ayaz", "Kuzey", "Güney", "Yamaç", "Uçurum", "Vadi", "Ova",
            "Çöl", "Kum", "Toz", "Bozkır", "Pusu", "Avcı", "Nişancı", "Komando", "Akıncı", "Sipahi", "Yeniçeri", "Mehmetçik", "Paşa", "Ağa",
            "Reis", "Kaptan", "Çavuş", "Onbaşı", "Yüzbaşı", "Albay", "Dağcı", "Keşifçi", "Sızma", "Hücum", "Baskın", "Yarma", "Delik", "Mermi",
            "Şarapnel", "Fişek", "Barut", "Tetik", "Namlu", "Dipçik", "Kundak", "Sürgü", "Şarjör", "Dürbün", "Radar", "Sonar", "Pusula", "Harita",
            "Sınır", "Karakol", "Siper", "Mevzi", "Tabya", "Kale", "Burç", "Sur", "Kapı", "Köprü", "Tünel",
        };
        static readonly string[] NamesEN =
        {
            "Viper", "Ghost", "Raven", "Cobra", "Jackal", "Wolf", "Reaper", "Hyena", "Falcon", "Hawk", "Eagle", "Osprey", "Condor", "Vulture",
            "Phantom", "Specter", "Shadow", "Shade", "Wraith", "Banshee", "Nomad", "Ranger", "Hunter", "Stalker", "Tracker", "Scout", "Sniper", "Gunner",
            "Rook", "Bishop", "Knight", "Ace", "Joker", "Maverick", "Iceman", "Viking", "Spartan", "Titan", "Atlas", "Orion", "Apollo", "Hermes",
            "Ares", "Zeus", "Odin", "Thor", "Loki", "Fenrir", "Kraken", "Hydra", "Basilisk", "Wyvern", "Dragon", "Griffin", "Sphinx", "Mamba",
            "Python", "Rattler", "Scorpion", "Hornet", "Wasp", "Mantis", "Spider", "Panther", "Puma", "Jaguar", "Lynx", "Cougar", "Bobcat", "Bison",
            "Rhino", "Grizzly", "Kodiak", "Badger", "Wolverine", "Mongoose", "Coyote", "Dingo", "Fox", "Ferret", "Raptor", "Talon", "Fang", "Claw",
            "Blade", "Saber", "Dagger", "Arrow", "Bolt", "Spear", "Hammer", "Anvil", "Forge", "Steel", "Iron", "Cobalt", "Titanium", "Chrome",
            "Onyx", "Flint", "Granite", "Boulder", "Ridge", "Canyon", "Mesa", "Dune", "Sandman", "Dust", "Storm", "Thunder", "Lightning", "Blitz",
            "Tempest", "Cyclone", "Typhoon", "Hurricane", "Frost", "Blizzard", "Glacier", "Ember", "Blaze", "Inferno", "Cinder", "Ash", "Smoke", "Havoc",
            "Chaos", "Mayhem", "Fury", "Rage", "Wrath", "Vandal", "Rogue", "Outlaw", "Bandit", "Mercenary", "Hitman", "Deadeye", "Bullseye", "Trigger",
            "Recoil", "Ricochet", "Shrapnel", "Payload", "Warhead", "Rocket", "Missile", "Torpedo", "Cannon", "Mortar", "Howitzer", "Bravo", "Charlie", "Delta",
            "Echo", "Foxtrot", "Kilo", "Lima", "Sierra", "Tango", "Victor", "Zulu", "Romeo", "Hotel", "India", "Yankee", "Xray", "Nova",
            "Comet", "Meteor", "Quasar", "Pulsar", "Vortex", "Zenith", "Apex", "Summit", "Vector", "Cipher", "Enigma", "Matrix", "Proxy", "Glitch",
            "Static", "Volt", "Surge", "Spark", "Fuse", "Tank", "Bulldog", "Mastiff", "Rottweiler", "Doberman", "Husky",
        };
        public static string[] AllyNames = new string[4], EnemyNames = new string[5];
        static bool namesUsed = true;

        /// Pick a new set of bot names (lobby shows them, the next match uses the same ones).
        public static void RollNames()
        {
            var taken = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { Settings.Callsign ?? "" };
            string Pick(bool turkish)
            {
                var pool = (turkish ? Random.value < 0.8f : Random.value < 0.2f) ? NamesTR : NamesEN;
                for (int tries = 0; tries < 50; tries++) { var n = pool[Random.Range(0, pool.Length)]; if (taken.Add(n)) return n; }
                return pool[0] + Random.Range(10, 99);
            }
            if (AllyNames.Length != TeamSize - 1) AllyNames = new string[TeamSize - 1];
            if (EnemyNames.Length != TeamSize) EnemyNames = new string[TeamSize];
            for (int i = 0; i < AllyNames.Length; i++) AllyNames[i] = Pick(true);
            for (int i = 0; i < EnemyNames.Length; i++) EnemyNames[i] = Pick(false);
            namesUsed = false;
        }
        public static void EnsureNames() { if (namesUsed || AllyNames[0] == null) RollNames(); namesUsed = true; }

        public void Begin()
        {
            I = this;
            FX.ClearHoles();
            StartCoroutine(Setup());
        }

        IEnumerator Setup()
        {
            var R = Z47Refs.I;
            worldRoot = new GameObject("World").transform;
            worldRoot.SetParent(transform);
            Sfx.ClearSurfaceCache();
            Weather.ApplyMatch();
            var map = Instantiate(Settings.Map == 1 && R.portPrefab != null ? R.portPrefab : Settings.Map == 2 && R.cityPrefab != null ? R.cityPrefab : R.mapPrefab, worldRoot);
            map.name = "Map"; LoadingScreen.Progress(0.2f);
            Physics.SyncTransforms();
            yield return null;

            MeasureMap(map);
            BuildProps();
            yield return StartCoroutine(MapSnapshot()); LoadingScreen.Progress(0.45f);
            BuildBounds();
            Physics.SyncTransforms();
            yield return null;
            BakeNavMesh(); LoadingScreen.Progress(0.7f);
            yield return null;
            BuildWaypoints();
            FixSpawns(); LoadingScreen.Progress(0.85f);

            // spawn teams
            var player = PlayerController.Create(SpawnPoint(0, 0), SpawnRot(0));
            player.transform.SetParent(transform);
            for (int i = 1; i < TeamSize; i++) SpawnBot(0, i, i == 3 ? "awp" : "m4", AllyNames[(i - 1) % AllyNames.Length]);
            for (int i = 0; i < TeamSize; i++) SpawnBot(1, i, i == 2 ? "awp" : "ak", EnemyNames[i % EnemyNames.Length]);

            HUD.Create(this);
            Sfx.Music(null, 0);
            Sfx.Ambience(R.ambience, 0.35f);
            LoadingScreen.Done();
            while (LoadingScreen.Visible) yield return null;        // countdown starts once the loading screen is gone
            foreach (var a in Actor.All) a.spawnTime = Time.time + countdown;
            while (countdown > 0f) { countdown -= Time.deltaTime; yield return null; }
            countdown = 0f;
            Running = true;
            HUD.I?.ShowToastBig(Settings.Turkish ? "SAVAŞ BAŞLADI" : "FIGHT!");
        }

        void SpawnBot(int team, int idx, string weapon, string name)
        {
            var b = BotController.Create(team, SpawnPoint(team, idx), SpawnRot(team), weapon, name);
            b.transform.SetParent(transform);
        }

        void MeasureMap(GameObject map)
        {
            Bounds b = new Bounds(Vector3.zero, Vector3.zero); bool first = true;
            Renderer ground = null;
            foreach (var r in map.GetComponentsInChildren<Renderer>())
            {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                // the main ground is the biggest "ground" surface (indoor floors are also called Ground_*)
                if (r.name.ToLower().Contains("ground") && (ground == null || r.bounds.size.x * r.bounds.size.z > ground.bounds.size.x * ground.bounds.size.z)) ground = r;
            }
            var gb = ground != null ? ground.bounds : b;
            mapCenter = new Vector3(gb.center.x, 0, gb.center.z);
            mapRadius = Mathf.Min(gb.extents.x, gb.extents.z) * 0.92f;
            // spawns at opposite ends along the longer axis of the building layout
            Vector3 axis = b.extents.x >= b.extents.z ? Vector3.right : Vector3.forward;
            spawnCenter[0] = mapCenter - axis * mapRadius * 0.78f;
            spawnCenter[1] = mapCenter + axis * mapRadius * 0.78f;
        }

        void FixSpawns()
        {
            for (int t = 0; t < 2; t++)
                if (NavMesh.SamplePosition(spawnCenter[t], out var h, 12f, NavMesh.AllAreas)) spawnCenter[t] = h.position;
        }

        public Vector3 SpawnPoint(int team, int idx)
        {
            Vector3 side = Vector3.Cross((spawnCenter[1 - team] - spawnCenter[team]).normalized, Vector3.up);
            for (int tries = 0; tries < 24; tries++)
            {
                float spread = tries < 12 ? 1f : 2f;
                Vector3 p = spawnCenter[team] + side * ((idx - 2) * 2.2f + Random.Range(-1.5f, 1.5f)) * spread + Random.insideUnitSphere.Flat() * 2.5f * spread;
                if (!NavMesh.SamplePosition(p, out var h, 4f, NavMesh.AllAreas)) continue;
                // must be on the ground (not a container roof) and free of any solid object
                if (h.position.y > spawnCenter[team].y + 1f) continue;
                Vector3 q = h.position;
                if (Physics.CheckCapsule(q + Vector3.up * 0.65f, q + Vector3.up * 1.6f, 0.38f, ~0, QueryTriggerInteraction.Ignore)) continue;
                return q;
            }
            return spawnCenter[team] + Vector3.up * 0.1f;
        }

        public Quaternion SpawnRot(int team)
        {
            Vector3 d = (spawnCenter[1 - team] - spawnCenter[team]).Flat().normalized;
            return Quaternion.LookRotation(d == Vector3.zero ? Vector3.forward : d);
        }

        // ------------------------------------------------------------- props / cover
        void BuildProps()
        {
            var R = Z47Refs.I;
            var rnd = new System.Random(4747);
            var props = new GameObject("Props").transform; props.SetParent(worldRoot);
            int placed = 0;
            for (int tries = 0; tries < 900 && placed < 46; tries++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rad = Mathf.Sqrt((float)rnd.NextDouble()) * mapRadius * 0.9f;
                Vector3 p = mapCenter + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * rad;
                // keep spawns clear
                if ((p - spawnCenter[0]).Flat().magnitude < 7f || (p - spawnCenter[1]).Flat().magnitude < 7f) continue;
                if (!Physics.Raycast(p + Vector3.up * 40f, Vector3.down, out var hit, 80f)) continue;
                string hn = hit.collider.name.ToLower();
                if (!(hn.Contains("ground") || hn.Contains("road") || hn.Contains("circle") || hn.Contains("plane"))) continue;
                if (hit.point.y > 0.6f) continue;
                int kind = rnd.Next(5);
                Vector3 size; Material mat; PrimitiveType prim = PrimitiveType.Cube;
                switch (kind)
                {
                    case 0: size = new Vector3(1.2f, 1.2f, 1.2f); mat = R.crateMat; break;
                    case 1: size = new Vector3(2.8f, 1.05f, 0.55f); mat = R.concreteMat; break;
                    case 2: size = new Vector3(3.2f, 0.95f, 0.8f); mat = R.sandbagMat; break;
                    case 3: size = new Vector3(0.7f, 0.95f, 0.7f); mat = R.barrelMat; prim = PrimitiveType.Cylinder; break;
                    default: size = new Vector3(2.4f, 2.4f, 1.2f); mat = R.crateMat; break;
                }
                float yaw = (float)rnd.NextDouble() * 360f;
                var rot = Quaternion.Euler(0, yaw, 0);
                Vector3 center = hit.point + Vector3.up * (size.y * 0.5f + 0.02f);
                if (Physics.CheckBox(center + Vector3.up * 0.05f, size * 0.5f + new Vector3(0.6f, -0.05f, 0.6f), rot, ~0, QueryTriggerInteraction.Ignore)) continue;
                var go = GameObject.CreatePrimitive(prim);
                go.name = "Cover";
                go.transform.SetParent(props);
                go.transform.SetPositionAndRotation(center, rot);
                go.transform.localScale = prim == PrimitiveType.Cylinder ? new Vector3(size.x, size.y * 0.5f, size.z) : size;
                go.GetComponent<Renderer>().sharedMaterial = mat;
                if (size.y >= 0.9f) covers.Add(center);
                if (prim == PrimitiveType.Cylinder)
                {
                    // barrels come in clusters
                    for (int k = 0; k < 2; k++)
                    {
                        var b2 = Instantiate(go, props);
                        b2.transform.position = center + rot * new Vector3(k == 0 ? 0.78f : 0.36f, 0, k == 0 ? 0 : 0.7f);
                    }
                }
                placed++;
            }
        }

        public RenderTexture mapTex;
        public float mapHalf;
        /// Renders the map once from above; the HUD minimap scrolls/rotates this picture.
        IEnumerator MapSnapshot()
        {
            mapHalf = mapRadius / 0.92f + 4f;
            mapTex = new RenderTexture(768, 768, 24) { name = "MinimapTex" };
            var g = new GameObject("MapSnapCam");
            g.transform.SetPositionAndRotation(new Vector3(mapCenter.x, 120f, mapCenter.z), Quaternion.Euler(90f, 0f, 0f));
            var c = g.AddComponent<Camera>();
            c.orthographic = true; c.orthographicSize = mapHalf; c.aspect = 1f;
            c.nearClipPlane = 1f; c.farClipPlane = 250f;
            c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.08f, 0.08f, 0.07f);
            c.targetTexture = mapTex;
            var d = c.GetUniversalAdditionalCameraData(); d.renderPostProcessing = false;
            bool fog = RenderSettings.fog; RenderSettings.fog = false;
            yield return null; yield return null;
            RenderSettings.fog = fog;
            c.targetTexture = null;
            Destroy(g);
        }

        void BuildBounds()
        {
            var wall = new GameObject("Bounds").transform; wall.SetParent(worldRoot);
            int n = 36;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var g = new GameObject("w");
                g.transform.SetParent(wall);
                g.transform.position = mapCenter + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (mapRadius + 0.5f) + Vector3.up * 5f;
                g.transform.rotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
                var bc = g.AddComponent<BoxCollider>();
                bc.size = new Vector3(2f * Mathf.PI * mapRadius / n + 2f, 12f, 1f);
            }
        }

        NavMeshDataInstance navInst;
        void BakeNavMesh()
        {
            var sources = new List<NavMeshBuildSource>();
            var bounds = new Bounds(mapCenter, new Vector3(mapRadius * 2.4f, 40f, mapRadius * 2.4f));
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            var st = NavMesh.GetSettingsByID(0);
            st.agentRadius = 0.4f; st.agentHeight = 1.8f; st.agentClimb = 0.45f; st.agentSlope = 40f;
            var data = NavMeshBuilder.BuildNavMeshData(st, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data != null) navInst = NavMesh.AddNavMeshData(data);
        }

        void BuildWaypoints()
        {
            waypoints.Clear();
            for (int i = 0; i < 400 && waypoints.Count < 70; i++)
            {
                Vector3 p = mapCenter + Random.insideUnitSphere.Flat() * mapRadius * 0.9f;
                if (NavMesh.SamplePosition(p, out var h, 3f, NavMesh.AllAreas)) waypoints.Add(h.position);
            }
            if (waypoints.Count == 0) waypoints.Add(mapCenter);
        }

        void OnDestroy()
        {
            if (mapTex != null) mapTex.Release();
            if (navInst.valid) NavMesh.RemoveNavMeshData(navInst);
            if (I == this) I = null;
        }

        // ------------------------------------------------------------- gameplay
        void Update()
        {
            if (!Running || Finished) return;
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f) { timeLeft = 0f; End(); }
        }

        public void OnKill(Actor killer, Actor victim, bool headshot)
        {
            int scoringTeam = killer != null && killer != victim ? killer.team : 1 - victim.team;
            score[scoringTeam]++;
            HUD.I?.KillFeed(killer, victim, headshot);
            StartCoroutine(RespawnLater(victim));
            if (score[scoringTeam] >= KillTarget) End();
        }

        IEnumerator RespawnLater(Actor a)
        {
            float delay = a.isPlayer ? 4f : 5f; a.respawnAt = Time.time + delay;
            yield return new WaitForSeconds(delay);
            if (Finished || a == null) yield break;
            // pick the spawn slot furthest from enemies
            Vector3 best = SpawnPoint(a.team, Random.Range(0, TeamSize)); float bestScore = -1;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = SpawnPoint(a.team, Random.Range(0, TeamSize));
                float m = 999f;
                foreach (var o in Actor.All) if (o.team != a.team && o.Alive) m = Mathf.Min(m, Vector3.Distance(o.ChestPos, p));
                if (m > bestScore) { bestScore = m; best = p; }
            }
            a.Respawn(best, SpawnRot(a.team));
            a.spawnTime = Time.time;
        }

        void End()
        {
            if (Finished) return;
            Finished = true; Running = false;
            Sfx.Ambience(null, 0);
            HUD.I?.ShowEnd(score[0] > score[1] ? 1 : score[0] < score[1] ? -1 : 0);
        }

        /// Gunshots/explosions alert bots in hearing range.
        public void Noise(Vector3 p, Actor src)
        {
            if (src == null) return;
            foreach (var a in Actor.All)
                if (a is BotController b && b.team != src.team && b.Alive && Vector3.Distance(b.ChestPos, p) < 45f)
                    b.Hear(src);
        }
    }

    public static class VecExt
    {
        public static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
