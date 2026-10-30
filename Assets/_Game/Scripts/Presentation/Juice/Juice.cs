using PrimeTween;
using UnityEngine;

namespace Gargote.Presentation
{
    /// <summary>Squash &amp; stretch sans dérive : chaque punch repart de l'échelle de base (taps rapides).</summary>
    public static class Juice
    {
        public static void Punch(ref Tween handle, Transform target, Vector3 baseScale, float strength, float duration)
        {
            if (handle.isAlive) handle.Stop();
            target.localScale = baseScale;
            if (strength <= 0f || duration <= 0f) return;
            // Écrase en Y, élargit en X/Z : lecture « squash » nette sur des formes simples.
            var s = new Vector3(strength * baseScale.x, -strength * baseScale.y, strength * baseScale.z);
            handle = Tween.PunchScale(target, s, duration, 8f);
        }

        /// <summary>Rebond d'apparition (0 → base) façon « pop ».</summary>
        public static void Pop(ref Tween handle, Transform target, Vector3 baseScale, float duration)
        {
            if (handle.isAlive) handle.Stop();
            target.localScale = baseScale * 0.01f;
            handle = Tween.Scale(target, baseScale, duration, Ease.OutBack);
        }
    }
}
