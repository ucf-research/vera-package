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
        public VERATrialWorkflowManager trialWorkflow { get; private set; }
        private VERAPeriodicSyncHandler periodicSyncHandler;
        private VERAGenericFileHelper genericFileHelper;
        private VERASurveyStarter surveyStarter;
        private Dictionary<string, int> manualBetweenSubjectsAssignments;

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
        public string csvDirectoryPath = "";
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

            // Setup generic file helper first (needed for uploading existing files)
            genericFileHelper = gameObject.AddComponent<VERAGenericFileHelper>();

            // Upload any existing unuploaded files
            //UploadExistingUnuploadedFiles(Path.Combine(dataPath, "uploadedCSVs.txt"), dataPath, ".csv");
            //UploadExistingUnuploadedFiles(Path.Combine(dataPath, "uploadedImages.txt"), dataPath, ".png");
            //UploadExistingUnuploadedFiles(Path.Combine(dataPath, "uploadedGeneric.txt"), genericDataPath, "");

            // Setup any file types, CSV logging, and the generic file helper
            SetupFileTypes();

            // Fetch the Survey_Responses file type ID from server (needed for CSV upload)
            yield return FetchSurveyResponsesFileTypeId();

            // Initialize experiment conditions BEFORE enabling collection
            // This ensures conditions are available when baseline logging starts
            yield return InitializeExperimentConditions();

            collecting = true;

            // Auto-setup baseline data collection if not already present
            EnsureBaselineDataLoggingSetup();
            periodicSyncHandler = gameObject.AddComponent<VERAPeriodicSyncHandler>();
            periodicSyncHandler.StartPeriodicSync();

            // Set up survey starter
            surveyStarter = gameObject.AddComponent<VERASurveyStarter>();

            yield return InitializeExperimentConditions();
            // Initialize trial workflow manager (pass participant info for between-subjects assignment and checkpointing)
            trialWorkflow = gameObject.AddComponent<VERATrialWorkflowManager>();
            int participantNum = activeParticipant != null ? activeParticipant.participantShortId : -1;
            string participantId = activeParticipant != null ? activeParticipant.participantUUID : null;

            // Allow for manual between-subjects assignments (set via SetBetweenSubjectsAssignment before initialization)
            yield return trialWorkflow.Initialize(experimentUUID, apiKey, participantNum, participantId, manualBetweenSubjectsAssignments);

            // Auto-apply Latin square ordering if required
            if (trialWorkflow.RequiresLatinSquareOrdering && activeParticipant != null)
            {
                trialWorkflow.ApplyLatinSquareOrdering(participantNum);
                Debug.Log($"[VERA Logger] Auto-applied Latin square ordering for participant {participantNum}.");
            }

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

            // Load column definitions from Resources
            VERAColumnDefinition[] resourceDefinitions = Resources.LoadAll<VERAColumnDefinition>("");

            // Add programmatic column definitions (survey responses) only if not already loaded from server
            var allDefinitions = new List<VERAColumnDefinition>(resourceDefinitions ?? new VERAColumnDefinition[0]);

            // Check if Survey_Responses was already fetched from server (has valid _id, not placeholder)
            // Match various naming conventions: Survey_Responses, survey-responses, SurveyResponses, etc.
            bool surveyResponsesExists = false;
            foreach (var def in allDefinitions)
            {
                string name = def?.fileType?.name?.ToLowerInvariant()?.Replace("_", "").Replace("-", "").Replace(" ", "") ?? "";
                if (name == "surveyresponses" &&
                    !string.IsNullOrEmpty(def.fileType.fileTypeId) &&
                    def.fileType.fileTypeId != "survey-responses")
                {
                    surveyResponsesExists = true;
                    Debug.Log($"[VERA Logger] Survey_Responses file type loaded from server with ID: {def.fileType.fileTypeId}");
                    break;
                }
            }

            // Only add hardcoded definition if server-fetched one doesn't exist
            if (!surveyResponsesExists)
            {
                allDefinitions.Add(VERASurveyResponseColumnDefinition.Create());
                Debug.Log("[VERA Logger] Using programmatic Survey_Responses definition (will fetch ID from server)");
            }

            if (allDefinitions.Count == 0)
            {
                VERADebugger.Log("No CSV file types found; continuing assuming no CSV logging will occur.", "VERA Logger", DebugPreference.Informative);
                return;
            }

            csvHandlers = new VERACsvHandler[allDefinitions.Count];

            // For each column definition, set it up as its own CSV handler
            for (int i = 0; i < csvHandlers.Length; i++)
            {
                // Skip local sync for survey responses since we upload individual responses immediately via API, instead of batch uploading a CSV
                bool skipLocalSync = allDefinitions[i].fileType.name == "Survey_Responses";

                csvHandlers[i] = gameObject.AddComponent<VERACsvHandler>();
                csvHandlers[i].Initialize(allDefinitions[i], skipLocalSync);
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

                autoSetup.logEveryFrame = true;  // Log every frame for maximum fidelity
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

        /// <summary>
        /// Fetches the Survey_Responses file type ID from the server.
        /// This ID is needed to upload survey response CSV files via the file type API.
        /// Endpoint: GET /api/experiments/:experimentId/filetypes/survey-responses
        /// </summary>
        private IEnumerator FetchSurveyResponsesFileTypeId()
        {
            // Find the Survey_Responses handler directly (collecting flag not set yet)
            VERACsvHandler surveyHandler = null;
            if (csvHandlers != null)
            {
                foreach (var handler in csvHandlers)
                {
                    if (handler?.columnDefinition?.fileType?.name == "Survey_Responses")
                    {
                        surveyHandler = handler;
                        break;
                    }
                }
            }

            if (surveyHandler == null)
            {
                Debug.LogWarning("[VERA Logger] Survey_Responses CSV handler not found; skipping file type ID fetch.");
                yield break;
            }

            // Skip fetch if already have a valid ID (loaded from server-fetched column definition)
            string existingId = surveyHandler.columnDefinition.fileType.fileTypeId;
            if (!string.IsNullOrEmpty(existingId) && existingId != "survey-responses")
            {
                Debug.Log($"[VERA Logger] Survey_Responses already has valid file type ID: {existingId}");
                yield break;
            }

            string url = $"{VERAHost.hostUrl}/api/experiments/{experimentUUID}/filetypes/survey-responses";
            Debug.Log($"[VERA Logger] Fetching Survey_Responses file type ID from: {url}");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Authorization", "Bearer " + apiKey);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string responseText = request.downloadHandler.text;
                        var json = Newtonsoft.Json.Linq.JObject.Parse(responseText);
                        string fileTypeId = json["_id"]?.ToString();

                        if (!string.IsNullOrEmpty(fileTypeId))
                        {
                            surveyHandler.columnDefinition.fileType.fileTypeId = fileTypeId;
                            Debug.Log($"[VERA Logger] Survey_Responses file type ID fetched: {fileTypeId}");
                        }
                        else
                        {
                            Debug.LogWarning("[VERA Logger] Survey_Responses file type response missing _id field. CSV upload will be skipped.");
                            surveyHandler.columnDefinition.fileType.skipUpload = true;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[VERA Logger] Failed to parse survey-responses file type response: {ex.Message}. CSV upload will be skipped.");
                        surveyHandler.columnDefinition.fileType.skipUpload = true;
                    }
                }
                else
                {
                    // Log detailed error info for debugging
                    Debug.LogWarning($"[VERA Logger] Failed to fetch Survey_Responses file type (HTTP {request.responseCode}): {request.error}");
                    Debug.LogWarning($"[VERA Logger] Survey_Responses CSV upload will be disabled. Individual responses are still submitted via /api/surveys/responses.");
                    // Disable upload for this file type since we don't have a valid ID
                    surveyHandler.columnDefinition.fileType.skipUpload = true;
                }
            }
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
                            StartCoroutine(SubmitExistingImageCoroutine(file));
                            break;
                        default:
                            StartCoroutine(SubmitExistingGenericFileCoroutine(file));
                            break;
                    }
                }
            }
        }


        // Submits an existing CSV (from a previous session) based on its file path
        private IEnumerator SubmitExistingCSVCoroutine(string csvFilePath)
        {
            var basename = Path.GetFileName(csvFilePath);

            // Skip survey response backup files - these use a different naming format
            // and are uploaded immediately during survey completion, not via this re-upload mechanism
            if (!basename.Contains("-") || basename.Split('-').Length < 4)
            {
                Debug.Log($"[VERA Logger] Skipping file \"{basename}\" - not in standard VERA format (likely a survey backup file).");
                yield break;
            }

            // Get the IDs associated with this file
            string host = VERAHost.hostUrl;
            string file_experimentUUID;
            string file_siteUUID;
            string file_participant_UDID;
            string fileTypeId;

            // Files are in format expId-siteId-partId-fileTypeId.csv
            // Note: fileTypeId may contain hyphens (e.g., "survey-responses")
            // So we split by '-' but only take first 3 parts as IDs, join the rest as fileTypeId
            string[] split = basename.Split('-');

            file_experimentUUID = split[0];
            file_siteUUID = split[1];
            file_participant_UDID = split[2];

            // Join remaining parts as fileTypeId (handles hyphens in file type like "survey-responses")
            // Remove .csv extension from the last part
            string lastPart = split[split.Length - 1];
            if (lastPart.EndsWith(".csv"))
            {
                split[split.Length - 1] = lastPart.Substring(0, lastPart.Length - 4);
            }
            fileTypeId = string.Join("-", split, 3, split.Length - 3);

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
            // Only upload files that match the current experiment
            if (file_experimentUUID != experimentUUID)
            {
                Debug.Log($"[VERA Logger] Skipping file \"{basename}\" - belongs to different experiment ({file_experimentUUID} vs current {experimentUUID})");
                yield break;
            }

            // Skip survey-responses files - these use the dedicated survey API, not the file type API
            if (fileTypeId == "survey-responses")
            {
                Debug.Log($"[VERA Logger] Skipping survey-responses file \"{basename}\" - survey responses use dedicated API.");
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
            }
            else
            {
                Debug.LogWarning($"[VERA Logger] Failed to upload existing file \"{csvFilePath}\". HTTP {request.responseCode}: {request.error}");
                if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                {
                    Debug.LogWarning($"[VERA Logger] Response Body: {request.downloadHandler.text}");
                }

                // Mark as uploaded for permanent failures so we don't retry every session
                // 403 = concluded participant, 404 = file type or participant not found
                if (request.responseCode == 403 || request.responseCode == 404)
                {
                    Debug.LogWarning($"[VERA Logger] Permanent failure (HTTP {request.responseCode}); marking file as uploaded to prevent repeated retries.");
                }
            }
        }


        // Submits an existing image file (from a previous session) based on its file path
        private IEnumerator SubmitExistingImageCoroutine(string imageFilePath)
        {
            var basename = Path.GetFileName(imageFilePath);

            // Check if file belongs to current experiment
            if (imageFilePath.Length > 0 && imageFilePath.Contains("-"))
            {
                string[] split = basename.Split('-');
                if (split.Length >= 1)
                {
                    string file_experimentUUID = split[0];

                    // Only upload files that match the current experiment
                    if (file_experimentUUID != experimentUUID)
                    {
                        Debug.Log($"[VERA Logger] Skipping image file \"{basename}\" - belongs to different experiment ({file_experimentUUID} vs current {experimentUUID})");
                        genericFileHelper.OnImageFullyUploaded(imageFilePath);
                        yield break;
                    }
                }
            }

            // File belongs to current experiment, proceed with upload
            SubmitImageFile(imageFilePath);
        }


        // Submits an existing generic file (from a previous session) based on its file path
        private IEnumerator SubmitExistingGenericFileCoroutine(string genericFilePath)
        {
            var basename = Path.GetFileName(genericFilePath);

            // Check if file belongs to current experiment
            if (genericFilePath.Length > 0 && genericFilePath.Contains("-"))
            {
                string[] split = basename.Split('-');
                if (split.Length >= 1)
                {
                    string file_experimentUUID = split[0];

                    // Only upload files that match the current experiment
                    if (file_experimentUUID != experimentUUID)
                    {
                        Debug.Log($"[VERA Logger] Skipping generic file \"{basename}\" - belongs to different experiment ({file_experimentUUID} vs current {experimentUUID})");
                        genericFileHelper.OnGenericFullyUploaded(genericFilePath);
                        yield break;
                    }
                }
            }

            // File belongs to current experiment, proceed with upload
            SubmitGenericFile(genericFilePath);
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
        // Sets the LOCAL cached version, then optionally syncs to the server asynchronously
        public string SetSelectedIVValue(string ivName, string ivValue, bool syncToServer = true)
        {
            // Update existing IV or add new one (for trial-based condition assignment)
            if (conditionsCache.ContainsKey(ivName))
            {
                conditionsCache[ivName] = ivValue;
            }
            else
            {
                // Add new IV to cache - this happens when trials set conditions for IVs
                // that weren't initialized at experiment start
                conditionsCache.Add(ivName, ivValue);
            }

            // Sync to server if requested (not needed for trial-based workflows where execution order defines conditions)
            if (syncToServer)
            {
                StartCoroutine(SyncParticipantCondition(ivName));
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
            // Always add all IVs to the cache - they'll appear in telemetry immediately
            // Trials will update the values when they run
            conditionsCache.Clear();
            foreach (IVInfo iv in resp.independentVariables)
            {
                if (iv.selectedCondition != null && !string.IsNullOrEmpty(iv.selectedCondition.name))
                {
                    // IV has a selected condition from the server
                    conditionsCache.Add(iv.ivName, iv.selectedCondition.name);
                }
                else if (iv.conditions != null && iv.conditions.Length > 0 && !string.IsNullOrEmpty(iv.conditions[0].name))
                {
                    // No selected condition - use the first available condition value
                    conditionsCache.Add(iv.ivName, iv.conditions[0].name);
                    Debug.Log($"[VERA Logger] Independent variable \"{iv.ivName}\" initialized with first condition: {iv.conditions[0].name}");
                }
                else
                {
                    // No conditions available - initialize with empty value
                    conditionsCache.Add(iv.ivName, "");
                    Debug.Log($"[VERA Logger] Independent variable \"{iv.ivName}\" has no conditions - initialized with empty value.");
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


        #region SURVEY MANAGEMENT


        // Starts a survey for the participant based on the provided survey info
        public void StartSurvey(VERASurveyInfo surveyToStart, bool transportToLobby = true, bool dimEnvironment = true, float heightOffset = 0f, float distanceOffset = 3f, Action onSurveyComplete = null)
        {
            if (surveyStarter == null)
            {
                VERADebugger.LogError("No survey starter found; cannot start survey.", "VERA Logger");
                return;
            }

            if (surveyToStart == null)
            {
                VERADebugger.LogError("No survey info provided; cannot start survey.", "VERA Logger");
                return;
            }

            surveyStarter.StartSurvey(surveyToStart, transportToLobby, dimEnvironment, heightOffset, distanceOffset, onSurveyComplete);
        }


        #endregion


        #region TRIAL WORKFLOW


        /// <summary>
        /// Gets the current trial configuration.
        /// Returns null if no trial is active or workflow is not initialized.
        /// </summary>
        public TrialConfig GetCurrentTrial()
        {
            if (trialWorkflow == null)
            {
                Debug.LogWarning("[VERA Logger] Trial workflow not initialized.");
                return null;
            }
            return trialWorkflow.GetCurrentTrial();
        }

        /// <summary>
        /// Advances to the next trial and starts it.
        /// Automatically sets the experiment's condition values based on the trial's conditions.
        /// Returns the trial that was started, or null if there are no more trials.
        /// </summary>
        public TrialConfig StartNextTrial()
        {
            if (trialWorkflow == null)
            {
                Debug.LogWarning("[VERA Logger] Trial workflow not initialized.");
                return null;
            }

            TrialConfig trial = trialWorkflow.StartNextTrial();

            // Automatically set condition values from trial data
            if (trial?.conditions != null)
            {
                foreach (var condition in trial.conditions)
                {
                    SetSelectedIVValue(condition.Key, condition.Value);
                }
            }

            return trial;
        }

        /// <summary>
        /// Marks the current trial as completed.
        /// Returns true if successful.
        /// </summary>
        public bool CompleteTrial()
        {
            if (trialWorkflow == null)
            {
                Debug.LogWarning("[VERA Logger] Trial workflow not initialized.");
                return false;
            }
            return trialWorkflow.CompleteTrial();
        }

        /// <summary>
        /// Marks the current trial as aborted.
        /// </summary>
        /// <param name="reason">Optional reason for aborting</param>
        public bool AbortTrial(string reason = "")
        {
            if (trialWorkflow == null)
            {
                Debug.LogWarning("[VERA Logger] Trial workflow not initialized.");
                return false;
            }
            return trialWorkflow.AbortTrial(reason);
        }

        /// <summary>
        /// Gets the next trial without starting it (preview).
        /// </summary>
        public TrialConfig PeekNextTrial()
        {
            return trialWorkflow?.PeekNextTrial();
        }

        /// <summary>
        /// Gets a condition value for the current trial.
        /// </summary>
        /// <param name="conditionName">The condition/IV name</param>
        public string GetTrialConditionValue(string conditionName)
        {
            return trialWorkflow?.GetConditionValue(conditionName);
        }

        /// <summary>
        /// Gets the elapsed time for the current trial in seconds.
        /// </summary>
        public float GetTrialElapsedTime()
        {
            return trialWorkflow?.GetTrialElapsedTime() ?? 0f;
        }

        /// <summary>
        /// Checks if there are more trials remaining.
        /// </summary>
        public bool HasMoreTrials => trialWorkflow?.HasMoreTrials ?? false;

        /// <summary>
        /// Gets the total number of trials in the workflow.
        /// </summary>
        public int TotalTrialCount => trialWorkflow?.TotalTrialCount ?? 0;

        /// <summary>
        /// Gets the current trial index (0-based). Returns -1 if no trial started.
        /// </summary>
        public int CurrentTrialIndex => trialWorkflow?.CurrentTrialIndex ?? -1;

        /// <summary>
        /// Checks if the current trial belongs to a within/between-subjects group.
        /// </summary>
        public bool IsCurrentTrialInGroup()
        {
            return trialWorkflow?.IsCurrentTrialInGroup() ?? false;
        }

        /// <summary>
        /// Gets the parent group ID for the current trial (null if standalone).
        /// </summary>
        public string GetCurrentGroupId()
        {
            return trialWorkflow?.GetCurrentGroupId();
        }

        /// <summary>
        /// Gets the parent group type ("within" or "between") for the current trial.
        /// </summary>
        public string GetCurrentGroupType()
        {
            return trialWorkflow?.GetCurrentGroupType();
        }

        /// <summary>
        /// Checks if this is the first trial in its group.
        /// Useful for showing group instructions/setup.
        /// </summary>
        public bool IsFirstTrialInGroup()
        {
            return trialWorkflow?.IsFirstTrialInGroup() ?? false;
        }

        /// <summary>
        /// Checks if this is the last trial in its group.
        /// Useful for group completion logic.
        /// </summary>
        public bool IsLastTrialInGroup()
        {
            return trialWorkflow?.IsLastTrialInGroup() ?? false;
        }

        /// <summary>
        /// Gets the trial's position within its group (1-indexed, 0 if standalone).
        /// </summary>
        public int GetTrialPositionInGroup()
        {
            return trialWorkflow?.GetTrialPositionInGroup() ?? 0;
        }

        /// <summary>
        /// Gets the total trials in the current trial's group (0 if standalone).
        /// </summary>
        public int GetGroupTrialCount()
        {
            return trialWorkflow?.GetGroupTrialCount() ?? 0;
        }

        /// <summary>
        /// Checks if the current workflow item is a standalone survey.
        /// </summary>
        public bool IsCurrentItemSurvey()
        {
            return trialWorkflow?.IsCurrentItemSurvey() ?? false;
        }

        /// <summary>
        /// Checks if the current trial has an attached survey.
        /// </summary>
        public bool CurrentTrialHasSurvey()
        {
            return trialWorkflow?.CurrentTrialHasSurvey() ?? false;
        }

        /// <summary>
        /// Checks if a survey should be shown BEFORE the current trial.
        /// </summary>
        public bool ShouldShowSurveyBefore()
        {
            return trialWorkflow?.ShouldShowSurveyBefore() ?? false;
        }

        /// <summary>
        /// Checks if a survey should be shown AFTER the current trial.
        /// </summary>
        public bool ShouldShowSurveyAfter()
        {
            return trialWorkflow?.ShouldShowSurveyAfter() ?? false;
        }

        /// <summary>
        /// Gets the survey ID for the current item (standalone or attached).
        /// </summary>
        public string GetCurrentSurveyId()
        {
            return trialWorkflow?.GetCurrentSurveyId();
        }

        /// <summary>
        /// Gets the survey name for the current item.
        /// </summary>
        public string GetCurrentSurveyName()
        {
            return trialWorkflow?.GetCurrentSurveyName();
        }

        /// <summary>
        /// Checks if the workflow requires Latin square ordering.
        /// If true, call ApplyLatinSquareOrdering() with the participant number before starting trials.
        /// </summary>
        public bool RequiresLatinSquareOrdering => trialWorkflow?.RequiresLatinSquareOrdering ?? false;

        /// <summary>
        /// Applies Latin Square counterbalancing to the trial workflow.
        /// Call this after initialization but before starting any trials.
        ///
        /// Example usage:
        ///   if (VERALogger.Instance.RequiresLatinSquareOrdering)
        ///   {
        ///       int participantNum = VERALogger.Instance.activeParticipant.participantShortId;
        ///       VERALogger.Instance.ApplyLatinSquareOrdering(participantNum);
        ///   }
        /// </summary>
        /// <param name="participantNumber">The participant's sequential number (0-indexed)</param>
        public void ApplyLatinSquareOrdering(int participantNumber)
        {
            if (trialWorkflow == null)
            {
                Debug.LogWarning("[VERA Logger] Trial workflow not initialized.");
                return;
            }
            trialWorkflow.ApplyLatinSquareOrdering(participantNumber);
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
        /// Example usage:
        ///   // For a study with 30 total participants
        ///   int participantNum = VERALogger.Instance.activeParticipant.participantShortId;
        ///   bool success = VERALogger.Instance.ApplyLatinSquareOrdering(participantNum, 30);
        ///   if (!success) {
        ///       Debug.LogError("Failed to apply Latin square ordering! Check console for details.");
        ///   }
        /// </summary>
        /// <param name="participantNumber">The participant's sequential number (0-indexed). Must be less than totalParticipants.</param>
        /// <param name="totalParticipants">The total number of participants in the study. Must be >= number of conditions.</param>
        /// <returns>True if Latin square ordering was applied successfully, false if validation failed.</returns>
        public bool ApplyLatinSquareOrdering(int participantNumber, int totalParticipants)
        {
            if (trialWorkflow == null)
            {
                Debug.LogWarning("[VERA Logger] Trial workflow not initialized.");
                return false;
            }
            return trialWorkflow.ApplyLatinSquareOrdering(participantNumber, totalParticipants);
        }

        /// <summary>
        /// Gets the current trial ordering as a debug string.
        /// Useful for logging/verifying the order.
        /// </summary>
        public string GetTrialOrderingDebugString()
        {
            return trialWorkflow?.GetTrialOrderingDebugString() ?? "No workflow";
        }

        /// <summary>
        /// Gets info about within-subjects groups that need Latin square ordering.
        /// Returns a dictionary of group ID -> number of trials in that group.
        /// </summary>
        public Dictionary<string, int> GetWithinGroupsForLatinSquare()
        {
            return trialWorkflow?.GetWithinGroupsForLatinSquare() ?? new Dictionary<string, int>();
        }

        /// <summary>
        /// Manually assigns a participant to a specific condition in a between-subjects group.
        /// Must be called BEFORE the logger initializes the trial workflow.
        ///
        /// Usage example (assign based on gender):
        ///   // Call this in Awake() before logger initialization
        ///   string gender = GetParticipantGender(); // "male" or "female"
        ///   int conditionIndex = gender == "male" ? 0 : 1;
        ///   VERALogger.Instance.SetBetweenSubjectsAssignment("group-id-123", conditionIndex);
        /// </summary>
        /// <param name="betweenSubjectsGroupId">The ID of the between-subjects group</param>
        /// <param name="conditionIndex">The 0-based index of the condition to assign (0 = first condition, 1 = second, etc.)</param>
        public void SetBetweenSubjectsAssignment(string betweenSubjectsGroupId, int conditionIndex)
        {
            if (initialized)
            {
                Debug.LogWarning("[VERA Logger] Cannot set between-subjects assignment after initialization. Call this before logger initializes.");
                return;
            }

            if (manualBetweenSubjectsAssignments == null)
            {
                manualBetweenSubjectsAssignments = new Dictionary<string, int>();
            }

            manualBetweenSubjectsAssignments[betweenSubjectsGroupId] = conditionIndex;
            Debug.Log($"[VERA Logger] Manual between-subjects assignment: group '{betweenSubjectsGroupId}' → condition {conditionIndex}");
        }


        /// <summary>
        /// Starts the automated trial workflow. The workflow will progress through all trials,
        /// pausing for surveys (OnSurveyRequired) and trial logic (OnTrialReady).
        ///
        /// Subscribe to trialWorkflow.OnTrialReady to receive each trial when it's ready.
        /// Call CompleteAutomatedTrial() when your trial logic finishes.
        /// Subscribe to trialWorkflow.OnSurveyRequired for survey handling.
        /// Subscribe to trialWorkflow.OnWorkflowCompleted to know when all trials are done.
        /// </summary>
        public void StartAutomatedWorkflow()
        {
            if (trialWorkflow == null)
            {
                Debug.LogWarning("[VERA Logger] Trial workflow not initialized.");
                return;
            }

            // Subscribe to OnTrialReady so we can auto-set IV values when each trial starts
            trialWorkflow.OnTrialReady += AutoSetTrialConditions;
            trialWorkflow.OnWorkflowCompleted += OnAutomatedWorkflowDone;
            trialWorkflow.StartAutomatedWorkflow();
        }

        /// <summary>
        /// Stops the automated workflow. You can resume manual control after stopping.
        /// </summary>
        public void StopAutomatedWorkflow()
        {
            if (trialWorkflow == null)
            {
                Debug.LogWarning("[VERA Logger] Trial workflow not initialized.");
                return;
            }
            trialWorkflow.OnTrialReady -= AutoSetTrialConditions;
            trialWorkflow.OnWorkflowCompleted -= OnAutomatedWorkflowDone;
            trialWorkflow.StopAutomatedWorkflow();
        }

        private void OnAutomatedWorkflowDone()
        {
            if (trialWorkflow != null)
            {
                trialWorkflow.OnTrialReady -= AutoSetTrialConditions;
                trialWorkflow.OnWorkflowCompleted -= OnAutomatedWorkflowDone;
            }
        }

        private void AutoSetTrialConditions(TrialConfig trial)
        {
            if (trial?.conditions != null)
            {
                foreach (var condition in trial.conditions)
                {
                    SetSelectedIVValue(condition.Key, condition.Value);
                }
            }
        }

        /// <summary>
        /// Call this from your trial logic to signal the current trial is done (automated mode only).
        /// This completes the trial and lets the workflow advance to the next item.
        /// </summary>
        public void CompleteAutomatedTrial()
        {
            if (trialWorkflow == null)
            {
                Debug.LogWarning("[VERA Logger] Trial workflow not initialized.");
                return;
            }
            trialWorkflow.CompleteAutomatedTrial();
        }

        /// <summary>
        /// Whether the workflow is currently running in automated mode.
        /// </summary>
        public bool IsAutomatedMode => trialWorkflow?.IsAutomatedMode ?? false;

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


        public string GetCsvDirectory()
        {
            // Build the hierarchical directory path and get the directory for CSV storage
            if (String.IsNullOrEmpty(csvDirectoryPath))
            {
                csvDirectoryPath = BuildHierarchicalDirectoryPath();
            }

            return csvDirectoryPath;
        }

        /// <summary>
        /// Builds the hierarchical directory path based on experiment, site, build version, and participant.
        /// Creates the directory if it doesn't already exist.
        /// </summary>
        private string BuildHierarchicalDirectoryPath()
        {
            VERABuildAuthInfo authInfo = buildAuthInfo;
            string participantShortId = activeParticipant.participantShortId.ToString();

            // Start with the base directory from baseFilePath
            string baseDirectory = Path.GetDirectoryName(baseFilePath);

            // 1. Experiment folder (replace spaces with dashes)
            string experimentFolder = authInfo.activeExperimentName.Replace(" ", "-");
            string path = Path.Combine(baseDirectory, experimentFolder);

            // 2. Site folder (only if multi-site)
            if (authInfo.isMultiSite)
            {
                string siteFolder = "Site-" + authInfo.activeSiteName.Replace(" ", "-");
                path = Path.Combine(path, siteFolder);
            }

            // 3. Build version folder
            string buildVersionFolder;
            if (authInfo.currentBuildNumber <= 0)
            {
                buildVersionFolder = "BuildVersion_PreBuild";
            }
            else
            {
                buildVersionFolder = "BuildVersion_" + authInfo.currentBuildNumber.ToString();
            }
            path = Path.Combine(path, buildVersionFolder);

            // 4. Participant folder
            string participantFolder = "Participant-" + participantShortId;
            path = Path.Combine(path, participantFolder);

            // Create directory if it doesn't exist
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                VERADebugger.Log("Created directory structure: " + path, "VERACsvHandler", DebugPreference.Verbose);
            }

            return path;
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
