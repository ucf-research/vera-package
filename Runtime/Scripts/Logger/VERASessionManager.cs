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
                Debug.LogWarning("[VERASessionManager] Cannot finalize session because VERA is not initialized.");
                return;
            }

            if (!collecting)
            {
                Debug.LogWarning("[VERASessionManager] Cannot finalize session because data collection is not active.");
                return;
            }

            VERALogger.Instance.FinalizeSession();
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
                Debug.LogWarning("[VERASessionManager] Cannot create CSV entry because VERA is not initialized.");
                return;
            }

            if (!collecting)
            {
                Debug.LogWarning("[VERASessionManager] Cannot create CSV entry because data collection is not active.");
                return;
            }

            VERALogger.Instance.CreateCsvEntry(fileName, eventId, values);
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
    }
}