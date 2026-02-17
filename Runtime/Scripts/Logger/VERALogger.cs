using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Linq;
using UnityEngine.Events;
using MimeTypes;

namespace VERA
{
    // Helper class to encapsulate web request results
    [System.Serializable]
    internal class VERAWebRequestResult
    {
        public bool success;
        public string jsonResponse;
        public string error;
        public int responseCode;
    }

    internal class VERALogger : MonoBehaviour
    {

        public static VERALogger Instance;

        public VERAParticipantManager activeParticipant { get; private set; }
        private VERAPeriodicSyncHandler periodicSyncHandler;
        private VERAGenericFileHelper genericFileHelper;

        // Keys
        public string apiKey;
        public string experimentUUID;
        public string siteUUID;
        public bool overrideActiveSite = false; // If true, will override the active site with the one provided in the URL
        public string overrideParticipantId = ""; // If set, will override the participant ID with the provided value

        // Paths
        public string baseFilePath = "";
        public string dataPath = "";
        public string genericDataPath = "";
        public VERABuildAuthInfo buildAuthInfo;

        // Experiment management
        public bool sessionFinalized { get; private set; } = false;
        public bool collecting { get; private set; } = false;
        public bool initialized { get; private set; } = false;
        public UnityEvent onLoggerInitialized = new UnityEvent();
        public VERACsvHandler[] csvHandlers { get; private set; }
        private Dictionary<string, string> conditionsCache = new Dictionary<string, string>();

        // Upload events and progress
        private UnityWebRequest trackedUploadRequest;
        public UnityEvent onBeginFileUpload = new UnityEvent();
        public UnityEvent onFileUploadExited = new UnityEvent();


        #region INITIALIZATION


        // Awake, sets up singleton and loads authentication
        // If we are in a WebXR build, does NOT initialize the logger yet;
        // It will initialize via a message from the WebXR build when the site ID is received.
        // If we are in the Unity editor or non-WebXR build, initializes the logger immediately.
        private void Awake()
        {
            // Set up singleton
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Disable MSAA for WebGL builds to improve performance
#if UNITY_WEBGL && !UNITY_EDITOR
        if (QualitySettings.antiAliasing != 0)
        {
            VERADebugger.Log("Disabling MSAA for WebGL build.", "VERA Logger", DebugPreference.Informative);
            QualitySettings.antiAliasing = 0;
        }
#endif

            LoadAuthentication();

            // If we are NOT in a WebXR build (WebGL), initialize immediately
            // WebXR builds will initialize later via a message from the WebXR build
#if UNITY_WEBGL && !UNITY_EDITOR
        VERADebugger.Log("WebXR build detected; initialization will occur via Unity message from the VERA site. Not initializing yet.", "VERA Logger", DebugPreference.Informative);
#else
            VERADebugger.Log("Session is not running in an active WebXR session; initializing logger directly...", "VERA Logger", DebugPreference.Informative);
            StartCoroutine(Initialize());
#endif
        }


        // Initializes the logger, setting up keys, paths, and uploading existing files
        private IEnumerator Initialize()
        {
            if (initialized)
            {
                VERADebugger.LogWarning("Logger is already initialized. Skipping reinitialization.", "VERA Logger");
                yield break;
            }

            yield return SetupKeysAndPaths();

            // If no experiment ID found from above keys and paths call, stop initialization.
            if (experimentUUID == "N/A" || string.IsNullOrEmpty(experimentUUID))
            {
                yield break;
            }

            // Upload any existing unuploaded files
            //UploadExistingUnuploadedFiles(Path.Combine(dataPath, "uploadedCSVs.txt"), dataPath, ".csv");
            //UploadExistingUnuploadedFiles(Path.Combine(dataPath, "uploadedImages.txt"), dataPath, ".png");
            //UploadExistingUnuploadedFiles(Path.Combine(dataPath, "uploadedGeneric.txt"), genericDataPath, "");

            // Setup any file types, CSV logging, and the generic file helper
            SetupFileTypes();
            collecting = true;

            // Auto-setup baseline data collection if not already present
            EnsureBaselineDataLoggingSetup();

            genericFileHelper = gameObject.AddComponent<VERAGenericFileHelper>();
            periodicSyncHandler = gameObject.AddComponent<VERAPeriodicSyncHandler>();
            periodicSyncHandler.StartPeriodicSync();

            yield return InitializeExperimentConditions();

            // Short buffer for subscriptions to register
            yield return null;

            VERADebugger.Log("Logger initialized successfully. Data collection can now begin.", "VERA Logger", DebugPreference.Minimal);
            initialized = true;
            onLoggerInitialized?.Invoke();
        }


