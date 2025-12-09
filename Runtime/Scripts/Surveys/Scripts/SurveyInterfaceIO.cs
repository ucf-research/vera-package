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
        private string apiUrl = "https://sreal.ucf.edu/vera-portal";
        private string surveyId;

        private bool testingLocally = false;

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
            surveyManager.Setup();

            if (testingLocally)
            {
                apiUrl = "http://localhost:4000/vera-portal";
            }
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

        // Outputs given results of given survey back to the database
        public IEnumerator OutputSurveyResults(SurveyInfo surveyToOutput, KeyValuePair<string, string>[] surveyResults)
        {
            uploadSuccessful = false;

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
                string instanceId = jsonResponse["_id"]?.ToString();

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
            OnSurveyCompleted?.Invoke();
        }

        #endregion

    }

    // Survey web result JSON parsing classes
    [System.Serializable]
    internal class Survey
    {
        public string _id;
        public string surveyName;
        public string surveyDescription;
        public string surveyEndStatement;
        public List<SurveyQuestion> questions;
        public string createdAt;
        public int __v;
    }

    [System.Serializable]
    internal class SurveyQuestion
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
    internal class SurveyResponse
    {
        public string question;
        public string surveyInstance;
        public string answer;
    }
}