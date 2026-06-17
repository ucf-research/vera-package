using System;
using System.Collections.Generic;
using UnityEngine;

namespace VERA
{
    public static class VERATween
    {
        private static readonly List<ITweenInstance> ActiveTweens = new List<ITweenInstance>();
        private static readonly Dictionary<GameObject, List<ITweenInstance>> TweensByTarget = new Dictionary<GameObject, List<ITweenInstance>>();
        private static VERATweenRunner _runner;
        private static int _nextId = 1;

        public static TweenHandle<float> Value(float from, float to, float duration)
        {
            return CreateTween(null, from, to, duration, Mathf.Lerp);
        }

        public static TweenHandle<float> Value(GameObject target, float from, float to, float duration)
        {
            return CreateTween(target, from, to, duration, Mathf.Lerp);
        }

        public static TweenHandle<Color> Value(GameObject target, Color from, Color to, float duration)
        {
            return CreateTween(target, from, to, duration, Color.Lerp);
        }

        public static TweenHandle<Vector2> Value(GameObject target, Vector2 from, Vector2 to, float duration)
        {
            return CreateTween(target, from, to, duration, Vector2.Lerp);
        }

        public static TweenHandle<Vector3> Value(GameObject target, Vector3 from, Vector3 to, float duration)
        {
            return CreateTween(target, from, to, duration, Vector3.Lerp);
        }

        public static void Cancel(GameObject target)
        {
            if (target == null || !TweensByTarget.TryGetValue(target, out List<ITweenInstance> tweens))
                return;

            for (int i = tweens.Count - 1; i >= 0; i--)
                tweens[i].Cancel();

            tweens.Clear();
        }

        private static void Unregister(ITweenInstance tween)
        {
            if (tween.Target != null && TweensByTarget.TryGetValue(tween.Target, out List<ITweenInstance> tweens))
            {
                tweens.Remove(tween);
                if (tweens.Count == 0)
                    TweensByTarget.Remove(tween.Target);
            }
        }

        private static TweenHandle<T> CreateTween<T>(
            GameObject target,
            T from,
            T to,
            float duration,
            Func<T, T, float, T> lerp)
        {
            EnsureRunner();

            var tween = new TweenInstance<T>
            {
                Id = _nextId++,
                Target = target,
                From = from,
                To = to,
                Duration = Mathf.Max(0f, duration),
                Lerp = lerp
            };

            ActiveTweens.Add(tween);

            if (target != null)
            {
                if (!TweensByTarget.TryGetValue(target, out List<ITweenInstance> tweens))
                {
                    tweens = new List<ITweenInstance>();
                    TweensByTarget[target] = tweens;
                }

                tweens.Add(tween);
            }

            return new TweenHandle<T>(tween);
        }

        private static void EnsureRunner()
        {
            if (_runner != null)
                return;

            var runnerObject = new GameObject("[VERATween]");
            runnerObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(runnerObject);
            _runner = runnerObject.AddComponent<VERATweenRunner>();
        }

        private static void Tick(float deltaTime)
        {
            for (int i = ActiveTweens.Count - 1; i >= 0; i--)
            {
                if (!ActiveTweens[i].Step(deltaTime))
                {
                    Unregister(ActiveTweens[i]);
                    ActiveTweens.RemoveAt(i);
                }
            }
        }

        internal interface ITweenInstance
        {
            GameObject Target { get; }
            float Duration { get; }
            float Delay { get; set; }
            VERAEaseType Ease { get; set; }
            bool Step(float deltaTime);
            void Cancel();
        }

        internal sealed class TweenInstance<T> : ITweenInstance
        {
            public int Id;
            public GameObject Target { get; set; }
            public float Duration { get; set; }
            public float Delay { get; set; }
            public float Elapsed;
            public VERAEaseType Ease { get; set; } = VERAEaseType.Linear;
            public bool IsCancelled;
            public T From;
            public T To;
            public Func<T, T, float, T> Lerp;
            public Action<T> OnUpdate;
            public Action OnComplete;

            public bool Step(float deltaTime)
            {
                if (IsCancelled)
                    return false;

                if (Delay > 0f)
                {
                    Delay -= deltaTime;
                    return true;
                }

                Elapsed += deltaTime;
                float progress = Duration <= 0f ? 1f : Mathf.Clamp01(Elapsed / Duration);
                float easedProgress = VERAEase.Evaluate(Ease, progress);
                OnUpdate?.Invoke(Lerp(From, To, easedProgress));

                if (progress >= 1f)
                {
                    OnComplete?.Invoke();
                    return false;
                }

                return true;
            }

