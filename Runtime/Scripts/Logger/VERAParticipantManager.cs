using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;

namespace VERA
{
    // Data structure for parsing the participant response JSON
    [System.Serializable]
    internal class ParticipantResponse
    {
        public string databaseID;
        public string unityID;
        public string pID;
        public string prolificID;
    }

    internal class VERAParticipantManager : MonoBehaviour
    {

        // VERAParticipantManager handles the creation, ID, state change, etc. of the active participant

        public string participantUUID { get; private set; }
        public int participantShortId { get; private set; }
        public string prolificID { get; private set; }

        // Participant state management
        public enum ParticipantProgressState { RECRUITED, ACCEPTED, WAITLISTED, IN_EXPERIMENT, TERMINATED, INCOMPLETE, GHOSTED, COMPLETE };
        public ParticipantProgressState currentParticipantProgressState { get; private set; }
        private int changeProgressMaxRetries = 3;


        #region PARTICIPANT CREATION


        // Creates the participant entity using the provided override ID
        public IEnumerator CreateParticipant(string overrideParticipantId)
        {
            // If a participant already exists, do not create a new one
            if (!string.IsNullOrEmpty(participantUUID))
            {
                VERADebugger.LogWarning("A participant already exists; not creating a new one.", "VERA Participant");
                yield break;
            }

            // Get the participant data from the server using the override ID, then push a new Unity ID to it, then set them as in experiment
            yield return GetParticipantFromOverrideId(overrideParticipantId);
            yield return PushUidToActiveParticipant(overrideParticipantId);
            yield return RetryableChangeProgress(ParticipantProgressState.IN_EXPERIMENT);
        }


        // Creates the participant entity from scratch (or uses active participant if it exists)
        public IEnumerator CreateParticipant()
        {
            // If a participant already exists, do not create a new one
            if (!string.IsNullOrEmpty(participantUUID))
            {
                VERADebugger.LogWarning("A participant already exists; not creating a new one.", "VERA Participant");
                yield break;
            }

            // Check data recording type
            DataRecordingType dataRecordingType = VERALogger.Instance.GetDataRecordingType();
            switch (dataRecordingType)
            {
                case DataRecordingType.DoNotRecord:
                    VERADebugger.Log("Data recording type is set to Do Not Record; not creating participant.", "VERA Participant", DebugPreference.Informative);
                    break;
                case DataRecordingType.OnlyRecordLocally:
                    // Recording locally, use generated UID and random short ID
                    participantUUID = Guid.NewGuid().ToString().Replace("-", "");
                    participantShortId = UnityEngine.Random.Range(100000, 999999);
                    VERADebugger.Log("Data recording type is set to Only Record Locally; using generated participant UUID and random short ID for local recording.", "VERA Participant", DebugPreference.Informative);
                    break;
                case DataRecordingType.RecordLocallyAndLive:
                default:
                    // Ensure a participant exists to record data to
                    yield return EnsureParticipant();

                    // If after ensuring the participant we don't have a valid short ID, attempt creation retries
                    int attempts = 0;
                    int maxAttempts = 3;
                    while ((participantShortId == 0) && attempts < maxAttempts)
                    {
                        attempts++;
                        VERADebugger.LogWarning($"participantShortId is {participantShortId} after EnsureParticipant(); attempting creation attempt {attempts}/{maxAttempts}...", "VERA Participant");
                        yield return CreateParticipantCoroutine();

                        if (participantShortId > 0)
                        {
                            VERADebugger.Log("Participant creation succeeded on retry.", "VERA Participant", DebugPreference.Informative);
                            yield break;
                        }

                        // small delay before next attempt
                        yield return new WaitForSeconds(1f);
                    }

                    if (participantShortId == 0)
                    {
                        VERADebugger.LogError("Unable to obtain a valid participant short ID after retries. Local recording will continue but uploads may not attach to the correct participant on the server.", "VERA Participant");
                    }
                    break;
            }
        }


