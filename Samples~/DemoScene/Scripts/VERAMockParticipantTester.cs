using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VERA
{
    /// <summary>
    /// Mock participant testing tool for the VERA trial workflow.
    /// Allows creation of mock participants and automated trial progression for testing.
    /// </summary>
    internal class VERAMockParticipantTester : MonoBehaviour
    {
        [Header("Mock Participant Settings")]
        [Tooltip("Enable to create a mock participant automatically on start")]
        public bool createMockParticipantOnStart = true;

        [Tooltip("Use a specific participant ID (leave empty for auto-generated)")]
        public string overrideParticipantId = "";

        [Header("Trial Workflow Testing")]
        [Tooltip("Auto-advance through trials (useful for testing workflow progression)")]
        public bool autoAdvanceTrials = false;

        [Tooltip("Time in seconds between auto-advancing trials")]
        public float autoAdvanceDelay = 3f;

        [Tooltip("Auto-complete trials when advancing (simulates participant completing tasks)")]
        public bool autoCompletTrials = true;

        [Header("Manual Between-Subjects Conditions")]
        [Tooltip("Set specific between-subjects conditions (format: 'conditionName:value')")]
        public List<string> manualConditions = new List<string>();

        [Header("Debugging")]
        [Tooltip("Enable verbose logging for testing")]
        public bool verboseLogging = true;

        [Tooltip("Log trial workflow state every N seconds (0 = disabled)")]
        public float logWorkflowStateInterval = 5f;

        private bool isInitialized = false;
        private Coroutine autoAdvanceCoroutine;
        private Coroutine loggingCoroutine;

        void Start()
        {
            // Subscribe to VERA initialization event
            VERASessionManager.onInitialized.AddListener(OnVERAInitialized);

            if (verboseLogging)
            {
                Debug.Log("[Mock Participant Tester] Waiting for VERA to initialize...");
            }
        }

        private void OnVERAInitialized()
        {
            isInitialized = true;
            if (verboseLogging)
            {
                Debug.Log("[Mock Participant Tester] VERA initialized successfully!");
            }

            // Apply manual between-subjects conditions if specified
            if (manualConditions.Count > 0)
            {
                ApplyManualConditions();
            }

            // Start periodic workflow state logging if enabled
            if (logWorkflowStateInterval > 0)
            {
                loggingCoroutine = StartCoroutine(PeriodicWorkflowLogging());
            }

            // Start auto-advance if enabled
            if (autoAdvanceTrials)
            {
                autoAdvanceCoroutine = StartCoroutine(AutoAdvanceTrials());
            }

            // Log initial workflow state
            LogCurrentWorkflowState();
        }

        private void ApplyManualConditions()
        {
            if (VERALogger.Instance?.trialWorkflow == null)
            {
                Debug.LogError("[Mock Participant Tester] Cannot apply conditions - trial workflow not available");
                return;
            }

            var assignments = new Dictionary<string, int>();
            foreach (string condition in manualConditions)
            {
                if (string.IsNullOrEmpty(condition)) continue;

                string[] parts = condition.Split(':');
                if (parts.Length == 2)
                {
                    string conditionName = parts[0].Trim();
                    if (int.TryParse(parts[1].Trim(), out int value))
                    {
                        assignments[conditionName] = value;
                        if (verboseLogging)
                        {
                            Debug.Log($"[Mock Participant Tester] Setting condition '{conditionName}' = {value}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[Mock Participant Tester] Invalid condition value: {condition}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[Mock Participant Tester] Invalid condition format: {condition} (use 'name:value')");
                }
            }

            if (assignments.Count > 0)
            {
                VERALogger.Instance.trialWorkflow.manualBetweenSubjectsAssignments = assignments;
                if (verboseLogging)
                {
                    Debug.Log($"[Mock Participant Tester] Applied {assignments.Count} manual conditions");
                }
            }
        }

        private IEnumerator AutoAdvanceTrials()
        {
            if (verboseLogging)
            {
                Debug.Log($"[Mock Participant Tester] Auto-advance enabled (delay: {autoAdvanceDelay}s, auto-complete: {autoCompletTrials})");
            }

            // Wait a moment for everything to settle
            yield return new WaitForSeconds(1f);

            while (true)
            {
                if (VERALogger.Instance?.trialWorkflow != null)
                {
                    var currentTrial = VERALogger.Instance.trialWorkflow.GetCurrentTrial();

                    if (currentTrial != null)
                    {
                        if (verboseLogging)
                        {
                            Debug.Log($"[Mock Participant Tester] Current trial: {currentTrial.label} (ID: {currentTrial.id}, Type: {currentTrial.type})");
                        }

                        // Complete current trial if enabled
                        if (autoCompletTrials)
                        {
                            if (verboseLogging)
                            {
                                Debug.Log($"[Mock Participant Tester] Completing trial: {currentTrial.label}");
                            }
                            VERALogger.Instance.trialWorkflow.CompleteTrial();
                        }

                        // Wait before advancing
                        yield return new WaitForSeconds(autoAdvanceDelay);

                        // Advance to next trial
                        if (verboseLogging)
                        {
                            Debug.Log("[Mock Participant Tester] Advancing to next trial...");
                        }
                        VERALogger.Instance.trialWorkflow.StartNextTrial();
                    }
                    else
                    {
                        if (verboseLogging)
                        {
                            Debug.Log("[Mock Participant Tester] No current trial - workflow may be complete");
                        }
                        yield break;
                    }
                }

                yield return new WaitForSeconds(autoAdvanceDelay);
            }
        }

        private IEnumerator PeriodicWorkflowLogging()
        {
            while (true)
            {
                yield return new WaitForSeconds(logWorkflowStateInterval);
                LogCurrentWorkflowState();
            }
        }

        private void LogCurrentWorkflowState()
        {
            if (!isInitialized || VERALogger.Instance?.trialWorkflow == null)
            {
                return;
            }

            var workflow = VERALogger.Instance.trialWorkflow;
            var currentTrial = workflow.GetCurrentTrial();
            var participant = VERALogger.Instance.activeParticipant;

            Debug.Log("=== VERA Trial Workflow State ===");

            if (participant != null)
            {
                Debug.Log($"Participant ID: {participant.participantShortId}");
                Debug.Log($"Participant UUID: {participant.participantUUID}");
                Debug.Log($"Participant State: {participant.currentParticipantProgressState}");
            }

            if (currentTrial != null)
            {
                Debug.Log($"Current Trial: {currentTrial.label}");
                Debug.Log($"  - ID: {currentTrial.id}");
                Debug.Log($"  - Type: {currentTrial.type}");
                Debug.Log($"  - Order: {currentTrial.order}");
                Debug.Log($"  - Description: {currentTrial.description}");

                if (currentTrial.conditions != null && currentTrial.conditions.Count > 0)
                {
                    Debug.Log("  - Conditions:");
                    foreach (var kvp in currentTrial.conditions)
                    {
                        Debug.Log($"    * {kvp.Key} = {kvp.Value}");
                    }
                }

                if (!string.IsNullOrEmpty(currentTrial.attachedSurveyId))
                {
                    Debug.Log($"  - Attached Survey: {currentTrial.attachedSurveyName} (Position: {currentTrial.surveyPosition})");
                }
            }
            else
            {
                Debug.Log("Current Trial: None (workflow may be complete or not started)");
            }

            Debug.Log("================================");
        }

        #region Public API for Manual Testing

        /// <summary>
        /// Manually create a mock participant. Call this from other scripts or Unity events.
        /// </summary>
        public void CreateMockParticipant()
        {
            if (VERALogger.Instance?.activeParticipant == null)
            {
                Debug.LogError("[Mock Participant Tester] VERALogger or participant manager not available");
                return;
            }

            if (!string.IsNullOrEmpty(overrideParticipantId))
            {
                StartCoroutine(VERALogger.Instance.activeParticipant.CreateParticipant(overrideParticipantId));
                if (verboseLogging)
                {
                    Debug.Log($"[Mock Participant Tester] Creating mock participant with ID: {overrideParticipantId}");
                }
            }
            else
            {
                StartCoroutine(VERALogger.Instance.activeParticipant.CreateParticipant());
                if (verboseLogging)
                {
                    Debug.Log("[Mock Participant Tester] Creating mock participant with auto-generated ID");
                }
            }
        }

        /// <summary>
        /// Advance to the next trial in the workflow.
        /// </summary>
        public void AdvanceToNextTrial()
        {
            if (VERALogger.Instance?.trialWorkflow == null)
            {
                Debug.LogError("[Mock Participant Tester] Trial workflow not available");
                return;
            }

            var currentTrial = VERALogger.Instance.trialWorkflow.GetCurrentTrial();
            if (currentTrial != null && verboseLogging)
            {
                Debug.Log($"[Mock Participant Tester] Advancing from trial: {currentTrial.label}");
            }

            VERALogger.Instance.trialWorkflow.StartNextTrial();
        }

        /// <summary>
        /// Complete the current trial and advance to the next.
        /// </summary>
        public void CompleteCurrentTrial()
        {
            if (VERALogger.Instance?.trialWorkflow == null)
            {
                Debug.LogError("[Mock Participant Tester] Trial workflow not available");
                return;
            }

            var currentTrial = VERALogger.Instance.trialWorkflow.GetCurrentTrial();
            if (currentTrial != null && verboseLogging)
            {
                Debug.Log($"[Mock Participant Tester] Completing trial: {currentTrial.label}");
            }

            VERALogger.Instance.trialWorkflow.CompleteTrial();
            VERALogger.Instance.trialWorkflow.StartNextTrial();
        }

        /// <summary>
        /// Set a specific between-subjects condition value.
        /// </summary>
        public void SetCondition(string conditionName, int value)
        {
            if (VERALogger.Instance?.trialWorkflow == null)
            {
                Debug.LogError("[Mock Participant Tester] Trial workflow not available");
                return;
            }

            if (VERALogger.Instance.trialWorkflow.manualBetweenSubjectsAssignments == null)
            {
                VERALogger.Instance.trialWorkflow.manualBetweenSubjectsAssignments = new Dictionary<string, int>();
            }

            VERALogger.Instance.trialWorkflow.manualBetweenSubjectsAssignments[conditionName] = value;

            if (verboseLogging)
            {
                Debug.Log($"[Mock Participant Tester] Set condition '{conditionName}' = {value}");
            }
        }

        /// <summary>
        /// Log the current workflow state to the console.
        /// </summary>
        public void LogWorkflowState()
        {
            LogCurrentWorkflowState();
        }

        /// <summary>
        /// Get the current trial configuration.
        /// </summary>
        public TrialConfig GetCurrentTrial()
        {
            return VERALogger.Instance?.trialWorkflow?.GetCurrentTrial();
        }

        /// <summary>
        /// Toggle auto-advance on/off at runtime.
        /// </summary>
        public void ToggleAutoAdvance()
        {
            autoAdvanceTrials = !autoAdvanceTrials;

            if (autoAdvanceTrials && autoAdvanceCoroutine == null)
            {
                autoAdvanceCoroutine = StartCoroutine(AutoAdvanceTrials());
                Debug.Log("[Mock Participant Tester] Auto-advance enabled");
            }
            else if (!autoAdvanceTrials && autoAdvanceCoroutine != null)
            {
                StopCoroutine(autoAdvanceCoroutine);
                autoAdvanceCoroutine = null;
                Debug.Log("[Mock Participant Tester] Auto-advance disabled");
            }
        }

        #endregion

        void OnDestroy()
        {
            // Clean up event listener
            VERASessionManager.onInitialized.RemoveListener(OnVERAInitialized);

            // Stop coroutines
            if (autoAdvanceCoroutine != null)
            {
                StopCoroutine(autoAdvanceCoroutine);
            }
            if (loggingCoroutine != null)
            {
                StopCoroutine(loggingCoroutine);
            }
        }

        #region Unity Editor Buttons (visible in Inspector)

        [ContextMenu("Create Mock Participant Now")]
        private void EditorCreateMockParticipant()
        {
            CreateMockParticipant();
        }

        [ContextMenu("Advance to Next Trial")]
        private void EditorAdvanceToNextTrial()
        {
            AdvanceToNextTrial();
        }

        [ContextMenu("Complete Current Trial")]
        private void EditorCompleteCurrentTrial()
        {
            CompleteCurrentTrial();
        }

        [ContextMenu("Log Current Workflow State")]
        private void EditorLogWorkflowState()
        {
            LogWorkflowState();
        }

        [ContextMenu("Toggle Auto-Advance")]
        private void EditorToggleAutoAdvance()
        {
            ToggleAutoAdvance();
        }

        #endregion
    }
}
