using UnityEngine;

namespace VERA
{
    internal class SurveyScreen : MonoBehaviour
    {

        // SurveyScreen is a parent script for the different screens involved in the survey (start screen, question screens, end screen).


        #region VARIABLES


        [SerializeField] protected CanvasGroup mainCanvasGroup;


        #endregion


        #region DISPLAY


        /// <summary>
        /// Hides this survey screen by fading it out. The screen will be set to inactive after the fade out is complete.
        /// </summary>
        public virtual void HideScreen()
        {
            mainCanvasGroup.LeanAlpha(0f, SurveyDisplay.SCREEN_FADE_DURATION).setOnComplete(() => gameObject.SetActive(false));
        }


        #endregion

    }
}