using TMPro;
using UnityEngine;

namespace VERA
{
    internal class SurveyStartScreen : SurveyScreen
    {

        // SurveyStartScreen handles the display of the start screen for the survey.


        #region VARIABLES


        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;


        #endregion


        #region DISPLAY


        /// <summary>
        /// Displays the start screen with the given title and description.
        /// </summary>
        /// <param name="title">The title to display on the start screen.</param>
        /// <param name="description">The description to display on the start screen.</param>
        public void DisplayStartScreen(string title, string description)
        {
            gameObject.SetActive(true);

            titleText.text = title;
            descriptionText.text = description;

            mainCanvasGroup.alpha = 0f;
            mainCanvasGroup.TweenAlpha(1f, SurveyDisplay.SCREEN_FADE_DURATION);
        }


        #endregion


    }
}
