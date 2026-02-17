using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace VERA
{
    internal class VERASurveyStarter : MonoBehaviour
    {

        private SurveyManager activeSurveyInterface;

        /// <summary>
        /// Starts a survey for the current participant session, based on the provided SurveyInfo.
        /// Spawns in a survey interface to display the survey questions and record responses.
        /// Subscribes to the necessary events to manage the survey lifecycle and ensure proper logging of survey responses.
        /// </summary>
        /// <param name="surveyInfo">The SurveyInfo ScriptableObject containing all necessary information about the survey to start</param>
        /// <param name="transportToLobby">Whether to temporarily transport the participant to a survey lobby while the survey is active. Default is true.</param>
        /// <param name="dimEnvironment">Whether to fade the surrounding environment slightly, to help focus on the survey. Default is true.</param>
        /// <param name="heightOffset">How far the survey will be offset vertically from the user's head position. Default is 0.</param>
        /// <param name="distanceOffset">How far the survey will be offset horizontally from the user's head position. Default is 3.</param>
        /// <param name="onSurveyComplete">An optional callback Action that will be invoked when the survey is completed by the participant.</param>
        public void StartSurvey(VERASurveyInfo surveyInfo, bool transportToLobby = true, bool dimEnvironment = true, float heightOffset = 0f, float distanceOffset = 3f, System.Action onSurveyComplete = null)
        {
            if (surveyInfo == null)
            {
                VERADebugger.LogError("Cannot start survey because the provided survey info is null.", "VERASurveyStarter");
                return;
            }

            if (!VERALogger.Instance.initialized)
            {
                VERADebugger.LogWarning("Cannot start survey because VERA is not initialized.", "VERASurveyStarter");
                return;
            }

            if (!VERALogger.Instance.collecting)
            {
                VERADebugger.LogWarning("Cannot start survey because data collection is not active.", "VERASurveyStarter");
                return;
            }

            VERADebugger.Log("Starting survey: " + surveyInfo.surveyName, "VERASurveyStarter", DebugPreference.Verbose);

            StartCoroutine(BeginSurveyCoroutine(surveyInfo, transportToLobby, dimEnvironment, heightOffset, distanceOffset, onSurveyComplete));
        }

        private IEnumerator BeginSurveyCoroutine(VERASurveyInfo surveyInfo, bool transportToLobby, bool dimEnvironment, float heightOffset, float distanceOffset, System.Action onSurveyComplete)
        {
            // Get the VERAFadeCanvas reference; if it does not exist, spawn it in
            if (VERAFadeCanvas.Instance == null)
            {
                VERADebugger.Log("No fade canvas found - instantiating a new one to use for survey lobby transport", "VERASurveyStarter", DebugPreference.Verbose);
                VERAFadeCanvas fadeCanvasPrefab = Resources.Load<VERAFadeCanvas>("VERAFadeCanvas");
                VERAFadeCanvas fadeCanvas = Instantiate(fadeCanvasPrefab);
                fadeCanvas.SetupCanvas();
            }

            // If transporting to a lobby, move the participant to the lobby location before starting the survey
            if (transportToLobby)
            {
                // Fade black screen in
                VERAFadeCanvas.Instance.FadeIn(1f);
                yield return new WaitForSeconds(1f);

                // Transport participant
                Transform playerTransform = FindAnyObjectByType<XROrigin>().transform;
                playerTransform.position = new Vector3(0, 0, 1000);
                yield return new WaitForSeconds(0.5f);

                // Fade black screen out
                if (dimEnvironment)
                    VERAFadeCanvas.Instance.FadeTo(0.8f, 1f);
                else
                    VERAFadeCanvas.Instance.FadeOut(1f);
            }
            else
            {
                if (dimEnvironment)
                    VERAFadeCanvas.Instance.FadeTo(0.8f, 1f);
            }

            // Use existing survey interface if one already exists, or spawn a new one if not
            if (activeSurveyInterface == null)
            {
                VERADebugger.Log("No survey interface found - instantiating a new one to display survey", "VERASurveyStarter", DebugPreference.Verbose);
                // Load the survey interface prefab from resources and spawn in a new instance
                SurveyManager surveyInterfacePrefab = Resources.Load<SurveyManager>("VERASurveyInterface");
                activeSurveyInterface = Instantiate(surveyInterfacePrefab);
            }

            // Start the survey
            activeSurveyInterface.BeginSurvey(surveyInfo, heightOffset, distanceOffset, onSurveyComplete);
        }
    }
}
