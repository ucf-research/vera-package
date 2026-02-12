using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        [Header("Workflow Mode")]
        [Tooltip("Use the new automated workflow (recommended). When enabled, the workflow manager drives trial progression automatically via events.")]
        public bool useAutomatedWorkflow = true;

        [Header("Trial Workflow Testing")]
        [Tooltip("Auto-advance through trials (only used when useAutomatedWorkflow is false)")]
        public bool autoAdvanceTrials = false;

        [Tooltip("Time in seconds to simulate trial logic before completing a trial")]
        public float trialSimulationDelay = 3f;

        [Tooltip("Time in seconds to simulate survey completion")]
        public float surveySimulationDelay = 2f;

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

            // Log initial workflow state
            LogCurrentWorkflowState();

            if (useAutomatedWorkflow)
            {
                StartAutomatedWorkflowTest();
            }
            else if (autoAdvanceTrials)
            {
                autoAdvanceCoroutine = StartCoroutine(AutoAdvanceTrials());
            }
        }

        #region Automated Workflow Testing

        private void StartAutomatedWorkflowTest()
        {
            var workflow = VERALogger.Instance?.trialWorkflow;
            if (workflow == null)
            {
                Debug.LogError("[Mock Participant Tester] Trial workflow not available for automation.");
                return;
            }

            Debug.Log("=== STARTING AUTOMATED WORKFLOW TEST ===");
            Debug.Log($"Total workflow items: {workflow.TotalTrialCount}");
            Debug.Log($"Trial simulation delay: {trialSimulationDelay}s");
            Debug.Log($"Survey simulation delay: {surveySimulationDelay}s");
            Debug.Log($"Workflow order: {workflow.GetTrialOrderingDebugString()}");
            Debug.Log("=========================================");

            // Subscribe to automation events
            workflow.OnTrialReady += OnTrialReady;
            workflow.OnSurveyRequired += OnSurveyRequired;
            workflow.OnWorkflowCompleted += OnWorkflowCompleted;

            // Start the automated workflow
            VERALogger.Instance.StartAutomatedWorkflow();
        }

        private void OnTrialReady(TrialConfig trial)
        {
            Debug.Log($"[Mock Participant Tester] >>> TRIAL READY: {trial.label ?? "Unlabeled"} (Type: {trial.type})");

            if (trial.conditions != null && trial.conditions.Count > 0)
            {
                foreach (var kvp in trial.conditions)
                {
                    Debug.Log($"  Condition: {kvp.Key} = {kvp.Value}");
                }
            }

            if (!string.IsNullOrEmpty(trial.parentGroupId))
            {
                Debug.Log($"  Group: {trial.parentGroupId} ({trial.parentGroupType})");
            }

            // Simulate trial logic taking some time, then complete
            StartCoroutine(SimulateTrialLogic(trial));
        }

        private IEnumerator SimulateTrialLogic(TrialConfig trial)
        {
            Debug.Log($"[Mock Participant Tester] Simulating trial logic for '{trial.label}'... ({trialSimulationDelay}s)");
            yield return new WaitForSeconds(trialSimulationDelay);

            Debug.Log($"[Mock Participant Tester] Trial logic done for '{trial.label}'. Completing trial.");
            VERALogger.Instance.CompleteAutomatedTrial();
        }

        private void OnSurveyRequired(string surveyId, string surveyName, string position)
        {
            Debug.Log($"[Mock Participant Tester] >>> SURVEY REQUIRED: '{surveyName}' (ID: {surveyId}, Position: {position})");

            // Simulate survey completion
            StartCoroutine(SimulateSurveyCompletion(surveyId, surveyName, position));
        }

        private IEnumerator SimulateSurveyCompletion(string surveyId, string surveyName, string position)
        {
            Debug.Log($"[Mock Participant Tester] Simulating survey '{surveyName}' ({position})... ({surveySimulationDelay}s)");
            yield return new WaitForSeconds(surveySimulationDelay);

            // Generate mock survey responses and upload them
            yield return GenerateAndUploadMockSurveyResponses(surveyId, surveyName);

            Debug.Log($"[Mock Participant Tester] Survey '{surveyName}' completed. Marking as done.");
            VERALogger.Instance.trialWorkflow.MarkSurveyCompleted();
        }

        private IEnumerator GenerateAndUploadMockSurveyResponses(string surveyId, string surveyName)
        {
            if (verboseLogging)
            {
                Debug.Log($"[Mock Participant Tester] Generating and uploading mock survey responses...");
            }

            // Find or create SurveyInterfaceIO
            SurveyInterfaceIO surveyIO = FindAnyObjectByType<SurveyInterfaceIO>();
            if (surveyIO == null)
            {
                Debug.LogWarning("[Mock Participant Tester] No SurveyInterfaceIO found in scene. Creating CSV only (no upload).");
                GenerateMockSurveyResponsesLocal(surveyId, surveyName);
                yield break;
            }

            // Generate mock questions and answers
            int numQuestions = UnityEngine.Random.Range(3, 8);

            // Create mock SurveyInfo
            SurveyInfo mockSurvey = ScriptableObject.CreateInstance<SurveyInfo>();
            mockSurvey.surveyId = surveyId;
            mockSurvey.surveyName = surveyName;
            mockSurvey.surveyDescription = "Mock survey for testing";
            mockSurvey.surveyEndStatement = "Thank you for participating in this mock survey";
            mockSurvey.surveyQuestions = new List<SurveyQuestionInfo>();

            // Create survey results array
            KeyValuePair<string, string>[] surveyResults = new KeyValuePair<string, string>[numQuestions];

            for (int i = 0; i < numQuestions; i++)
            {
                string questionId = $"q{i + 1}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
                string questionText = $"Mock Question {i + 1}";
                string answer = GenerateMockAnswer(i);

                // Add to survey info
                SurveyQuestionInfo questionInfo = new SurveyQuestionInfo();
                questionInfo.questionId = questionId;
                questionInfo.questionText = questionText;
                questionInfo.orderInSurvey = i;
                mockSurvey.surveyQuestions.Add(questionInfo);

                // Add to results
                surveyResults[i] = new KeyValuePair<string, string>(questionId, answer);
            }

            if (verboseLogging)
            {
                Debug.Log($"[Mock Participant Tester] Uploading {numQuestions} mock survey responses to VERA...");
            }

            // Call the actual upload method
            yield return surveyIO.OutputSurveyResults(mockSurvey, surveyResults);

            if (verboseLogging)
            {
                if (surveyIO.uploadSuccessful)
                {
                    Debug.Log($"[Mock Participant Tester] ✓ Successfully uploaded mock survey responses to VERA");
                }
                else
                {
                    Debug.LogWarning($"[Mock Participant Tester] ✗ Failed to upload mock survey responses (CSV file still created locally)");
                }
            }
        }

        private void GenerateMockSurveyResponsesLocal(string surveyId, string surveyName)
        {
            try
            {
                string timestamp = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                string studyId = VERALogger.Instance.experimentUUID;
                string participantId = VERALogger.Instance.activeParticipant.participantUUID;
                string instanceId = System.Guid.NewGuid().ToString();

                // Generate mock questions and answers
                int numQuestions = UnityEngine.Random.Range(3, 8);
                List<(string questionId, string questionText, string answer)> responses = new List<(string, string, string)>();

                for (int i = 0; i < numQuestions; i++)
                {
                    string questionId = $"q{i + 1}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
                    string questionText = $"Mock Question {i + 1}";
                    string answer = GenerateMockAnswer(i);

                    responses.Add((questionId, questionText, answer));
                }

                // Create filename with participant ID and instance ID
                string filename = $"survey_responses_{participantId}_{instanceId}.csv";
                string filepath = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "SurveyResponses", filename);

                // Ensure the directory exists
                string directory = System.IO.Path.GetDirectoryName(filepath);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                // Create CSV content
                using (System.IO.StreamWriter writer = new System.IO.StreamWriter(filepath))
                {
                    // Write header
                    writer.WriteLine("timestamp,studyId,surveyId,surveyName,participantId,instanceId,questionId,questionText,answer,uploaded");

                    // Write each response as a row
                    foreach (var response in responses)
                    {
                        string questionIdEscaped = EscapeCsvField(response.questionId);
                        string questionTextEscaped = EscapeCsvField(response.questionText);
                        string answerEscaped = EscapeCsvField(response.answer);
                        string surveyNameEscaped = EscapeCsvField(surveyName);

                        writer.WriteLine($"{timestamp},{studyId},{surveyId},{surveyNameEscaped},{participantId},{instanceId},{questionIdEscaped},{questionTextEscaped},{answerEscaped},false");
                    }
                }

                if (verboseLogging)
                {
                    Debug.Log($"[Mock Participant Tester] Generated mock survey responses and saved to: {filepath}");
                    Debug.Log($"[Mock Participant Tester] Created {numQuestions} mock responses for survey '{surveyName}'");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Mock Participant Tester] Failed to generate mock survey responses: {ex.Message}");
            }
        }

        private string GenerateMockAnswer(int questionIndex)
        {
            // Generate different types of mock answers for variety
            int answerType = questionIndex % 4;

            switch (answerType)
            {
                case 0: // Multiple choice (single selection)
                    return UnityEngine.Random.Range(0, 5).ToString();

                case 1: // Selection (multiple selections)
                    int numSelections = UnityEngine.Random.Range(1, 4);
                    System.Collections.Generic.List<int> selections = new System.Collections.Generic.List<int>();
                    for (int i = 0; i < numSelections; i++)
                    {
                        selections.Add(UnityEngine.Random.Range(0, 5));
                    }
                    return string.Join(", ", selections.Distinct().OrderBy(x => x));

                case 2: // Slider (0-1 float)
                    return UnityEngine.Random.Range(0f, 1f).ToString("F2");

                case 3: // Matrix (comma-separated column indices)
                    int numRows = UnityEngine.Random.Range(3, 6);
                    System.Collections.Generic.List<int> matrixAnswers = new System.Collections.Generic.List<int>();
                    for (int i = 0; i < numRows; i++)
                    {
                        matrixAnswers.Add(UnityEngine.Random.Range(0, 5));
                    }
                    return string.Join(", ", matrixAnswers);

                default:
                    return "Mock Answer";
            }
        }

        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "";

            // If field contains comma, quote, or newline, wrap it in quotes and escape internal quotes
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }

        private void OnWorkflowCompleted()
        {
            Debug.Log("=== AUTOMATED WORKFLOW TEST COMPLETE ===");
            Debug.Log("[Mock Participant Tester] All trials and surveys have been processed!");
            Debug.Log("=========================================");

            // Unsubscribe from events
            var workflow = VERALogger.Instance?.trialWorkflow;
            if (workflow != null)
            {
                workflow.OnTrialReady -= OnTrialReady;
                workflow.OnSurveyRequired -= OnSurveyRequired;
                workflow.OnWorkflowCompleted -= OnWorkflowCompleted;
            }
        }

        #endregion

        #region Legacy Manual Auto-Advance

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
                Debug.Log($"[Mock Participant Tester] Legacy auto-advance enabled (delay: {trialSimulationDelay}s, auto-complete: {autoCompletTrials})");
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
                        yield return new WaitForSeconds(trialSimulationDelay);

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

                yield return new WaitForSeconds(trialSimulationDelay);
            }
        }

        #endregion

        #region Workflow State Logging

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

            Debug.Log($"Automated Mode: {workflow.IsAutomatedMode}");
            Debug.Log($"Waiting for Trial Logic: {workflow.IsWaitingForTrialLogic}");
            Debug.Log($"Waiting for Survey: {workflow.IsWaitingForSurvey()}");
            Debug.Log($"Trial Index: {workflow.CurrentTrialIndex + 1}/{workflow.TotalTrialCount}");

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

        #endregion

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
        /// Toggle auto-advance on/off at runtime (legacy mode only).
        /// </summary>
        public void ToggleAutoAdvance()
        {
            if (useAutomatedWorkflow)
            {
                Debug.LogWarning("[Mock Participant Tester] Use StartAutomatedWorkflow/StopAutomatedWorkflow in automated mode.");
                return;
            }

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

            // Unsubscribe from workflow events
            var workflow = VERALogger.Instance?.trialWorkflow;
            if (workflow != null)
            {
                workflow.OnTrialReady -= OnTrialReady;
                workflow.OnSurveyRequired -= OnSurveyRequired;
                workflow.OnWorkflowCompleted -= OnWorkflowCompleted;
            }

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
