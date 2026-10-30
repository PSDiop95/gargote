using Gargote.Systems.Street;
using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>État visuel d'un commerce transmis à la foule (lu dans l'économie par le presenter).</summary>
    public struct CrowdBusinessInput
    {
        public bool Unlocked;
        public bool Visible;
        public bool HasManager;
        public bool Running;
        public bool Rush;
        public int Desired;
        public double ServiceSeconds;
        public Vector3 QueueHead;
        /// <summary>Direction de la file (vers la chaussée).</summary>
        public Vector3 QueueDirection;
        public float LaneX;
    }

    /// <summary>
    /// Foule placeholder : capsules + tête, mouvement procédural (rebond de marche, balancement, saut de joie),
    /// voies et waypoints (pas de NavMesh). Représentation plafonnée et découplée des cycles :
    /// la densité vient de <see cref="CrowdDensity"/>, le rythme de service est borné par la vitesse visuelle max.
    /// Agents en tableau de structs préalloué, aucune allocation après construction.
    /// </summary>
    public sealed class CrowdDirector
    {
        private enum AgentState : byte { Inactive, Arriving, Queued, Served, Leaving }

        private struct Agent
        {
            public AgentState State;
            public int Business;
            public int Slot;
            public Vector3 Position;
            public Vector3 Waypoint;
            public Vector3 Target;
            public bool HasWaypoint;
            public float Timer;
            public float Phase;
            public float Yaw;
            public int Variant;
            public int Carry;
        }

        private readonly CrowdConfig _config;
        private readonly CrowdRules _rules;
        private readonly PersonRenderer _renderer;
        private readonly int[] _variantBatches;
        private readonly int[] _carryBatches;
        private readonly Agent[] _agents;
        private readonly CrowdBusinessInput[] _inputs;
        private readonly int[] _desired, _allocation, _present, _queueLength, _pendingServes;
        private readonly bool[] _visible;
        private readonly float[] _spawnCooldown, _serveTimer;
        private int _maxTotal;
        private uint _random = 2463534242u;

        public int ActiveCount { get; private set; }
        public int BusinessCount => _inputs.Length;
        public CrowdBusinessInput[] Inputs => _inputs;

        /// <summary>Levé quand un client est servi devant un commerce (joie, son).</summary>
        public System.Action<int, Vector3> Served;

        public CrowdDirector(CrowdConfig config, CrowdRules rules, PersonRenderer renderer, int[] variantBatches,
            int[] carryBatches, int businessCount)
        {
            _config = config;
            _rules = rules;
            _renderer = renderer;
            _variantBatches = variantBatches;
            _carryBatches = carryBatches;
            _maxTotal = rules.MaxTotal;
            _agents = new Agent[Mathf.Max(1, rules.MaxTotal)];
            _inputs = new CrowdBusinessInput[businessCount];
            _desired = new int[businessCount];
            _allocation = new int[businessCount];
            _present = new int[businessCount];
            _queueLength = new int[businessCount];
            _pendingServes = new int[businessCount];
            _visible = new bool[businessCount];
            _spawnCooldown = new float[businessCount];
            _serveTimer = new float[businessCount];
        }

        /// <summary>Budget global (qualité) : jamais au-delà de la capacité préallouée.</summary>
        public void SetMaxTotal(int max) => _maxTotal = Mathf.Clamp(max, 0, _agents.Length);

        public int PresentAt(int business) => _present[business];

        /// <summary>Clients encore rattachés (en route, en file, servis) à un commerce caché ou fermé. Attendu : 0 après Tick.</summary>
        public int CountAttachedToHidden()
        {
            int count = 0;
            for (int i = 0; i < _agents.Length; i++)
            {
                var state = _agents[i].State;
                if (state == AgentState.Inactive || state == AgentState.Leaving) continue;
                if (!_visible[_agents[i].Business]) count++;
            }
            return count;
        }

        public int Capacity => _agents.Length;

        /// <summary>Fin de cycle d'un commerce sans gérant : le client en tête est servi.</summary>
        public void NotifyCycle(int business)
        {
            if (!_inputs[business].HasManager && _pendingServes[business] < _queueLength[business]) _pendingServes[business]++;
        }

        public void Clear()
        {
            for (int i = 0; i < _agents.Length; i++) _agents[i].State = AgentState.Inactive;
            for (int b = 0; b < _inputs.Length; b++) { _present[b] = 0; _queueLength[b] = 0; _pendingServes[b] = 0; }
            ActiveCount = 0;
        }

        /// <summary>Met à jour la foule. <paramref name="visibleMinZ"/>/<paramref name="visibleMaxZ"/> = sol visible.</summary>
        public void Tick(float dt, float time, float visibleMinZ, float visibleMaxZ)
        {
            int n = _inputs.Length;
            for (int b = 0; b < n; b++)
            {
                _desired[b] = _inputs[b].Unlocked ? Mathf.Min(_inputs[b].Desired, _rules.MaxPerBusiness) : 0;
                _visible[b] = _inputs[b].Visible && _inputs[b].Unlocked;
            }
            CrowdDensity.Allocate(_desired, _visible, _maxTotal, _allocation);

            // Commerce sorti de l'écran (ou fermé) : ses clients disparaissent immédiatement.
            for (int i = 0; i < _agents.Length; i++)
            {
                ref var a = ref _agents[i];
                if (a.State == AgentState.Inactive || a.State == AgentState.Leaving) continue;
                if (!_visible[a.Business]) Deactivate(ref a);
            }

            for (int b = 0; b < n; b++)
            {
                _spawnCooldown[b] -= dt;
                if (!_visible[b]) { _pendingServes[b] = 0; _serveTimer[b] = 0f; continue; }
                if (_present[b] < _allocation[b] && _spawnCooldown[b] <= 0f && ActiveCount < _maxTotal)
                {
                    Spawn(b, visibleMinZ, visibleMaxZ);
                    _spawnCooldown[b] = _inputs[b].Rush ? _config.spawnInterval * 0.35f : _config.spawnInterval;
                }
                UpdateService(b, dt);
            }

            int active = 0;
            for (int i = 0; i < _agents.Length; i++)
            {
                ref var a = ref _agents[i];
                if (a.State == AgentState.Inactive) continue;
                Step(ref a, dt, visibleMinZ, visibleMaxZ);
                if (a.State == AgentState.Inactive) continue;
                active++;
                Draw(ref a, time);
            }
            ActiveCount = active;
        }

        private void UpdateService(int b, float dt)
        {
            int head = FindQueued(b, 0);
            if (head < 0 || !Arrived(ref _agents[head])) return;
            var input = _inputs[b];
            bool serve = false;
            if (input.HasManager || input.Rush)
            {
                _serveTimer[b] += dt;
                if (_serveTimer[b] >= input.ServiceSeconds)
                {
                    _serveTimer[b] = 0f;
                    serve = true;
                }
            }
            else if (_pendingServes[b] > 0)
            {
                _pendingServes[b]--;
                serve = true;
            }
            if (!serve) return;

            ref var a = ref _agents[head];
            a.State = AgentState.Served;
            a.Timer = _config.joyJumpDuration;
            a.Carry = (int)(NextRandom() % 2u);
            _queueLength[b]--;
            Served?.Invoke(b, a.Position);
            // La file avance d'un cran.
            for (int i = 0; i < _agents.Length; i++)
            {
                ref var q = ref _agents[i];
                if (q.Business != b || (q.State != AgentState.Queued && q.State != AgentState.Arriving) || q.Slot <= 0) continue;
                q.Slot--;
                q.Target = SlotPosition(b, q.Slot);
                if (q.State == AgentState.Queued) q.HasWaypoint = false;
            }
        }

        private int FindQueued(int business, int slot)
        {
            for (int i = 0; i < _agents.Length; i++)
            {
                ref var a = ref _agents[i];
                if (a.Business == business && a.Slot == slot && (a.State == AgentState.Queued || a.State == AgentState.Arriving))
                    return i;
            }
            return -1;
        }

        private static bool Arrived(ref Agent a) => a.State == AgentState.Queued && !a.HasWaypoint &&
                                                    (a.Position - a.Target).sqrMagnitude < 0.0025f;

        private Vector3 SlotPosition(int b, int slot) =>
            _inputs[b].QueueHead + _inputs[b].QueueDirection * (_rules.MaxPerBusiness > 0 ? slot * QueueSpacing : 0f);

        private float QueueSpacing => 0.62f * _config.personScale / 0.9f;

        private void Spawn(int b, float minZ, float maxZ)
        {
            int index = -1;
            for (int i = 0; i < _agents.Length; i++)
                if (_agents[i].State == AgentState.Inactive) { index = i; break; }
            if (index < 0) return;

            var input = _inputs[b];
            ref var a = ref _agents[index];
            bool fromBottom = (NextRandom() & 1u) == 0u;
            float z = fromBottom ? minZ - _config.offscreenMargin : maxZ + _config.offscreenMargin;
            a.State = AgentState.Arriving;
            a.Business = b;
            a.Slot = _queueLength[b];
            a.Position = new Vector3(input.LaneX, 0f, z);
            a.Target = SlotPosition(b, a.Slot);
            a.Waypoint = new Vector3(input.LaneX, 0f, a.Target.z);
            a.HasWaypoint = true;
            a.Phase = (NextRandom() % 1000u) * 0.001f * Mathf.PI * 2f;
            a.Variant = (int)(NextRandom() % (uint)Mathf.Max(1, _variantBatches.Length));
            a.Carry = -1;
            a.Timer = 0f;
            _queueLength[b]++;
            _present[b]++;
            ActiveCount++;
        }

        private void Step(ref Agent a, float dt, float minZ, float maxZ)
        {
            float speed = _inputs[a.Business].Rush ? _config.rushWalkSpeed : _config.walkSpeed;
            switch (a.State)
            {
                case AgentState.Arriving:
                case AgentState.Queued:
                {
                    Vector3 goal = a.HasWaypoint ? a.Waypoint : a.Target;
                    if (MoveTowards(ref a, goal, speed * dt))
                    {
                        if (a.HasWaypoint) a.HasWaypoint = false;
                        else
                        {
                            a.State = AgentState.Queued;
                            a.Yaw = 0f; // face au comptoir (+Z)
                        }
                    }
                    break;
                }
                case AgentState.Served:
                    a.Timer -= dt;
                    if (a.Timer > 0f) break;
                    a.State = AgentState.Leaving;
                    a.Waypoint = new Vector3(_inputs[a.Business].LaneX, 0f, a.Position.z);
                    a.HasWaypoint = true;
                    // Repart vers le bord d'écran le plus proche.
                    float exitZ = a.Position.z - minZ < maxZ - a.Position.z ? minZ - _config.offscreenMargin : maxZ + _config.offscreenMargin;
                    a.Target = new Vector3(_inputs[a.Business].LaneX, 0f, exitZ);
                    _present[a.Business]--;
                    break;
                case AgentState.Leaving:
                    if (MoveTowards(ref a, a.HasWaypoint ? a.Waypoint : a.Target, speed * dt))
                    {
                        if (a.HasWaypoint) a.HasWaypoint = false;
                        else a.State = AgentState.Inactive;
                    }
                    break;
            }
        }

        private bool MoveTowards(ref Agent a, Vector3 goal, float maxStep)
        {
            Vector3 delta = goal - a.Position;
            float dist = delta.magnitude;
            if (dist <= maxStep || dist < 1e-4f)
            {
                a.Position = goal;
                return true;
            }
            a.Position += delta * (maxStep / dist);
            a.Yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg + 180f;
            a.Phase += maxStep * _config.bobFrequency;
            return false;
        }

        private void Deactivate(ref Agent a)
        {
            if (a.State == AgentState.Arriving || a.State == AgentState.Queued) _queueLength[a.Business]--;
            if (a.State != AgentState.Leaving) _present[a.Business]--;
            a.State = AgentState.Inactive;
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
        }

        private void Draw(ref Agent a, float time)
        {
            float y = 0f, roll = 0f;
            bool walking = a.State == AgentState.Arriving || a.State == AgentState.Leaving ||
                           (a.State == AgentState.Queued && (a.HasWaypoint || (a.Position - a.Target).sqrMagnitude > 0.0025f));
            if (walking) y = Mathf.Abs(Mathf.Sin(a.Phase)) * _config.bobHeight;
            else if (a.State == AgentState.Served)
            {
                float t = 1f - Mathf.Clamp01(a.Timer / Mathf.Max(0.01f, _config.joyJumpDuration));
                y = Mathf.Sin(t * Mathf.PI) * _config.joyJumpHeight;
            }
            else roll = Mathf.Sin(time * _config.idleSwayFrequency + a.Phase) * _config.idleSwayDegrees;

            // Les personnages regardent vers -Z par construction : yaw 180 = face au comptoir.
            var rotation = Quaternion.Euler(0f, a.State == AgentState.Queued && !walking ? 180f : a.Yaw, roll);
            float s = _config.personScale;
            var matrix = Matrix4x4.TRS(a.Position + new Vector3(0f, y, 0f), rotation, new Vector3(s, s, s));
            _renderer.Add(_variantBatches[a.Variant], matrix);
            if (a.Carry >= 0 && a.Carry < _carryBatches.Length)
            {
                Vector3 hand = a.Position + rotation * new Vector3(0.32f * s, 0.45f * s + y, -0.05f);
                _renderer.Add(_carryBatches[a.Carry], Matrix4x4.TRS(hand, rotation, new Vector3(s, s, s)));
            }
        }

        private uint NextRandom()
        {
            _random ^= _random << 13;
            _random ^= _random >> 17;
            _random ^= _random << 5;
            return _random;
        }
    }
}
