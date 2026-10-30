using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>
    /// Particules placeholder (confettis wax, poussière, étoiles, étincelles de rush) : 4 systèmes préalloués,
    /// émission via EmitParams (aucune allocation). Quantités réduites par la qualité et « effets réduits ».
    /// </summary>
    public sealed class EffectsFx
    {
        private readonly JuiceConfig _juice;
        private readonly ParticleSystem _confetti, _dust, _stars, _sparks;
        private float _particleScale = 1f;

        public EffectsFx(JuiceConfig juice, Material material, Transform parent, Color[] waxColors)
        {
            _juice = juice;
            _confetti = Create("Confetti", parent, material, 400, 1.6f, new Vector2(4f, 8f), 0.12f, 2.2f, waxColors);
            _dust = Create("Dust", parent, material, 200, 0.7f, new Vector2(0.8f, 2.2f), 0.35f, -0.2f,
                new[] { new Color(0.85f, 0.75f, 0.6f, 0.8f), new Color(0.75f, 0.65f, 0.5f, 0.6f) });
            _stars = Create("Stars", parent, material, 150, 0.9f, new Vector2(1.5f, 3.5f), 0.18f, 0.3f,
                new[] { new Color(1f, 0.9f, 0.3f), Color.white });
            _sparks = Create("Sparks", parent, material, 150, 0.45f, new Vector2(2f, 4f), 0.08f, 1.5f,
                new[] { new Color(1f, 0.7f, 0.2f), new Color(1f, 0.95f, 0.6f) });
        }

        public void SetParticleScale(float scale) => _particleScale = Mathf.Clamp(scale, 0.1f, 1f);

        public int AliveParticles => _confetti.particleCount + _dust.particleCount + _stars.particleCount + _sparks.particleCount;

        private static ParticleSystem Create(string name, Transform parent, Material material, int max, float lifetime,
            Vector2 speed, float size, float gravity, Color[] colors)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = max;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var gradient = new Gradient();
            var keys = new GradientColorKey[Mathf.Min(8, colors.Length)];
            for (int i = 0; i < keys.Length; i++) keys[i] = new GradientColorKey(colors[i], keys.Length == 1 ? 0f : i / (keys.Length - 1f));
            gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            gradient.mode = GradientMode.Fixed;
            main.startColor = new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.4f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = fade;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            return ps;
        }

        private int Scaled(int count) => Mathf.Max(1, Mathf.RoundToInt(_juice.Particles(count) * _particleScale));

        private static void Emit(ParticleSystem ps, Vector3 position, int count)
        {
            var p = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };
            ps.Emit(p, count);
        }

        public void Confetti(Vector3 position) => Emit(_confetti, position, Scaled(_juice.confettiCount));
        public void Dust(Vector3 position) => Emit(_dust, position, Scaled(_juice.dustCount));
        public void Stars(Vector3 position) => Emit(_stars, position, Scaled(_juice.starCount));
        public void Sparks(Vector3 position) => Emit(_sparks, position, Scaled(_juice.sparkCount));
    }
}
