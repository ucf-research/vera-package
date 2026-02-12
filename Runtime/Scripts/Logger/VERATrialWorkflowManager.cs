using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

namespace VERA
{
    /// <summary>
    /// Represents a trial configuration from the VERA trial workflow.
    /// Contains trial metadata, type information, conditions, and child trials for within/between-subjects designs.
    /// </summary>
    [System.Serializable]
    public class TrialConfig
    {
        public string id;
        public string type;
        public string label;
        public string description;
        public int order;
        public int repetitionCount;
        public Dictionary<string, string> conditions;
        public TrialConfig[] childTrials;
        public string[] withinSubjectsIVs;
        public string[] betweenSubjectsIVs;
        public string randomizationType;
        public string trialOrdering;
        public string randomizationLevel;
        public bool isRandomized;
        // For surveys attached to trials
        public string attachedSurveyId;
        public string attachedSurveyName;
        public string surveyPosition;
        // For standalone surveys
        public string surveyId;
        public string surveyName; // Full survey name (different from label)
        public string instanceId;
        public Survey survey;
        public SurveyInstanceData surveyInstanceData;
        public Dictionary<string, int> perTrialRepetitions;

        // Group tracking (set during flattening)
        public string parentGroupId;        // ID of parent within/between group (null if standalone)
        public string parentGroupType;      // "within" or "between" (null if standalone)
        public bool parentRequiresLatinSquare;  // Whether parent group uses Latin square ordering
    }

    /// <summary>
    /// Represents the current state of a trial
    /// </summary>
    public enum TrialState
    {
        NotStarted,
        InProgress,
        Completed,
        Aborted
    }

    /// <summary>
    /// Manages trial workflow for VERA experiments.
    /// Access through VERALogger: VERALogger.Instance.GetCurrentTrial(), VERALogger.Instance.StartNextTrial(), etc.
    /// </summary>
    internal class VERATrialWorkflowManager : MonoBehaviour
    {
        private List<TrialConfig> trialWorkflow = new List<TrialConfig>();
        private List<TrialConfig> allTrials = new List<TrialConfig>();
        private int currentTrialIndex = -1;

        public bool isInitialized { get; private set; } = false;
        public TrialState currentTrialState { get; private set; } = TrialState.NotStarted;

        // Survey-related events
        public event System.Action<string, string, string> OnSurveyRequired; // surveyId, surveyName, position ("before", "after", or "standalone")
        public event System.Action<TrialConfig> OnTrialStarting; // Called before trial starts, allows survey check
        public event System.Action<TrialConfig> OnTrialCompleted; // Called after trial completes, allows survey check

        // Automation events
        public event System.Action<TrialConfig> OnTrialReady; // Fired in automated mode when a trial is active and ready for developer logic. Call CompleteAutomatedTrial() when done.
        public event System.Action OnWorkflowCompleted; // Fired when the automated workflow finishes all trials

        private bool waitingForSurvey = false;
        private string pendingSurveyPosition = null;

        // Automation state
        private bool automatedMode = false;
        private bool waitingForTrialLogic = false;
        private Coroutine automatedWorkflowCoroutine = null;

        private float trialStartTime = 0f;
        private float trialDuration = 0f;
        private string experimentUUID;
        private string apiKey;
        private string participantUUID;

        // Retry configuration
        private const int MAX_API_RETRIES = 3;
        private const float INITIAL_RETRY_DELAY = 1f;
        private const float MAX_RETRY_DELAY = 10f;

        // Group metadata cache for performance
        private Dictionary<string, GroupMetadata> groupMetadataCache = new Dictionary<string, GroupMetadata>();
        private Dictionary<string, int> trialToGroupIndexCache = new Dictionary<string, int>();

        // Local checkpoint storage key
        private string LocalCheckpointKey => $"VERA_Checkpoint_{experimentUUID}_{participantUUID}";

        [System.Serializable]
        private class GroupMetadata
        {
            public string groupId;
            public int totalTrials;
            public List<int> trialIndices;
        }


        #region LIFECYCLE

        /// <summary>
        /// Called when the GameObject is destroyed. Ensures proper cleanup.
        /// </summary>
        private void OnDestroy()
        {
            Cleanup();
        }

        #endregion


        #region INITIALIZATION

        public IEnumerator Initialize(string experimentId, string authToken, int participantNumber = -1, string participantId = null, Dictionary<string, int> manualBetweenSubjectsAssignments = null)
        {
            if (isInitialized)
            {
                Debug.LogWarning("[VERA Trial Workflow] Trial workflow is already initialized.");
                yield break;
            }

            experimentUUID = experimentId;
            apiKey = authToken;
            this.participantNumber = participantNumber;
            this.participantUUID = participantId;
            this.manualBetweenSubjectsAssignments = manualBetweenSubjectsAssignments;

            yield return FetchTrialWorkflow();

            // Check for saved progress and resume if available
            if (!string.IsNullOrEmpty(participantUUID))
            {
                yield return CheckAndResumeProgress();
            }
        }

        private int participantNumber = -1;
        private Dictionary<string, int> manualBetweenSubjectsAssignments;

        #endregion


        #region HELPER METHODS

        /// <summary>
        /// Executes a UnityWebRequest with retry logic and proper disposal.
        /// </summary>
        private IEnumerator ExecuteWebRequestWithRetry(UnityWebRequest request, System.Action<VERAWebRequestResult> onComplete, int maxRetries = MAX_API_RETRIES)
        {
            VERAWebRequestResult result = new VERAWebRequestResult();
            int attemptCount = 0;
            float retryDelay = INITIAL_RETRY_DELAY;

            while (attemptCount < maxRetries)
            {
                attemptCount++;
                UnityWebRequest currentRequest = null;

                try
                {
                    // Clone the request for retry attempts
                    currentRequest = CloneWebRequest(request);

                    yield return currentRequest.SendWebRequest();

                    if (currentRequest.result == UnityWebRequest.Result.Success)
                    {
                        result.success = true;
                        result.jsonResponse = currentRequest.downloadHandler?.text;
                        result.responseCode = (int)currentRequest.responseCode;
                        onComplete?.Invoke(result);
                        yield break;
                    }
                    else if (currentRequest.responseCode == 404 || currentRequest.responseCode == 401 || currentRequest.responseCode == 403)
                    {
                        // Don't retry on client errors
                        result.success = false;
                        result.error = currentRequest.error;
                        result.responseCode = (int)currentRequest.responseCode;
                        Debug.LogWarning($"[VERA Trial Workflow] Request failed with non-retryable error (HTTP {currentRequest.responseCode}): {currentRequest.error}");
                        onComplete?.Invoke(result);
                        yield break;
                    }
                    else
                    {
                        Debug.LogWarning($"[VERA Trial Workflow] Request attempt {attemptCount}/{maxRetries} failed: {currentRequest.error}");

                        if (attemptCount < maxRetries)
                        {
                            yield return new WaitForSeconds(retryDelay);
                            retryDelay = Mathf.Min(retryDelay * 2f, MAX_RETRY_DELAY);
                        }
                    }
                }
                finally
                {
                    currentRequest?.Dispose();
                }
            }

            // All retries exhausted
            result.success = false;
            result.error = $"Request failed after {maxRetries} attempts";
            Debug.LogError($"[VERA Trial Workflow] {result.error}");
            onComplete?.Invoke(result);
        }

        /// <summary>
        /// Clones a UnityWebRequest for retry attempts.
        /// </summary>
        private UnityWebRequest CloneWebRequest(UnityWebRequest original)
        {
            UnityWebRequest clone;

            if (original.method == "POST" || original.method == "PUT")
            {
                clone = new UnityWebRequest(original.url, original.method);
                if (original.uploadHandler != null && original.uploadHandler.data != null)
                {
                    clone.uploadHandler = new UploadHandlerRaw(original.uploadHandler.data);
                }
                clone.downloadHandler = new DownloadHandlerBuffer();
            }
            else
            {
                clone = UnityWebRequest.Get(original.url);
            }

            // Copy headers
            var headers = original.GetRequestHeader("Authorization");
            if (!string.IsNullOrEmpty(headers))
                clone.SetRequestHeader("Authorization", headers);

            headers = original.GetRequestHeader("Content-Type");
            if (!string.IsNullOrEmpty(headers))
                clone.SetRequestHeader("Content-Type", headers);

            return clone;
        }

