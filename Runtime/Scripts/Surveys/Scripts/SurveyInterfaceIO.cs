using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        private string apiUrl;
        private string surveyId;

        public bool uploadSuccessful { get; private set; } = false;
        public bool reconnectSuccessful { get; private set; } = false;
        private bool fileUploadSuccessful = false;

        private SurveyInfo surveyInfo;

        // Track survey instance counts for file naming
        private static Dictionary<string, int> surveyInstanceCounts = new Dictionary<string, int>();

        [SerializeField] private UnityEvent OnSurveyCompleted;

        #endregion


        #region MONOBEHAVIOUR

        // Start
        private void Awake()
        {
            surveyManager = GetComponent<SurveyManager>();

            // Only call Setup() if SurveyManager exists and is properly configured
            // This allows SurveyInterfaceIO to work standalone for API-only operations (e.g., mock tests)
            if (surveyManager != null)
            {
                try
                {
                    surveyManager.Setup();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[VERA Survey] SurveyManager.Setup() failed (UI components may be missing). SurveyInterfaceIO will operate in API-only mode. Error: {e.Message}");
                }
            }

            // Use the global VERA host URL
            apiUrl = VERAHost.hostUrl;
        }

        #endregion


        #region START SURVEY

        // Starts a survey by a given ID, as found in the database
        public void StartSurveyById(string surveyId)
        {
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
                        Debug.LogError("Error parsing survey return (may be using the wrong API calls, or wrong host); survey return: " + jsonResponse);
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
                    foreach (SurveyQuestion question in survey.questions)
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

                    // SurveyInfo is used for in-Unity survey interface setup
                    surveyInfo = ScriptableObject.CreateInstance<SurveyInfo>();

                    // Convert general info into SurveyInfo format
                    surveyInfo.surveyName = survey.surveyName;
                    surveyInfo.surveyDescription = survey.surveyDescription;
                    surveyInfo.surveyEndStatement = survey.surveyEndStatement;
                    surveyInfo.surveyId = survey._id;

                    List<SurveyQuestionInfo> surveyQuestionInfos = new List<SurveyQuestionInfo>();

                    // Convert questions into SurveyInfo format
                    foreach (SurveyQuestion question in survey.questions)
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


        #region OUTPUT

        // Creates and uploads a CSV file for this specific survey instance
        private IEnumerator UploadSurveyInstanceFile(SurveyInfo surveyToOutput, KeyValuePair<string, string>[] surveyResults, string instanceId)
        {
            int pID = VERALogger.Instance.activeParticipant.participantShortId;
            string ts = Time.realtimeSinceStartup.ToString();
            string studyId = VERALogger.Instance.experimentUUID;
            string surveyId = surveyToOutput.surveyId;
            string surveyName = surveyToOutput.surveyName;

            // Build question text lookup
            Dictionary<string, string> questionTextLookup = new Dictionary<string, string>();
            if (surveyToOutput.surveyQuestions != null)
            {
                foreach (var question in surveyToOutput.surveyQuestions)
                {
                    if (!string.IsNullOrEmpty(question.questionId))
                    {
                        questionTextLookup[question.questionId] = question.questionText ?? "";
                    }
                }
            }

            // Get the file type ID from the Survey_Responses handler
            VERACsvHandler surveyHandler = VERALogger.Instance.FindCsvHandlerByFileName("Survey_Responses");
            if (surveyHandler == null)
            {
                Debug.LogError("[VERA Survey] No CSV handler found for Survey_Responses file type. Cannot upload survey instance file.");
                fileUploadSuccessful = false;
                yield break;
            }

            string fileTypeId = surveyHandler.columnDefinition.fileType.fileTypeId;
            if (string.IsNullOrEmpty(fileTypeId) || fileTypeId == "survey-responses")
            {
                Debug.LogWarning("[VERA Survey] Survey_Responses file type ID not available. Skipping per-instance upload.");
                // Fall back to recording to the shared CSV (will be uploaded at finalization)
                RecordToSharedCsv(surveyHandler, surveyResults, pID, ts, studyId, surveyId, surveyName, instanceId, questionTextLookup);
                fileUploadSuccessful = false;
                yield break;
            }

            // Build CSV content
            StringBuilder csvContent = new StringBuilder();

            // Header row
            csvContent.AppendLine("pID,ts,studyId,surveyId,surveyName,instanceId,questionId,questionText,answer");

            // Data rows
            foreach (var response in surveyResults)
            {
                string questionText = "";
                if (questionTextLookup.TryGetValue(response.Key, out string text))
                {
                    questionText = text;
                }

                // Escape CSV values
                string escapedQuestionText = EscapeCsvValue(questionText);
                string escapedAnswer = EscapeCsvValue(response.Value);
                string escapedSurveyName = EscapeCsvValue(surveyName);

                csvContent.AppendLine($"{pID},{ts},{studyId},{surveyId},{escapedSurveyName},{instanceId},{response.Key},{escapedQuestionText},{escapedAnswer}");
            }

            // Create temp file
            string participantUUID = VERALogger.Instance.activeParticipant.participantUUID;
            string sanitizedSurveyName = System.Text.RegularExpressions.Regex.Replace(surveyName ?? "Survey", @"[<>:""/\\|?*\s]", "_");

            // Track instance count per survey name (e.g., VRSQ.csv, VRSQ_2.csv, VRSQ_3.csv)
            // Only increment on success to avoid duplicate filenames on retry
            if (!surveyInstanceCounts.ContainsKey(sanitizedSurveyName))
            {
                surveyInstanceCounts[sanitizedSurveyName] = 0;
            }
            // Use pending count (current + 1) for this attempt
            int instanceCount = surveyInstanceCounts[sanitizedSurveyName] + 1;

            // First instance has no suffix, subsequent instances have _2, _3, etc.
            string fileName = instanceCount == 1
                ? $"{sanitizedSurveyName}.csv"
                : $"{sanitizedSurveyName}_{instanceCount}.csv";
            string tempFilePath = Path.Combine(Application.temporaryCachePath, fileName);

            try
            {
                File.WriteAllText(tempFilePath, csvContent.ToString());

                // Also save a backup copy to the VERA data directory for testing/debugging
                string dataPath = Path.Combine(Application.dataPath, "VERA", "data");
                if (!Directory.Exists(dataPath))
                {
                    Directory.CreateDirectory(dataPath);
                }
                string backupFilePath = Path.Combine(dataPath, fileName);
                File.WriteAllText(backupFilePath, csvContent.ToString());
                Debug.Log($"[VERA Survey] Backup saved: {backupFilePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[VERA Survey] Failed to create survey instance CSV file: {ex.Message}");
                // Fall back to shared CSV
                RecordToSharedCsv(surveyHandler, surveyResults, pID, ts, studyId, surveyId, surveyName, instanceId, questionTextLookup);
                fileUploadSuccessful = false;
                yield break;
            }

            // Upload the file
            string host = VERAHost.hostUrl;
            string url = $"{host}/api/participants/{participantUUID}/filetypes/{fileTypeId}/files";
            string apiKey = VERALogger.Instance.apiKey;

            Debug.Log($"[VERA Survey] Uploading survey instance file: {fileName}");

            byte[] fileData = File.ReadAllBytes(tempFilePath);

            WWWForm form = new WWWForm();
            form.AddField("participant_UUID", participantUUID);
            form.AddBinaryData("fileUpload", fileData, fileName, "text/csv");

            using (UnityWebRequest request = UnityWebRequest.Post(url, form))
            {
                request.SetRequestHeader("Authorization", "Bearer " + apiKey);
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[VERA Survey] ✓ Successfully uploaded survey instance file: {fileName}");
                    fileUploadSuccessful = true;
                    // Commit the instance count increment now that upload succeeded
                    surveyInstanceCounts[sanitizedSurveyName] = instanceCount;
                }
                else
                {
                    Debug.LogError($"[VERA Survey] Failed to upload survey instance file: {request.error}");
                    Debug.LogError($"[VERA Survey] HTTP Status: {request.responseCode}");
                    fileUploadSuccessful = false;
                    // Fall back to shared CSV as backup
                    RecordToSharedCsv(surveyHandler, surveyResults, pID, ts, studyId, surveyId, surveyName, instanceId, questionTextLookup);
                }
            }

            // Clean up temp file
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch { }
        }

        // Records responses to the shared Survey_Responses CSV (backup/fallback)
        private void RecordToSharedCsv(VERACsvHandler csvHandler, KeyValuePair<string, string>[] surveyResults,
            int pID, string ts, string studyId, string surveyId, string surveyName, string instanceId,
            Dictionary<string, string> questionTextLookup)
        {
            foreach (var response in surveyResults)
            {
                string questionText = "";
                if (questionTextLookup.TryGetValue(response.Key, out string text))
                {
                    questionText = text;
                }
                csvHandler.CreateEntry(0, pID, ts, studyId, surveyId, surveyName, instanceId, response.Key, questionText, response.Value);
            }
            Debug.Log($"[VERA Survey] Recorded {surveyResults.Length} survey responses to shared Survey_Responses file.");
        }

        // Escapes a value for CSV format
        private string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            // If the value contains comma, quote, or newline, wrap in quotes and escape internal quotes
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }

        // Legacy method - records survey responses to the shared Survey_Responses CSV file type via VERACsvHandler
        private void RecordSurveyResponses(SurveyInfo surveyToOutput, KeyValuePair<string, string>[] surveyResults, string instanceId)
        {
            int pID = VERALogger.Instance.activeParticipant.participantShortId;
            string ts = Time.realtimeSinceStartup.ToString();
            string studyId = VERALogger.Instance.experimentUUID;
            string surveyId = surveyToOutput.surveyId;
            string surveyName = surveyToOutput.surveyName;

            // Build question text lookup
            Dictionary<string, string> questionTextLookup = new Dictionary<string, string>();
            if (surveyToOutput.surveyQuestions != null)
            {
                foreach (var question in surveyToOutput.surveyQuestions)
                {
                    if (!string.IsNullOrEmpty(question.questionId))
                    {
                        questionTextLookup[question.questionId] = question.questionText ?? "";
                    }
                }
            }

            VERACsvHandler csvHandler = VERALogger.Instance.FindCsvHandlerByFileName("Survey_Responses");
            if (csvHandler == null)
            {
                Debug.LogError("[VERA Survey] No CSV handler found for Survey_Responses file type.");
                return;
            }

            foreach (var response in surveyResults)
            {
                string questionText = "";
                if (questionTextLookup.TryGetValue(response.Key, out string text))
                {
                    questionText = text;
                }

                // All 9 columns provided explicitly (skipAutoColumns file type)
                csvHandler.CreateEntry(0, pID, ts, studyId, surveyId, surveyName, instanceId, response.Key, questionText, response.Value);
            }

            Debug.Log($"[VERA Survey] Recorded {surveyResults.Length} survey responses to Survey_Responses file type.");
        }

        // Outputs given results of given survey back to the database
        // Flow: submit responses to the API, then record to the Survey_Responses CSV file type
        public IEnumerator OutputSurveyResults(SurveyInfo surveyToOutput, KeyValuePair<string, string>[] surveyResults)
        {
            uploadSuccessful = false;
            fileUploadSuccessful = false;
            string instanceId = null;
            string participantId = VERALogger.Instance.activeParticipant.participantUUID;

            // Always generate a local instance ID so we can save the CSV regardless of API success
            string localInstanceId = System.Guid.NewGuid().ToString("N");

            UnityWebRequest instanceRequest = null;

            // Check if survey instance ID is already provided (pre-existing instance)
            if (!string.IsNullOrEmpty(surveyToOutput.surveyInstanceId))
            {
                // Use existing survey instance ID
                instanceId = surveyToOutput.surveyInstanceId;
                Debug.Log($"[VERA Survey] Using existing survey instance ID: {instanceId}");
            }
            else
            {
                // Create a new SurveyInstance
                Debug.Log("[VERA Survey] No existing instance ID found. Creating new survey instance.");

                SurveyInstance surveyInstance = new SurveyInstance();
                surveyInstance.studyId = VERALogger.Instance.experimentUUID;
                surveyInstance.survey = surveyToOutput.surveyId;
                surveyInstance.participantId = participantId;

                // Convert to JSON
                string instanceJson = JsonUtility.ToJson(surveyInstance);

                // Create the request
                instanceRequest = new UnityWebRequest(apiUrl + "/api/surveys/instances", "POST");
                byte[] instanceBodyRaw = System.Text.Encoding.UTF8.GetBytes(instanceJson);
                instanceRequest.uploadHandler = new UploadHandlerRaw(instanceBodyRaw);
                instanceRequest.downloadHandler = new DownloadHandlerBuffer();
                instanceRequest.SetRequestHeader("Content-Type", "application/json");

                // Send the request and wait for a response
                yield return instanceRequest.SendWebRequest();
            }

            if (instanceRequest == null || instanceRequest.result == UnityWebRequest.Result.Success)
            {
                // Parse the response to get the ID of the created SurveyInstance (only if we created a new one)
                if (instanceRequest != null)
                {
                    string responseText = instanceRequest.downloadHandler.text;
                    JObject jsonResponse = JObject.Parse(responseText);
                    instanceId = jsonResponse["_id"]?.ToString();
                }

                // Submit individual responses to the response API
                Debug.Log($"[VERA Survey] Submitting {surveyResults.Length} responses with instanceId={instanceId}, participantId={participantId}");

                for (int i = 0; i < surveyResults.Length; i++)
                {
                    SurveyResponse surveyResponse = new SurveyResponse
                    {
                        question = surveyResults[i].Key,
                        surveyInstance = instanceId,
                        answer = surveyResults[i].Value,
                        participantId = participantId
                    };

                    string responseJson = JsonUtility.ToJson(surveyResponse);
                    Debug.Log($"[VERA Survey] Submitting response {i+1}/{surveyResults.Length}: {responseJson}");

                    UnityWebRequest responseRequest = new UnityWebRequest(apiUrl + "/api/surveys/responses", "POST");
                    byte[] responseBodyRaw = System.Text.Encoding.UTF8.GetBytes(responseJson);
                    responseRequest.uploadHandler = new UploadHandlerRaw(responseBodyRaw);
                    responseRequest.downloadHandler = new DownloadHandlerBuffer();
                    responseRequest.SetRequestHeader("Content-Type", "application/json");

                    yield return responseRequest.SendWebRequest();

                    if (responseRequest.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[VERA Survey] Error creating SurveyResponse: {responseRequest.error}");
                        Debug.LogError($"[VERA Survey] HTTP Status Code: {responseRequest.responseCode}");
                        Debug.LogError($"[VERA Survey] Question ID: {surveyResults[i].Key}");
                        Debug.LogError($"[VERA Survey] URL: {apiUrl}/api/surveys/responses");
                        if (!string.IsNullOrEmpty(responseRequest.downloadHandler?.text))
                        {
                            Debug.LogError($"[VERA Survey] Response Body: {responseRequest.downloadHandler.text}");
                        }
                        Debug.LogError($"[VERA Survey] Request Body: {responseJson}");
                        break;
                    }
                    else
                    {
                        Debug.Log($"[VERA Survey] ✓ Response {i+1} submitted successfully");
                    }
                }
            }
            else
            {
                // Only log error if we actually tried to create an instance
                if (instanceRequest != null)
                {
                    Debug.LogError($"[VERA Survey] Error creating SurveyInstance: {instanceRequest.error}");
                    Debug.LogError($"[VERA Survey] HTTP Status Code: {instanceRequest.responseCode}");
                    Debug.LogError($"[VERA Survey] URL: {apiUrl}/api/surveys/instances");
                    if (!string.IsNullOrEmpty(instanceRequest.downloadHandler?.text))
                    {
                        Debug.LogError($"[VERA Survey] Response Body: {instanceRequest.downloadHandler.text}");
                    }
                }
            }

            // Upload survey responses as a separate CSV file for this instance
            string csvInstanceId = instanceId ?? localInstanceId;
            yield return UploadSurveyInstanceFile(surveyToOutput, surveyResults, csvInstanceId);

            // Survey upload is successful only if the file was uploaded successfully
            // Instance creation is secondary - the file upload is what we require
            if (fileUploadSuccessful)
            {
                Debug.Log("[VERA Survey] Survey response file successfully uploaded.");
                uploadSuccessful = true;
            }
            else
            {
                Debug.LogWarning("[VERA Survey] Survey response file upload failed. Responses saved locally - retry required.");
                uploadSuccessful = false;
            }
        }

        // Invokes completion of the survey
        public void InvokeCompletion()
        {
            OnSurveyCompleted?.Invoke();
        }

        #endregion

    }

    // Survey web result JSON parsing classes
    [System.Serializable]
    public class Survey
    {
        public string _id;
        public string surveyName;
        public string shortSurveyName;
        public string surveyDescription;
        public string surveyEndStatement;
        public List<string> tags;
        public List<SurveyCitation> citations;
        public List<SurveyQuestion> questions;
        public string createdBy;
        public string experimentId;
        public bool isTemplate;
        public string createdAt;
        public int __v;
    }

    [System.Serializable]
    public class SurveyCitation
    {
        public string _id;
        public string title;
        public string fullCitation;
    }

    [System.Serializable]
    public class SurveyQuestion
    {
        public string _id;
        public string surveyParent;
        public int questionNumberInSurvey;
        public string questionText;
        public string questionType;
        public List<string> questionOptions;
        public List<string> matrixColumnNames;
        public string leftSliderText;
        public string rightSliderText;
        public string createdAt;
        public int __v;
    }

    [System.Serializable]
    internal class SurveyInstance
    {
        public string studyId;
        public string survey;
        public string participantId;
    }

    [System.Serializable]
    public class SurveyInstanceData
    {
        public string instanceId;
        public string experimentId;
        public bool activated;
        public bool requiresCompletion;
    }

    [System.Serializable]
    internal class SurveyResponse
    {
        public string question;
        public string surveyInstance;
        public string answer;
        public string participantId;
    }
}
