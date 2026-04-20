using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace VERA
{
    internal class VERASurveyStarter : MonoBehaviour
    {

        private SurveyManager activeSurveyInterface;
        private GameObject spawnedSurveyLobby;
        private bool webSurveyRunning = false;

        /// <summary>
        /// Starts a survey for the current participant session, based on the provided SurveyInfo.
        /// Spawns in a survey interface to display the survey questions and record responses.
        /// Subscribes to the necessary events to manage the survey lifecycle and ensure proper logging of survey responses.
        /// </summary>
        /// <param name="surveyInfo">The SurveyInfo ScriptableObject containing all necessary information about the survey to start</param>
        /// <param name="runInWeb">Whether the survey should be run in the web context (i.e. not in VR). Default is false.</param>
        /// <param name="transportToLobby">Whether to temporarily transport the participant to a survey lobby while the survey is active. Default is true.</param>
        /// <param name="dimEnvironment">Whether to fade the surrounding environment slightly, to help focus on the survey. Default is true.</param>
        /// <param name="heightOffset">How far the survey will be offset vertically from the user's head position. Default is 0.</param>
        /// <param name="distanceOffset">How far the survey will be offset horizontally from the user's head position. Default is 3.</param>
        /// <param name="onSurveyComplete">An optional callback Action that will be invoked when the survey is completed by the participant.</param>
        public void StartSurvey(VERASurveyInfo surveyInfo, bool runInWeb = false, bool transportToLobby = true, bool dimEnvironment = true, float heightOffset = 0f, float distanceOffset = 3f, System.Action onSurveyComplete = null)
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

            if (runInWeb)
                StartCoroutine(BeginWebSurveyCoroutine(surveyInfo, onSurveyComplete));
            else
                StartCoroutine(BeginVrSurveyCoroutine(surveyInfo, transportToLobby, dimEnvironment, heightOffset, distanceOffset, onSurveyComplete));
        }

        private IEnumerator BeginWebSurveyCoroutine(VERASurveyInfo surveyInfo, System.Action onSurveyComplete)
        {
            VERADebugger.Log("Starting web survey: " + surveyInfo.surveyName, "VERASurveyStarter", DebugPreference.Informative);
            // If we are in a WebGL / browser build context we need to call out to the web survey / browser to run the survey
#if UNITY_WEBGL && !UNITY_EDITOR
            webSurveyRunning = true;
            Application.ExternalEval("window.unityMessageHandler('SHOW_SURVEY', '" + surveyInfo.surveyId + "');");
            yield return new WaitUntil(() => !webSurveyRunning);
            onSurveyComplete?.Invoke();
            yield break;
#else
            VERADebugger.LogWarning("Attempted to start a survey in the web context, but this build / platform " +
            "is not WebGL. This call will work fine in final WebGL builds - but for now, the call will be skipped " +
            "and the experiment will continue as if the participant successfully finished the survey.", "VERASurveyStarter");
            onSurveyComplete?.Invoke();
            yield break;
#endif
        }

        // Called when the web survey that was launched via the browser / WebGL context has completed and returned data
        public void OnWebSurveyCompleted(string data)
        {
            if (!webSurveyRunning)
                return;

            webSurveyRunning = false;
        }

        // Runs a survey in VR (spawns the survey interface / lobby transport flow)
        private IEnumerator BeginVrSurveyCoroutine(VERASurveyInfo surveyInfo, bool transportToLobby, bool dimEnvironment, float heightOffset, float distanceOffset, System.Action onSurveyComplete)
        {
            // Get the VERAFadeCanvas reference; if it does not exist, spawn it in
            if (VERAFadeCanvas.Instance == null)
            {
                VERADebugger.Log("No fade canvas found - instantiating a new one to use for survey lobby transport", "VERASurveyStarter", DebugPreference.Verbose);
                VERAFadeCanvas fadeCanvasPrefab = Resources.Load<VERAFadeCanvas>("VERAFadeCanvas");
                VERAFadeCanvas fadeCanvas = Instantiate(fadeCanvasPrefab);
                fadeCanvas.SetupCanvas();
            }

            Transform playerTransform = FindAnyObjectByType<XROrigin>().transform;
            Vector3 startPosition = playerTransform.position;

            // If transporting to a lobby, move the participant to the lobby location before starting the survey
            if (transportToLobby)
            {
                // Fade black screen in
                VERAFadeCanvas.Instance.FadeIn(1f);
                yield return new WaitForSeconds(1f);

                // Spawn the survey lobby at the transport location
                Vector3 lobbyPosition = new Vector3(0, -1000, 0);
                GameObject surveyLobbyPrefab = Resources.Load<GameObject>("VERASurveyLobby");
                if (surveyLobbyPrefab != null)
                {
                    spawnedSurveyLobby = Instantiate(surveyLobbyPrefab, lobbyPosition, Quaternion.identity);
                }
                else
                {
                    VERADebugger.LogWarning("VERASurveyLobby prefab not found in Resources folder.", "VERASurveyStarter");
                }

                // Transport participant
                playerTransform.position = lobbyPosition;
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
            activeSurveyInterface.BeginSurvey(surveyInfo, heightOffset, distanceOffset, onSurveyComplete: () =>
            {
                StartCoroutine(CleanupSurveyCoroutine(startPosition, transportToLobby, dimEnvironment, onSurveyComplete));
            });
        }

        private IEnumerator CleanupSurveyCoroutine(Vector3 startPosition, bool transportedToLobby, bool dimEnvironment, System.Action onSurveyComplete)
        {
            yield return null;

            // Destroy the spawned survey lobby if it exists
            if (spawnedSurveyLobby != null)
            {
                Destroy(spawnedSurveyLobby);
                spawnedSurveyLobby = null;
            }

            // If we transported the participant to a lobby, return them to their original position
            if (transportedToLobby)
            {
                // Fade black screen in
                VERAFadeCanvas.Instance.FadeIn(1f);
                yield return new WaitForSeconds(1f);

                // Transport participant back to original position
                Transform playerTransform = FindAnyObjectByType<XROrigin>().transform;
                playerTransform.position = startPosition;
                yield return new WaitForSeconds(0.5f);

                // Fade black screen out
                VERAFadeCanvas.Instance.FadeOut(1f);
            }
            else
            {
                if (dimEnvironment)
                    VERAFadeCanvas.Instance.FadeOut(1f);
            }

            // Invoke the onSurveyComplete callback if it was provided
            onSurveyComplete?.Invoke();
        }
    }
}