        /// <summary>
        /// Validates trial configuration data to prevent runtime errors.
        /// </summary>
        private bool ValidateTrialConfig(TrialConfig trial, out string validationError)
        {
            validationError = null;

            if (trial == null)
            {
                validationError = "Trial configuration is null";
                return false;
            }

            if (string.IsNullOrEmpty(trial.id))
            {
                validationError = "Trial ID is missing";
                return false;
            }

            if (string.IsNullOrEmpty(trial.type))
            {
                validationError = $"Trial type is missing for trial '{trial.id}'";
                return false;
            }

            if (trial.repetitionCount < 0)
            {
                validationError = $"Invalid repetitionCount ({trial.repetitionCount}) for trial '{trial.id}'";
                return false;
            }

            // Validate that within/between GROUPS have child trials
            // Only trials with IVs defined are groups that need childTrials
            bool isGroup = (trial.type == "within" && trial.withinSubjectsIVs != null && trial.withinSubjectsIVs.Length > 0) ||
                           (trial.type == "between" && trial.betweenSubjectsIVs != null && trial.betweenSubjectsIVs.Length > 0);

            if (isGroup && (trial.childTrials == null || trial.childTrials.Length == 0))
            {
                validationError = $"Group trial '{trial.id}' (type: {trial.type}) has no child trials";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Saves checkpoint to local storage as fallback.
        /// </summary>
        private void SaveLocalCheckpoint(int trialIndex)
        {
            try
            {
                string checkpointData = JsonUtility.ToJson(new CheckpointResponse { currentTrialIndex = trialIndex });
                PlayerPrefs.SetString(LocalCheckpointKey, checkpointData);
                PlayerPrefs.Save();
                Debug.Log($"[VERA Trial Workflow] Local checkpoint saved: trial {trialIndex + 1}/{trialWorkflow.Count}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VERA Trial Workflow] Failed to save local checkpoint: {e.Message}");
            }
        }

        /// <summary>
        /// Loads checkpoint from local storage.
        /// </summary>
        private bool TryLoadLocalCheckpoint(out int trialIndex)
        {
            trialIndex = -1;
            try
            {
                if (PlayerPrefs.HasKey(LocalCheckpointKey))
                {
                    string checkpointData = PlayerPrefs.GetString(LocalCheckpointKey);
                    CheckpointResponse checkpoint = JsonUtility.FromJson<CheckpointResponse>(checkpointData);
                    if (checkpoint != null && checkpoint.currentTrialIndex >= 0)
                    {
                        trialIndex = checkpoint.currentTrialIndex;
                        Debug.Log($"[VERA Trial Workflow] Local checkpoint found: trial {trialIndex + 1}");
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VERA Trial Workflow] Failed to load local checkpoint: {e.Message}");
            }
            return false;
        }

        /// <summary>
        /// Clears local checkpoint storage.
        /// </summary>
        private void ClearLocalCheckpoint()
        {
            if (PlayerPrefs.HasKey(LocalCheckpointKey))
            {
                PlayerPrefs.DeleteKey(LocalCheckpointKey);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Validates that the workflow is in a valid state to start trials.
        /// </summary>
        private bool ValidateWorkflowReadyToStart(out string error)
        {
            error = null;

            if (!isInitialized)
            {
                error = "Workflow not initialized";
                return false;
            }

            if (trialWorkflow.Count == 0)
            {
                error = "No trials in workflow";
                return false;
            }

            // Check if Latin square is required but not applied
            if (withinGroupsForLatinSquare.Count > 0 && currentTrialIndex < 0)
            {
                // This is just a warning, not an error - Latin square is optional
                Debug.LogWarning("[VERA Trial Workflow] Latin square ordering is available but may not have been applied. Call ApplyLatinSquareOrdering() if needed.");
            }

            return true;
        }

        /// <summary>
        /// Builds group metadata cache for performance optimization.
        /// </summary>
        private void BuildGroupMetadataCache()
        {
            groupMetadataCache.Clear();
            trialToGroupIndexCache.Clear();

            for (int i = 0; i < trialWorkflow.Count; i++)
            {
                var trial = trialWorkflow[i];
                if (!string.IsNullOrEmpty(trial.parentGroupId))
                {
                    if (!groupMetadataCache.ContainsKey(trial.parentGroupId))
                    {
                        groupMetadataCache[trial.parentGroupId] = new GroupMetadata
                        {
                            groupId = trial.parentGroupId,
                            totalTrials = 0,
                            trialIndices = new List<int>()
                        };
                    }

                    var metadata = groupMetadataCache[trial.parentGroupId];
                    metadata.totalTrials++;
                    metadata.trialIndices.Add(i);

                    // Cache the trial's position in its group
                    string cacheKey = $"{trial.parentGroupId}_{i}";
                    trialToGroupIndexCache[cacheKey] = metadata.trialIndices.Count - 1;
                }
            }

            Debug.Log($"[VERA Trial Workflow] Built group metadata cache: {groupMetadataCache.Count} groups");
        }

        /// <summary>
        /// Validates that the participant number is appropriate for complete Latin square counterbalancing.
        /// Warns if the participant assignment may result in incomplete counterbalancing.
        /// </summary>
        private void ValidateLatinSquareCounterbalancing(int participantNumber)
        {
            if (withinGroupsForLatinSquare == null || withinGroupsForLatinSquare.Count == 0)
            {
                // Check entire workflow for legacy mode
                int totalConditions = trialWorkflow != null ? trialWorkflow.Count : 0;
                if (totalConditions > 0)
                {
                    ValidateCounterbalancingForGroup("entire workflow", totalConditions, participantNumber);
                }
                return;
            }

            // Check each within-subjects group
            foreach (var group in withinGroupsForLatinSquare)
            {
                string groupId = group.Key;
                List<int> indices = group.Value;

                if (indices != null && indices.Count > 0)
                {
                    int conditionCount = indices.Count;
                    ValidateCounterbalancingForGroup(groupId, conditionCount, participantNumber);
                }
            }
        }

        /// <summary>
        /// Validates counterbalancing for a specific group and provides warnings.
        /// </summary>
        private void ValidateCounterbalancingForGroup(string groupName, int conditionCount, int participantNumber)
        {
            if (conditionCount == 0) return;

            // Calculate which "cycle" this participant is in
            int cyclePosition = participantNumber % conditionCount;
            int cycleNumber = participantNumber / conditionCount;

            // Warn if we're in a very high cycle (might indicate an error)
            if (cycleNumber >= 100)
            {
                Debug.LogWarning($"[VERA Trial Workflow] Participant {participantNumber} is in cycle {cycleNumber} for group '{groupName}' ({conditionCount} conditions). This seems unusually high - please verify the participant number is correct.");
            }

            // Provide info about counterbalancing status
            if (cycleNumber == 0)
            {
                // First cycle - perfect for counterbalancing
                int participantsNeeded = conditionCount - (participantNumber + 1);
                if (participantsNeeded > 0)
                {
                    Debug.Log($"[VERA Trial Workflow] Group '{groupName}': Participant {participantNumber} assigned to row {cyclePosition}/{conditionCount}. Need {participantsNeeded} more participant(s) to complete first counterbalancing cycle.");
                }
                else
                {
                    Debug.Log($"[VERA Trial Workflow] Group '{groupName}': First counterbalancing cycle complete. Participant {participantNumber} is the last of cycle 0.");
                }
            }
            else
            {
                Debug.Log($"[VERA Trial Workflow] Group '{groupName}': Participant {participantNumber} assigned to row {cyclePosition}/{conditionCount} (cycle {cycleNumber}). Counterbalancing will continue across multiple cycles.");
            }

            // Warn if using a large participant number with few conditions
            if (conditionCount <= 4 && participantNumber > 1000)
            {
                Debug.LogWarning($"[VERA Trial Workflow] Group '{groupName}' has only {conditionCount} conditions but participant number is {participantNumber}. Consider using sequential participant numbers (0, 1, 2, ...) for better counterbalancing tracking.");
            }
        }

        #endregion


        #region PROPERTIES

        public int TotalTrialCount => trialWorkflow.Count;
        public int CurrentTrialIndex => currentTrialIndex;
        public bool HasMoreTrials => isInitialized && (currentTrialIndex + 1) < trialWorkflow.Count;
        public bool IsTrialInProgress => currentTrialState == TrialState.InProgress;
        public List<TrialConfig> AllWorkflowItems => allTrials;
        public TrialConfig CurrentTrial => GetCurrentTrial();

        #endregion


        #region TRIAL ACCESS

        public TrialConfig GetCurrentTrial()
        {
            if (!isInitialized || currentTrialIndex < 0 || currentTrialIndex >= trialWorkflow.Count)
            {
                return null;
            }
            return trialWorkflow[currentTrialIndex];
        }

        public TrialConfig StartNextTrial()
        {
            // Validate workflow state
            if (!ValidateWorkflowReadyToStart(out string error))
            {
                Debug.LogWarning($"[VERA Trial Workflow] Cannot start next trial: {error}");
                return null;
            }

            // Prevent starting next trial if current trial is still in progress
            if (currentTrialState == TrialState.InProgress)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot start next trial: current trial is still in progress. Call CompleteTrial() or AbortTrial() first.");
                return null;
            }

            // Check if we're waiting for a survey to complete
            if (waitingForSurvey)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot start next trial: waiting for attached survey to complete. Call MarkSurveyCompleted() first.");
                return null;
            }

            currentTrialIndex++;

            if (currentTrialIndex >= trialWorkflow.Count)
            {
                Debug.Log("[VERA Trial Workflow] No more trials in workflow.");
                currentTrialIndex = trialWorkflow.Count;
                return null;
            }

            TrialConfig trial = trialWorkflow[currentTrialIndex];

            // Validate trial before starting
            if (trial == null)
            {
                Debug.LogError($"[VERA Trial Workflow] Trial at index {currentTrialIndex} is null! This should not happen.");
                currentTrialState = TrialState.Aborted;
                return null;
            }

            // Fire event before starting trial (allows for survey checks)
            OnTrialStarting?.Invoke(trial);

            // Check for standalone survey - must complete before workflow continues
            if (trial.type == "survey")
            {
                string sid = trial.instanceId ?? trial.surveyId;
                string sname = trial.surveyName ?? trial.label;
                Debug.Log($"[VERA Trial Workflow] Standalone survey '{sname}' encountered. Waiting for survey completion.");
                waitingForSurvey = true;
                pendingSurveyPosition = "standalone";
                OnSurveyRequired?.Invoke(sid, sname, "standalone");
                currentTrialIndex--; // Reset index so we can retry after survey completes
                return null;
            }

            // Check for attached survey that should show BEFORE trial
            if (!string.IsNullOrEmpty(trial.attachedSurveyId) && trial.surveyPosition == "before")
            {
                Debug.Log($"[VERA Trial Workflow] Trial has attached survey '{trial.attachedSurveyName}' to show BEFORE trial starts.");
                waitingForSurvey = true;
                pendingSurveyPosition = "before";
                OnSurveyRequired?.Invoke(trial.attachedSurveyId, trial.attachedSurveyName, "before");

                // Don't start trial yet - waiting for survey completion
                // Developer should call MarkSurveyCompleted() when survey is done, then call StartNextTrial() again
                currentTrialIndex--; // Reset index so we can try again after survey
                return null;
            }

            currentTrialState = TrialState.InProgress;
            trialStartTime = Time.time;
            trialDuration = 0f;

            Debug.Log($"[VERA Trial Workflow] Started trial {currentTrialIndex + 1}/{trialWorkflow.Count}: {trial.label ?? "Unlabeled"}");

            // Update experiment-level IV conditions from trial conditions
            if (trial.conditions != null && trial.conditions.Count > 0)
            {
                string conditionsStr = string.Join(", ",
                    System.Linq.Enumerable.Select(trial.conditions, kvp => $"{kvp.Key}={kvp.Value}"));
                Debug.Log($"[VERA Trial Workflow] Trial conditions: {conditionsStr}");

                // Set the experiment-level IV values to match this trial's conditions
                foreach (var kvp in trial.conditions)
                {
                    VERALogger.Instance.SetSelectedIVValue(kvp.Key, kvp.Value);
                }
            }

            return trial;
        }

        public TrialConfig PeekNextTrial()
        {
            if (!isInitialized)
                return null;

            int nextIndex = currentTrialIndex + 1;
            if (nextIndex >= trialWorkflow.Count)
                return null;

            return trialWorkflow[nextIndex];
        }

        public TrialConfig GetNextTrial()
        {
            // Validate workflow state
            if (!ValidateWorkflowReadyToStart(out string error))
            {
                Debug.LogWarning($"[VERA Trial Workflow] Cannot get next trial: {error}");
                return null;
            }

            // Prevent advancing if current trial is still in progress
            if (currentTrialState == TrialState.InProgress)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot advance to next trial: current trial is still in progress. Call CompleteTrial() or AbortTrial() first.");
                return null;
            }

            currentTrialIndex++;

            if (currentTrialIndex >= trialWorkflow.Count)
            {
                Debug.Log("[VERA Trial Workflow] No more trials in workflow.");
                currentTrialIndex = trialWorkflow.Count;
                return null;
            }

            TrialConfig trial = trialWorkflow[currentTrialIndex];

            if (trial == null)
            {
                Debug.LogError($"[VERA Trial Workflow] Trial at index {currentTrialIndex} is null! This should not happen.");
                return null;
            }

            Debug.Log($"[VERA Trial Workflow] Advanced to trial {currentTrialIndex + 1}/{trialWorkflow.Count}: {trial.label ?? "Unlabeled"}");

            return trial;
        }

        public bool StartTrial()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot start trial: workflow not initialized.");
                return false;
            }

            if (currentTrialIndex < 0 || currentTrialIndex >= trialWorkflow.Count)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot start trial: no trial is current. Call GetNextTrial() first.");
                return false;
            }

