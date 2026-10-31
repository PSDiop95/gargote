using System;
using Gargote.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gargote.Presentation
{
    /// <summary>
    /// Billets FCFA qui volent en arc vers le compteur, popups « +1,2K » avec rebond, pluie de billets.
    /// Tout en pools préchauffés (UI overlay, batchée avec le canvas), animation manuelle sans allocation.
    /// Purement cosmétique : l'argent est déjà crédité par l'économie.
    /// </summary>
    public sealed class MoneyFx
    {
        private struct Flight
        {
            public bool Active;
            public Vector2 From, Control, To;
            public float Delay, T, Duration, Spin;
            public bool Rain;
        }

        private struct Popup
        {
            public bool Active;
            public Vector2 Origin;
            public float T;
        }

        private readonly JuiceConfig _juice;
        private readonly RectTransform _root;
        private readonly Camera _camera;
        private readonly Image[] _bills;
        private readonly TextMeshProUGUI[] _billLabels;
        private readonly Flight[] _flights;
        private readonly TextMeshProUGUI[] _popupTexts;
        private readonly Popup[] _popups;
        private readonly TextBuffer _buffer = new TextBuffer(48);
        private Func<Vector2> _targetProvider;
        private uint _random = 0x9E3779B9u;

        public int ActiveBills { get; private set; }
        public int ActivePopups { get; private set; }
        public int BillCapacity => _bills.Length;
        public int PopupCapacity => _popupTexts.Length;

        /// <summary>Un billet est arrivé au compteur (pulse + son).</summary>
        public Action BillArrived;

        public MoneyFx(JuiceConfig juice, HudConfig hud, RectTransform root, Camera camera, Sprite billSprite,
            int billCapacity, int popupCapacity)
        {
            _juice = juice;
            _root = root;
            _camera = camera;
            _bills = new Image[billCapacity];
            _billLabels = new TextMeshProUGUI[billCapacity];
            _flights = new Flight[billCapacity];
            for (int i = 0; i < billCapacity; i++)
            {
                var rt = UiFactory.Anchored(root, "Bill", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(juice.billSize * 1.6f, juice.billSize));
                _bills[i] = UiFactory.Image(rt, juice.billColor, billSprite);
                var labelRt = UiFactory.Stretch(rt, "Count");
                _billLabels[i] = UiFactory.Text(labelRt, hud, juice.billSize * 0.55f, Color.black);
                _billLabels[i].gameObject.SetActive(false);
                rt.gameObject.SetActive(false);
            }
            _popupTexts = new TextMeshProUGUI[popupCapacity];
            _popups = new Popup[popupCapacity];
            for (int i = 0; i < popupCapacity; i++)
            {
                var rt = UiFactory.Anchored(root, "Popup", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero,
                    new Vector2(500f, juice.popupFontSize * 1.4f));
                var text = UiFactory.Text(rt, hud, juice.popupFontSize, hud.money);
                text.outlineWidth = 0.25f;
                text.outlineColor = new Color32(30, 20, 10, 255);
                _popupTexts[i] = text;
                rt.gameObject.SetActive(false);
            }
        }

        /// <summary>Fournit la position (locale au root) du compteur FCFA du HUD.</summary>
        public void SetTarget(Func<Vector2> targetProvider) => _targetProvider = targetProvider;

        public bool WorldToLocal(Vector3 world, out Vector2 local)
        {
            Vector3 screen = _camera.WorldToScreenPoint(world);
            local = default;
            if (screen.z < 0f) return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out local);
        }

        /// <summary>Envol de billets depuis un point du monde. <paramref name="cycles"/> &gt; 1 = gros billet « xN ».</summary>
        public void LaunchFromWorld(Vector3 world, int bills, long cycles)
        {
            if (!WorldToLocal(world, out var from)) return;
            Launch(from, bills, cycles);
        }

        public void Launch(Vector2 from, int bills, long cycles)
        {
            Vector2 to = _targetProvider != null ? _targetProvider() : Vector2.zero;
            bool grouped = cycles > 1;
            int count = grouped ? 1 : Mathf.Max(1, bills);
            for (int k = 0; k < count; k++)
            {
                int i = FreeBill();
                if (i < 0) return;
                ref var f = ref _flights[i];
                f.Active = true;
                f.Rain = false;
                f.From = from + new Vector2(Rand(-30f, 30f), Rand(-10f, 20f));
                f.To = to;
                Vector2 mid = (f.From + f.To) * 0.5f;
                f.Control = mid + new Vector2(Rand(-120f, 120f), _juice.billArcHeight + Rand(0f, 80f));
                f.Delay = k * _juice.billStagger;
                f.T = 0f;
                f.Duration = _juice.billFlightDuration * Rand(0.9f, 1.1f);
                f.Spin = Rand(-360f, 360f);
                var image = _bills[i];
                image.color = grouped ? _juice.bigBillColor : _juice.billColor;
                image.rectTransform.localScale = grouped ? Vector3.one * 1.5f : Vector3.one;
                image.rectTransform.anchoredPosition = f.From;
                image.gameObject.SetActive(true);
                var label = _billLabels[i];
                label.gameObject.SetActive(grouped);
                if (grouped) _buffer.Clear().Append('x').AppendLong(cycles).ApplyTo(label);
                ActiveBills++;
            }
        }

        /// <summary>Pluie de billets (écran hors ligne) : tombent du haut et filent vers le compteur.</summary>
        public void Rain(int count, float width, float height)
        {
            Vector2 to = _targetProvider != null ? _targetProvider() : Vector2.zero;
            for (int k = 0; k < count; k++)
            {
                int i = FreeBill();
                if (i < 0) return;
                ref var f = ref _flights[i];
                f.Active = true;
                f.Rain = true;
                f.From = new Vector2(Rand(-width * 0.5f, width * 0.5f), height * 0.5f + 80f);
                f.Control = new Vector2(f.From.x * 0.8f, Rand(-height * 0.3f, height * 0.1f));
                f.To = to;
                f.Delay = Rand(0f, 1.2f);
                f.T = 0f;
                f.Duration = Rand(1.1f, 1.7f);
                f.Spin = Rand(-540f, 540f);
                var image = _bills[i];
                image.color = _juice.billColor;
                image.rectTransform.localScale = Vector3.one;
                image.rectTransform.anchoredPosition = f.From;
                image.gameObject.SetActive(true);
                _billLabels[i].gameObject.SetActive(false);
                ActiveBills++;
            }
        }

        /// <summary>Popup « +1,2K » (préfixe optionnel, ex. « x12 ») au-dessus d'un point du monde.</summary>
        private Vector2 PopupSize => new Vector2(500f, _juice.popupFontSize * 1.4f);

        /// <summary>Zones réservées : un popup hors de la rue n'est pas affiché, sinon il y est maintenu.</summary>
        public UiLayout Layout;

        public void PopupAtWorld(Vector3 world, BigDouble amount, long cycles, float yOffset = 0f)
        {
            if (!WorldToLocal(world, out var local)) return;
            local.y += yOffset;
            if (Layout != null)
            {
                if (!Layout.InStreet(local)) return;
                // La montée du popup reste dans la rue : on part plus bas si besoin.
                local = Layout.ClampToStreet(local, new Vector2(PopupSize.x, PopupSize.y + _juice.popupRise), new Vector2(0.5f, 0f));
            }
            int i = -1;
            for (int k = 0; k < _popups.Length; k++) if (!_popups[k].Active) { i = k; break; }
            if (i < 0)
            {
                // Pool plein (effets regroupés) : on recycle le plus ancien.
                float oldest = -1f;
                for (int k = 0; k < _popups.Length; k++) if (_popups[k].T > oldest) { oldest = _popups[k].T; i = k; }
            }
            else ActivePopups++;
            ref var p = ref _popups[i];
            p.Active = true;
            p.Origin = local;
            p.T = 0f;
            var text = _popupTexts[i];
            _buffer.Clear();
            if (cycles > 1) _buffer.Append('x').AppendLong(cycles).Append(' ');
            _buffer.Append('+').Append(amount).ApplyTo(text);
            text.rectTransform.anchoredPosition = local;
            text.rectTransform.localScale = Vector3.one * 0.3f;
            text.alpha = 1f;
            text.gameObject.SetActive(true);
        }

        public void Update(float dt)
        {
            for (int i = 0; i < _flights.Length; i++)
            {
                ref var f = ref _flights[i];
                if (!f.Active) continue;
                if (f.Delay > 0f) { f.Delay -= dt; continue; }
                f.T += dt / f.Duration;
                float t = Mathf.Clamp01(f.T);
                // Pluie : chute accélérée ; envol : départ doux puis aspiration vers le compteur.
                float e = f.Rain ? t * t : t * t * (3f - 2f * t);
                Vector2 p = Bezier(f.From, f.Control, f.To, e);
                var rt = _bills[i].rectTransform;
                rt.anchoredPosition = p;
                rt.localEulerAngles = new Vector3(0f, 0f, f.Spin * t);
                // Étirement en vol, écrasement à l'arrivée.
                float stretch = 1f + Mathf.Sin(t * Mathf.PI) * 0.25f;
                float baseScale = _billLabels[i].gameObject.activeSelf ? 1.5f : 1f;
                rt.localScale = new Vector3(baseScale / stretch, baseScale * stretch, 1f) * (1f - t * 0.35f);
                if (t < 1f) continue;
                f.Active = false;
                rt.gameObject.SetActive(false);
                ActiveBills--;
                BillArrived?.Invoke();
            }

            for (int i = 0; i < _popups.Length; i++)
            {
                ref var p = ref _popups[i];
                if (!p.Active) continue;
                p.T += dt / Mathf.Max(0.05f, _juice.popupDuration);
                float t = Mathf.Clamp01(p.T);
                var text = _popupTexts[i];
                float pop = t < 0.25f ? OutBack(t / 0.25f) : 1f;
                text.rectTransform.localScale = Vector3.one * pop;
                text.rectTransform.anchoredPosition = p.Origin + new Vector2(0f, _juice.popupRise * (1f - (1f - t) * (1f - t)));
                text.alpha = t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.35f;
                if (t < 1f) continue;
                p.Active = false;
                text.gameObject.SetActive(false);
                ActivePopups--;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _flights.Length; i++) { _flights[i].Active = false; _bills[i].gameObject.SetActive(false); }
            for (int i = 0; i < _popups.Length; i++) { _popups[i].Active = false; _popupTexts[i].gameObject.SetActive(false); }
            ActiveBills = 0;
            ActivePopups = 0;
        }

        private int FreeBill()
        {
            for (int i = 0; i < _flights.Length; i++) if (!_flights[i].Active) return i;
            return -1;
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        private static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        private float Rand(float min, float max)
        {
            _random ^= _random << 13;
            _random ^= _random >> 17;
            _random ^= _random << 5;
            return min + (max - min) * ((_random & 0xFFFFFF) / 16777215f);
        }
    }
}
