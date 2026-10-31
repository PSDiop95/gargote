using System;
using Gargote.Core;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gargote.Presentation
{
    /// <summary>Gabarit « préfixe {0} suffixe » découpé une fois : texte dynamique sans allocation via <see cref="TextBuffer"/>.</summary>
    public sealed class LocTemplate
    {
        public readonly string Prefix, Middle, Suffix;

        public LocTemplate(string key)
        {
            string s = Loc.Get(key);
            int a = s.IndexOf("{0}", StringComparison.Ordinal);
            int b = s.IndexOf("{1}", StringComparison.Ordinal);
            if (a < 0) { Prefix = s; Middle = Suffix = string.Empty; return; }
            Prefix = s.Substring(0, a);
            if (b > a) { Middle = s.Substring(a + 3, b - a - 3); Suffix = s.Substring(b + 3); }
            else { Middle = string.Empty; Suffix = s.Substring(a + 3); }
        }

        /// <summary>Formatage ponctuel (ouverture d'écran) : alloue, à ne pas appeler par frame.</summary>
        public string Format(object a, object b = null) => Prefix + a + Middle + (b ?? string.Empty) + Suffix;
    }

    public static class MetaFormat
    {
        public static readonly Color[] RarityColors =
        {
            new Color(0.62f, 0.62f, 0.66f), new Color(0.25f, 0.55f, 0.95f), new Color(0.65f, 0.30f, 0.90f), new Color(1f, 0.72f, 0.15f),
        };

        public static Color Rarity(Rarity r) => RarityColors[(int)r];
        public static string RarityName(Rarity r) => Loc.Get("rarity." + r.ToString().ToLowerInvariant());

        /// <summary>« 3 h 05 », « 12:30 », « 45 s » sans allocation.</summary>
        public static TextBuffer AppendDuration(this TextBuffer b, double seconds)
        {
            long s = (long)Math.Ceiling(Math.Max(0d, seconds));
            long h = s / 3600, m = s / 60 % 60, r = s % 60;
            if (h > 0)
            {
                b.AppendLong(h).Append(" h ");
                if (m < 10) b.Append('0');
                return b.AppendLong(m);
            }
            if (m > 0)
            {
                b.AppendLong(m).Append(':');
                if (r < 10) b.Append('0');
                return b.AppendLong(r);
            }
            return b.AppendLong(r).Append(" s");
        }

        /// <summary>Pourcentage entier : 0,25 → « 25 % ».</summary>
        public static string Percent(double ratio) => Math.Round(ratio * 100d).ToString("0") + " %";

        /// <summary>Durée lisible ponctuelle (allocation, écrans uniquement).</summary>
        public static string Duration(double seconds)
        {
            var tb = new TextBuffer(24);
            tb.AppendDuration(seconds);
            return tb.ToString();
        }
    }

    /// <summary>Petits constructeurs communs aux écrans méta (même thème que l'UI M2).</summary>
    public static class MetaUiKit
    {
        /// <summary>Point rouge en haut à droite d'un bouton (règle unique : <see cref="Gargote.Systems.Meta.AttentionRules"/>).</summary>
        public static Image RedDot(RectTransform parent, StreetAssets assets, float size = 38f)
        {
            var rt = UiFactory.Anchored(parent, "RedDot", new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-8f, -8f), new Vector2(size, size));
            var img = UiFactory.Image(rt, new Color(0.92f, 0.18f, 0.15f), assets.circle);
            rt.gameObject.SetActive(false);
            return img;
        }

        /// <summary>Contour sombre : texte lisible sur la rue claire ou sur une barre remplie (une instance de matériau, à la construction).</summary>
        public static TextMeshProUGUI Outline(TextMeshProUGUI t)
        {
            if (_outlined == null && t.font != null)
            {
                // Un seul matériau partagé (batching conservé, aucune instance par texte).
                _outlined = new Material(t.font.material) { name = "TMP Outline (Gargote)" };
                _outlined.EnableKeyword(ShaderUtilities.Keyword_Outline);
                _outlined.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
                _outlined.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(20, 16, 24, 255));
            }
            if (_outlined != null) t.fontSharedMaterial = _outlined;
            return t;
        }

        private static Material _outlined;

        public static void SetDot(Image dot, bool on)
        {
            if (dot != null && dot.gameObject.activeSelf != on) dot.gameObject.SetActive(on);
        }

        /// <summary>Barre de progression (fond + remplissage horizontal).</summary>
        public static Image Bar(RectTransform parent, string name, HudConfig hud, StreetAssets assets, Color fill,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var back = UiFactory.Rect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            UiFactory.Image(back, hud.panel, assets.rounded);
            var fillRt = UiFactory.Stretch(back, "Fill");
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(-3f, -3f);
            var img = UiFactory.Image(fillRt, fill, assets.rounded);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillAmount = 0f;
            return img;
        }

        public static TextMeshProUGUI Label(RectTransform parent, string name, HudConfig hud, float size, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            TextAlignmentOptions align = TextAlignmentOptions.Left, string text = "")
        {
            var rt = UiFactory.Rect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            var t = UiFactory.Text(rt, hud, size, color, align, text);
            return t;
        }

        /// <summary>Texte multiligne qui s'adapte (descriptions, anecdotes).</summary>
        public static TextMeshProUGUI Paragraph(RectTransform parent, string name, HudConfig hud, float size, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var t = Label(parent, name, hud, size, color, anchorMin, anchorMax, offsetMin, offsetMax, align);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.fontStyle = FontStyles.Normal;
            return t;
        }

        public static UiButton Button(RectTransform parent, string name, HudConfig hud, StreetAssets assets, string label, float size,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color? color = null)
        {
            var cell = UiFactory.Rect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            var b = UiFactory.Button(cell, "Button", hud, assets.rounded, label, size);
            if (color.HasValue)
            {
                b.EnabledColor = color.Value;
                b.Background.color = color.Value;
            }
            return b;
        }

        /// <summary>Liste verticale défilante (contenu ancré en haut). Retourne le contenu à remplir.</summary>
        public static RectTransform ScrollList(RectTransform parent, string name, Vector2 offsetMin, Vector2 offsetMax, out ScrollRect scroll)
        {
            var viewport = UiFactory.Rect(parent, name, Vector2.zero, Vector2.one, offsetMin, offsetMax);
            viewport.gameObject.AddComponent<RectMask2D>();
            UiFactory.Image(viewport, Color.clear, null, true);
            var content = UiFactory.Rect(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            return content;
        }

        /// <summary>Ligne de liste à la position <paramref name="index"/> (hauteur fixe).</summary>
        public static RectTransform Row(RectTransform content, int index, float height, HudConfig hud, StreetAssets assets, string name = "Row")
        {
            var rt = UiFactory.Rect(content, name + index, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -(index + 1) * height + 6f), new Vector2(0f, -index * height - 6f));
            UiFactory.Image(rt, hud.panelLight, assets.rounded, true);
            return rt;
        }
    }

    /// <summary>
    /// Écran modal générique : voile, carte centrale, titre, bouton fermer. Construit une fois au démarrage
    /// (ouverture &lt; 150 ms, aucun Instantiate à l'ouverture), simplement activé / désactivé.
    /// </summary>
    public abstract class MetaScreen
    {
        protected readonly HudConfig Hud;
        protected readonly StreetAssets Assets;
        protected readonly RectTransform Root, Card;
        protected readonly TextMeshProUGUI Title;
        protected readonly UiButton Close;
        private Tween _pop;
        private readonly RectTransform _fit;
        private readonly Vector2 _size;

        public bool IsOpen => Root.gameObject.activeSelf;
        public RectTransform CardRect => Card;
        /// <summary>Fermé par le joueur (bouton ou voile).</summary>
        public Action Closed;

        protected MetaScreen(RectTransform parent, string name, HudConfig hud, StreetAssets assets, Vector2 size, string title, bool closable = true)
        {
            Hud = hud;
            Assets = assets;
            Root = UiFactory.Stretch(parent, name);
            UiFactory.Image(Root, hud.modalVeil, null, true);
            // Carte dessinée pour 1080 de large : réduite à l'ouverture si l'écran est plus étroit (20:9, safe area).
            _fit = UiFactory.Stretch(Root, "Fit");
            _size = size;
            Card = UiFactory.Anchored(_fit, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            // Carte opaque : une fenêtre empilée ne doit jamais transparaître.
            UiFactory.Image(Card, new Color(hud.panel.r, hud.panel.g, hud.panel.b, 1f), assets.rounded, true);
            Title = MetaUiKit.Label(Card, "Title", hud, hud.fontSizeLarge * 0.75f, hud.text, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(130f, -110f), new Vector2(-130f, -20f), TextAlignmentOptions.Center, title);
            if (closable)
            {
                Close = MetaUiKit.Button(Card, "Close", hud, assets, "X", hud.fontSizeMedium, new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-136f, -136f), new Vector2(-16f, -16f), hud.panelLight);
                Close.Clicked = Hide;
            }
            Root.gameObject.SetActive(false);
        }

        public void Show()
        {
            if (!IsOpen)
            {
                Root.gameObject.SetActive(true);
                Root.SetAsLastSibling();
                FitToScreen();
                Juice.Pop(ref _pop, Card, Vector3.one, 0.3f);
            }
            Refresh();
        }

        /// <summary>Échelle uniforme ≤ 1 pour que la carte tienne avec une marge (le rebond d'ouverture reste sur la carte).</summary>
        private void FitToScreen()
        {
            var r = Root.rect;
            float k = Mathf.Min(1f, Mathf.Min((r.width - 24f) / _size.x, (r.height - 48f) / _size.y));
            if (k > 0f) _fit.localScale = new Vector3(k, k, 1f);
        }

        public void Hide()
        {
            if (!IsOpen) return;
            Root.gameObject.SetActive(false);
            OnHidden();
            Closed?.Invoke();
        }

        protected virtual void OnHidden() { }

        /// <summary>Recalcule le contenu (à l'ouverture, puis à intervalle régulier tant que l'écran est ouvert).</summary>
        public abstract void Refresh();

        /// <summary>Animations et minuteries par frame (sans allocation).</summary>
        public virtual void Tick(float dt) { }
    }
}