            if (currentTrialState == TrialState.InProgress)
            {
                Debug.LogWarning("[VERA Trial Workflow] Trial is already in progress.");
                return false;
            }

            // Check if we're waiting for a survey to complete
            if (waitingForSurvey)
            {
                Debug.LogWarning($"[VERA Trial Workflow] Cannot start trial: waiting for '{pendingSurveyPosition}' survey to complete. Call MarkSurveyCompleted() first.");
                return false;
            }

            TrialConfig trial = trialWorkflow[currentTrialIndex];
            if (trial == null)
            {
                Debug.LogError($"[VERA Trial Workflow] Current trial is null!");
                return false;
            }

            // Fire event before starting trial (allows for survey checks)
            OnTrialStarting?.Invoke(trial);

            // Check for standalone survey - must complete before workflow continues
            if (trial.type == "survey")
            {
                string sid = trial.instanceId ?? trial.surveyId;
                string sname = trial.surveyName ?? trial.label;
                Debug.Log($"[VERA Trial Workflow] Standalone survey '{sname}' encountered. Waiting for survey completion.");
                waitingForSurvey = true;
                pendingSurveyPosition = "standalone";
                OnSurveyRequired?.Invoke(sid, sname, "standalone");
                return false;
            }

            // Check for attached survey that should show BEFORE trial
            if (!string.IsNullOrEmpty(trial.attachedSurveyId) && trial.surveyPosition == "before")
            {
                Debug.Log($"[VERA Trial Workflow] Trial has attached survey '{trial.attachedSurveyName}' to show BEFORE trial starts.");
                waitingForSurvey = true;
                pendingSurveyPosition = "before";
                OnSurveyRequired?.Invoke(trial.attachedSurveyId, trial.attachedSurveyName, "before");
                return false; // Don't start trial yet
            }

            currentTrialState = TrialState.InProgress;
            trialStartTime = Time.time;
            trialDuration = 0f;

            Debug.Log($"[VERA Trial Workflow] Started trial {currentTrialIndex + 1}/{trialWorkflow.Count}: {trial.label ?? "Unlabeled"}");

            if (trial.conditions != null && trial.conditions.Count > 0)
            {
                string conditionsStr = string.Join(", ",
                    trial.conditions.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                Debug.Log($"[VERA Trial Workflow] Trial conditions: {conditionsStr}");
            }

            return true;
        }

        public bool CompleteTrial()
        {
            if (!isInitialized || currentTrialState != TrialState.InProgress)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot complete trial: no trial is currently in progress.");
                return false;
            }

            trialDuration = Time.time - trialStartTime;
            currentTrialState = TrialState.Completed;

            TrialConfig trial = GetCurrentTrial();
            Debug.Log($"[VERA Trial Workflow] Completed trial: {trial?.label ?? "Unknown"} (Duration: {trialDuration:F2}s)");

            // Fire event after trial completes
            OnTrialCompleted?.Invoke(trial);

            // Check for attached survey that should show AFTER trial
            if (!string.IsNullOrEmpty(trial.attachedSurveyId) && trial.surveyPosition == "after")
            {
                Debug.Log($"[VERA Trial Workflow] Trial has attached survey '{trial.attachedSurveyName}' to show AFTER trial completion.");
                waitingForSurvey = true;
                pendingSurveyPosition = "after";
                OnSurveyRequired?.Invoke(trial.attachedSurveyId, trial.attachedSurveyName, "after");

                // Don't auto-advance - waiting for survey completion
                // Developer should call MarkSurveyCompleted() when survey is done
            }
            else
            {
                // Auto-save checkpoint after trial completion (only if no survey pending)
                if (!string.IsNullOrEmpty(participantUUID))
                {
                    VERALogger.Instance.StartCoroutine(SaveCheckpoint());
                }
            }

            return true;
        }

        public bool AbortTrial(string reason = "")
        {
            if (!isInitialized || currentTrialState != TrialState.InProgress)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot abort trial: no trial is currently in progress.");
                return false;
            }

            trialDuration = Time.time - trialStartTime;
            currentTrialState = TrialState.Aborted;

            TrialConfig trial = GetCurrentTrial();
            string logMessage = $"[VERA Trial Workflow] Aborted trial: {trial?.label ?? "Unknown"} (Duration: {trialDuration:F2}s)";
            if (!string.IsNullOrEmpty(reason))
                logMessage += $" - Reason: {reason}";
            Debug.LogWarning(logMessage);

