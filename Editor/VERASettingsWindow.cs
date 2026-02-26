#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VERA
{
    internal class VERASettingsWindow : EditorWindow
    {

        private int selectedExperimentIndex;
        private int selectedSiteIndex;
        private List<Experiment> experimentList = null;
        private string timeExperimentsLastRefreshed = string.Empty;
        private Dictionary<string, IVGroup> ivFetchCache = new Dictionary<string, IVGroup>();

        // Foldout states for collapsible sections
        private bool experimentFoldout = true;
        private bool dataRecordingFoldout = true;
        private bool debugPreferencesFoldout = true;
        private bool buildUploadFoldout = true;
        private bool conditionsFoldout = true;

        [MenuItem("VERA/Settings")]
        public static void ShowWindow()
        {
            // Show existing window instance or create a new one
            VERASettingsWindow window = GetWindow<VERASettingsWindow>("VERA Settings");
            window.Show();
        }


        #region ON GUI


        private void OnGUI()
        {
            GUILayout.Label("VERA Settings", EditorStyles.boldLabel);

            // Display differently according to whether user is authenticated or not
            if (PlayerPrefs.GetInt("VERA_UserAuthenticated") == 1)
            {
                if (experimentList == null)
                    LoadSettings();

                // Display the welcome message with the user's name
                string userName = PlayerPrefs.GetString("VERA_UserName", "User");
                GUILayout.Label($"Welcome {userName}!", EditorStyles.wordWrappedLabel);
                GUILayout.Space(10);

                // Display de-authenticate button
                if (GUILayout.Button("Log Out"))
                {
                    VERAAuthenticator.ClearAuthentication();
                }

                // Display connection test button
                if (GUILayout.Button("Am I Connected?"))
                {
                    TestUserConnection(false);
                }

                // Display help button
                if (GUILayout.Button("Open Help Window"))
                {
                    VERAHelpWindow.ShowWindow();
                }

                GUILayout.Space(10); // Add space between sections

                // Add the experiment selection description
                string[] options = experimentList != null ? new string[experimentList.Count] : new string[0];

                // Your Experiment section with foldout
                experimentFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(experimentFoldout, "Your Experiment");
                if (experimentFoldout)
                {
                    if (experimentList == null || experimentList.Count == 0)
                    {
                        EditorGUILayout.HelpBox("No experiments could be found associated with your account. If this is not correct, please try refreshing experiments or re-authenticating.", MessageType.Error);
                        if (GUILayout.Button("Retry Loading Experiments"))
                        {
                            LoadSettings();
                        }
                        GUILayout.Space(10);
                        GUILayout.Label("Troubleshooting:", EditorStyles.boldLabel);
                        GUILayout.Label("- Ensure you are logged in and have a valid network connection.", EditorStyles.wordWrappedLabel);
                        GUILayout.Label("- If the problem persists, contact your system administrator.", EditorStyles.wordWrappedLabel);
                    }
                    else
                    {
                        GUILayout.Label("Use the dropdown below to select from your experiments. Your Unity project can only be linked to a single experiment at a time.", EditorStyles.wordWrappedLabel);
                        GUILayout.Space(5);

                        GUILayout.Label("If you don't see your experiment in the dropdown, use the button below to refresh, and look for your experiment again.", EditorStyles.wordWrappedLabel);
                        GUILayout.Space(5);

                        for (int i = 0; i < experimentList.Count; i++)
                        {
                            options[i] = experimentList[i].name;
                        }
                        int newSelectedExperimentIndex = EditorGUILayout.Popup("Select Experiment", selectedExperimentIndex, options);

                        // Check if the dropdown index has changed
                        if (newSelectedExperimentIndex != selectedExperimentIndex)
                        {
                            selectedExperimentIndex = newSelectedExperimentIndex;
                            VERAAuthenticator.ChangeActiveExperiment(experimentList[selectedExperimentIndex]._id, experimentList[selectedExperimentIndex].name, experimentList[selectedExperimentIndex].isMultiSite, experimentList[selectedExperimentIndex].webXrBuildNumber);
                            selectedSiteIndex = 0;
                            VERAAuthenticator.ChangeActiveSite(experimentList[selectedExperimentIndex].sites[selectedSiteIndex]._id, experimentList[selectedExperimentIndex].sites[selectedSiteIndex].name);
                            SaveSettings();
                            // Generate condition code for the new experiment
                            ConditionGenerator.GenerateAllConditionCsCode(experimentList[selectedExperimentIndex]);
                        }

                        // Display multi-site options, if applicable
                        if (selectedExperimentIndex < experimentList.Count && experimentList[selectedExperimentIndex] != null && experimentList[selectedExperimentIndex].isMultiSite)
                        {
                            // Dropdown menu for site selection
                            List<Site> siteList = experimentList[selectedExperimentIndex].sites;
                            string[] siteOptions = new string[siteList.Count];
                            for (int i = 0; i < siteList.Count; i++)
                            {
                                siteOptions[i] = siteList[i].name;
                            }
                            int newSelectedSiteIndex = EditorGUILayout.Popup("Select Site", selectedSiteIndex, siteOptions);

                            // Check if the site index has changed
                            if (newSelectedSiteIndex != selectedSiteIndex)
                            {
                                selectedSiteIndex = newSelectedSiteIndex;
                                VERAAuthenticator.ChangeActiveSite(experimentList[selectedExperimentIndex].sites[selectedSiteIndex]._id, experimentList[selectedExperimentIndex].sites[selectedSiteIndex].name);
                                SaveSettings();
                            }
                        }

                        // Display refresh experiments button
                        if (GUILayout.Button("Refresh Experiments"))
                        {
                            RefreshExperiments();
                        }

                        // Display last updated time
                        GUILayout.Label("Experiments last updated on " + timeExperimentsLastRefreshed + ".", EditorStyles.wordWrappedLabel);
                    }
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

                // Display data recording type dropdown
                DisplayDataRecordingOptions();

                // Display debug preferences dropdown
                DisplayDebugPreferenceOptions();

                if (options.Length > 0)
                {
                    GUILayout.Space(10);
                    DisplayBuildOptions();
                }

                // Display conditions for the selected experiment
                if (experimentList != null && selectedExperimentIndex >= 0 && selectedExperimentIndex < experimentList.Count)
                {
                    var experiment = experimentList[selectedExperimentIndex];
                    if (experiment.conditions != null && experiment.conditions.Count > 0)
                    {
                        GUILayout.Space(10);
                        conditionsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(conditionsFoldout, "Experiment Conditions");
                        if (conditionsFoldout)
                        {
                            foreach (var iv in experiment.conditions)
                            {
                                if (iv != null && iv.conditions != null)
                                {
                                    GUILayout.Label($"Independent Variable: {iv.ivName}", EditorStyles.miniBoldLabel);
                                    // If any condition lacks an encoding, fetch the authoritative IV group once
                                    bool anyMissingEncoding = iv.conditions.Any(c => string.IsNullOrEmpty(c.encoding));
                                    if (anyMissingEncoding && !ivFetchCache.ContainsKey(iv.ivName))
                                    {
                                        // mark as pending to avoid duplicate calls
                                        ivFetchCache[iv.ivName] = null;
                                        VERAAuthenticator.GetIVGroupConditions(PlayerPrefs.GetString("VERA_ActiveExperiment"), iv.ivName, (fetched) =>
                                        {
                                            if (fetched != null && fetched.conditions != null)
                                            {
                                                // update in-memory experimentList for display
                                                var e = experimentList.FirstOrDefault(x => x._id == PlayerPrefs.GetString("VERA_ActiveExperiment"));
                                                if (e != null)
                                                {
                                                    var existing = e.conditions.FirstOrDefault(x => x.ivName == fetched.ivName);
                                                    if (existing != null)
                                                    {
                                                        existing.conditions = fetched.conditions;
                                                    }
                                                    else
                                                    {
                                                        e.conditions.Add(fetched);
                                                    }
                                                }
                                                ivFetchCache[fetched.ivName] = fetched;
                                            }
                                            else
                                            {
                                                ivFetchCache[iv.ivName] = new IVGroup { ivName = iv.ivName, conditions = new List<Condition>() };
                                            }
                                            // repaint the window to show updated encodings
                                            EditorApplication.delayCall += () => { Repaint(); };
                                        });
                                    }

                                    foreach (var condition in iv.conditions)
                                    {
                                        if (condition != null)
                                        {
                                            // Prefer showing the condition encoding in parentheses (e.g., "Bunnies (bun)")
                                            string displayName = condition.name;
                                            if (!string.IsNullOrEmpty(condition.encoding))
                                            {
                                                displayName = $"{condition.name} ({condition.encoding})";
                                            }
                                            GUILayout.Label($"    {displayName}", EditorStyles.wordWrappedLabel);
                                        }
                                    }
                                }
                            }
                        }
                        EditorGUILayout.EndFoldoutHeaderGroup();
                    }
                    else
                    {
                        GUILayout.Space(10);
                        conditionsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(conditionsFoldout, "Experiment Conditions");
                        if (conditionsFoldout)
                        {
                            GUILayout.Label("No conditions found for this experiment.", EditorStyles.wordWrappedLabel);
                        }
                        EditorGUILayout.EndFoldoutHeaderGroup();
                    }
                }
            }
            else
            {
                // Notify the user is not authenticated
                GUILayout.Label("You are not yet authenticated. Click the button below to authenticate, and be able to use VERA's tools." +
                    "\nMake sure you are connected to the internet before authenticating.", EditorStyles.wordWrappedLabel);
                GUILayout.Space(10);

                // Display authentication button
                if (GUILayout.Button("Authenticate"))
                {
                    experimentList = null;
                    VERAAuthenticator.StartUserAuthentication();
                }
            }
        }


        #endregion


        #region REFRESH EXPERIMENTS


        // Refreshes the displayed experiments
        private void RefreshExperiments()
        {
            // When refreshed, get all user experiments from web
            VERAAuthenticator.GetUserExperiments((result) =>
            {
                string oldActiveId;
                string oldActiveSiteId;

                // Store old active id, to set active experiment to it (if applicable)
                oldActiveId = PlayerPrefs.GetString("VERA_ActiveExperiment");
                oldActiveSiteId = PlayerPrefs.GetString("VERA_ActiveSite");

                // Set the list, and add an option for no active experiment
                experimentList = result;
                if (experimentList != null && experimentList.Count != 0)
                {
                    // Find the index of the element with Value matching oldActiveId
                    selectedExperimentIndex = -1;
                    for (int i = 0; i < experimentList.Count; i++)
                    {
                        if (experimentList[i]._id == oldActiveId)
                        {
                            selectedExperimentIndex = i;
                            break;
                        }
                    }

                    if (selectedExperimentIndex == -1)
                    {
                        // Element not found, update active experiment to a default value (first element)
                        selectedExperimentIndex = 0;

                        // Check if the selected experiment is real
                        if (experimentList != null && experimentList.Count > 0 && experimentList[selectedExperimentIndex] != null)
                        {
                            // Update the active experiment to the real experiment
                            VERAAuthenticator.ChangeActiveExperiment(experimentList[selectedExperimentIndex]._id, experimentList[selectedExperimentIndex].name, experimentList[selectedExperimentIndex].isMultiSite, experimentList[selectedExperimentIndex].webXrBuildNumber);
                        }
                        // If the selected experiment is not real, change the active experiment to nothing
                        else
                        {
                            VERAAuthenticator.ChangeActiveExperiment(null, null, false, -1);
                        }
                    }
                    else
                    {
                        VERAAuthenticator.ChangeActiveExperiment(experimentList[selectedExperimentIndex]._id, experimentList[selectedExperimentIndex].name, experimentList[selectedExperimentIndex].isMultiSite, experimentList[selectedExperimentIndex].webXrBuildNumber);
                    }

                    // Find the index of the old selected site
                    selectedSiteIndex = -1;
                    List<Site> siteList = experimentList[selectedExperimentIndex].sites;
                    for (int i = 0; i < siteList.Count; i++)
                    {
                        if (siteList[i]._id == oldActiveSiteId)
                        {
                            selectedSiteIndex = i;
                            break;
                        }
                    }

                    if (selectedSiteIndex == -1)
                    {
                        // Element not found, update active site to a default value (first element; can be empty site)
                        selectedSiteIndex = 0;
                    }

                    VERAAuthenticator.ChangeActiveSite(experimentList[selectedExperimentIndex].sites[selectedSiteIndex]._id, experimentList[selectedExperimentIndex].sites[selectedSiteIndex].name);
                }
                else
                {
                    VERAAuthenticator.ChangeActiveExperiment(null, null, false, -1);

                    VERADebugger.LogWarning("No experiments could be found associated with your account. Without an active experiment, you will not be able to record data. " +
                        "If this is incorrect, try refreshing experiments or re-authenticating from the VERA Settings window (menu bar -> VERA -> VERA Settings).", "VERA Settings Window");
                }

                timeExperimentsLastRefreshed = DateTime.Now.ToString("MMMM dd, h:mm:ss tt");
                SaveSettings();

                // Generate condition code for the selected experiment
                if (experimentList != null && experimentList.Count > 0 && selectedExperimentIndex >= 0)
                {
                    ConditionGenerator.GenerateAllConditionCsCode(experimentList[selectedExperimentIndex]);
                }
            });
        }


        #endregion


        #region SAVE / LOAD SETTINGS


        // Saves VERA settings to PlayerPrefs (for persistent data)
        private void SaveSettings()
        {
            // Save selectedExperimentIndex
            PlayerPrefs.SetInt("VERA_SelectedExperimentIndex", selectedExperimentIndex);
            PlayerPrefs.SetInt("VERA_SelectedSiteIndex", selectedSiteIndex);

            // Save experimentList as a JSON string
            if (experimentList != null)
            {
                string json = JsonUtility.ToJson(new SerializableList<Experiment>(experimentList));
                PlayerPrefs.SetString("VERA_ExperimentList", json);
            }
        }

        // Loads VERA settings from PlayerPrefs (for persistent data)
        private void LoadSettings()
        {
            // Load selectedExperimentIndex and selectedSiteIndex
            selectedExperimentIndex = PlayerPrefs.GetInt("VERA_SelectedExperimentIndex", 0);
            selectedSiteIndex = PlayerPrefs.GetInt("VERA_SelectedSiteIndex", 0);

            // Load and generate conditions if we have an experiment list saved
            string experimentListJson = PlayerPrefs.GetString("VERA_ExperimentList", null);
            if (!string.IsNullOrEmpty(experimentListJson))
            {
                SerializableList<Experiment> list = JsonUtility.FromJson<SerializableList<Experiment>>(experimentListJson);
                experimentList = list?.List;
                if (experimentList != null && experimentList.Count > 0 && selectedExperimentIndex >= 0)
                {
                    ConditionGenerator.GenerateAllConditionCsCode(experimentList[selectedExperimentIndex]);
                }
            }

            // Load experimentList from JSON string
            string storedJson = PlayerPrefs.GetString("VERA_ExperimentList", null);
            if (!string.IsNullOrEmpty(storedJson))
            {
                var deserializedList = JsonUtility.FromJson<SerializableList<Experiment>>(storedJson);
                experimentList = deserializedList?.List;
            }

            RefreshExperiments();
        }


        #endregion


        #region DATA RECORDING OPTIONS


        /// <summary>
        /// Display labels for the data recording type dropdown options.
        /// </summary>
        private static readonly string[] DataRecordingTypeLabels = new string[]
        {
            "Do not record",
            "Only record locally",
            "Record locally and live"
        };

        /// <summary>
        /// Display descriptions for each data recording type option.
        /// </summary>
        private static readonly string[] DataRecordingTypeDescriptions = new string[]
        {
            "VERA will not record any data locally, nor will it push any data to the VERA web portal. All calls to VERA's logging functions will be ignored.",
            "VERA will save data locally on the device running the experiment. No data will be automatically sent to the VERA web portal.",
            "VERA will save data locally and also push it to the VERA web portal in real-time. This is the recommended setting for most experiments."
        };

        /// <summary>
        /// Displays the data recording type dropdown and its description.
        /// </summary>
        private void DisplayDataRecordingOptions()
        {
            GUILayout.Space(10);
            dataRecordingFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(dataRecordingFoldout, "Data Recording");
            if (dataRecordingFoldout)
            {
                GUILayout.Label("Select how VERA should handle data recording for this experiment.", EditorStyles.wordWrappedLabel);
                GUILayout.Space(5);

                // Get current recording type
                DataRecordingType currentRecordingType = VERAAuthenticator.GetDataRecordingType();
                int currentIndex = (int)currentRecordingType;

                // Ensure index is within valid bounds
                if (currentIndex < 0 || currentIndex >= DataRecordingTypeLabels.Length)
                {
                    currentIndex = (int)DataRecordingType.RecordLocallyAndLive;
                }

                // Display the dropdown
                int newIndex = EditorGUILayout.Popup("Recording Type", currentIndex, DataRecordingTypeLabels);

                // Check if the selection has changed
                if (newIndex != currentIndex)
                {
                    VERAAuthenticator.ChangeDataRecordingType((DataRecordingType)newIndex);
                }

                // Display description for the selected option
                GUILayout.Space(5);
                EditorGUILayout.HelpBox(DataRecordingTypeDescriptions[newIndex], MessageType.Info);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }


        #endregion


        #region DEBUG PREFERENCES


        /// <summary>
        /// Display labels for the debug preference dropdown options.
        /// </summary>
        private static readonly string[] DebugPreferenceLabels = new string[]
        {
            "Verbose",
            "Informative",
            "Minimal",
            "None"
        };

        /// <summary>
        /// Display descriptions for each debug preference option.
        /// </summary>
        private static readonly string[] DebugPreferenceDescriptions = new string[]
        {
            "VERA will output detailed debug logs to the console, including all internal operations and state changes. Useful for debugging issues during development.",
            "VERA will output informative logs including errors, warnings, and important state changes. This is the recommended setting for most use cases.",
            "VERA will only output essential logs such as errors and critical warnings. Use this setting if you want to minimize console output.",
            "VERA will not output any debug logs, warnings, or errors to the console. Use this setting if you want a completely silent experience."
        };

        /// <summary>
        /// Displays the debug preference dropdown and its description.
        /// </summary>
        private void DisplayDebugPreferenceOptions()
        {
            GUILayout.Space(10);
            debugPreferencesFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(debugPreferencesFoldout, "Debug Preferences");
            if (debugPreferencesFoldout)
            {
                GUILayout.Label("Select the level of debug logging VERA should output to the console.", EditorStyles.wordWrappedLabel);
                GUILayout.Space(5);

                // Get current debug preference
                DebugPreference currentDebugPreference = VERAAuthenticator.GetDebugPreference();
                int currentIndex = (int)currentDebugPreference;

                // Ensure index is within valid bounds
                if (currentIndex < 0 || currentIndex >= DebugPreferenceLabels.Length)
                {
                    currentIndex = (int)DebugPreference.Informative;
                }

                // Display the dropdown
                int newIndex = EditorGUILayout.Popup("Debug Level", currentIndex, DebugPreferenceLabels);

                // Check if the selection has changed
                if (newIndex != currentIndex)
                {
                    VERAAuthenticator.ChangeDebugPreference((DebugPreference)newIndex);
                }

                // Display description for the selected option
                GUILayout.Space(5);
                EditorGUILayout.HelpBox(DebugPreferenceDescriptions[newIndex], MessageType.Info);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }


        #endregion


        #region CONNECTION STATUS


        // Called when the Unity editor first launches; if debug level is informative or less, test the connection.
        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            // Check if debug preference is Informative or less (Verbose or Informative)
            DebugPreference debugPref = VERAAuthenticator.GetDebugPreference();
            if (debugPref == DebugPreference.Verbose || debugPref == DebugPreference.Informative)
            {
                TestUserConnection(true);
            }
        }

        // Tests the user's connection, and prints the result in the console
        private static void TestUserConnection(bool canUserDisable)
        {
            string authSuccess = "You are successfully connected to the VERA portal.";
            string unauthError = "You are not connected to the VERA portal, and will not be able " +
                                "to run experiments. Use the \"VERA -> Settings\" menu bar item to connect.";
            if (canUserDisable)
            {
                string disablableMessage = "\n\nYou can disable this message by setting Debug Level to \"Minimal\" or \"None\" in the \"VERA -> Settings\" window.";
                authSuccess += disablableMessage;
                unauthError += disablableMessage;
            }

            // Ensure we are connected / token is not expired
            VERAAuthenticator.IsUserConnected((isConnected) =>
            {
                if (isConnected)
                {
                    if (canUserDisable)
                    {
                        VERADebugger.Log("You are successfully connected to the VERA portal.", "VERA Settings Window", DebugPreference.Informative);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("VERA Connection Status", authSuccess, "Okay");
                    }
                }
                else
                {
                    if (canUserDisable)
                    {
                        VERADebugger.LogError("You are not connected to the VERA portal, and will not be able " +
                                      "to run experiments. Use the \"VERA -> Settings\" menu bar item to connect.\nYou can disable this message in the \"VERA -> Settings\" window.", "VERA Settings Window");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("VERA Connection Status", unauthError, "Okay");
                    }
                    VERAAuthenticator.ClearAuthentication();
                }
            });
        }


        #endregion


        #region BUILD OPTIONS


        // Displays the build options for the user
        private void DisplayBuildOptions()
        {
            buildUploadFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(buildUploadFoldout, "Build Upload");
            if (buildUploadFoldout)
            {
                GUILayout.Label("Once your experiment is completed and you are ready to upload it to the VERA portal, " +
                    "you will need to build for WebXR and send the build to the portal.", EditorStyles.wordWrappedLabel);

                GUILayout.Space(5);

                GUILayout.Label("Press the button below to automatically perform this build and upload " +
                    "process.", EditorStyles.wordWrappedLabel);

                if (GUILayout.Button("Build and Upload Experiment"))
                {
                    // Display a confirmation dialog before proceeding
                    if (EditorUtility.DisplayDialog("Build and Upload Experiment",
                        "This will build your experiment for WebXR and upload it to the VERA portal. Any existing upload will be replaced. " +
                        "Make sure you have selected the correct experiment in the settings window before proceeding. " +
                        "\n\nThis process may take a while.",
                        "Proceed", "Cancel"))
                    {
                        // Call the build and upload method
                        VERABuildUploader.BuildAndUploadExperiment();
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }


        #endregion


        // Get conditions as JSON string for the selected experiment
        public string GetSelectedExperimentConditionsJson()
        {
            if (experimentList == null || selectedExperimentIndex < 0 || selectedExperimentIndex >= experimentList.Count)
                return "";
            var experiment = experimentList[selectedExperimentIndex];
            // Wrap conditions for Unity JsonUtility
            var wrapper = new IVGroupWrapper { conditions = experiment.conditions };
            return JsonUtility.ToJson(wrapper, true);
        }

        [System.Serializable]
        private class IVGroupWrapper
        {
            public List<IVGroup> conditions;
        }


        // Serializable wrapper for List (for saving data as json)
        [System.Serializable]
        public class SerializableList<T>
        {
            public List<T> List;

            public SerializableList() => List = new List<T>();
            public SerializableList(List<T> list) => List = list;
        }


    }
}
#endif