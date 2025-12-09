#if UNITY_EDITOR && VERA_DEV_MODE
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace VERA
{
    internal class VERADevTools : EditorWindow
    {

        private VERAColumnDefinition[] columnDefinitions;
        private string[] definitionNames;
        private int selectedDefinitionIndex;

        [MenuItem("VERA/Dev Tools")]
        public static void ShowWindow()
        {
            // Show existing window instance or create a new one
            VERADevTools window = GetWindow<VERADevTools>("VERA Dev Tools");
            window.Show();
        }

        private void OnGUI()
        {
            if (PlayerPrefs.GetInt("VERA_UserAuthenticated") == 1)
            {
                GUILayout.Label("VERA Dev Tools", EditorStyles.boldLabel);
                GUILayout.Space(10);

                // Simulate a participant
                GUILayout.Label("Click the button below to simulate a participant. " +
                    "This will create a participant on the web portal. The simulated participant will be " +
                    "associated with your active experiment / site.", EditorStyles.wordWrappedLabel);

                if (GUILayout.Button("Create Simulated Participant"))
                {
                    CreateSimParticipant((success) =>
                    {
                        if (success)
                        {
                            SessionState.SetBool("VERA_SimParticipant", true);
                        }
                        else
                        {
                            SessionState.SetBool("VERA_SimParticipant", false);
                        }
                    });
                }

                // Only provide options for creating an entry / finalizing a session if a sim participant is created
                if (SessionState.GetBool("VERA_SimParticipant", false))
                {
                    // Load column definitions
                    columnDefinitions = Resources.LoadAll<VERAColumnDefinition>("");

                    // If at least one column definition exists, provide options for submitting dummy data
                    if (columnDefinitions.Length > 0)
                    {
                        GUILayout.Space(10);
                        GUILayout.Label("Participant Control", EditorStyles.boldLabel);

                        // Create a dummy entry
                        GUILayout.Label("If you would like to upload a \"dummy file\" to a particular file type, " +
                            "use the dropdown below to select the file type you want to upload to. This dropdown selects " +
                            "from the list of file types you have defined for this experiment on the web portal. " +
                            "After you have selected a file type, click the button below to simulate a dummy data entry. " +
                            "This data entry will automatically be uploaded to the portal.", EditorStyles.wordWrappedLabel);

                        definitionNames = columnDefinitions.Select(def => def.fileType != null ? def.fileType.name : "Unnamed Definition").ToArray();

                        selectedDefinitionIndex = EditorGUILayout.Popup("File Type", selectedDefinitionIndex, definitionNames);
                        VERAColumnDefinition selectedDefinition = columnDefinitions[selectedDefinitionIndex];

                        if (selectedDefinition != null)
                        {
                            if (GUILayout.Button("Simulate Dummy Data"))
                            {
                                LogDummyEntry(selectedDefinition);
                            }
                        }
                    }

                    // Add option for finalizing session
                    GUILayout.Space(10);
                    GUILayout.Label("Finalize Simulated Session", EditorStyles.boldLabel);
                    GUILayout.Label("To finalize the simulated participant session and mark them as COMPLETE, " +
                        "click the button below.", EditorStyles.wordWrappedLabel);
                    if (GUILayout.Button("Finalize Simulated Session"))
                    {
                        FinalizeSimParticipant();
                    }
                }
            }
            else
            {
                // Notify the user is not authenticated
                GUILayout.Label("You are not yet authenticated. Please navigate to the \"VERA -> Settings\" " +
                    "menu item to begin authentication.", EditorStyles.wordWrappedLabel);
                GUILayout.Space(10);
            }
        }

        // Creates a simulated participant with no data; only for use internally with these dev toops
        // Calls onComplete when finished, providing true/false whether the participant was successfully created or not
        public static void CreateSimParticipant(Action<bool> onComplete)
        {
            // Check whether user is authenticated or not
            if (PlayerPrefs.GetInt("VERA_UserAuthenticated", 0) == 0)
            {
                Debug.LogError("[VERA Dev Tools] You are not authenticated; cannot create simulated participant. " +
                    "Please authenticate with the VERA -> Settings menu bar item and then try again.");
                return;
            }

            // Create a new UUID for the simulated participant
            string participantUUID = Guid.NewGuid().ToString().Replace("-", "");

            // Set up the request
            string expId = PlayerPrefs.GetString("VERA_ActiveExperiment");
            string siteId = PlayerPrefs.GetString("VERA_ActiveSite");
            string apiKey = PlayerPrefs.GetString("VERA_UserAuthToken");

            string host = VERAHost.hostUrl;
            string url = host + "/api/participants/logs/" + expId + "/" + siteId + "/" + participantUUID;

            byte[] emptyData = new byte[0];

            WWWForm form = new WWWForm();
            form.AddField("experiment_UUID", expId);
            form.AddField("participant_UUID", participantUUID);
            form.AddField("site_UUID", siteId);
            form.AddBinaryData("file", emptyData, expId + "-" + siteId + "-" + participantUUID + ".csv", "text/csv");

            // Send the request
            UnityWebRequest request = UnityWebRequest.Post(url, form);
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            // Send the request
            var operation = request.SendWebRequest();

            // Use EditorApplication.update to check the request's progress
            EditorApplication.update += EditorUpdate;

            void EditorUpdate()
            {
                if (operation.isDone)
                {
                    EditorApplication.update -= EditorUpdate;

                    // Check success
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        // Save the participant ID into PlayerPrefs to be used for other dev tools
                        PlayerPrefs.SetString("VERA_SimParticipant", participantUUID);

                        Debug.Log("[VERA Dev Tools] Successfully created a new simulated participant; " +
                            "options are now available for recording dummy data to this participant.");
                        onComplete?.Invoke(true);
                    }
                    else
                    {
                        Debug.LogError("[VERA Dev Tools] Failed to create a new simulated participant: " + request.error);
                        onComplete?.Invoke(false);
                    }

                    request.Dispose();
                    return;
                }
            }
        }

        // Uploads "dummy data" to a participant, writing one line to each CSV file type
        public static void LogDummyEntry(VERAColumnDefinition columnDef)
        {
            // Paths, keys, and IDs
            string expId = PlayerPrefs.GetString("VERA_ActiveExperiment");
            string siteId = PlayerPrefs.GetString("VERA_ActiveSite");
            string apiKey = PlayerPrefs.GetString("VERA_UserAuthToken");
            string participantUUID = PlayerPrefs.GetString("VERA_SimParticipant");
            string fileTypeId = columnDef.fileType.fileTypeId;

            string host = VERAHost.hostUrl;
            string url = $"{host}/api/participants/{participantUUID}/filetypes/{fileTypeId}/files";

            string baseFilePath = Path.Combine(Application.dataPath, "VERA", "data", expId + "-" + siteId);
            string filePath = baseFilePath + "-" + participantUUID + "-" + columnDef.fileType.fileTypeId + ".csv";

            // Set up the dummy file
            List<string> columnNames = new List<string>();
            List<string> dummyData = new List<string>();
            foreach (VERAColumnDefinition.Column column in columnDef.columns)
            {
                // Add the column name, for the header
                columnNames.Add(column.name);
                // Add the dummy data according to the column definition
                switch (column.type)
                {
                    case VERAColumnDefinition.DataType.Date:
                        dummyData.Add(DateTime.UtcNow.ToString("o"));
                        break;
                    case VERAColumnDefinition.DataType.Number:
                        dummyData.Add("0");
                        break;
                    case VERAColumnDefinition.DataType.String:
                        dummyData.Add("String");
                        break;
                    case VERAColumnDefinition.DataType.Transform:
                        var settings = new JsonSerializerSettings
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        };
                        string value = (JsonConvert.SerializeObject(new
                        {
                            position = new { x = 0f, y = 0f, z = 0f },
                            rotation = new { x = 0f, y = 0f, z = 0f, w = 1f },
                            localScale = new { x = 1f, y = 1f, z = 1f }
                        }, settings));
                        value = value.Replace("\"", "\"\"");
                        if (value.Contains(",") || value.Contains("\n") || value.Contains("\""))
                            value = $"\"{value}\"";
                        dummyData.Add(value);
                        break;
                }
            }

            // Write the file using StreamWriter
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine(string.Join(",", columnNames));
                writer.WriteLine(string.Join(",", dummyData));
                writer.Flush();
            }

            // Read the data associated with the file
            byte[] fileData = null;

            try
            {
                fileData = File.ReadAllBytes(filePath);
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[VERA Dev Tools] Failed to read file due to a sharing violation. Please try again. " + ex);
            }

            // Set up the request
            WWWForm form = new WWWForm();
            form.AddField("participant_UUID", participantUUID);
            form.AddBinaryData("fileUpload", fileData, expId + "-" + siteId + "-" + participantUUID + "-" + fileTypeId + ".csv", "text/csv");

            // Send the request
            UnityWebRequest request = UnityWebRequest.Post(url, form);
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            var operation = request.SendWebRequest();

            // Use EditorApplication.update to check the request's progress
            EditorApplication.update += EditorUpdate;

            void EditorUpdate()
            {
                if (operation.isDone)
                {
                    EditorApplication.update -= EditorUpdate;

                    // Check success
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        // Append the uploaded file name to the "uploadedCSVs.txt" file as a new line
                        string dataPath = Path.Combine(Application.dataPath, "VERA", "data");
                        string uploadRecordFilePath = Path.Combine(dataPath, "uploadedCSVs.txt");

                        // Check if uploadedCSVs exists; create it if it doesn't
                        if (!File.Exists(uploadRecordFilePath))
                        {
                            File.WriteAllText(uploadRecordFilePath, String.Empty);
                        }

                        var uploaded = File.ReadAllLines(uploadRecordFilePath);
                        if (!Array.Exists(uploaded, element => element == Path.GetFileName(filePath)))
                        {
                            File.AppendAllText(uploadRecordFilePath,
                            Path.GetFileName(filePath) + Environment.NewLine);
                        }

                        Debug.Log("[VERA Dev Tools] Successfully submitted dummy data for this participant under file type \"" +
                            columnDef.fileType.name + "\"");
                    }
                    else
                    {
                        Debug.LogError("[VERA Dev Tools] Failed to submit dummy data: " + request.error);
                    }

                    request.Dispose();
                    return;
                }
            }
        }

        // Finalizes the current simulated participant
        public static void FinalizeSimParticipant()
        {
            // Paths, keys, and IDs
            string expId = PlayerPrefs.GetString("VERA_ActiveExperiment");
            string siteId = PlayerPrefs.GetString("VERA_ActiveSite");
            string apiKey = PlayerPrefs.GetString("VERA_UserAuthToken");
            string participantUUID = PlayerPrefs.GetString("VERA_SimParticipant");

            // Send the request
            UnityWebRequest request = UnityWebRequest.Put(
                  $"{VERAHost.hostUrl}/api/participants/progress/{expId}/{siteId}/{participantUUID}/" +
                  $"{VERAParticipantManager.ParticipantProgressState.COMPLETE.ToString()}",
                  new byte[0]
                );
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            var operation = request.SendWebRequest();

            // Use EditorApplication.update to check the request's progress
            EditorApplication.update += EditorUpdate;

            void EditorUpdate()
            {
                if (operation.isDone)
                {
                    EditorApplication.update -= EditorUpdate;

                    if (request.result == UnityWebRequest.Result.Success)
                        Debug.Log($"[VERA Dev Tools] Successfully finalized simulated participant and set their state to COMPLETE.");
                    else
                        Debug.LogError($"[VERA Dev Tools] Failed to finalize simulated session: " + request.error);

                    request.Dispose();
                    return;
                }
            }
        }
    }
}
#endif