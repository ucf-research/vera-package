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
            Debug.Log("[VERA Logger] Disabling MSAA for WebGL build.");
            QualitySettings.antiAliasing = 0;
        }
#endif

            LoadAuthentication();

            // If we are NOT in a WebXR build (WebGL), initialize immediately
            // WebXR builds will initialize later via a message from the WebXR build
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("[VERA Logger] WebXR build detected; initialization will occur via Unity message from the VERA site. Not initializing yet.");
#else
            Debug.Log("[VERA Logger] Session is not running in an active WebXR session; initializing logger directly...");
            StartCoroutine(Initialize());
#endif
        }


        // Initializes the logger, setting up keys, paths, and uploading existing files
        private IEnumerator Initialize()
        {
            if (initialized)
            {
                Debug.LogWarning("[VERA Logger] Logger is already initialized. Skipping reinitialization.");
                yield break;
            }

            yield return SetupKeysAndPaths();

            // If no experiment ID found from above keys and paths call, stop initialization.
            if (experimentUUID == "N/A" || string.IsNullOrEmpty(experimentUUID))
            {
                yield break;
            }

            // Upload any existing unuploaded files
            UploadExistingUnuploadedFiles(Path.Combine(dataPath, "uploadedCSVs.txt"), dataPath, ".csv");
            UploadExistingUnuploadedFiles(Path.Combine(dataPath, "uploadedImages.txt"), dataPath, ".png");
            UploadExistingUnuploadedFiles(Path.Combine(dataPath, "uploadedGeneric.txt"), genericDataPath, "");

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

            Debug.Log("[VERA Logger] Logger initialized successfully.");
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

                // Set PlayerPrefs to match file
                PlayerPrefs.SetInt("VERA_BuildAuthenticated", authInfo.authenticated ? 1 : 0);
                PlayerPrefs.SetString("VERA_BuildAuthToken", authInfo.buildAuthToken);
                PlayerPrefs.SetString("VERA_ActiveExperiment", authInfo.activeExperiment);
                PlayerPrefs.SetString("VERA_ActiveSite", authInfo.activeSite);
            }
            else
            {
                // File not found, authentication likely not set up yet; set default values
                PlayerPrefs.SetInt("VERA_BuildAuthenticated", 0);
                PlayerPrefs.SetString("VERA_BuildAuthToken", String.Empty);
                PlayerPrefs.SetString("VERA_ActiveExperiment", String.Empty);
                PlayerPrefs.SetString("VERA_ActiveSite", String.Empty);

                // Log unauthenticated user
                Debug.LogError("[VERA Authentication] Your experiment has not been authenticated, and will not be " +
                    "able to record data. Authenticate via the Unity menu bar item, VERA -> Settings; if you are already " +
                    "authenticated, try logging out and reauthenticating.");
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
                Debug.LogError("[VERA Logger] You do not have an active experiment. Without an active experiment, data cannot be collected using VERA." +
                    " In the Menu Bar at the top of the editor, please click on the 'VERA' dropdown, then select 'Settings' to pick an active experiment.");
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
                Debug.Log("[VERA Connection] You are connected to the VERA servers.");
            else
                Debug.LogError("[VERA Connection] Failed to connect to the VERA servers: " + www.error);
        }

        // Manually initializes the logger with the specified site ID and participant ID.
        // Most often will be called from VERASessionManager, prompted from the WebXR build.
        // If site is provided, override the active site with the provided ID; otherwise, use the active site from PlayerPrefs.
        // If participant is provided, override the active participant with the provided ID; otherwise, create a new participant.
        // Continues to initialize the logger after setting the site ID.
        public void ManualInitialization(string siteId, string participantId)
        {
            Debug.Log("[VERA Logger] Overriding initialization with given parameters...");

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
                Debug.LogError($"[VERA Logger] GET request failed for URL: {url}. Error: {result.error}. Response Code: {result.responseCode}");
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
                Debug.Log("[VERA Logger] No CSV file types found; continuing assuming no CSV logging will occur.");
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
                Debug.Log("[VERA Logger] Auto-creating baseline data logging setup...");

                // Add the component to the VERALogger GameObject
                VERABaselineAutoSetup autoSetup = gameObject.AddComponent<VERABaselineAutoSetup>();

                // Configure with sensible defaults
                autoSetup.autoCreateBaselineLogger = true;
                autoSetup.startOnExperimentBegin = true;
                autoSetup.fallbackAutoStart = true;
                autoSetup.defaultSamplingRate = 30;

                Debug.Log("[VERA Logger] Baseline data logging setup created automatically. " +
                         "VR baseline data will be collected at 30Hz when experiment starts.");
            }
            else
            {
                Debug.Log("[VERA Logger] Baseline data logging setup already exists - using existing configuration.");
            }
        }


        // Finds a csv handler by provided FileType name
        // Ignores extension, returns null on failure to find
        public VERACsvHandler FindCsvHandlerByFileName(string name)
        {
            if (!collecting)
            {
                Debug.LogWarning("[VERA Logger] Cannot access CSV handers - collection is not yet enabled.");
                return null;
            }
            if (!initialized)
            {
                Debug.LogWarning("[VERA Logger]: Cannot access CSV handers - logger is not yet initialized.");
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


        // Creates a CSV entry for the given file type
        public void CreateCsvEntry(string fileTypeName, int eventId, params object[] values)
        {
            if (!collecting || !initialized)
                return;

            VERACsvHandler csvHandler = FindCsvHandlerByFileName(fileTypeName);
            if (csvHandler == null)
            {
                Debug.LogError("[VERA Logger]: No file type could be found associated with provided name \"" +
                  fileTypeName + "\"; cannot log CSV entry to the file as desired.");
                return;
            }

            csvHandler.CreateEntry(eventId, values);
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
                Debug.LogError("[VERA Logger]: No file type could be found associated with provided name \"" +
                  fileTypeName + "\"; cannot submit the CSV as desired.");
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
                Debug.Log($"[VERA Logger] Directory [{existingFilesFolderPath}] does not exist, creating it");
                Directory.CreateDirectory(existingFilesFolderPath);
            }
            if (!File.Exists(alreadyUploadedTxtPath))
            {
                Debug.Log($"File [{alreadyUploadedTxtPath}] does not exist, creating it");
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
                        Debug.Log("[VERA Logger] Found a partial sync file \"" + file + "\"; deleting file...");
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError("[VERA Logger] Failed to delete partial sync file \"" + file + "\": " + e.Message);
                        }
                        continue;
                    }

                    Debug.Log("[VERA Logger] Found an unuploaded file \"" + file + "\"; uploading file...");

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
                    Debug.LogError("VERA: Invalid file name");
                    yield break;
                }
            }
            else
            {
                Debug.LogError("VERA: Invalid file name");
                yield break;
            }

            string url = $"{host}/api/participants/{file_participant_UDID}/filetypes/{fileTypeId}/files";
            byte[] fileData = null;

            // Read the file's data
            yield return ReadBinaryDataFile(csvFilePath, (result) => fileData = result);
            if (fileData == null)
            {
                Debug.Log("No file data to submit");
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
                Debug.Log("[VERA Logger] Successfully uploaded existing file \"" + csvFilePath + "\".");
                OnCsvFullyUploaded(csvFilePath);
            }
            else
            {
                Debug.LogError("[VERA Logger] Failed to upload existing file \"" + csvFilePath + "\".");
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
            Debug.Log("[VERA Logger] Session finalized; marking COMPLETE and doing final sync.");

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
                    Debug.Log("[VERA Logger] Waiting for active upload requests to complete before marking participant as COMPLETE.");
                    yield return new WaitForSeconds(0.5f);
                }
                else
                {
                    Debug.Log("[VERA Logger] All active upload requests completed. Marking participant as COMPLETE.");
                }
            }

            if (!activeParticipant.IsInFinalizedState())
                yield return StartCoroutine(activeParticipant.RetryableChangeProgress(VERAParticipantManager.ParticipantProgressState.COMPLETE));
            else
                Debug.LogWarning("[VERA Logger] Participant is already in a finalized state before session completion. No action taken.");

            Debug.Log("[VERA Logger] Participant marked as COMPLETE successfully. All finalization tasks complete.");

            #if UNITY_WEBGL && !UNITY_EDITOR
            // In WebGL builds, notify the site itself that the session is complete
            Debug.Log("[VERA Logger] Notifying VERA portal that session has been finalized.");   
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

            Debug.LogError("[VERA Logger] No independent variable found with name \"" + ivName + "\"; cannot get selected value.");
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

            Debug.LogError("[VERA Logger] No independent variable found with name \"" + ivName + "\"; cannot set selected value.");
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
                Debug.LogError("[VERA Logger] Failed to get experiment conditions from server; cannot initialize conditions.");
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
                Debug.LogError("[VERA Logger] Failed to parse experiment conditions response: " + e.Message);
                yield break;
            }

            if (resp == null || !resp.success || resp.independentVariables == null || resp.independentVariables.Length == 0)
            {
                Debug.Log("[VERA Logger] No independent variables found in experiment conditions response; continuing assuming experiment has no conditions.");
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
                    Debug.LogWarning("[VERA Logger] Independent variable \"" + iv.ivName + "\" has no selected condition value; defaulting to first condition.");
                    if (iv.conditions != null && iv.conditions.Length > 0 && iv.conditions[0].name != null && iv.conditions[0].name != "")
                    {
                        conditionsCache.Add(iv.ivName, iv.conditions[0].name);
                        ivChanged = true;
                    }
                    else
                    {
                        Debug.LogError("[VERA Logger] Independent variable \"" + iv.ivName + "\" has no conditions defined; skipping.");
                    }
                }
            }

            if (ivChanged)
            {
                Debug.Log("[VERA Logger] One or more independent variables had no selected condition; syncing updated conditions to server.");
                foreach (string ivName in conditionsCache.Keys)
                {
                    yield return StartCoroutine(SyncParticipantCondition(ivName));
                }
            }

            Debug.Log("[VERA Logger] Experiment conditions initialized successfully with " + conditionsCache.Count + " independent variables. Conditions: " + GetExperimentConditions());
        }

        // Syncs a single independent variable's selected condition to the server
        private IEnumerator SyncParticipantCondition(string ivName)
        {
            if (!conditionsCache.ContainsKey(ivName))
            {
                Debug.LogError("[VERA Logger] No independent variable found with name \"" + ivName + "\"; cannot sync condition to server.");
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
                Debug.Log("[VERA Logger] Successfully synced condition for IV \"" + ivName + "\" with value \"" + ivValue + "\" to server.");
            }
            else
            {
                Debug.LogError("[VERA Logger] Failed to sync condition for IV \"" + ivName + "\" to server. Error: " + request.error);
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
                Debug.LogWarning("[VERA Logger] App is quitting before participant is finalized. Marking participant as INCOMPLETE.");
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
                    //Debug.Log("Reading file: " + filePath);
                    fileData = File.ReadAllBytes(filePath);
                    fileReadSuccess = true;
                    break;
                }
                catch (IOException ex)
                {
                    Debug.LogWarning($"[VERA Logger] Attempt {ex.Message} {i + 1}: Failed to read file due to sharing violation. Retrying...");
                }

                if (!fileReadSuccess)
                {
                    yield return new WaitForSeconds(0.1f); // Add a small delay before retrying
                }
            }

            if (!fileReadSuccess)
            {
                Debug.LogError("[VERA Logger] Failed to read the file after multiple attempts.");
                yield break;
            }

            if (fileData.Length > 50 * 1024 * 1024)
            {
                Debug.LogError("[VERA Logger] File \"" + filePath + "\" size exceeds the limit of 50MB. File will not be uploaded.");
                yield break;
            }

            callback?.Invoke(fileData);
        }


        #endregion


    }


    [System.Serializable]
    internal class VERABuildAuthInfo
    {
        public bool authenticated = false;
        public string buildAuthToken = String.Empty;
        public string activeExperiment = String.Empty;
        public string activeSite = String.Empty;
    }
}