using UnityEngine;

namespace VERA
{
    /// <summary>
    /// Public API for triggering and managing surveys in VERA experiments.
    /// Provides a simple interface for displaying surveys to participants and collecting responses.
    /// </summary>
    public static class VERASurveyManager
    {
        private static SurveyInterfaceIO instance;

        /// <summary>
        /// Displays a survey to the participant by survey ID.
        /// If a survey is already active, the new survey will be queued.
        /// </summary>
        /// <param name="surveyId">The ID of the survey from the VERA portal</param>
        /// <param name="onCompleted">Optional callback invoked when the survey is completed</param>
        public static void ShowSurvey(string surveyId, System.Action onCompleted = null)
        {
            GetOrCreateInstance().StartSurveyById(surveyId, onCompleted);
        }

        /// <summary>
        /// Queues a survey to be shown after the current survey completes.
        /// </summary>
        /// <param name="surveyId">The ID of the survey from the VERA portal</param>
        /// <param name="onCompleted">Optional callback invoked when the survey is completed</param>
        public static void QueueSurvey(string surveyId, System.Action onCompleted = null)
        {
            GetOrCreateInstance().QueueSurvey(surveyId, onCompleted);
        }

        /// <summary>
        /// Returns true if a survey is currently being displayed.
        /// </summary>
        public static bool IsSurveyActive
        {
            get { return GetOrCreateInstance().IsSurveyActive; }
        }

        /// <summary>
        /// Returns the number of surveys currently in the queue.
        /// </summary>
        public static int QueuedSurveysCount
        {
            get { return GetOrCreateInstance().QueuedSurveysCount; }
        }

        /// <summary>
        /// Clears all queued surveys (does not affect the currently active survey).
        /// </summary>
        public static void ClearQueue()
        {
            GetOrCreateInstance().ClearQueue();
        }

        private static SurveyInterfaceIO GetOrCreateInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            // Try to find existing SurveyInterfaceIO in the scene
            instance = Object.FindFirstObjectByType<SurveyInterfaceIO>();

            if (instance != null)
            {
                return instance;
            }

            // If not found, try to load and instantiate the SurveyInterface prefab
            GameObject prefab = Resources.Load<GameObject>("SurveyInterface");
            if (prefab == null)
            {
                // Try alternative path
                prefab = Resources.Load<GameObject>("Prefabs/SurveyInterface");
            }

            if (prefab != null)
            {
                GameObject surveyObject = Object.Instantiate(prefab);
                instance = surveyObject.GetComponent<SurveyInterfaceIO>();

                if (instance != null)
                {
                    Debug.Log("[VERASurveyManager] Created SurveyInterface from prefab.");
                    return instance;
                }
            }

            // If all else fails, create a new GameObject with the required components
            Debug.LogWarning("[VERASurveyManager] Could not find SurveyInterface prefab. Creating minimal survey system.");
            GameObject newSurveyObject = new GameObject("SurveyInterface");
            newSurveyObject.AddComponent<SurveyManager>();
            instance = newSurveyObject.AddComponent<SurveyInterfaceIO>();
            Object.DontDestroyOnLoad(newSurveyObject);

            return instance;
        }
    }
}