        // Loads authentication from streaming assets into PlayerPrefs
        private void LoadAuthentication()
        {
            TextAsset buildAuthFile = Resources.Load<TextAsset>("VERABuildAuthentication");
            if (buildAuthFile != null)
            {
                // Load from file
                string json = buildAuthFile.text;
                VERABuildAuthInfo authInfo = JsonUtility.FromJson<VERABuildAuthInfo>(json);
                buildAuthInfo = authInfo;

                // Set PlayerPrefs to match file
                PlayerPrefs.SetInt("VERA_BuildAuthenticated", authInfo.authenticated ? 1 : 0);
                PlayerPrefs.SetString("VERA_BuildAuthToken", authInfo.buildAuthToken);
                PlayerPrefs.SetString("VERA_ActiveExperiment", authInfo.activeExperiment);
                PlayerPrefs.SetString("VERA_ActiveSite", authInfo.activeSite);
                PlayerPrefs.SetInt("VERA_DataRecordingType", (int)authInfo.dataRecordingType);
                PlayerPrefs.SetInt("VERA_DebugPreference", (int)authInfo.debugPreference);
            }
            else
            {
                VERABuildAuthInfo emptyAuthInfo = new VERABuildAuthInfo
                {
                    authenticated = false,
                    buildAuthToken = String.Empty,
                    activeExperiment = String.Empty,
                    activeExperimentName = String.Empty,
                    activeSite = String.Empty,
                    activeSiteName = String.Empty,
                    isMultiSite = false,
                    currentBuildNumber = -1,
                    dataRecordingType = DataRecordingType.RecordLocallyAndLive,
                    debugPreference = DebugPreference.Informative
                };
                buildAuthInfo = emptyAuthInfo;

                // File not found, authentication likely not set up yet; set default values
                PlayerPrefs.SetInt("VERA_BuildAuthenticated", 0);
                PlayerPrefs.SetString("VERA_BuildAuthToken", emptyAuthInfo.buildAuthToken);
                PlayerPrefs.SetString("VERA_ActiveExperiment", emptyAuthInfo.activeExperiment);
                PlayerPrefs.SetString("VERA_ActiveSite", emptyAuthInfo.activeSite);
                PlayerPrefs.SetInt("VERA_DataRecordingType", (int)emptyAuthInfo.dataRecordingType);
                PlayerPrefs.SetInt("VERA_DebugPreference", (int)emptyAuthInfo.debugPreference);

                // Log unauthenticated user
                VERADebugger.LogError("Your experiment has not been authenticated, and will not be " +
                    "able to record data. Authenticate via the Unity menu bar item, VERA -> Settings; if you are already " +
                    "authenticated, try logging out and reauthenticating.", "VERA Authentication");
            }
        }


        // Sets up the various important keys and paths relating to logging data
        private IEnumerator SetupKeysAndPaths()
        {
            // Keys and IDs
            apiKey = PlayerPrefs.GetString("VERA_BuildAuthToken");
            experimentUUID = PlayerPrefs.GetString("VERA_ActiveExperiment");

            // If we are overriding the active site, do not set the siteUUID from PlayerPrefs
            // When overriding, the siteUUID should be set via Unity message from the WebXR build
            if (!overrideActiveSite)
                siteUUID = PlayerPrefs.GetString("VERA_ActiveSite");

            if (string.IsNullOrEmpty(experimentUUID) || experimentUUID == "N/A")
            {
                VERADebugger.LogError("You do not have an active experiment. Without an active experiment, data cannot be collected using VERA." +
                    " In the Menu Bar at the top of the editor, please click on the 'VERA' dropdown, then select 'Settings' to pick an active experiment.", "VERA Logger");
                yield break;
            }

            // Participant
            activeParticipant = gameObject.AddComponent<VERAParticipantManager>();

            // If overriding participant ID, use it to create/retrieve participant
            if (!string.IsNullOrEmpty(overrideParticipantId))
                yield return activeParticipant.CreateParticipant(overrideParticipantId);
            else
                yield return activeParticipant.CreateParticipant();

            // CSV and file paths
            if (baseFilePath == "")
            {
#if UNITY_EDITOR
                // Save to Assets/VERA/data
                baseFilePath = Path.Combine(Application.dataPath, "VERA", "data", experimentUUID + "-" + siteUUID);
                dataPath = Path.Combine(Application.dataPath, "VERA", "data");
                genericDataPath = Path.Combine(Application.dataPath, "VERA", "data", "generic");
#else
            baseFilePath = Path.Combine(Application.persistentDataPath, experimentUUID + "-" + siteUUID);
            dataPath = Path.Combine(Application.persistentDataPath);
            genericDataPath = Path.Combine(Application.persistentDataPath, "generic");
#endif
            }
        }


