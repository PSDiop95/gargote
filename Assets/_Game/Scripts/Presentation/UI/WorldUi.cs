using Gargote.Core;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gargote.Presentation
{
    /// <summary>
    /// UI accrochée au monde (overlay, batchée) : coût sur le rideau, badges « x2 MATIN / NUIT », main qui pulse,
    /// bulle de gérant (une seule à l'écran), étiquette du client spécial, bannière centrale.
    /// </summary>
    public sealed class WorldUi
    {
        private sealed class Label
        {
            public RectTransform Cost, Badge, Hand;
            public TextMeshProUGUI CostText, BadgeText;
            public readonly TextBuffer CostBuffer = new TextBuffer(40);
            public int BadgeShown = -1;
        }

        private readonly RectTransform _root;
        private readonly Camera _camera;
        private readonly JuiceConfig _juice;
        private readonly Label[] _labels;
        private readonly RectTransform _bubble, _special, _banner;
        private readonly TextMeshProUGUI _bubbleText, _specialText, _bannerText;
        private readonly string _morning, _night;
        private float _bubbleUntil, _bannerUntil;
        private Transform _bubbleAnchor;
        private Vector3 _specialWorld;
        private bool _specialShown;
        private Tween _bannerTween, _bubbleTween;

        public bool BubbleVisible => _bubble.gameObject.activeSelf;
        /// <summary>Zones réservées : les labels sortent de la rue = masqués, bulle et client spécial = repoussés dedans.</summary>
        public UiLayout Layout;
        public RectTransform SpecialRect => _special;
        public RectTransform BubbleRect => _bubble;
        public RectTransform BannerRect => _banner;
        public RectTransform BadgeRect(int index) => _labels[index].Badge;
        public RectTransform CostRect(int index) => _labels[index].Cost;

        public WorldUi(RectTransform root, Camera camera, HudConfig hud, JuiceConfig juice, StreetAssets assets, int businessCount)
        {
            _root = root;
            _camera = camera;
            _juice = juice;
            _morning = Loc.Get("badge.morning");
            _night = Loc.Get("badge.night");
            _labels = new Label[businessCount];
            var center = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < businessCount; i++)
            {
                var l = new Label();
                l.Cost = UiFactory.Anchored(root, "Cost" + i, center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 76f));
                UiFactory.Image(l.Cost, hud.panel, assets.rounded);
                l.CostText = UiFactory.Text(UiFactory.Stretch(l.Cost, "Text"), hud, hud.fontSizeSmall, hud.money);
                l.Badge = UiFactory.Anchored(root, "Badge" + i, center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210f, 56f));
                UiFactory.Image(l.Badge, hud.accent, assets.rounded);
                l.BadgeText = UiFactory.Text(UiFactory.Stretch(l.Badge, "Text"), hud, hud.fontSizeSmall * 0.85f, hud.text);
                l.Hand = UiFactory.Anchored(root, "Hand" + i, center, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(96f, 96f));
                UiFactory.Image(l.Hand, Color.white, assets.hand);
                l.Cost.gameObject.SetActive(false);
                l.Badge.gameObject.SetActive(false);
                l.Hand.gameObject.SetActive(false);
                _labels[i] = l;
            }

            _bubble = UiFactory.Anchored(root, "Bubble", center, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(560f, 120f));
            UiFactory.Image(_bubble, new Color(1f, 1f, 1f, 0.96f), assets.rounded);
            _bubbleText = UiFactory.Text(UiFactory.Stretch(_bubble, "Text"), hud, hud.fontSizeSmall, new Color(0.15f, 0.1f, 0.1f));
            _bubbleText.textWrappingMode = TextWrappingModes.Normal;
            _bubbleText.margin = new Vector4(18f, 8f, 18f, 8f);
            _bubble.gameObject.SetActive(false);

            _special = UiFactory.Anchored(root, "Special", center, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(420f, 70f));
            UiFactory.Image(_special, hud.accent, assets.rounded);
            _specialText = UiFactory.Text(UiFactory.Stretch(_special, "Text"), hud, hud.fontSizeSmall, hud.text);
            _special.gameObject.SetActive(false);

            _banner = UiFactory.Anchored(root, "Banner", new Vector2(0.5f, 0.62f), center, Vector2.zero, new Vector2(1000f, 160f));
            _bannerText = UiFactory.Text(_banner, hud, hud.fontSizeLarge * 1.3f, hud.accent);
            _bannerText.outlineWidth = 0.3f;
            _bannerText.outlineColor = new Color32(40, 20, 10, 255);
            _banner.gameObject.SetActive(false);
        }

        private bool Place(RectTransform rt, Vector3 world, Vector2 offset, bool clamp = false)
        {
            Vector3 screen = _camera.WorldToScreenPoint(world);
            if (screen.z < 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local)) return false;
            Vector2 p = local + offset;
            if (Layout != null)
            {
                if (clamp) p = Layout.ClampToStreet(p, rt.sizeDelta, rt.pivot);
                else if (!Layout.InStreet(p)) return false;
            }
            rt.anchoredPosition = p;
            return true;
        }

        /// <summary>Met à jour les labels d'un commerce (appelé chaque frame pour les commerces visibles).</summary>
        public void UpdateBusiness(int index, BusinessView view, bool visible, bool locked, BigDouble unlockCost,
            int badge, bool showHand, float time)
        {
            var l = _labels[index];
            bool showCost = visible && locked;
            if (l.Cost.gameObject.activeSelf != showCost) l.Cost.gameObject.SetActive(showCost);
            if (showCost)
            {
                l.CostBuffer.Clear().Append(unlockCost).Append(" FCFA").ApplyIfChanged(l.CostText);
                if (!Place(l.Cost, view.transform.position + new Vector3(0f, 1.1f, -1.4f), Vector2.zero)) l.Cost.gameObject.SetActive(false);
            }

            bool showBadge = visible && !locked && badge > 0 && (Layout == null || OnScreenInStreet(view.labelAnchor.position, UiLayout.BadgeOffset));
            if (l.Badge.gameObject.activeSelf != showBadge) l.Badge.gameObject.SetActive(showBadge);
            if (showBadge)
            {
                if (l.BadgeShown != badge)
                {
                    l.BadgeShown = badge;
                    l.BadgeText.text = badge == 1 ? _morning : _night;
                }
                Place(l.Badge, view.labelAnchor.position, new Vector2(0f, UiLayout.BadgeOffset));
            }

            bool hand = visible && showHand;
            if (l.Hand.gameObject.activeSelf != hand) l.Hand.gameObject.SetActive(hand);
            if (hand)
            {
                float pulse = 1f + 0.15f * Mathf.Sin(time * 6f);
                l.Hand.localScale = new Vector3(pulse, pulse, 1f);
                if (!Place(l.Hand, view.PickCenter, new Vector2(30f, -10f + 14f * Mathf.Sin(time * 6f)))) l.Hand.gameObject.SetActive(false);
            }
        }

        /// <summary>Le badge d'un commerce est-il affiché (les popups se placent alors au-dessus) ?</summary>
        public bool IsBadgeShown(int index) => _labels[index].Badge.gameObject.activeSelf;

        private bool OnScreenInStreet(Vector3 world, float yOffset)
        {
            Vector3 screen = _camera.WorldToScreenPoint(world);
            if (screen.z < 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local)) return false;
            return Layout.InStreet(local + new Vector2(0f, yOffset));
        }

        /// <summary>Affiche une réplique de gérant ; refusé si une bulle est déjà à l'écran.</summary>
        public bool ShowBubble(Transform anchor, string line)
        {
            if (BubbleVisible) return false;
            _bubbleAnchor = anchor;
            _bubbleText.text = line;
            _bubble.gameObject.SetActive(true);
            _bubbleUntil = Time.unscaledTime + _juice.bubbleDuration;
            Juice.Pop(ref _bubbleTween, _bubble, Vector3.one, 0.3f);
            return true;
        }

        public void ShowSpecial(string name) { _specialShown = true; _specialText.text = name; }
        public void HideSpecial() { _specialShown = false; _special.gameObject.SetActive(false); }
        public void SetSpecialPosition(Vector3 world) => _specialWorld = world;

        /// <summary>Bannière centrale (« VITESSE x2 ! », déblocage…) avec pop.</summary>
        public void Banner(string text)
        {
            _bannerText.text = text;
            _banner.gameObject.SetActive(true);
            _bannerUntil = Time.unscaledTime + _juice.bannerDuration;
            Juice.Pop(ref _bannerTween, _banner, Vector3.one, 0.35f);
            _bannerText.alpha = 1f;
        }

        public void Tick()
        {
            float now = Time.unscaledTime;
            if (BubbleVisible)
            {
                bool placed = _bubbleAnchor != null && Place(_bubble, _bubbleAnchor.position + Vector3.up * 1.5f, Vector2.zero, true);
                if (now >= _bubbleUntil || !placed) _bubble.gameObject.SetActive(false);
            }
            if (_specialShown)
            {
                bool placed = Place(_special, _specialWorld + Vector3.up * 1.6f, Vector2.zero, true);
                if (_special.gameObject.activeSelf != placed) _special.gameObject.SetActive(placed);
            }
            if (_banner.gameObject.activeSelf)
            {
                float left = _bannerUntil - now;
                _bannerText.alpha = Mathf.Clamp01(left / 0.3f);
                if (left <= 0f) _banner.gameObject.SetActive(false);
            }
        }

        public void HideAll()
        {
            foreach (var l in _labels)
            {
                l.Cost.gameObject.SetActive(false);
                l.Badge.gameObject.SetActive(false);
                l.Hand.gameObject.SetActive(false);
            }
            _bubble.gameObject.SetActive(false);
            HideSpecial();
        }
    }
}
