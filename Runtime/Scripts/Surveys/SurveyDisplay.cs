using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VERA;

namespace VERA
{
    [RequireComponent(typeof(VERAFocusUI))]
    internal class SurveyDisplay : MonoBehaviour
    {

        // SurveyDisplay handles the display of an in-VR survey. It is the central manager for the survey.


        #region CONSTANTS


        // Fades and animations
        public static float SCREEN_FADE_DURATION = 0.3f;
        private const float WARNING_DISPLAY_DURATION = 2f;
        private const float WARNING_SHAKE_DURATION = 0.4f;
        private const float WARNING_SHAKE_MAGNITUDE = 10f;

        // Buttons and text
        private const string NEXT_BUTTON_START_TEXT = "→ Start →";
        private const string NEXT_BUTTON_DEFAULT_TEXT = "→ Next →";
        private const string NEXT_BUTTON_SUBMIT_TEXT = "→ Continue →";
        private const string PREVIOUS_BUTTON_DEFAULT_TEXT = "← Back ←";
        public const float QUESTION_TEXT_MAX_HEIGHT = 300f;

        // Colors
        public static readonly Color BACKGROUND_COLOR = ColorUtility.TryParseHtmlString("#08080A", out var c) ? c : Color.black;
        public static readonly Color HIGH_BACKGROUND_COLOR = ColorUtility.TryParseHtmlString("#14131A", out var c) ? c : Color.black;
        public static readonly Color ELEMENT_COLOR = ColorUtility.TryParseHtmlString("#292733", out var c) ? c : Color.darkGray;
        public static readonly Color HIGHLIGHT_COLOR = ColorUtility.TryParseHtmlString("#4F46E5", out var c) ? c : Color.blueViolet;

        public static readonly Color TEXT_PRIMARY_COLOR = ColorUtility.TryParseHtmlString("#FFFFFF", out var c) ? c : Color.white;
        public static readonly Color TEXT_SECONDARY_COLOR = ColorUtility.TryParseHtmlString("#B0B0B0", out var c) ? c : Color.gray;

        public static readonly Color BUTTON_ACTIVE_COLOR = ColorUtility.TryParseHtmlString("#292733", out var c) ? c : Color.blueViolet;
        public static readonly Color BUTTON_HOVER_COLOR = ColorUtility.TryParseHtmlString("#332B52", out var c) ? c : Color.blueViolet;
        public static readonly Color BUTTON_INACTIVE_COLOR = ColorUtility.TryParseHtmlString("#000000", out var c) ? c : Color.blueViolet;
        public static readonly Color BUTTON_SELECTED_COLOR = ColorUtility.TryParseHtmlString("#4F46E5", out var c) ? c : Color.blueViolet;
        public static readonly Color BUTTON_SELECTED_HOVER_COLOR = ColorUtility.TryParseHtmlString("#5c57c4", out var c) ? c : Color.blueViolet;


        #endregion


        #region VARIABLES

        private VERASurveyInfo activeSurveyInfo;
        private Action onSurveyComplete;

        private VERAFocusUI focusUi;
        [SerializeField] private VERASurveyComponents surveyComponents;

        private enum SurveyDisplayState
        {
            StartingScreen,
            InSurvey,
            EndingScreen
        }

        private SurveyDisplayState currentSurveyState = SurveyDisplayState.StartingScreen;
        private SurveyScreen activeSurveyScreen;
        private KeyValuePair<string, string>[] surveyResponses;
        private int currentQuestionIndex = 0;


        #endregion


        #region SURVEY COMPONENTS


        /// <summary>
        /// SurveyDisplayComponents is a container for references to the various components that make up the survey display, 
        /// such as the question screens, answer options, submit button, etc.
        /// </summary>
        [System.Serializable]
        private class VERASurveyComponents
        {
            public SurveyStartScreen startScreen;
            public SurveyQuestionScreen questionScreen;
            public SurveyEndScreen endScreen;

            public CanvasGroup mainCanvasGroup;

            public Button nextButton;
            public TMP_Text nextButtonText;
            public Button previousButton;
            public TMP_Text previousButtonText;

            public TMP_Text miniNameText;
            public TMP_Text progressText;

            public CanvasGroup finishQuestionWarning;
        }


        #endregion


        #region MONOBEHAVIOUR AND SETUP


        private void Awake()
        {
            focusUi = GetComponent<VERAFocusUI>();
        }


