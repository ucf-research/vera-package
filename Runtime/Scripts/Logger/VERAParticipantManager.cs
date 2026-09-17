using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;

namespace VERA
{
    // Data structure for parsing participant/session identity from VERA API responses.
    // pID is a JSON number on current APIs; string values such as "P1" are recovered from raw JSON.
    [System.Serializable]
    internal class VeraParticipantIdentity
    {
        public bool success;
        public string databaseID;   // GET /api/sites/:siteId/active-participant
        public string databaseId;   // GET/POST /api/participants/...
        public string sessionId;
        public int sessionNumber;
        public int pID;
        public string unityID;
        public string prolificID;
    }

    [System.Serializable]
    internal class ParticipantCreationErrorResponse
    {
        public bool success;
        public string code;
        public string activationStatus;
        public string message;
    }

    internal class VERAParticipantManager : MonoBehaviour
    {

        // VERAParticipantManager handles the creation, ID, state change, etc. of the active participant

        public string participantUUID { get; private set; }
        public string participantDatabaseId { get; private set; }
        public string participantShortId { get; private set; }
        public string prolificID { get; private set; }
        public string sessionId { get; private set; }

        /// <summary>
        /// 1-based visit/session number from the VERA API. Single-session studies are 1.
        /// Returns -1 if no session has been assigned yet.
        /// </summary>
        public int sessionNumber { get; private set; } = -1;

        /// <summary>
        /// Parses a participant short ID from the server, which may be a plain integer ("1")
        /// or pilot-prefixed ("P1", "p2").
        /// </summary>
        public static bool TryParseParticipantShortId(string pID, out int numericId)
        {
            numericId = -1;
            if (string.IsNullOrEmpty(pID))
                return false;

            string numericPart = pID;
            if (pID.Length > 1 && (pID[0] == 'P' || pID[0] == 'p'))
                numericPart = pID.Substring(1);

            return int.TryParse(numericPart, out numericId);
        }

        /// <summary>
        /// Returns the numeric portion of the participant short ID for counterbalancing.
        /// Returns -1 if the short ID is missing or invalid.
        /// </summary>
        public int GetNumericParticipantShortId()
        {
            TryParseParticipantShortId(participantShortId, out int numericId);
            return numericId;
        }

        /// <summary>
        /// Participant short ID with an appended session suffix when a session number is assigned
        /// (e.g. "3S2"). Returns the unmodified short ID when sessionNumber is -1.
        /// </summary>
        public static string FormatParticipantSessionLabel(string participantShortId, int sessionNumber)
        {
            if (string.IsNullOrEmpty(participantShortId) || sessionNumber == -1)
                return participantShortId ?? "";

            return participantShortId + "S" + sessionNumber;
        }

        /// <summary>
        /// Participant short ID with session suffix when assigned (e.g. "3S2").
        /// </summary>
        public string GetParticipantSessionLabel()
        {
            return FormatParticipantSessionLabel(participantShortId, sessionNumber);
        }

        private bool TryAssignParticipantShortId(string pID)
        {
            if (string.IsNullOrEmpty(pID) || !TryParseParticipantShortId(pID, out _))
                return false;

            participantShortId = pID;
            return true;
        }

        private static bool TryParseParticipantIdentity(string responseText, out VeraParticipantIdentity response)
        {
            response = null;
            if (string.IsNullOrEmpty(responseText))
                return false;

            try
            {
                response = JsonUtility.FromJson<VeraParticipantIdentity>(responseText);
                return response != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Reads pID from raw JSON so both numeric (27) and pilot-prefixed ("P1") values work.
        /// JsonUtility cannot coerce JSON numbers into strings or quoted strings into ints.
        /// </summary>
        private static string ExtractParticipantShortId(string json, int numericPid)
        {
            if (TryExtractJsonField(json, "pID", out string rawPid))
            {
                rawPid = rawPid.Trim();
                if (TryParseParticipantShortId(rawPid, out _))
                    return rawPid;
            }

            if (numericPid != 0)
                return numericPid.ToString();

            return null;
        }

        private static bool TryExtractJsonField(string json, string fieldName, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName))
                return false;

            string key = "\"" + fieldName + "\"";
            int keyIndex = json.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex < 0)
                return false;

