using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VERA
{
    internal abstract class SurveyQuestionContent : MonoBehaviour
    {

        // SurveyQuestionContent handles the display and response recording for an individual survey question.
        // This script is meant to be extended by other scripts that handle specific question types (e.g. multiple choice, slider, etc.)

        #region VARIABLES


        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] protected TMP_Text questionText;
        [SerializeField] protected RectTransform questionTextContainer;
        [SerializeField] protected RectTransform responseContainer;
        [SerializeField] protected RectTransform fullDisplayContainer;
        [SerializeField] protected ScrollRect responseScrollRect;

        [SerializeField] private CanvasGroup scrollWarning;
        private bool _requiresScrolling;
        private bool _hasScrolledToBottom = true;

        /// <summary>
        /// Whether this question's response area has more content than fits in the viewport.
        /// </summary>
        public bool RequiresScrolling => _requiresScrolling;

        /// <summary>
        /// Whether the user has scrolled to the bottom of the response area (or scrolling is not required).
        /// </summary>
        public bool HasScrolledToBottom => _hasScrolledToBottom;

        /// <summary>
        /// Invoked when the user scrolls to the bottom of the response area for the first time.
        /// </summary>
        public Action OnScrolledToBottom;


        #endregion


        #region FADE IN / OUT


        /// <summary>
        /// Fades in this question content, making it visible to the user.
        /// </summary>
        public void FadeIn()
        {
            canvasGroup.TweenAlpha(1f, SurveyDisplay.SCREEN_FADE_DURATION);
        }


        /// <summary>
        /// Fades out this question content and destroys the GameObject after the fade-out is complete.
        /// </summary>
        public void FadeOutAndDestroy()
        {
            canvasGroup.TweenAlpha(0f, SurveyDisplay.SCREEN_FADE_DURATION).SetOnComplete(() => Destroy(gameObject));
        }


        #endregion


        #region DISPLAY QUESTION / RESPONSE


        /// <summary>
        /// Displays the provided survey question on this question screen.
        /// </summary>
        /// <param name="question">The survey question to display.</param>
        public virtual void DisplayQuestion(VERASurveyQuestionInfo question)
        {
            canvasGroup.alpha = 0f;
            FadeIn();

            questionText.text = question.questionText;

            // Force the full canvas layout to update first so questionText has a valid width from its parent chain,
            // then force TMP to recalculate its mesh with that correct width so preferredHeight is accurate.
            Canvas.ForceUpdateCanvases();
            questionText.ForceMeshUpdate();

            // Resize the question text container to fit the question text, up to a maximum height (after which text is scrollable)
            float preferredHeight = questionText.preferredHeight;
            float clampedHeight = Mathf.Min(preferredHeight, SurveyDisplay.QUESTION_TEXT_MAX_HEIGHT);
            questionTextContainer.sizeDelta = new Vector2(questionTextContainer.sizeDelta.x, clampedHeight);

            // Resize response container to fill remaining space below question text
            // Use rect.height for the actual pixel height regardless of anchor configuration
            float responseContainerHeight = fullDisplayContainer.rect.height - clampedHeight - 5f;
            responseContainer.sizeDelta = new Vector2(responseContainer.sizeDelta.x, responseContainerHeight);

            // Question-specific display logic (e.g. showing response options) will be handled in child classes that extend this base class

            // Always reset the scroll warning; the coroutine will re-show it if the content actually overflows
            HideScrollWarning();

            // Initialize scroll tracking if a scroll rect is assigned (check happens next frame after subclass content is spawned)
            if (responseScrollRect != null)
            {
                _hasScrolledToBottom = false;
                responseScrollRect.verticalNormalizedPosition = 1f;
                responseScrollRect.onValueChanged.AddListener(OnScrollValueChanged);
                StartCoroutine(EvaluateScrollRequirement());
            }
            else
            {
                _hasScrolledToBottom = true;
            }
        }


        /// <summary>
        /// Retrieves the user's response to this survey question in string format.
        /// </summary>
        /// <returns>The user's response as a string.</returns>
        public abstract string GetResponse();


        /// <summary>
        /// Returns whether the user has sufficiently answered this question to proceed.
        /// </summary>
        public abstract bool IsAnswered();


        #endregion


        #region SCROLL TRACKING


        private IEnumerator EvaluateScrollRequirement()
        {
            // Wait two frames: first for child-class Instantiate calls to run,
            // second for ContentSizeFitter / LayoutGroup rebuilds in LateUpdate to settle.
            yield return null;
            yield return null;

            Canvas.ForceUpdateCanvases();

            if (responseScrollRect.content == null) yield break;

            // Force an explicit layout rebuild on the scroll content to ensure ContentSizeFitter
            // has applied the correct height before we measure — Canvas.ForceUpdateCanvases() alone
            // may not be sufficient for nested layout groups.
            LayoutRebuilder.ForceRebuildLayoutImmediate(responseScrollRect.content);

            // ScrollRect.viewport is optional — fall back to the scroll rect's own transform if unassigned.
            RectTransform viewport = responseScrollRect.viewport != null
                ? responseScrollRect.viewport
                : (RectTransform)responseScrollRect.transform;

            float contentHeight = responseScrollRect.content.rect.height;
            float viewportHeight = viewport.rect.height;

            _requiresScrolling = contentHeight > viewportHeight + 1f;

            if (!_requiresScrolling)
                _hasScrolledToBottom = true;
            else
                ShowScrollWarning();
        }


        private void OnScrollValueChanged(Vector2 scrollPos)
        {
            if (!_requiresScrolling || _hasScrolledToBottom) return;

            // verticalNormalizedPosition: 1 = top, 0 = bottom
            if (scrollPos.y <= 0.02f)
            {
                _hasScrolledToBottom = true;
                HideScrollWarning();
                OnScrolledToBottom?.Invoke();
            }
        }


        private void ShowScrollWarning()
        {
            if (scrollWarning == null) return;
            VERATween.Cancel(scrollWarning.gameObject);
            scrollWarning.TweenAlpha(1f, SurveyDisplay.SCREEN_FADE_DURATION);
        }


        private void HideScrollWarning()
        {
            if (scrollWarning == null) return;
            VERATween.Cancel(scrollWarning.gameObject);
            scrollWarning.TweenAlpha(0f, SurveyDisplay.SCREEN_FADE_DURATION);
        }


        #endregion


    }

}
