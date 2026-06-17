using System.Collections;
using TMPro;
using UnityEngine;

namespace VERA
{
    [RequireComponent(typeof(SurveyInterfaceIO))]
    internal class SurveyEndScreen : SurveyScreen
    {

        // SurveyEndScreen handles the display of the end screen for the survey.


        #region VARIABLES


        [SerializeField] private TMP_Text titleText;
        [SerializeField] private CanvasGroup titleCanvasGroup;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private CanvasGroup descriptionCanvasGroup;
        [SerializeField] private SurveyDisplay surveyDisplay;
        [SerializeField] private SurveyInterfaceIO surveyInterfaceIO;

        private const string END_SCREEN_TITLE = "Ready to Continue?";
        private const string END_SCREEN_DESCRIPTION = "You've completed all questions in this survey. Continue to upload your responses (you won't be able to change your answers later).";

        private const string UPLOADING_TITLE = "Uploading Responses...";
        private const string UPLOADING_DESCRIPTION = "Please wait while we upload your responses...";

        private const string UPLOAD_COMPLETE_TITLE = "Upload Complete!";
        private const string UPLOAD_COMPLETE_DESCRIPTION = "The experiment will now continue. Do not remove your headset yet.";


        #endregion


        #region DISPLAY


        /// <summary>
        /// Displays the end screen.
        /// </summary>
        public void DisplayEndScreen()
        {
            gameObject.SetActive(true);

            titleText.text = END_SCREEN_TITLE;
            descriptionText.text = END_SCREEN_DESCRIPTION;

            mainCanvasGroup.alpha = 0f;
            mainCanvasGroup.TweenAlpha(1f, SurveyDisplay.SCREEN_FADE_DURATION);
        }


        /// <summary>
        /// Begins the process of uploading the survey responses. On completion, notifies the SurveyDisplay.
        /// </summary>
        public void BeginUploadProcess()
        {
            titleText.text = UPLOADING_TITLE;
            descriptionText.text = UPLOADING_DESCRIPTION;

            StartCoroutine(UploadAndFinish());
        }


        /// <summary>
        /// Uploads the survey responses, then displays a completion message and notifies the SurveyDisplay that the survey is finished.
        /// </summary>
        private IEnumerator UploadAndFinish()
        {
            // Upload responses
            float timeBeforeUpload = Time.time;
            yield return surveyInterfaceIO.OutputSurveyResults(surveyDisplay.GetActiveSurveyInfo(), surveyDisplay.GetSurveyResults());

            // Ensure at least 1 second wait to avoid quick flashes
            float uploadTime = Time.time - timeBeforeUpload;
            if (uploadTime < 1f)
            {
                yield return new WaitForSeconds(1f - uploadTime);
            }

            // Display the upload complete message
            titleText.text = UPLOAD_COMPLETE_TITLE;
            descriptionText.text = UPLOAD_COMPLETE_DESCRIPTION;

            // Wait, then finish the survey and continue with the experiment
            yield return new WaitForSeconds(3.5f);
            surveyDisplay.FinishSurvey();
        }


        #endregion


    }
}