        #endregion


        #region START / END


        /// <summary>
        /// Start a survey by VERASurveyInfo. Triggers the display of the survey in VR, and the collection of responses.
        /// </summary>
        /// <param name="surveyToStart">The VERASurveyInfo object containing the survey details to start.</param>
        /// <param name="onSurveyComplete">A callback Action that will be invoked when the survey is completed by the participant.</param>
        /// <param name="heightOffset">How far the survey will be offset vertically from the user's head position. Default is 0.</param>
        /// <param name="distanceOffset">How far the survey will be offset horizontally from the user's head position. Default is 3.</param>
        public void StartSurvey(VERASurveyInfo surveyToStart, Action onSurveyComplete, float heightOffset = 0f, float distanceOffset = 3f)
        {
            LeanTween.cancel(surveyComponents.mainCanvasGroup.gameObject);
            surveyComponents.mainCanvasGroup.LeanAlpha(1f, SurveyDisplay.SCREEN_FADE_DURATION);

            activeSurveyInfo = surveyToStart;
            this.onSurveyComplete = onSurveyComplete;

            // Reset responses and question index
            surveyResponses = new KeyValuePair<string, string>[activeSurveyInfo.surveyQuestions.Count];
            currentQuestionIndex = 0;

            // Apply height and distance offset parameters
            focusUi.SetParameters(heightOffset, distanceOffset);
            focusUi.ResetPositionImmediate();

            // Display the starting screen of the survey, which will then lead into the rest of the survey flow
            DisplayStartingScreen();
        }


        /// <summary>
        /// Finishes the survey; should be called when the participant has fully completed the survey, 
        /// and the results have been successfully uploaded (i.e. ready to move on).
        /// </summary>
        public void FinishSurvey()
        {
            // If we have a stored callback for when the survey is complete, invoke it to trigger any necessary follow-up actions
            if (onSurveyComplete != null)
            {
                onSurveyComplete.Invoke();

                LeanTween.cancel(surveyComponents.mainCanvasGroup.gameObject);
                surveyComponents.mainCanvasGroup.LeanAlpha(0f, SurveyDisplay.SCREEN_FADE_DURATION).setOnComplete(() => Destroy(gameObject));
            }
        }


        #endregion


        #region NEXT / PREVIOUS BUTTONS


        /// <summary>
        /// Handles the logic for when the Previous button is pressed (previous question, previous section, etc.).
        /// </summary>
        public void OnPreviousButtonPressed()
        {
            switch (currentSurveyState)
            {
                case SurveyDisplayState.StartingScreen:
                    // If we're on the starting screen, just do nothing since there's no previous screen to go back to
                    // This case should not happen, as the previous button should be disabled
                    break;
                case SurveyDisplayState.InSurvey:
                    // In-survey, just go to the previous question; go back to the starting screen if we're on the first question
                    SaveCurrentQuestionResponse();

                    if (currentQuestionIndex > 0)
                    {
                        currentQuestionIndex--;
                        surveyComponents.questionScreen.DisplayQuestion(activeSurveyInfo.surveyQuestions[currentQuestionIndex]);
                        SetProgressText(true, currentQuestionIndex);
                    }
                    else
                    {
                        activeSurveyScreen.HideScreen();
                        DisplayStartingScreen();
                        SetProgressText(false);
                    }
                    break;
                case SurveyDisplayState.EndingScreen:
                    // If we're on the ending screen, go back to the last question in the survey
                    activeSurveyScreen.HideScreen();
                    currentQuestionIndex = activeSurveyInfo.surveyQuestions.Count - 1;
                    DisplayQuestionsScreen();
                    SetProgressText(true, currentQuestionIndex);
                    break;
            }
        }