        // Tests the connection to the VERA server via simple API call; prints result.
        public IEnumerator TestConnection()
        {
            string host = VERAHost.hostUrl;
            string url = host + "/api/";

            UnityWebRequest www = UnityWebRequest.Get(url);
            www.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
                VERADebugger.Log("You are connected to the VERA servers.", "VERA Connection", DebugPreference.Informative);
            else
                VERADebugger.LogError("Failed to connect to the VERA servers: " + www.error, "VERA Connection");
        }

        // Manually initializes the logger with the specified site ID and participant ID.
        // Most often will be called from VERASessionManager, prompted from the WebXR build.
        // If site is provided, override the active site with the provided ID; otherwise, use the active site from PlayerPrefs.
        // If participant is provided, override the active participant with the provided ID; otherwise, create a new participant.
        // Continues to initialize the logger after setting the site ID.
        public void ManualInitialization(string siteId, string participantId)
        {
            VERADebugger.Log("Overriding initialization with given parameters...", "VERA Logger", DebugPreference.Informative);

            if (!string.IsNullOrEmpty(siteId))
            {
                overrideActiveSite = true;
                siteUUID = siteId;
            }

            if (!string.IsNullOrEmpty(participantId))
            {
                overrideParticipantId = participantId;
            }

            StartCoroutine(Initialize());
        }


        #endregion


        #region WEB REQUEST HELPERS


        // Generalized function to send GET request and return result via callback
        public IEnumerator SendGetRequestCoroutine(string url, System.Action<VERAWebRequestResult> onComplete)
        {
            UnityWebRequest request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return request.SendWebRequest();

            VERAWebRequestResult result = new VERAWebRequestResult
            {
                success = request.result == UnityWebRequest.Result.Success,
                jsonResponse = request.result == UnityWebRequest.Result.Success ? request.downloadHandler.text : null,
                error = request.result != UnityWebRequest.Result.Success ? request.error : null,
                responseCode = (int)request.responseCode
            };

            if (!result.success)
            {
                VERADebugger.LogError($"GET request failed for URL: {url}. Error: {result.error}. Response Code: {result.responseCode}", "VERA Logger");
            }

            request.Dispose();
            onComplete?.Invoke(result);
        }


        #endregion


        #region FILE TYPE / COLUMN DEFINITION MANAGEMENT


        // Sets up the file types and associated column definitions for this experiment
        private void SetupFileTypes()
        {
            // Validate and fix column definitions before loading them
            VERAColumnValidator.ValidateAndFixColumnDefinitions();

            // Load column definitions
            VERAColumnDefinition[] columnDefinitions = Resources.LoadAll<VERAColumnDefinition>("");
            if (columnDefinitions == null || columnDefinitions.Length == 0)
            {
                VERADebugger.Log("No CSV file types found; continuing assuming no CSV logging will occur.", "VERA Logger", DebugPreference.Informative);
                return;
            }

            csvHandlers = new VERACsvHandler[columnDefinitions.Length];

            // For each column definition, set it up as its own CSV handler
            for (int i = 0; i < csvHandlers.Length; i++)
            {
                csvHandlers[i] = gameObject.AddComponent<VERACsvHandler>();
                csvHandlers[i].Initialize(columnDefinitions[i]);
            }
        }

        // Ensures baseline data logging is automatically set up if not already present
        private void EnsureBaselineDataLoggingSetup()
        {
            // Check if there's already a VERABaselineAutoSetup component in the scene
#if UNITY_2023_1_OR_NEWER
            VERABaselineAutoSetup existingSetup = FindAnyObjectByType<VERABaselineAutoSetup>();
#else
        VERABaselineAutoSetup existingSetup = FindObjectOfType<VERABaselineAutoSetup>();
#endif

            if (existingSetup == null)
            {
                // No baseline setup found, create one automatically
                VERADebugger.Log("Auto-creating baseline data logging setup...", "VERA Logger", DebugPreference.Verbose);

                // Add the component to the VERALogger GameObject
                VERABaselineAutoSetup autoSetup = gameObject.AddComponent<VERABaselineAutoSetup>();

                // Configure with sensible defaults
                autoSetup.autoCreateBaselineLogger = true;
                autoSetup.startOnExperimentBegin = true;
                autoSetup.fallbackAutoStart = true;

                // TODO: RESET
                // autoSetup.defaultSamplingRate = 30;

                VERADebugger.Log("Baseline data logging setup created automatically. " +
                         "VR baseline data will be collected every frame when experiment starts.", "VERA Logger", DebugPreference.Informative);
            }
            else
            {
                VERADebugger.Log("Baseline data logging setup already exists - using existing configuration.", "VERA Logger", DebugPreference.Informative);
            }
        }


