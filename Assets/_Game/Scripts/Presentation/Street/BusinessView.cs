using PrimeTween;
using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>
    /// Diorama placeholder d'un commerce (prefab généré). Purement visuel : reçoit l'état à afficher
    /// du <see cref="StreetPresenter"/>, ne lit ni ne modifie l'économie.
    /// </summary>
    public sealed class BusinessView : MonoBehaviour
    {
        [Header("Rendu")]
        public MeshFilter stageFilter;
        public MeshRenderer stageRenderer;
        public MeshRenderer lockedShell;
        public Transform shutter;
        public MeshRenderer shutterGlow;
        public MeshRenderer lamp;
        public Transform barRoot;
        public Transform barFill;
        public MeshFilter barFillFilter;
        public Transform pile;

        [Header("Ancres")]
        public Transform counter;
        public Transform employeeAnchor;
        public Transform managerAnchor;
        public Transform labelAnchor;

        public int Index { get; private set; }
        public int Stage { get; private set; } = -1;
        public bool IsLocked { get; private set; } = true;
        public bool HasManagerShown { get; private set; }
        public float Progress { get; private set; }
        public int PileCount { get; private set; }

        private StreetAssets _assets;
        private Mesh _fillMesh, _rushMesh;
        private bool _rush;
        private float _barWidth;
        private Vector3 _stageScale = Vector3.one;
        private Tween _punch, _shutterTween, _pileTween;
        private MaterialPropertyBlock _flashBlock;
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private float _flashUntil;

        public Vector3 PickCenter => transform.position + Vector3.up * 1.2f;
        public Vector3 CounterPosition => counter != null ? counter.position : transform.position;

        public void Setup(int index, StreetAssets assets, Mesh fillMesh, Mesh rushMesh)
        {
            Index = index;
            _assets = assets;
            _fillMesh = fillMesh;
            _rushMesh = rushMesh;
            _barWidth = barRoot != null ? barRoot.localScale.x : 1f;
            _stageScale = stageRenderer.transform.localScale;
            _flashBlock = new MaterialPropertyBlock();
            SetPile(0, 1);
            SetProgress(0f, false);
            ShowLocked(false);
        }

        public void ShowLocked(bool affordable)
        {
            IsLocked = true;
            Stage = -1;
            stageRenderer.enabled = false;
            lockedShell.enabled = true;
            shutter.gameObject.SetActive(true);
            shutter.localPosition = new Vector3(shutter.localPosition.x, 0f, shutter.localPosition.z);
            shutterGlow.enabled = affordable;
            barRoot.gameObject.SetActive(false);
            lamp.enabled = false;
        }

        public void SetAffordable(bool affordable)
        {
            if (IsLocked) shutterGlow.enabled = affordable;
        }

        /// <summary>Rideau qui se lève (ou ouverture immédiate au chargement).</summary>
        public void Unlock(bool animate, float duration)
        {
            IsLocked = false;
            shutterGlow.enabled = false;
            barRoot.gameObject.SetActive(true);
            lamp.enabled = true;
            if (_shutterTween.isAlive) _shutterTween.Stop();
            if (!animate)
            {
                shutter.gameObject.SetActive(false);
                lockedShell.enabled = false;
                return;
            }
            _shutterTween = Tween.LocalPositionY(shutter, 2.4f, duration, Ease.InOutCubic)
                .OnComplete(this, v =>
                {
                    v.shutter.gameObject.SetActive(false);
                    v.lockedShell.enabled = v.Stage < 0;
                }, warnIfTargetDestroyed: false);
        }

        /// <summary>Change le stade visuel ; renvoie vrai si le stade a changé.</summary>
        public bool SetStage(int stage, bool animate, float popDuration)
        {
            if (stage == Stage || stage < 0) return false;
            Stage = stage;
            stageFilter.sharedMesh = _assets.Stage(Index, stage);
            stageRenderer.enabled = true;
            if (!shutter.gameObject.activeSelf) lockedShell.enabled = false;
            if (animate) Juice.Pop(ref _punch, stageRenderer.transform, _stageScale, popDuration);
            else stageRenderer.transform.localScale = _stageScale;
            return true;
        }

        public void ShowManager(bool shown) => HasManagerShown = shown;

        public void SetProgress(float progress01, bool rush)
        {
            Progress = Mathf.Clamp01(progress01);
            if (rush != _rush)
            {
                _rush = rush;
                barFillFilter.sharedMesh = rush ? _rushMesh : _fillMesh;
            }
            float w = rush ? 1f : Progress;
            barFill.localScale = new Vector3(Mathf.Max(0.0001f, w), 1f, 1f);
        }

        public void Punch(float strength, float duration)
        {
            if (Stage < 0) return;
            Juice.Punch(ref _punch, stageRenderer.transform, _stageScale, strength, duration);
        }

        /// <summary>Flash blanc bref (achat) via MaterialPropertyBlock, retiré ensuite pour rester dans le SRP Batcher.</summary>
        public void Flash(float duration)
        {
            if (Stage < 0) return;
            _flashBlock.SetFloat(FlashId, 0.55f);
            stageRenderer.SetPropertyBlock(_flashBlock);
            _flashUntil = Time.unscaledTime + duration;
        }

        public void SetPile(int count, int max)
        {
            PileCount = Mathf.Clamp(count, 0, Mathf.Max(1, max));
            pile.gameObject.SetActive(PileCount > 0);
            pile.localScale = new Vector3(1f, 0.035f * PileCount + 0.01f, 1f);
        }

        public void PunchPile(float duration)
        {
            if (PileCount <= 0) return;
            if (_pileTween.isAlive) _pileTween.Stop();
            _pileTween = Tween.PunchScale(pile, new Vector3(0.25f, 0f, 0.25f), duration, 8f);
        }

        private void LateUpdate()
        {
            if (_flashUntil > 0f && Time.unscaledTime >= _flashUntil)
            {
                _flashUntil = 0f;
                stageRenderer.SetPropertyBlock(null);
            }
        }
    }
}