            int colonIndex = json.IndexOf(':', keyIndex + key.Length);
            if (colonIndex < 0)
                return false;

            int i = colonIndex + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i]))
                i++;

            if (i >= json.Length)
                return false;

            if (json[i] == '"')
            {
                int end = json.IndexOf('"', i + 1);
                if (end < 0)
                    return false;

                value = json.Substring(i + 1, end - i - 1);
                return true;
            }

            int start = i;
            if (json[i] == '-')
                i++;

            while (i < json.Length && char.IsDigit(json[i]))
                i++;

            if (i <= start || (json[start] == '-' && i == start + 1))
                return false;

            value = json.Substring(start, i - start);
            return true;
        }

        private bool TryApplyParticipantIdentity(string responseText)
        {
            if (!TryParseParticipantIdentity(responseText, out VeraParticipantIdentity response))
                return false;

            if (!TryAssignParticipantShortId(ExtractParticipantShortId(responseText, response.pID)))
                return false;

            string resolvedDatabaseId = !string.IsNullOrEmpty(response.databaseID)
                ? response.databaseID
                : response.databaseId;
            if (string.IsNullOrEmpty(resolvedDatabaseId))
                resolvedDatabaseId = ExtractFirstJsonField(responseText, "databaseID", "databaseId");
            if (!string.IsNullOrEmpty(resolvedDatabaseId))
                participantDatabaseId = resolvedDatabaseId;

            if (!string.IsNullOrEmpty(response.sessionId))
                sessionId = response.sessionId;

            sessionNumber = response.sessionNumber > 0 ? response.sessionNumber : 1;

            if (!string.IsNullOrEmpty(response.prolificID))
                prolificID = response.prolificID;

            string resolvedUnityId = !string.IsNullOrEmpty(response.unityID)
                ? response.unityID
                : ExtractFirstJsonField(responseText, "unityID", "unityId", "uid");
            if (!string.IsNullOrEmpty(resolvedUnityId))
                participantUUID = resolvedUnityId;

            // File/progress APIs need a participant ID. If the server omitted unityID,
            // fall back to the enrollment database ID rather than leaving uploads broken.
            if (string.IsNullOrEmpty(participantUUID) && !string.IsNullOrEmpty(participantDatabaseId))
                participantUUID = participantDatabaseId;

            return true;
        }

        private static string ExtractFirstJsonField(string json, params string[] fieldNames)
        {
            if (fieldNames == null)
                return null;

            for (int i = 0; i < fieldNames.Length; i++)
            {
                if (TryExtractJsonField(json, fieldNames[i], out string value) && !string.IsNullOrEmpty(value))
                    return value;
            }

            return null;
        }

        private void ClearAssignedParticipantIdentity()
        {
            participantShortId = null;
            sessionId = null;
            sessionNumber = -1;
        }

        private static bool IsExperimentUnavailableErrorCode(string code)
        {
            return code == "EXPERIMENT_PAUSED"
                || code == "EXPERIMENT_INACTIVE"
                || code == "EXPERIMENT_FULL";
        }

        private static bool TryParseExperimentUnavailableError(UnityWebRequest request, out ParticipantCreationErrorResponse errorResponse)
        {
            errorResponse = null;

            if (request.responseCode != 403)
                return false;

            string responseText = request.downloadHandler?.text;
            if (string.IsNullOrEmpty(responseText))
                return false;

            try
            {
                errorResponse = JsonUtility.FromJson<ParticipantCreationErrorResponse>(responseText);
            }
            catch (Exception)
            {
                return false;
            }

            return errorResponse != null
                && !errorResponse.success
                && IsExperimentUnavailableErrorCode(errorResponse.code)
                && !string.IsNullOrEmpty(errorResponse.message);
        }

        // Participant state management
        public enum ParticipantProgressState { CREATED, PRE_VR, IN_VR, POST_VR, WITHDRAWN, INCOMPLETE, PROCESSING };
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

            // Get the participant data from the server using the override ID, then set them as in experiment.
            // WebXR builds pass the portal/database participant ID here; that participant often has no
            // unityID yet, so we fall back to the portal ID for session APIs.
            yield return GetParticipantFromOverrideId(overrideParticipantId);
            EnsureParticipantIdForSessionApis(overrideParticipantId);

            if (string.IsNullOrEmpty(GetParticipantIdForSessionApis()))
            {
                VERADebugger.LogError(
                    "Cannot set IN_VR progress after override participant lookup: no participant ID is available.",
                    "VERA Participant");
                yield break;
            }

            // [LEGACY - Active Participant Workflow]
            // yield return PushUidToActiveParticipant(overrideParticipantId);
            yield return RetryableChangeProgress(ParticipantProgressState.IN_VR);
        }


        // Creates the participant entity from scratch (or uses the site's active participant if it exists)
        public IEnumerator CreateParticipant()
        {
            yield return CreateParticipantInternal(null);
        }

        // Creates or starts a new participant session using a specific researcher-visible pID
        public IEnumerator CreateParticipant(int manualId)
        {
            yield return CreateParticipantInternal(manualId);
        }

        private IEnumerator CreateParticipantInternal(int? manualId)
        {
            // If a participant already exists, do not create a new one
            if (!string.IsNullOrEmpty(participantUUID))
            {
                VERADebugger.LogWarning("A participant already exists; not creating a new one.", "VERA Participant");
                yield break;
            }

            DataRecordingType dataRecordingType = VERALogger.Instance.GetDataRecordingType();
            switch (dataRecordingType)
            {
                case DataRecordingType.DoNotRecord:
                    VERADebugger.Log("Data recording type is set to Do Not Record; not creating participant.", "VERA Participant", DebugPreference.Informative);
                    break;
                case DataRecordingType.OnlyRecordLocally:
                    participantUUID = Guid.NewGuid().ToString().Replace("-", "");
                    participantShortId = manualId.HasValue
                        ? manualId.Value.ToString()
                        : UnityEngine.Random.Range(100000, 999999).ToString();
                    sessionNumber = 1;
                    VERADebugger.Log(
                        manualId.HasValue
                            ? "Data recording type is set to Only Record Locally; using generated participant UUID and manual short ID " + participantShortId + " for local recording."
                            : "Data recording type is set to Only Record Locally; using generated participant UUID and random short ID for local recording.",
                        "VERA Participant",
                        DebugPreference.Informative);
                    break;
                case DataRecordingType.RecordLocallyAndLive:
                default:
                    yield return CreateOrFetchLiveParticipant(manualId);
                    break;
            }
        }


        // [LEGACY - Active Participant Workflow]
        // Ensure the participant entity is created
        // Uses site's active participant if it exists, otherwise creates a new one
