using UnityEngine;

namespace VERA
{
    internal static class VERADebugger
    {

        /// <summary>
        /// Logs a message to the Unity console with the caller's name.
        /// Only logs according to preferences (i.e. suppresses certain logs if not enabled).
        /// </summary>
        /// <param name="message">Corresponding message log (for example, "Participant started session")</param>
        /// <param name="callerName">Name of the caller (for example, "VERA Participant")</param>
        /// <param name="debugLevel">Level of the debug log. Will be suppressed if the current preference is higher than this level.</param>
        internal static void Log(string message, string callerName, DebugPreference debugLevel = DebugPreference.Verbose)
        {
            DebugPreference currentPreference = GetDebugPref();

            if (currentPreference > debugLevel)
            {
                return;
            }

            if (string.IsNullOrEmpty(callerName))
            {
                Debug.Log(message);
                return;
            }

            Debug.Log("[" + callerName + "] " + message);
        }

        /// <summary>
        /// Logs a message to the Unity console with the caller's name.
        /// Only logs according to preferences (i.e. suppresses certain logs if not enabled).
        /// </summary>
        /// <param name="message">Corresponding message log (for example, "Participant started session")</param>
        internal static void Log(string message)
        {
            Log(message, "", DebugPreference.Verbose);
        }

        /// <summary>
        /// Logs a warning to the Unity console with the caller's name.
        /// Only logs according to preferences (i.e. suppresses warnings if not enabled).
        /// </summary>
        /// <param name="callerName">Name of the caller (for example, "VERA Participant")</param>
        /// <param name="message">Corresponding warning log (for example, "Participant took headset off")</param>
        internal static void LogWarning(string message, string callerName)
        {
            DebugPreference currentPreference = GetDebugPref();

            if (currentPreference > DebugPreference.Minimal)
            {
                return;
            }

            if (string.IsNullOrEmpty(callerName))
            {
                Debug.LogWarning(message);
                return;
            }

            Debug.LogWarning("[" + callerName + "] " + message);
        }

        /// <summary>
        /// Logs a warning to the Unity console with the caller's name.
        /// Only logs according to preferences (i.e. suppresses warnings if not enabled).
        /// </summary>
        /// <param name="message">Corresponding warning log (for example, "Participant took headset off")</param>
        internal static void LogWarning(string message)
        {
            LogWarning(message, "");
        }

        /// <summary>
        /// Logs an error to the Unity console with the caller's name.
        /// Only logs according to preferences (i.e. suppresses errors if not enabled).
        /// </summary>
        /// <param name="callerName">Name of the caller (for example, "VERA Participant")</param>
        /// <param name="message">Corresponding message log (for example, "Participant is null")</param>
        internal static void LogError(string message, string callerName)
        {
            DebugPreference currentPreference = GetDebugPref();

            if (currentPreference > DebugPreference.Minimal)
            {
                return;
            }

            if (string.IsNullOrEmpty(callerName))
            {
                Debug.LogError(message);
                return;
            }

            Debug.LogError("[" + callerName + "] " + message);
        }

        /// <summary>
        /// Logs an error to the Unity console with the caller's name.
        /// Only logs according to preferences (i.e. suppresses errors if not enabled).
        /// </summary>
        /// <param name="message">Corresponding message log (for example, "Participant is null")</param>
        internal static void LogError(string message)
        {
            LogError(message, "");
        }

        private static DebugPreference GetDebugPref()
        {
            DebugPreference currentPreference;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                currentPreference = DebugPreference.Informative; // Default for editor mode
            }
            else
            {
                currentPreference = (DebugPreference)PlayerPrefs.GetInt("VERA_DebugPreference", (int)DebugPreference.Informative);
            }
#else
            currentPreference = (DebugPreference)PlayerPrefs.GetInt("VERA_DebugPreference", (int)DebugPreference.Informative);
#endif

            return currentPreference;
        }
    }
}
