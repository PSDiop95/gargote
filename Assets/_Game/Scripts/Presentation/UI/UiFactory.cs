using System;
using Gargote.Core;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gargote.Presentation
{
    /// <summary>Construction de l'UI placeholder en code (thème unique : HudConfig).</summary>
    public static class UiFactory
    {
        public static Canvas Canvas(string name, int sortOrder, HudConfig hud, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            canvas.pixelPerfect = false;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = hud.referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        /// <summary>Rect de taille fixe ancré en un point.</summary>
        public static RectTransform Anchored(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rt = Rect(parent, name, anchor, anchor, Vector2.zero, Vector2.zero);
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name) =>
            Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        public static Image Image(RectTransform rt, Color color, Sprite sprite, bool raycast = false)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static TextMeshProUGUI Text(RectTransform rt, HudConfig hud, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, string initial = "")
        {
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (hud.font != null) text.font = hud.font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.fontStyle = FontStyles.Bold;
            text.text = initial;
            return text;
        }

        public static UiButton Button(Transform parent, string name, HudConfig hud, Sprite sprite, string label, float fontSize)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var image = Image(rt, hud.buttonEnabled, sprite, true);
            var labelRt = Stretch(rt, "Label");
            labelRt.offsetMin = new Vector2(8f, 4f);
            labelRt.offsetMax = new Vector2(-8f, -4f);
            var text = Text(labelRt, hud, fontSize, hud.text, TextAlignmentOptions.Center, label);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMin = fontSize * 0.55f;
            text.fontSizeMax = fontSize;
            var button = rt.gameObject.AddComponent<UiButton>();
            button.Init(image, text, hud);
            return button;
        }
    }

    /// <summary>Écriture de textes dynamiques sans allocation (tampon de caractères + SetCharArray).</summary>
    public sealed class TextBuffer
    {
        private readonly char[] _chars;
        private int _length;

        public TextBuffer(int capacity = 64) => _chars = new char[capacity];

        public TextBuffer Clear()
        {
            _length = 0;
            return this;
        }

        public TextBuffer Append(string text)
        {
            if (text == null) return this;
            for (int i = 0; i < text.Length && _length < _chars.Length; i++) _chars[_length++] = text[i];
            return this;
        }

        public TextBuffer Append(char c)
        {
            if (_length < _chars.Length) _chars[_length++] = c;
            return this;
        }

        public TextBuffer Append(BigDouble value)
        {
            if (_length + NumberFormatter.BufferSize > _chars.Length) return this;
            int n = NumberFormatter.FormatTo(value, Scratch);
            Array.Copy(Scratch, 0, _chars, _length, n);
            _length += n;
            return this;
        }

        private static readonly char[] Scratch = new char[NumberFormatter.BufferSize];

        public TextBuffer AppendLong(long value)
        {
            if (value < 0) { Append('-'); value = -value; }
            int start = _length;
            do
            {
                if (_length >= _chars.Length) break;
                _chars[_length++] = (char)('0' + value % 10);
                value /= 10;
            } while (value > 0);
            Array.Reverse(_chars, start, _length - start);
            return this;
        }

        public void ApplyTo(TMP_Text text) => text.SetCharArray(_chars, 0, _length);

        private int _appliedHash = int.MinValue;

        /// <summary>N'envoie le texte à TMP que s'il a changé (évite les reconstructions de mesh inutiles).</summary>
        public void ApplyIfChanged(TMP_Text text)
        {
            int hash = _length;
            for (int i = 0; i < _length; i++) hash = hash * 31 + _chars[i];
            if (hash == _appliedHash) return;
            _appliedHash = hash;
            text.SetCharArray(_chars, 0, _length);
        }
    }
}
