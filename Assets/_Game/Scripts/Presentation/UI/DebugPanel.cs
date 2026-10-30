using System;
using TMPro;
using Unity.Profiling;
using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>
    /// Menu debug (éditeur et builds de développement uniquement) : boutons fournis par le GameRoot
    /// et overlay FPS / batches / triangles / allocations / mémoire via ProfilerRecorder.
    /// </summary>
    public sealed class DebugPanel : IDisposable
    {
        public static bool Available =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

        private readonly RectTransform _panel;
        private readonly TextMeshProUGUI _stats;
        private readonly HudConfig _hud;
        private readonly StreetAssets _assets;
        private int _buttons;
        private ProfilerRecorder _batches, _triangles, _gcAlloc, _systemMemory, _setPass;
        private float _fpsAccum, _fpsTimer, _fps, _minFps = float.MaxValue;
        private int _fpsFrames;
        private bool _statsVisible;

        public Func<string> Extra;

        public DebugPanel(RectTransform safeArea, HudConfig hud, StreetAssets assets)
        {
            _hud = hud;
            _assets = assets;
            var toggleCell = UiFactory.Anchored(safeArea, "DebugToggle", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -hud.topBarHeight - 30f), new Vector2(130f, 90f));
            var toggle = UiFactory.Button(toggleCell, "Button", hud, assets.rounded, "DBG", hud.fontSizeSmall);
            toggle.EnabledColor = new Color(0.6f, 0.15f, 0.6f, 0.85f);
            toggle.Background.color = toggle.EnabledColor;

            _panel = UiFactory.Anchored(safeArea, "DebugPanel", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -hud.topBarHeight - 130f), new Vector2(520f, 1100f));
            UiFactory.Image(_panel, new Color(0.05f, 0.05f, 0.08f, 0.92f), assets.rounded, true);
            _panel.gameObject.SetActive(false);
            toggle.Clicked = () => _panel.gameObject.SetActive(!_panel.gameObject.activeSelf);

            var statsRt = UiFactory.Anchored(safeArea, "DebugStats", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-16f, -hud.topBarHeight - 30f), new Vector2(520f, 260f));
            UiFactory.Image(statsRt, new Color(0f, 0f, 0f, 0.55f), assets.rounded);
            _stats = UiFactory.Text(UiFactory.Stretch(statsRt, "Text"), hud, hud.fontSizeSmall * 0.75f, Color.white, TextAlignmentOptions.TopLeft);
            _stats.margin = new Vector4(14f, 10f, 10f, 10f);
            _stats.textWrappingMode = TextWrappingModes.Normal;
            statsRt.gameObject.SetActive(false);

            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _systemMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        }

        public void AddButton(string label, Action onClick)
        {
            const float height = 96f, gap = 10f;
            var cell = UiFactory.Anchored(_panel, "Debug" + _buttons, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -16f - _buttons * (height + gap)), new Vector2(480f, height));
            var b = UiFactory.Button(cell, "Button", _hud, _assets.rounded, label, _hud.fontSizeSmall * 0.85f);
            b.Clicked = onClick;
            _buttons++;
        }

        public void ToggleStats()
        {
            _statsVisible = !_statsVisible;
            _stats.transform.parent.gameObject.SetActive(_statsVisible);
        }

        public float MinFps => _minFps;
        public float Fps => _fps;
        public long LastGcAlloc => _gcAlloc.Valid ? _gcAlloc.LastValue : -1;
        public long Batches => _batches.Valid ? _batches.LastValue : -1;

        public void ResetMinFps() => _minFps = float.MaxValue;

        public void Tick(float dt)
        {
            _fpsAccum += dt;
            _fpsFrames++;
            _fpsTimer += dt;
            if (dt > 0f && Time.frameCount > 30) _minFps = Mathf.Min(_minFps, 1f / dt);
            if (_fpsTimer < 0.5f || !_statsVisible) return;
            _fps = _fpsFrames / Mathf.Max(0.0001f, _fpsAccum);
            _fpsAccum = 0f;
            _fpsFrames = 0;
            _fpsTimer = 0f;
            // SetText avec arguments numériques : aucune allocation.
            _stats.SetText("FPS {0:0} (min {1:0})\nBatches {2}  SetPass {3}\nTris {4:0}K\nGC/frame {5} B\nRAM {6:0} Mo",
                _fps, _minFps == float.MaxValue ? 0f : _minFps,
                _batches.Valid ? _batches.LastValue : -1, _setPass.Valid ? _setPass.LastValue : -1,
                (_triangles.Valid ? _triangles.LastValue : 0) / 1000f,
                _gcAlloc.Valid ? _gcAlloc.LastValue : -1,
                (_systemMemory.Valid ? _systemMemory.LastValue : 0) / (1024f * 1024f));
        }

        public void Dispose()
        {
            _batches.Dispose();
            _setPass.Dispose();
            _triangles.Dispose();
            _gcAlloc.Dispose();
            _systemMemory.Dispose();
        }
    }
}
