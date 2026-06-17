using UnityEngine;
using UnityEngine.UI;

namespace VERA
{
    internal class SurveyQuestionScreen : SurveyScreen
    {


        #region VARIABLES


        [SerializeField] private RectTransform questionContentContainer;
        [SerializeField] private CanvasGroup scrollWarning;

        [SerializeField] private SurveyQuestionContent multipleChoicePrefab; // Also selection
        [SerializeField] private SurveyQuestionContent sliderPrefab;
        [SerializeField] private SurveyQuestionContent matrixPrefab; // Also Likert

        private SurveyQuestionContent activeQuestionContent;


        #endregion


        #region DISPLAY


        /// <summary>
        /// Displays this question screen with a fade-in effect.
        /// </summary>
        public void ShowQuestionScreen()
        {
            gameObject.SetActive(true);
            mainCanvasGroup.alpha = 0f;
            mainCanvasGroup.TweenAlpha(1f, SurveyDisplay.SCREEN_FADE_DURATION);
        }


        /// <summary>
        /// Displays the given survey question on this screen, populating all relevant UI elements with the 
        /// question text, response options, etc.
        /// </summary>
        /// <param name="question">The survey question to display.</param>
        public void DisplayQuestion(VERASurveyQuestionInfo question)
        {
            VERADebugger.Log($"Displaying question {question.orderInSurvey + 1}: {question.questionText}", "SurveyDisplay", DebugPreference.Verbose);

            if (activeQuestionContent != null)
            {
                activeQuestionContent.FadeOutAndDestroy();
            }

            // Instantiate the appropriate content prefab based on the question type, and pass the question info to it for display
            switch (question.questionType)
            {
                case VERASurveyQuestionInfo.VERASurveyQuestionType.MultipleChoice:
                case VERASurveyQuestionInfo.VERASurveyQuestionType.Selection:
                    activeQuestionContent = Instantiate(multipleChoicePrefab, questionContentContainer);
                    break;
                case VERASurveyQuestionInfo.VERASurveyQuestionType.Slider:
                    activeQuestionContent = Instantiate(sliderPrefab, questionContentContainer);
                    break;
                case VERASurveyQuestionInfo.VERASurveyQuestionType.Matrix:
                    activeQuestionContent = Instantiate(matrixPrefab, questionContentContainer);
                    break;
                default:
                    VERADebugger.LogError($"Unsupported question type: {question.questionType}", "SurveyDisplay");
                    break;
            }

            if (activeQuestionContent != null)
            {
                activeQuestionContent.DisplayQuestion(question);
            }
        }


        #endregion


        #region RESPONSES


        /// <summary>
        /// Retrieves the user's current response to this question, based on their interactions with the UI elements on this screen.
        /// </summary>
        /// <returns>The user's current response as a string.</returns>
        public string GetCurrentResponse()
        {
            if (activeQuestionContent != null)
                return activeQuestionContent.GetResponse();
            return "-1";
        }


        /// <summary>
        /// Returns whether the current question has been sufficiently answered to proceed.
        /// </summary>
        public bool IsCurrentQuestionAnswered()
        {
            if (activeQuestionContent != null)
                return activeQuestionContent.IsAnswered();
            return false;
        }


        /// <summary>
        /// Returns whether the user has scrolled to the bottom of the current question's response area.
        /// </summary>
        public bool HasCurrentQuestionScrolledToBottom()
        {
            return activeQuestionContent == null || activeQuestionContent.HasScrolledToBottom;
        }


        #endregion


    }
}