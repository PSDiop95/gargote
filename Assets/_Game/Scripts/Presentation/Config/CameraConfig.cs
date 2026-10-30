using System;
using Gargote.Systems.Street;
using UnityEngine;
using UnityEngine.Audio;

namespace Gargote.Presentation
{
    /// <summary>Caméra orthographique isométrique : scroll à inertie, rebond aux bords, pinch-zoom borné.</summary>
    [CreateAssetMenu(menuName = "Gargote/Visual/Camera Config", fileName = "CameraConfig")]
    public sealed class CameraConfig : ScriptableObject
    {
        [Header("Cadrage")]
        [Range(20f, 70f)] public float pitch = 45f;
        [Range(-60f, 60f)] public float yaw = 0f;
        public float distance = 40f;
        [Tooltip("Taille orthographique minimale à zoom 1 (demi-hauteur visible en unités monde).")]
        public float orthographicSize = 7f;
        [Tooltip("Demi-largeur de rue toujours visible à zoom 1, quel que soit le ratio d'écran (les deux rangées de commerces).")]
        public float visibleHalfWidth = 5.4f;
        [Tooltip("Décalage du point visé sur l'axe X de la rue.")]
        public float lookOffsetX = 0f;

        [Header("Zoom (pinch)")]
        public float minZoom = 0.8f;
        public float maxZoom = 1.3f;
        public float wheelZoomStep = 0.08f;

        [Header("Scroll")]
        [Tooltip("Pixels avant qu'un appui devienne un glissé (et non un tap).")]
        public float dragThresholdPixels = 18f;
        [Tooltip("Décélération de l'inertie (1/s).")]
        public float inertiaDamping = 4.5f;
        public float maxInertiaSpeed = 60f;
        [Tooltip("Dépassement maximal permis aux bords avant rebond (unités monde).")]
        public float overscroll = 3f;
        [Tooltip("Raideur du ressort de rebond aux bords.")]
        public float bounceStiffness = 14f;
        [Tooltip("Marge ajoutée aux bornes calculées depuis les emplacements de la rue.")]
        public float boundsMargin = 2f;

        [Header("Recentrage / effets")]
        public float focusDuration = 0.45f;
        [Tooltip("Décalage vers le haut de l'écran du commerce recentré (laisse la place au panneau).")]
        public float focusScreenOffset = 2.5f;
        public float milestoneZoomPunch = 0.06f;
        public float milestoneZoomDuration = 0.35f;
    }
}
