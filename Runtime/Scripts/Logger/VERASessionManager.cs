// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

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
        /// The current participant's short ID as assigned by the server (e.g. "1", "P1").
        /// </summary>
        public static string participantID { get { return VERALogger.Instance.activeParticipant.participantShortId; } }

        /// <summary>
        /// The numeric portion of the participant short ID (e.g. 1 for both "1" or "P1").
        /// Returns -1 if the short ID is missing or invalid.
        /// </summary>
        public static int participantIDInt
        {
            get
            {
                int id;
                if (int.TryParse(participantID, out id))
                {
                    return id;
                }
                else if (participantID.StartsWith("P") && int.TryParse(participantID.Substring(1), out id))
                {
                    return id;
                }
                else
                {
                    VERADebugger.LogWarning($"Participant ID '{participantID}' is not a valid integer or prefixed with 'P'. Returning -1.", "VERASessionManager");
                    return -1;
                }
            }
        }

        /// <summary>
        /// The numeric portion of the participant short ID (e.g. 1 for "1" or "P1").
        /// Returns -1 if the short ID is missing or invalid.
        /// </summary>
        public static int participantNumber { get { return VERALogger.Instance.activeParticipant.GetNumericParticipantShortId(); } }

        /// <summary>
        /// The 1-based visit/session number for this participant (1 for single-session studies).
        /// Returns -1 if no session has been assigned yet.
        /// </summary>
        public static int sessionNumber { get { return VERALogger.Instance.activeParticipant.sessionNumber; } }

        /// <summary>
        /// The server ID of the current visit/session, when provided by the VERA API.
        /// </summary>
        public static string sessionID { get { return VERALogger.Instance.activeParticipant.sessionId; } }

        /// <summary>
        /// Participant short ID with session suffix when assigned (e.g. "3S2").
        /// Equals participantID when sessionNumber is -1.
        /// </summary>
        public static string participantSessionID { get { return VERALogger.Instance.activeParticipant.GetParticipantSessionLabel(); } }

        /// <summary>
        /// The participant's Prolific PID when one is associated with this session.
        /// Null if the participant has no Prolific ID.
        /// </summary>
        public static string prolificPID { get { return VERALogger.Instance.activeParticipant.prolificID; } }

        /// <summary>
        /// Whether the WebXR hosting page has forwarded its URL query string into this build.
        /// Remains false in the editor and in non-WebGL players.
        /// </summary>
        public static bool areUrlParametersReceived
        {
            get { return VERALogger.Instance != null && VERALogger.Instance.AreUrlParametersReceived; }
        }

        /// <summary>
        /// Raised when the WebXR hosting page has forwarded its URL query string.
        /// A handler subscribed after the parameters have already arrived is invoked immediately.
        /// </summary>
        public static event Action onUrlParametersReceived
        {
            add { VERALogger.Instance?.AddUrlParametersReceivedListener(value); }
            remove { VERALogger.Instance?.RemoveUrlParametersReceivedListener(value); }
        }

        /// <summary>
        /// Returns every URL parameter forwarded from the WebXR hosting page.
        /// Includes siteId and participantId when the landing page resolved them.
        /// Empty until <see cref="onUrlParametersReceived"/> fires.
        /// </summary>
        public static Dictionary<string, string> GetAllUrlParameters()
        {
            if (VERALogger.Instance == null)
                return new Dictionary<string, string>();

            return VERALogger.Instance.GetAllUrlParameters();
        }

        /// <summary>
        /// Returns the decoded value of a URL parameter forwarded from the WebXR hosting page.
        /// Returns null if that key was not present, or if the hosting page has not called in yet.
        /// A landing URL ending in <c>?group=2</c> yields <c>GetUrlParameter("group") == "2"</c>.
        /// </summary>
        /// <param name="key">Parameter name as it appears in the hosting page URL.</param>
        public static string GetUrlParameter(string key)
        {
            return VERALogger.Instance?.GetUrlParameter(key);
        }

        /// <summary>
        /// Whether the VERA logger singleton exists and session APIs can be accessed.
        /// In WebXR builds, this becomes true once the runtime logger is created, before initialization completes.
        /// </summary>
        public static bool IsReady => VERALogger.Instance != null;

        /// <summary>
        /// Whether VERA has been initialized and is ready to start a participant session.
        /// True once the logger is in the scene and authentication/keys are loaded.
        /// Does not mean a participant session has started or that data is being recorded.
        /// </summary>
        public static bool initialized { get { return VERALogger.Instance != null && VERALogger.Instance.initialized; } }

        /// <summary>
        /// Event that is invoked when VERA has been initialized and is ready to start a participant session.
        /// Subscribe to onSessionStart for actions that need an active participant and data recording.
        /// </summary>
        public static UnityEvent onInitialized { get { return VERALogger.Instance.onLoggerInitialized; } }

        /// <summary>
        /// Whether a participant session has been started and data is actively being recorded.
        /// </summary>
        public static bool sessionInProgress { get { return VERALogger.Instance != null && VERALogger.Instance.sessionInProgress; } }

        /// <summary>
        /// Obsolete. Use sessionInProgress instead.
        /// </summary>
        [Obsolete("Use sessionInProgress instead.")]
        public static bool collecting { get { return sessionInProgress; } }

        /// <summary>
        /// Event that is invoked when a participant session has started and data recording is active.
        /// Subscribe to this event for actions that depend on an active participant (IDs, logging, conditions, trials).
        /// </summary>
        public static UnityEvent onSessionStart { get { return VERALogger.Instance.onSessionStart; } }

        /// <summary>
        /// Event that is invoked when a participant session has ended (after FinalizeSession completes).
        /// </summary>
        public static UnityEvent onSessionEnd { get { return VERALogger.Instance.onSessionEnd; } }

        /// <summary>
        /// Invoked when the participant submits a survey in the VERA survey interface.
        /// The argument is that submission's responses, built from the answers just collected.
        /// VERASurveyHelper.onSurveySubmitted forwards to this event.
        /// </summary>
        public static event Action<VERASurveyResults> onSurveySubmitted;

        internal static void NotifySurveySubmitted(VERASurveyResults results)
        {
            if (results == null)
                return;

            onSurveySubmitted?.Invoke(results);
        }

        /// <summary>
        /// Starts a participant session: creates or looks up the participant on the server and begins data collection.
        /// Call this when Auto-Start Participant Sessions is disabled in VERA Settings.
        /// If auto-start is enabled, VERA already starts a session automatically and this method is not needed.
        /// The server auto-assigns a participant ID. To start from a specific researcher-visible pID, use
        /// ManualStartParticipantSessionFromId instead.
        /// In WebXR builds, the portal still supplies the site and participant IDs; those IDs are applied when
        /// the session starts, but data recording does not begin until this method is called (when auto-start is off).
        /// If this is called before the WebXR parameters arrive, the start is deferred until they do.
        /// </summary>
        public static void ManualStartParticipantSession()
        {
            if (!TryPrepareManualSessionStart())
                return;

            VERALogger.Instance.ManualStartParticipantSession();
        }

        /// <summary>
        /// Starts a participant session for a specific researcher-visible participant ID (Live pID, e.g. 1, 2, 3).
        /// If that participant exists, a new session is always started for them (prior visits are not reused).
        /// If they do not exist, they are created with that exact pID, then session 1 is started.
        /// Uses the same participant API routes as ManualStartParticipantSession; this method additionally
        /// passes the ID as manualId.
        /// Call this when Auto-Start Participant Sessions is disabled in VERA Settings.
        /// </summary>
        /// <param name="participantId">The Live pID to fetch or create, as shown in the researcher UI.</param>
        public static void ManualStartParticipantSessionFromId(int participantId)
        {
            if (!TryPrepareManualSessionStart())
                return;

            VERALogger.Instance.ManualStartParticipantSessionFromId(participantId);
        }

        private static bool TryPrepareManualSessionStart()
        {
            if (VERALogger.Instance == null)
            {
                VERADebugger.LogWarning("Cannot start a participant session because VERA is not present in the scene.", "VERASessionManager");
                return false;
            }

            if (sessionInProgress)
            {
                VERADebugger.LogWarning("Cannot start a participant session because a session is already in progress.", "VERASessionManager");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Finalizes the current participant session.
        /// This marks the participant as having completed the experiment and prevents further logging.
        /// It is highly recommended to call this method at the end of the experiment to ensure data integrity.
        /// </summary>
        public static void FinalizeSession()
        {
            if (!sessionInProgress)
            {
                VERADebugger.LogWarning("Cannot finalize session because no participant session is in progress.", "VERASessionManager");
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
        /// <param name="onSurveyComplete">Invoked when the survey is completed by the participant.</param>
        /// <param name="runInWeb">Whether the survey should be run in the web context (i.e. not in VR). Default is false.</param>
        /// <param name="transportToLobby">Whether to temporarily transport the participant to a survey lobby while the survey is active. Default is true.</param>
        /// <param name="heightOffset">How far the survey will be offset vertically from the user's head position. Default is 0.</param>
        /// <param name="distanceOffset">How far the survey will be offset horizontally from the user's head position. Default is 3.</param>
        public static void StartSurvey(VERASurveyInfo surveyToStart, System.Action onSurveyComplete, bool runInWeb = false, bool transportToLobby = true, float heightOffset = 0f, float distanceOffset = 3f)
        {
            Action<VERASurveyResults> callback = onSurveyComplete == null ? null : _ => onSurveyComplete();
            StartSurvey(surveyToStart, callback, runInWeb, transportToLobby, heightOffset, distanceOffset);
        }

        /// <summary>
        /// Starts a survey for the current participant session, based on the provided SurveyInfo.
        /// Whereas you can use this method to start any survey at any time, it is recommended to use the
        /// automatically generated VERASurveyHelper static class to do so in a more convenient manner.
        /// </summary>
        /// <param name="surveyToStart">A VERASurveyInfo scriptable object representing the survey to start.</param>
        /// <param name="onSurveyComplete">Invoked when the survey is completed. The argument is that submission's responses.</param>
        /// <param name="runInWeb">Whether the survey should be run in the web context (i.e. not in VR). Default is false.</param>
        /// <param name="transportToLobby">Whether to temporarily transport the participant to a survey lobby while the survey is active. Default is true.</param>
        /// <param name="heightOffset">How far the survey will be offset vertically from the user's head position. Default is 0.</param>
        /// <param name="distanceOffset">How far the survey will be offset horizontally from the user's head position. Default is 3.</param>
        public static void StartSurvey(VERASurveyInfo surveyToStart, Action<VERASurveyResults> onSurveyComplete, bool runInWeb = false, bool transportToLobby = true, float heightOffset = 0f, float distanceOffset = 3f)
        {
            if (!sessionInProgress)
            {
                VERADebugger.LogWarning("Cannot start survey because no participant session is in progress.", "VERASessionManager");
                return;
            }

            VERALogger.Instance.StartSurvey(surveyToStart, onSurveyComplete, runInWeb, transportToLobby, heightOffset, distanceOffset);
        }

        /// <summary>
        /// Fetches the current participant's completed responses for a survey from the VERA server.
        /// Prefer VERASurveyHelper.GetSurveyResponses, which accepts a generated survey reference.
        /// Leave sessionNumber and instanceNumber unset to return every completion of this survey.
        /// instanceNumber alone selects that run across all sessions.
        /// sessionNumber alone returns every run from that visit.
        /// When both are set, instanceNumber is the run inside that session.
        /// A participant with no matching completion invokes onFailure.
        /// </summary>
        /// <param name="surveyId">Experiment survey id, not the global template id.</param>
        /// <param name="onSuccess">Called with the response set when the request succeeds.</param>
        /// <param name="sessionNumber">Optional longitudinal visit number (1, 2, 3, ...).</param>
        /// <param name="instanceNumber">Optional run number. Counted inside the session when sessionNumber is also set.</param>
        /// <param name="onFailure">Called with an error message when the request fails.</param>
        public static void GetSurveyResponses(string surveyId, Action<VERASurveyResults> onSuccess, int? sessionNumber = null, int? instanceNumber = null, Action<string> onFailure = null)
        {
            if (onSuccess == null)
            {
                VERADebugger.LogWarning("Cannot get survey responses because no success callback was provided.", "VERASessionManager");
                return;
            }

            if (!sessionInProgress)
            {
                const string message = "Cannot get survey responses because no participant session is in progress.";
                VERADebugger.LogWarning(message, "VERASessionManager");
                onFailure?.Invoke("No participant session is in progress.");
                return;
            }

            if (VERALogger.Instance.activeParticipant == null)
            {
                onFailure?.Invoke("No active participant is available.");
                return;
            }

            VERALogger.Instance.StartCoroutine(
                VERALogger.Instance.activeParticipant.GetSurveyResponses(surveyId, onSuccess, sessionNumber, instanceNumber, onFailure));
        }

        /// <summary>
        /// Creates a new arbitrary CSV entry with the specified file name and values.
        /// It is highly recommended to use the generated VERAFile_[FileName].CreateCsvEntry methods instead of this method,
        /// as those methods provide type safety and ensure correct column ordering. Use this function only as a last resort.
        /// </summary>
        /// <param name="fileName">The name of the CSV file to which this entry should be added, without the .csv extension.</param>
        /// <param name="values">The values to be logged in this CSV entry, in the correct order as per the file's configuration.</param>
        public static void CreateArbitraryCsvEntry(string fileName, params object[] values)
        {
            if (!sessionInProgress)
            {
                VERADebugger.LogWarning("Cannot create CSV entry because no participant session is in progress.", "VERASessionManager");
                return;
            }

            VERALogger.Instance.CreateCsvEntry(fileName, values);
        }

        /// <summary>
        /// Uploads a file to a non-CSV file type on the VERA server.
        /// The generated VERAFile_[FileName].UploadFile() methods call this internally.
        /// </summary>
        /// <param name="fileTypeName">The name of the file type to upload to.</param>
        /// <param name="filePath">The full path to the file to upload.</param>
        /// <param name="expectedExtension">The expected file extension (e.g., "json", "txt").</param>
        public static void UploadFileTypeFile(string fileTypeName, string filePath, string expectedExtension)
        {
            if (!sessionInProgress)
            {
                VERADebugger.LogWarning("Cannot upload file because no participant session is in progress.", "VERASessionManager");
                return;
            }

            VERALogger.Instance.UploadFileTypeFile(fileTypeName, filePath, expectedExtension);
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
        /// Applies site and participant IDs, typically from a WebXR portal message.
        /// IDs are always stored so the eventual session attaches to the portal-assigned participant.
        /// The session itself (participant lookup, data recording) starts immediately only when
        /// Auto-Start Participant Sessions is enabled; otherwise wait for ManualStartParticipantSession().
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
                if (!sessionInProgress)
                {
                    VERADebugger.LogWarning("[VERASessionManager] Cannot get current trial because no participant session is in progress.");
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
            if (!sessionInProgress)
            {
                VERADebugger.LogWarning("[VERASessionManager] Cannot get next trial because no participant session is in progress.");
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
            if (!sessionInProgress)
            {
                VERADebugger.LogWarning("[VERASessionManager] Cannot reset trial workflow because no participant session is in progress.");
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
            if (!sessionInProgress)
            {
                VERADebugger.LogWarning("[VERASessionManager] Cannot start trial because no participant session is in progress.");
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
            if (!sessionInProgress)
            {
                VERADebugger.LogWarning("[VERASessionManager] Cannot complete trial because no participant session is in progress.");
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
            if (!sessionInProgress)
            {
                VERADebugger.LogWarning("[VERASessionManager] Cannot abort trial because no participant session is in progress.");
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
            if (!sessionInProgress)
            {
                VERADebugger.LogWarning("[VERASessionManager] Cannot randomize: no participant session is in progress.");
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
            if (!sessionInProgress)
            {
                VERADebugger.LogWarning("[VERASessionManager] Cannot apply Latin square: no participant session is in progress.");
                return;
            }
            int participantId = VERALogger.Instance.activeParticipant.GetNumericParticipantShortId();
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
            if (!sessionInProgress)
            {
                VERADebugger.LogWarning("[VERASessionManager] Cannot apply Latin square: no participant session is in progress.");
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
        ///   int participantNum = VERALogger.Instance.activeParticipant.GetNumericParticipantShortId();
        ///   bool success = VERASessionManager.ApplyLatinSquareCounterbalancing(participantNum, 30);
        ///   if (!success) VERADebugger.LogError("Latin square failed - check console!");
        /// </summary>
        /// <param name="participantNumber">The participant's sequential number (0-indexed). Must be less than totalParticipants.</param>
        /// <param name="totalParticipants">The total number of participants in the study. Must be >= number of conditions.</param>
        /// <returns>True if Latin square ordering was applied successfully, false if validation failed.</returns>
        public static bool ApplyLatinSquareCounterbalancing(int participantNumber, int totalParticipants)
        {
            if (!sessionInProgress)
            {
                VERADebugger.LogWarning("[VERASessionManager] Cannot apply Latin square: no participant session is in progress.");
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
                if (!sessionInProgress)
                {
                    VERADebugger.LogWarning("[VERASessionManager] Cannot get within-subjects IVs: no participant session is in progress.");
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
                if (!sessionInProgress)
                {
                    VERADebugger.LogWarning("[VERASessionManager] Cannot get between-subjects IVs: no participant session is in progress.");
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
                if (!sessionInProgress)
                {
                    VERADebugger.LogWarning("[VERASessionManager] Cannot get randomization type: no participant session is in progress.");
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
                if (!sessionInProgress)
                {
                    VERADebugger.LogWarning("[VERASessionManager] Cannot get trial ordering: no participant session is in progress.");
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
                if (!sessionInProgress)
                {
                    VERADebugger.LogWarning("[VERASessionManager] Cannot get trial distributions: no participant session is in progress.");
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
                if (!sessionInProgress)
                {
                    VERADebugger.LogWarning("[VERASessionManager] Cannot get trial ID: no participant session is in progress.");
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
                if (!sessionInProgress)
                {
                    VERADebugger.LogWarning("[VERASessionManager] Cannot get trial type: no participant session is in progress.");
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
                if (!sessionInProgress)
                {
                    VERADebugger.LogWarning("[VERASessionManager] Cannot get trial conditions: no participant session is in progress.");
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
            if (!sessionInProgress)
            {
                VERADebugger.LogWarning("[VERASessionManager] Cannot randomize blocks: no participant session is in progress.");
                return;
            }
            VERALogger.Instance?.trialWorkflow?.RandomizeWithinBlocks(blockSize);
        }

        /// <summary>
        /// Fetches the current participant's accessibility settings from the VERA server.
        /// </summary>
        /// <param name="onSuccess">Called with the participant's accessibility settings when the request succeeds.</param>
        /// <param name="onFailure">Called with an error message when the request fails.</param>
        public static void FetchAccessibilitySettings(Action<VERAAccessibilitySettings> onSuccess, Action<string> onFailure = null)
        {
            if (!sessionInProgress)
            {
                VERADebugger.LogWarning("Cannot fetch accessibility settings because no participant session is in progress.", "VERASessionManager");
                onFailure?.Invoke("No participant session is in progress.");
                return;
            }

            if (VERALogger.Instance?.activeParticipant == null)
            {
                onFailure?.Invoke("No active participant is available.");
                return;
            }

            VERALogger.Instance.StartCoroutine(
                VERALogger.Instance.activeParticipant.FetchAccessibilitySettings(onSuccess, onFailure));
        }

        /// <summary>
        /// Stores or updates a memory field for the current participant on the VERA server.
        /// Memory persists across sessions for the same participant within an experiment.
        /// Keys are trimmed, case-sensitive, max 128 characters, and cannot contain '/', '\', or control characters.
        /// Values are plain strings up to 2 MB.
        /// </summary>
        /// <param name="keyName">The memory field name (e.g. "Gender").</param>
        /// <param name="keyValue">The value to store (e.g. "Male").</param>
        /// <param name="onSuccess">Called when the value is stored successfully.</param>
        /// <param name="onFailure">Called with an error message when the request fails.</param>
        public static void SetMemoryKey(string keyName, string keyValue, Action onSuccess = null, Action<string> onFailure = null)
        {
            if (!TryPrepareMemoryApi("set a memory key", onFailure))
                return;

            VERALogger.Instance.StartCoroutine(
                VERALogger.Instance.activeParticipant.SetMemoryKey(keyName, keyValue, onSuccess, onFailure));
        }

        /// <summary>
        /// Fetches a single memory field for the current participant from the VERA server.
        /// If the key does not exist, <paramref name="onSuccess"/> is invoked with null (not treated as an error).
        /// </summary>
        /// <param name="keyName">The memory field name to retrieve.</param>
        /// <param name="onSuccess">Called with the stored value, or null if the key does not exist.</param>
        /// <param name="onFailure">Called with an error message when the request fails.</param>
        public static void GetMemoryKey(string keyName, Action<string> onSuccess, Action<string> onFailure = null)
        {
            if (!TryPrepareMemoryApi("get a memory key", onFailure))
                return;

            VERALogger.Instance.StartCoroutine(
                VERALogger.Instance.activeParticipant.GetMemoryKey(keyName, onSuccess, onFailure));
        }

        /// <summary>
        /// Fetches all memory fields (with values) for the current participant from the VERA server.
        /// </summary>
        /// <param name="onSuccess">Called with a dictionary of key → value pairs when the request succeeds.</param>
        /// <param name="onFailure">Called with an error message when the request fails.</param>
        public static void GetAllMemoryKeys(Action<Dictionary<string, string>> onSuccess, Action<string> onFailure = null)
        {
            if (!TryPrepareMemoryApi("get all memory keys", onFailure))
                return;

            VERALogger.Instance.StartCoroutine(
                VERALogger.Instance.activeParticipant.GetAllMemoryKeys(onSuccess, onFailure));
        }

        /// <summary>
        /// Deletes a memory field for the current participant on the VERA server.
        /// Clearing a key that does not exist is treated as success.
        /// </summary>
        /// <param name="keyName">The memory field name to delete.</param>
        /// <param name="onSuccess">Called when the key is cleared (or was already absent).</param>
        /// <param name="onFailure">Called with an error message when the request fails.</param>
        public static void ClearMemoryKey(string keyName, Action onSuccess = null, Action<string> onFailure = null)
        {
            if (!TryPrepareMemoryApi("clear a memory key", onFailure))
                return;

            VERALogger.Instance.StartCoroutine(
                VERALogger.Instance.activeParticipant.ClearMemoryKey(keyName, onSuccess, onFailure));
        }

        private static bool TryPrepareMemoryApi(string actionDescription, Action<string> onFailure)
        {
            if (!sessionInProgress)
            {
                string message = "Cannot " + actionDescription + " because no participant session is in progress.";
                VERADebugger.LogWarning(message, "VERASessionManager");
                onFailure?.Invoke("No participant session is in progress.");
                return false;
            }

            if (VERALogger.Instance?.activeParticipant == null)
            {
                onFailure?.Invoke("No active participant is available.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Stores or replaces an experiment-wide memory field on the VERA server.
        /// The value is shared by every participant in this experiment and persists until it is cleared or the experiment is deleted.
        /// Keys are trimmed, case-sensitive, max 128 characters, and cannot contain '/', '\', or control characters.
        /// Values are plain strings up to 2 MB. The last write wins if two sessions set the same key at the same time.
        /// Use <see cref="AddToExperimentMemoryKey"/> when several sessions need to count.
        /// </summary>
        /// <param name="keyName">The memory field name (e.g. "RecruitmentStatus").</param>
        /// <param name="keyValue">The string value to store. Send numbers as strings, such as "15".</param>
        /// <param name="onSuccess">Called when the value is stored successfully.</param>
        /// <param name="onFailure">Called with an error message when the request fails.</param>
        public static void SetExperimentMemoryKey(string keyName, string keyValue, Action onSuccess = null, Action<string> onFailure = null)
        {
            if (!TryPrepareExperimentMemoryApi("set an experiment memory key", onFailure))
                return;

            VERALogger.Instance.StartCoroutine(
                VERAExperimentMemory.SetKey(keyName, keyValue, onSuccess, onFailure));
        }

        /// <summary>
        /// Fetches a single experiment-wide memory field from the VERA server.
        /// If the key has never been saved, <paramref name="onSuccess"/> is invoked with null.
        /// When the field is used as a counter, treat that null as zero.
        /// </summary>
        /// <param name="keyName">The memory field name to retrieve.</param>
        /// <param name="onSuccess">Called with the stored value, or null if the key does not exist.</param>
        /// <param name="onFailure">Called with an error message when the request fails.</param>
        public static void GetExperimentMemoryKey(string keyName, Action<string> onSuccess, Action<string> onFailure = null)
        {
            if (!TryPrepareExperimentMemoryApi("get an experiment memory key", onFailure))
                return;

            VERALogger.Instance.StartCoroutine(
                VERAExperimentMemory.GetKey(keyName, onSuccess, onFailure));
        }

        /// <summary>
        /// Adds <paramref name="delta"/> to an integer experiment-wide memory field.
        /// Pass a negative delta to subtract. The default delta is 1.
        /// If the key does not exist, it is created and set to <paramref name="delta"/>.
        /// This update is safe when several sessions count the same key. The stored value must be an integer string.
        /// </summary>
        /// <param name="keyName">The memory field name to adjust (e.g. "ParticipantsInConditionA").</param>
        /// <param name="delta">Amount to add. Negative values subtract. Defaults to 1.</param>
        /// <param name="onSuccess">Called with the new total, as a string, when the update succeeds.</param>
        /// <param name="onFailure">Called with an error message when the request fails.</param>
        public static void AddToExperimentMemoryKey(string keyName, int delta = 1, Action<string> onSuccess = null, Action<string> onFailure = null)
        {
            if (!TryPrepareExperimentMemoryApi("add to an experiment memory key", onFailure))
                return;

            VERALogger.Instance.StartCoroutine(
                VERAExperimentMemory.AddToKey(keyName, delta, onSuccess, onFailure));
        }

        /// <summary>
        /// Deletes an experiment-wide memory field on the VERA server.
        /// Clearing a key that does not exist is treated as success.
        /// </summary>
        /// <param name="keyName">The memory field name to delete.</param>
        /// <param name="onSuccess">Called when the key is cleared (or was already absent).</param>
        /// <param name="onFailure">Called with an error message when the request fails.</param>
        public static void ClearExperimentMemoryKey(string keyName, Action onSuccess = null, Action<string> onFailure = null)
        {
            if (!TryPrepareExperimentMemoryApi("clear an experiment memory key", onFailure))
                return;

            VERALogger.Instance.StartCoroutine(
                VERAExperimentMemory.ClearKey(keyName, onSuccess, onFailure));
        }

        private static bool TryPrepareExperimentMemoryApi(string actionDescription, Action<string> onFailure)
        {
            if (!sessionInProgress)
            {
                string message = "Cannot " + actionDescription + " because no participant session is in progress.";
                VERADebugger.LogWarning(message, "VERASessionManager");
                onFailure?.Invoke("No participant session is in progress.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Randomizes the trial workflow using a specific seed for reproducible results.
        /// Must be called after VERA initialization but before starting any trials.
        /// Useful for debugging or when deterministic randomization is needed.
        /// </summary>
        /// <param name="seed">Random seed value</param>
        public static void RandomizeTrialOrderWithSeed(int seed)
        {
            if (!sessionInProgress)
            {
                VERADebugger.LogWarning("[VERASessionManager] Cannot randomize: no participant session is in progress.");
                return;
            }
            VERALogger.Instance?.trialWorkflow?.RandomizeWithSeed(seed);
        }
    }
}
