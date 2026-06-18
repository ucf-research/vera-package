using UnityEngine;

namespace VERA
{
    internal static class VERATweenExtensions
    {
        public static VERATween.TweenHandle<float> TweenAlpha(this CanvasGroup canvasGroup, float to, float duration)
        {
            return VERATween.Value(canvasGroup.gameObject, canvasGroup.alpha, to, duration)
                .SetOnUpdate(alpha => canvasGroup.alpha = alpha);
        }
    }
}