        // Ensure the participant entity is created
        // Uses site's active participant if it exists, otherwise creates a new one
        private IEnumerator EnsureParticipant()
        {
            // Check if the site has an active participant
            string siteId = VERALogger.Instance.siteUUID;

            string host = VERAHost.hostUrl;
            string url = host + "/api/sites/" + siteId + "/active-participant";
            VERADebugger.Log("Checking for active participant at url " + url, "VERA Participant", DebugPreference.Verbose);

            UnityWebRequest request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();

            // Check success
            if (request.result == UnityWebRequest.Result.Success)
            {
                // 200, active participant exists
                if (request.responseCode == 200)
                {
                    // Parse the response to get the participant ID
                    string responseText = request.downloadHandler.text;

                    // Parse JSON response
                    ParticipantResponse response = null;
                    bool parseSuccess = false;

                    try
                    {
                        response = JsonUtility.FromJson<ParticipantResponse>(responseText);
                        parseSuccess = true;
                    }
                    catch (System.Exception)
                    {
                        parseSuccess = false;
                    }

                    if (parseSuccess && response != null)
                    {
                        string existingUid = response.unityID;
                        string databaseId = response.databaseID;
                        if (int.TryParse(response.pID, out int pID))
                        {
                            // If uid exists, set it as the participant UUID
                            if (!String.IsNullOrEmpty(existingUid))
                            {
                                participantUUID = existingUid;
                                participantShortId = pID;
                                VERADebugger.Log("Active participant found with UUID: " + participantUUID + " (pID=" + participantShortId + ")", "VERA Participant", DebugPreference.Informative);
                                request.Dispose();
                                yield break;
                            }
                            // Active participant exists, but no uid found in response, create a uid and push to database
                            else
                            {
                                VERADebugger.Log("Active participant found, but has no associated uid. Pushing a new uid...", "VERA Participant", DebugPreference.Informative);
                                request.Dispose();
                                yield return PushUidToActiveParticipant(databaseId);
                                yield break;
                            }
                        }
                        else
                        {
                            VERADebugger.LogWarning($"Active participant response had invalid pID value: '{response.pID}'. Proceeding to create a new participant.", "VERA Participant");
                        }
                    }
                    else
                    {
                        VERADebugger.LogWarning("Failed to parse active participant response or response was null; proceeding to create a new participant.", "VERA Participant");
                    }
                }
            }

            // If the request failed to reach the server, log diagnostic details
            if (request.result != UnityWebRequest.Result.Success)
            {
                if (request.responseCode != 404)
                {
                    VERADebugger.LogWarning($"Active participant lookup request failed: result={request.result}, code={request.responseCode}, error={request.error}", "VERA Participant");
                }
            }

            // Dispose request before continuing
            request.Dispose();

            // If we reach here, no active participant was found, create one
            VERADebugger.Log("No active participant found for site; creating a new participant...", "VERA Participant", DebugPreference.Informative);
            yield return CreateParticipantCoroutine();
            yield break;
        }


        // Creates the participant and uploads to the site
        // If overrideId is provided, it will use that as the participant UUID and just GET the existing participant
        private IEnumerator CreateParticipantCoroutine()
        {
            // Set up the request
            string expId = VERALogger.Instance.experimentUUID;
            string siteId = VERALogger.Instance.siteUUID;
            string apiKey = VERALogger.Instance.apiKey;

            string host = VERAHost.hostUrl;

            participantUUID = Guid.NewGuid().ToString().Replace("-", "");
            string url = host + "/api/participants/" + expId + "/" + siteId;
            VERADebugger.Log("Creating participant at url " + url, "VERA Participant", DebugPreference.Verbose);

            WWWForm form = new WWWForm();
            form.AddField("participantId", participantUUID);

            // Send the request
            UnityWebRequest request = UnityWebRequest.Post(url, form);
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return request.SendWebRequest();

            // Check success
            if (request.result == UnityWebRequest.Result.Success)
            {
                VERADebugger.Log("Successfully created a new participant; data will be recorded to this participant.", "VERA Participant", DebugPreference.Informative);

                // Parse the response to get the participant short ID
                string responseText = request.downloadHandler.text;

                // Parse JSON response
                ParticipantResponse response = null;
                bool parseSuccess = false;

                try
                {
                    response = JsonUtility.FromJson<ParticipantResponse>(responseText);
                    parseSuccess = true;
                }
                catch (System.Exception)
                {
                    parseSuccess = false;
                }

                if (parseSuccess && response != null && !string.IsNullOrEmpty(response.pID))
                {
                    if (int.TryParse(response.pID, out int pID))
                    {
                        participantShortId = pID;
                        VERADebugger.Log("Assigned participant short ID: " + participantShortId, "VERA Participant", DebugPreference.Informative);
                    }
                    else
                    {
                        VERADebugger.LogError("Failed to parse pID as integer; setting participantShortId=0 to allow local recording.", "VERA Participant");
                        participantShortId = 0;
                    }
                }
                else
                {
                    VERADebugger.LogError("Failed to create a new participant (missing/invalid pID in response); setting participantShortId=0 to allow local recording.", "VERA Participant");
                    participantShortId = 0;
                }
            }
            else
            {
                VERADebugger.LogError($"Failed to create a new participant; server request failed: result={request.result}, code={request.responseCode}, error={request.error}. Setting participantShortId=0 to allow local recording.", "VERA Participant");
                participantShortId = 0;
            }

            request.Dispose();
        }


