using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;

namespace VERA
{
    // Data structures for parsing the trial workflow response JSON
    [System.Serializable]
    internal class TrialWorkflowResponse
    {
        public TrialConfig[] trials;
        public string version;
    }

    /// <summary>
    /// Represents a trial configuration from the VERA trial workflow.
    /// Contains trial metadata, type information, conditions, and child trials for within/between-subjects designs.
    /// </summary>
    [System.Serializable]
    public class TrialConfig
    {
        public string id;                           // Trial unique identifier
        public string type;                         // Trial type: "standalone", "within", "between"
        public string label;                        // Trial label/name
        public int order;                           // Trial order in sequence
        public int repetitionCount;                 // Number of times this trial should repeat
        public Dictionary<string, string> conditions;  // Condition values for this trial (IV assignments)
        public TrialConfig[] childTrials;          // Child trials for within/between-subjects designs
        public string[] withinSubjectsIVs;         // Within-subjects independent variables
        public string[] betweenSubjectsIVs;        // Between-subjects independent variables
        public string randomizationType;            // Randomization type: "standard", etc.
        public string trialOrdering;                // Trial ordering: "sequential", "random", etc.
        public Dictionary<string, float> perTrialDistributions;  // Distribution percentages for between-subjects trials
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

    internal class VERATrialWorkflowManager : MonoBehaviour
    {
        // VERATrialWorkflowManager handles fetching and managing the trial workflow order
        // from the VERA API endpoint: /api/experiments/{experimentId}/trials/order
        // It also manages trial lifecycle including start time, duration, and completion state

        private List<TrialConfig> trialWorkflow = new List<TrialConfig>();
        private int currentTrialIndex = -1;
        public bool isInitialized { get; private set; } = false;

        // Trial state tracking
        public TrialState currentTrialState { get; private set; } = TrialState.NotStarted;
        private float trialStartTime = 0f;
        private float trialDuration = 0f;

        private string experimentUUID;
        private string apiKey;


        #region INITIALIZATION

        // Initializes the trial workflow manager by fetching the trial order from the API
        public IEnumerator Initialize(string experimentId, string authToken)
        {
            if (isInitialized)
            {
                Debug.LogWarning("[VERA Trial Workflow] Trial workflow is already initialized.");
                yield break;
            }

            experimentUUID = experimentId;
            apiKey = authToken;

            yield return FetchTrialWorkflow();
        }

        #endregion


        #region TRIAL WORKFLOW API

        // Fetches the trial workflow from the VERA API
        private IEnumerator FetchTrialWorkflow()
        {
            string host = VERAHost.hostUrl;
            string url = $"{host}/api/experiments/{experimentUUID}/trials/order";

            UnityWebRequest www = UnityWebRequest.Get(url);
            www.SetRequestHeader("Authorization", "Bearer " + apiKey);

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = www.downloadHandler.text;
                    TrialWorkflowResponse response = JsonUtility.FromJson<TrialWorkflowResponse>(jsonResponse);

                    if (response != null && response.trials != null && response.trials.Length > 0)
                    {
                        // Flatten the hierarchical trial structure into a sequential workflow
                        trialWorkflow = FlattenTrialHierarchy(response.trials);

                        isInitialized = true;
                        Debug.Log($"[VERA Trial Workflow] Successfully loaded {trialWorkflow.Count} trials from workflow (version: {response.version})");
                    }
                    else
                    {
                        Debug.LogError("[VERA Trial Workflow] API returned null or empty trials array.");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[VERA Trial Workflow] Failed to parse trial workflow response: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[VERA Trial Workflow] Failed to fetch trial workflow: {www.error}");
            }
        }

