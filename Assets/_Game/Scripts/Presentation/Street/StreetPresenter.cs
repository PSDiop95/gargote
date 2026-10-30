using Gargote.Core;
using Gargote.Systems;
using Gargote.Systems.Street;
using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>
    /// Pont économie → rue. Écoute les événements C# des systèmes (déblocage, niveau, palier, gérant, cycle, prestige),
    /// lit l'état pour l'afficher et transmet les intentions du joueur (tap). Aucun calcul de gain ici :
    /// tout montant affiché vient de l'économie (événements ou état).
    /// </summary>
    public sealed class StreetPresenter
    {
        private readonly GameSession _session;
        private readonly StreetConfig _street;
        private readonly CrowdConfig _crowdConfig;
        private readonly JuiceConfig _juice;
        private readonly CrowdRules _crowdRules;
        private readonly MoneyFlowRules _moneyRules;
        private readonly BusinessView[] _views;
        private readonly CrowdDirector _crowd;
        private readonly StaffDirector _staff;
        private readonly SpecialCustomerSystem _specials;
        private readonly SpecialCustomerView _specialView;
        private readonly string[] _specialNames;
        private readonly MoneyFlowBatcher _batcher;
        private readonly CameraRig _rig;
        private readonly MoneyFx _money;
        private readonly EffectsFx _effects;
        private readonly AudioDirector _audio;
        private readonly WorldUi _worldUi;
        private readonly HudView _hud;
        private readonly PersonRenderer _renderer;
        private readonly bool[] _visible;
        private readonly float[] _lastActivity, _pileTimer, _sparkTimer;
        private readonly string[][] _managerLines;
        private readonly double[] _baseRps;
        private BusinessSystem _bound;
        private float _bubbleTimer, _lastBuyTime = -10f, _buyPitch = 1f;
        private uint _random = 0x2545F491u;

        public BusinessView[] Views => _views;
        public CrowdDirector Crowd => _crowd;
        public bool IsVisible(int index) => _visible[index];
        public int SelectedBusiness { get; private set; }

        /// <summary>Tap sur un commerce (après transmission à l'économie) : sélection dans le panneau.</summary>
        public System.Action<int> BusinessSelected;

        public StreetPresenter(GameSession session, StreetConfig street, CrowdConfig crowdConfig, JuiceConfig juice,
            CrowdRules crowdRules, MoneyFlowRules moneyRules, StreetAssets assets, Transform worldRoot, PersonRenderer renderer,
            SpecialCustomerSystem specials, SpecialCustomerRules specialRules, string[] specialNames,
            CameraRig rig, MoneyFx money, EffectsFx effects, AudioDirector audio, WorldUi worldUi, HudView hud)
        {
            _session = session;
            _street = street;
            _crowdConfig = crowdConfig;
            _juice = juice;
            _crowdRules = crowdRules;
            _moneyRules = moneyRules;
            _renderer = renderer;
            _specials = specials;
            _specialNames = specialNames;
            _rig = rig;
            _money = money;
            _effects = effects;
            _audio = audio;
            _worldUi = worldUi;
            _hud = hud;

            int count = session.Businesses.Count;
            var streetGo = new GameObject("Street", typeof(MeshFilter), typeof(MeshRenderer));
            streetGo.transform.SetParent(worldRoot, false);
            streetGo.GetComponent<MeshFilter>().sharedMesh = assets.street;
            streetGo.GetComponent<MeshRenderer>().sharedMaterial = assets.toon;

            Mesh fill = Placeholders.BarFill(street.progressFill), rush = Placeholders.BarFill(street.progressRush);
            _views = new BusinessView[count];
            for (int i = 0; i < count; i++)
            {
                var slot = street.slots[Mathf.Min(i, street.slots.Length - 1)];
                var view = Object.Instantiate(assets.businessPrefab, worldRoot);
                view.name = "Business_" + session.Businesses[i].Definition.Id;
                view.transform.localPosition = slot.position;
                view.Setup(i, assets, fill, rush);
                _views[i] = view;
            }

            var customerBatches = new int[assets.customerMeshes.Length];
            for (int i = 0; i < customerBatches.Length; i++) customerBatches[i] = renderer.Register(assets.customerMeshes[i], crowdRules.MaxTotal);
            var carryBatches = new int[assets.carryMeshes.Length];
            for (int i = 0; i < carryBatches.Length; i++) carryBatches[i] = renderer.Register(assets.carryMeshes[i], crowdRules.MaxTotal);
            _crowd = new CrowdDirector(crowdConfig, crowdRules, renderer, customerBatches, carryBatches, count);
            _crowd.Served = OnCustomerServed;
            _staff = new StaffDirector(crowdConfig, renderer, renderer.Register(assets.employeeMesh, count),
                renderer.Register(assets.managerMesh, count), count);
            var specialBatches = new int[assets.specialMeshes.Length];
            for (int i = 0; i < specialBatches.Length; i++) specialBatches[i] = renderer.Register(assets.specialMeshes[i], 1);
            _specialView = new SpecialCustomerView(specials, specialRules, renderer, specialBatches, crowdConfig);
            specials.Spawned += OnSpecialSpawned;
            specials.Rewarded += OnSpecialRewarded;
            specials.Missed += _ => _worldUi.HideSpecial();

            _batcher = new MoneyFlowBatcher(count, moneyRules);
            _visible = new bool[count];
            _lastActivity = new float[count];
            _pileTimer = new float[count];
            _sparkTimer = new float[count];
            _baseRps = new double[count];
            _managerLines = new string[count][];
            for (int i = 0; i < count; i++)
            {
                var def = session.Businesses[i].Definition;
                _baseRps[i] = def.BaseRevenue / def.BaseCycleSeconds;
                string prefix = "manager." + def.Id;
                int lines = Loc.CountLines(prefix);
                _managerLines[i] = new string[lines];
                for (int l = 0; l < lines; l++) _managerLines[i][l] = Loc.Get(prefix + "." + (l + 1));
            }
            _bubbleTimer = Random(_juice.managerLineInterval.x, _juice.managerLineInterval.y);

            session.Prestiged += (stars, moved) => Resync();
            session.CityChanged += Bind;
            Bind(session.Businesses);
            Resync();
        }

        private void Bind(BusinessSystem businesses)
        {
            if (_bound != null)
            {
                _bound.Unlocked -= OnUnlocked;
                _bound.LevelChanged -= OnLevelChanged;
                _bound.MilestoneReached -= OnMilestone;
                _bound.GlobalMilestoneReached -= OnGlobalMilestone;
                _bound.ManagerHired -= OnManagerHired;
                _bound.CycleCompleted -= OnCycleCompleted;
            }
            _bound = businesses;
            businesses.Unlocked += OnUnlocked;
            businesses.LevelChanged += OnLevelChanged;
            businesses.MilestoneReached += OnMilestone;
            businesses.GlobalMilestoneReached += OnGlobalMilestone;
            businesses.ManagerHired += OnManagerHired;
            businesses.CycleCompleted += OnCycleCompleted;
        }

        /// <summary>Réaligne toutes les vues sur l'état (chargement, prestige), sans animation.</summary>
        public void Resync()
        {
            var businesses = _session.Businesses;
            _crowd.Clear();
            _money.Clear();
            for (int i = 0; i < _views.Length; i++)
            {
                var s = businesses[i];
                var view = _views[i];
                _batcher.Clear(i);
                _staff.Reset(i);
                view.SetPile(0, _juice.pileMaxBills);
                view.ShowManager(s.HasManager);
                if (!s.IsUnlocked)
                {
                    view.ShowLocked(false);
                    continue;
                }
                view.Unlock(false, 0f);
                view.SetStage(VisualStages.StageFor(s.Level, _street.stageLevels), false, 0f);
                if (s.HasManager) _staff.ShowManager(i, false);
            }
        }

        // ---------- Événements de l'économie ----------

        private void OnUnlocked(int i)
        {
            var view = _views[i];
            view.Unlock(true, _juice.shutterOpenDuration);
            view.SetStage(0, true, 0.4f);
            _effects.Dust(view.CounterPosition);
            _effects.Confetti(view.PickCenter + Vector3.up);
            _audio.Play(SoundId.Shutter);
            _audio.Play(SoundId.Unlock);
            _worldUi.Banner(Loc.Get("banner.unlock"));
            _rig.FocusOn(view.transform.position.z);
            _lastActivity[i] = Time.unscaledTime;
            if (!_juice.reducedEffects) _rig.Shake(_juice.shakeStrength * 0.6f, _juice.shakeDuration);
        }

        private void OnLevelChanged(int i, long level)
        {
            var view = _views[i];
            if (view.IsLocked) return;
            int stage = VisualStages.StageFor(level, _street.stageLevels);
            if (view.SetStage(stage, true, 0.45f))
            {
                _effects.Dust(view.CounterPosition);
                _effects.Stars(view.PickCenter + Vector3.up * 1.5f);
            }
            else
            {
                view.Punch(_juice.buyPunch, _juice.punchDuration);
            }
            view.Flash(0.08f);
            // Son d'achat qui monte quand les achats s'enchaînent (le SoundBank limite à 1 son / 50 ms).
            float now = Time.unscaledTime;
            _buyPitch = now - _lastBuyTime < _juice.buyComboWindow ? Mathf.Min(_juice.buyPitchMax, _buyPitch + _juice.buyPitchStep) : 1f;
            _lastBuyTime = now;
            _audio.Play(SoundId.Buy, _buyPitch);
        }

        private void OnMilestone(int i, int milestoneIndex)
        {
            var view = _views[i];
            var s = _session.Businesses[i];
            bool speedCapped = s.CycleSeconds <= _session.EconomyConfig.MinCycleSeconds + 1e-9;
            _worldUi.Banner(Loc.Get(speedCapped ? "banner.revenue_x2" : "banner.speed_x2"));
            _effects.Confetti(view.PickCenter + Vector3.up * 1.5f);
            _rig.ZoomPunch();
            if (!_juice.reducedEffects) _rig.Shake(_juice.shakeStrength, _juice.shakeDuration);
            if (_juice.haptics) Haptics.Light();
            _audio.Play(SoundId.Milestone);
        }

        private void OnGlobalMilestone(int level)
        {
            _worldUi.Banner(Loc.Get("banner.global"));
            for (int i = 0; i < _views.Length; i++) if (_visible[i]) _effects.Confetti(_views[i].PickCenter + Vector3.up);
            _audio.Play(SoundId.Milestone, 1.12f);
        }

        private void OnManagerHired(int i)
        {
            var view = _views[i];
            view.ShowManager(true);
            _staff.ShowManager(i, true);
            _effects.Stars(view.managerAnchor.position + Vector3.up);
            _audio.Play(SoundId.Manager);
            _hud.Pulse();
            if (_managerLines[i].Length > 0) _worldUi.ShowBubble(view.managerAnchor, _managerLines[i][0]);
            _bubbleTimer = Random(_juice.managerLineInterval.x, _juice.managerLineInterval.y);
        }

        private void OnCycleCompleted(int i, BigDouble earned)
        {
            _lastActivity[i] = Time.unscaledTime;
            _crowd.NotifyCycle(i);
            if (_visible[i]) _batcher.Add(i, earned);
        }

        private void OnCustomerServed(int business, Vector3 position)
        {
            if (!_session.Businesses[business].HasManager) _views[business].Punch(_juice.tapPunch * 0.5f, _juice.punchDuration);
        }

        private void OnSpecialSpawned(int index)
        {
            _specialView.Begin(index, _rig.VisibleMinZ, _rig.VisibleMaxZ, 0.6f);
            _worldUi.ShowSpecial(index < _specialNames.Length ? _specialNames[index] : string.Empty);
            _audio.Play(SoundId.Horn, 1.2f, 0.6f);
        }

        private void OnSpecialRewarded(int index, BigDouble bonus)
        {
            Vector3 p = _specialView.Position + Vector3.up;
            _effects.Stars(p);
            _effects.Confetti(p);
            _money.LaunchFromWorld(p, _moneyRules.MaxBills, 1);
            _money.PopupAtWorld(p + Vector3.up, bonus, 1);
            _audio.Play(SoundId.Special);
            _worldUi.HideSpecial();
            _specialView.End();
        }

        // ---------- Intentions du joueur ----------

        /// <summary>Tap sur le monde : client spécial, pile de billets, puis commerce.</summary>
        public bool HandleTap(Vector2 screen)
        {
            var camera = _rig.Camera;
            float pixelsPerUnit = Screen.height / Mathf.Max(0.01f, camera.orthographicSize * 2f);

            if (_specialView.IsActive)
            {
                Vector3 sp = camera.WorldToScreenPoint(_specialView.Position + Vector3.up * 0.7f);
                if (((Vector2)sp - screen).sqrMagnitude < Sq(1.2f * pixelsPerUnit))
                {
                    _specials.TryTap(out _);
                    return true;
                }
            }

            for (int i = 0; i < _views.Length; i++)
            {
                var view = _views[i];
                if (!_visible[i] || view.PileCount <= 0) continue;
                Vector3 pp = camera.WorldToScreenPoint(view.pile.position + Vector3.up * 0.2f);
                if (((Vector2)pp - screen).sqrMagnitude < Sq(0.7f * pixelsPerUnit))
                {
                    CollectPile(i, true);
                    return true;
                }
            }

            int best = -1;
            float bestDistance = Sq(1.9f * pixelsPerUnit);
            for (int i = 0; i < _views.Length; i++)
            {
                if (!_visible[i]) continue;
                Vector3 bp = camera.WorldToScreenPoint(_views[i].PickCenter);
                float d = ((Vector2)bp - screen).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            if (best < 0) return false;

            var view2 = _views[best];
            if (_session.Businesses.Tap(best))
            {
                _lastActivity[best] = Time.unscaledTime;
                _audio.Play(SoundId.Tap);
            }
            else _audio.Play(SoundId.Button, 0.9f);
            view2.Punch(_juice.tapPunch, _juice.punchDuration);
            _rig.FocusOn(view2.transform.position.z);
            SelectedBusiness = best;
            BusinessSelected?.Invoke(best);
            return true;
        }

        private static float Sq(float x) => x * x;

        private void CollectPile(int i, bool tapped)
        {
            var view = _views[i];
            if (view.PileCount <= 0) return;
            view.PunchPile(0.2f);
            _money.LaunchFromWorld(view.pile.position, Mathf.Min(view.PileCount, _moneyRules.MaxBills), 1);
            view.SetPile(0, _juice.pileMaxBills);
            _pileTimer[i] = 0f;
            _audio.Play(SoundId.Collect, tapped ? 1.1f : 1f, tapped ? 1f : 0.6f);
        }

        // ---------- Frame ----------

        /// <param name="dt">Pas réel (animations).</param>
        /// <param name="gameDt">Pas de jeu (accéléré en debug) pour la foule et le staff.</param>
        public void Tick(float dt, float gameDt, float time, double hourOfDay)
        {
            var businesses = _session.Businesses;
            var dayNight = _session.EconomyConfig.DayNight;
            BigDouble totalIncome = businesses.TotalIncomePerSecond();
            BigDouble money = _session.Economy.Money;
            double global = _session.Economy.GlobalRevenueMultiplier * businesses.AllBusinessesMultiplier;
            var inputs = _crowd.Inputs;
            float now = Time.unscaledTime;
            float margin = 2.5f;

            for (int i = 0; i < _views.Length; i++)
            {
                var s = businesses[i];
                var view = _views[i];
                bool visible = _rig.IsVisible(view.PickCenter, margin);
                _visible[i] = visible;
                bool unlocked = s.IsUnlocked;

                bool rush = CrowdDensity.IsRush(s.CycleSeconds, s.IsRunning, _crowdRules);
                if (unlocked) view.SetProgress((float)s.Progress01, rush);

                double dn = DayNight.Multiplier(s.Definition.Affinity, hourOfDay, dayNight);
                ref var input = ref inputs[i];
                input.Unlocked = unlocked;
                input.Visible = visible;
                input.HasManager = s.HasManager;
                input.Running = s.IsRunning;
                input.Rush = rush;
                input.Desired = unlocked ? CrowdDensity.DesiredCustomers(s.RevenuePerSecond * (dn * global), _baseRps[i], _crowdRules) : 0;
                input.ServiceSeconds = CrowdDensity.VisualServiceSeconds(s.CycleSeconds, _crowdRules);
                int side = _street.slots[Mathf.Min(i, _street.slots.Length - 1)].side;
                input.QueueHead = view.CounterPosition + new Vector3(0f, 0f, -0.6f);
                input.QueueDirection = new Vector3(-side, 0f, 0f);
                input.LaneX = side * _street.laneX;

                if (!visible) _batcher.Clear(i);
                else if (_batcher.TryEmit(i, now, totalIncome, out var burst))
                {
                    Vector3 from = view.CounterPosition + Vector3.up * 1.2f;
                    _money.LaunchFromWorld(from, burst.Bills, burst.Cycles);
                    if (!_juice.reducedEffects || burst.IsGrouped) _money.PopupAtWorld(view.labelAnchor.position, burst.Amount, burst.Cycles);
                    view.SetPile(view.PileCount + 1, _juice.pileMaxBills);
                    _audio.Play(SoundId.Cash, 1f, 0.7f);
                }

                if (view.PileCount > 0)
                {
                    _pileTimer[i] += dt;
                    if (_pileTimer[i] >= _juice.pileAutoCollectSeconds || view.PileCount >= _juice.pileMaxBills) CollectPile(i, false);
                }

                if (visible && rush)
                {
                    _sparkTimer[i] -= dt;
                    if (_sparkTimer[i] <= 0f)
                    {
                        _effects.Sparks(view.CounterPosition + Vector3.up * 1.1f);
                        _sparkTimer[i] = 0.5f;
                    }
                }

                if (!unlocked) view.SetAffordable(money >= businesses.QuoteLevels(i, 1).Cost);
                bool hand = unlocked && !s.HasManager && !s.IsRunning && now - _lastActivity[i] > _juice.handHintDelay;
                int badge = (int)DayNightBadges.For(s.Definition.Affinity, hourOfDay, dayNight);
                _worldUi.UpdateBusiness(i, view, visible, !unlocked, unlocked ? BigDouble.Zero : businesses.QuoteLevels(i, 1).Cost,
                    badge, hand, time);

                _staff.Draw(i, view, visible, s.IsRunning, s.CycleSeconds, gameDt, time);
            }

            _crowd.Tick(gameDt, time, _rig.VisibleMinZ, _rig.VisibleMaxZ);
            _specialView.Draw(time);
            if (_specialView.IsActive) _worldUi.SetSpecialPosition(_specialView.Position);
            UpdateBubbles(dt);
        }

        private void UpdateBubbles(float dt)
        {
            _bubbleTimer -= dt;
            if (_bubbleTimer > 0f) return;
            _bubbleTimer = Random(_juice.managerLineInterval.x, _juice.managerLineInterval.y);
            if (_worldUi.BubbleVisible) return;
            int candidates = 0;
            for (int i = 0; i < _views.Length; i++)
                if (_visible[i] && _session.Businesses[i].HasManager && _managerLines[i].Length > 0) candidates++;
            if (candidates == 0) return;
            int pick = (int)(Next() % (uint)candidates);
            for (int i = 0; i < _views.Length; i++)
            {
                if (!_visible[i] || !_session.Businesses[i].HasManager || _managerLines[i].Length == 0) continue;
                if (pick-- > 0) continue;
                var lines = _managerLines[i];
                _worldUi.ShowBubble(_views[i].managerAnchor, lines[(int)(Next() % (uint)lines.Length)]);
                _audio.Play(SoundId.Manager, 1.15f, 0.5f);
                return;
            }
        }

        /// <summary>Part des commerces ouverts et visibles (ambiance sonore).</summary>
        public float Activity()
        {
            int active = 0;
            for (int i = 0; i < _views.Length; i++) if (_visible[i] && !_views[i].IsLocked) active++;
            return _views.Length > 0 ? (float)active / _views.Length : 0f;
        }

        private uint Next()
        {
            _random ^= _random << 13;
            _random ^= _random >> 17;
            _random ^= _random << 5;
            return _random;
        }

        private float Random(float min, float max) => min + (max - min) * ((Next() & 0xFFFFFF) / 16777215f);
    }
}
