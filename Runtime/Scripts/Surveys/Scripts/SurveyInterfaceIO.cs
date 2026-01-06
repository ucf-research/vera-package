using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace VERA
{
    [RequireComponent(typeof(SurveyManager))]
    internal class SurveyInterfaceIO : MonoBehaviour
    {

        // SurveyInterfaceIO handles the input and output of the survey interface.
        //     Handles connection, downloading, starting, and uploading survey.

        #region VARIABLES

        private SurveyManager surveyManager;
        private string apiUrl { get { return VERAHost.hostUrl; } }
        private string surveyId;

        public bool uploadSuccessful { get; private set; } = false;
        public bool reconnectSuccessful { get; private set; } = false;

        private SurveyInfo surveyInfo;

        [SerializeField] private UnityEvent OnSurveyCompleted;

        // Survey queue system
        private class SurveyQueueItem
        {
            public string surveyId;
            public System.Action onCompleted;
        }

        private Queue<SurveyQueueItem> surveyQueue = new Queue<SurveyQueueItem>();
        private bool isSurveyActive = false;
        private System.Action currentSurveyCallback;
        private const int MAX_QUEUE_SIZE = 10;

        #endregion


        #region MONOBEHAVIOUR

        // Start
        private void Awake()
        {
            surveyManager = GetComponent<SurveyManager>();
            surveyManager.Setup();
        }

        #endregion


        #region START SURVEY

        // Starts a survey by a given ID, as found in the database
        public void StartSurveyById(string surveyId)
        {
            StartSurveyById(surveyId, null);
        }

        // Starts a survey by ID with optional completion callback
        public void StartSurveyById(string surveyId, System.Action onCompleted)
        {
            if (!ValidateVERAContext())
            {
                Debug.LogWarning("[Survey] VERA context validation failed. Survey request ignored.");
                return;
            }

            if (isSurveyActive)
            {
                QueueSurvey(surveyId, onCompleted);
                return;
            }

            isSurveyActive = true;
            this.currentSurveyCallback = onCompleted;
            this.surveyId = surveyId;
            StartCoroutine(StartSurveyByIdCoroutine());
        }

        // Coroutine for above
        private IEnumerator StartSurveyByIdCoroutine()
        {
            using (UnityWebRequest request = UnityWebRequest.Get(apiUrl + "/api/surveys/" + surveyId))
            {
                // Send the request and wait for a response
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    reconnectSuccessful = false;
                    isSurveyActive = false;
                    Debug.LogError("Error fetching survey: " + request.error);
                    surveyManager.DisplayConnectionIssue();
                    yield break;
                }
                else
                {
                    // Get JSON and parse into Survey
                    string jsonResponse = request.downloadHandler.text;

                    Survey survey;
                    try
                    {
                        survey = JsonUtility.FromJson<Survey>(jsonResponse);
                    }
                    catch
                    {
                        reconnectSuccessful = false;
                        isSurveyActive = false;
                        Debug.LogError("Error parsing survey return (may be using the wrong API calls, or wrong host); survey return: " + jsonResponse);
                        surveyManager.DisplayConnectionIssue();
                        yield break;
                    }

                    // Survey.questions is now an array of question IDs, need to fetch the actual questions
                    List<SurveyQuestion> questionObjects = new List<SurveyQuestion>();

                    if (survey.questions != null && survey.questions.Count > 0)
                    {
                        // Fetch questions from the questions endpoint
                        yield return FetchQuestionsForSurvey(survey._id, questionObjects);

                        if (questionObjects.Count == 0)
                        {
                            reconnectSuccessful = false;
                            isSurveyActive = false;
                            Debug.LogError("Error: Failed to fetch questions for survey.");
                            surveyManager.DisplayConnectionIssue();
                            yield break;
                        }
                    }
                    else
                    {
                        reconnectSuccessful = false;
                        isSurveyActive = false;
                        Debug.LogError("Error: Survey has no questions.");
                        surveyManager.DisplayConnectionIssue();
                        yield break;
                    }

                    // SurveyInfo is used for in-Unity survey interface setup
                    surveyInfo = ScriptableObject.CreateInstance<SurveyInfo>();

                    // Convert general info into SurveyInfo format
                    surveyInfo.surveyName = survey.surveyName;
                    surveyInfo.surveyDescription = survey.surveyDescription;
                    surveyInfo.surveyEndStatement = survey.surveyEndStatement;
                    surveyInfo.surveyId = survey._id;

                    List<SurveyQuestionInfo> surveyQuestionInfos = new List<SurveyQuestionInfo>();

                    // Convert questions into SurveyInfo format
                    foreach (SurveyQuestion question in questionObjects)
                    {
                        SurveyQuestionInfo currentQuestion = new SurveyQuestionInfo();
                        currentQuestion.questionText = question.questionText;
                        currentQuestion.orderInSurvey = question.questionNumberInSurvey;
                        currentQuestion.questionId = question._id;

                        // Set up question based on its type
                        switch (question.questionType)
                        {
                            case "selection":
                                currentQuestion.questionType = SurveyQuestionInfo.SurveyQuestionType.Selection;
                                currentQuestion.selectionOptions = question.questionOptions.ToArray();
                                break;
                            case "multipleChoice":
                                currentQuestion.questionType = SurveyQuestionInfo.SurveyQuestionType.MultipleChoice;
                                currentQuestion.selectionOptions = question.questionOptions.ToArray();
                                break;
                            case "likert":
                                // Likert scales are typically handled as selection with options
                                currentQuestion.questionType = SurveyQuestionInfo.SurveyQuestionType.Selection;
                                currentQuestion.selectionOptions = question.questionOptions.ToArray();
                                break;
                            case "slider":
                                currentQuestion.questionType = SurveyQuestionInfo.SurveyQuestionType.Slider;
                                currentQuestion.leftSliderText = question.leftSliderText;
                                currentQuestion.rightSliderText = question.rightSliderText;
                                break;
                            case "matrix":
                                currentQuestion.questionType = SurveyQuestionInfo.SurveyQuestionType.Matrix;
                                currentQuestion.matrixColumnTexts = question.matrixColumnNames.ToArray();
                                currentQuestion.matrixRowTexts = question.questionOptions.ToArray();
                                break;
                            default:
                                Debug.LogError("Unsupported survey question type: " + question.questionType);
                                break;
                        }

                        surveyQuestionInfos.Add(currentQuestion);
                    }

                    // Sort questions based on their order in the survey
                    surveyQuestionInfos = surveyQuestionInfos.OrderBy(q => q.orderInSurvey).ToList();

                    surveyInfo.surveyQuestions = surveyQuestionInfos;

                    // Begin the survey
                    surveyManager.BeginSurvey(surveyInfo);
                }
            }
        }

        // Starts survey again from reconnection success
        public void StartSurveyFromReconnect()
        {
            surveyManager.BeginSurvey(surveyInfo);
        }

        // Tries to reconnect, setting survey info on success
        public IEnumerator TryReconnect()
        {
            reconnectSuccessful = false;

            using (UnityWebRequest request = UnityWebRequest.Get(apiUrl + "/api/surveys/" + surveyId))
            {
                // Send the request and wait for a response
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError("Error fetching survey: " + request.error);
                    yield break;
                }
                else
                {
                    // Get JSON and parse into Survey
                    string jsonResponse = request.downloadHandler.text;

                    Survey survey;
                    try
                    {
                        survey = JsonUtility.FromJson<Survey>(jsonResponse);
                    }
                    catch
                    {
                        Debug.LogError("Error parsing survey return (may be using the wrong API calls, or wrong host); survey return: " + jsonResponse);
                        yield break;
                    }

                    // Survey.questions is now an array of question IDs, need to fetch the actual questions
                    List<SurveyQuestion> questionObjects = new List<SurveyQuestion>();

                    if (survey.questions != null && survey.questions.Count > 0)
                    {
                        // Fetch questions from the questions endpoint
                        yield return FetchQuestionsForSurvey(survey._id, questionObjects);

                        if (questionObjects.Count == 0)
                        {
                            Debug.LogError("Error: Failed to fetch questions for survey during reconnect.");
                            yield break;
                        }
                    }
                    else
                    {
                        Debug.LogError("Error: Survey has no questions during reconnect.");
                        yield break;
                    }

                    // SurveyInfo is used for in-Unity survey interface setup
                    surveyInfo = ScriptableObject.CreateInstance<SurveyInfo>();

                    // Convert general info into SurveyInfo format
                    surveyInfo.surveyName = survey.surveyName;
                    surveyInfo.surveyDescription = survey.surveyDescription;
                    surveyInfo.surveyEndStatement = survey.surveyEndStatement;
                    surveyInfo.surveyId = survey._id;

                    List<SurveyQuestionInfo> surveyQuestionInfos = new List<SurveyQuestionInfo>();

                    // Convert questions into SurveyInfo format
                    foreach (SurveyQuestion question in questionObjects)
                    {
                        SurveyQuestionInfo currentQuestion = new SurveyQuestionInfo();
                        currentQuestion.questionText = question.questionText;
                        currentQuestion.orderInSurvey = question.questionNumberInSurvey;
                        currentQuestion.questionId = question._id;

                        // Set up question based on its type
                        switch (question.questionType)
                        {
                            case "selection":
                                currentQuestion.questionType = SurveyQuestionInfo.SurveyQuestionType.Selection;
                                currentQuestion.selectionOptions = question.questionOptions.ToArray();
                                break;
                            case "multipleChoice":
                                currentQuestion.questionType = SurveyQuestionInfo.SurveyQuestionType.MultipleChoice;
                                currentQuestion.selectionOptions = question.questionOptions.ToArray();
                                break;
                            case "likert":
                                currentQuestion.questionType = SurveyQuestionInfo.SurveyQuestionType.Selection;
                                currentQuestion.selectionOptions = question.questionOptions.ToArray();
                                break;
                            case "slider":
                                currentQuestion.questionType = SurveyQuestionInfo.SurveyQuestionType.Slider;
                                currentQuestion.leftSliderText = question.leftSliderText;
                                currentQuestion.rightSliderText = question.rightSliderText;
                                break;
                            case "matrix":
                                currentQuestion.questionType = SurveyQuestionInfo.SurveyQuestionType.Matrix;
                                currentQuestion.matrixColumnTexts = question.matrixColumnNames.ToArray();
                                currentQuestion.matrixRowTexts = question.questionOptions.ToArray();
                                break;
                            default:
                                Debug.LogError("Unsupported survey question type: " + question.questionType);
                                break;
                        }

                        surveyQuestionInfos.Add(currentQuestion);
                    }

                    // Sort questions based on their order in the survey
                    surveyQuestionInfos = surveyQuestionInfos.OrderBy(q => q.orderInSurvey).ToList();

                    surveyInfo.surveyQuestions = surveyQuestionInfos;
                    reconnectSuccessful = true;
                    yield break;
                }
            }
        }



        #endregion


        #region QUEUE MANAGEMENT

        // Queue a survey to be shown after the current one completes
        public void QueueSurvey(string surveyId, System.Action onCompleted = null)
        {
            if (surveyQueue.Count >= MAX_QUEUE_SIZE)
            {
                Debug.LogWarning($"[Survey] Queue is full ({MAX_QUEUE_SIZE} surveys). Cannot queue survey {surveyId}.");
                return;
            }

            surveyQueue.Enqueue(new SurveyQueueItem
            {
                surveyId = surveyId,
                onCompleted = onCompleted
            });

            Debug.Log($"[Survey] Queued survey {surveyId}. Queue size: {surveyQueue.Count}");
        }

        // Check if a survey is currently active
        public bool IsSurveyActive
        {
            get { return isSurveyActive; }
        }

        // Get the number of queued surveys
        public int QueuedSurveysCount
        {
            get { return surveyQueue.Count; }
        }

        // Clear all queued surveys
        public void ClearQueue()
        {
            surveyQueue.Clear();
            Debug.Log("[Survey] Survey queue cleared.");
        }

        // Process the next survey in the queue
        private void ProcessNextSurvey()
        {
            if (surveyQueue.Count > 0)
            {
                SurveyQueueItem next = surveyQueue.Dequeue();
                Debug.Log($"[Survey] Processing next survey from queue: {next.surveyId}");
                StartSurveyById(next.surveyId, next.onCompleted);
            }
        }

        #endregion


        #region QUESTIONS ENDPOINT

        // Fetch questions separately for a survey
        private IEnumerator FetchQuestionsForSurvey(string surveyId, List<SurveyQuestion> outputList)
        {
            Debug.Log($"[Survey] Fetching questions for survey {surveyId} from questions endpoint.");

            using (UnityWebRequest request = UnityWebRequest.Get(apiUrl + "/api/surveys/" + surveyId + "/questions"))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"[Survey] Error fetching questions: {request.error}");
                    yield break;
                }
                else
                {
                    string jsonResponse = request.downloadHandler.text;

                    try
                    {
                        // The API returns an array of questions directly
                        SurveyQuestionsResponse questionsResponse = JsonUtility.FromJson<SurveyQuestionsResponse>("{\"questions\":" + jsonResponse + "}");

                        if (questionsResponse != null && questionsResponse.questions != null && questionsResponse.questions.Count > 0)
                        {
                            outputList.AddRange(questionsResponse.questions);
                            Debug.Log($"[Survey] Successfully fetched {questionsResponse.questions.Count} questions.");
                        }
                        else
                        {
                            Debug.LogError("[Survey] Questions response was null or had no questions.");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[Survey] Error parsing questions response: {e.Message}. Response: {jsonResponse}");
                    }
                }
            }
        }

        #endregion


        #region VALIDATION

        // Validate that VERA context is properly initialized
        private bool ValidateVERAContext()
        {
            if (VERALogger.Instance == null)
            {
                Debug.LogError("[Survey] VERALogger instance not found. Surveys require VERA logging to be initialized.");
                return false;
            }

            if (!VERALogger.Instance.initialized)
            {
                Debug.LogWarning("[Survey] VERALogger not yet initialized. Survey may fail.");
                // Don't return false - let it try anyway for now
            }

            return true;
        }

        #endregion


        #region OUTPUT

        // Outputs given results of given survey back to the database
        public IEnumerator OutputSurveyResults(SurveyInfo surveyToOutput, KeyValuePair<string, string>[] surveyResults)
        {
            uploadSuccessful = false;

            // Create the SurveyInstance based on input
            SurveyInstance surveyInstance = new SurveyInstance();
            surveyInstance.studyId = VERALogger.Instance.experimentUUID;
            surveyInstance.experimentId = VERALogger.Instance.experimentUUID; // Same as studyId for now
            surveyInstance.survey = surveyToOutput.surveyId;
            surveyInstance.participantId = VERALogger.Instance.activeParticipant.participantUUID;
            surveyInstance.activated = true;

            // Convert to JSON
            string instanceJson = JsonUtility.ToJson(surveyInstance);

            // Create the request
            UnityWebRequest instanceRequest = new UnityWebRequest(apiUrl + "/api/surveys/instances", "POST");
            byte[] instanceBodyRaw = System.Text.Encoding.UTF8.GetBytes(instanceJson);
            instanceRequest.uploadHandler = new UploadHandlerRaw(instanceBodyRaw);
            instanceRequest.downloadHandler = new DownloadHandlerBuffer();
            instanceRequest.SetRequestHeader("Content-Type", "application/json");

            // Send the request and wait for a response
            yield return instanceRequest.SendWebRequest();

            if (instanceRequest.result == UnityWebRequest.Result.Success)
            {
                // Parse the response to get the ID of the created SurveyInstance
                string responseText = instanceRequest.downloadHandler.text;
                JObject jsonResponse = JObject.Parse(responseText);
                string instanceId = jsonResponse["_id"]?.ToString();

                // Create SurveyResponses
                for (int i = 0; i < surveyResults.Length; i++)
                {
                    // Create the SurveyResponse based on input
                    SurveyResponse surveyResponse = new SurveyResponse
                    {
                        question = surveyResults[i].Key,
                        surveyInstance = instanceId,
                        answer = surveyResults[i].Value,
                        participantId = VERALogger.Instance.activeParticipant.participantUUID
                    };

                    // Convert to JSON
                    string responseJson = JsonUtility.ToJson(surveyResponse);

                    // Create the request
                    UnityWebRequest responseRequest = new UnityWebRequest(apiUrl + "/api/surveys/responses", "POST");
                    byte[] responseBodyRaw = System.Text.Encoding.UTF8.GetBytes(responseJson);
                    responseRequest.uploadHandler = new UploadHandlerRaw(responseBodyRaw);
                    responseRequest.downloadHandler = new DownloadHandlerBuffer();
                    responseRequest.SetRequestHeader("Content-Type", "application/json");

                    // Send the request and wait for a response
                    yield return responseRequest.SendWebRequest();

                    if (responseRequest.result == UnityWebRequest.Result.Success)
                    {
                        continue;
                    }
                    else
                    {
                        Debug.LogError("Error creating SurveyResponse: " + responseRequest.error);
                        yield break;
                    }
                }
            }
            else
            {
                Debug.LogError("Error creating SurveyInstance: " + instanceRequest.error);
                yield break;
            }

            Debug.Log("VERA Survey results successfully uploaded.");
            uploadSuccessful = true;
            yield break;
        }

        // Invokes completion of the survey
        public void InvokeCompletion()
        {
            // Invoke the callback for the completed survey
            currentSurveyCallback?.Invoke();
            OnSurveyCompleted?.Invoke();

            // Mark survey as no longer active
            isSurveyActive = false;
            currentSurveyCallback = null;

            // Process next survey in queue if any
            ProcessNextSurvey();
        }

        #endregion

    }

    // Survey web result JSON parsing classes
    [System.Serializable]
    internal class Survey
    {
        public string _id;
        public string surveyName;
        public string shortSurveyName;
        public string surveyDescription;
        public string surveyEndStatement;
        public List<string> tags;
        public List<string> citations;
        public List<string> questions; // Array of question IDs (not embedded objects)
        public string createdBy;
        public bool isTemplate;
        public string createdAt;
        public string updatedAt;
    }

    [System.Serializable]
    internal class SurveyQuestion
    {
        public string _id;
        public string surveyParent;
        public int questionNumberInSurvey;
        public string questionText;
        public string questionType; // "likert", "multipleChoice", "selection", "slider", "matrix", etc.
        public List<string> questionOptions;
        public string leftSliderText;
        public string rightSliderText;
        public List<string> matrixColumnNames;
        public int? timeCheckSeconds; // Nullable int
        public string createdAt;
        public string updatedAt;
    }

    [System.Serializable]
    internal class SurveyInstance
    {
        public string studyId;
        public string experimentId;
        public string survey;
        public string participantId;
        public bool activated;
    }

    [System.Serializable]
    internal class SurveyResponse
    {
        public string question;
        public string surveyInstance;
        public string answer;
        public string participantId;
    }

    [System.Serializable]
    internal class SurveyQuestionsResponse
    {
        public List<SurveyQuestion> questions;
    }
}