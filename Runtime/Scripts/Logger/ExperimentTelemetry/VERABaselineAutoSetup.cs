using UnityEngine;
using System.Collections;

namespace VERA
{
    [AddComponentMenu("VERA/Baseline Auto-Setup")]
    internal class VERABaselineAutoSetup : MonoBehaviour
    {
        [Header("Auto-Setup Configuration")]
        [Tooltip("Automatically create a baseline logger if none exists in the scene")]
        public bool autoCreateBaselineLogger = true;
        
        [Tooltip("Start logging when experiment begins (IN_EXPERIMENT state)")]
        public bool startOnExperimentBegin = true;
        
        [Tooltip("Also start logging immediately when scene starts (fallback if no experiment state)")]
        public bool fallbackAutoStart = true;
        
        [Tooltip("Sampling rate for baseline data collection (samples per second)")]
        [Range(1, 120)]
        public int defaultSamplingRate = 30;

        [Header("Info")]
        [TextArea(3, 5)]
        public string info = "This component automatically sets up baseline VR data logging tied to VERA experiment lifecycle. " +
                           "Baseline logging will start when the experiment begins (participant state: IN_EXPERIMENT). " +
                           "No manual configuration needed - just add this to any GameObject in your scene.";

        private VERABaselineDataLogger createdLogger;
        private bool isMonitoringExperiment = false;

        void Awake()
        {
            // Validate column definitions early to prevent runtime errors
            VERAColumnValidator.ValidateAndFixColumnDefinitions();
            
            if (autoCreateBaselineLogger)
            {
                EnsureBaselineLoggerExists();
            }
        }

        void Start()
        {
            if (startOnExperimentBegin)
            {
                StartMonitoringExperiment();
            }
            
            // Always try fallback auto-start as well for immediate testing
            if (fallbackAutoStart)
            {
                StartCoroutine(StartImmediateBaselineLogging());
            }
        }

        private void EnsureBaselineLoggerExists()
        {
            // Check if a baseline logger already exists in the scene
#if UNITY_2023_1_OR_NEWER
            VERABaselineDataLogger existingLogger = FindAnyObjectByType<VERABaselineDataLogger>();
#else
            VERABaselineDataLogger existingLogger = FindObjectOfType<VERABaselineDataLogger>();
#endif
            
            if (existingLogger == null)
            {
                CreateBaselineLogger();
            }
            else
            {
                createdLogger = existingLogger;
                
                // Configure the existing logger for experiment-driven start
                if (startOnExperimentBegin)
                {
                    existingLogger.autoStartLogging = false; // Disable auto-start, we'll start manually
                }
                else if (fallbackAutoStart)
                {
                    existingLogger.autoStartLogging = true; // Enable auto-start
                }
            }
        }

        private void CreateBaselineLogger()
        {
            // Create a new GameObject for the baseline logger
            GameObject loggerObject = new GameObject("VERA Baseline Data Logger (Auto-Created)");
            
            // Add the baseline data logger component
            VERABaselineDataLogger logger = loggerObject.AddComponent<VERABaselineDataLogger>();
            
            // Configure with sensible defaults - always enable auto-start
            logger.autoStartLogging = true;  // Always start when VERA Logger is ready
            
            logger.SetLogRate(defaultSamplingRate);
            
            createdLogger = logger;
            
            // Try to auto-assign VR components if available
            AutoAssignVRComponents(logger);
        }

        private void StartMonitoringExperiment()
        {
            if (isMonitoringExperiment) return;
            
            // Start monitoring for experiment state changes
            StartCoroutine(MonitorExperimentState());
            isMonitoringExperiment = true;
        }

        private IEnumerator MonitorExperimentState()
        {
            while (true)
            {
                // Wait for VERA Logger to be initialized
                if (VERALogger.Instance == null || !VERALogger.Instance.initialized)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }

                // Check if participant is in experiment
                if (VERALogger.Instance.activeParticipant != null)
                {
                    var currentState = VERALogger.Instance.activeParticipant.currentParticipantProgressState;
                    
                    if (currentState == VERAParticipantManager.ParticipantProgressState.IN_EXPERIMENT)
                    {
                        StartBaselineLogging();
                        yield break; // Stop monitoring once we've started
                    }
                }
                
                yield return new WaitForSeconds(0.5f); // Check every 500ms
            }
        }

        private void StartBaselineLogging()
        {
            if (createdLogger != null && !createdLogger.IsLogging())
            {
                createdLogger.StartBaselineLogging();
            }
        }

        private void AutoAssignVRComponents(VERABaselineDataLogger logger)
        {
            // Try to find common VR camera setups
            Camera mainCamera = Camera.main;

            // Look for XR Origin or similar VR rig  
            GameObject xrOrigin = GameObject.Find("XR Origin") ?? GameObject.Find("XR Rig") ?? GameObject.Find("VR Rig");
        }

        private IEnumerator StartImmediateBaselineLogging()
        {
            // Wait a frame for all components to initialize
            yield return new WaitForEndOfFrame();
            
            // Wait for VERA Logger to be ready
            while (VERALogger.Instance == null || !VERALogger.Instance.initialized)
            {
                yield return new WaitForSeconds(1f);
            }
            
            // Check if we should start logging
            bool shouldStart = false;
            
            if (VERALogger.Instance.activeParticipant != null)
            {
                var currentState = VERALogger.Instance.activeParticipant.currentParticipantProgressState;
                if (currentState == VERAParticipantManager.ParticipantProgressState.IN_EXPERIMENT)
                {
                    shouldStart = true;
                }
                else
                {
                    shouldStart = fallbackAutoStart; // Start anyway if fallback is enabled
                }
            }
            else
            {
                shouldStart = fallbackAutoStart;
            }
            
            if (shouldStart)
            {
                StartBaselineLogging();
            }
        }

        private void OnDestroy()
        {
            isMonitoringExperiment = false;
        }
    }
}