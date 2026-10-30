using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>Thème de l'UI placeholder (HUD, panneau bas).</summary>
    [CreateAssetMenu(menuName = "Gargote/Visual/Hud Config", fileName = "HudConfig")]
    public sealed class HudConfig : ScriptableObject
    {
        public Vector2 referenceResolution = new Vector2(1080, 1920);
        [Tooltip("Hauteur minimale d'une cible tactile (px de référence, ≈ 48 dp).")]
        public float minTouchSize = 132f;
        public float topBarHeight = 230f;
        public float sheetCollapsedHeight = 330f;
        public float sheetExpandedHeight = 1100f;
        public float rowHeight = 190f;
        public float sheetAnimSeconds = 0.3f;
        public float refreshInterval = 0.2f;
        public Color panel = new Color(0.12f, 0.10f, 0.14f, 0.92f);
        public Color panelLight = new Color(0.22f, 0.19f, 0.25f, 1f);
        public Color accent = new Color(0.98f, 0.62f, 0.12f);
        public Color money = new Color(0.55f, 0.90f, 0.45f);
        public Color cauris = new Color(0.95f, 0.92f, 0.85f);
        public Color buttonEnabled = new Color(0.22f, 0.70f, 0.32f);
        public Color buttonDisabled = new Color(0.35f, 0.35f, 0.38f);
        public Color buttonGlow = new Color(0.65f, 1f, 0.55f);
        public Color text = Color.white;
        public Color mutedText = new Color(0.75f, 0.72f, 0.78f);
        public float fontSizeLarge = 64f;
        public float fontSizeMedium = 40f;
        public float fontSizeSmall = 30f;
        public TMPro.TMP_FontAsset font;
    }
}
