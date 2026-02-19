using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VERA
{
    /// <summary>
    /// Manages the current VERA participant session.
    /// Can be used to access and modify the current state of the session.
    /// </summary>
    public static class VERASessionManager
    {

        /// <summary>
        /// The current participant's ID.
        /// </summary>
        public static int participantID { get { return VERALogger.Instance.activeParticipant.participantShortId; } }

        /// <summary>
        /// Whether VERA has been initialized for the current session, and is ready to log data.
        /// </summary>
        public static bool initialized { get { return VERALogger.Instance.initialized; } }

        /// <summary>
        /// Event that is invoked when VERA has been successfully initialized for this session and is ready to log data.
        /// Subscribe to this event to perform actions that depend on VERA being fully initialized, such as setting initial condition values.
        /// </summary>
        public static UnityEvent onInitialized { get { return VERALogger.Instance.onLoggerInitialized; } }

        /// <summary>
        /// Whether data collection is currently active for the current participant session.
        /// </summary>
        public static bool collecting { get { return VERALogger.Instance.collecting; } }

        /// <summary>
        /// Finalizes the current participant session.
        /// This marks the participant as having completed the experiment and prevents further logging.
        /// It is highly recommended to call this method at the end of the experiment to ensure data integrity.
        /// </summary>
        public static void FinalizeSession()
        {
            if (!initialized)
            {
                VERADebugger.LogWarning("Cannot finalize session because VERA is not initialized.", "VERASessionManager");
                return;
            }

            if (!collecting)
            {
                VERADebugger.LogWarning("Cannot finalize session because data collection is not active.", "VERASessionManager");
                return;
            }

            VERALogger.Instance.FinalizeSession();
        }

        /// <summary>
        /// Starts a survey for the current participant session, based on the provided SurveyInfo.
        /// Whereas you can use this method to start any survey at any time, it is recommended to use the
        /// automatically generated VERASurveyHelper static class to do so in a more convenient manner.
        /// </summary>
        /// <param name="surveyToStart">A VERASurveyInfo scriptable object representing the survey to start.</param>
        /// <param name="transportToLobby">Whether to temporarily transport the participant to a survey lobby while the survey is active. Default is true.</param>
        /// <param name="dimEnvironment">Whether to fade the surrounding environment slightly, to help focus on the survey. Default is true.</param>
        /// <param name="heightOffset">How far the survey will be offset vertically from the user's head position. Default is 0.</param>
        /// <param name="distanceOffset">How far the survey will be offset horizontally from the user's head position. Default is 3.</param>
        /// <param name="onSurveyComplete">An optional callback Action that will be invoked when the survey is completed by the participant.</param>
        public static void StartSurvey(VERASurveyInfo surveyToStart, bool transportToLobby = true, bool dimEnvironment = true, float heightOffset = 0f, float distanceOffset = 3f, Action onSurveyComplete = null)
        {
            if (!initialized)
            {
                VERADebugger.LogWarning("Cannot start survey because VERA is not initialized.", "VERASessionManager");
                return;
            }

            if (!collecting)
            {
                VERADebugger.LogWarning("Cannot start survey because data collection is not active.", "VERASessionManager");
                return;
            }

            VERALogger.Instance.StartSurvey(surveyToStart, transportToLobby, dimEnvironment, heightOffset, distanceOffset, onSurveyComplete);
        }

        /// <summary>
        /// Creates a new arbitrary CSV entry with the specified file name, event ID, and values.
        /// It is highly recommended to use the generated VERAFile_[FileName].CreateCsvEntry methods instead of this method,
        /// as those methods provide type safety and ensure correct column ordering. Use this function only as a last resort.
        /// </summary>
        /// <param name="eventId">An identifier for this log entry, of type int. Mandatory for each user-generated file type, but may be arbitrarily assigned according to your preferences.</param>
        /// <param name="fileName">The name of the CSV file to which this entry should be added, without the .csv extension.</param>
        /// <param name="values">The values to be logged in this CSV entry, in the correct order as per the file's configuration.</param>
        public static void CreateArbitraryCsvEntry(string fileName, int eventId, params object[] values)
        {
            if (!initialized)
            {
                VERADebugger.LogWarning("Cannot create CSV entry because VERA is not initialized.", "VERASessionManager");
                return;
            }

            if (!collecting)
            {
                VERADebugger.LogWarning("Cannot create CSV entry because data collection is not active.", "VERASessionManager");
                return;
            }

            VERALogger.Instance.CreateCsvEntry(fileName, eventId, values);
        }

        /// <summary>
        /// Creates a new row entry to a CSV log file by its file name, without an eventId.
        /// This overload is used for baseline telemetry and other auto-populated file types that don't use eventId.
        /// </summary>
        /// <param name="fileName">The name of the CSV file to which this entry should be added, without the .csv extension.</param>
        /// <param name="values">The values to be logged in this CSV entry, in the correct order as per the file's configuration.</param>
        public static void CreateArbitraryCsvEntry(string fileName, params object[] values)
        {
            if (!initialized)
            {
                VERADebugger.LogWarning("Cannot create CSV entry because VERA is not initialized.", "VERASessionManager");
                return;
            }

            if (!collecting)
            {
                VERADebugger.LogWarning("Cannot create CSV entry because data collection is not active.", "VERASessionManager");
                return;
            }

            VERALogger.Instance.CreateCsvEntry(fileName, values);
        }

        /// <summary>
        /// Gets the currently selected condition value of the specified independent variable.
        /// It is highly recommended to use the generated VERAIV_[IVGroupName].GetSelectedValue() methods instead of this method,
        /// as those methods provide type safety and ensure correct value handling. Use this function only as a last resort.
        /// </summary>
        /// <param name="ivGroupName">The name of the independent variable to get the value of</param>
        /// <returns>The current selected value of the independent variable</returns>
        public static string GetSelectedIVValue(string ivGroupName)
        {
            return VERALogger.Instance?.GetSelectedIVValue(ivGroupName);
        }

        /// <summary>
        /// Sets the currently selected condition value of the specified independent variable.
        /// It is highly recommended to use the generated VERAIV_[IVGroupName].SetSelectedValue() methods instead of this method,
        /// as those methods provide type safety and ensure correct value handling. Use this function only as a last resort.
        /// </summary>
        /// <param name="ivGroupName">The name of the independent variable to set the value of</param>
        /// <param name="value">The new value to set</param>
        public static void SetSelectedIVValue(string ivGroupName, string value)
        {
            VERALogger.Instance?.SetSelectedIVValue(ivGroupName, value);
        }

        /// <summary>
        /// Manually initializes VERA with the specified site ID and participant ID.
        /// It is highly recommended to allow VERA to initialize itself automatically.
        /// You should not need to call this method unless you have a specific reason to do so.
        /// </summary>
        /// <param name="siteId">The site ID to use for this session</param>
        /// <param name="participantId">The participant ID to use for this session</param>
        public static void ManualInitialization(string siteId, string participantId)
        {
            VERALogger.Instance?.ManualInitialization(siteId, participantId);
        }

        /// <summary>
        /// Gets the current trial configuration from the trial workflow.
        /// Returns null if no trial is currently active or workflow is not initialized.
        /// </summary>
        public static TrialConfig CurrentTrial
        {
            get
            {
                if (!initialized)
                {
                    Debug.LogWarning("[VERASessionManager] Cannot get current trial because VERA is not initialized.");
                    return null;
                }
                return VERALogger.Instance?.trialWorkflow?.CurrentTrial;
            }
        }

        /// <summary>
        /// Advances to the next trial in the workflow and returns it.
        /// Returns null if there are no more trials or workflow is not initialized.
        /// </summary>
        public static TrialConfig GetNextTrial()
        {
            if (!initialized)
            {
                Debug.LogWarning("[VERASessionManager] Cannot get next trial because VERA is not initialized.");
                return null;
            }
            return VERALogger.Instance?.trialWorkflow?.GetNextTrial();
        }

        /// <summary>
        /// Gets the total number of trials in the workflow.
        /// Returns 0 if workflow is not initialized.
        /// </summary>
        public static int TotalTrialCount
        {
            get { return VERALogger.Instance?.trialWorkflow?.TotalTrialCount ?? 0; }
        }

        /// <summary>
        /// Gets the current trial index (0-based).
        /// Returns -1 if no trial has been started yet or workflow is not initialized.
        /// </summary>
        public static int CurrentTrialIndex
        {
            get { return VERALogger.Instance?.trialWorkflow?.CurrentTrialIndex ?? -1; }
        }

        /// <summary>
        /// Checks if there are more trials remaining in the workflow.
        /// Returns false if workflow is not initialized.
        /// </summary>
        public static bool HasMoreTrials
        {
            get { return VERALogger.Instance?.trialWorkflow?.HasMoreTrials ?? false; }
        }

        /// <summary>
        /// Resets the trial workflow to the beginning.
        /// </summary>
        public static void ResetTrialWorkflow()
        {
            if (!initialized)
            {
                Debug.LogWarning("[VERASessionManager] Cannot reset trial workflow because VERA is not initialized.");
                return;
            }
            VERALogger.Instance?.trialWorkflow?.ResetWorkflow();
        }

        /// <summary>
        /// Starts the current trial, marking it as in progress and beginning time tracking.
        /// Must be called after GetNextTrial() to properly manage trial lifecycle.
        /// This allocates resources and time for the participant to complete the trial.
        /// </summary>
        /// <returns>True if trial was started successfully, false otherwise</returns>
        public static bool StartTrial()
        {
            if (!initialized)
            {
                Debug.LogWarning("[VERASessionManager] Cannot start trial because VERA is not initialized.");
                return false;
            }
            return VERALogger.Instance?.trialWorkflow?.StartTrial() ?? false;
        }

        /// <summary>
        /// Marks the current trial as completed and records its duration.
        /// Call this when the participant has successfully finished the trial.
        /// </summary>
        /// <returns>True if trial was completed successfully, false otherwise</returns>
        public static bool CompleteTrial()
        {
            if (!initialized)
            {
                Debug.LogWarning("[VERASessionManager] Cannot complete trial because VERA is not initialized.");
                return false;
            }
            return VERALogger.Instance?.trialWorkflow?.CompleteTrial() ?? false;
        }

        /// <summary>
        /// Marks the current trial as aborted due to an unexpected event.
        /// Use this when something abrupt happens and the trial cannot be completed normally.
        /// </summary>
        /// <param name="reason">Optional reason for aborting the trial</param>
        /// <returns>True if trial was aborted successfully, false otherwise</returns>
        public static bool AbortTrial(string reason = "")
        {
            if (!initialized)
            {
                Debug.LogWarning("[VERASessionManager] Cannot abort trial because VERA is not initialized.");
                return false;
            }
            return VERALogger.Instance?.trialWorkflow?.AbortTrial(reason) ?? false;
        }

        /// <summary>
        /// Gets the elapsed time for the current trial in seconds.
        /// Returns 0 if no trial is in progress.
        /// </summary>
        public static float GetTrialElapsedTime()
        {
            return VERALogger.Instance?.trialWorkflow?.GetTrialElapsedTime() ?? 0f;
        }

        /// <summary>
        /// Gets the duration of the last completed or aborted trial in seconds.
        /// Returns 0 if no trial has been completed yet.
        /// </summary>
        public static float GetLastTrialDuration()
        {
            return VERALogger.Instance?.trialWorkflow?.GetLastTrialDuration() ?? 0f;
        }

        /// <summary>
        /// Gets the current state of the active trial.
        /// </summary>
        public static TrialState CurrentTrialState
        {
            get { return VERALogger.Instance?.trialWorkflow?.currentTrialState ?? TrialState.NotStarted; }
        }

        /// <summary>
        /// Checks if a trial is currently in progress.
        /// </summary>
        public static bool IsTrialInProgress
        {
            get { return VERALogger.Instance?.trialWorkflow?.IsTrialInProgress ?? false; }
        }

        /// <summary>
        /// Randomizes the trial workflow order using Fisher-Yates shuffle.
        /// Must be called after VERA initialization but before starting any trials.
        /// This provides complete randomization of trial order.
        /// </summary>
        public static void RandomizeTrialOrder()
        {
            if (!initialized)
            {
                Debug.LogWarning("[VERASessionManager] Cannot randomize: VERA not initialized.");
                return;
            }
            VERALogger.Instance?.trialWorkflow?.RandomizeWorkflow();
        }

        /// <summary>
        /// Applies Latin Square counterbalancing to the trial workflow.
        /// Must be called after VERA initialization but before starting any trials.
        /// Uses participant ID to determine the counterbalancing offset.
        /// </summary>
        public static void ApplyLatinSquareCounterbalancing()
        {
            if (!initialized)
            {
                Debug.LogWarning("[VERASessionManager] Cannot apply Latin square: VERA not initialized.");
                return;
            }
            int participantId = VERALogger.Instance.activeParticipant.participantShortId;
            VERALogger.Instance?.trialWorkflow?.ApplyLatinSquareOrdering(participantId);
        }

        /// <summary>
        /// Applies Latin Square counterbalancing using a custom participant number.
        /// Must be called after VERA initialization but before starting any trials.
        /// Useful when you want to override the default participant ID for counterbalancing.
        /// </summary>
        /// <param name="participantNumber">The participant number to use for counterbalancing</param>
        public static void ApplyLatinSquareCounterbalancing(int participantNumber)
        {
            if (!initialized)
            {
                Debug.LogWarning("[VERASessionManager] Cannot apply Latin square: VERA not initialized.");
                return;
            }
            VERALogger.Instance?.trialWorkflow?.ApplyLatinSquareOrdering(participantNumber);
        }

        /// <summary>
        /// Applies Latin Square counterbalancing with total participant count for proper validation.
        /// This is the RECOMMENDED method for applying Latin square ordering.
        ///
        /// IMPORTANT - Enforces complete counterbalancing:
        /// - totalParticipants MUST be >= number of conditions (returns false otherwise)
        /// - participantNumber must be less than totalParticipants
        /// - If validation fails, Latin square is NOT applied (returns false)
        ///
        /// Example:
        ///   // For a study with 30 total participants
        ///   int participantNum = VERALogger.Instance.activeParticipant.participantShortId;
        ///   bool success = VERASessionManager.ApplyLatinSquareCounterbalancing(participantNum, 30);
        ///   if (!success) Debug.LogError("Latin square failed - check console!");
        /// </summary>
        /// <param name="participantNumber">The participant's sequential number (0-indexed). Must be less than totalParticipants.</param>
        /// <param name="totalParticipants">The total number of participants in the study. Must be >= number of conditions.</param>
        /// <returns>True if Latin square ordering was applied successfully, false if validation failed.</returns>
        public static bool ApplyLatinSquareCounterbalancing(int participantNumber, int totalParticipants)
        {
            if (!initialized)
            {
                Debug.LogWarning("[VERASessionManager] Cannot apply Latin square: VERA not initialized.");
                return false;
            }
            return VERALogger.Instance?.trialWorkflow?.ApplyLatinSquareOrdering(participantNumber, totalParticipants) ?? false;
        }

        /// <summary>
        /// Gets the within-subjects independent variables for the current trial.
        /// Returns null if no trial is current or the trial has no within-subjects IVs.
        /// </summary>
        public static string[] CurrentTrialWithinSubjectsIVs
        {
            get
            {
                if (!initialized)
                {
                    Debug.LogWarning("[VERASessionManager] Cannot get within-subjects IVs: VERA not initialized.");
                    return null;
                }
                return VERALogger.Instance?.trialWorkflow?.GetCurrentTrialWithinSubjectsIVs();
            }
        }

        /// <summary>
        /// Gets the between-subjects independent variables for the current trial.
        /// Returns null if no trial is current or the trial has no between-subjects IVs.
        /// </summary>
        public static string[] CurrentTrialBetweenSubjectsIVs
        {
            get
            {
                if (!initialized)
                {
                    Debug.LogWarning("[VERASessionManager] Cannot get between-subjects IVs: VERA not initialized.");
                    return null;
                }
                return VERALogger.Instance?.trialWorkflow?.GetCurrentTrialBetweenSubjectsIVs();
            }
        }

        /// <summary>
        /// Gets the randomization type for the current trial.
        /// Returns null if no trial is current.
        /// </summary>
        public static string CurrentTrialRandomizationType
        {
            get
            {
                if (!initialized)
                {
                    Debug.LogWarning("[VERASessionManager] Cannot get randomization type: VERA not initialized.");
                    return null;
                }
                return VERALogger.Instance?.trialWorkflow?.GetCurrentTrialRandomizationType();
            }
        }

        /// <summary>
        /// Gets the trial ordering configuration for the current trial.
        /// Returns null if no trial is current.
        /// </summary>
        public static string CurrentTrialOrdering
        {
            get
            {
                if (!initialized)
                {
                    Debug.LogWarning("[VERASessionManager] Cannot get trial ordering: VERA not initialized.");
                    return null;
                }
                return VERALogger.Instance?.trialWorkflow?.GetCurrentTrialOrdering();
            }
        }

        /// <summary>
        /// Gets the per-trial distributions for the current trial.
        /// Used for between-subjects designs to specify condition distribution percentages.
        /// Returns null if no trial is current or the trial has no distributions.
        /// </summary>
        public static Dictionary<string, float> CurrentTrialDistributions
        {
            get
            {
                if (!initialized)
                {
                    Debug.LogWarning("[VERASessionManager] Cannot get trial distributions: VERA not initialized.");
                    return null;
                }
                return VERALogger.Instance?.trialWorkflow?.GetCurrentTrialDistributions();
            }
        }

        /// <summary>
        /// Gets the trial ID for the current trial.
        /// Returns null if no trial is current.
        /// </summary>
        public static string CurrentTrialId
        {
            get
            {
                if (!initialized)
                {
                    Debug.LogWarning("[VERASessionManager] Cannot get trial ID: VERA not initialized.");
                    return null;
                }
                return VERALogger.Instance?.trialWorkflow?.GetCurrentTrialId();
            }
        }

        /// <summary>
        /// Gets the trial type for the current trial.
        /// Returns null if no trial is current.
        /// </summary>
        public static string CurrentTrialType
        {
            get
            {
                if (!initialized)
                {
                    Debug.LogWarning("[VERASessionManager] Cannot get trial type: VERA not initialized.");
                    return null;
                }
                return VERALogger.Instance?.trialWorkflow?.GetCurrentTrialType();
            }
        }

        /// <summary>
        /// Gets the trial conditions (IV assignments) for the current trial.
        /// Returns null if no trial is current or the trial has no conditions.
        /// </summary>
        public static Dictionary<string, string> CurrentTrialConditions
        {
            get
            {
                if (!initialized)
                {
                    Debug.LogWarning("[VERASessionManager] Cannot get trial conditions: VERA not initialized.");
                    return null;
                }
                return VERALogger.Instance?.trialWorkflow?.GetCurrentTrialConditions();
            }
        }

        /// <summary>
        /// Randomizes trials within blocks while preserving block structure.
        /// Must be called after VERA initialization but before starting any trials.
        /// Useful for blocked randomization designs.
        /// </summary>
        /// <param name="blockSize">Number of trials per block</param>
        public static void RandomizeTrialsWithinBlocks(int blockSize)
        {
            if (!initialized)
            {
                Debug.LogWarning("[VERASessionManager] Cannot randomize blocks: VERA not initialized.");
                return;
            }
            VERALogger.Instance?.trialWorkflow?.RandomizeWithinBlocks(blockSize);
        }

        /// <summary>
        /// Randomizes the trial workflow using a specific seed for reproducible results.
        /// Must be called after VERA initialization but before starting any trials.
        /// Useful for debugging or when deterministic randomization is needed.
        /// </summary>
        /// <param name="seed">Random seed value</param>
        public static void RandomizeTrialOrderWithSeed(int seed)
        {
            if (!initialized)
            {
                Debug.LogWarning("[VERASessionManager] Cannot randomize: VERA not initialized.");
                return;
            }
            VERALogger.Instance?.trialWorkflow?.RandomizeWithSeed(seed);
        }
    }
}