        public void OnNextButtonPressed()
        {
            switch (currentSurveyState)
            {
                case SurveyDisplayState.StartingScreen:
                    // If we're on the starting screen, go to the first question screen
                    activeSurveyScreen.HideScreen();
                    currentQuestionIndex = 0;
                    DisplayQuestionsScreen();
                    break;
                case SurveyDisplayState.InSurvey:
                    // Validate that the current question is answered and scrolled before allowing progression
                    if (!surveyComponents.questionScreen.IsCurrentQuestionAnswered()
                        || !surveyComponents.questionScreen.HasCurrentQuestionScrolledToBottom())
                    {
                        ShowFinishQuestionWarning();
                        return;
                    }

                    // In-survey, just go to the next question; go to the ending screen if we're on the last question
                    SaveCurrentQuestionResponse();

                    if (currentQuestionIndex < activeSurveyInfo.surveyQuestions.Count - 1)
                    {
                        currentQuestionIndex++;
                        surveyComponents.questionScreen.DisplayQuestion(activeSurveyInfo.surveyQuestions[currentQuestionIndex]);
                        SetProgressText(true, currentQuestionIndex);
                    }
                    else
                    {
                        activeSurveyScreen.HideScreen();
                        DisplayEndingScreen();
                    }
                    break;
                case SurveyDisplayState.EndingScreen:
                    // If we're on the ending screen, push to the ending screen handler to begin the upload process
                    surveyComponents.endScreen.BeginUploadProcess();
                    SetNavButtons(false, NEXT_BUTTON_SUBMIT_TEXT, false, PREVIOUS_BUTTON_DEFAULT_TEXT);
                    break;
            }
        }


        #endregion


        #region SHOW SCREENS


        /// <summary>
        /// Displays the starting screen of the survey, which may include instructions or introductory text. 
        /// This is the first screen that will be shown when a survey starts, and should lead into the rest of the survey flow.
        /// </summary>
        private void DisplayStartingScreen()
        {
            currentSurveyState = SurveyDisplayState.StartingScreen;
            surveyComponents.startScreen.DisplayStartScreen(activeSurveyInfo.surveyName, activeSurveyInfo.surveyDescription);
            activeSurveyScreen = surveyComponents.startScreen;

            SetNavButtons(true, NEXT_BUTTON_START_TEXT, false, PREVIOUS_BUTTON_DEFAULT_TEXT);
            SetProgressText(false);
        }


        /// <summary>
        /// Displays the question screen for the current question index. 
        /// This is where the participant will answer the survey questions.
        /// </summary>
        private void DisplayQuestionsScreen()
        {
            currentSurveyState = SurveyDisplayState.InSurvey;
            surveyComponents.questionScreen.ShowQuestionScreen();
            surveyComponents.questionScreen.DisplayQuestion(activeSurveyInfo.surveyQuestions[currentQuestionIndex]);
            activeSurveyScreen = surveyComponents.questionScreen;

            SetNavButtons(true, NEXT_BUTTON_DEFAULT_TEXT, true, PREVIOUS_BUTTON_DEFAULT_TEXT);
            SetProgressText(true, currentQuestionIndex);
        }


        /// <summary>
        /// Displays the ending screen of the survey, which may include a summary or thank you message. 
        /// This is the final screen that will be shown after the participant has completed all survey questions, 
        /// and should lead to the completion of the survey process.
        /// </summary>
        private void DisplayEndingScreen()
        {
            currentSurveyState = SurveyDisplayState.EndingScreen;
            surveyComponents.endScreen.DisplayEndScreen();
            activeSurveyScreen = surveyComponents.endScreen;

            SetNavButtons(true, NEXT_BUTTON_SUBMIT_TEXT, true, PREVIOUS_BUTTON_DEFAULT_TEXT);
            SetProgressText(false);
        }


        #endregion


        #region RESPONSE MANAGEMENT


        /// <summary>
        /// Saves the response for the current question. 
        /// This should be called before navigating away from a question screen (e.g. when Next or Previous is pressed)
        /// </summary>
        private void SaveCurrentQuestionResponse()
        {
            if (currentQuestionIndex < 0 || currentQuestionIndex >= activeSurveyInfo.surveyQuestions.Count)
            {
                VERADebugger.LogWarning($"Attempted to save survey response for question index {currentQuestionIndex}, which is out of bounds. No response saved.", "SurveyDisplay");
                return;
            }

            // Save the response for the current question
            surveyResponses[currentQuestionIndex] = new KeyValuePair<string, string>(activeSurveyInfo.surveyQuestions[currentQuestionIndex].questionId, surveyComponents.questionScreen.GetCurrentResponse());
        }


        /// <summary>
        /// Returns the survey responses collected from the participant.
        /// </summary>
        /// <returns>A KeyValuePair containing the survey ID and the participant's responses.</returns>
        public KeyValuePair<string, string>[] GetSurveyResults()
        {
            return surveyResponses;
        }


        #endregion


        #region HELPERS