        // Finds a csv handler by provided FileType name
        // Ignores extension, returns null on failure to find
        public VERACsvHandler FindCsvHandlerByFileName(string name)
        {
            if (!collecting)
            {
                VERADebugger.LogWarning("Cannot access CSV handers - collection is not yet enabled.", "VERA Logger");
                return null;
            }
            if (!initialized)
            {
                VERADebugger.LogWarning("Cannot access CSV handers - logger is not yet initialized.", "VERA Logger");
                return null;
            }

            // Remove extension to check for name directly with no extension
            if (name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - 4);
            }

            // Find the CSV handler that matches this name
            foreach (VERACsvHandler csvHandler in csvHandlers)
            {
                if (csvHandler.columnDefinition.fileType.name == name)
                {
                    return csvHandler;
                }
            }

            // If no column definition is found, return null
            return null;
        }


        #endregion


        #region ENTRY LOGGING AND SUBMISSION


        /// <summary>
        /// Gets the current data recording type set for this build.
        /// </summary>
        /// <returns>DataRecordingType enum value representing the current data recording type.</returns>
        public DataRecordingType GetDataRecordingType()
        {
            int recordingTypeInt = PlayerPrefs.GetInt("VERA_DataRecordingType", (int)DataRecordingType.RecordLocallyAndLive);
            return (DataRecordingType)recordingTypeInt;
        }


        // Creates a CSV entry for the given file type
        public void CreateCsvEntry(string fileTypeName, int eventId, params object[] values)
        {
            if (!collecting || !initialized || GetDataRecordingType() == DataRecordingType.DoNotRecord)
                return;

            VERACsvHandler csvHandler = FindCsvHandlerByFileName(fileTypeName);
            if (csvHandler == null)
            {
                VERADebugger.LogError("No file type could be found associated with provided name \"" +
                  fileTypeName + "\"; cannot log CSV entry to the file as desired.", "VERA Logger");
                return;
            }

            csvHandler.CreateEntry(eventId, values);
        }

        // Creates a CSV entry for the given file type without an eventId (for baseline telemetry)
        public void CreateCsvEntry(string fileTypeName, params object[] values)
        {
            if (!collecting || !initialized || GetDataRecordingType() == DataRecordingType.DoNotRecord)
                return;

            VERACsvHandler csvHandler = FindCsvHandlerByFileName(fileTypeName);
            if (csvHandler == null)
            {
                VERADebugger.LogError("No file type could be found associated with provided name \"" +
                  fileTypeName + "\"; cannot log CSV entry to the file as desired.", "VERA Logger");
                return;
            }

            csvHandler.CreateEntry(values);
        }


        // Submits all CSVs that are currently being recorded
        public void SubmitAllCSVs(bool usePartialSync = true)
        {
            foreach (VERACsvHandler csvHandler in csvHandlers)
            {
                csvHandler.StartCoroutine(csvHandler.SubmitFileWithRetry(usePartial: usePartialSync));
            }
        }


        // Submits the file for a given file type by name
        public void SubmitCsvFile(string fileTypeName, bool triggerUploadProgress = false, bool usePartialSync = false)
        {
            VERACsvHandler csvHandler = FindCsvHandlerByFileName(fileTypeName);

            if (csvHandler == null)
            {
                VERADebugger.LogError("No file type could be found associated with provided name \"" +
                  fileTypeName + "\"; cannot submit the CSV as desired.", "VERA Logger");
                return;
            }

            if (triggerUploadProgress)
            {
                StartCoroutine(SubmitFileWithProgressCoroutine(csvHandler, usePartialSync: usePartialSync));
            }
            else
            {
                csvHandler.StartCoroutine(csvHandler.SubmitFileWithRetry(usePartial: usePartialSync));
            }
        }


        // Submits file from given CSV handler, and triggers events (to show progress)
        public IEnumerator SubmitFileWithProgressCoroutine(VERACsvHandler csvHandler, bool usePartialSync = false)
        {
            onBeginFileUpload?.Invoke();
            trackedUploadRequest = csvHandler.activeWebRequest;
            yield return csvHandler.StartCoroutine(csvHandler.SubmitFileWithRetry(usePartial: usePartialSync));
            onFileUploadExited?.Invoke();
        }


        // Gets the upload progress of the tracked upload request
        public float UploadProgress
        {
            get
            {
                if (trackedUploadRequest == null || trackedUploadRequest.isDone) return -1;
                return trackedUploadRequest.uploadProgress;
            }
        }


