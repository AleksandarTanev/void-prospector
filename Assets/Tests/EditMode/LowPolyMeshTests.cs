using NUnit.Framework;
using UnityEngine;
using VoidProspector.Fx;

namespace VoidProspector.Tests
{
    public class LowPolyMeshTests
    {
        [Test]
        public void Rock_HasEightyUnweldedTriangles()
        {
            var mesh = LowPolyMeshes.BuildRock(3);
            Assert.AreEqual(240, mesh.triangles.Length);
            Assert.AreEqual(240, mesh.vertexCount);
        }

        [TestCase(0), TestCase(5)]
        public void Rock_FacesAreFlatAndPointOutward(int seed)
        {
            var mesh = LowPolyMeshes.BuildRock(seed);
            var v = mesh.vertices;
            var n = mesh.normals;
            var t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                Assert.AreEqual(n[t[i]], n[t[i + 1]]);
                Assert.AreEqual(n[t[i]], n[t[i + 2]]);
                Assert.Greater(Vector3.Dot(n[t[i]], a + b + c), 0f, "face points inward");
                // Unity front faces: cross(b-a, c-a) matches the stored normal.
                Assert.Greater(Vector3.Dot(Vector3.Cross(b - a, c - a), n[t[i]]), 0f, "wrong winding");
            }
        }

        [Test]
        public void Rock_StaysAboutHalfAUnitAcross()
        {
            foreach (var p in LowPolyMeshes.BuildRock(1).vertices)
            {
                Assert.LessOrEqual(p.magnitude, 0.5f * 1.05f + 1e-4f);
                Assert.GreaterOrEqual(p.magnitude, 0.5f * 0.78f * 0.8f - 1e-4f);
            }
        }

        [Test]
        public void Rock_SameVariantSameShape()
        {
            CollectionAssert.AreEqual(LowPolyMeshes.BuildRock(2).vertices, LowPolyMeshes.BuildRock(2).vertices);
            CollectionAssert.AreNotEqual(LowPolyMeshes.BuildRock(2).vertices, LowPolyMeshes.BuildRock(4).vertices);
        }

        [TestCase(6), TestCase(8)]
        public void Cone_IsClosedAndFacesOutward(int sides)
        {
            var mesh = LowPolyMeshes.Cone(sides);
            Assert.AreEqual(sides * 2 * 3, mesh.triangles.Length);
            Assert.That(mesh.bounds.size.y, Is.EqualTo(1f).Within(1e-4f));
            var v = mesh.vertices;
            var n = mesh.normals;
            for (int i = 0; i < v.Length; i += 3)
                Assert.Greater(Vector3.Dot(Vector3.Cross(v[i + 1] - v[i], v[i + 2] - v[i]), n[i]), 0f, "wrong winding");
        }

        [Test]
        public void Gem_IsClosedOctahedron()
        {
            var mesh = LowPolyMeshes.Gem;
            Assert.AreEqual(24, mesh.triangles.Length);
            Assert.That(mesh.bounds.size.y, Is.EqualTo(1f).Within(1e-4f));
        }
    }
}
