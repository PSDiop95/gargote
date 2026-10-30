using UnityEngine;
using UnityEngine.Rendering;

namespace Gargote.Presentation
{
    /// <summary>
    /// Dessine tous les personnages (foule, employés, gérants, clients spéciaux, accessoires) par GPU instancing :
    /// un draw par variante de mesh, tableaux de matrices préalloués, aucune allocation par frame.
    /// </summary>
    public sealed class PersonRenderer
    {
        private sealed class Batch
        {
            public Mesh Mesh;
            public Matrix4x4[] Matrices;
            public int Count;
        }

        private readonly Batch[] _batches;
        private int _batchCount;
        private RenderParams _params;

        public int DrawnInstances { get; private set; }
        /// <summary>Diagnostic (capture de perf) : coupe le rendu des personnages.</summary>
        public bool Enabled = true;
        public int DrawCalls { get; private set; }

        public PersonRenderer(Material material, int maxBatches, Bounds worldBounds)
        {
            material.enableInstancing = true;
            _batches = new Batch[maxBatches];
            _params = new RenderParams(material)
            {
                worldBounds = worldBounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                layer = 0,
            };
        }

        /// <summary>Ombre temps réel des personnages (désactivée par défaut : ombre « blob » dans le mesh).</summary>
        public void SetShadows(bool castShadows) =>
            _params.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

        /// <summary>Déclare une variante ; renvoie son identifiant (à faire au chargement).</summary>
        public int Register(Mesh mesh, int capacity)
        {
            _batches[_batchCount] = new Batch { Mesh = mesh, Matrices = new Matrix4x4[Mathf.Max(1, capacity)] };
            return _batchCount++;
        }

        public void Add(int batch, in Matrix4x4 matrix)
        {
            if (batch < 0) return;
            var b = _batches[batch];
            if (b.Count < b.Matrices.Length) b.Matrices[b.Count++] = matrix;
        }

        /// <summary>Soumet les instances de la frame puis vide les tampons.</summary>
        public void Render()
        {
            int drawn = 0, calls = 0;
            for (int i = 0; i < _batchCount; i++)
            {
                var b = _batches[i];
                if (b.Count == 0 || b.Mesh == null) continue;
                if (Enabled) Graphics.RenderMeshInstanced(_params, b.Mesh, 0, b.Matrices, b.Count);
                drawn += b.Count;
                calls++;
                b.Count = 0;
            }
            DrawnInstances = drawn;
            DrawCalls = calls;
        }
    }
}
