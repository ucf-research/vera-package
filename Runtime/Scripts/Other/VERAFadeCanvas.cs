using UnityEngine;

namespace VERA
{
    internal class VERAFadeCanvas : MonoBehaviour
    {

        public static VERAFadeCanvas Instance { get; private set; }

        [SerializeField] private CanvasGroup canvasGroup;

        // Sets up the canvas to parent the correct camera
        public void SetupCanvas()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            Transform newParent = Camera.main.transform;
            transform.SetParent(newParent);
            transform.localPosition = new Vector3(0f, 0f, 0.01f);
            transform.localRotation = Quaternion.identity;
        }

        // Fades the canvas in over the specified duration
        public void FadeIn(float duration)
        {
            canvasGroup.LeanAlpha(1f, duration);
        }

        // Fades the canvas out over the specified duration
        public void FadeOut(float duration)
        {
            canvasGroup.LeanAlpha(0f, duration);
        }

        // Fades the canvas to a specific alpha value over the specified duration
        public void FadeTo(float targetAlpha, float duration)
        {
            canvasGroup.LeanAlpha(targetAlpha, duration);
        }
    }
}