        // Gets an existing participant from the server using the provided override ID
        private IEnumerator GetParticipantFromOverrideId(string overrideParticipantId)
        {
            VERADebugger.Log("Using override participant ID; attempting to retrieve existing participant...", "VERA Participant", DebugPreference.Informative);

            string expId = VERALogger.Instance.experimentUUID;
            string siteId = VERALogger.Instance.siteUUID;
            string apiKey = VERALogger.Instance.apiKey;

            string host = VERAHost.hostUrl;

            string urlGet = host + "/api/participants/" + overrideParticipantId;
            UnityWebRequest getRequest = UnityWebRequest.Get(urlGet);
            getRequest.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return getRequest.SendWebRequest();

            // Check success
            if (getRequest.result == UnityWebRequest.Result.Success)
            {
                // Parse the response to get the participant short ID
                string responseText = getRequest.downloadHandler.text;

                // Parse JSON response
                ParticipantResponse response = null;
                bool parseSuccess = false;

                try
                {
                    response = JsonUtility.FromJson<ParticipantResponse>(responseText);
                    parseSuccess = true;
                }
                catch (System.Exception)
                {
                    parseSuccess = false;
                }

                if (parseSuccess && response != null && int.TryParse(response.pID, out int pID))
                {
                    participantShortId = pID;
                    if (!string.IsNullOrEmpty(response.prolificID))
                    {
                        prolificID = response.prolificID;
                    }
                    if (!string.IsNullOrEmpty(response.unityID))
                    {
                        participantUUID = response.unityID;
                    }
                    VERADebugger.Log("Retrieved existing participant with short ID: " + participantShortId, "VERA Participant", DebugPreference.Informative);
                    getRequest.Dispose();
                    yield break;
                }
                else
                {
                    VERADebugger.LogError("Failed to retrieve existing participant with override ID; proceeding to create a new participant.", "VERA Participant");
                }
            }
            else
            {
                VERADebugger.LogError("Failed to retrieve existing participant with override ID; proceeding to create a new participant.", "VERA Participant");
            }

            getRequest.Dispose();
        }


        // Pushes a new Unity ID to an existing active participant in the database
        private IEnumerator PushUidToActiveParticipant(string databaseId)
        {
            // Create a new UUID
            participantUUID = Guid.NewGuid().ToString().Replace("-", "");

            // Set up the request
            string apiKey = VERALogger.Instance.apiKey;

            string host = VERAHost.hostUrl;
            string url = host + "/api/participants/" + databaseId + "/uid";
            VERADebugger.Log("Pushing unity ID to active participant at url " + url, "VERA Participant", DebugPreference.Informative);

            // Create JSON payload
            string jsonPayload = "{\"uid\":\"" + participantUUID + "\"}";
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);

            // Send the request
            UnityWebRequest request = UnityWebRequest.Put(url, bodyRaw);
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();

            // Check success
            if (request.result == UnityWebRequest.Result.Success)
            {
                VERADebugger.Log("Successfully updated active participant with Unity ID; data will be recorded to this participant.", "VERA Participant", DebugPreference.Informative);
            }
            else
            {
                VERADebugger.LogError("Failed to update active participant with Unity ID; data will not be recorded. Response: " + request.result + " - " + request.error, "VERA Participant");
            }

            request.Dispose();
        }


        #endregion


        #region PARTICIPANT PROGRESS


        // Sets the participant's progress via coroutine below
        public void SetParticipantProgress(ParticipantProgressState state)
        {
            StartCoroutine(RetryableChangeProgress(state));
        }


        // Tries to change the participant's progress; will try multiple times
        public IEnumerator RetryableChangeProgress(ParticipantProgressState state)
        {
            currentParticipantProgressState = state;

            DataRecordingType dataRecordingType = VERALogger.Instance.GetDataRecordingType();
            if (dataRecordingType == DataRecordingType.DoNotRecord ||
                dataRecordingType == DataRecordingType.OnlyRecordLocally)
            {
                yield break;
            }

            VERADebugger.Log("Updating current participant's state to \"" + state.ToString() + "\"...", "VERA Participant", DebugPreference.Verbose);

            // Try multiple times to send the request, in case of failure
            int attempt = 0;
            while (attempt < changeProgressMaxRetries)
            {
                string expId = VERALogger.Instance.experimentUUID;
                string siteId = VERALogger.Instance.siteUUID;
                string apiKey = VERALogger.Instance.apiKey;

                // Send the request
                UnityWebRequest request = UnityWebRequest.Put(
                  $"{VERAHost.hostUrl}/api/participants/progress/{expId}/{siteId}/{participantUUID}/{state.ToString()}",
                  new byte[0]
                );
                request.SetRequestHeader("Authorization", "Bearer " + apiKey);
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    // On success, notify completion
                    VERADebugger.Log($"Successfully set participant's state to {state}.", "VERA Participant", DebugPreference.Verbose);

                    // If the state is COMPLETE, mark the session as finalized
                    if (state == ParticipantProgressState.COMPLETE)
                        VERALogger.Instance.FinalizeSession();

                    request.Dispose();
                    yield break;
                }
                else
                {
                    // On failure, notify non-completion, wait, and try again
                    attempt++;
                    VERADebugger.LogWarning($"Attempt {attempt}: failed to set participant's state to {state}: {request.error}", "VERA Participant");
                    request.Dispose();
                    yield return new WaitForSeconds(1f);
                }
            }

            VERADebugger.LogError($"Failed to set participant's state to {state} after {changeProgressMaxRetries} attempts.", "VERA Participant");
        }


        // Returns whether this participant is in a "finalized" state (i.e., no new data should be recorded)
        // Finalized states currently include complete, incomplete, and terminated
        public bool IsInFinalizedState()
        {
            return (currentParticipantProgressState == ParticipantProgressState.COMPLETE ||
                currentParticipantProgressState == ParticipantProgressState.INCOMPLETE ||
                currentParticipantProgressState == ParticipantProgressState.TERMINATED);
        }


        #endregion


    }
}