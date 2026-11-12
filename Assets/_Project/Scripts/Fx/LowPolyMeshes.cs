using System.Collections.Generic;
using UnityEngine;

namespace VoidProspector.Fx
{
    /// <summary>
    /// Flat-shaded low-poly meshes generated in code (rocks and gems), 1 world unit across like
    /// <see cref="ShapeSprites"/>, plus the shared lit material. Meshes are cached and shared between objects.
    /// </summary>
    public static class LowPolyMeshes
    {
        public const int RockVariants = 8;
        public const string MaterialPath = "LowPoly/LowPolyLit";

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly Mesh[] Rocks = new Mesh[RockVariants];
        static Mesh _gem;
        static Material _material;
        static MaterialPropertyBlock _block;

        /// <summary>The shared lit material (Resources/LowPoly/LowPolyLit, created by Void Prospector → Low-poly Setup).</summary>
        public static Material Material
        {
            get
            {
                if (_material != null) return _material;
                _material = Resources.Load<Material>(MaterialPath);
                if (_material == null)
                {
                    Debug.LogWarning("LowPolyMeshes: Resources/" + MaterialPath + " is missing; run Void Prospector → Low-poly Setup.");
                    var shader = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Lit");
                    _material = new Material(shader) { name = "LowPolyLit (fallback)" };
                }
                return _material;
            }
        }

        /// <summary>A lumpy rock, radius about 0.5. The same variant always gives the same shape.</summary>
        public static Mesh Rock(int variant)
        {
            variant = ((variant % RockVariants) + RockVariants) % RockVariants;
            return Rocks[variant] != null ? Rocks[variant] : (Rocks[variant] = BuildRock(variant));
        }

        /// <summary>A stretched octahedron (chunks and mineral shards), 1 unit tall, 0.7 wide.</summary>
        public static Mesh Gem => _gem != null ? _gem : (_gem = BuildGem());

        public static Mesh BuildRock(int seed)
        {
            Icosphere(out var verts, out var tris);
            var rng = new System.Random(1000 + seed * 7919);
            for (int i = 0; i < verts.Count; i++)
                verts[i] = verts[i] * (0.5f * Mathf.Lerp(0.78f, 1.05f, (float)rng.NextDouble()));
            // Slightly squashed on one axis so rocks aren't all round.
            float squash = Mathf.Lerp(0.8f, 0.95f, (float)rng.NextDouble());
            for (int i = 0; i < verts.Count; i++) verts[i] = new Vector3(verts[i].x, verts[i].y, verts[i].z * squash);
            return Flat(verts, tris, "Rock " + seed);
        }

        static Mesh BuildGem()
        {
            const float w = 0.35f, h = 0.5f;
            var v = new List<Vector3>
            {
                new Vector3(0, h, 0), new Vector3(0, -h, 0),
                new Vector3(w, 0, 0), new Vector3(0, 0, w), new Vector3(-w, 0, 0), new Vector3(0, 0, -w),
            };
            var t = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                int a = 2 + i, b = 2 + (i + 1) % 4;
                t.AddRange(new[] { 0, b, a });
                t.AddRange(new[] { 1, a, b });
            }
            return Flat(v, t, "Gem");
        }

        static readonly Dictionary<int, Mesh> Cones = new Dictionary<int, Mesh>();

        /// <summary>A closed cone pointing up (+Y), 1 unit tall and 1 across, centred on the origin.</summary>
        public static Mesh Cone(int sides = 6)
        {
            sides = Mathf.Max(3, sides);
            if (Cones.TryGetValue(sides, out var m) && m != null) return m;
            var v = new List<Vector3> { new Vector3(0, 0.5f, 0), new Vector3(0, -0.5f, 0) };
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                v.Add(new Vector3(Mathf.Cos(a) * 0.5f, -0.5f, Mathf.Sin(a) * 0.5f));
            }
            var t = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                int a = 2 + i, b = 2 + (i + 1) % sides;
                t.AddRange(new[] { 0, a, b });
                t.AddRange(new[] { 1, a, b });
            }
            return Cones[sides] = Flat(v, t, "Cone " + sides);
        }

        /// <summary>Icosahedron subdivided once: 42 points, 80 triangles, on the unit sphere.</summary>
        static void Icosphere(out List<Vector3> verts, out List<int> tris)
        {
            float p = (1f + Mathf.Sqrt(5f)) / 2f;
            verts = new List<Vector3>
            {
                new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
                new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
                new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1),
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            var faces = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            var cache = new Dictionary<long, int>();
            var vs = verts;
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (cache.TryGetValue(key, out int m)) return m;
                vs.Add(((vs[a] + vs[b]) * 0.5f).normalized);
                cache[key] = vs.Count - 1;
                return vs.Count - 1;
            }

            tris = new List<int>();
            for (int i = 0; i < faces.Count; i += 3)
            {
                int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
        }

        /// <summary>Unwelds the triangles so each face has its own vertices and normal (flat shading).</summary>
        static Mesh Flat(List<Vector3> verts, List<int> tris, string name)
        {
            var v = new Vector3[tris.Count];
            var n = new Vector3[tris.Count];
            var t = new int[tris.Count];
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                var normal = Vector3.Cross(b - a, c - a).normalized;
                // In Unity cross(b-a, c-a) points out of a clockwise (front) face; flip faces that point inward.
                if (Vector3.Dot(normal, a + b + c) < 0) { (b, c) = (c, b); normal = -normal; }
                v[i] = a; v[i + 1] = b; v[i + 2] = c;
                n[i] = n[i + 1] = n[i + 2] = normal;
                t[i] = i; t[i + 1] = i + 1; t[i + 2] = i + 2;
            }
            var mesh = new Mesh { name = name, vertices = v, normals = n, triangles = t };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        /// <summary>Creates a child with a MeshFilter and MeshRenderer using the shared material, no shadows.</summary>
        public static MeshRenderer NewRenderer(string name, Mesh mesh, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return mr;
        }

        /// <summary>Sets the renderer's colour (and optional self-glow, 0–1) without making a new material.</summary>
        public static void SetColor(Renderer renderer, Color color, float glow = 0f)
        {
            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            _block.SetColor(EmissionId, color * glow);
            renderer.SetPropertyBlock(_block);
        }
    }
}