        /// <summary>
        /// Helper method to set the visibility and text of the navigation buttons (Next and Previous) based on the current state of the survey.
        /// </summary>
        /// <param name="showNext">Whether to show the Next button.</param>
        /// <param name="nextButtonText">The text to display on the Next button.</param>
        /// <param name="showPrevious">Whether to show the Previous button.</param>
        /// <param name="previousButtonText">The text to display on the Previous button.</param>
        private void SetNavButtons(bool showNext, string nextButtonText, bool showPrevious, string previousButtonText)
        {
            surveyComponents.nextButton.interactable = showNext;
            surveyComponents.nextButtonText.text = nextButtonText;
            surveyComponents.previousButton.interactable = showPrevious;
            surveyComponents.previousButtonText.text = previousButtonText;
        }


        /// <summary>
        /// Helper method to set the visibility and text of the progress text based on the current state of the survey.
        /// </summary>
        /// <param name="show">Whether to show the progress text.</param>
        /// <param name="questionIndex">The index of the current question.</param>
        private void SetProgressText(bool show, int questionIndex = 0)
        {
            if (show)
            {
                if (surveyComponents.progressText.gameObject.activeSelf == false)
                {
                    surveyComponents.progressText.gameObject.SetActive(true);
                    surveyComponents.progressText.alpha = 0f;
                    surveyComponents.miniNameText.gameObject.SetActive(true);
                    surveyComponents.miniNameText.alpha = 0f;
                    LeanTween.cancel(surveyComponents.progressText.gameObject);
                    LeanTween.cancel(surveyComponents.miniNameText.gameObject);
                    LeanTween.value(surveyComponents.progressText.alpha, 1f, SCREEN_FADE_DURATION)
                        .setOnUpdate((float val) => surveyComponents.progressText.alpha = val);
                    LeanTween.value(surveyComponents.miniNameText.alpha, 1f, SCREEN_FADE_DURATION)
                        .setOnUpdate((float val) => surveyComponents.miniNameText.alpha = val);
                }

                surveyComponents.miniNameText.text = activeSurveyInfo.surveyName;
                surveyComponents.progressText.text = $"Question {questionIndex + 1} of {activeSurveyInfo.surveyQuestions.Count}";
            }
            else
            {
                if (surveyComponents.progressText.gameObject.activeSelf == true)
                {
                    LeanTween.cancel(surveyComponents.progressText.gameObject);
                    LeanTween.cancel(surveyComponents.miniNameText.gameObject);
                    LeanTween.value(surveyComponents.progressText.alpha, 0f, SCREEN_FADE_DURATION)
                        .setOnUpdate((float val) => surveyComponents.progressText.alpha = val)
                        .setOnComplete(() => surveyComponents.progressText.gameObject.SetActive(false));
                    LeanTween.value(surveyComponents.miniNameText.alpha, 0f, SCREEN_FADE_DURATION)
                        .setOnUpdate((float val) => surveyComponents.miniNameText.alpha = val)
                        .setOnComplete(() => surveyComponents.miniNameText.gameObject.SetActive(false));
                }
            }
        }


        public VERASurveyInfo GetActiveSurveyInfo()
        {
            return activeSurveyInfo;
        }


        /// <summary>
        /// Shows the finish-question warning with a shake animation, then fades it back out.
        /// </summary>
        private void ShowFinishQuestionWarning()
        {
            CanvasGroup warning = surveyComponents.finishQuestionWarning;
            RectTransform warningRect = warning.GetComponent<RectTransform>();
            Vector3 originalPos = warningRect.localPosition;

            // Cancel any in-progress warning animations
            LeanTween.cancel(warning.gameObject);

            // Snap visible and shake
            warning.alpha = 1f;
            LeanTween.value(warning.gameObject, 0f, 1f, WARNING_SHAKE_DURATION)
                .setOnUpdate((float t) =>
                {
                    float decay = 1f - t;
                    float offsetX = Mathf.Sin(t * Mathf.PI * 6f) * WARNING_SHAKE_MAGNITUDE * decay;
                    warningRect.localPosition = originalPos + new Vector3(offsetX, 0f, 0f);
                })
                .setOnComplete(() =>
                {
                    warningRect.localPosition = originalPos;

                    // Hold briefly, then fade out
                    LeanTween.value(warning.gameObject, 1f, 0f, SCREEN_FADE_DURATION)
                        .setDelay(WARNING_DISPLAY_DURATION)
                        .setOnUpdate((float val) => warning.alpha = val);
                });
        }


        #endregion


    }
}