using System;
using UnityEngine;

namespace VERA
{
    /// <summary>
    /// Example script showing how to integrate baseline data logging with existing VERA experiments.
    /// This demonstrates best practices for using baseline data in research scenarios.
    /// </summary>
    internal class VERABaselineDataIntegrationExample : MonoBehaviour
    {
        [Header("Integration Settings")]
        [Tooltip("Reference to the baseline data logger")]
        [SerializeField] private VERABaselineDataLogger baselineLogger;
        
        [Tooltip("Custom event file type name for correlating with baseline data")]
        [SerializeField] private string customEventFileType = "ExperimentEvents";
        
        [Header("Event Correlation")]
        [Tooltip("Log baseline sample index with custom events for correlation")]
        [SerializeField] private bool correlateEventsWithBaseline = true;
        
        private void Start()
        {
            SetupBaselineIntegration();
        }
        
        private void SetupBaselineIntegration()
        {
            // Find or create baseline logger
            if (baselineLogger == null)
            {
#if UNITY_2023_1_OR_NEWER
                baselineLogger = FindAnyObjectByType<VERABaselineDataLogger>();
#else
                baselineLogger = FindObjectOfType<VERABaselineDataLogger>();
#endif
                
                if (baselineLogger == null)
                {
                    Debug.LogWarning("[Baseline Integration] No baseline logger found. Consider adding VERABaselineDataLogger to your scene.");
                    return;
                }
            }
            
            // Wait for VERA Logger to be ready
            if (VERALogger.Instance != null && VERALogger.Instance.initialized)
            {
                OnVERALoggerReady();
            }
            else if (VERALogger.Instance != null)
            {
                VERALogger.Instance.onLoggerInitialized.AddListener(OnVERALoggerReady);
            }
        }
        
        private void OnVERALoggerReady()
        {
            // VERA Logger ready - baseline data logging active
            
            // Example: Log an experiment start event with baseline correlation
            LogExperimentEvent("ExperimentStart", "Experiment session initiated");
        }
        
        public void LogExperimentEvent(string eventType, string eventDescription)
        {
            if (VERALogger.Instance == null || !VERALogger.Instance.collecting)
                return;
            
            try
            {
                if (correlateEventsWithBaseline && baselineLogger != null)
                {
                    // Get current baseline sample index for correlation
                    int baselineSample = baselineLogger.GetCurrentSampleIndex();
                    
                    // Log event with baseline correlation
                    VERALogger.Instance.CreateCsvEntry(customEventFileType, 
                        1, // event type ID
                        eventType,
                        eventDescription,
                        baselineSample, // baseline sample index for correlation
                        Time.time, // Unity time
                        DateTime.UtcNow.ToString("o") // precise timestamp
                    );
                }
                else
                {
                    // Log event without baseline correlation
                    VERALogger.Instance.CreateCsvEntry(customEventFileType,
                        1,
                        eventType,
                        eventDescription,
                        Time.time
                    );
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Baseline Integration] Error logging event: {e.Message}");
            }
        }
        
        public void OnObjectInteraction(GameObject interactedObject, string interactionType)
        {
            string eventDescription = $"Participant {interactionType} object: {interactedObject.name}";
            LogExperimentEvent("ObjectInteraction", eventDescription);
        }
        
        public void OnTaskCompleted(string taskName, float completionTime, int score)
        {
            string eventDescription = $"Task: {taskName}, Time: {completionTime:F2}s, Score: {score}";
            LogExperimentEvent("TaskCompleted", eventDescription);
        }
        
        public void OnPhaseTransition(string fromPhase, string toPhase)
        {
            string eventDescription = $"Phase transition: {fromPhase} -> {toPhase}";
            LogExperimentEvent("PhaseTransition", eventDescription);
        }
        
        public BaselineCorrelationInfo GetBaselineCorrelationInfo()
        {
            if (baselineLogger == null)
                return new BaselineCorrelationInfo { isValid = false };
                
            return new BaselineCorrelationInfo
            {
                isValid = true,
                currentSampleIndex = baselineLogger.GetCurrentSampleIndex(),
                logRate = baselineLogger.GetLogRate(),
                isLogging = baselineLogger.IsLogging(),
                timestamp = System.DateTime.UtcNow
            };
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                OnObjectInteraction(gameObject, "entered");
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                OnObjectInteraction(gameObject, "exited");
            }
        }
        
        [System.Serializable]
        public struct BaselineCorrelationInfo
        {
            public bool isValid;
            public int currentSampleIndex;
            public float logRate;
            public bool isLogging;
            public System.DateTime timestamp;
            
            public override string ToString()
            {
                return $"Baseline Sample: {currentSampleIndex}, Rate: {logRate}Hz, Logging: {isLogging}";
            }
        }
        
        #region Public API for External Integration
        

        public static void LogEventWithBaseline(string eventType, string description)
        {
#if UNITY_2023_1_OR_NEWER
            var integrator = FindAnyObjectByType<VERABaselineDataIntegrationExample>();
#else
            var integrator = FindObjectOfType<VERABaselineDataIntegrationExample>();
#endif
            if (integrator != null)
            {
                integrator.LogExperimentEvent(eventType, description);
            }
            else
            {
                // No integration example found in scene
            }
        }

        public static int GetCurrentBaselineSample()
        {
#if UNITY_2023_1_OR_NEWER
            var integrator = FindAnyObjectByType<VERABaselineDataIntegrationExample>();
#else
            var integrator = FindObjectOfType<VERABaselineDataIntegrationExample>();
#endif
            if (integrator != null && integrator.baselineLogger != null)
            {
                return integrator.baselineLogger.GetCurrentSampleIndex();
            }
            return -1;
        }
        
        #endregion
        
        #region Debug and Testing
        
        [ContextMenu("Test Event Logging")]
        private void TestEventLogging()
        {
            LogExperimentEvent("TestEvent", "This is a test event for debugging");
        }
        
        [ContextMenu("Print Baseline Status")]
        private void PrintBaselineStatus()
        {
            var info = GetBaselineCorrelationInfo();
            // Print baseline status for debugging
        }
        
        #endregion
    }
}