using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

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

        private void OnSurveyRequired(string surveyId, string surveyName, string position, string instanceId)
        {
            if (verboseLogging)
            {
                Debug.Log($"[VERA Mock Test] Survey required: {surveyName} (position: {position}, instanceId: {instanceId})");
            }

            // Simulate survey completion
            StartCoroutine(SimulateSurveyCompletion(surveyId, surveyName, position, instanceId));
        }

        private IEnumerator SimulateSurveyCompletion(string surveyId, string surveyName, string position, string instanceId)
        {
            if (verboseLogging)
            {
                Debug.Log($"[VERA Mock Test] Simulating survey completion for {surveySimulationDelay}s...");
            }

            yield return new WaitForSeconds(surveySimulationDelay);

            // Generate mock survey responses and upload them
            yield return GenerateAndUploadMockSurveyResponses(surveyId, surveyName, instanceId);

            if (verboseLogging)
            {
                Debug.Log($"[VERA Mock Test] Survey complete. Marking as done and continuing workflow.");
            }

            // Always mark survey as completed, even if upload failed
            // This ensures the workflow can progress
            var workflow = VERALogger.Instance?.trialWorkflow;
            if (workflow != null)
            {
                workflow.MarkSurveyCompleted();

                if (verboseLogging)
                {
                    Debug.Log($"[VERA Mock Test] Survey marked complete. Workflow should now continue.");
                }
            }
            else
            {
                Debug.LogError($"[VERA Mock Test] Cannot mark survey complete - workflow is null!");
            }
        }

        private IEnumerator GenerateAndUploadMockSurveyResponses(string surveyId, string surveyName, string instanceId)
        {
            if (verboseLogging)
            {
                Debug.Log($"[VERA Mock Test] Generating and uploading mock survey responses...");
                Debug.Log($"[VERA Mock Test]   surveyId: {surveyId}");
                Debug.Log($"[VERA Mock Test]   surveyName: {surveyName}");
                Debug.Log($"[VERA Mock Test]   instanceId: {instanceId}");
            }

            // Get survey data from the workflow (which already has it embedded)
            var workflow = VERALogger.Instance?.trialWorkflow;
            if (workflow == null)
            {
                Debug.LogWarning($"[VERA Mock Test] Workflow not available. Falling back to local CSV recording only.");
                GenerateMockSurveyResponsesLocal(surveyId, surveyName);
                yield return null; // Always return normally so caller can mark survey complete
                yield break;
            }

            Debug.Log($"[VERA Mock Test] AllWorkflowItems count: {workflow.AllWorkflowItems?.Count ?? 0}");

            VERASurvey surveyData = GetSurveyDataFromWorkflow(surveyId);
            if (surveyData == null || surveyData.questions == null || surveyData.questions.Count == 0)
            {
                Debug.LogWarning($"[VERA Mock Test] Survey data not found in workflow. Attempting to fetch from API...");

                // Try to fetch survey data from the API
                yield return FetchSurveyDataFromAPI(surveyId, (fetchedSurvey) =>
                {
                    surveyData = fetchedSurvey;
                });

                if (surveyData == null || surveyData.questions == null || surveyData.questions.Count == 0)
                {
                    Debug.LogWarning($"[VERA Mock Test] Survey data could not be fetched from API. Falling back to local CSV recording only.");
                    GenerateMockSurveyResponsesLocal(surveyId, surveyName);
                    yield return null; // Always return normally so caller can mark survey complete
                    yield break;
                }
                else
                {
                    Debug.Log($"[VERA Mock Test] ✓ Successfully fetched survey data from API with {surveyData.questions.Count} questions");
                }
            }

            // Find or create SurveyInterfaceIO
            SurveyInterfaceIO surveyIO = FindAnyObjectByType<SurveyInterfaceIO>();
            if (surveyIO == null)
            {
                if (verboseLogging)
                {
                    Debug.Log($"[VERA Mock Test] No SurveyInterfaceIO found in scene. Auto-creating one with API URL: {VERAHost.hostUrl}");
                }

                GameObject surveyIOObject = new GameObject("SurveyInterfaceIO (Auto-Created)");
                surveyIO = surveyIOObject.AddComponent<SurveyInterfaceIO>();

                if (verboseLogging)
                {
                    Debug.Log("[VERA Mock Test] SurveyInterfaceIO created and configured successfully.");
                }
            }

            // Convert Survey to SurveyInfo
            VERASurveyInfo surveyInfo = ConvertSurveyToSurveyInfo(surveyData, instanceId);
            int numQuestions = surveyInfo.surveyQuestions.Count;

            // Create survey results array using real question IDs from workflow
            KeyValuePair<string, string>[] surveyResults = new KeyValuePair<string, string>[numQuestions];

            for (int i = 0; i < numQuestions; i++)
            {
                string questionId = surveyInfo.surveyQuestions[i].questionId;
                string answer = GenerateMockAnswer(i);
                surveyResults[i] = new KeyValuePair<string, string>(questionId, answer);
            }

            if (verboseLogging)
            {
                Debug.Log($"[VERA Mock Test] Uploading {numQuestions} mock survey responses to VERA...");
            }

            // Call the actual upload method
            yield return surveyIO.OutputSurveyResults(surveyInfo, surveyResults);

            // Give the CSV handler time to flush entries to disk
            yield return new WaitForSeconds(0.5f);

            if (verboseLogging)
            {
                if (surveyIO.uploadSuccessful)
                {
                    Debug.Log($"[VERA Mock Test] ✓ Successfully uploaded mock survey responses to VERA");
                }
                else
                {
                    Debug.LogWarning($"[VERA Mock Test] ✗ Failed to upload mock survey responses to API (CSV file still created locally)");
                }
            }
        }

        private VERASurvey GetSurveyDataFromWorkflow(string surveyId)
        {
            var workflow = VERALogger.Instance?.trialWorkflow;
            if (workflow == null || workflow.AllWorkflowItems == null)
            {
                Debug.LogWarning($"[VERA Mock Test] Workflow or AllWorkflowItems is null");
                return null;
            }

            Debug.Log($"[VERA Mock Test] Searching for survey ID '{surveyId}' in {workflow.AllWorkflowItems.Count} workflow items");

            // Search through all trials to find the survey with this ID
            foreach (var trial in workflow.AllWorkflowItems)
            {
                if (trial == null)
                    continue;

                if (verboseLogging)
                {
                    Debug.Log($"[VERA Mock Test]   Checking trial: type='{trial.type}', surveyId='{trial.surveyId}', instanceId='{trial.instanceId}', hasSurveyData={trial.survey != null}");
                }

                // Check if this is a standalone survey with matching surveyId
                if (trial.type == "survey" && trial.surveyId == surveyId && trial.survey != null)
                {
                    Debug.Log($"[VERA Mock Test] ✓ Found survey data for standalone survey '{surveyId}' with {trial.survey.questions?.Count ?? 0} questions");
                    return trial.survey;
                }

                // Also check by instanceId (surveys use instanceId as the identifier)
                if (trial.type == "survey" && trial.instanceId == surveyId && trial.survey != null)
                {
                    Debug.Log($"[VERA Mock Test] ✓ Found survey data by instanceId '{surveyId}' with {trial.survey.questions?.Count ?? 0} questions");
                    return trial.survey;
                }

                // Also check attached surveys
                if (trial.attachedSurveyId == surveyId && trial.survey != null)
                {
                    Debug.Log($"[VERA Mock Test] ✓ Found survey data for attached survey '{surveyId}' with {trial.survey.questions?.Count ?? 0} questions");
                    return trial.survey;
                }
            }

            Debug.LogWarning($"[VERA Mock Test] Survey data not found for ID '{surveyId}'");
            return null;
        }

        private VERASurveyInfo ConvertSurveyToSurveyInfo(VERASurvey survey, string instanceId)
        {
            VERASurveyInfo surveyInfo = ScriptableObject.CreateInstance<VERASurveyInfo>();
            surveyInfo.surveyName = survey.surveyName;
            surveyInfo.surveyDescription = survey.surveyDescription;
            surveyInfo.surveyEndStatement = survey.surveyEndStatement;
            surveyInfo.surveyId = survey._id;
            surveyInfo.surveyInstanceId = instanceId;

            List<VERASurveyQuestionInfo> surveyQuestionInfos = new List<VERASurveyQuestionInfo>();

            foreach (VERASurveyQuestion question in survey.questions)
            {
                VERASurveyQuestionInfo currentQuestion = new VERASurveyQuestionInfo();
                currentQuestion.questionText = question.questionText;
                currentQuestion.orderInSurvey = question.questionNumberInSurvey;
                currentQuestion.questionId = question._id;

                switch (question.questionType)
                {
                    case "selection":
                        currentQuestion.questionType = VERASurveyQuestionInfo.VERASurveyQuestionType.Selection;
                        currentQuestion.selectionOptions = question.questionOptions.ToArray();
                        break;
                    case "multipleChoice":
                        currentQuestion.questionType = VERASurveyQuestionInfo.VERASurveyQuestionType.MultipleChoice;
                        currentQuestion.selectionOptions = question.questionOptions.ToArray();
                        break;
                    case "slider":
                        currentQuestion.questionType = VERASurveyQuestionInfo.VERASurveyQuestionType.Slider;
                        currentQuestion.leftSliderText = question.leftSliderText;
                        currentQuestion.rightSliderText = question.rightSliderText;
                        break;
                    case "matrix":
                        currentQuestion.questionType = VERASurveyQuestionInfo.VERASurveyQuestionType.Matrix;
                        currentQuestion.matrixColumnTexts = question.matrixColumnNames.ToArray();
                        currentQuestion.matrixRowTexts = question.questionOptions.ToArray();
                        break;
                }

                surveyQuestionInfos.Add(currentQuestion);
            }

            surveyInfo.surveyQuestions = surveyQuestionInfos.OrderBy(q => q.orderInSurvey).ToList();
            return surveyInfo;
        }


        private void GenerateMockSurveyResponsesLocal(string surveyId, string surveyName)
        {
            try
            {
                int pID = VERALogger.Instance.activeParticipant.participantShortId;
                string ts = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                string studyId = VERALogger.Instance.experimentUUID;
                string instanceId = System.Guid.NewGuid().ToString();

                // Generate mock questions and answers
                int numQuestions = UnityEngine.Random.Range(3, 8);

                VERACsvHandler csvHandler = VERALogger.Instance.FindCsvHandlerByFileName("Survey_Responses");
                if (csvHandler == null)
                {
                    Debug.LogError("[VERA Mock Test] No CSV handler found for Survey_Responses file type.");
                    return;
                }

                for (int i = 0; i < numQuestions; i++)
                {
                    string questionId = $"q{i + 1}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
                    string questionText = $"Mock Question {i + 1}";
                    string answer = GenerateMockAnswer(i);

                    csvHandler.CreateEntry(0, pID, ts, studyId, surveyId, surveyName, instanceId, questionId, questionText, answer);
                }

                if (verboseLogging)
                {
                    Debug.Log($"[VERA Mock Test] Generated {numQuestions} mock survey responses via Survey_Responses file type.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[VERA Mock Test] Failed to generate mock survey responses: {ex.Message}");
            }
        }

        private IEnumerator FetchSurveyDataFromAPI(string surveyId, System.Action<VERASurvey> onComplete)
        {
            string apiUrl = VERAHost.hostUrl;
            string url = $"{apiUrl}/api/surveys/{surveyId}";

            if (verboseLogging)
            {
                Debug.Log($"[VERA Mock Test] Fetching survey data from API: {url}");
            }

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                // Note: Survey fetch does not require authentication (matches SurveyInterfaceIO behavior)
                // Surveys are publicly accessible as they need to work in the VR environment

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = request.downloadHandler.text;

                    if (verboseLogging)
                    {
                        Debug.Log($"[VERA Mock Test] Survey API response (first 500 chars): {jsonResponse.Substring(0, System.Math.Min(500, jsonResponse.Length))}");
                    }

                    try
                    {
                        // Parse the survey JSON using Newtonsoft.Json (same as in workflow parsing)
                        JObject surveyObj = JObject.Parse(jsonResponse);
                        VERASurvey survey = ParseSurveyFromJSON(surveyObj);

                        if (survey != null && survey.questions != null && survey.questions.Count > 0)
                        {
                            Debug.Log($"[VERA Mock Test] ✓ Fetched survey with {survey.questions.Count} questions from API");
                            onComplete?.Invoke(survey);
                        }
                        else
                        {
                            Debug.LogError($"[VERA Mock Test] Survey fetched from API has no questions");
                            onComplete?.Invoke(null);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[VERA Mock Test] Failed to parse survey JSON from API: {ex.Message}");
                        Debug.LogError($"[VERA Mock Test] Raw response was: {jsonResponse.Substring(0, System.Math.Min(200, jsonResponse.Length))}");
                        onComplete?.Invoke(null);
                    }
                }
                else
                {
                    Debug.LogError($"[VERA Mock Test] Failed to fetch survey from API: {request.error} (HTTP {request.responseCode})");
                    onComplete?.Invoke(null);
                }
            }
        }

        private VERASurvey ParseSurveyFromJSON(JObject surveyObj)
        {
            if (surveyObj == null)
                return null;

            try
            {
                var survey = new VERASurvey
                {
                    _id = surveyObj["_id"]?.ToString(),
                    surveyName = surveyObj["surveyName"]?.ToString(),
                    shortSurveyName = surveyObj["shortSurveyName"]?.ToString(),
                    surveyDescription = surveyObj["surveyDescription"]?.ToString(),
                    surveyEndStatement = surveyObj["surveyEndStatement"]?.ToString(),
                    createdBy = surveyObj["createdBy"]?.ToString(),
                    experimentId = surveyObj["experimentId"]?.ToString(),
                    isTemplate = surveyObj["isTemplate"]?.Value<bool>() ?? false
                };

                // Parse questions
                var questionsToken = surveyObj["questions"];
                if (questionsToken != null && questionsToken.Type == JTokenType.Array)
                {
                    survey.questions = new List<VERASurveyQuestion>();
                    foreach (var qToken in questionsToken)
                    {
                        if (qToken.Type == JTokenType.Object)
                        {
                            var question = new VERASurveyQuestion
                            {
                                _id = qToken["_id"]?.ToString(),
                                surveyParent = qToken["surveyParent"]?.ToString(),
                                questionNumberInSurvey = qToken["questionNumberInSurvey"]?.Value<int>() ?? 0,
                                questionText = qToken["questionText"]?.ToString(),
                                questionType = qToken["questionType"]?.ToString(),
                                leftSliderText = qToken["leftSliderText"]?.ToString(),
                                rightSliderText = qToken["rightSliderText"]?.ToString()
                            };

                            // Parse question options
                            var optionsToken = qToken["questionOptions"];
                            if (optionsToken != null && optionsToken.Type == JTokenType.Array)
                            {
                                question.questionOptions = new List<string>();
                                foreach (var opt in optionsToken)
                                    question.questionOptions.Add(opt.ToString());
                            }

                            // Parse matrix column names
                            var matrixToken = qToken["matrixColumnNames"];
                            if (matrixToken != null && matrixToken.Type == JTokenType.Array)
                            {
                                question.matrixColumnNames = new List<string>();
                                foreach (var col in matrixToken)
                                    question.matrixColumnNames.Add(col.ToString());
                            }

                            survey.questions.Add(question);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[VERA Mock Test] Survey has no questions array. Questions token type: {questionsToken?.Type.ToString() ?? "null"}");
                }

                return survey;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[VERA Mock Test] Error in ParseSurveyFromJSON: {ex.Message}");
                return null;
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

        private void OnWorkflowCompleted()
        {
            Debug.Log("[VERA Mock Test] ✓ Automated workflow test completed successfully!");
            Debug.Log($"[VERA Mock Test] All trials and surveys were processed.");

            // Finalize session to upload all files and mark participant as COMPLETE
            Debug.Log("[VERA Mock Test] Finalizing session and uploading files...");
            VERASessionManager.FinalizeSession();
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
