using System;
using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>Références vers les placeholders générés (meshes, matériaux, prefab, sprites). Remplacés en M4.</summary>
    [CreateAssetMenu(menuName = "Gargote/Visual/Street Assets", fileName = "StreetAssets")]
    public sealed class StreetAssets : ScriptableObject
    {
        [Header("Matériaux")]
        public Material toon;
        public Material particles;

        [Header("Rue et commerces")]
        public Mesh street;
        public BusinessView businessPrefab;
        [Tooltip("Stades par commerce : index = commerce × 5 + stade.")]
        public Mesh[] stageMeshes = Array.Empty<Mesh>();

        [Header("Personnages (instancing)")]
        public Mesh[] customerMeshes = Array.Empty<Mesh>();
        public Mesh employeeMesh;
        public Mesh managerMesh;
        [Tooltip("Même ordre que SpecialCustomersConfig.customers.")]
        public Mesh[] specialMeshes = Array.Empty<Mesh>();
        [Tooltip("0 = sac, 1 = assiette.")]
        public Mesh[] carryMeshes = Array.Empty<Mesh>();

        [Header("UI")]
        public Sprite rounded;
        public Sprite circle;
        public Sprite bill;
        public Sprite hand;
        public Sprite star;

        public Mesh Stage(int business, int stage)
        {
            int i = business * Placeholders.StageCount + stage;
            return i >= 0 && i < stageMeshes.Length ? stageMeshes[i] : null;
        }
    }
}
