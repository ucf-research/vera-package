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


        public bool uploadSuccessful { get; private set; } = false;

        #endregion


        #region OUTPUT

        // Outputs given results of given survey back to the database
        public IEnumerator OutputSurveyResults(VERASurveyInfo surveyToOutput, KeyValuePair<string, string>[] surveyResults)
        {
            uploadSuccessful = false;

            // Create the SurveyInstance based on input
            SurveyInstance surveyInstance = new SurveyInstance();
            surveyInstance.studyId = VERALogger.Instance.experimentUUID;
            surveyInstance.experimentId = VERALogger.Instance.experimentUUID;
            surveyInstance.survey = surveyToOutput.surveyId;
            surveyInstance.participantId = VERALogger.Instance.activeParticipant.participantUUID;

            // Convert to JSON
            string instanceJson = JsonUtility.ToJson(surveyInstance);

            // Create the request
            UnityWebRequest instanceRequest = new UnityWebRequest(VERAHost.hostUrl + "/api/surveys/instances", "POST");
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
                    UnityWebRequest responseRequest = new UnityWebRequest(VERAHost.hostUrl + "/api/surveys/responses", "POST");
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
                        VERADebugger.LogError("Error creating SurveyResponse: " + responseRequest.error, "SurveyInterfaceIO");
                        yield break;
                    }
                }
            }
            else
            {
                VERADebugger.LogError("Error creating SurveyInstance: " + instanceRequest.error, "SurveyInterfaceIO");
                yield break;
            }

            VERADebugger.Log("VERA Survey results successfully uploaded.", "SurveyInterfaceIO", DebugPreference.Informative);
            uploadSuccessful = true;
            yield break;
        }

        #endregion

    }

    [System.Serializable]
    internal class SurveyInstance
    {
        public string studyId;
        public string experimentId;
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