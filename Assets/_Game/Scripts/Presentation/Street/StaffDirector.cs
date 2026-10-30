using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>
    /// Employés (un par commerce ouvert, geste répété au rythme du cycle, plafonné visuellement)
    /// et gérants (personnage distinct, apparition en pop). Dessinés par instancing.
    /// </summary>
    public sealed class StaffDirector
    {
        private readonly CrowdConfig _config;
        private readonly PersonRenderer _renderer;
        private readonly int _employeeBatch, _managerBatch;
        private readonly float[] _gesturePhase, _managerAppear;
        private readonly bool[] _managerShown;

        public StaffDirector(CrowdConfig config, PersonRenderer renderer, int employeeBatch, int managerBatch, int businessCount)
        {
            _config = config;
            _renderer = renderer;
            _employeeBatch = employeeBatch;
            _managerBatch = managerBatch;
            _gesturePhase = new float[businessCount];
            _managerAppear = new float[businessCount];
            _managerShown = new bool[businessCount];
        }

        /// <summary>Gérant embauché : <paramref name="animate"/> = arrivée en pop (sinon présent d'emblée).</summary>
        public void ShowManager(int business, bool animate)
        {
            _managerShown[business] = true;
            _managerAppear[business] = animate ? 0f : 1f;
        }

        public void Reset(int business)
        {
            _managerShown[business] = false;
            _gesturePhase[business] = 0f;
        }

        /// <param name="cycleSeconds">Cycle réel ; le geste est borné entre gestureMin et gestureMax.</param>
        public void Draw(int business, BusinessView view, bool visible, bool working, double cycleSeconds, float dt, float time)
        {
            if (view.IsLocked || view.Stage < 0) return;
            float period = Mathf.Clamp((float)cycleSeconds, _config.gestureMinSeconds, _config.gestureMaxSeconds);
            if (working) _gesturePhase[business] += dt / period;
            if (_managerShown[business] && _managerAppear[business] < 1f)
                _managerAppear[business] = Mathf.Min(1f, _managerAppear[business] + dt / 0.45f);
            if (!visible) return;

            float s = _config.personScale;
            // Geste « retourner / servir » : écrasement + petit pivot à chaque période.
            float g = working ? Mathf.Sin(_gesturePhase[business] * Mathf.PI * 2f) : 0f;
            float squash = 1f - Mathf.Max(0f, g) * 0.12f;
            float yaw = 180f + g * 22f;
            var employee = view.employeeAnchor;
            _renderer.Add(_employeeBatch, Matrix4x4.TRS(employee.position,
                employee.rotation * Quaternion.Euler(0f, yaw, 0f), new Vector3(s * (2f - squash), s * squash, s)));

            if (!_managerShown[business]) return;
            float a = _managerAppear[business];
            float pop = a >= 1f ? 1f : OutBack(a);
            float sway = Mathf.Sin(time * 1.3f + business) * 4f;
            var manager = view.managerAnchor;
            _renderer.Add(_managerBatch, Matrix4x4.TRS(manager.position,
                manager.rotation * Quaternion.Euler(0f, 180f + sway * 2f, sway), Vector3.one * (s * 1.08f * pop)));
        }

        private static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