        // Flattens the hierarchical trial structure into a sequential list
        // Handles standalone trials, within-subjects trials, and between-subjects trials
        private List<TrialConfig> FlattenTrialHierarchy(TrialConfig[] topLevelTrials)
        {
            List<TrialConfig> flattenedTrials = new List<TrialConfig>();

            foreach (TrialConfig trial in topLevelTrials)
            {
                // Process trials based on their type
                if (trial.type == "standalone")
                {
                    // Standalone trials are added directly
                    // Handle repetitions by adding the trial multiple times
                    for (int i = 0; i < trial.repetitionCount; i++)
                    {
                        flattenedTrials.Add(trial);
                    }
                }
                else if (trial.type == "within" || trial.type == "between")
                {
                    // Within/Between-subjects trials have child trials
                    if (trial.childTrials != null && trial.childTrials.Length > 0)
                    {
                        // Add all child trials (already flattened in the response)
                        foreach (TrialConfig childTrial in trial.childTrials)
                        {
                            // Handle repetitions for each child trial
                            for (int i = 0; i < childTrial.repetitionCount; i++)
                            {
                                flattenedTrials.Add(childTrial);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[VERA Trial Workflow] Trial {trial.label} has type '{trial.type}' but no child trials.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[VERA Trial Workflow] Unknown trial type '{trial.type}' for trial {trial.label}");
                }
            }

            return flattenedTrials;
        }

        #endregion


        #region TRIAL ACCESS METHODS

        /// <summary>
        /// Gets the current trial configuration.
        /// Returns null if no trial is currently active or workflow is not initialized.
        /// </summary>
        public TrialConfig CurrentTrial
        {
            get
            {
                if (!isInitialized)
                {
                    Debug.LogWarning("[VERA Trial Workflow] Cannot get current trial: workflow not initialized.");
                    return null;
                }

                if (currentTrialIndex < 0 || currentTrialIndex >= trialWorkflow.Count)
                {
                    return null;
                }

                return trialWorkflow[currentTrialIndex];
            }
        }

        /// <summary>
        /// Advances to the next trial in the workflow and returns it.
        /// NOTE: This method only advances the index. You must call StartTrial() to begin the trial lifecycle.
        /// Returns null if there are no more trials or workflow is not initialized.
        /// </summary>
        public TrialConfig GetNextTrial()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot get next trial: workflow not initialized.");
                return null;
            }

            // Check if current trial must be completed before advancing
            if (currentTrialState == TrialState.InProgress)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot advance to next trial: current trial is still in progress. Call CompleteTrial() or AbortTrial() first.");
                return null;
            }

            currentTrialIndex++;

            if (currentTrialIndex >= trialWorkflow.Count)
            {
                Debug.Log("[VERA Trial Workflow] No more trials in workflow.");
                return null;
            }

            TrialConfig nextTrial = trialWorkflow[currentTrialIndex];
            currentTrialState = TrialState.NotStarted;
            Debug.Log($"[VERA Trial Workflow] Advanced to trial {currentTrialIndex + 1}/{trialWorkflow.Count}: {nextTrial.label}");

            return nextTrial;
        }

        /// <summary>
        /// Gets the total number of trials in the workflow.
        /// </summary>
        public int TotalTrialCount
        {
            get { return trialWorkflow.Count; }
        }

        /// <summary>
        /// Gets the current trial index (0-based).
        /// Returns -1 if no trial has been started yet.
        /// </summary>
        public int CurrentTrialIndex
        {
            get { return currentTrialIndex; }
        }

        /// <summary>
        /// Checks if there are more trials remaining in the workflow.
        /// </summary>
        public bool HasMoreTrials
        {
            get { return isInitialized && (currentTrialIndex + 1) < trialWorkflow.Count; }
        }

        /// <summary>
        /// Resets the trial workflow to the beginning.
        /// </summary>
        public void ResetWorkflow()
        {
            currentTrialIndex = -1;
            currentTrialState = TrialState.NotStarted;
            trialStartTime = 0f;
            trialDuration = 0f;
            Debug.Log("[VERA Trial Workflow] Workflow reset to beginning.");
        }

        #endregion