        // Gets the upload file size of the tracked upload request in bytes
        public int uploadFileSizeBytes
        {
            get
            {
                if (trackedUploadRequest == null || trackedUploadRequest.isDone) return -1;
                return trackedUploadRequest.uploadHandler.data.Length;
            }
        }


        // Called when a CSV file is fully uploaded
        // Append the CSV file's name to the uploaded records so we know to not upload it again
        public void OnCsvFullyUploaded(string csvFilePath)
        {
            // Append the uploaded file name to the "uploadedCSVs.txt" file as a new line
            string uploadRecordFilePath = Path.Combine(dataPath, "uploadedCSVs.txt");
            var uploaded = File.ReadAllLines(uploadRecordFilePath);
            if (!Array.Exists(uploaded, element => element == Path.GetFileName(csvFilePath)))
            {
                File.AppendAllText(uploadRecordFilePath,
                Path.GetFileName(csvFilePath) + Environment.NewLine);
            }
        }


        #endregion


        #region EXISTING FILES SUBMISSION


        private void UploadExistingUnuploadedFiles(string alreadyUploadedTxtPath, string existingFilesFolderPath, string extensionToUpload)
        {
            // Create directories if necessary
            if (!Directory.Exists(existingFilesFolderPath))
            {
                VERADebugger.Log($"Directory [{existingFilesFolderPath}] does not exist, creating it", "VERA Logger", DebugPreference.Informative);
                Directory.CreateDirectory(existingFilesFolderPath);
            }
            if (!File.Exists(alreadyUploadedTxtPath))
            {
                VERADebugger.Log($"File [{alreadyUploadedTxtPath}] does not exist, creating it", "VERA Logger", DebugPreference.Informative);
                FileStream f = File.Create(alreadyUploadedTxtPath);
                f.Close();
            }

            // Get existing and uploaded files
            string[] existingFiles = Directory.GetFiles(existingFilesFolderPath, $"*{extensionToUpload}");
            string[] alreadyUploadedFiles = File.ReadAllLines(alreadyUploadedTxtPath);

            // For each existing file, if it is not already uploaded, upload it
            foreach (string file in existingFiles)
            {
                if (file != "" && !Array.Exists(alreadyUploadedFiles, element => element == Path.GetFileName(file)))
                {
                    // Ignore and delete the file if it is partial
                    if (file.Contains("partial"))
                    {
                        VERADebugger.Log("Found a partial sync file \"" + file + "\"; deleting file...", "VERA Logger", DebugPreference.Informative);
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception e)
                        {
                            VERADebugger.LogError("Failed to delete partial sync file \"" + file + "\": " + e.Message, "VERA Logger");
                        }
                        continue;
                    }

                    VERADebugger.Log("Found an unuploaded file \"" + file + "\"; uploading file...", "VERA Logger", DebugPreference.Informative);

                    switch (extensionToUpload)
                    {
                        case ".csv":
                            StartCoroutine(SubmitExistingCSVCoroutine(file));
                            break;
                        case ".png":
                            SubmitImageFile(file);
                            break;
                        default:
                            SubmitGenericFile(file);
                            break;
                    }
                }
            }
        }


        // Submits an existing CSV (from a previous session) based on its file path
        private IEnumerator SubmitExistingCSVCoroutine(string csvFilePath)
        {
            var basename = Path.GetFileName(csvFilePath);

            // Get the IDs associated with this file
            string host = VERAHost.hostUrl;
            string file_participant_UDID;
            string fileTypeId;
            if (csvFilePath.Length > 0 && csvFilePath.Contains("-"))
            {
                // Files are in format expId-siteId-partId-fileTypeId.csv
                // To get participant ID, it will be the third element separated by -'s; fileTypeId will be the fourth.
                string[] split = basename.Split('-');

                if (split.Length == 4)
                {
                    file_participant_UDID = split[2];
                    fileTypeId = split[3].Split('.')[0];
                }
                else
                {
                    VERADebugger.LogError("Invalid file name", "VERA Logger");
                    yield break;
                }
            }
            else
            {
                VERADebugger.LogError("Invalid file name", "VERA Logger");
                yield break;
            }

            string url = $"{host}/api/participants/{file_participant_UDID}/filetypes/{fileTypeId}/files";
            byte[] fileData = null;

            // Read the file's data
            yield return ReadBinaryDataFile(csvFilePath, (result) => fileData = result);
            if (fileData == null)
            {
                VERADebugger.Log("No file data to submit", "VERA Logger", DebugPreference.Informative);
                yield break;
            }

            // Set up the request
            WWWForm form = new WWWForm();
            form.AddField("experiment_UUID", experimentUUID);
            form.AddField("participant_UUID", file_participant_UDID);
            form.AddField("site_UUID", siteUUID);
            form.AddBinaryData("fileUpload", fileData, experimentUUID + "-" + siteUUID + "-" + file_participant_UDID + "-" + fileTypeId + ".csv", "text/csv");

            // Send the request
            UnityWebRequest request = UnityWebRequest.Post(url, form);
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return request.SendWebRequest();

            // Check success
            if (request.result == UnityWebRequest.Result.Success)
            {
                VERADebugger.Log("Successfully uploaded existing file \"" + csvFilePath + "\".", "VERA Logger", DebugPreference.Informative);
                OnCsvFullyUploaded(csvFilePath);
            }
            else
            {
                VERADebugger.LogError("Failed to upload existing file \"" + csvFilePath + "\".", "VERA Logger");
            }
        }


        #endregion


        #region SESSION MANAGEMENT

        // Finalizes the current experiment session. Should be called when the experiment is "complete".
        public void FinalizeSession()
        {
            if (sessionFinalized)
                return;

            sessionFinalized = true;

            DataRecordingType recordingType = GetDataRecordingType();
            if (recordingType == DataRecordingType.DoNotRecord)
            {
                return;
            }

            VERADebugger.Log("Session finalized; marking COMPLETE and doing final sync.", "VERA Logger", DebugPreference.Minimal);

            StartCoroutine(SyncFilesThenComplete());
        }

        private IEnumerator SyncFilesThenComplete()
        {
            // Finish periodic sync and perform one final sync
            yield return StartCoroutine(periodicSyncHandler.FinalSync());

            // Wait for all active upload requests to complete
            bool allUploadsCompleted = false;
            while (!allUploadsCompleted)
            {
                allUploadsCompleted = true;
                foreach (VERACsvHandler csvHandler in csvHandlers)
                {
                    if (!csvHandler.finalEntryUploaded)
                    {
                        allUploadsCompleted = false;
                    }
                }

                if (!allUploadsCompleted)
                {
                    // If there is an active upload request, wait for it to complete
                    VERADebugger.Log("Waiting for active upload requests to complete before marking participant as COMPLETE.", "VERA Logger", DebugPreference.Verbose);
                    yield return new WaitForSeconds(0.5f);
                }
                else
                {
                    VERADebugger.Log("All active upload requests completed. Marking participant as COMPLETE.", "VERA Logger", DebugPreference.Verbose);
                }
            }

            if (!activeParticipant.IsInFinalizedState())
                yield return StartCoroutine(activeParticipant.RetryableChangeProgress(VERAParticipantManager.ParticipantProgressState.COMPLETE));
            else
                VERADebugger.LogWarning("Participant is already in a finalized state before session completion. No action taken.", "VERA Logger");

            VERADebugger.Log("All finalization tasks have completed successfully.", "VERA Logger", DebugPreference.Minimal);
#if UNITY_WEBGL && !UNITY_EDITOR
            // In WebGL builds, notify the site itself that the session is complete
            VERADebugger.Log("Notifying VERA portal that session has been finalized.", "VERA Logger", DebugPreference.Informative);   
#if UNITY_2021_1_OR_NEWER
            Application.ExternalEval("window.unityMessageHandler('FINALIZE_SESSION')");
#else
            Application.ExternalCall("unityMessageHandler", "FINALIZE_SESSION");
#endif
#endif
        }


        #endregion


        #region CONDITION MANAGEMENT


        // Gets the selected value of an independent variable by name
        // Gets the LOCAL cached version, NOT the server version
        // Local should always be up to date unless the server was changed externally
        public string GetSelectedIVValue(string ivName)
        {
            if (conditionsCache.ContainsKey(ivName))
            {
                return conditionsCache[ivName];
            }

            VERADebugger.LogError("No independent variable found with name \"" + ivName + "\"; cannot get selected value.", "VERA Logger");
            return null;
        }

        // Sets the selected value of an independent variable by name
        // Sets the LOCAL cached version, then syncs to the server asynchronously
        public string SetSelectedIVValue(string ivName, string ivValue)
        {
            if (conditionsCache.ContainsKey(ivName))
            {
                conditionsCache[ivName] = ivValue;
                StartCoroutine(SyncParticipantCondition(ivName));
                return ivValue;
            }

            VERADebugger.LogError("No independent variable found with name \"" + ivName + "\"; cannot set selected value.", "VERA Logger");
            return null;
        }

        // Initializes participant's conditions according to experiment's current values
        // Should only be called once at the beginning of the experiment
        private IEnumerator InitializeExperimentConditions()
        {
            // Get the experiment conditions from the server
            string url = $"{VERAHost.hostUrl}/api/experiments/{experimentUUID}/conditions";
            VERAWebRequestResult result = null;
            yield return StartCoroutine(SendGetRequestCoroutine(url, (r) => result = r));

            if (result == null || !result.success)
            {
                VERADebugger.LogError("Failed to get experiment conditions from server; cannot initialize conditions.", "VERA Logger");
                yield break;
            }

            // Parse the response
            GetConditionsResponse resp = null;
            try
            {
                resp = JsonUtility.FromJson<GetConditionsResponse>(result.jsonResponse);
            }
            catch (Exception e)
            {
                VERADebugger.LogError("Failed to parse experiment conditions response: " + e.Message, "VERA Logger");
                yield break;
            }

            if (resp == null || !resp.success || resp.independentVariables == null || resp.independentVariables.Length == 0)
            {
                VERADebugger.Log("No independent variables found in experiment conditions response; continuing assuming experiment has no conditions.", "VERA Logger", DebugPreference.Informative);
                yield break;
            }

            // Initialize the conditions cache with the experiment's conditions
            conditionsCache.Clear();
            bool ivChanged = false;
            foreach (IVInfo iv in resp.independentVariables)
            {
                if (iv.selectedCondition != null && iv.selectedCondition.name != null && iv.selectedCondition.name != "")
                {
                    conditionsCache.Add(iv.ivName, iv.selectedCondition.name);
                }
                else
                {
                    VERADebugger.LogWarning("Independent variable \"" + iv.ivName + "\" has no selected condition value; defaulting to first condition.", "VERA Logger");
                    if (iv.conditions != null && iv.conditions.Length > 0 && iv.conditions[0].name != null && iv.conditions[0].name != "")
                    {
                        conditionsCache.Add(iv.ivName, iv.conditions[0].name);
                        ivChanged = true;
                    }
                    else
                    {
                        VERADebugger.LogError("Independent variable \"" + iv.ivName + "\" has no conditions defined; skipping.", "VERA Logger");
                    }
                }
            }

            if (ivChanged)
            {
                VERADebugger.Log("One or more independent variables had no selected condition; syncing updated conditions to server.", "VERA Logger", DebugPreference.Informative);
                foreach (string ivName in conditionsCache.Keys)
                {
                    yield return StartCoroutine(SyncParticipantCondition(ivName));
                }
            }

            VERADebugger.Log("Experiment conditions initialized successfully with " + conditionsCache.Count + " independent variables. Conditions: " + GetExperimentConditions(), "VERA Logger", DebugPreference.Informative);
        }

        // Syncs a single independent variable's selected condition to the server
        private IEnumerator SyncParticipantCondition(string ivName)
        {
            if (!conditionsCache.ContainsKey(ivName))
            {
                VERADebugger.LogError("No independent variable found with name \"" + ivName + "\"; cannot sync condition to server.", "VERA Logger");
                yield break;
            }

            string ivValue = conditionsCache[ivName];

            // Prepare the request
            string url = $"{VERAHost.hostUrl}/api/experiments/{experimentUUID}/conditions/{ivName}";
            // Use simple string formatting for WebGL compatibility (no reflection emit)
            string jsonPayload = "{\"selected\":\"" + ivValue.Replace("\"", "\\\"") + "\"}";
            // Alternative: string jsonPayload = $"{{\"selected\":\"{ivValue.Replace("\"", "\\\"")}\"}}";


            // Send the request
            UnityWebRequest request = new UnityWebRequest(url, "PATCH");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                VERADebugger.Log("Successfully synced condition for IV \"" + ivName + "\" with value \"" + ivValue + "\" to server.", "VERA Logger", DebugPreference.Informative);
            }
            else
            {
                VERADebugger.LogError("Failed to sync condition for IV \"" + ivName + "\" to server. Error: " + request.error, "VERA Logger");
            }
        }

        // Returns a string representing the experiment conditions
        // JSON format - lists all IVs and their values
        public string GetExperimentConditions()
        {
            // Manual JSON serialization for WebGL compatibility (no reflection emit)
            if (conditionsCache == null || conditionsCache.Count == 0)
                return "{}";

            var pairs = new List<string>();
            foreach (var kvp in conditionsCache)
            {
                string key = kvp.Key.Replace("\"", "\\\"");
                string value = kvp.Value.Replace("\"", "\\\"");
                pairs.Add($"\"{key}\":\"{value}\"");
            }
            return "{" + string.Join(",", pairs.ToArray()) + "}";
        }

        [System.Serializable]
        private class GetConditionsResponse
        {
            public bool success;
            public IVInfo[] independentVariables;
            public int count;
        }

        [System.Serializable]
        private class IVInfo
        {
            public string ivName;
            public ConditionsInfo[] conditions;
            public ConditionsInfo selectedCondition;
            public int conditionCount;
        }

        [System.Serializable]
        private class ConditionsInfo
        {
            public string name;
            public string encoding;
        }


        #endregion


        #region QUIT HANDLING


        // OnApplicationQuit, check if participant needs to be marked as incomplete
        private void OnApplicationQuit()
        {
            // If participant is still in progress, mark as incomplete
            if (!sessionFinalized && !activeParticipant.IsInFinalizedState())
            {
                VERADebugger.LogWarning("App is quitting before participant is finalized. Marking participant as INCOMPLETE.", "VERA Logger");
                activeParticipant.SetParticipantProgress(VERAParticipantManager.ParticipantProgressState.INCOMPLETE);
                periodicSyncHandler.StopPeriodicSync();
                periodicSyncHandler.CleanupPartialSyncFiles();
            }
        }


        #endregion


        #region OTHER HELPERS


        // Submits a generic file to a participant, unassociated with a file type
        public void SubmitGenericFile(string filePath, string timestamp = null, byte[] fileData = null, bool moveFileToUploadDirectory = true)
        {
            genericFileHelper.StartCoroutine(genericFileHelper.SubmitGenericFileCoroutine(filePath, timestamp, fileData, moveFileToUploadDirectory));
        }


        // Submits an image file to a participant, unassociated with a file type
        public void SubmitImageFile(string filePath, string timestamp = null, byte[] fileData = null)
        {
            genericFileHelper.StartCoroutine(genericFileHelper.SubmitImageFileCoroutine(filePath, timestamp, fileData));
        }


        // Reads a binary data file and invokes the callback with the read data
        public IEnumerator ReadBinaryDataFile(string filePath, Action<byte[]> callback)
        {
            bool fileReadSuccess = false;
            byte[] fileData = null;

            // Retry mechanism to handle sharing violation
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    //VERADebugger.Log("Reading file: " + filePath, "VERA Logger", DebugPreference.Verbose);
                    fileData = File.ReadAllBytes(filePath);
                    fileReadSuccess = true;
                    break;
                }
                catch (IOException ex)
                {
                    VERADebugger.LogWarning($"Attempt {ex.Message} {i + 1}: Failed to read file due to sharing violation. Retrying...", "VERA Logger");
                }

                if (!fileReadSuccess)
                {
                    yield return new WaitForSeconds(0.1f); // Add a small delay before retrying
                }
            }

            if (!fileReadSuccess)
            {
                VERADebugger.LogError("Failed to read the file after multiple attempts.", "VERA Logger");
                yield break;
            }

            if (fileData.Length > 50 * 1024 * 1024)
            {
                VERADebugger.LogError("File \"" + filePath + "\" size exceeds the limit of 50MB. File will not be uploaded.", "VERA Logger");
                yield break;
            }

            callback?.Invoke(fileData);
        }


        #endregion


    }


    /// <summary>
    /// Defines how VERA should handle data recording.
    /// </summary>
    internal enum DataRecordingType
    {
        /// <summary>
        /// VERA will not record any data. All logging calls will be ignored.
        /// </summary>
        DoNotRecord = 0,

        /// <summary>
        /// VERA will only save data locally. No data will be sent to the VERA web portal.
        /// </summary>
        OnlyRecordLocally = 1,

        /// <summary>
        /// VERA will save data locally and also push it to the VERA web portal in real-time.
        /// </summary>
        RecordLocallyAndLive = 2
    }

    /// <summary>
    /// Defines the level of debug logging VERA should output.
    /// </summary>
    public enum DebugPreference
    {
        /// <summary>
        /// VERA will output detailed debug logs, including all internal operations and state changes.
        /// </summary>
        Verbose = 0,

        /// <summary>
        /// VERA will output informative logs including errors, warnings, and important state changes.
        /// </summary>
        Informative = 1,

        /// <summary>
        /// VERA will only output essential logs such as errors and warnings.
        /// </summary>
        Minimal = 2,

        /// <summary>
        /// VERA will not output any debug logs to the console.
        /// </summary>
        None = 3
    }

    [System.Serializable]
    internal class VERABuildAuthInfo
    {
        public bool authenticated = false;
        public string buildAuthToken = String.Empty;
        public string activeExperiment = String.Empty;
        public string activeExperimentName = String.Empty;
        public bool isMultiSite = false;
        public string activeSite = String.Empty;
        public string activeSiteName = String.Empty;
        public int currentBuildNumber = -1;
        public DataRecordingType dataRecordingType = DataRecordingType.RecordLocallyAndLive;
        public DebugPreference debugPreference = DebugPreference.Informative;
    }
}