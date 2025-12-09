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
                Debug.LogWarning("[VERA Participant] A participant already exists; not creating a new one.");
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
                Debug.LogWarning("[VERA Participant] A participant already exists; not creating a new one.");
                yield break;
            }

            // Ensure a participant exists to record data to
            yield return EnsureParticipant();

            // If after ensuring the participant we don't have a valid short ID, attempt creation retries
            int attempts = 0;
            int maxAttempts = 3;
            while ((participantShortId == 0) && attempts < maxAttempts)
            {
                attempts++;
                Debug.LogWarning($"[VERA Participant] participantShortId is {participantShortId} after EnsureParticipant(); attempting creation attempt {attempts}/{maxAttempts}...");
                yield return CreateParticipantCoroutine();

                if (participantShortId > 0)
                {
                    Debug.Log("[VERA Participant] Participant creation succeeded on retry.");
                    yield break;
                }

                // small delay before next attempt
                yield return new WaitForSeconds(1f);
            }

            if (participantShortId == 0)
            {
                Debug.LogError("[VERA Participant] Unable to obtain a valid participant short ID after retries. Local recording will continue but uploads may not attach to the correct participant on the server.");
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
            Debug.Log("[VERA Participant] Checking for active participant at url " + url);

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
                                Debug.Log("[VERA Participant] Active participant found with UUID: " + participantUUID + " (pID=" + participantShortId + ")");
                                request.Dispose();
                                yield break;
                            }
                            // Active participant exists, but no uid found in response, create a uid and push to database
                            else
                            {
                                Debug.Log("[VERA Participant] Active participant found, but has no associated uid. Pushing a new uid...");
                                request.Dispose();
                                yield return PushUidToActiveParticipant(databaseId);
                                yield break;
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[VERA Participant] Active participant response had invalid pID value: '{response.pID}'. Proceeding to create a new participant.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[VERA Participant] Failed to parse active participant response or response was null; proceeding to create a new participant.");
                    }
                }
            }

            // If the request failed to reach the server, log diagnostic details
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[VERA Participant] Active participant lookup request failed: result={request.result}, code={request.responseCode}, error={request.error}");
            }

            // Dispose request before continuing
            request.Dispose();

            // If we reach here, no active participant was found, create one
            Debug.Log("[VERA Participant] No active participant found for site; creating a new participant...");
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
            Debug.Log("[VERA Participant] Creating participant at url " + url);

            WWWForm form = new WWWForm();
            form.AddField("participantId", participantUUID);

            // Send the request
            UnityWebRequest request = UnityWebRequest.Post(url, form);
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return request.SendWebRequest();

            // Check success
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[VERA Participant] Successfully created a new participant; data will be recorded to this participant.");

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
                        Debug.Log("[VERA Participant] Assigned participant short ID: " + participantShortId);
                    }
                    else
                    {
                        Debug.LogError("[VERA Participant] Failed to parse pID as integer; setting participantShortId=0 to allow local recording.");
                        participantShortId = 0;
                    }
                }
                else
                {
                    Debug.LogError("[VERA Participant] Failed to create a new participant (missing/invalid pID in response); setting participantShortId=0 to allow local recording.");
                    participantShortId = 0;
                }
            }
            else
            {
                Debug.LogError($"[VERA Participant] Failed to create a new participant; server request failed: result={request.result}, code={request.responseCode}, error={request.error}. Setting participantShortId=0 to allow local recording.");
                participantShortId = 0;
            }

            request.Dispose();
        }


        // Gets an existing participant from the server using the provided override ID
        private IEnumerator GetParticipantFromOverrideId(string overrideParticipantId)
        {
            Debug.Log("[VERA Participant] Using override participant ID; attempting to retrieve existing participant...");

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
                    Debug.Log("[VERA Participant] Retrieved existing participant with short ID: " + participantShortId);
                    getRequest.Dispose();
                    yield break;
                }
                else
                {
                    Debug.LogError("[VERA Participant] Failed to retrieve existing participant with override ID; proceeding to create a new participant.");
                }
            }
            else
            {
                Debug.LogError("[VERA Participant] Failed to retrieve existing participant with override ID; proceeding to create a new participant.");
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
            Debug.Log("[VERA Participant] Pushing unity ID to active participant at url " + url);

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
                Debug.Log("[VERA Participant] Successfully updated active participant with Unity ID; data will be recorded to this participant.");
            }
            else
            {
                Debug.LogError("[VERA Participant] Failed to update active participant with Unity ID; data will not be recorded. Response: " + request.result + " - " + request.error);
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
            Debug.Log("[VERA Participant] Updating current participant's state to \"" + state.ToString() + "\"...");

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
                    Debug.Log($"[VERA Participant] Successfully set participant's state to {state}.");

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
                    Debug.LogWarning($"[VERA Participant] Attempt {attempt}: failed to set participant's state to {state}: {request.error}");
                    request.Dispose();
                    yield return new WaitForSeconds(1f);
                }
            }

            Debug.LogError($"[VERA Participant] Failed to set participant's state to {state} after {changeProgressMaxRetries} attempts.");
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