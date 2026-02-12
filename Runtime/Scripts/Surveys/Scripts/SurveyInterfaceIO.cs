using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        private string apiUrl;
        private string surveyId;

        public bool uploadSuccessful { get; private set; } = false;
        public bool reconnectSuccessful { get; private set; } = false;

        private SurveyInfo surveyInfo;

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

        // Marks the local survey response file as uploaded
        private void MarkLocalSurveyAsUploaded(string instanceId, string participantId)
        {
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "SurveyResponses");
                if (!Directory.Exists(directory))
                {
                    return;
                }

                // Find the survey response file for this participant and instance
                string filename = $"survey_responses_{participantId}_{instanceId}.csv";
                string filepath = Path.Combine(directory, filename);

                if (!File.Exists(filepath))
                {
                    Debug.LogWarning($"[VERA Survey] Local survey file not found: {filename}");
                    return;
                }

                // Read all lines from the CSV
                string[] lines = File.ReadAllLines(filepath);

                if (lines.Length < 2) // Need at least header + 1 data row
                {
                    Debug.LogWarning($"[VERA Survey] CSV file is empty or invalid: {filename}");
                    return;
                }

                // Update the uploaded column (last column) to true for all rows
                for (int i = 1; i < lines.Length; i++) // Start at 1 to skip header
                {
                    if (!string.IsNullOrEmpty(lines[i]))
                    {
                        // Replace the last value (uploaded) with true
                        int lastCommaIndex = lines[i].LastIndexOf(',');
                        if (lastCommaIndex >= 0)
                        {
                            lines[i] = lines[i].Substring(0, lastCommaIndex + 1) + "true";
                        }
                    }
                }

                // Write back to file
                File.WriteAllLines(filepath, lines);

                Debug.Log($"[VERA Survey] Marked local survey file as uploaded: {filename}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[VERA Survey] Failed to mark local survey as uploaded: {ex.Message}");
            }
        }

        // Saves survey responses to a local CSV file for later upload
        private void SaveSurveyResponsesLocally(SurveyInfo surveyToOutput, KeyValuePair<string, string>[] surveyResults, string instanceId)
        {
            try
            {
                string timestamp = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                string studyId = VERALogger.Instance.experimentUUID;
                string surveyId = surveyToOutput.surveyId;
                string surveyName = surveyToOutput.surveyName;
                string participantId = VERALogger.Instance.activeParticipant.participantUUID;

                // Create a lookup dictionary for question text by question ID
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

                // Create filename with participant ID and instance ID
                string filename = $"survey_responses_{participantId}_{instanceId}.csv";
                string filepath = Path.Combine(Application.persistentDataPath, "SurveyResponses", filename);

                // Ensure the directory exists
                string directory = Path.GetDirectoryName(filepath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Create CSV content
                using (StreamWriter writer = new StreamWriter(filepath))
                {
                    // Write header
                    writer.WriteLine("timestamp,studyId,surveyId,surveyName,participantId,instanceId,questionId,questionText,answer,uploaded");

                    // Write each response as a row
                    foreach (var response in surveyResults)
                    {
                        string questionId = EscapeCsvField(response.Key);

                        // Get question text from lookup, or use empty string if not found
                        string questionText = "";
                        if (questionTextLookup.TryGetValue(response.Key, out string text))
                        {
                            questionText = EscapeCsvField(text);
                        }

                        string answer = EscapeCsvField(response.Value);
                        string surveyNameEscaped = EscapeCsvField(surveyName);

                        writer.WriteLine($"{timestamp},{studyId},{surveyId},{surveyNameEscaped},{participantId},{instanceId},{questionId},{questionText},{answer},false");
                    }
                }

                Debug.Log($"[VERA Survey] Saved survey responses locally to: {filepath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[VERA Survey] Failed to save survey responses locally: {ex.Message}");
            }
        }

        // Escapes CSV fields that contain commas, quotes, or newlines
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

        // Outputs given results of given survey back to the database
        public IEnumerator OutputSurveyResults(SurveyInfo surveyToOutput, KeyValuePair<string, string>[] surveyResults)
        {
            uploadSuccessful = false;
            string instanceId = null;

            // Create the SurveyInstance based on input
            SurveyInstance surveyInstance = new SurveyInstance();
            surveyInstance.studyId = VERALogger.Instance.experimentUUID;
            surveyInstance.survey = surveyToOutput.surveyId;
            surveyInstance.participantId = VERALogger.Instance.activeParticipant.participantUUID;

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
                instanceId = jsonResponse["_id"]?.ToString();

                // Save responses locally with the instance ID
                SaveSurveyResponsesLocally(surveyToOutput, surveyResults, instanceId);

                // Create SurveyResponses
                for (int i = 0; i < surveyResults.Length; i++)
                {
                    // Create the SurveyResponse based on input
                    SurveyResponse surveyResponse = new SurveyResponse
                    {
                        question = surveyResults[i].Key,
                        surveyInstance = instanceId,
                        answer = surveyResults[i].Value
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
                        Debug.LogError($"[VERA Survey] Error creating SurveyResponse: {responseRequest.error}");
                        Debug.LogError($"[VERA Survey] HTTP Status Code: {responseRequest.responseCode}");
                        Debug.LogError($"[VERA Survey] Question ID: {surveyResults[i].Key}");
                        Debug.LogError($"[VERA Survey] URL: {apiUrl}/api/surveys/responses");
                        if (!string.IsNullOrEmpty(responseRequest.downloadHandler?.text))
                        {
                            Debug.LogError($"[VERA Survey] Response Body: {responseRequest.downloadHandler.text}");
                        }
                        yield break;
                    }
                }
            }
            else
            {
                Debug.LogError($"[VERA Survey] Error creating SurveyInstance: {instanceRequest.error}");
                Debug.LogError($"[VERA Survey] HTTP Status Code: {instanceRequest.responseCode}");
                Debug.LogError($"[VERA Survey] URL: {apiUrl}/api/surveys/instances");
                if (!string.IsNullOrEmpty(instanceRequest.downloadHandler?.text))
                {
                    Debug.LogError($"[VERA Survey] Response Body: {instanceRequest.downloadHandler.text}");
                }
                yield break;
            }

            Debug.Log("VERA Survey results successfully uploaded.");
            uploadSuccessful = true;

            // Mark the local file as uploaded
            MarkLocalSurveyAsUploaded(instanceId, VERALogger.Instance.activeParticipant.participantUUID);

            yield break;
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
    }

    [System.Serializable]
    internal class SurveyResponse
    {
        public string question;
        public string surveyInstance;
        public string answer;
    }
}