#if false
        private IEnumerator EnsureParticipant()
        {
            // Check if the site has an active participant
            string siteId = VERALogger.Instance.siteUUID;

            string host = VERAHost.hostUrl;
            string url = host + "/api/sites/" + siteId + "/active-participant";
            VERADebugger.Log("Checking for active participant at url " + url, "VERA Participant", DebugPreference.Verbose);

            UnityWebRequest request = UnityWebRequest.Get(url);
            VERAHost.ApplyUserAgent(request);
            yield return request.SendWebRequest();

            // Check success
            if (request.result == UnityWebRequest.Result.Success)
            {
                // 200, active participant exists
                if (request.responseCode == 200)
                {
                    string responseText = request.downloadHandler.text;
                    if (TryApplyParticipantIdentity(responseText))
                    {
                        if (!string.IsNullOrEmpty(participantUUID))
                        {
                            VERADebugger.Log("Active participant found with UUID: " + participantUUID + " (pID=" + participantShortId + ", session=" + sessionNumber + ")", "VERA Participant", DebugPreference.Informative);
                            request.Dispose();
                            yield break;
                        }

                        VERADebugger.Log("Active participant found, but has no associated uid. Pushing a new uid...", "VERA Participant", DebugPreference.Informative);
                        request.Dispose();
                        yield return PushUidToActiveParticipant(participantDatabaseId);
                        yield break;
                    }

                    VERADebugger.LogWarning("Failed to parse active participant response or response was missing a valid pID; proceeding to create a new participant.", "VERA Participant");
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
#endif


        private IEnumerator CreateOrFetchLiveParticipant(int? manualId)
        {
            string fallbackOverrideId = VERALogger.Instance != null ? VERALogger.Instance.overrideParticipantId : null;

            // manualId must go through POST /create so the server starts a new session.
            // GET /active-participant returns the site's current visit; if that happens to be
            // the requested pID, a GET-first flow would reuse the old session and skip create.
            if (manualId.HasValue)
            {
                yield return CreateActiveParticipant(manualId);
            }
            else
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                yield return FetchActiveParticipant(null);
                if (string.IsNullOrEmpty(GetParticipantIdForSessionApis()) && !string.IsNullOrEmpty(fallbackOverrideId))
                    yield return GetParticipantFromOverrideId(fallbackOverrideId);
#else
                yield return CreateActiveParticipant(null);
#endif
            }

            EnsureParticipantIdForSessionApis(fallbackOverrideId);

            if (manualId.HasValue)
                WarnIfAssignedIdDoesNotMatch(manualId.Value);

            if (string.IsNullOrEmpty(GetParticipantIdForSessionApis()))
            {
                VERADebugger.LogError(
                    "Participant identity is missing unityID/databaseID; live uploads will fail until the server returns one of those fields.",
                    "VERA Participant");
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            if (string.IsNullOrEmpty(GetParticipantIdForSessionApis()))
            {
                VERADebugger.LogError(
                    "Cannot set IN_VR progress after participant lookup: no participant ID is available.",
                    "VERA Participant");
                yield break;
            }

            yield return RetryableChangeProgress(ParticipantProgressState.IN_VR);
#endif
        }

        private void WarnIfAssignedIdDoesNotMatch(int requestedId)
        {
            if (AssignedIdMatches(requestedId))
                return;

            if (string.IsNullOrEmpty(participantShortId))
            {
                VERADebugger.LogError(
                    "Requested participant ID " + requestedId + " but the server did not return a pID.",
                    "VERA Participant");
                return;
            }

            VERADebugger.LogError(
                "Requested participant ID " + requestedId + " but the server assigned pID '" + participantShortId +
                "'. The backend may not have received manualId.",
                "VERA Participant");
        }

        private bool AssignedIdMatches(int requestedId)
        {
            return TryParseParticipantShortId(participantShortId, out int assignedId) && assignedId == requestedId;
        }

        private static string AppendManualIdQuery(string url, int? manualId)
        {
            if (!manualId.HasValue)
                return url;

            return url + (url.Contains("?") ? "&" : "?") + "manualId=" + manualId.Value;
        }

        private static string CreateActiveParticipantUrl(string siteId, int? manualId)
        {
            // Stay on /create (no path segment) so older servers do not 404.
            // Put manualId in the query string AND the urlencoded body (see CreateActiveParticipant).
            string url = VERAHost.hostUrl + "/api/sites/" + siteId + "/active-participant/create";
            if (manualId.HasValue)
                url += "?manualId=" + manualId.Value;
            return url;
        }

        // WebXR / manual-ID: fetch the site's current participant, optionally looking up/creating a specific pID.
        private IEnumerator FetchActiveParticipant(int? manualId)
        {
            string siteId = VERALogger.Instance.siteUUID;
            string apiKey = VERALogger.Instance.apiKey;
            string url = AppendManualIdQuery(
                VERAHost.hostUrl + "/api/sites/" + siteId + "/active-participant",
                manualId);

            VERADebugger.Log(
                manualId.HasValue
                    ? "Fetching active participant with manual ID " + manualId.Value + " at url " + url
                    : "Fetching active participant at url " + url,
                "VERA Participant",
                manualId.HasValue ? DebugPreference.Informative : DebugPreference.Verbose);

            UnityWebRequest request = UnityWebRequest.Get(url);
            VERAHost.ApplyBearerAuth(request, apiKey);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler != null ? request.downloadHandler.text : "";
                VERADebugger.Log("Active participant response: " + responseText, "VERA Participant", DebugPreference.Verbose);
                if (TryApplyParticipantIdentity(responseText))
                {
                    VERADebugger.Log(
                        "Active participant found with UUID: " + (participantUUID ?? "null") + " (pID=" + participantShortId + ", session=" + sessionNumber + ")",
                        "VERA Participant",
                        DebugPreference.Informative);
                }
                else
                {
                    VERADebugger.LogError(
                        "Failed to parse active participant response or response was missing a valid pID. Response: " + responseText,
                        "VERA Participant");
                    ClearAssignedParticipantIdentity();
                }
            }
            else
            {
                LogParticipantRequestFailure(request, manualId, "fetch active participant");
            }

            request.Dispose();
        }

        // Standalone: create or start a new session for the site's active participant, optionally using a specific pID.
        private IEnumerator CreateActiveParticipant(int? manualId)
        {
            string siteId = VERALogger.Instance.siteUUID;
            string apiKey = VERALogger.Instance.apiKey;
            string url = CreateActiveParticipantUrl(siteId, manualId);

            VERADebugger.Log(
                manualId.HasValue
                    ? "Creating participant with manual ID " + manualId.Value + " at url " + url
                    : "Creating participant at url " + url,
                "VERA Participant",
                manualId.HasValue ? DebugPreference.Informative : DebugPreference.Verbose);

            // Do NOT use WWWForm here: Unity WWWForm posts multipart/form-data, which Express
            // does not parse without multer, so req.body.manualId never arrives and the server
            // auto-assigns the next pID. Use urlencoded so bodyParser.urlencoded populates req.body.
            UnityWebRequest request;
            if (manualId.HasValue)
            {
                string formBody = "manualId=" + Uri.EscapeDataString(manualId.Value.ToString());
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(formBody);
                request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
                request.SetRequestHeader("X-Vera-Manual-Id", manualId.Value.ToString());
            }
            else
            {
                request = CreateJsonPostRequest(url, "{}");
            }
            VERAHost.ApplyBearerAuth(request, apiKey);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler != null ? request.downloadHandler.text : "";
                VERADebugger.Log("Create participant response: " + responseText, "VERA Participant", DebugPreference.Verbose);

                if (TryApplyParticipantIdentity(responseText))
                {
                    VERADebugger.Log(
                        "Assigned participant short ID: " + participantShortId + " (session " + sessionNumber + ", uuid=" + (participantUUID ?? "null") + ")",
                        "VERA Participant",
                        DebugPreference.Informative);
                }
                else
                {
                    VERADebugger.LogError(
                        "Failed to create a new participant (missing/invalid pID in response). Response: " + responseText,
                        "VERA Participant");
                    ClearAssignedParticipantIdentity();
                }
            }
            else
            {
                if (TryParseExperimentUnavailableError(request, out ParticipantCreationErrorResponse errorResponse))
                {
                    participantUUID = null;
                    ClearAssignedParticipantIdentity();
                    request.Dispose();
                    VERADebugger.LogError(errorResponse.message, "VERA Participant");
                    throw new VERAExperimentUnavailableException(
                        errorResponse.code,
                        errorResponse.activationStatus,
                        errorResponse.message);
                }

                LogParticipantRequestFailure(request, manualId, "create participant");
                if (string.IsNullOrEmpty(participantUUID))
                    participantUUID = Guid.NewGuid().ToString().Replace("-", "");
                ClearAssignedParticipantIdentity();
            }

            request.Dispose();
        }

        private static UnityWebRequest CreateJsonPostRequest(string url, string jsonPayload)
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload ?? "{}");
            UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            UploadHandlerRaw uploadHandler = new UploadHandlerRaw(bodyRaw);
            uploadHandler.contentType = "application/json";
            request.uploadHandler = uploadHandler;
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        private void LogParticipantRequestFailure(UnityWebRequest request, int? manualId, string action)
        {
            string responseText = request.downloadHandler != null ? request.downloadHandler.text : "";
            string details = "result=" + request.result + ", code=" + request.responseCode + ", error=" + request.error +
                (string.IsNullOrEmpty(responseText) ? "" : ", response=" + responseText);

            if (request.responseCode == 400 && manualId.HasValue)
            {
                VERADebugger.LogError(
                    "Invalid manual participant ID '" + manualId.Value + "' while trying to " + action + ": " + details,
                    "VERA Participant");
                return;
            }

            VERADebugger.LogError("Failed to " + action + ": " + details, "VERA Participant");
        }


        // Gets an existing participant from the server using the provided override ID
        private IEnumerator GetParticipantFromOverrideId(string overrideParticipantId)
        {
            VERADebugger.Log("Using override participant ID; attempting to retrieve existing participant...", "VERA Participant", DebugPreference.Informative);

            participantDatabaseId = overrideParticipantId;

            string expId = VERALogger.Instance.experimentUUID;
            string siteId = VERALogger.Instance.siteUUID;
            string apiKey = VERALogger.Instance.apiKey;

            string host = VERAHost.hostUrl;

            string urlGet = host + "/api/participants/" + overrideParticipantId;
            UnityWebRequest getRequest = UnityWebRequest.Get(urlGet);
            VERAHost.ApplyBearerAuth(getRequest, apiKey);
            yield return getRequest.SendWebRequest();

            // Check success
            if (getRequest.result == UnityWebRequest.Result.Success)
            {
                if (TryApplyParticipantIdentity(getRequest.downloadHandler.text))
                {
                    if (string.IsNullOrEmpty(participantUUID))
                    {
                        // Portal/WebXR participants frequently have no unityID. Use the portal
                        // database ID so progress/upload URLs are not built with an empty segment.
                        EnsureParticipantIdForSessionApis(overrideParticipantId);
                        VERADebugger.LogWarning(
                            $"Retrieved participant short ID {participantShortId}, but unityID was empty. " +
                            $"Using portal participant ID '{GetParticipantIdForSessionApis()}' for session APIs.",
                            "VERA Participant");
                    }
                    VERADebugger.Log("Retrieved existing participant with short ID: " + participantShortId + " (session " + sessionNumber + ")", "VERA Participant", DebugPreference.Informative);
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


        // [LEGACY - Active Participant Workflow]
        // Pushes a new Unity ID to an existing active participant in the database
#if false
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
            VERAHost.ApplyBearerAuth(request, apiKey);
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
#endif


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

            string progressParticipantId = GetParticipantIdForSessionApis();
            if (string.IsNullOrEmpty(progressParticipantId))
            {
                VERADebugger.LogError(
                    $"Cannot update participant state to {state}: participant ID is empty " +
                    $"(unityID='{participantUUID ?? ""}', databaseID='{participantDatabaseId ?? ""}').",
                    "VERA Participant");
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
                  $"{VERAHost.hostUrl}/api/participants/progress/{expId}/{siteId}/{progressParticipantId}/{state.ToString()}",
                  new byte[0]
                );
                VERAHost.ApplyBearerAuth(request, apiKey);
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    // On success, notify completion
                    VERADebugger.Log($"Successfully set participant's state to {state}.", "VERA Participant", DebugPreference.Verbose);

                    // If the state is POST_VR (VR session completed), mark the session as finalized
                    if (state == ParticipantProgressState.POST_VR)
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
        // Finalized states currently include post-VR (VR session completed), incomplete, and withdrawn
        public bool IsInFinalizedState()
        {
            return (currentParticipantProgressState == ParticipantProgressState.POST_VR ||
                currentParticipantProgressState == ParticipantProgressState.INCOMPLETE ||
                currentParticipantProgressState == ParticipantProgressState.WITHDRAWN);
        }


        #endregion


        #region ACCESSIBILITY SETTINGS


        /// <summary>
        /// Fetches the participant's accessibility settings from the VERA server.
        /// </summary>
        public IEnumerator FetchAccessibilitySettings(Action<VERAAccessibilitySettings> onSuccess, Action<string> onFailure = null)
        {
            DataRecordingType dataRecordingType = VERALogger.Instance.GetDataRecordingType();
            if (dataRecordingType != DataRecordingType.RecordLocallyAndLive)
            {
                onFailure?.Invoke("Accessibility settings are only available when recording to the server.");
                yield break;
            }

            string participantId = GetParticipantIdForAccessibilityApi();
            if (string.IsNullOrEmpty(participantId))
            {
                onFailure?.Invoke("No participant ID available.");
                yield break;
            }

            string apiKey = VERALogger.Instance.apiKey;
            string url = $"{VERAHost.hostUrl}/api/participants/{participantId}/accessibility-settings";
            VERADebugger.Log("Fetching accessibility settings from " + url, "VERA Participant", DebugPreference.Verbose);

            UnityWebRequest request = UnityWebRequest.Get(url);
            VERAHost.ApplyBearerAuth(request, apiKey);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = $"Failed to fetch accessibility settings: {request.error}";
                VERADebugger.LogWarning(error, "VERA Participant");
                onFailure?.Invoke(error);
                request.Dispose();
                yield break;
            }

            string responseText = request.downloadHandler?.text;
            request.Dispose();

            if (string.IsNullOrEmpty(responseText))
            {
                onFailure?.Invoke("Accessibility settings response was empty.");
                yield break;
            }

            VERAAccessibilitySettingsResponse response = null;
            try
            {
                response = JsonUtility.FromJson<VERAAccessibilitySettingsResponse>(responseText);
            }
            catch (Exception ex)
            {
                onFailure?.Invoke($"Failed to parse accessibility settings response: {ex.Message}");
                yield break;
            }

            if (response == null || !response.success || response.accessibilitySettings == null)
            {
                onFailure?.Invoke("Accessibility settings response was invalid.");
                yield break;
            }

            onSuccess?.Invoke(response.accessibilitySettings);
        }


        private string GetParticipantIdForAccessibilityApi()
        {
            // Accessibility settings are keyed by the portal/database participant ID when available.
            if (!string.IsNullOrEmpty(participantDatabaseId))
                return participantDatabaseId;

            return participantUUID;
        }

        /// <summary>
        /// Resolves the participant identifier used in progress APIs.
        /// Prefers unityID when present; otherwise falls back to the portal/database participant ID
        /// used by WebXR override initialization.
        /// </summary>
        private string GetParticipantIdForSessionApis()
        {
            if (!string.IsNullOrEmpty(participantUUID))
                return participantUUID;

            if (!string.IsNullOrEmpty(participantDatabaseId))
                return participantDatabaseId;

            return null;
        }

        /// <summary>
        /// Ensures participantUUID is populated for runtime APIs when a portal participant was loaded
        /// without a unityID (common in WebXR builds).
        /// </summary>
        private void EnsureParticipantIdForSessionApis(string fallbackOverrideId = null)
        {
            if (!string.IsNullOrEmpty(participantUUID))
                return;

            if (!string.IsNullOrEmpty(participantDatabaseId))
            {
                participantUUID = participantDatabaseId;
                return;
            }

            if (!string.IsNullOrEmpty(fallbackOverrideId))
            {
                participantDatabaseId = fallbackOverrideId;
                participantUUID = fallbackOverrideId;
            }
        }


        #endregion


    }
}