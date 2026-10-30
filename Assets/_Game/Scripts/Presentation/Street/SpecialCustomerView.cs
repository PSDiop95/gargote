using Gargote.Systems.Street;
using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>
    /// Affiche le client spécial actif de <see cref="SpecialCustomerSystem"/> : il traverse la chaussée,
    /// sa position découle du temps restant (le système reste la source de vérité). Tap → système.
    /// </summary>
    public sealed class SpecialCustomerView
    {
        private readonly SpecialCustomerSystem _system;
        private readonly SpecialCustomerRules _rules;
        private readonly PersonRenderer _renderer;
        private readonly int[] _batches;
        private readonly CrowdConfig _config;
        private float _startZ, _endZ, _x;
        private int _shownIndex = -1;

        public bool IsActive => _shownIndex >= 0 && _system.ActiveIndex == _shownIndex;
        public Vector3 Position { get; private set; }

        public SpecialCustomerView(SpecialCustomerSystem system, SpecialCustomerRules rules, PersonRenderer renderer,
            int[] batches, CrowdConfig config)
        {
            _system = system;
            _rules = rules;
            _renderer = renderer;
            _batches = batches;
            _config = config;
        }

        /// <summary>Le système vient de faire apparaître un client : il traverse l'écran visible de haut en bas, face caméra.</summary>
        public void Begin(int index, float visibleMinZ, float visibleMaxZ, float x)
        {
            _shownIndex = index;
            _startZ = visibleMaxZ + _config.offscreenMargin;
            _endZ = visibleMinZ - _config.offscreenMargin;
            _x = x;
        }

        public void End() => _shownIndex = -1;

        public void Draw(float time)
        {
            if (!IsActive) { _shownIndex = -1; return; }
            var def = _rules.Customers[_shownIndex];
            float t = 1f - Mathf.Clamp01((float)(_system.ActiveRemainingSeconds / def.CrossingSeconds));
            float z = Mathf.Lerp(_startZ, _endZ, t);
            float phase = time * _config.bobFrequency * 1.4f;
            float y = Mathf.Abs(Mathf.Sin(phase)) * _config.bobHeight * 1.5f;
            Position = new Vector3(_x, 0f, z);
            float s = _config.personScale * 1.15f;
            int batch = _shownIndex < _batches.Length ? _batches[_shownIndex] : -1;
            // Marche vers -Z : le modèle regarde déjà la caméra.
            _renderer.Add(batch, Matrix4x4.TRS(Position + new Vector3(0f, y, 0f), Quaternion.identity, new Vector3(s, s, s)));
        }
    }
}