        #region TRIAL LIFECYCLE MANAGEMENT

        /// <summary>
        /// Starts the current trial, marking it as in progress and beginning time tracking.
        /// Must be called after GetNextTrial() to properly manage trial lifecycle.
        /// </summary>
        /// <returns>True if trial was started successfully, false otherwise</returns>
        public bool StartTrial()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot start trial: workflow not initialized.");
                return false;
            }

            if (CurrentTrial == null)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot start trial: no current trial. Call GetNextTrial() first.");
                return false;
            }

            if (currentTrialState == TrialState.InProgress)
            {
                Debug.LogWarning("[VERA Trial Workflow] Trial is already in progress.");
                return false;
            }

            currentTrialState = TrialState.InProgress;
            trialStartTime = Time.time;
            trialDuration = 0f;

            Debug.Log($"[VERA Trial Workflow] Started trial: {CurrentTrial.label}");
            return true;
        }

        /// <summary>
        /// Marks the current trial as completed and records its duration.
        /// The participant has successfully finished the trial.
        /// </summary>
        /// <returns>True if trial was completed successfully, false otherwise</returns>
        public bool CompleteTrial()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot complete trial: workflow not initialized.");
                return false;
            }

            if (currentTrialState != TrialState.InProgress)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot complete trial: no trial is currently in progress.");
                return false;
            }

            trialDuration = Time.time - trialStartTime;
            currentTrialState = TrialState.Completed;

            Debug.Log($"[VERA Trial Workflow] Completed trial: {CurrentTrial.label} (Duration: {trialDuration:F2}s)");
            return true;
        }

        /// <summary>
        /// Marks the current trial as aborted due to an unexpected event.
        /// Use this when something goes wrong and the trial cannot be completed normally.
        /// </summary>
        /// <param name="reason">Optional reason for aborting the trial</param>
        /// <returns>True if trial was aborted successfully, false otherwise</returns>
        public bool AbortTrial(string reason = "")
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot abort trial: workflow not initialized.");
                return false;
            }

            if (currentTrialState != TrialState.InProgress)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot abort trial: no trial is currently in progress.");
                return false;
            }

            trialDuration = Time.time - trialStartTime;
            currentTrialState = TrialState.Aborted;

            string logMessage = $"[VERA Trial Workflow] Aborted trial: {CurrentTrial.label} (Duration: {trialDuration:F2}s)";
            if (!string.IsNullOrEmpty(reason))
            {
                logMessage += $" - Reason: {reason}";
            }
            Debug.LogWarning(logMessage);

            return true;
        }

        /// <summary>
        /// Gets the elapsed time for the current trial in seconds.
        /// Returns 0 if no trial is in progress.
        /// </summary>
        public float GetTrialElapsedTime()
        {
            if (currentTrialState == TrialState.InProgress)
            {
                return Time.time - trialStartTime;
            }
            return 0f;
        }

        /// <summary>
        /// Gets the duration of the last completed or aborted trial in seconds.
        /// Returns 0 if no trial has been completed yet.
        /// </summary>
        public float GetLastTrialDuration()
        {
            return trialDuration;
        }

        /// <summary>
        /// Checks if a trial is currently in progress.
        /// </summary>
        public bool IsTrialInProgress
        {
            get { return currentTrialState == TrialState.InProgress; }
        }

        #endregion


        #region TRIAL ORDERING HELPERS

        /// <summary>
        /// Randomizes the entire trial workflow using Fisher-Yates shuffle algorithm.
        /// Should be called after initialization but before starting any trials.
        /// WARNING: Cannot be called once trials have started (currentTrialIndex > -1).
        /// </summary>
        public void RandomizeWorkflow()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot randomize: workflow not initialized.");
                return;
            }

            if (currentTrialIndex >= 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot randomize: trials have already started.");
                return;
            }

            if (trialWorkflow.Count <= 1)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot randomize: workflow has 0 or 1 trials.");
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
        }

        /// <summary>
        /// Applies Latin Square counterbalancing to the trial workflow.
        /// Uses participant number to determine the row offset in the Latin square.
        /// Should be called after initialization but before starting any trials.
        /// WARNING: Cannot be called once trials have started (currentTrialIndex > -1).
        /// </summary>
        /// <param name="participantNumber">The participant number (determines Latin square row)</param>
        public void ApplyLatinSquareOrdering(int participantNumber)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot apply Latin square: workflow not initialized.");
                return;
            }

            if (currentTrialIndex >= 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot apply Latin square: trials have already started.");
                return;
            }

            if (trialWorkflow.Count == 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot apply Latin square: workflow is empty.");
                return;
            }

            int n = trialWorkflow.Count;
            List<TrialConfig> latinSquareOrder = new List<TrialConfig>();

            // Calculate offset based on participant number modulo trial count
            int offset = participantNumber % n;

            // Generate Latin square row
            for (int i = 0; i < n; i++)
            {
                int index = (i + offset) % n;
                latinSquareOrder.Add(trialWorkflow[index]);
            }

            trialWorkflow = latinSquareOrder;
            Debug.Log($"[VERA Trial Workflow] Applied Latin square ordering for participant {participantNumber} (offset: {offset}).");
        }

        /// <summary>
        /// Randomizes trials within blocks while preserving block structure.
        /// Useful for blocked randomization designs where you want randomization within each block
        /// but maintain the overall block order.
        /// Should be called after initialization but before starting any trials.
        /// WARNING: Cannot be called once trials have started (currentTrialIndex > -1).
        /// </summary>
        /// <param name="blockSize">Number of trials per block</param>
        public void RandomizeWithinBlocks(int blockSize)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot randomize blocks: workflow not initialized.");
                return;
            }

            if (currentTrialIndex >= 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot randomize blocks: trials have already started.");
                return;
            }

            if (blockSize <= 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot randomize blocks: block size must be > 0.");
                return;
            }

            if (trialWorkflow.Count == 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot randomize blocks: workflow is empty.");
                return;
            }

            System.Random rng = new System.Random();
            List<TrialConfig> randomizedWorkflow = new List<TrialConfig>();

            // Process trials in blocks
            for (int blockStart = 0; blockStart < trialWorkflow.Count; blockStart += blockSize)
            {
                int blockEnd = Mathf.Min(blockStart + blockSize, trialWorkflow.Count);
                List<TrialConfig> block = trialWorkflow.GetRange(blockStart, blockEnd - blockStart);

                // Shuffle within block using Fisher-Yates
                for (int i = block.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    TrialConfig temp = block[j];
                    block[j] = block[i];
                    block[i] = temp;
                }

                randomizedWorkflow.AddRange(block);
            }

            trialWorkflow = randomizedWorkflow;
            Debug.Log($"[VERA Trial Workflow] Applied block randomization with block size {blockSize}.");
        }

        /// <summary>
        /// Randomizes trials using a seeded random number generator.
        /// Ensures reproducible randomization for a given seed value.
        /// Useful for debugging or when you need deterministic "randomness".
        /// Should be called after initialization but before starting any trials.
        /// WARNING: Cannot be called once trials have started (currentTrialIndex > -1).
        /// </summary>
        /// <param name="seed">Random seed value</param>
        public void RandomizeWithSeed(int seed)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot randomize: workflow not initialized.");
                return;
            }

            if (currentTrialIndex >= 0)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot randomize: trials have already started.");
                return;
            }

            if (trialWorkflow.Count <= 1)
            {
                Debug.LogWarning("[VERA Trial Workflow] Cannot randomize: workflow has 0 or 1 trials.");
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

            Debug.Log($"[VERA Trial Workflow] Workflow randomized with seed {seed}.");
        }

        #endregion
    }
}
