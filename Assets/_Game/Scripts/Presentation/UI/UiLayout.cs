using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>Zones réservées de l'écran : rien d'autre ne s'y affiche (§M3 prérequis).</summary>
    public enum UiZone
    {
        /// <summary>HUD haut (FCFA, cauris, objectif) et rangée méta juste dessous (niveau, boutons).</summary>
        TopHud = 0,
        /// <summary>Colonne de boutons méta à gauche (missions, cadeaux, coffres…).</summary>
        LeftRail = 1,
        /// <summary>Colonne droite (boost de gérant, boost x2).</summary>
        RightRail = 2,
        /// <summary>Panneau bas (liste des commerces, onglets).</summary>
        BottomSheet = 3,
        /// <summary>Bande des toasts (succès, recette, fonctionnalité) sous le HUD.</summary>
        Toasts = 4,
        /// <summary>Bannière centrale (« VITESSE x2 ! »).</summary>
        Banner = 5,
    }

    /// <summary>
    /// Couches et placement sans chevauchement. Tous les rectangles sont exprimés dans l'espace local du canvas monde
    /// (même échelle que les autres canvas). Les éléments accrochés au monde (popups, badges, clients spéciaux, bulle)
    /// sont repoussés dans la zone de rue : entre le HUD (et les toasts) et le panneau bas, hors des rails latéraux.
    /// Aucune allocation : les rectangles sont recalculés dans des champs à chaque <see cref="Refresh"/>.
    /// </summary>
    public sealed class UiLayout
    {
        public const float Margin = 8f;
        /// <summary>Hauteur d'un badge « x2 MATIN / NUIT » au-dessus de l'ancre du commerce (offset + hauteur).</summary>
        public const float BadgeOffset = 40f, BadgeHeight = 56f;

        private static readonly Vector3[] Corners = new Vector3[4];
        private readonly RectTransform _space;
        private readonly RectTransform[] _sources = new RectTransform[6];
        private readonly Rect[] _zones = new Rect[6];
        private readonly bool[] _active = new bool[6];
        private Rect _screen, _street;

        public UiLayout(RectTransform space) { _space = space; }

        public Rect Screen => _screen;
        /// <summary>Zone libre pour les éléments accrochés au monde.</summary>
        public Rect Street => _street;

        public void Register(UiZone zone, RectTransform source) => _sources[(int)zone] = source;
        public Rect Zone(UiZone zone) => _zones[(int)zone];
        public bool IsActive(UiZone zone) => _active[(int)zone];

        /// <summary>Recalcule les zones (une fois par frame, avant de placer les éléments du monde).</summary>
        public void Refresh()
        {
            _screen = _space.rect;
            for (int i = 0; i < _sources.Length; i++)
            {
                var rt = _sources[i];
                _active[i] = rt != null && rt.gameObject.activeInHierarchy;
                bool rail = i == (int)UiZone.LeftRail || i == (int)UiZone.RightRail;
                _zones[i] = !_active[i] ? default : rail ? ChildrenBounds(rt, out _active[i]) : ToSpace(rt);
            }
            float top = _screen.yMax, bottom = _screen.yMin, left = _screen.xMin, right = _screen.xMax;
            if (_active[(int)UiZone.TopHud]) top = Mathf.Min(top, _zones[(int)UiZone.TopHud].yMin);
            if (_active[(int)UiZone.Toasts]) top = Mathf.Min(top, _zones[(int)UiZone.Toasts].yMin);
            if (_active[(int)UiZone.BottomSheet]) bottom = Mathf.Max(bottom, _zones[(int)UiZone.BottomSheet].yMax);
            // Les rails ne rétrécissent pas la rue : ce sont des îlots exclus (InStreet / ClampToStreet).
            _street = Rect.MinMaxRect(left + Margin, bottom + Margin, Mathf.Max(left + Margin, right - Margin), Mathf.Max(bottom + Margin, top - Margin));
        }

        public Rect ToSpace(RectTransform rt)
        {
            rt.GetWorldCorners(Corners);
            Vector2 a = _space.InverseTransformPoint(Corners[0]), b = _space.InverseTransformPoint(Corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>Union des enfants actifs (un rail ne réserve que ses boutons visibles, pas toute sa colonne).</summary>
        private Rect ChildrenBounds(RectTransform rt, out bool any)
        {
            any = false;
            Rect r = default;
            for (int c = 0; c < rt.childCount; c++)
            {
                var child = rt.GetChild(c) as RectTransform;
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                Rect cr = ToSpace(child);
                r = any ? Rect.MinMaxRect(Mathf.Min(r.xMin, cr.xMin), Mathf.Min(r.yMin, cr.yMin), Mathf.Max(r.xMax, cr.xMax), Mathf.Max(r.yMax, cr.yMax)) : cr;
                any = true;
            }
            return r;
        }

        private bool InRail(Rect r, UiZone zone) => _active[(int)zone] && Overlaps(r, _zones[(int)zone]);

        /// <summary>Position (pivot) corrigée pour qu'un rectangle de cette taille tienne dans la zone de rue.</summary>
        public Vector2 ClampToStreet(Vector2 position, Vector2 size, Vector2 pivot)
        {
            float minX = _street.xMin + size.x * pivot.x, maxX = _street.xMax - size.x * (1f - pivot.x);
            float minY = _street.yMin + size.y * pivot.y, maxY = _street.yMax - size.y * (1f - pivot.y);
            position.x = minX > maxX ? (_street.xMin + _street.xMax) * 0.5f : Mathf.Clamp(position.x, minX, maxX);
            position.y = minY > maxY ? (_street.yMin + _street.yMax) * 0.5f : Mathf.Clamp(position.y, minY, maxY);
            // Hors des rails : poussé vers le centre.
            var r = new Rect(position.x - size.x * pivot.x, position.y - size.y * pivot.y, size.x, size.y);
            if (InRail(r, UiZone.LeftRail)) position.x += _zones[(int)UiZone.LeftRail].xMax + Margin - r.xMin;
            else if (InRail(r, UiZone.RightRail)) position.x -= r.xMax - (_zones[(int)UiZone.RightRail].xMin - Margin);
            return position;
        }

        /// <summary>Le point est-il dans la zone de rue (sinon l'élément est masqué plutôt que collé au bord) ?</summary>
        public bool InStreet(Vector2 position) =>
            _street.Contains(position) &&
            !(_active[(int)UiZone.LeftRail] && _zones[(int)UiZone.LeftRail].Contains(position)) &&
            !(_active[(int)UiZone.RightRail] && _zones[(int)UiZone.RightRail].Contains(position));

        /// <summary>Décalage vertical d'un popup « +12 000 » : au-dessus du badge x2 quand il est affiché.</summary>
        public static float PopupOffset(bool badgeShown) => badgeShown ? BadgeOffset + BadgeHeight * 0.5f + 40f : 0f;

        public static bool Overlaps(Rect a, Rect b) => a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;
    }
}
