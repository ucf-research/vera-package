using UnityEngine;

namespace VERA
{
    public static class VERATweenExtensions
    {
        public static VERATween.TweenHandle<float> TweenAlpha(this CanvasGroup canvasGroup, float to, float duration)
        {
            return VERATween.Value(canvasGroup.gameObject, canvasGroup.alpha, to, duration)
                .SetOnUpdate(alpha => canvasGroup.alpha = alpha);
        }
    }
}
