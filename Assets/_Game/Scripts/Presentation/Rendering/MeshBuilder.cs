using System.Collections.Generic;
using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>
    /// Assemble des primitives low-poly à normales plates en un seul mesh à couleurs de sommet
    /// (alpha = masque émissif). Sert à générer les placeholders (éditeur) : un mesh = un draw.
    /// </summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<int> _triangles = new List<int>();
        private Matrix4x4 _matrix = Matrix4x4.identity;

        public MeshBuilder SetTransform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            _matrix = Matrix4x4.TRS(position, rotation, scale);
            return this;
        }

        public MeshBuilder ResetTransform()
        {
            _matrix = Matrix4x4.identity;
            return this;
        }

        private static Color32 ToColor(Color color, bool emissive)
        {
            Color32 c = color;
            c.a = (byte)(emissive ? 255 : 0);
            return c;
        }

        private void AddFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color)
        {
            a = _matrix.MultiplyPoint3x4(a);
            b = _matrix.MultiplyPoint3x4(b);
            c = _matrix.MultiplyPoint3x4(c);
            d = _matrix.MultiplyPoint3x4(d);
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int i = _vertices.Count;
            _vertices.Add(a); _vertices.Add(b); _vertices.Add(c); _vertices.Add(d);
            for (int k = 0; k < 4; k++) { _normals.Add(n); _colors.Add(color); }
            _triangles.Add(i); _triangles.Add(i + 1); _triangles.Add(i + 2);
            _triangles.Add(i); _triangles.Add(i + 2); _triangles.Add(i + 3);
        }

        private void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color32 color)
        {
            a = _matrix.MultiplyPoint3x4(a);
            b = _matrix.MultiplyPoint3x4(b);
            c = _matrix.MultiplyPoint3x4(c);
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int i = _vertices.Count;
            _vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
            for (int k = 0; k < 3; k++) { _normals.Add(n); _colors.Add(color); }
            _triangles.Add(i); _triangles.Add(i + 1); _triangles.Add(i + 2);
        }

        /// <summary>Boîte centrée en <paramref name="center"/>. <paramref name="skipBottom"/> économise 2 triangles au sol.</summary>
        public MeshBuilder Box(Vector3 center, Vector3 size, Color color, bool emissive = false, bool skipBottom = true)
        {
            var c = ToColor(color, emissive);
            Vector3 h = size * 0.5f;
            Vector3 p000 = center + new Vector3(-h.x, -h.y, -h.z), p100 = center + new Vector3(h.x, -h.y, -h.z);
            Vector3 p010 = center + new Vector3(-h.x, h.y, -h.z), p110 = center + new Vector3(h.x, h.y, -h.z);
            Vector3 p001 = center + new Vector3(-h.x, -h.y, h.z), p101 = center + new Vector3(h.x, -h.y, h.z);
            Vector3 p011 = center + new Vector3(-h.x, h.y, h.z), p111 = center + new Vector3(h.x, h.y, h.z);
            AddFace(p000, p010, p110, p100, c); // avant (-Z)
            AddFace(p101, p111, p011, p001, c); // arrière
            AddFace(p001, p011, p010, p000, c); // gauche
            AddFace(p100, p110, p111, p101, c); // droite
            AddFace(p010, p011, p111, p110, c); // dessus
            if (!skipBottom) AddFace(p001, p000, p100, p101, c);
            return this;
        }

        /// <summary>Boîte posée au sol : <paramref name="baseCenter"/> = centre de la face inférieure.</summary>
        public MeshBuilder BoxOnGround(Vector3 baseCenter, Vector3 size, Color color, bool emissive = false) =>
            Box(baseCenter + new Vector3(0f, size.y * 0.5f, 0f), size, color, emissive);

        /// <summary>Cylindre vertical (ou cône si <paramref name="topRadius"/> = 0) posé sur <paramref name="baseCenter"/>.</summary>
        public MeshBuilder Cylinder(Vector3 baseCenter, float radius, float height, int segments, Color color,
            float topRadius = -1f, bool emissive = false)
        {
            if (topRadius < 0f) topRadius = radius;
            var c = ToColor(color, emissive);
            Vector3 top = baseCenter + Vector3.up * height;
            for (int s = 0; s < segments; s++)
            {
                float a0 = s * Mathf.PI * 2f / segments, a1 = (s + 1) * Mathf.PI * 2f / segments;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 b0 = baseCenter + d0 * radius, b1 = baseCenter + d1 * radius;
                if (topRadius > 0f)
                {
                    Vector3 t0 = top + d0 * topRadius, t1 = top + d1 * topRadius;
                    AddFace(b0, t0, t1, b1, c);
                    AddTriangle(top, t1, t0, c);
                }
                else AddTriangle(b0, top, b1, c);
            }
            return this;
        }

        /// <summary>Sphère low-poly (anneaux × segments) centrée.</summary>
        public MeshBuilder Sphere(Vector3 center, float radius, Color color, int rings = 5, int segments = 8, bool emissive = false,
            float squashY = 1f)
        {
            var c = ToColor(color, emissive);
            for (int r = 0; r < rings; r++)
            {
                float v0 = Mathf.PI * r / rings - Mathf.PI * 0.5f, v1 = Mathf.PI * (r + 1) / rings - Mathf.PI * 0.5f;
                for (int s = 0; s < segments; s++)
                {
                    float u0 = s * Mathf.PI * 2f / segments, u1 = (s + 1) * Mathf.PI * 2f / segments;
                    Vector3 p00 = center + Point(u0, v0, radius, squashY), p10 = center + Point(u1, v0, radius, squashY);
                    Vector3 p01 = center + Point(u0, v1, radius, squashY), p11 = center + Point(u1, v1, radius, squashY);
                    if (r == 0) AddTriangle(p00, p01, p11, c);
                    else if (r == rings - 1) AddTriangle(p00, p01, p10, c);
                    else AddFace(p00, p01, p11, p10, c);
                }
            }
            return this;
        }

        private static Vector3 Point(float u, float v, float radius, float squashY) =>
            new Vector3(Mathf.Cos(v) * Mathf.Cos(u), Mathf.Sin(v) * squashY, Mathf.Cos(v) * Mathf.Sin(u)) * radius;

        /// <summary>Plan horizontal (sol, chaussée) à la hauteur y.</summary>
        public MeshBuilder Plane(Vector3 center, Vector2 size, Color color, bool emissive = false)
        {
            var c = ToColor(color, emissive);
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            AddFace(center + new Vector3(-hx, 0f, -hz), center + new Vector3(-hx, 0f, hz),
                center + new Vector3(hx, 0f, hz), center + new Vector3(hx, 0f, -hz), c);
            return this;
        }

        public int TriangleCount => _triangles.Count / 3;

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        public void Clear()
        {
            _vertices.Clear();
            _normals.Clear();
            _colors.Clear();
            _triangles.Clear();
            _matrix = Matrix4x4.identity;
        }
    }
}
