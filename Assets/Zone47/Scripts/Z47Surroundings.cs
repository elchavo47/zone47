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
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.02f);
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
            float ex = mb.extents.x, ez = mb.extents.z;
            // distance from the map centre to the map edge along a direction (rectangle), so shapes follow any layout
            float Edge(float dx, float dz)
            {
                float tx = Mathf.Abs(dx) < 1e-4f ? 1e9f : ex / Mathf.Abs(dx), tz = Mathf.Abs(dz) < 1e-4f ? 1e9f : ez / Mathf.Abs(dz);
                return Mathf.Min(tx, tz);
            }

            // 1) huge ground disk just under the map floor: no more void / flat grey beyond the edge
            Emit("Ground", Disk(c + Vector3.down * 0.06f, 900f, 40), Tex(new Color(0.78f, 0.74f, 0.68f), 1f), false);

            // 2) rolling hills close to the map + a taller mountain ring far away
            Emit("Hills", Ring(c, Edge, 14f, 70f, 12f, 48, groundY, rng, 5f, 13f), Tex(new Color(0.70f, 0.64f, 0.56f), 0.8f), false);
            Emit("Mountains", Ring(c, Edge, 150f, 90f, 18f, 72, groundY, rng, 55f, 140f), Tex(new Color(0.40f, 0.34f, 0.29f), 0.12f), false);

            // 3) vegetation clusters outside the bounds
            var pine = Pine(); var trunk = Cyl(0.16f, 1.8f, 6); var deadTrunk = DeadTree(); var bush = Cone(1.0f, 0.9f, 7, 0.25f); var rock = Rock();
            var leaf = new List<CombineInstance>(); var wood = new List<CombineInstance>(); var dry = new List<CombineInstance>(); var scrub = new List<CombineInstance>(); var stone = new List<CombineInstance>();
            void Add(List<CombineInstance> l, Mesh m, Vector3 p, float yaw, Vector3 s, float tiltX = 0f, float tiltZ = 0f)
                => l.Add(new CombineInstance { mesh = m, transform = Matrix4x4.TRS(p, Quaternion.Euler(tiltX, yaw, tiltZ), s) });

            bool Inside(float x, float z, float margin) => Mathf.Abs(x - c.x) < ex + margin && Mathf.Abs(z - c.z) < ez + margin;
            float span = Mathf.Max(ex, ez) + 260f;
            int clusters = 22;
            for (int k = 0; k < clusters; k++)
            {
                float cx, cz; int guard = 0;
                do { cx = c.x + R(-span, span); cz = c.z + R(-span, span); } while (Inside(cx, cz, 14f) && ++guard < 60);
                if (Inside(cx, cz, 10f)) continue;
                float rad = R(12f, 34f); int n = rng.Next(14, 36); bool pines = false;
                for (int i = 0; i < n; i++)
                {
                    float a = R(0f, 6.283f), d = Mathf.Sqrt((float)rng.NextDouble()) * rad;
                    float x = cx + Mathf.Cos(a) * d, z = cz + Mathf.Sin(a) * d;
                    if (Inside(x, z, 8f)) continue;
                    float s = R(0.9f, 2.3f), roll = R(0f, 360f);
                    if (pines && rng.NextDouble() < 0.8) { Add(leaf, pine, new Vector3(x, groundY, z), roll, new Vector3(s, s * R(0.9f, 1.4f), s)); Add(wood, trunk, new Vector3(x, groundY, z), roll, new Vector3(s, s, s)); }
                    else if (rng.NextDouble() < 0.5) Add(dry, deadTrunk, new Vector3(x, groundY, z), roll, new Vector3(s * 0.9f, s * R(0.9f, 1.6f), s * 0.9f));
                    else Add(scrub, bush, new Vector3(x, groundY, z), roll, new Vector3(s * R(0.8f, 1.6f), s * R(0.6f, 1.1f), s * R(0.8f, 1.6f)));
                }
            }
            // loose rocks and scrub in the open between the clusters
            for (int i = 0; i < 260; i++)
            {
                float x = c.x + R(-span, span), z = c.z + R(-span, span);
                if (Inside(x, z, 6f)) continue;
                float s = R(0.5f, 2.6f);
                if (rng.NextDouble() < 0.55) Add(stone, rock, new Vector3(x, groundY + s * 0.15f, z), R(0, 360), new Vector3(s * R(0.8f, 1.6f), s * R(0.5f, 1f), s * R(0.8f, 1.6f)), R(-10, 10), R(-10, 10));
                else Add(scrub, bush, new Vector3(x, groundY, z), R(0, 360), new Vector3(s * 0.7f, s * 0.5f, s * 0.7f));
            }
            void Flush(string name, List<CombineInstance> l, Material m, bool cast)
            {
                if (l.Count == 0) return;
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(l.ToArray(), true, true); mesh.RecalculateBounds();
                Emit(name, mesh, m, cast);
            }
            Flush("PineLeaves", leaf, Mat(new Color(0.10f, 0.15f, 0.08f)), true);
            Flush("Trunks", wood, Mat(new Color(0.24f, 0.17f, 0.11f)), true);
            Flush("DeadTrees", dry, Mat(new Color(0.22f, 0.18f, 0.14f)), true);
            Flush("Scrub", scrub, Mat(new Color(0.24f, 0.24f, 0.13f)), false);
            Flush("Rocks", stone, Tex(new Color(0.55f, 0.52f, 0.48f), 0.5f), true);
        }

        // ------------------------------------------------------------------ meshes
        static Mesh Disk(Vector3 centre, float radius, int seg)
        {
            var v = new List<Vector3> { centre }; var t = new List<int>();
            for (int i = 0; i < seg; i++) { float a = i / (float)seg * Mathf.PI * 2f; v.Add(centre + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius)); }
            for (int i = 0; i < seg; i++) { t.Add(0); t.Add(1 + (i + 1) % seg); t.Add(1 + i); }
            var m = new Mesh { name = "Disk" }; m.SetVertices(v); m.SetUVs(0, PlanarUV(v)); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        /// Noisy ridge: starts `gap` metres outside the map edge, rises over `width` metres to a random peak height.
        static Mesh Ring(Vector3 c, System.Func<float, float, float> edge, float gap, float width, float outer, int seg, float gy, System.Random rng, float hMin, float hMax)
        {
            const int rows = 5;
            var v = new List<Vector3>(); var t = new List<int>();
            float[] peak = new float[seg];
            for (int i = 0; i < seg; i++) peak[i] = hMin + (float)rng.NextDouble() * (hMax - hMin);
            for (int s = 0; s < seg; s++)
            {
                float a = s / (float)seg * Mathf.PI * 2f; float dx = Mathf.Cos(a), dz = Mathf.Sin(a);
                float e = edge(dx, dz) + gap;
                float h0 = (peak[s] + peak[(s + seg - 1) % seg] + peak[(s + 1) % seg]) / 3f;
                for (int r = 0; r < rows; r++)
                {
                    float k = r / (float)(rows - 1);
                    float d = e + k * width + (float)rng.NextDouble() * outer * 0.2f;
                    float h = (r == 0 ? -0.1f : Mathf.Sin(k * Mathf.PI * 0.5f) * h0 * (r == rows - 1 ? 0.35f + 0.65f * 1f : 1f)) + (r > 0 ? (float)rng.NextDouble() * h0 * 0.12f : 0f);
                    if (r == rows - 1) h = h0 * 0.05f;      // far side drops back to the ground (closed ridge, no cliff showing)
                    v.Add(new Vector3(c.x + dx * d, gy + h, c.z + dz * d));
                }
            }
            for (int s = 0; s < seg; s++)
                for (int r = 0; r < rows - 1; r++)
                {
                    int a0 = s * rows + r, a1 = s * rows + r + 1, b0 = ((s + 1) % seg) * rows + r, b1 = ((s + 1) % seg) * rows + r + 1;
                    t.Add(a0); t.Add(b0); t.Add(a1); t.Add(a1); t.Add(b0); t.Add(b1);
                }
            var m = new Mesh { name = "Ridge", indexFormat = IndexFormat.UInt32 }; m.SetVertices(v); m.SetUVs(0, PlanarUV(v)); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            // faces must point up; flip if the winding came out downward
            var n = m.normals; int up = 0; for (int i = 0; i < n.Length; i++) if (n[i].y > 0) up++;
            if (up < n.Length / 2) { var tt = m.GetTriangles(0); for (int i = 0; i < tt.Length; i += 3) { int x = tt[i]; tt[i] = tt[i + 2]; tt[i + 2] = x; } m.SetTriangles(tt, 0); m.RecalculateNormals(); }
            return m;
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

        static Mesh Pine()
            => Combine((Cone(1.6f, 2.4f, 7, 1.3f), Matrix4x4.identity), (Cone(1.25f, 2.2f, 7, 2.5f), Matrix4x4.identity), (Cone(0.85f, 2.0f, 7, 3.7f), Matrix4x4.identity));

        static Mesh DeadTree()
        {
            var trunk = Cyl(0.15f, 4.2f, 5); var br = Cyl(0.07f, 1.6f, 4);
            return Combine((trunk, Matrix4x4.identity),
                (br, Matrix4x4.TRS(new Vector3(0, 2.6f, 0), Quaternion.Euler(0, 20, 48), Vector3.one)),
                (br, Matrix4x4.TRS(new Vector3(0, 3.3f, 0), Quaternion.Euler(0, 200, -42), Vector3.one * 0.8f)));
        }

        static Mesh Rock()
        {
            // low-poly icosahedron-ish blob
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] p = { new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1) };
            int[] f = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            var v = new List<Vector3>(); var tr = new List<int>(); var rn = new System.Random(5);
            for (int i = 0; i < p.Length; i++) v.Add(p[i].normalized * (0.75f + (float)rn.NextDouble() * 0.35f));
            tr.AddRange(f);
            var m = new Mesh { name = "Rock" }; m.SetVertices(v); m.SetUVs(0, PlanarUV(v)); m.SetTriangles(tr, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }
    }
}