            public void Cancel()
            {
                IsCancelled = true;
            }
        }

        private sealed class VERATweenRunner : MonoBehaviour
        {
            private void Update()
            {
                Tick(Time.deltaTime);
            }
        }

        public sealed class TweenHandle<T>
        {
            private readonly TweenInstance<T> _tween;

            internal TweenHandle(TweenInstance<T> tween)
            {
                _tween = tween;
            }

            public TweenHandle<T> SetOnUpdate(Action<T> onUpdate)
            {
                _tween.OnUpdate = onUpdate;
                return this;
            }

            public TweenHandle<T> SetOnComplete(Action onComplete)
            {
                _tween.OnComplete = onComplete;
                return this;
            }

            public TweenHandle<T> SetDelay(float delay)
            {
                _tween.Delay = Mathf.Max(0f, delay);
                return this;
            }

            public TweenHandle<T> SetEase(VERAEaseType ease)
            {
                _tween.Ease = ease;
                return this;
            }

            public TweenHandle<T> SetEaseLinear() => SetEase(VERAEaseType.Linear);
            public TweenHandle<T> SetEaseInQuad() => SetEase(VERAEaseType.InQuad);
            public TweenHandle<T> SetEaseOutQuad() => SetEase(VERAEaseType.OutQuad);
            public TweenHandle<T> SetEaseInOutQuad() => SetEase(VERAEaseType.InOutQuad);
            public TweenHandle<T> SetEaseInCubic() => SetEase(VERAEaseType.InCubic);
            public TweenHandle<T> SetEaseOutCubic() => SetEase(VERAEaseType.OutCubic);
            public TweenHandle<T> SetEaseInOutCubic() => SetEase(VERAEaseType.InOutCubic);
            public TweenHandle<T> SetEaseInQuart() => SetEase(VERAEaseType.InQuart);
            public TweenHandle<T> SetEaseOutQuart() => SetEase(VERAEaseType.OutQuart);
            public TweenHandle<T> SetEaseInOutQuart() => SetEase(VERAEaseType.InOutQuart);
            public TweenHandle<T> SetEaseInQuint() => SetEase(VERAEaseType.InQuint);
            public TweenHandle<T> SetEaseOutQuint() => SetEase(VERAEaseType.OutQuint);
            public TweenHandle<T> SetEaseInOutQuint() => SetEase(VERAEaseType.InOutQuint);
            public TweenHandle<T> SetEaseInSine() => SetEase(VERAEaseType.InSine);
            public TweenHandle<T> SetEaseOutSine() => SetEase(VERAEaseType.OutSine);
            public TweenHandle<T> SetEaseInOutSine() => SetEase(VERAEaseType.InOutSine);
            public TweenHandle<T> SetEaseInExpo() => SetEase(VERAEaseType.InExpo);
            public TweenHandle<T> SetEaseOutExpo() => SetEase(VERAEaseType.OutExpo);
            public TweenHandle<T> SetEaseInOutExpo() => SetEase(VERAEaseType.InOutExpo);
            public TweenHandle<T> SetEaseInCirc() => SetEase(VERAEaseType.InCirc);
            public TweenHandle<T> SetEaseOutCirc() => SetEase(VERAEaseType.OutCirc);
            public TweenHandle<T> SetEaseInOutCirc() => SetEase(VERAEaseType.InOutCirc);
            public TweenHandle<T> SetEaseInBack() => SetEase(VERAEaseType.InBack);
            public TweenHandle<T> SetEaseOutBack() => SetEase(VERAEaseType.OutBack);
            public TweenHandle<T> SetEaseInOutBack() => SetEase(VERAEaseType.InOutBack);
            public TweenHandle<T> SetEaseInElastic() => SetEase(VERAEaseType.InElastic);
            public TweenHandle<T> SetEaseOutElastic() => SetEase(VERAEaseType.OutElastic);
            public TweenHandle<T> SetEaseInOutElastic() => SetEase(VERAEaseType.InOutElastic);
            public TweenHandle<T> SetEaseInBounce() => SetEase(VERAEaseType.InBounce);
            public TweenHandle<T> SetEaseOutBounce() => SetEase(VERAEaseType.OutBounce);
            public TweenHandle<T> SetEaseInOutBounce() => SetEase(VERAEaseType.InOutBounce);

            public void Cancel()
            {
                _tween.Cancel();
            }
        }
    }
}
