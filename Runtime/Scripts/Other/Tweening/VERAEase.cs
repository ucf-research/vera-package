using UnityEngine;

namespace VERA
{
    public enum VERAEaseType
    {
        Linear,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic,
        InQuart,
        OutQuart,
        InOutQuart,
        InQuint,
        OutQuint,
        InOutQuint,
        InSine,
        OutSine,
        InOutSine,
        InExpo,
        OutExpo,
        InOutExpo,
        InCirc,
        OutCirc,
        InOutCirc,
        InBack,
        OutBack,
        InOutBack,
        InElastic,
        OutElastic,
        InOutElastic,
        InBounce,
        OutBounce,
        InOutBounce
    }

    public static class VERAEase
    {
        private const float BackOvershoot = 1.70158f;
        private const float TwoPi = Mathf.PI * 2f;
        private const float HalfPi = Mathf.PI * 0.5f;

        public static float Evaluate(VERAEaseType ease, float t)
        {
            t = Mathf.Clamp01(t);

            switch (ease)
            {
                case VERAEaseType.Linear:
                    return t;
                case VERAEaseType.InQuad:
                    return t * t;
                case VERAEaseType.OutQuad:
                    return 1f - (1f - t) * (1f - t);
                case VERAEaseType.InOutQuad:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
                case VERAEaseType.InCubic:
                    return t * t * t;
                case VERAEaseType.OutCubic:
                    return 1f - Mathf.Pow(1f - t, 3f);
                case VERAEaseType.InOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
                case VERAEaseType.InQuart:
                    return t * t * t * t;
                case VERAEaseType.OutQuart:
                    return 1f - Mathf.Pow(1f - t, 4f);
                case VERAEaseType.InOutQuart:
                    return t < 0.5f ? 8f * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 4f) * 0.5f;
                case VERAEaseType.InQuint:
                    return t * t * t * t * t;
                case VERAEaseType.OutQuint:
                    return 1f - Mathf.Pow(1f - t, 5f);
                case VERAEaseType.InOutQuint:
                    return t < 0.5f ? 16f * t * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 5f) * 0.5f;
                case VERAEaseType.InSine:
                    return 1f - Mathf.Cos(t * HalfPi);
                case VERAEaseType.OutSine:
                    return Mathf.Sin(t * HalfPi);
                case VERAEaseType.InOutSine:
                    return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                case VERAEaseType.InExpo:
                    return t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
                case VERAEaseType.OutExpo:
                    return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                case VERAEaseType.InOutExpo:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return t < 0.5f
                        ? Mathf.Pow(2f, 20f * t - 10f) * 0.5f
                        : (2f - Mathf.Pow(2f, -20f * t + 10f)) * 0.5f;
                case VERAEaseType.InCirc:
                    return 1f - Mathf.Sqrt(1f - t * t);
                case VERAEaseType.OutCirc:
                    return Mathf.Sqrt(1f - Mathf.Pow(t - 1f, 2f));
                case VERAEaseType.InOutCirc:
                    return t < 0.5f
                        ? (1f - Mathf.Sqrt(1f - Mathf.Pow(2f * t, 2f))) * 0.5f
                        : (Mathf.Sqrt(1f - Mathf.Pow(-2f * t + 2f, 2f)) + 1f) * 0.5f;
                case VERAEaseType.InBack:
                    return (BackOvershoot + 1f) * t * t * t - BackOvershoot * t * t;
                case VERAEaseType.OutBack:
                    return 1f + (BackOvershoot + 1f) * Mathf.Pow(t - 1f, 3f) + BackOvershoot * Mathf.Pow(t - 1f, 2f);
                case VERAEaseType.InOutBack:
                    return t < 0.5f
                        ? (Mathf.Pow(2f * t, 2f) * ((BackOvershoot * 1.525f + 1f) * 2f * t - BackOvershoot * 1.525f)) * 0.5f
                        : (Mathf.Pow(2f * t - 2f, 2f) * ((BackOvershoot * 1.525f + 1f) * (2f * t - 2f) + BackOvershoot * 1.525f) + 2f) * 0.5f;
                case VERAEaseType.InElastic:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return -Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((t * 10f - 10.75f) * TwoPi / 3f);
                case VERAEaseType.OutElastic:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * TwoPi / 3f) + 1f;
                case VERAEaseType.InOutElastic:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return t < 0.5f
                        ? -(Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * TwoPi / 4.5f)) * 0.5f
                        : (Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * TwoPi / 4.5f)) * 0.5f + 1f;
                case VERAEaseType.InBounce:
                    return 1f - Evaluate(VERAEaseType.OutBounce, 1f - t);
                case VERAEaseType.OutBounce:
                    const float n1 = 7.5625f;
                    const float d1 = 2.75f;
                    if (t < 1f / d1) return n1 * t * t;
                    if (t < 2f / d1) return n1 * (t -= 1.5f / d1) * t + 0.75f;
                    if (t < 2.5f / d1) return n1 * (t -= 2.25f / d1) * t + 0.9375f;
                    return n1 * (t -= 2.625f / d1) * t + 0.984375f;
                case VERAEaseType.InOutBounce:
                    return t < 0.5f
                        ? (1f - Evaluate(VERAEaseType.OutBounce, 1f - 2f * t)) * 0.5f
                        : (1f + Evaluate(VERAEaseType.OutBounce, 2f * t - 1f)) * 0.5f;
                default:
                    return t;
            }
        }
    }
}
