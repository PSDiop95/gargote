using System;
using Gargote.Systems.Street;
using UnityEngine;
using UnityEngine.Audio;

namespace Gargote.Presentation
{
    [Serializable]
    public struct StreetSlot
    {
        public Vector3 position;
        [Tooltip("Côté de la rue : -1 gauche, +1 droite (la file part vers la chaussée).")]
        public int side;
    }

    /// <summary>Disposition et palette de la rue (placeholder) et stades visuels des commerces.</summary>
    [CreateAssetMenu(menuName = "Gargote/Visual/Street Config", fileName = "StreetConfig")]
    public sealed class StreetConfig : ScriptableObject
    {
        [Header("Disposition")]
        public StreetSlot[] slots = Array.Empty<StreetSlot>();
        public float roadHalfWidth = 1.3f;
        public float sidewalkWidth = 0.9f;
        [Tooltip("Abscisse des voies piétonnes (de part et d'autre de la chaussée).")]
        public float laneX = 1.75f;
        [Tooltip("Abscisse des commerces (gauche = -x, droite = +x) utilisée pour générer les emplacements.")]
        public float businessX = 3.9f;
        [Tooltip("Écart en Z entre deux commerces consécutifs (alternés gauche / droite).")]
        public float slotSpacingZ = 5f;
        public float streetStartZ = -8f;
        public float streetEndZ = 44f;
        [Tooltip("Pas des façades voisines (entre les commerces) et des immeubles de second rang.")]
        public float neighbourSpacingZ = 5f;
        public float outerRowX = 8.2f;
        public int backgroundBlocks = 26;
        public Vector2 blockHeightRange = new Vector2(2.5f, 7f);
        public int seed = 1960;

        [Header("File d'attente")]
        [Tooltip("Position du comptoir relative au commerce (côté rue).")]
        public Vector3 counterOffset = new Vector3(0f, 0f, -1.6f);
        public float queueSpacing = 0.62f;

        [Header("Stades visuels (brouette, étal, bâche, boutique, façade néon)")]
        [Tooltip("Niveau d'entrée de chaque stade ; [0] = ouverture.")]
        public long[] stageLevels = { 1, 25, 100, 300, 750 };

        [Header("Palette Dakar (placeholder)")]
        public Color ground = new Color(0.86f, 0.72f, 0.52f);
        public Color road = new Color(0.30f, 0.29f, 0.31f);
        public Color roadLine = new Color(0.95f, 0.85f, 0.35f);
        public Color sidewalk = new Color(0.93f, 0.84f, 0.68f);
        public Color[] buildingColors =
        {
            new Color(0.80f, 0.52f, 0.30f), new Color(0.20f, 0.45f, 0.65f), new Color(0.93f, 0.55f, 0.20f),
            new Color(0.92f, 0.88f, 0.78f), new Color(0.62f, 0.32f, 0.22f),
        };
        public Color[] businessAccents =
        {
            new Color(0.93f, 0.40f, 0.15f), new Color(0.95f, 0.75f, 0.20f), new Color(0.45f, 0.25f, 0.65f),
            new Color(0.15f, 0.55f, 0.45f), new Color(0.75f, 0.15f, 0.20f), new Color(0.95f, 0.55f, 0.55f),
            new Color(0.15f, 0.40f, 0.75f), new Color(0.85f, 0.65f, 0.25f),
        };
        public Color shutter = new Color(0.55f, 0.58f, 0.62f);
        public Color shutterGlow = new Color(1f, 0.85f, 0.3f);
        public Color progressBack = new Color(0.15f, 0.15f, 0.18f);
        public Color progressFill = new Color(0.30f, 0.85f, 0.35f);
        public Color progressRush = new Color(1f, 0.75f, 0.15f);
    }
}
