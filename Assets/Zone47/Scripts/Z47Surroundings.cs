using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zone47
{
    /// Fills the empty space around the map: a big ground disk, hills and distant mountains, and clusters of trees, bushes and rocks
    /// that sit OUTSIDE the playable bounds. All procedural (no extra assets): one combined mesh per material, so it costs ~6 draw calls.
    public static class Surroundings
    {
        public static void Build(Transform parent, Bounds mb, float groundY, Material proto, Material groundMat, int seed)
        {
            if (proto == null) return;
            var rng = new System.Random(seed * 7919 + 13);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var root = new GameObject("Surroundings").transform; root.SetParent(parent, false);
            bool shadows = Settings.Quality >= 1;

            Material Mat(Color c, float smooth = 0.04f)
            {
                var m = new Material(proto) { color = c };
                m.mainTexture = null;
                if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", null);
                m.DisableKeyword("_NORMALMAP"); m.DisableKeyword("_EMISSION");
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                return m;
            }
            Material Tex(Color tint, float tile)
            {
                if (groundMat == null) return Mat(tint);
                var m = new Material(groundMat);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint); else m.color = tint;
                m.mainTextureScale = new Vector2(tile, tile);
                if (m.HasProperty("_BumpMap")) m.SetTextureScale("_BumpMap", new Vector2(tile, tile));
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);       // no sun glints on the far ground
                if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                return m;
            }
            void Emit(string name, Mesh mesh, Material mat, bool cast)
            {
                var go = new GameObject(name); go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
                mr.shadowCastingMode = cast && shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                mr.receiveShadows = true;
            }

            Vector3 c = new Vector3(mb.center.x, groundY, mb.center.z);
            float ex = mb.extents.x + 30f, ez = mb.extents.z + 30f;       // town + a margin
            // distance from the map centre to the map edge along a direction (rectangle), so shapes follow any layout
            float Edge(float dx, float dz)
            {
                float tx = Mathf.Abs(dx) < 1e-4f ? 1e9f : ex / Mathf.Abs(dx), tz = Mathf.Abs(dz) < 1e-4f ? 1e9f : ez / Mathf.Abs(dz);
                return Mathf.Min(tx, tz);
            }

            // ---- terrain: one height field around the map (flat at the map edge, dunes, rocky foothills, tall far mountains)
            const float RAD = 720f; const float CELL = 6f;
            float Fbm(float x, float z, float f, int oct, float seedOff)
            {
                float sum = 0f, amp = 1f, tot = 0f;
                for (int o = 0; o < oct; o++) { sum += Mathf.PerlinNoise(x * f + seedOff + o * 17.3f, z * f + seedOff * 0.7f - o * 9.1f) * amp; tot += amp; amp *= 0.5f; f *= 2.03f; }
                return sum / tot;
            }
            float so = (seed * 31 % 97) + 3.7f;
            float Dist(float x, float z)            // metres outside the map rectangle (0 inside)
            {
                float dx = Mathf.Max(Mathf.Abs(x - c.x) - ex, 0f), dz = Mathf.Max(Mathf.Abs(z - c.z) - ez, 0f);
                return Mathf.Sqrt(dx * dx + dz * dz);
            }
            float H(float x, float z)
            {
                float d = Dist(x, z);
                float m0 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 5f) / 60f));          // blends out of the flat map floor
                float dunes = (Fbm(x, z, 0.012f, 3, so) - 0.45f) * 22f + (Fbm(x, z, 0.06f, 2, so + 40f) - 0.5f) * 2.2f;
                float foot = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 70f) / 140f));
                float ridge = 1f - Mathf.Abs(Fbm(x, z, 0.0042f, 4, so + 80f) * 2f - 1f);        // ridged noise = sharp crests
                ridge = ridge * ridge;
                float big = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 150f) / 220f));
                float rock = Fbm(x, z, 0.03f, 3, so + 120f) * 18f * foot;
                float h = m0 * (dunes + 5f) + foot * 14f * Fbm(x, z, 0.008f, 3, so + 200f) + rock + big * (25f + 150f * ridge) * (0.6f + 0.8f * Fbm(x, z, 0.0025f, 2, so + 300f));
                float r = Mathf.Sqrt((x - c.x) * (x - c.x) + (z - c.z) * (z - c.z));
                h *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 560f) / 140f));       // settle back to the horizon floor
                return Mathf.Max(h, 0f) * (d < 5f ? 0f : 1f) + (d < 5f ? -0.06f : 0f);
            }
            int N = Mathf.RoundToInt(RAD * 2f / CELL) + 1;
            var tv = new Vector3[N * N]; var tuv = new Vector2[N * N];
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                {
                    float x = c.x - RAD + i * CELL, z = c.z - RAD + j * CELL;
                    tv[i * N + j] = new Vector3(x, groundY + H(x, z) - (Dist(x, z) < 5f ? 0f : 0.06f), z);
                    tuv[i * N + j] = new Vector2(x, z) * 0.1f;
                }
            var band = new List<int>[3] { new List<int>(), new List<int>(), new List<int>() };
            for (int i = 0; i < N - 1; i++)
                for (int j = 0; j < N - 1; j++)
                {
                    int i00 = i * N + j, i01 = i00 + 1, i10 = i00 + N, i11 = i10 + 1;
                    float ay = (tv[i00].y + tv[i01].y + tv[i10].y + tv[i11].y) * 0.25f - groundY;
                    var l = band[ay < 12f ? 0 : ay < 38f ? 1 : 2];
                    l.Add(i00); l.Add(i01); l.Add(i10); l.Add(i10); l.Add(i01); l.Add(i11);
                }
            var tmesh = new Mesh { name = "Terrain", indexFormat = IndexFormat.UInt32 };
            tmesh.SetVertices(tv); tmesh.SetUVs(0, tuv); tmesh.subMeshCount = 3;
            for (int k = 0; k < 3; k++) tmesh.SetTriangles(band[k], k);
            tmesh.RecalculateNormals(); tmesh.RecalculateBounds();
            {
                var go = new GameObject("Terrain"); go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = tmesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { Tex(new Color(0.42f, 0.38f, 0.32f), 0.35f), Tex(new Color(0.26f, 0.23f, 0.19f), 0.25f), Tex(new Color(0.14f, 0.125f, 0.11f), 0.15f) };
                mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = true;
            }
            Emit("Horizon", Disk(c + Vector3.down * 0.3f, 1400f, 32), Tex(new Color(0.30f, 0.27f, 0.23f), 0.3f), false);

            // ---- vegetation and rocks, standing on the terrain; 4 chunks so far ones are culled
            bool mob = Settings.Mobile; float dens = mob ? 0.5f : 1f;
            var shrubM = new Mesh[] { Blob(1f, 0.6f, 1, 0.28f, 11), Blob(1f, 0.7f, 1, 0.3f, 12), Blob(1f, 0.55f, 1, 0.34f, 13) };
            var canopy = Blob(1f, 0.38f, 2, 0.16f, 21); var trunkM = Trunk(); var deadM = DeadTree();
            var rockM = new Mesh[] { Rock(31, 0.34f), Rock(32, 0.45f), Rock(33, 0.28f) };
            var lists = new Dictionary<string, List<CombineInstance>>();
            void Put(string key, Mesh m, Vector3 p, float yaw, Vector3 sc, float tx = 0f, float tz = 0f)
            {
                string ck = key + (p.x > c.x ? "E" : "W") + (p.z > c.z ? "N" : "S");
                if (!lists.TryGetValue(ck, out var l)) lists[ck] = l = new List<CombineInstance>();
                l.Add(new CombineInstance { mesh = m, transform = Matrix4x4.TRS(p, Quaternion.Euler(tx, yaw, tz), sc) });
            }
            Vector3 G(float x, float z, float sink = 0f) => new Vector3(x, groundY + H(x, z) - sink, z);
            bool Free(float x, float z, float margin) => Dist(x, z) > margin;
            float Veg(float x, float z) => Mathf.Clamp01((Fbm(x, z, 0.011f, 3, so + 500f) - 0.38f) * 3.2f);      // patchy: groves and bare sand
            float span = RAD - 40f;

            // shrubs and dry brush: dense, in patches
            int nShrub = Mathf.RoundToInt(4200 * dens);
            for (int i = 0; i < nShrub; i++)
            {
                float x = c.x + R(-span, span), z = c.z + R(-span, span);
                if (!Free(x, z, 7f) || Dist(x, z) > 480f) continue;
                if (rng.NextDouble() > 0.12f + 0.88f * Veg(x, z)) continue;
                float ht = H(x, z); if (ht > 55f) continue;
                float s = R(0.55f, 1.7f); int v = rng.Next(3);
                Put("S" + v, shrubM[v], G(x, z, 0.05f), R(0, 360), new Vector3(s * R(0.9f, 1.5f), s * R(0.7f, 1.1f), s * R(0.9f, 1.5f)));
                if (rng.NextDouble() < 0.5) Put("S" + v, shrubM[(v + 1) % 3], G(x + R(-1, 1), z + R(-1, 1), 0.05f), R(0, 360), new Vector3(s * 0.7f, s * 0.6f, s * 0.7f));
            }
            // acacia-like trees and dead trunks in groves
            int nTree = Mathf.RoundToInt(520 * dens);
            for (int i = 0; i < nTree; i++)
            {
                float x = c.x + R(-span, span), z = c.z + R(-span, span);
                if (!Free(x, z, 12f) || Dist(x, z) > 420f) continue;
                if (rng.NextDouble() > Veg(x, z) * Veg(x * 1.3f + 50f, z * 1.3f)) continue;
                if (H(x, z) > 40f) continue;
                float s = R(0.8f, 1.9f), yaw = R(0, 360);
                var p = G(x, z, 0.1f);
                if (rng.NextDouble() < 0.28)
                    Put("D", deadM, p, yaw, new Vector3(s, s * R(0.9f, 1.5f), s), R(-4, 4), R(-4, 4));
                else
                {
                    float th = 3.6f * s;
                    Put("T", trunkM, p, yaw, new Vector3(s, s, s), R(-5, 5), R(-5, 5));
                    Put("L", canopy, p + new Vector3(0, th, 0), yaw, new Vector3(s * R(2.6f, 3.6f), s * R(0.9f, 1.3f), s * R(2.6f, 3.6f)));
                    Put("L", canopy, p + new Vector3(R(-1.6f, 1.6f) * s, th - 0.5f * s, R(-1.6f, 1.6f) * s), yaw, new Vector3(s * 2f, s * 0.8f, s * 2f));
                }
            }
            // boulders and rock outcrops: singles everywhere, bigger piles on the foothills
            int nRock = Mathf.RoundToInt(900 * dens);
            for (int i = 0; i < nRock; i++)
            {
                float x = c.x + R(-span, span), z = c.z + R(-span, span);
                if (!Free(x, z, 6f) || Dist(x, z) > 520f) continue;
                float ht = H(x, z); int v = rng.Next(3);
                float big = ht > 20f ? 1f : 0f; float s = R(0.5f, 2.2f) * (1f + big * R(0.5f, 2.5f));
                var p = G(x, z, s * 0.25f);
                Put("R" + v, rockM[v], p, R(0, 360), new Vector3(s * R(0.9f, 1.7f), s * R(0.6f, 1.2f), s * R(0.9f, 1.7f)), R(-8, 8), R(-8, 8));
                if (rng.NextDouble() < 0.4) Put("R" + ((v + 1) % 3), rockM[(v + 1) % 3], G(x + R(-2, 2) * s, z + R(-2, 2) * s, s * 0.2f), R(0, 360), new Vector3(s * 0.6f, s * 0.5f, s * 0.6f));
            }
            Material[] shrubMat = { Mat(new Color(0.23f, 0.23f, 0.12f), 0.02f), Mat(new Color(0.29f, 0.25f, 0.14f), 0.02f), Mat(new Color(0.18f, 0.19f, 0.11f), 0.02f) };
            Material[] rockMat = { Tex(new Color(0.52f, 0.48f, 0.42f), 0.5f), Tex(new Color(0.42f, 0.38f, 0.34f), 0.5f), Tex(new Color(0.60f, 0.55f, 0.47f), 0.5f) };
            var wood = Mat(new Color(0.20f, 0.15f, 0.10f), 0.02f); var leafM = Mat(new Color(0.13f, 0.16f, 0.08f), 0.02f); var deadMat = Mat(new Color(0.30f, 0.26f, 0.20f), 0.02f);
            foreach (var kv in lists)
            {
                string k = kv.Key; Material m; bool cast = true;
                if (k[0] == 'S') { m = shrubMat[k[1] - '0']; cast = false; }
                else if (k[0] == 'R') m = rockMat[k[1] - '0'];
                else if (k[0] == 'T') m = wood; else if (k[0] == 'L') m = leafM; else m = deadMat;
                var mesh = new Mesh { name = k, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true); mesh.RecalculateBounds();
                Emit(k, mesh, m, cast);
            }
        }

        // ------------------------------------------------------------------ meshes
        static Mesh Disk(Vector3 centre, float radius, int seg)
        {
            var v = new List<Vector3> { centre }; var t = new List<int>();
            for (int i = 0; i < seg; i++) { float a = i / (float)seg * Mathf.PI * 2f; v.Add(centre + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius)); }
            for (int i = 0; i < seg; i++) { t.Add(0); t.Add(1 + (i + 1) % seg); t.Add(1 + i); }
            var m = new Mesh { name = "Disk" }; m.SetVertices(v); m.SetUVs(0, PlanarUV(v)); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        static List<Vector2> PlanarUV(List<Vector3> v)
        {
            var uv = new List<Vector2>(v.Count);
            for (int i = 0; i < v.Count; i++) uv.Add(new Vector2(v[i].x, v[i].z) * 0.1f);
            return uv;
        }

        static Mesh Cone(float radius, float height, int sides, float baseY)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            v.Add(new Vector3(0, baseY + height, 0)); v.Add(new Vector3(0, baseY, 0));
            for (int i = 0; i < sides; i++) { float a = i / (float)sides * Mathf.PI * 2f; v.Add(new Vector3(Mathf.Cos(a) * radius, baseY, Mathf.Sin(a) * radius)); }
            for (int i = 0; i < sides; i++) { int a = 2 + i, b = 2 + (i + 1) % sides; t.Add(0); t.Add(b); t.Add(a); t.Add(1); t.Add(a); t.Add(b); }
            var m = new Mesh { name = "Cone" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        static Mesh Cyl(float radius, float height, int sides)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < sides; i++) { float a = i / (float)sides * Mathf.PI * 2f; var p = new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius); v.Add(p); v.Add(p + Vector3.up * height); }
            for (int i = 0; i < sides; i++) { int a = i * 2, b = ((i + 1) % sides) * 2; t.Add(a); t.Add(a + 1); t.Add(b); t.Add(b); t.Add(a + 1); t.Add(b + 1); }
            var m = new Mesh { name = "Cyl" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        static Mesh Combine(params (Mesh m, Matrix4x4 x)[] parts)
        {
            var ci = new CombineInstance[parts.Length];
            for (int i = 0; i < parts.Length; i++) ci[i] = new CombineInstance { mesh = parts[i].m, transform = parts[i].x };
            var r = new Mesh(); r.CombineMeshes(ci, true, true); r.RecalculateBounds(); return r;
        }

        /// Lumpy squashed sphere (subdivided icosahedron with noise): shrub clumps and tree canopies.
        static Mesh Blob(float r, float squash, int sub, float noise, int seed)
        {
            var m = Ico(sub, seed, r, noise, squash); m.name = "Blob"; return m;
        }

        static Mesh Ico(int sub, int seed, float r, float noise, float squash, bool flat = false)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new List<Vector3> { new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1) };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            for (int s = 0; s < sub; s++)
            {
                var cache = new Dictionary<long, int>(); var nf = new List<int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int id)) return id;
                    v.Add(((v[a] + v[b]) * 0.5f).normalized); return cache[key] = v.Count - 1;
                }
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = nf;
            }
            // noise by direction so shared vertices move together (no cracks)
            var rn = new System.Random(seed); float ox = (float)rn.NextDouble() * 50f, oy = (float)rn.NextDouble() * 50f, oz = (float)rn.NextDouble() * 50f;
            var pv = new Vector3[v.Count];
            for (int i = 0; i < v.Count; i++)
            {
                var d = v[i];
                float n = Mathf.PerlinNoise(d.x * 1.7f + ox, d.y * 1.7f + oy) * 0.6f + Mathf.PerlinNoise(d.z * 2.3f + oz, d.x * 2.1f + ox) * 0.4f;
                var p = d * r * (1f - noise + n * noise * 2f);
                p.y *= squash; pv[i] = p;
            }
            // cut away the flat underside: it is never seen and costs triangles
            var verts = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            if (flat)
            {
                for (int i = 0; i < f.Count; i += 3) for (int k = 0; k < 3; k++) { var p = pv[f[i + k]]; tri.Add(verts.Count); verts.Add(p); uv.Add(new Vector2(p.x, p.z) * 0.8f); }
            }
            else { verts.AddRange(pv); foreach (var p in pv) uv.Add(new Vector2(p.x, p.z) * 0.8f); tri.AddRange(f); }
            var m = new Mesh { name = "Ico" }; m.SetVertices(verts); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        static Mesh Trunk()
        {
            var main = Cyl(0.2f, 3.8f, 6);
            var br = Cyl(0.09f, 1.7f, 5);
            return Combine((main, Matrix4x4.identity),
                (br, Matrix4x4.TRS(new Vector3(0, 2.7f, 0), Quaternion.Euler(0, 30, 42), Vector3.one)),
                (br, Matrix4x4.TRS(new Vector3(0, 3.0f, 0), Quaternion.Euler(0, 210, -45), Vector3.one * 0.9f)),
                (br, Matrix4x4.TRS(new Vector3(0, 3.2f, 0), Quaternion.Euler(0, 120, 30), Vector3.one * 0.7f)));
        }

        static Mesh DeadTree()
        {
            var trunk = Cyl(0.15f, 4.2f, 5); var br = Cyl(0.07f, 1.6f, 4);
            return Combine((trunk, Matrix4x4.identity),
                (br, Matrix4x4.TRS(new Vector3(0, 2.6f, 0), Quaternion.Euler(0, 20, 48), Vector3.one)),
                (br, Matrix4x4.TRS(new Vector3(0, 3.3f, 0), Quaternion.Euler(0, 200, -42), Vector3.one * 0.8f)));
        }

        /// Faceted boulder: noisy flat-shaded blob.
        static Mesh Rock(int seed, float noise) => Ico(1, seed, 1f, noise, 0.8f, true);
    }
}
