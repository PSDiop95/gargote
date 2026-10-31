using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Gargote.Presentation
{
    /// <summary>
    /// Fenêtre méta ouverte : la rue 3D est rendue une fois dans une texture (demi-résolution) affichée sous les étiquettes,
    /// le HUD et le voile, puis la caméra est coupée jusqu'à la fermeture. Mesuré sur A23 (Adreno 610) : rue 60 FPS,
    /// fenêtre 60 FPS, les deux ensemble 45 FPS ; rue figée + fenêtre : 60 FPS. La simulation, le HUD et la fenêtre
    /// restent vivants ; seule l'image de la rue (assombrie à 85 %) ne bouge plus.
    /// </summary>
    public sealed class StreetFreeze
    {
        private readonly Camera _camera;
        private readonly RawImage _image;
        private readonly float _scale;
        private RenderTexture _texture;
        private bool _frozen;

        public bool Frozen => _frozen;

        /// <param name="parent">Premier canvas dessiné (WorldCanvas) : l'image passe sous tout le reste de l'UI.</param>
        public StreetFreeze(RectTransform parent, Camera camera, float scale)
        {
            _camera = camera;
            _scale = Mathf.Clamp(scale, 0.25f, 1f);
            var rt = UiFactory.Stretch(parent, "StreetFreeze");
            rt.SetAsFirstSibling();
            _image = rt.gameObject.AddComponent<RawImage>();
            _image.raycastTarget = false;
            _image.enabled = false;
        }

        public void Set(bool freeze)
        {
            if (freeze == _frozen || _camera == null) return;
            if (freeze && !Capture()) return;
            _frozen = freeze;
            _image.enabled = freeze;
            _camera.enabled = !freeze;
        }

        private bool Capture()
        {
            int w = Mathf.Max(64, Mathf.RoundToInt(Screen.width * _scale));
            int h = Mathf.Max(64, Mathf.RoundToInt(Screen.height * _scale));
            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                if (_texture != null) _texture.Release();
                _texture = new RenderTexture(w, h, 24, RenderTextureFormat.Default) { name = "StreetFreeze" };
                _texture.Create();
                _image.texture = _texture;
            }
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = _texture };
            if (!RenderPipeline.SupportsRenderRequest(_camera, request)) return false;
            RenderPipeline.SubmitRenderRequest(_camera, request);
            return true;
        }

        /// <summary>Libère la texture (scène détruite).</summary>
        public void Dispose()
        {
            if (_texture == null) return;
            _texture.Release();
            Object.Destroy(_texture);
            _texture = null;
        }
    }
}
