using UnityEngine;

namespace VERA
{
    /// <summary>
    /// Example script demonstrating how to use VERASurveyManager to display surveys
    /// at different points in an experiment.
    /// </summary>
    public class ExperimentSurveyExample : MonoBehaviour
    {
        [Header("Survey IDs")]
        [Tooltip("Survey ID to show before the experiment starts")]
        public string preSurveyId = "your-pre-survey-id";

        [Tooltip("Survey ID to show during the experiment (e.g., after certain trials)")]
        public string midSurveyId = "your-mid-survey-id";

        [Tooltip("Survey ID to show after the experiment ends")]
        public string postSurveyId = "your-post-survey-id";

        [Header("Example Settings")]
        [Tooltip("Show mid-survey after this many trials")]
        public int trialsBeforeMidSurvey = 5;

        private int currentTrial = 0;

        void Start()
        {
            // Example 1: Show a pre-survey before starting the experiment
            Debug.Log("[Example] Showing pre-survey...");
            VERASurveyManager.ShowSurvey(preSurveyId, OnPreSurveyComplete);
        }

        void OnPreSurveyComplete()
        {
            Debug.Log("[Example] Pre-survey completed. Starting experiment...");
            StartExperiment();
        }

        void StartExperiment()
        {
            Debug.Log("[Example] Experiment started.");
            // Your experiment initialization code here
        }

        // Call this method when a trial is completed
        public void OnTrialComplete()
        {
            currentTrial++;
            Debug.Log($"[Example] Trial {currentTrial} completed.");

            // Example 2: Show a mid-experiment survey after specific trials
            if (currentTrial == trialsBeforeMidSurvey)
            {
                Debug.Log("[Example] Showing mid-experiment survey...");
                VERASurveyManager.ShowSurvey(midSurveyId, OnMidSurveyComplete);
            }
        }

        void OnMidSurveyComplete()
        {
            Debug.Log("[Example] Mid-survey completed. Continuing experiment...");
            // Continue with experiment
        }

        // Call this method when the experiment ends
        public void OnExperimentComplete()
        {
            Debug.Log("[Example] Experiment complete. Showing post-survey...");

            // Example 3: Show a post-survey at the end
            VERASurveyManager.ShowSurvey(postSurveyId, OnPostSurveyComplete);
        }

        void OnPostSurveyComplete()
        {
            Debug.Log("[Example] Post-survey completed. Finalizing session...");
            // Optionally finalize the VERA session
            // VERASessionManager.FinalizeSession();
        }

        // Example 4: Queue multiple surveys to be shown in sequence
        public void ShowMultipleSurveysInSequence()
        {
            Debug.Log("[Example] Queueing multiple surveys...");
            VERASurveyManager.ShowSurvey("survey-1", () => Debug.Log("Survey 1 complete"));
            VERASurveyManager.QueueSurvey("survey-2", () => Debug.Log("Survey 2 complete"));
            VERASurveyManager.QueueSurvey("survey-3", () => Debug.Log("Survey 3 complete"));
            // They will be shown one after another automatically
        }

        // Example 5: Check if a survey is currently active
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                if (VERASurveyManager.IsSurveyActive)
                {
                    Debug.Log($"[Example] A survey is currently active. {VERASurveyManager.QueuedSurveysCount} surveys in queue.");
                }
                else
                {
                    Debug.Log("[Example] No survey is currently active.");
                }
            }
        }
    }
}