            return true;
        }

        public void ResetWorkflow()
        {
            currentTrialIndex = -1;
            currentTrialState = TrialState.NotStarted;
            trialStartTime = 0f;
            trialDuration = 0f;
            waitingForSurvey = false;
            pendingSurveyPosition = null;
            Debug.Log("[VERA Trial Workflow] Workflow reset to beginning.");
        }

        /// <summary>
        /// Cleans up resources and resets the trial workflow manager.
        /// Call this when you're done with the workflow or need to reload it.
        /// </summary>
        public void Cleanup()
        {
            Debug.Log("[VERA Trial Workflow] Cleaning up trial workflow manager...");

            // Stop any running coroutines
            StopAllCoroutines();

            // Clear workflow data
            if (trialWorkflow != null)
                trialWorkflow.Clear();

            if (allTrials != null)
                allTrials.Clear();

            if (withinGroupsForLatinSquare != null)
                withinGroupsForLatinSquare.Clear();

            if (groupMetadataCache != null)
                groupMetadataCache.Clear();

            if (trialToGroupIndexCache != null)
                trialToGroupIndexCache.Clear();

            // Reset state
            currentTrialIndex = -1;
            currentTrialState = TrialState.NotStarted;
            trialStartTime = 0f;
            trialDuration = 0f;
            isInitialized = false;
            waitingForSurvey = false;
            pendingSurveyPosition = null;
            automatedMode = false;
            waitingForTrialLogic = false;
            automatedWorkflowCoroutine = null;

            // Clear credentials
            experimentUUID = null;
            apiKey = null;
            participantUUID = null;
            participantNumber = -1;
            manualBetweenSubjectsAssignments = null;

            Debug.Log("[VERA Trial Workflow] Cleanup complete.");
        }

        #endregion


        #region TRIAL INFO ACCESSORS

        public float GetTrialElapsedTime()
        {
            if (currentTrialState == TrialState.InProgress)
                return Time.time - trialStartTime;
            return 0f;
        }

        public float GetLastTrialDuration()
        {
            return trialDuration;
        }

        public string GetConditionValue(string conditionName)
        {
            if (string.IsNullOrEmpty(conditionName))
            {
                Debug.LogWarning("[VERA Trial Workflow] GetConditionValue called with null or empty conditionName.");
                return null;
            }

            var trial = GetCurrentTrial();
            if (trial?.conditions != null && trial.conditions.TryGetValue(conditionName, out string value))
                return value;
            return null;
        }

        public string[] GetCurrentTrialWithinSubjectsIVs()
        {
            return GetCurrentTrial()?.withinSubjectsIVs;
        }

        public string[] GetCurrentTrialBetweenSubjectsIVs()
        {
            return GetCurrentTrial()?.betweenSubjectsIVs;
        }

        public string GetCurrentTrialRandomizationType()
        {
            return GetCurrentTrial()?.randomizationType;
        }

        public string GetCurrentTrialOrdering()
        {
            return GetCurrentTrial()?.trialOrdering;
        }

        public Dictionary<string, float> GetCurrentTrialDistributions()
        {
            // Per-trial distributions are not currently stored in TrialConfig
            // Return null for now; this can be extended if needed
            return null;
        }

        public string GetCurrentTrialId()
        {
            return GetCurrentTrial()?.id;
        }

        public string GetCurrentTrialType()
        {
            return GetCurrentTrial()?.type;
        }

        public Dictionary<string, string> GetCurrentTrialConditions()
        {
            return GetCurrentTrial()?.conditions;
        }

        #endregion


        #region GROUP AWARENESS

        /// <summary>
        /// Checks if the current trial belongs to a within/between-subjects group.
        /// </summary>
        public bool IsCurrentTrialInGroup()
        {
            var trial = GetCurrentTrial();
            return trial != null && !string.IsNullOrEmpty(trial.parentGroupId);
        }

        /// <summary>
        /// Gets the parent group ID for the current trial.
        /// Returns null if the trial is standalone.
        /// </summary>
        public string GetCurrentGroupId()
        {
            return GetCurrentTrial()?.parentGroupId;
        }

        /// <summary>
        /// Gets the parent group type ("within" or "between") for the current trial.
        /// Returns null if the trial is standalone.
        /// </summary>
        public string GetCurrentGroupType()
        {
            return GetCurrentTrial()?.parentGroupType;
        }

        /// <summary>
        /// Checks if the current trial is the first trial in its group.
        /// Returns false if standalone or not the first.
        /// </summary>
        public bool IsFirstTrialInGroup()
        {
            var trial = GetCurrentTrial();
            if (trial == null || string.IsNullOrEmpty(trial.parentGroupId))
                return false;

            // Check if previous trial was in a different group (or no previous)
            if (currentTrialIndex == 0)
                return true;

            var prevTrial = trialWorkflow[currentTrialIndex - 1];
            return prevTrial.parentGroupId != trial.parentGroupId;
        }

        /// <summary>
        /// Checks if the current trial is the last trial in its group.
        /// Returns false if standalone or not the last.
        /// </summary>
        public bool IsLastTrialInGroup()
        {
            var trial = GetCurrentTrial();
            if (trial == null || string.IsNullOrEmpty(trial.parentGroupId))
                return false;

            // Check if next trial is in a different group (or no next)
            if (currentTrialIndex >= trialWorkflow.Count - 1)
                return true;

            var nextTrial = trialWorkflow[currentTrialIndex + 1];
            return nextTrial.parentGroupId != trial.parentGroupId;
        }

        /// <summary>
        /// Gets the trial's position within its group (1-indexed).
        /// Returns 0 if standalone.
        /// </summary>
        public int GetTrialPositionInGroup()
        {
            var trial = GetCurrentTrial();
            if (trial == null || string.IsNullOrEmpty(trial.parentGroupId))
                return 0;

            // Use cache if available
            string cacheKey = $"{trial.parentGroupId}_{currentTrialIndex}";
            if (trialToGroupIndexCache.TryGetValue(cacheKey, out int cachedPosition))
            {
                return cachedPosition + 1; // Convert from 0-indexed to 1-indexed
            }

            // Fallback to iteration if cache miss
            int position = 1;
            for (int i = currentTrialIndex - 1; i >= 0; i--)
            {
                if (i >= 0 && i < trialWorkflow.Count && trialWorkflow[i].parentGroupId == trial.parentGroupId)
                    position++;
                else
                    break;
            }
            return position;
        }

        /// <summary>
        /// Gets the total number of trials in the current trial's group.
        /// Returns 0 if standalone.
        /// </summary>
        public int GetGroupTrialCount()
        {
            var trial = GetCurrentTrial();
            if (trial == null || string.IsNullOrEmpty(trial.parentGroupId))
                return 0;

            // Use cache if available
            if (groupMetadataCache.TryGetValue(trial.parentGroupId, out GroupMetadata metadata))
            {
                return metadata.totalTrials;
            }

            // Fallback to iteration if cache miss
            int count = 0;
            foreach (var t in trialWorkflow)
            {
                if (t != null && t.parentGroupId == trial.parentGroupId)
                    count++;
            }
            return count;
        }

        #endregion


        #region SURVEY HANDLING

        /// <summary>
        /// Survey Handling in Trial Workflow
        ///
        /// The workflow manager handles two kinds of surveys:
        ///
        /// 1. STANDALONE SURVEYS (type == "survey"):
        ///    These are workflow items that ARE surveys. When encountered, the workflow pauses
        ///    and fires OnSurveyRequired with position "standalone". The participant must
        ///    complete the survey before the workflow continues.
        ///
        /// 2. ATTACHED SURVEYS:
        ///    These are surveys attached to a trial, configured to show "before" or "after" the trial.
        ///    The workflow pauses and fires OnSurveyRequired with the appropriate position.
        ///
        /// Workflow with surveys:
        /// 1. Call StartNextTrial() or GetNextTrial() + StartTrial()
        /// 2. If standalone survey or "before" survey: OnSurveyRequired event fires, returns null/false
        /// 3. Show survey to participant using the surveyId from the event
        /// 4. When survey completes, call MarkSurveyCompleted()
        /// 5. Call StartNextTrial() again to continue (standalone surveys advance; "before" surveys start the trial)
        /// 6. Run your trial logic
        /// 7. Call CompleteTrial()
        /// 8. If trial has "after" survey: OnSurveyRequired event fires
        /// 9. Show survey to participant
        /// 10. When survey completes, call MarkSurveyCompleted()
        /// 11. Continue to next trial
        ///
        /// Example usage:
        /// <code>
        /// // Subscribe to survey event - handles standalone, before, and after surveys
        /// trialManager.OnSurveyRequired += (surveyId, surveyName, position) => {
        ///     Debug.Log($"Survey required: {surveyName} ({position})");
        ///     StartCoroutine(ShowSurvey(surveyId, surveyName));
        /// };
        ///
        /// // Start trial
        /// var trial = trialManager.StartNextTrial();
        /// if (trial == null && trialManager.IsWaitingForSurvey()) {
        ///     // Survey is being shown, wait for it to complete
        ///     return;
        /// }
        ///
        /// // In your survey completion handler:
        /// trialManager.MarkSurveyCompleted();
        /// if (trialManager.GetPendingSurveyPosition() == null) {
        ///     // No more surveys, continue workflow
        ///     var nextTrial = trialManager.StartNextTrial();
        /// }
        /// </code>
        /// </summary>

        /// <summary>
        /// Checks if the current workflow item is a standalone survey.
        /// </summary>
        public bool IsCurrentItemSurvey()
        {
            var trial = GetCurrentTrial();
            return trial != null && trial.type == "survey";
        }

        /// <summary>
        /// Checks if the current trial has an attached survey.
        /// </summary>
        public bool CurrentTrialHasSurvey()
        {
            var trial = GetCurrentTrial();
            return trial != null && !string.IsNullOrEmpty(trial.attachedSurveyId);
        }

        /// <summary>
        /// Checks if a survey should be shown BEFORE the current trial starts.
        /// </summary>
        public bool ShouldShowSurveyBefore()
        {
            var trial = GetCurrentTrial();
            if (trial == null || string.IsNullOrEmpty(trial.attachedSurveyId))
                return false;
            return trial.surveyPosition == "before";
        }

        /// <summary>
        /// Checks if a survey should be shown AFTER the current trial completes.
        /// </summary>
        public bool ShouldShowSurveyAfter()
        {
            var trial = GetCurrentTrial();
            if (trial == null || string.IsNullOrEmpty(trial.attachedSurveyId))
                return false;
            return trial.surveyPosition == "after";
        }

        /// <summary>
        /// Gets the survey ID for the current trial (attached or standalone).
        /// Returns null if no survey is associated.
        /// </summary>
        public string GetCurrentSurveyId()
        {
            var trial = GetCurrentTrial();
            if (trial == null)
                return null;

            // For standalone surveys, use the surveyId field
            if (trial.type == "survey")
                return trial.surveyId;

            // For attached surveys
            return trial.attachedSurveyId;
        }

        /// <summary>
        /// Gets the survey name for the current trial.
        /// </summary>
        public string GetCurrentSurveyName()
        {
            var trial = GetCurrentTrial();
            if (trial == null)
                return null;

            // For standalone surveys, use the label
            if (trial.type == "survey")
                return trial.label;

            return trial.attachedSurveyName;
        }

        /// <summary>
        /// Marks an attached survey as completed. Call this after a survey finishes
        /// to allow the workflow to continue.
        /// </summary>
        public void MarkSurveyCompleted()
        {
            if (!waitingForSurvey)
            {
                Debug.LogWarning("[VERA Trial Workflow] MarkSurveyCompleted called but no survey was pending.");
                return;
            }

            Debug.Log($"[VERA Trial Workflow] Survey marked as completed (position: {pendingSurveyPosition})");
            waitingForSurvey = false;
            pendingSurveyPosition = null;

            // Auto-save checkpoint after survey completion
            if (!string.IsNullOrEmpty(participantUUID))
            {
                VERALogger.Instance.StartCoroutine(SaveCheckpoint());
            }
        }

        /// <summary>
        /// Checks if the workflow is currently waiting for a survey to complete.
        /// </summary>
        public bool IsWaitingForSurvey()
        {
            return waitingForSurvey;
        }

        /// <summary>
        /// Gets the position of the pending survey ("before" or "after"), or null if no survey is pending.
        /// </summary>
        public string GetPendingSurveyPosition()
        {
            return pendingSurveyPosition;
        }

        /// <summary>
        /// Gets the attached survey info for the current trial, if any.
        /// Returns null if no attached survey.
        /// </summary>
        public (string surveyId, string surveyName, string position)? GetCurrentAttachedSurvey()
        {
            var trial = GetCurrentTrial();
            if (trial == null || string.IsNullOrEmpty(trial.attachedSurveyId))
                return null;

            return (trial.attachedSurveyId, trial.attachedSurveyName, trial.surveyPosition);
        }

        #endregion


        #region TRIAL ORDERING

        /// <summary>
        /// Randomizes the entire trial workflow using Fisher-Yates shuffle.
        /// Must be called before starting any trials.
        /// Note: For standard randomization, trials typically come pre-randomized from the VERA API.
        /// </summary>
        public void RandomizeWorkflow()
        {
            if (currentTrialIndex >= 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot randomize: trials have already started.");
                return;
            }

            if (!isInitialized)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot randomize: workflow not initialized.");
                return;
            }

            if (trialWorkflow == null || trialWorkflow.Count <= 1)
            {
                Debug.Log("[VERA Trial Workflow] Workflow has 0-1 trials, no randomization needed.");
                return;
            }

            System.Random rng = new System.Random();
            int n = trialWorkflow.Count;

            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                TrialConfig temp = trialWorkflow[k];
                trialWorkflow[k] = trialWorkflow[n];
                trialWorkflow[n] = temp;
            }

            Debug.Log("[VERA Trial Workflow] Workflow randomized using Fisher-Yates shuffle.");

            // Rebuild cache after randomization
            BuildGroupMetadataCache();
        }

        public void RandomizeWithSeed(int seed)
        {
            if (currentTrialIndex >= 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot randomize: trials have already started.");
                return;
            }

            if (!isInitialized)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot randomize: workflow not initialized.");
                return;
            }

            if (trialWorkflow == null || trialWorkflow.Count <= 1)
            {
                Debug.Log("[VERA Trial Workflow] Workflow has 0-1 trials, no randomization needed.");
                return;
            }

            System.Random rng = new System.Random(seed);
            int n = trialWorkflow.Count;

            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                TrialConfig temp = trialWorkflow[k];
                trialWorkflow[k] = trialWorkflow[n];
                trialWorkflow[n] = temp;
            }

            Debug.Log($"[VERA Trial Workflow] Workflow randomized with seed {seed} using Fisher-Yates shuffle.");

            // Rebuild cache after randomization
            BuildGroupMetadataCache();
        }

        public void RandomizeWithinBlocks(int blockSize)
        {
            if (currentTrialIndex >= 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot randomize: trials have already started.");
                return;
            }

            if (!isInitialized)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot randomize: workflow not initialized.");
                return;
            }

            if (trialWorkflow == null || trialWorkflow.Count <= 1)
            {
                Debug.Log("[VERA Trial Workflow] Workflow has 0-1 trials, no randomization needed.");
                return;
            }

            if (blockSize <= 0)
            {
                Debug.LogWarning($"[VERA Trial Workflow] Invalid block size {blockSize}. Using default randomization.");
                RandomizeWorkflow();
                return;
            }

            System.Random rng = new System.Random();
            int totalTrials = trialWorkflow.Count;

            // Randomize within each block
            for (int blockStart = 0; blockStart < totalTrials; blockStart += blockSize)
            {
                int blockEnd = Mathf.Min(blockStart + blockSize, totalTrials);
                int blockLength = blockEnd - blockStart;

                // Fisher-Yates shuffle within this block
                for (int i = blockLength - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    int indexA = blockStart + i;
                    int indexB = blockStart + j;

                    TrialConfig temp = trialWorkflow[indexA];
                    trialWorkflow[indexA] = trialWorkflow[indexB];
                    trialWorkflow[indexB] = temp;
                }
            }

            Debug.Log($"[VERA Trial Workflow] Workflow randomized within blocks of size {blockSize}.");

            // Rebuild cache after randomization
            BuildGroupMetadataCache();
        }

        /// <summary>
        /// Applies Latin Square counterbalancing using the participant number provided during Initialize().
        /// Automatically uses the stored participant number from initialization.
        /// Only affects trials that belong to within-subjects groups marked for Latin square ordering.
        /// </summary>
        public void ApplyLatinSquareOrdering()
        {
            if (participantNumber < 0)
            {
                Debug.LogError("[VERA Trial Workflow] Cannot apply Latin square: no participant number was provided during Initialize(). Use ApplyLatinSquareOrdering(participantNumber) instead.");
                return;
            }

            ApplyLatinSquareOrdering(participantNumber);
        }

        /// <summary>
        /// Applies Latin Square counterbalancing to within-subjects groups in the workflow.
        /// Only affects trials that belong to within-subjects groups marked for Latin square ordering.
        /// Trials outside of within-subjects groups (standalone, between-subjects) keep their order.
        ///
        /// Example with within-group of 3 conditions (A, B, C):
        ///   Participant 0: A, B, C
        ///   Participant 1: B, C, A
        ///   Participant 2: C, A, B
        ///   Participant 3: A, B, C (cycles back)
        ///
        /// IMPORTANT: For complete counterbalancing, you need at least N participants for N conditions.
        /// The system will warn if participant assignment may result in incomplete counterbalancing.
        /// </summary>
        /// <param name="participantNumber">The participant's sequential number (0-indexed).
        /// Use VERALogger.Instance.activeParticipant.participantShortId or your own counter.</param>
        public void ApplyLatinSquareOrdering(int participantNumber)
        {
            if (currentTrialIndex >= 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot apply Latin square: trials have already started.");
                return;
            }

            if (!isInitialized)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot apply Latin square: workflow not initialized.");
                return;
            }

            if (trialWorkflow == null || trialWorkflow.Count == 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot apply Latin square: no trials in workflow.");
                return;
            }

            if (participantNumber < 0)
            {
                Debug.LogWarning($"[VERA Trial Workflow] Invalid participant number ({participantNumber}). Using 0 as fallback.");
                participantNumber = 0;
            }

            // Validate counterbalancing requirements
            ValidateLatinSquareCounterbalancing(participantNumber);

            // If no within-groups need Latin square, apply to entire workflow (legacy behavior)
            if (withinGroupsForLatinSquare == null || withinGroupsForLatinSquare.Count == 0)
            {
                Debug.Log("[VERA Trial Workflow] No within-subjects groups require Latin square. Applying to entire workflow (legacy mode).");
                ApplyLatinSquareToEntireWorkflow(participantNumber);
                return;
            }

            // Apply Latin square to each within-subjects group separately
            foreach (var group in withinGroupsForLatinSquare)
            {
                string groupId = group.Key;
                List<int> indices = group.Value;

                if (indices == null || indices.Count <= 1)
                {
                    Debug.Log($"[VERA Trial Workflow] Skipping Latin square for group '{groupId}': too few trials ({indices?.Count ?? 0}).");
                    continue;
                }

                // Validate all indices are within bounds
                bool invalidIndices = false;
                foreach (int idx in indices)
                {
                    if (idx < 0 || idx >= trialWorkflow.Count)
                    {
                        Debug.LogError($"[VERA Trial Workflow] Invalid trial index {idx} in group '{groupId}'. Workflow has {trialWorkflow.Count} trials.");
                        invalidIndices = true;
                        break;
                    }
                }

                if (invalidIndices)
                {
                    Debug.LogError($"[VERA Trial Workflow] Skipping Latin square for group '{groupId}' due to invalid indices.");
                    continue;
                }

                int n = indices.Count;
                int offset = participantNumber % n;

                // Extract trials at these indices
                List<TrialConfig> groupTrials = new List<TrialConfig>();
                foreach (int idx in indices)
                {
                    if (trialWorkflow[idx] == null)
                    {
                        Debug.LogError($"[VERA Trial Workflow] Trial at index {idx} is null in group '{groupId}'.");
                        invalidIndices = true;
                        break;
                    }
                    groupTrials.Add(trialWorkflow[idx]);
                }

                if (invalidIndices)
                {
                    Debug.LogError($"[VERA Trial Workflow] Skipping Latin square for group '{groupId}' due to null trials.");
                    continue;
                }

                // Reorder using Latin square
                for (int i = 0; i < n; i++)
                {
                    int sourceIndex = (i + offset) % n;
                    trialWorkflow[indices[i]] = groupTrials[sourceIndex];
                }

                Debug.Log($"[VERA Trial Workflow] Applied Latin square to within-group '{groupId}' for participant {participantNumber} (row {offset} of {n}).");
            }

            // Rebuild cache after reordering
            BuildGroupMetadataCache();
        }

        /// <summary>
        /// Applies Latin square to the entire workflow (for experiments without explicit within-groups).
        /// </summary>
        private void ApplyLatinSquareToEntireWorkflow(int participantNumber)
        {
            if (trialWorkflow == null || trialWorkflow.Count == 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot apply Latin square to entire workflow: workflow is empty.");
                return;
            }

            if (participantNumber < 0)
            {
                Debug.LogWarning($"[VERA Trial Workflow] Invalid participant number ({participantNumber}). Using 0.");
                participantNumber = 0;
            }

            int n = trialWorkflow.Count;
            int offset = participantNumber % n;

            List<TrialConfig> latinSquareOrder = new List<TrialConfig>();
            for (int i = 0; i < n; i++)
            {
                int index = (i + offset) % n;
                if (trialWorkflow[index] == null)
                {
                    Debug.LogError($"[VERA Trial Workflow] Trial at index {index} is null. Cannot apply Latin square.");
                    return;
                }
                latinSquareOrder.Add(trialWorkflow[index]);
            }

            trialWorkflow = latinSquareOrder;
            Debug.Log($"[VERA Trial Workflow] Applied Latin square to entire workflow for participant {participantNumber} (row {offset} of {n}).");

            // Rebuild cache after reordering
            BuildGroupMetadataCache();
        }

        /// <summary>
        /// Gets the within-subjects groups that require Latin square ordering.
        /// Returns group IDs and the number of trials in each group.
        /// </summary>
        public Dictionary<string, int> GetWithinGroupsForLatinSquare()
        {
            var result = new Dictionary<string, int>();
            foreach (var group in withinGroupsForLatinSquare)
            {
                result[group.Key] = group.Value.Count;
            }
            return result;
        }

        /// <summary>
        /// Gets the current trial ordering as a string for debugging/logging.
        /// </summary>
        public string GetTrialOrderingDebugString()
        {
            if (trialWorkflow.Count == 0)
                return "No trials";

            var labels = new List<string>();
            for (int i = 0; i < trialWorkflow.Count; i++)
            {
                string label = trialWorkflow[i].label ?? $"Trial {i}";
                labels.Add(label);
            }
            return string.Join(" -> ", labels);
        }

        #endregion


        #region WORKFLOW AUTOMATION

        /// <summary>
        /// Whether the workflow is currently running in automated mode.
        /// </summary>
        public bool IsAutomatedMode => automatedMode;

        /// <summary>
        /// Whether the automated workflow is waiting for the developer to call CompleteAutomatedTrial().
        /// </summary>
        public bool IsWaitingForTrialLogic => waitingForTrialLogic;

        /// <summary>
        /// Starts the automated workflow coroutine. The workflow will progress through all trials
        /// automatically, pausing for surveys (OnSurveyRequired) and trial logic (OnTrialReady).
        ///
        /// Flow for each workflow item:
        ///   1. Standalone survey → fires OnSurveyRequired, waits for MarkSurveyCompleted()
        ///   2. Trial with "before" survey → fires OnSurveyRequired, waits for MarkSurveyCompleted()
        ///   3. Trial starts → fires OnTrialReady with the TrialConfig (conditions already set)
        ///   4. Developer runs trial logic, then calls CompleteAutomatedTrial()
        ///   5. Trial with "after" survey → fires OnSurveyRequired, waits for MarkSurveyCompleted()
        ///   6. Checkpoint saved, advance to next item
        ///   7. When all items are done → fires OnWorkflowCompleted
        ///
        /// Usage:
        /// <code>
        /// var workflow = VERALogger.Instance.trialWorkflow;
        /// workflow.OnTrialReady += (trial) => {
        ///     // trial.conditions are already set; run your trial logic
        ///     StartCoroutine(RunMyTrialLogic(trial));
        /// };
        /// workflow.OnSurveyRequired += (surveyId, surveyName, position) => {
        ///     StartCoroutine(ShowSurveyUI(surveyId, surveyName, () => workflow.MarkSurveyCompleted()));
        /// };
        /// workflow.OnWorkflowCompleted += () => {
        ///     Debug.Log("All trials done!");
        /// };
        /// workflow.StartAutomatedWorkflow();
        /// </code>
        /// </summary>
        public void StartAutomatedWorkflow()
        {
            if (!isInitialized)
            {
                Debug.LogError("[VERA Trial Workflow] Cannot start automated workflow: not initialized.");
                return;
            }

            if (automatedMode)
            {
                Debug.LogWarning("[VERA Trial Workflow] Automated workflow is already running.");
                return;
            }

            if (trialWorkflow.Count == 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] No trials in workflow. Nothing to automate.");
                OnWorkflowCompleted?.Invoke();
                return;
            }

            automatedMode = true;
            Debug.Log($"[VERA Trial Workflow] Starting automated workflow with {trialWorkflow.Count} items.");
            automatedWorkflowCoroutine = StartCoroutine(RunAutomatedWorkflow());
        }

        /// <summary>
        /// Stops the automated workflow. The current trial remains in its current state.
        /// You can resume manual control after stopping.
        /// </summary>
        public void StopAutomatedWorkflow()
        {
            if (!automatedMode)
            {
                Debug.LogWarning("[VERA Trial Workflow] Automated workflow is not running.");
                return;
            }

            if (automatedWorkflowCoroutine != null)
            {
                StopCoroutine(automatedWorkflowCoroutine);
                automatedWorkflowCoroutine = null;
            }

            automatedMode = false;
            waitingForTrialLogic = false;
            Debug.Log("[VERA Trial Workflow] Automated workflow stopped.");
        }

        /// <summary>
        /// Call this from your trial logic to signal that the current trial is done.
        /// Only used in automated mode. This completes the trial and lets the workflow advance.
        /// </summary>
        public void CompleteAutomatedTrial()
        {
            if (!automatedMode)
            {
                Debug.LogWarning("[VERA Trial Workflow] CompleteAutomatedTrial called but automated mode is not active. Use CompleteTrial() for manual mode.");
                return;
            }

            if (!waitingForTrialLogic)
            {
                Debug.LogWarning("[VERA Trial Workflow] CompleteAutomatedTrial called but no trial is waiting for completion.");
                return;
            }

            // Complete the trial through the normal path
            CompleteTrial();
            waitingForTrialLogic = false;
        }

        /// <summary>
        /// The main automation coroutine that drives the entire trial workflow.
        /// </summary>
        private IEnumerator RunAutomatedWorkflow()
        {
            Debug.Log("[VERA Trial Workflow] Automated workflow coroutine started.");

            while (automatedMode)
            {
                // Check if there are more trials
                if (!HasMoreTrials && currentTrialState != TrialState.InProgress)
                {
                    // If we're past the last trial, we're done
                    if (currentTrialIndex >= trialWorkflow.Count - 1 && currentTrialState != TrialState.NotStarted)
                    {
                        break;
                    }
                    // Edge case: currentTrialIndex is at the end with no more
                    if (currentTrialIndex >= trialWorkflow.Count)
                    {
                        break;
                    }
                }

                // If waiting for a survey, poll until it's completed
                if (waitingForSurvey)
                {
                    yield return null;
                    continue;
                }

                // If waiting for developer trial logic, poll until they call CompleteAutomatedTrial()
                if (waitingForTrialLogic)
                {
                    yield return null;
                    continue;
                }

                // Try to start the next trial
                TrialConfig trial = StartNextTrial();

                if (trial == null)
                {
                    // StartNextTrial returned null - either waiting for survey, or no more trials
                    if (waitingForSurvey)
                    {
                        // A survey was triggered (standalone or "before"), wait for it
                        yield return null;
                        continue;
                    }

                    // No more trials
                    break;
                }

                // Trial started successfully - notify developer and wait for their logic
                waitingForTrialLogic = true;
                OnTrialReady?.Invoke(trial);

                // Wait for developer to call CompleteAutomatedTrial()
                while (waitingForTrialLogic && automatedMode)
                {
                    yield return null;
                }

                if (!automatedMode)
                    break;

                // After trial completion, CompleteTrial() may have triggered an "after" survey
                // Wait for that survey to complete before advancing
                while (waitingForSurvey && automatedMode)
                {
                    yield return null;
                }

                if (!automatedMode)
                    break;

                // Small yield to prevent tight loops
                yield return null;
            }

            automatedMode = false;
            waitingForTrialLogic = false;
            automatedWorkflowCoroutine = null;
            Debug.Log("[VERA Trial Workflow] Automated workflow completed.");
            OnWorkflowCompleted?.Invoke();
        }

        #endregion


        #region INTERNAL API

        private IEnumerator FetchTrialWorkflow()
        {
            string host = VERAHost.hostUrl;
            string url = $"{host}/api/experiments/{experimentUUID}/trials/execution-order";

            UnityWebRequest request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            bool requestComplete = false;
            VERAWebRequestResult result = null;

            yield return ExecuteWebRequestWithRetry(request, (r) =>
            {
                result = r;
                requestComplete = true;
            });

            // Wait for callback
            while (!requestComplete)
                yield return null;

            request.Dispose();

            if (result != null && result.success)
            {
                try
                {
                    string jsonResponse = result.jsonResponse;
                    if (string.IsNullOrEmpty(jsonResponse))
                    {
                        Debug.LogError("[VERA Trial Workflow] Empty response from server.");
                        yield break;
                    }

                    JToken parsed = JToken.Parse(jsonResponse);

                    JArray trialsArray;
                    if (parsed is JArray arr)
                        trialsArray = arr;
                    else if (parsed is JObject obj && obj["trials"] is JArray arr2)
                        trialsArray = arr2;
                    else if (parsed is JObject obj2 && obj2["executionOrder"] is JArray arr3)
                        trialsArray = arr3;
                    else
                    {
                        Debug.LogError($"[VERA Trial Workflow] Unexpected response format from server. Received: {jsonResponse.Substring(0, Mathf.Min(200, jsonResponse.Length))}...");
                        yield break;
                    }

                    if (trialsArray != null && trialsArray.Count > 0)
                    {
                        TrialConfig[] topLevelTrials = ParseTrials(trialsArray);
                        if (topLevelTrials == null || topLevelTrials.Length == 0)
                        {
                            Debug.LogError("[VERA Trial Workflow] Failed to parse trials from response.");
                            yield break;
                        }

                        allTrials = new List<TrialConfig>(topLevelTrials);
                        trialWorkflow = FlattenTrialHierarchy(topLevelTrials);
                        ApplyAutomaticOrdering(topLevelTrials);

                        // Build performance cache
                        BuildGroupMetadataCache();

                        isInitialized = true;
                        Debug.Log($"[VERA Trial Workflow] Successfully loaded {trialWorkflow.Count} executable trials from {topLevelTrials.Length} top-level items.");
                    }
                    else
                    {
                        Debug.LogWarning("[VERA Trial Workflow] No trials found in workflow.");
                        isInitialized = true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[VERA Trial Workflow] Failed to parse response: {e.Message}\nStack trace: {e.StackTrace}");
                }
            }
            else
            {
                Debug.LogError($"[VERA Trial Workflow] Failed to fetch workflow: {result?.error ?? "Unknown error"}");
            }
        }

        private Survey ParseSurveyData(JObject surveyObj)
        {
            if (surveyObj == null)
                return null;

            var survey = new Survey
            {
                _id = surveyObj.Value<string>("_id"),
                surveyName = surveyObj.Value<string>("surveyName"),
                shortSurveyName = surveyObj.Value<string>("shortSurveyName"),
                surveyDescription = surveyObj.Value<string>("surveyDescription"),
                surveyEndStatement = surveyObj.Value<string>("surveyEndStatement"),
                createdBy = surveyObj.Value<string>("createdBy"),
                experimentId = surveyObj.Value<string>("experimentId"),
                isTemplate = surveyObj.Value<bool>("isTemplate")
            };

            // Parse tags
            if (surveyObj["tags"] is JArray tagsArray)
            {
                survey.tags = new List<string>();
                foreach (var tag in tagsArray)
                    survey.tags.Add(tag.ToString());
            }

            // Parse citations
            if (surveyObj["citations"] is JArray citationsArray)
            {
                survey.citations = new List<SurveyCitation>();
                foreach (var citToken in citationsArray)
                {
                    if (citToken is JObject citObj)
                    {
                        survey.citations.Add(new SurveyCitation
                        {
                            _id = citObj.Value<string>("_id"),
                            title = citObj.Value<string>("title"),
                            fullCitation = citObj.Value<string>("fullCitation")
                        });
                    }
                }
            }

            // Parse questions
            if (surveyObj["questions"] is JArray questionsArray)
            {
                survey.questions = new List<SurveyQuestion>();
                foreach (var qToken in questionsArray)
                {
                    if (qToken is JObject qObj)
                    {
                        var question = new SurveyQuestion
                        {
                            _id = qObj.Value<string>("_id"),
                            surveyParent = qObj.Value<string>("surveyParent"),
                            questionNumberInSurvey = qObj.Value<int>("questionNumberInSurvey"),
                            questionText = qObj.Value<string>("questionText"),
                            questionType = qObj.Value<string>("questionType"),
                            leftSliderText = qObj.Value<string>("leftSliderText"),
                            rightSliderText = qObj.Value<string>("rightSliderText")
                        };

                        // Parse question options
                        if (qObj["questionOptions"] is JArray optionsArray)
                        {
                            question.questionOptions = new List<string>();
                            foreach (var opt in optionsArray)
                                question.questionOptions.Add(opt.ToString());
                        }

                        // Parse matrix column names
                        if (qObj["matrixColumnNames"] is JArray matrixArray)
                        {
                            question.matrixColumnNames = new List<string>();
                            foreach (var col in matrixArray)
                                question.matrixColumnNames.Add(col.ToString());
                        }

                        survey.questions.Add(question);
                    }
                }
            }

            return survey;
        }

        private TrialConfig[] ParseTrials(JArray trialsArray)
        {
            List<TrialConfig> trials = new List<TrialConfig>();

            if (trialsArray == null || trialsArray.Count == 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] ParseTrials received empty or null array.");
                return new TrialConfig[0];
            }

            foreach (JToken trialToken in trialsArray)
            {
                try
                {
                    if (trialToken == null)
                    {
                        Debug.LogWarning("[VERA Trial Workflow] Skipping null trial token.");
                        continue;
                    }

                    TrialConfig trial = new TrialConfig
                    {
                        id = trialToken.Value<string>("id"),
                        type = trialToken.Value<string>("type"),
                        label = trialToken.Value<string>("label"),
                        description = trialToken.Value<string>("description"),
                        order = trialToken.Value<int>("order"),
                        repetitionCount = trialToken.Value<int>("repetitionCount"),
                        randomizationType = trialToken.Value<string>("randomizationType"),
                        trialOrdering = trialToken.Value<string>("trialOrdering"),
                        randomizationLevel = trialToken.Value<string>("randomizationLevel"),
                        isRandomized = trialToken.Value<bool>("isRandomized"),
                        // For standalone surveys
                        surveyId = trialToken.Value<string>("surveyId"),
                        surveyName = trialToken.Value<string>("surveyName"),
                        instanceId = trialToken.Value<string>("instanceId")
                    };

                    // Parse attachedSurvey object (new format)
                    if (trialToken["attachedSurvey"] is JObject attachedSurveyObj)
                    {
                        trial.attachedSurveyId = attachedSurveyObj.Value<string>("instanceId");
                        trial.attachedSurveyName = attachedSurveyObj.Value<string>("surveyName");
                        trial.surveyPosition = attachedSurveyObj.Value<string>("position");
                    }
                    // Fallback to old format for backward compatibility
                    else
                    {
                        trial.attachedSurveyId = trialToken.Value<string>("attachedSurveyId");
                        trial.attachedSurveyName = trialToken.Value<string>("attachedSurveyName");
                        trial.surveyPosition = trialToken.Value<string>("surveyPosition");
                    }

                    if (trial.repetitionCount <= 0)
                        trial.repetitionCount = 1;

                    if (trialToken["conditions"] is JObject conditionsObj)
                    {
                        trial.conditions = new Dictionary<string, string>();
                        foreach (var prop in conditionsObj.Properties())
                            trial.conditions[prop.Name] = prop.Value?.ToString() ?? "";
                    }

                    if (trialToken["withinSubjectsIVs"] is JArray withinIVs)
                    {
                        List<string> ivList = new List<string>();
                        foreach (var iv in withinIVs)
                            ivList.Add(iv.ToString());
                        trial.withinSubjectsIVs = ivList.ToArray();
                    }

                    if (trialToken["betweenSubjectsIVs"] is JArray betweenIVs)
                    {
                        List<string> ivList = new List<string>();
                        foreach (var iv in betweenIVs)
                            ivList.Add(iv.ToString());
                        trial.betweenSubjectsIVs = ivList.ToArray();
                    }

                    if (trialToken["perTrialRepetitions"] is JObject perTrialReps)
                    {
                        trial.perTrialRepetitions = new Dictionary<string, int>();
                        foreach (var prop in perTrialReps.Properties())
                            trial.perTrialRepetitions[prop.Name] = prop.Value?.Value<int>() ?? 1;
                    }

                    if (trialToken["childTrials"] is JArray childTrialsArray && childTrialsArray.Count > 0)
                        trial.childTrials = ParseTrials(childTrialsArray);

                    // Parse survey data for standalone surveys
                    if (trialToken["survey"] is JObject surveyObj)
                    {
                        trial.survey = ParseSurveyData(surveyObj);
                    }

                    // Parse survey instance data
                    if (trialToken["surveyInstanceData"] is JObject instanceObj)
                    {
                        trial.surveyInstanceData = new SurveyInstanceData
                        {
                            instanceId = instanceObj.Value<string>("instanceId"),
                            experimentId = instanceObj.Value<string>("experimentId"),
                            activated = instanceObj.Value<bool>("activated")
                        };
                    }

                    // Validate trial configuration AFTER parsing all fields including childTrials
                    if (!ValidateTrialConfig(trial, out string validationError))
                    {
                        Debug.LogError($"[VERA Trial Workflow] Invalid trial configuration: {validationError}. Skipping trial.");
                        continue;
                    }

                    trials.Add(trial);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[VERA Trial Workflow] Error parsing trial: {e.Message}. Skipping trial.");
                }
            }

            return trials.ToArray();
        }

        private List<TrialConfig> FlattenTrialHierarchy(TrialConfig[] topLevelTrials)
        {
            List<TrialConfig> flattenedTrials = new List<TrialConfig>();
            withinGroupsForLatinSquare.Clear();

            if (topLevelTrials == null || topLevelTrials.Length == 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] FlattenTrialHierarchy received empty or null trials array.");
                return flattenedTrials;
            }

            foreach (TrialConfig trial in topLevelTrials)
            {
                if (trial == null)
                {
                    Debug.LogWarning("[VERA Trial Workflow] Skipping null trial in top-level trials.");
                    continue;
                }

                // Skip non-executable workflow items (handled by web portal)
                if (trial.type == "consent" || trial.type == "entry" || trial.type == "completion")
                    continue;

                if (trial.type == "standalone" || trial.type == "survey")
                {
                    // Standalone trials and standalone surveys - no parent group
                    int reps = trial.repetitionCount > 0 ? trial.repetitionCount : 1;
                    for (int i = 0; i < reps; i++)
                        flattenedTrials.Add(trial);
                }
                else if (trial.type == "within" || trial.type == "between")
                {
                    if (trial.childTrials != null && trial.childTrials.Length > 0)
                    {
                        // Check if this group requires Latin square
                        bool requiresLatinSquare = false;
                        if (!string.IsNullOrEmpty(trial.trialOrdering))
                        {
                            string ordering = trial.trialOrdering.ToLower();
                            requiresLatinSquare = ordering.Contains("latin") || ordering == "counterbalanced";
                        }
                        if (!string.IsNullOrEmpty(trial.randomizationType) && trial.randomizationType.ToLower().Contains("latin"))
                        {
                            requiresLatinSquare = true;
                        }

                        // For between-subjects: filter to one condition based on participant number or manual assignment
                        TrialConfig[] trialsToInclude = trial.childTrials;
                        if (trial.type == "between" && participantNumber >= 0)
                        {
                            // Check for manual assignment for this specific group
                            int conditionIndex = -1;
                            if (manualBetweenSubjectsAssignments != null && manualBetweenSubjectsAssignments.TryGetValue(trial.id, out int manualIndex))
                            {
                                conditionIndex = manualIndex;
                                Debug.Log($"[VERA Trial Workflow] Between-subjects group '{trial.label}': Using manual condition assignment (index {conditionIndex}).");
                            }

                            trialsToInclude = FilterBetweenSubjectsCondition(trial.childTrials, participantNumber, conditionIndex);
                            if (trialsToInclude.Length > 0)
                            {
                                Debug.Log($"[VERA Trial Workflow] Between-subjects group '{trial.label}': Participant assigned to condition with {trialsToInclude.Length} trials.");
                            }
                        }

                        // Track the group for Latin square ordering (within-subjects only)
                        if (requiresLatinSquare && trial.type == "within")
                        {
                            withinGroupsForLatinSquare.Add(trial.id, new List<int>());
                        }

                        foreach (TrialConfig childTrial in trialsToInclude)
                        {
                            if (childTrial == null)
                            {
                                Debug.LogWarning($"[VERA Trial Workflow] Skipping null child trial in group '{trial.id}'.");
                                continue;
                            }

                            // Set parent group info on child
                            childTrial.parentGroupId = trial.id;
                            childTrial.parentGroupType = trial.type;
                            childTrial.parentRequiresLatinSquare = requiresLatinSquare;

                            int reps = childTrial.repetitionCount > 0 ? childTrial.repetitionCount : 1;
                            if (reps > 100)
                            {
                                Debug.LogWarning($"[VERA Trial Workflow] Trial '{childTrial.id}' has unusually high repetition count ({reps}). Capping at 100.");
                                reps = 100;
                            }

                            for (int i = 0; i < reps; i++)
                            {
                                // Track indices for Latin square groups
                                if (requiresLatinSquare && trial.type == "within")
                                {
                                    withinGroupsForLatinSquare[trial.id].Add(flattenedTrials.Count);
                                }
                                flattenedTrials.Add(childTrial);
                            }
                        }
                    }
                }
            }

            return flattenedTrials;
        }

        // Tracks which trial indices belong to which within-subjects group (for Latin square)
        private Dictionary<string, List<int>> withinGroupsForLatinSquare = new Dictionary<string, List<int>>();

        /// <summary>
        /// Filters between-subjects trials to only include one condition based on participant number or manual assignment.
        /// Groups trials by their condition combinations and selects one group.
        /// </summary>
        /// <param name="childTrials">All child trials in the between-subjects group</param>
        /// <param name="participantNum">Participant number for auto-assignment</param>
        /// <param name="manualConditionIndex">Optional manual condition index (0-based). If >= 0, uses this instead of auto-assignment.</param>
        private TrialConfig[] FilterBetweenSubjectsCondition(TrialConfig[] childTrials, int participantNum, int manualConditionIndex = -1)
        {
            if (childTrials == null || childTrials.Length == 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] FilterBetweenSubjectsCondition received empty or null child trials.");
                return Array.Empty<TrialConfig>();
            }

            if (participantNum < 0)
            {
                Debug.LogWarning($"[VERA Trial Workflow] Invalid participant number ({participantNum}). Using 0 as fallback.");
                participantNum = 0;
            }

            // Group trials by their condition signature
            Dictionary<string, List<TrialConfig>> conditionGroups = new Dictionary<string, List<TrialConfig>>();

            foreach (var trial in childTrials)
            {
                if (trial == null)
                {
                    Debug.LogWarning("[VERA Trial Workflow] Skipping null trial in between-subjects group.");
                    continue;
                }

                // Create a signature from the trial's conditions
                string signature = GetConditionSignature(trial.conditions);

                if (!conditionGroups.ContainsKey(signature))
                {
                    conditionGroups[signature] = new List<TrialConfig>();
                }
                conditionGroups[signature].Add(trial);
            }

            if (conditionGroups.Count == 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] No valid condition groups found in between-subjects trials.");
                return Array.Empty<TrialConfig>();
            }

            // Select condition: manual override if provided, otherwise auto-balance
            int conditionIndex;
            if (manualConditionIndex >= 0 && manualConditionIndex < conditionGroups.Count)
            {
                conditionIndex = manualConditionIndex;
            }
            else
            {
                conditionIndex = participantNum % conditionGroups.Count;
            }

            // Safe access to condition groups
            var groupsList = conditionGroups.Values.ToList();
            if (conditionIndex >= groupsList.Count)
            {
                Debug.LogWarning($"[VERA Trial Workflow] Condition index {conditionIndex} out of range. Using index 0.");
                conditionIndex = 0;
            }

            var selectedGroup = groupsList[conditionIndex];

            if (selectedGroup == null || selectedGroup.Count == 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] Selected condition group is empty.");
                return Array.Empty<TrialConfig>();
            }

            return selectedGroup.ToArray();
        }

        /// <summary>
        /// Creates a consistent string signature from a conditions dictionary for grouping.
        /// </summary>
        private string GetConditionSignature(Dictionary<string, string> conditions)
        {
            if (conditions == null || conditions.Count == 0)
                return "no_conditions";

            try
            {
                // Sort keys for consistent ordering
                var sortedKeys = conditions.Keys.OrderBy(k => k).ToList();
                var parts = new List<string>();
                foreach (var key in sortedKeys)
                {
                    if (key != null && conditions.TryGetValue(key, out string value))
                    {
                        parts.Add($"{key}:{value ?? "null"}");
                    }
                }
                return parts.Count > 0 ? string.Join("|", parts) : "no_conditions";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VERA Trial Workflow] Error creating condition signature: {e.Message}");
                return "signature_error";
            }
        }

        /// <summary>
        /// Checks if the workflow requires Latin square ordering (set in VERA portal).
        /// If true, researcher should call ApplyLatinSquareOrdering() with their participant number.
        /// </summary>
        public bool RequiresLatinSquareOrdering => withinGroupsForLatinSquare.Count > 0;

        private void ApplyAutomaticOrdering(TrialConfig[] topLevelTrials)
        {
            // Latin square detection is now handled during FlattenTrialHierarchy
            // This method just logs the result
            if (withinGroupsForLatinSquare.Count > 0)
            {
                var groupInfo = new List<string>();
                foreach (var group in withinGroupsForLatinSquare)
                {
                    groupInfo.Add($"{group.Key} ({group.Value.Count} trials)");
                }
                Debug.Log($"[VERA Trial Workflow] Latin square ordering required for {withinGroupsForLatinSquare.Count} within-group(s): {string.Join(", ", groupInfo)}. Call ApplyLatinSquareOrdering(participantNumber) before starting trials.");
            }
        }

        /// <summary>
        /// Saves the current trial index as a checkpoint to the server with local fallback.
        /// </summary>
        private IEnumerator SaveCheckpoint()
        {
            if (string.IsNullOrEmpty(participantUUID) || string.IsNullOrEmpty(experimentUUID))
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot save checkpoint: missing participant or experiment UUID.");
                yield break;
            }

            // Always save locally first as a fallback
            SaveLocalCheckpoint(currentTrialIndex);

            string host = VERAHost.hostUrl;
            string url = $"{host}/api/participants/{participantUUID}/experiments/{experimentUUID}/checkpoint";

            // Create JSON payload
            string jsonPayload = $"{{\"currentTrialIndex\":{currentTrialIndex}}}";
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);

            UnityWebRequest request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            bool requestComplete = false;
            VERAWebRequestResult result = null;

            yield return ExecuteWebRequestWithRetry(request, (r) =>
            {
                result = r;
                requestComplete = true;
            });

            // Wait for callback
            while (!requestComplete)
                yield return null;

            request.Dispose();

            if (result != null && result.success)
            {
                Debug.Log($"[VERA Trial Workflow] Checkpoint saved to server: trial {currentTrialIndex + 1}/{trialWorkflow.Count}");
                // Clear local checkpoint after successful server save
                ClearLocalCheckpoint();
            }
            else
            {
                Debug.LogWarning($"[VERA Trial Workflow] Failed to save checkpoint to server (using local fallback): {result?.error ?? "Unknown error"}");
            }
        }

        /// <summary>
        /// Checks for saved progress and resumes if available (server first, local fallback).
        /// </summary>
        private IEnumerator CheckAndResumeProgress()
        {
            if (string.IsNullOrEmpty(participantUUID) || string.IsNullOrEmpty(experimentUUID))
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot check for saved progress: missing participant or experiment UUID.");
                yield break;
            }

            if (trialWorkflow == null || trialWorkflow.Count == 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot resume progress: workflow is empty.");
                yield break;
            }

            string host = VERAHost.hostUrl;
            string url = $"{host}/api/participants/{participantUUID}/experiments/{experimentUUID}/checkpoint";

            UnityWebRequest request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            bool requestComplete = false;
            VERAWebRequestResult result = null;

            yield return ExecuteWebRequestWithRetry(request, (r) =>
            {
                result = r;
                requestComplete = true;
            }, maxRetries: 2); // Fewer retries for checkpoint load

            // Wait for callback
            while (!requestComplete)
                yield return null;

            request.Dispose();

            bool checkpointLoaded = false;

            if (result != null && result.success)
            {
                try
                {
                    string jsonResponse = result.jsonResponse;
                    if (!string.IsNullOrEmpty(jsonResponse))
                    {
                        CheckpointResponse checkpoint = JsonUtility.FromJson<CheckpointResponse>(jsonResponse);

                        if (checkpoint != null && checkpoint.currentTrialIndex >= 0 && checkpoint.currentTrialIndex < trialWorkflow.Count)
                        {
                            currentTrialIndex = checkpoint.currentTrialIndex;
                            currentTrialState = TrialState.NotStarted;
                            Debug.Log($"[VERA Trial Workflow] Resumed from server checkpoint: trial {currentTrialIndex + 1}/{trialWorkflow.Count}");
                            checkpointLoaded = true;
                        }
                        else if (checkpoint != null)
                        {
                            Debug.LogWarning($"[VERA Trial Workflow] Server checkpoint is invalid (index {checkpoint.currentTrialIndex} out of range 0-{trialWorkflow.Count - 1}).");
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[VERA Trial Workflow] Failed to parse server checkpoint response: {e.Message}");
                }
            }
            else if (result != null && result.responseCode == 404)
            {
                Debug.Log("[VERA Trial Workflow] No server checkpoint found.");
            }
            else
            {
                Debug.LogWarning($"[VERA Trial Workflow] Failed to fetch server checkpoint: {result?.error ?? "Unknown error"}");
            }

            // Try local checkpoint if server checkpoint wasn't loaded
            if (!checkpointLoaded && TryLoadLocalCheckpoint(out int localTrialIndex))
            {
                if (localTrialIndex >= 0 && localTrialIndex < trialWorkflow.Count)
                {
                    currentTrialIndex = localTrialIndex;
                    currentTrialState = TrialState.NotStarted;
                    Debug.Log($"[VERA Trial Workflow] Resumed from local checkpoint: trial {currentTrialIndex + 1}/{trialWorkflow.Count}");
                    checkpointLoaded = true;
                }
                else
                {
                    Debug.LogWarning($"[VERA Trial Workflow] Local checkpoint is invalid (index {localTrialIndex} out of range 0-{trialWorkflow.Count - 1}).");
                }
            }

            if (!checkpointLoaded)
            {
                Debug.Log("[VERA Trial Workflow] No saved progress found, starting from beginning.");
            }
        }

        [System.Serializable]
        private class CheckpointResponse
        {
            public int currentTrialIndex;
        }

        #endregion
    }
}
