using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VERA
{
    /// <summary>
    /// Simple test runner for automated workflow testing.
    /// Automatically runs through the trial workflow, simulating a mock participant.
    /// </summary>
    [AddComponentMenu("VERA/Mock Test Runner")]
    internal class VERAMockTestRunner : MonoBehaviour
    {
        [Header("Test Settings")]
        [Tooltip("Time in seconds to simulate trial logic")]
        public float trialSimulationDelay = 3f;

        [Tooltip("Time in seconds to simulate survey completion")]
        public float surveySimulationDelay = 2f;

        [Tooltip("Enable verbose logging for debugging")]
        public bool verboseLogging = true;

        private void Start()
        {
            // Subscribe to VERA initialization
            VERASessionManager.onInitialized.AddListener(OnVERAInitialized);

            if (verboseLogging)
            {
                Debug.Log("[VERA Mock Test] Waiting for VERA to initialize...");
            }
        }

        private void OnVERAInitialized()
        {
            if (verboseLogging)
            {
                Debug.Log("[VERA Mock Test] VERA initialized. Starting automated workflow test...");
            }

            var workflow = VERALogger.Instance?.trialWorkflow;
            if (workflow == null)
            {
                Debug.LogError("[VERA Mock Test] Could not access trial workflow.");
                return;
            }

            // Subscribe to automation events
            workflow.OnTrialReady += OnTrialReady;
            workflow.OnSurveyRequired += OnSurveyRequired;
            workflow.OnWorkflowCompleted += OnWorkflowCompleted;

            // Start the automated workflow
            VERALogger.Instance.StartAutomatedWorkflow();
        }

        private void OnTrialReady(TrialConfig trial)
        {
            if (verboseLogging)
            {
                Debug.Log($"[VERA Mock Test] Trial ready: {trial.label}");
                if (trial.conditions != null && trial.conditions.Count > 0)
                {
                    foreach (var condition in trial.conditions)
                    {
                        Debug.Log($"[VERA Mock Test]   - {condition.Key} = {condition.Value}");
                    }
                }
            }

            // Simulate trial logic
            StartCoroutine(SimulateTrialLogic(trial));
        }

        private IEnumerator SimulateTrialLogic(TrialConfig trial)
        {
            if (verboseLogging)
            {
                Debug.Log($"[VERA Mock Test] Simulating trial logic for {trialSimulationDelay}s...");
            }

            yield return new WaitForSeconds(trialSimulationDelay);

            if (verboseLogging)
            {
                Debug.Log($"[VERA Mock Test] Trial logic complete. Marking trial as done.");
            }

            // Complete the trial
            VERALogger.Instance.CompleteAutomatedTrial();
        }

        private void OnSurveyRequired(string surveyId, string surveyName, string position)
        {
            if (verboseLogging)
            {
                Debug.Log($"[VERA Mock Test] Survey required: {surveyName} (position: {position})");
            }

            // Simulate survey completion
            StartCoroutine(SimulateSurveyCompletion(surveyId, surveyName, position));
        }

        private IEnumerator SimulateSurveyCompletion(string surveyId, string surveyName, string position)
        {
            if (verboseLogging)
            {
                Debug.Log($"[VERA Mock Test] Simulating survey completion for {surveySimulationDelay}s...");
            }

            yield return new WaitForSeconds(surveySimulationDelay);

            // Generate mock survey responses and upload them
            yield return GenerateAndUploadMockSurveyResponses(surveyId, surveyName);

            if (verboseLogging)
            {
                Debug.Log($"[VERA Mock Test] Survey complete. Marking as done.");
            }

            // Mark survey as completed
            var workflow = VERALogger.Instance?.trialWorkflow;
            workflow?.MarkSurveyCompleted();
        }

        private IEnumerator GenerateAndUploadMockSurveyResponses(string surveyId, string surveyName)
        {
            if (verboseLogging)
            {
                Debug.Log($"[VERA Mock Test] Generating and uploading mock survey responses...");
            }

            // Find or create SurveyInterfaceIO
            SurveyInterfaceIO surveyIO = FindAnyObjectByType<SurveyInterfaceIO>();
            if (surveyIO == null)
            {
                if (verboseLogging)
                {
                    Debug.Log($"[VERA Mock Test] No SurveyInterfaceIO found in scene. Auto-creating one with API URL: {VERAHost.hostUrl}");
                }

                // Create a new GameObject with SurveyInterfaceIO component
                // Note: SurveyManager is not required for API-only operations (mock tests)
                // SurveyInterfaceIO will operate in API-only mode without UI components
                GameObject surveyIOObject = new GameObject("SurveyInterfaceIO (Auto-Created)");
                surveyIO = surveyIOObject.AddComponent<SurveyInterfaceIO>();

                // SurveyInterfaceIO.Awake() automatically configures apiUrl from VERAHost.hostUrl
                if (verboseLogging)
                {
                    Debug.Log("[VERA Mock Test] SurveyInterfaceIO created and configured successfully.");
                }
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
                Debug.Log($"[VERA Mock Test] Uploading {numQuestions} mock survey responses to VERA...");
            }

            // Call the actual upload method
            yield return surveyIO.OutputSurveyResults(mockSurvey, surveyResults);

            if (verboseLogging)
            {
                if (surveyIO.uploadSuccessful)
                {
                    Debug.Log($"[VERA Mock Test] ✓ Successfully uploaded mock survey responses to VERA");
                }
                else
                {
                    Debug.LogWarning($"[VERA Mock Test] ✗ Failed to upload mock survey responses (CSV file still created locally)");
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
                    Debug.Log($"[VERA Mock Test] Generated mock survey responses and saved to: {filepath}");
                    Debug.Log($"[VERA Mock Test] Created {numQuestions} mock responses for survey '{surveyName}'");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[VERA Mock Test] Failed to generate mock survey responses: {ex.Message}");
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
                    List<int> selections = new List<int>();
                    for (int i = 0; i < numSelections; i++)
                    {
                        selections.Add(UnityEngine.Random.Range(0, 5));
                    }
                    return string.Join(", ", selections.Distinct().OrderBy(x => x));

                case 2: // Slider (0-1 float)
                    return UnityEngine.Random.Range(0f, 1f).ToString("F2");

                case 3: // Matrix (comma-separated column indices)
                    int numRows = UnityEngine.Random.Range(3, 6);
                    List<int> matrixAnswers = new List<int>();
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
            Debug.Log("[VERA Mock Test] ✓ Automated workflow test completed successfully!");
            Debug.Log($"[VERA Mock Test] All trials and surveys were processed.");
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            var workflow = VERALogger.Instance?.trialWorkflow;
            if (workflow != null)
            {
                workflow.OnTrialReady -= OnTrialReady;
                workflow.OnSurveyRequired -= OnSurveyRequired;
                workflow.OnWorkflowCompleted -= OnWorkflowCompleted;
            }
        }
    }
}
