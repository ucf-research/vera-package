using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace VERA
{

#if UNITY_EDITOR

    using UnityEditor.Build;

    [ExecuteInEditMode]
    internal static class VERAAuthenticator
    {
        private static HttpListener listener;
        private static bool isRunning = false;

        private const string listenUrl = "http://localhost:8080/auth";
        private const int authListenPort = 8080;
        private const int maxServerStartAttempts = 5;

        private const string userAuthFileName = "VERAUserAuthentication.json";
        private const string buildAuthFileName = "VERABuildAuthentication.json";

        [InitializeOnLoadMethod]
        private static void RegisterAuthServerCleanup()
        {
            EditorApplication.quitting += StopUserAuthServer;
            AssemblyReloadEvents.beforeAssemblyReload += StopUserAuthServer;
        }


        #region AUTHENTICATION SERVER CALLS


        public static void StartUserAuthentication()
        {
            if (!StartUserAuthServer())
            {
                VERADebugger.LogError(
                    $"Could not start the local authentication server on port {authListenPort}. " +
                    "Please wait a moment and try again.",
                    "VERA Authentication");
                return;
            }

            // Open the authentication URL in the default browser
            Application.OpenURL(VERAHost.hostUrl + "/Authenticate");
        }

        // Starts the server
        private static bool StartUserAuthServer()
        {
            StopUserAuthServer();

            for (int attempt = 0; attempt < maxServerStartAttempts; attempt++)
            {
                try
                {
                    listener = new HttpListener();
                    listener.Prefixes.Add(listenUrl + "/");
                    listener.Start();
                    isRunning = true;
                    listener.BeginGetContext(HandleAuthenticationRequest, listener);
                    return true;
                }
                catch (Exception ex) when (IsAddressInUseException(ex))
                {
                    StopUserAuthServer();
                    Thread.Sleep(100 * (attempt + 1));
                }
                catch (Exception ex)
                {
                    StopUserAuthServer();
                    VERADebugger.LogError(
                        $"Failed to start authentication server: {ex.Message}",
                        "VERA Authentication");
                    return false;
                }
            }

            return false;
        }

        // Stops the server
        private static void StopUserAuthServer()
        {
            isRunning = false;

            if (listener == null)
                return;

            HttpListener activeListener = listener;
            listener = null;

            try
            {
                activeListener.Abort();
            }
            catch (Exception)
            {
                try
                {
                    activeListener.Stop();
                }
                catch (Exception)
                {
                }
            }

            try
            {
                activeListener.Close();
            }
            catch (Exception)
            {
            }
        }

        private static bool IsAddressInUseException(Exception ex)
        {
            for (Exception current = ex; current != null; current = current.InnerException)
            {
                if (current is SocketException socketException &&
                    socketException.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    return true;
                }

                if (current is HttpListenerException httpListenerException &&
                    httpListenerException.Message.IndexOf("Only one usage", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void QueueNextAuthenticationRequest()
        {
            if (!isRunning || listener == null)
                return;

            try
            {
                listener.BeginGetContext(HandleAuthenticationRequest, listener);
            }
            catch (Exception ex)
            {
                EditorApplication.delayCall += () =>
                    VERADebugger.LogError(
                        $"Authentication server stopped listening: {ex.Message}",
                        "VERA Authentication");
                StopUserAuthServer();
            }
        }

        // Handles the authentication request
        private static void HandleAuthenticationRequest(IAsyncResult result)
        {
            if (!isRunning || listener == null)
                return;

            HttpListenerContext context = null;

            try
            {
                context = listener.EndGetContext(result);
            }
            catch (Exception ex)
            {
                if (isRunning)
                {
                    EditorApplication.delayCall += () =>
                        VERADebugger.LogError(
                            $"Authentication server request failed: {ex.Message}",
                            "VERA Authentication");
                }

                StopUserAuthServer();
                return;
            }

            EditorApplication.delayCall += () => VERADebugger.Log($"Sending request for authentication to the VERA portal...", "VERA Authentication", DebugPreference.None);

            var request = context.Request;
            bool keepListening = true;

            // Enable CORS
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            // Handle CORS Preflight
            if (request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.Close();
            }
            else if (request.HttpMethod == "POST")
            {
                try
                {
                    using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        string read = reader.ReadToEnd();
                        EditorApplication.delayCall += () => VERADebugger.Log($"Received response from VERA portal...", "VERA Authentication", DebugPreference.Informative);

                        UnityTokenResponse response = JsonUtility.FromJson<UnityTokenResponse>(read);

                        if (response == null || response.user == null || string.IsNullOrEmpty(response.token))
                        {
                            EditorApplication.delayCall += () => VERADebugger.LogError("Invalid authentication response received", "VERA Authentication");
                            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                            byte[] errorBytes = Encoding.UTF8.GetBytes("Invalid authentication data");
                            context.Response.ContentLength64 = errorBytes.Length;
                            context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
                            context.Response.Close();
                        }
                        else
                        {
                            string token = response.token;
                            string userId = response.user._id;
                            string userName = response.user.firstName + " " + response.user.lastName;
                            bool isPreviewAccount = response.user.previewAccount;
                            EditorApplication.delayCall += () => VERADebugger.Log($"USERS RESPONSE: {response.user}, previewAccount: {response.user.previewAccount}", "VERA Authentication", DebugPreference.Informative);
                            EditorApplication.delayCall += () => VERADebugger.Log($"Parsed data; name: {userName}, previewAccount: {isPreviewAccount}. Returning success to VERA portal...", "VERA Authentication", DebugPreference.Informative);

                            // Save
                            EditorApplication.delayCall += () =>
                            {
                                try
                                {
                                    SaveUserAuthentication(token, userId, userName, isPreviewAccount);
                                    VERADebugger.Log("[VERA Connection] You are successfully authenticated and connected to the VERA portal.\n", "VERA Authentication", DebugPreference.Informative);
                                }
                                catch (Exception ex)
                                {
                                    VERADebugger.LogError($"Failed to save authentication: {ex.Message}", "VERA Authentication");
                                }
                            };

                            // Respond with success
                            byte[] responseBytes = Encoding.UTF8.GetBytes("Token received");
                            context.Response.ContentLength64 = responseBytes.Length;
                            context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                            context.Response.Close();

                            // Stop the server after receiving the token
                            StopUserAuthServer();
                            keepListening = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    EditorApplication.delayCall += () => VERADebugger.LogError($"Error processing authentication request: {ex.Message}", "VERA Authentication");
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    byte[] errorBytes = Encoding.UTF8.GetBytes("Authentication processing failed");
                    context.Response.ContentLength64 = errorBytes.Length;
                    context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
                    context.Response.Close();
                }
            }

            if (keepListening)
            {
                QueueNextAuthenticationRequest();
            }
        }


        #endregion


        #region SAVING AUTHENTICATION


        // Saves incoming user authentication info
        private static void SaveUserAuthentication(string token, string userId, string userName, bool isPreviewAccount)
        {
            try
            {
                // Validate input parameters
                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userName))
                {
                    VERADebugger.LogError($"Invalid parameters - Token: {string.IsNullOrEmpty(token)}, UserID: {string.IsNullOrEmpty(userId)}, UserName: {string.IsNullOrEmpty(userName)}", "VERA Authentication");
                    return;
                }

                // Get current auth info, to not overwrite other existing info
                VERAUserAuthInfo newAuthInfo = new VERAUserAuthInfo();

                // Set info
                newAuthInfo.authenticated = true;
                newAuthInfo.userAuthToken = token;
                newAuthInfo.userId = userId;
                newAuthInfo.userName = userName;
                newAuthInfo.isPreviewAccount = isPreviewAccount;

                // Push to file (updates PlayerPrefs as well)
                SetSavedUserAuthInfo(newAuthInfo);
            }
            catch (Exception ex)
            {
                VERADebugger.LogError($"Error in SaveUserAuthentication: {ex.Message}\nStack trace: {ex.StackTrace}", "VERA Authentication");
            }
        }

        // Saves incoming build authentication info
        // Does NOT adjust active experiment or site, which is handled elsewhere when swapping exp/site
        private static void SaveBuildAuthentication(string token)
        {
            // Get current auth info, to not overwrite other existing info
            // (i.e., does not overwrite active experiment or site)
            VERABuildAuthInfo newAuthInfo = GetSavedBuildAuthInfo();

            // Set info
            newAuthInfo.authenticated = true;
            newAuthInfo.buildAuthToken = token;

            // Push to file (updates PlayerPrefs as well)
            SetSavedBuildAuthInfo(newAuthInfo);
        }

        // Clears the build authentication info, but keeps the active experiment and site
        private static void ClearBuildAuthentication()
        {
            // Get current auth info, to not overwrite other existing info
            // (i.e., does not overwrite active experiment or site)
            VERABuildAuthInfo newAuthInfo = GetSavedBuildAuthInfo();

            // Set info
            newAuthInfo.authenticated = false;
            newAuthInfo.buildAuthToken = String.Empty;

            // Push to file (updates PlayerPrefs as well)
            SetSavedBuildAuthInfo(newAuthInfo);
        }

        // Clears various authentication parameters
        private static void SaveUserDeauthentication()
        {
            // Set info to default (deauthenticated / no info)
            VERAUserAuthInfo userDeauthInfo = new VERAUserAuthInfo();
            VERABuildAuthInfo buildDeauthInfo = new VERABuildAuthInfo();

            VERABuildAuthInfo currentBuildInfo = GetSavedBuildAuthInfo();

            // Maintain the current active experiment and site, but not authentication
            if (currentBuildInfo != null)
            {
                buildDeauthInfo.activeExperiment = currentBuildInfo.activeExperiment;
                buildDeauthInfo.activeExperimentName = currentBuildInfo.activeExperimentName;
                buildDeauthInfo.activeSite = currentBuildInfo.activeSite;
                buildDeauthInfo.activeSiteName = currentBuildInfo.activeSiteName;
                buildDeauthInfo.isMultiSite = currentBuildInfo.isMultiSite;
                buildDeauthInfo.currentBuildNumber = currentBuildInfo.currentBuildNumber;
                buildDeauthInfo.dataRecordingType = currentBuildInfo.dataRecordingType;
                buildDeauthInfo.debugPreference = currentBuildInfo.debugPreference;
                buildDeauthInfo.rotationFormat = currentBuildInfo.rotationFormat;
                buildDeauthInfo.autoStartParticipantSessions = currentBuildInfo.autoStartParticipantSessions;
            }

            SetSavedUserAuthInfo(userDeauthInfo);
            SetSavedBuildAuthInfo(buildDeauthInfo);
        }

        // Clears various authentication parameters
        public static void ClearAuthentication()
        {
            // Save
            EditorApplication.delayCall += () =>
            {
                SaveUserDeauthentication();
            };
        }

        // Sets the saved build authentication info to a new authInfo
        private static void SetSavedBuildAuthInfo(VERABuildAuthInfo authInfo)
        {
            // Convert to JSON
            string json = JsonUtility.ToJson(authInfo, true); // Pretty print for readability

            // File paths
            string directoryPath = GetBuildAuthPath();
            string filePath = Path.Combine(directoryPath, buildAuthFileName);

            // Write to the file
            File.WriteAllText(filePath, json);

            // Update PlayerPrefs
            PlayerPrefs.SetString("VERA_BuildAuthToken", authInfo.buildAuthToken);
            PlayerPrefs.SetString("VERA_ActiveExperiment", authInfo.activeExperiment);
            PlayerPrefs.SetString("VERA_ActiveSite", authInfo.activeSite);
            PlayerPrefs.SetInt("VERA_BuildAuthenticated", authInfo.authenticated ? 1 : 0);
            PlayerPrefs.SetInt("VERA_DataRecordingType", (int)authInfo.dataRecordingType);
            PlayerPrefs.SetInt("VERA_DebugPreference", (int)authInfo.debugPreference);
            PlayerPrefs.SetInt("VERA_AutoStartParticipantSessions", authInfo.autoStartParticipantSessions ? 1 : 0);

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
        }


        // Sets the saved user authentication info to a new authInfo
        private static void SetSavedUserAuthInfo(VERAUserAuthInfo authInfo)
        {
            try
            {
                // Convert to JSON
                string json = JsonUtility.ToJson(authInfo, true); // Pretty print for readability

                // File paths
                string directoryPath = GetUserAuthPath();
                string filePath = Path.Combine(directoryPath, userAuthFileName);

                // Write to the file
                File.WriteAllText(filePath, json);

                // Update PlayerPrefs
                PlayerPrefs.SetString("VERA_UserId", authInfo.userId);
                PlayerPrefs.SetString("VERA_UserName", authInfo.userName);
                PlayerPrefs.SetString("VERA_UserAuthToken", authInfo.userAuthToken);
                PlayerPrefs.SetInt("VERA_UserAuthenticated", authInfo.authenticated ? 1 : 0);
                PlayerPrefs.SetInt("VERA_IsPreviewAccount", authInfo.isPreviewAccount ? 1 : 0);

                // Force save PlayerPrefs
                PlayerPrefs.Save();

                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
            }
            catch (Exception ex)
            {
                VERADebugger.LogError($"Failed to save user authentication: {ex.Message}\nStack trace: {ex.StackTrace}", "VERA Authentication");
            }
        }


        // Gets saved build authentication info (file in StreamingAssets)
        internal static VERABuildAuthInfo GetSavedBuildAuthInfo()
        {
            // File paths
            string directoryPath = GetBuildAuthPath();
            string filePath = Path.Combine(directoryPath, buildAuthFileName);

            // Ensure file exists
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                VERABuildAuthInfo authInfo = JsonUtility.FromJson<VERABuildAuthInfo>(json);
                // JsonUtility defaults missing bools to false; auto-start should remain enabled unless explicitly disabled
                if (authInfo != null && !json.Contains("\"autoStartParticipantSessions\""))
                    authInfo.autoStartParticipantSessions = true;
                return authInfo;
            }
            else
            {
                // File not found, authentication likely not set up yet
                return new VERABuildAuthInfo();
            }
        }


        // Gets saved user authentication info
        private static VERAUserAuthInfo GetSavedUserAuthInfo()
        {
            // File paths
            string directoryPath = GetUserAuthPath();
            string filePath = Path.Combine(directoryPath, userAuthFileName);

            // Ensure file exists
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                return JsonUtility.FromJson<VERAUserAuthInfo>(json);
            }
            else
            {
                // File not found, authentication likely not set up yet
                return new VERAUserAuthInfo();
            }
        }


        // Gets and returns the path to the build authentication file
        private static string GetBuildAuthPath()
        {
            // File paths
            string directoryPath = Path.Combine(Application.dataPath, "VERA", "Authentication", "Resources");

            // Ensure the directory exists
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            return directoryPath;
        }


        // Gets and returns the path to the user authentication file
        private static string GetUserAuthPath()
        {
            // File paths
            string directoryPath = Path.Combine(Application.dataPath, "VERA", "Authentication", "Editor");

            // Ensure the directory exists
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            return directoryPath;
        }


        #endregion


        #region USER CONNECTION


        // Gets whether the current user is connected to the VERA portal or not
        public static void IsUserConnected(Action<bool> onComplete)
        {
            // Check whether user is authenticated or not
            if (PlayerPrefs.GetInt("VERA_UserAuthenticated", 0) == 0)
            {
                onComplete?.Invoke(false);
                return;
            }

            // To test connection, make a request to get this user's experiments
            string url = $"{VERAHost.hostUrl}/api/experiments/";

            // Create a UnityWebRequest with the POST method
            UnityWebRequest request = new UnityWebRequest(url, "GET");
            request.downloadHandler = new DownloadHandlerBuffer();
            request.uploadHandler = new UploadHandlerRaw(new byte[0]); // Empty body

            // Set headers
            request.SetRequestHeader("Content-Type", "application/json");
            VERAHost.ApplyBearerAuth(request, PlayerPrefs.GetString("VERA_UserAuthToken"));

            // Send the request
            var operation = request.SendWebRequest();

            // Use EditorApplication.update to check the request's progress
            EditorApplication.update += EditorUpdate;

            void EditorUpdate()
            {
                if (operation.isDone)
                {
                    EditorApplication.update -= EditorUpdate;

                    // Check for errors
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        onComplete?.Invoke(false);
                        request.Dispose();
                        return;
                    }

                    onComplete?.Invoke(true);
                    request.Dispose();
                    return;
                }
            }
        }


        #endregion


        #region BUILD AUTHENTICATION


        // Gets build authentication token for a specific experiment
        public static void GetBuildAuthToken(string experimentId, Action<bool> onComplete)
        {
            string url = $"{VERAHost.hostUrl}/api/experiments/{experimentId}/authtoken";

            // Create a UnityWebRequest with the GET method
            UnityWebRequest request = new UnityWebRequest(url, "GET");
            request.downloadHandler = new DownloadHandlerBuffer();
            request.uploadHandler = new UploadHandlerRaw(new byte[0]); // Empty body

            // Set headers
            request.SetRequestHeader("Content-Type", "application/json");
            VERAHost.ApplyBearerAuth(request, PlayerPrefs.GetString("VERA_UserAuthToken"));

            // Send the request
            var operation = request.SendWebRequest();

            // Use EditorApplication.update to check the request's progress
            EditorApplication.update += EditorUpdate;

            void EditorUpdate()
            {
                if (operation.isDone)
                {
                    EditorApplication.update -= EditorUpdate;

                    // Check for errors
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                        {
                            VERADebugger.LogError("There was an issue connecting to the VERA portal to get build authentication. " +
                                "Please check your connection and try again.", "VERA Authentication");
                        }

                        ClearBuildAuthentication();
                        onComplete?.Invoke(false);
                    }
                    else
                    {
                        // Parse the response
                        string jsonResponse = request.downloadHandler.text;

                        try
                        {
                            // Parse the JSON response to extract the token
                            var responseObject = JObject.Parse(jsonResponse);
                            string token = responseObject["token"]?.ToString();

                            if (!string.IsNullOrEmpty(token))
                            {
                                // Save the build authentication token
                                SaveBuildAuthentication(token);
                                onComplete?.Invoke(true);
                            }
                            else
                            {
                                VERADebugger.LogError($"Failed to parse authentication response: {jsonResponse}", "VERA Authentication");
                                ClearBuildAuthentication();
                                onComplete?.Invoke(false);
                            }
                        }
                        catch (Exception e)
                        {
                            VERADebugger.LogError($"Failed to parse authentication response: {e.Message}", "VERA Authentication");
                            ClearBuildAuthentication();
                            onComplete?.Invoke(false);
                        }
                    }

                    // Dispose of the request
                    request.Dispose();
                }
            }
        }


        #endregion


        #region EXPERIMENT MANAGEMENT


        // Gets all experiments associated with a user
        public static void GetUserExperiments(Action<List<Experiment>> onComplete)
        {
            List<Experiment> ret = new List<Experiment>();

            string url = $"{VERAHost.hostUrl}/api/experiments/";

            // Create a UnityWebRequest with the POST method
            UnityWebRequest request = new UnityWebRequest(url, "GET");
            request.downloadHandler = new DownloadHandlerBuffer();
            request.uploadHandler = new UploadHandlerRaw(new byte[0]); // Empty body

            // Set headers
            request.SetRequestHeader("Content-Type", "application/json");
            VERAHost.ApplyBearerAuth(request, PlayerPrefs.GetString("VERA_UserAuthToken"));

            // Send the request
            var operation = request.SendWebRequest();

            // Use EditorApplication.update to check the request's progress
            EditorApplication.update += EditorUpdate;

            void EditorUpdate()
            {
                if (operation.isDone)
                {
                    EditorApplication.update -= EditorUpdate;

                    // Check for errors
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        if (request.result == UnityWebRequest.Result.ConnectionError)
                        {
                            VERADebugger.LogError("There was an issue connecting to the VERA portal, and you have been logged out. " +
                                "Please re-authenticate using the \"VERA -> Settings\" menu item.", "VERA Authentication");
                        }
                        else if (request.result == UnityWebRequest.Result.ProtocolError)
                        {
                            VERADebugger.LogError("You are not authenticated, and will not be able to run experiments. " +
                                "Use the \"VERA -> Settings\" menu bar item to authenticate.", "VERA Authentication");
                        }

                        ClearAuthentication();
                    }
                    else
                    {
                        // Parse the response
                        string jsonResponse = request.downloadHandler.text;

                        // Deserialize JSON to GetExperimentsResponse
                        GetExperimentsResponse response = JsonUtility.FromJson<GetExperimentsResponse>(jsonResponse);

                        if (response != null && response.success)
                        {
                            // Access the list of experiments
                            foreach (Experiment exp in response.experiments)
                            {
                                if (exp.sites.Count == 0)
                                {
                                    Site emptySite = new Site();
                                    emptySite._id = "none";
                                    emptySite.name = "none";
                                    emptySite.parentExperiment = exp._id;
                                    exp.sites.Add(emptySite);
                                }
                                ret.Add(exp);
                            }

                            onComplete?.Invoke(ret);
                        }
                        else
                        {
                            VERADebugger.LogError("Received an unexpected response from the VERA portal when fetching experiments. " +
                                "Please try again later.", "VERA Authentication");

                            onComplete?.Invoke(null);
                        }
                    }

                    // Dispose of the request
                    request.Dispose();
                }
            }
        }

        // Changes the currently active experiment
        public static void ChangeActiveExperiment(string activeExperimentId, string activeExperimentName, bool isMultiSite, int currentBuildNumber)
        {
            // Get current auth info, to not overwrite other existing info
            VERABuildAuthInfo currentAuthInfo = GetSavedBuildAuthInfo();

            // Set info
            currentAuthInfo.activeExperiment = activeExperimentId;
            currentAuthInfo.activeExperimentName = activeExperimentName;
            currentAuthInfo.isMultiSite = isMultiSite;
            currentAuthInfo.currentBuildNumber = currentBuildNumber;

            // Push to file (updates PlayerPrefs as well)
            SetSavedBuildAuthInfo(currentAuthInfo);

            // Update session state for dev tools sim participant to avoid inter-experiment conflicts
            SessionState.SetBool("VERA_SimParticipant", false);

            // Update all column definition assets to this new experiment
            UpdateColumnDefs();

            // Clear generated condition code and build authentication if no active experiment
            if (string.IsNullOrEmpty(activeExperimentId))
            {
                ConditionGenerator.ClearAllConditionCsCode();
                ClearBuildAuthentication();
                return;
            }

            // Update authentication token for the new experiment to allow data collection.
            // Survey helper generation is deferred until after the new token is saved, to avoid
            // fetching surveys with a stale/invalid token and getting a 400 response.
            GetBuildAuthToken(activeExperimentId, (success) =>
            {
                if (!success)
                {
                    VERADebugger.LogError("Failed to authenticate for experiment. Cannot change active experiment. " +
                        "Please check your internet connection, refresh experiments, and try again.", "VERA Authentication");
                }
                else
                {
                    // Generate survey helper code and SurveyInfo assets now that the correct token is saved
                    SurveyHelperGenerator.FetchAndConvertSurveys();
                }
            });
        }

        // Changes the currently active site
        public static void ChangeActiveSite(string activeSiteId, string activeSiteName)
        {
            // Get current auth info, to not overwrite other existing info
            VERABuildAuthInfo currentAuthInfo = GetSavedBuildAuthInfo();

            // Set info
            currentAuthInfo.activeSite = activeSiteId;
            currentAuthInfo.activeSiteName = activeSiteName;

            // Push to file (updates PlayerPrefs as well)
            SetSavedBuildAuthInfo(currentAuthInfo);
        }

        /// <summary>
        /// Changes the data recording type setting.
        /// </summary>
        /// <param name="recordingType">The new data recording type to set.</param>
        public static void ChangeDataRecordingType(DataRecordingType recordingType)
        {
            // Get current auth info, to not overwrite other existing info
            VERABuildAuthInfo currentAuthInfo = GetSavedBuildAuthInfo();

            // Set info
            currentAuthInfo.dataRecordingType = recordingType;

            // Push to file (updates PlayerPrefs as well)
            SetSavedBuildAuthInfo(currentAuthInfo);
        }

        /// <summary>
        /// Gets the current data recording type setting.
        /// </summary>
        /// <returns>The current data recording type.</returns>
        public static DataRecordingType GetDataRecordingType()
        {
            VERABuildAuthInfo currentAuthInfo = GetSavedBuildAuthInfo();
            return currentAuthInfo.dataRecordingType;
        }

        /// <summary>
        /// Changes the debug preference setting.
        /// </summary>
        /// <param name="debugPreference">The new debug preference to set.</param>
        public static void ChangeDebugPreference(DebugPreference debugPreference)
        {
            // Get current auth info, to not overwrite other existing info
            VERABuildAuthInfo currentAuthInfo = GetSavedBuildAuthInfo();

            // Set info
            currentAuthInfo.debugPreference = debugPreference;

            // Push to file (updates PlayerPrefs as well)
            SetSavedBuildAuthInfo(currentAuthInfo);
        }

        /// <summary>
        /// Gets the current debug preference setting.
        /// </summary>
        /// <returns>The current debug preference.</returns>
        public static DebugPreference GetDebugPreference()
        {
            VERABuildAuthInfo currentAuthInfo = GetSavedBuildAuthInfo();
            return currentAuthInfo.debugPreference;
        }

        /// <summary>
        /// Changes the rotation format setting for transform data logging.
        /// </summary>
        /// <param name="rotationFormat">The new rotation format to set.</param>
        public static void ChangeRotationFormat(RotationFormat rotationFormat)
        {
            // Get current auth info, to not overwrite other existing info
            VERABuildAuthInfo currentAuthInfo = GetSavedBuildAuthInfo();

            // Set info
            currentAuthInfo.rotationFormat = rotationFormat;

            // Push to file (updates PlayerPrefs as well)
            SetSavedBuildAuthInfo(currentAuthInfo);
        }

        /// <summary>
        /// Gets the current rotation format setting.
        /// </summary>
        /// <returns>The current rotation format.</returns>
        public static RotationFormat GetRotationFormat()
        {
            VERABuildAuthInfo currentAuthInfo = GetSavedBuildAuthInfo();
            return currentAuthInfo.rotationFormat;
        }

        /// <summary>
        /// Changes whether VERA automatically starts a participant session on application start.
        /// </summary>
        /// <param name="autoStart">True to create a participant and begin data collection automatically; false to wait for StartNewParticipantSession().</param>
        public static void ChangeAutoStartParticipantSessions(bool autoStart)
        {
            VERABuildAuthInfo currentAuthInfo = GetSavedBuildAuthInfo();
            currentAuthInfo.autoStartParticipantSessions = autoStart;
            SetSavedBuildAuthInfo(currentAuthInfo);
        }

        /// <summary>
        /// Gets whether VERA automatically starts a participant session on application start.
        /// Defaults to true when the setting has not been stored yet.
        /// </summary>
        public static bool GetAutoStartParticipantSessions()
        {
            VERABuildAuthInfo currentAuthInfo = GetSavedBuildAuthInfo();
            return currentAuthInfo != null && currentAuthInfo.autoStartParticipantSessions;
        }


        #endregion

        public static void GetIVGroupConditions(string experimentId, string ivName, Action<IVGroup> onComplete)
        {
            if (string.IsNullOrEmpty(experimentId) || string.IsNullOrEmpty(ivName))
            {
                onComplete?.Invoke(null);
                return;
            }

            string url = $"{VERAHost.hostUrl}/api/experiments/{experimentId}/conditions/{Uri.EscapeDataString(ivName)}";

            UnityWebRequest request = new UnityWebRequest(url, "GET");
            request.downloadHandler = new DownloadHandlerBuffer();
            request.uploadHandler = new UploadHandlerRaw(new byte[0]);
            request.SetRequestHeader("Content-Type", "application/json");
            VERAHost.ApplyBearerAuth(request, PlayerPrefs.GetString("VERA_UserAuthToken"));

            var operation = request.SendWebRequest();

            EditorApplication.update += EditorUpdate;

            void EditorUpdate()
            {
                if (!operation.isDone) return;
                EditorApplication.update -= EditorUpdate;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    VERADebugger.LogWarning($"Failed to fetch IV group conditions for {ivName}: {request.error}", "VERA Authentication");
                    request.Dispose();
                    onComplete?.Invoke(null);
                    return;
                }

                try
                {
                    string json = request.downloadHandler.text;
                    // Parse with JObject to be resilient to shape
                    var j = JObject.Parse(json);
                    if (j == null) { onComplete?.Invoke(null); request.Dispose(); return; }

                    IVGroup group = new IVGroup();
                    group.ivName = j.Value<string>("ivName") ?? ivName;
                    group.conditions = new List<Condition>();

                    var conds = j["conditions"] as JArray;
                    if (conds != null)
                    {
                        foreach (var c in conds)
                        {
                            Condition cc = new Condition();
                            cc.name = c.Value<string>("name");
                            cc.encoding = c.Value<string>("encoding");
                            cc._id = c.Value<string>("_id");
                            group.conditions.Add(cc);
                        }
                    }

                    onComplete?.Invoke(group);
                }
                catch (Exception e)
                {
                    VERADebugger.LogWarning($"Failed parsing IV group conditions for {ivName}: {e.Message}", "VERA Authentication");
                    onComplete?.Invoke(null);
                }
                finally
                {
                    request.Dispose();
                }
            }
        }


        #region FILE TYPE / COLUMN MANAGEMENT


        [MenuItem("VERA/Refresh File Types")]
        public static void MenuRefreshFileTypes()
        {
            if (PlayerPrefs.GetInt("VERA_UserAuthenticated") != 1)
            {
                VERADebugger.LogError("You must be authenticated to refresh file types. Open VERA -> Settings and authenticate first.", "VERA Authentication");
                return;
            }

            UpdateColumnDefs();
        }


        // Updates the column definition to the current experiment's column definition
        public static void UpdateColumnDefs()
        {
            // If there is no active experiment, we cannot do anything with the columns
            if (PlayerPrefs.GetString("VERA_ActiveExperiment", null) == null || PlayerPrefs.GetString("VERA_ActiveExperiment", null) == "")
            {
                DeleteExistingColumnDefs();
                ClearFileTypeDefineSymbols();
                return;
            }

            // Start by getting all FileTypes for the experiment;
            // Then, filter only by those which are CSV's; each CSV will have an associated column definition.
            // Make a new column definition asset for each CSV FileType, based on the FileType's fetched definition.
            // These column def's will be used by the VERALogger to record data.

            // URL to get all FileTypes for this experiment
            string url = $"{VERAHost.hostUrl}/api/experiments/{PlayerPrefs.GetString("VERA_ActiveExperiment")}/filetypes";

            // Create a UnityWebRequest with the GET method
            UnityWebRequest request = new UnityWebRequest(url, "GET");
            request.downloadHandler = new DownloadHandlerBuffer();
            request.uploadHandler = new UploadHandlerRaw(new byte[0]); // Empty body

            // Set headers
            request.SetRequestHeader("Content-Type", "application/json");
            VERAHost.ApplyBearerAuth(request, PlayerPrefs.GetString("VERA_UserAuthToken"));

            // Send the request
            var operation = request.SendWebRequest();

            // Use EditorApplication.update to check the request's progress
            EditorApplication.update += EditorUpdate;

            void EditorUpdate()
            {
                if (operation.isDone)
                {
                    EditorApplication.update -= EditorUpdate;
                    // On error, can't make any column definitions
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        VERADebugger.LogError("Unexpected response from server; could not get column definitions. " +
                                "Please try refreshing your experiments and trying again.", "VERA Authentication");
                        request.Dispose();
                        return;
                    }
                    else
                    {
                        // Parse the response
                        string jsonResponse = request.downloadHandler.text;
                        FileTypesResponse fileTypesResponse = JsonUtility.FromJson<FileTypesResponse>(jsonResponse);

                        if (fileTypesResponse == null || !fileTypesResponse.success || fileTypesResponse.fileTypes == null)
                        {
                            VERADebugger.LogError("Unexpected response from server; could not get column definitions. " +
                                "Please try refreshing your experiments and trying again.", "VERA Authentication");
                            request.Dispose();
                            return;
                        }

                        List<FtFileType> fileTypes = fileTypesResponse.fileTypes;

                        if (FileTypesAreUpToDate(fileTypes))
                        {
                            VERADebugger.Log(
                                "File types are already up to date; skipped regeneration.",
                                "VERA Authentication",
                                DebugPreference.Verbose);
                            request.Dispose();
                            return;
                        }

                        // Only wipe local column defs after a successful fetch so a failed request
                        // does not leave the project with no file types until the next re-auth.
                        DeleteExistingColumnDefs();

                        List<VERAColumnDefinition> columnDefs = new List<VERAColumnDefinition>();
                        List<string> definitionsToAdd = new List<string>();
                        for (int i = 0; i < fileTypes.Count; i++)
                        {
                            // Skip Experiment_Telemetry — its column definition is managed by
                            // VERABaselineDataSetup with hardcoded correct types. We only need
                            // to update its fileTypeId (handled below this loop).
                            if (fileTypes[i].name == VERAExperimentTelemetrySchema.Name)
                            {
                                continue;
                            }
                            // Skip Survey_Responses here - it's handled specially below with the programmatic definition
                            string normalizedNameCheck = (fileTypes[i].name ?? "").ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
                            if (normalizedNameCheck == "surveyresponses")
                                continue;

                            // Ensure the directory exists in the Packages folder before creating the asset
                            string columnsPath = GetAbsoluteColumnsFilePath();
                            if (!Directory.Exists(columnsPath))
                            {
                                Directory.CreateDirectory(columnsPath);
                                AssetDatabase.Refresh();
                            }

                            // Sanitize the filename to remove any invalid characters
                            string sanitizedName = Regex.Replace(fileTypes[i].name ?? "Unnamed", @"[<>:""/\\|?*]", "_");
                            string relativePath = GetRelativeColumnsFilePath() + "/VERA_" + sanitizedName + "_ColumnDefinition.asset";

                            bool isCsv = string.IsNullOrEmpty(fileTypes[i].extension) ||
                                         fileTypes[i].extension.Equals("csv", StringComparison.OrdinalIgnoreCase);

                            columnDefs.Add(ScriptableObject.CreateInstance<VERAColumnDefinition>());
                            int idx = columnDefs.Count - 1;

                            try
                            {
                                AssetDatabase.CreateAsset(columnDefs[idx], relativePath);
                            }
                            catch (System.Exception e)
                            {
                                VERADebugger.LogError($"Failed to create asset at path '{relativePath}': {e.Message}", "VERA Authentication");
                                continue;
                            }

                            columnDefs[idx].columns.Clear();

                            if (isCsv && fileTypes[i].columnDefinition?.columns != null)
                            {
                                // Sort the columns based on order
                                List<FtColumn> sortedCols = fileTypes[i].columnDefinition.columns.OrderBy(col => col.order).ToList();

                                for (int colIndex = 0; colIndex < sortedCols.Count; colIndex++)
                                {
                                    FtColumn col = sortedCols[colIndex];

                                    // LEGACY: Skip the fourth column if it's "eventId"
                                    if (colIndex == 3 && col.name?.ToLower() == "eventid")
                                    {
                                        continue;
                                    }

                                    VERAColumnDefinition.Column newCol = new VERAColumnDefinition.Column();
                                    newCol.name = col.name;
                                    newCol.description = col.description;
                                    newCol.type = MapApiDataType(col.dataType);

                                    columnDefs[idx].columns.Add(newCol);
                                }
                            }
                            else if (isCsv && (fileTypes[i].columnDefinition == null || fileTypes[i].columnDefinition.columns == null))
                            {
                                VERADebugger.LogWarning(
                                    $"File type \"{fileTypes[i].name}\" is a CSV but the portal did not return a column definition. " +
                                    "A wrapper class will still be generated; add columns on the portal and refresh file types to update it.",
                                    "VERA Authentication");
                            }

                            columnDefs[idx].fileType = new VERAColumnDefinition.FileType();
                            columnDefs[idx].fileType.fileTypeId = fileTypes[i]._id;
                            columnDefs[idx].fileType.name = fileTypes[i].name;
                            columnDefs[idx].fileType.description = fileTypes[i].description;
                            columnDefs[idx].fileType.extension = isCsv ? "csv" : fileTypes[i].extension;

                            EditorUtility.SetDirty(columnDefs[idx]);
                            AssetDatabase.SaveAssets();

                            // Generate from the in-memory asset so we do not depend on Resources.LoadAll
                            // seeing a brand-new asset in the same editor update tick.
                            FileTypeGenerator.GenerateFileTypeCsCode(columnDefs[idx], false);

                            definitionsToAdd.Add("VERAFile_" + fileTypes[i].name);
                        }

                        // Update the baseline telemetry column definition's fileTypeId
                        // The baseline definition is created locally with a placeholder "baseline-data" ID,
                        // but it needs the real server-assigned ID for uploads to succeed.
                        // Special handling for Survey_Responses: create column definition even without server-side columns
                        // The columns are predefined in Unity since survey responses have a fixed schema
                        for (int i = 0; i < fileTypes.Count; i++)
                        {
                            string normalizedName = (fileTypes[i].name ?? "").ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
                            if (normalizedName == "surveyresponses" &&
                                (string.IsNullOrEmpty(fileTypes[i].extension) ||
                                 fileTypes[i].extension.Equals("csv", StringComparison.OrdinalIgnoreCase)))
                            {
                                // Check if we already created this in the loop above
                                bool alreadyCreated = false;
                                foreach (var def in columnDefs)
                                {
                                    if (def.fileType.fileTypeId == fileTypes[i]._id)
                                    {
                                        alreadyCreated = true;
                                        break;
                                    }
                                }

                                if (!alreadyCreated)
                                {
                                    // Create Survey_Responses column definition with predefined columns
                                    var surveyDef = VERASurveyResponseColumnDefinition.Create();
                                    surveyDef.fileType.fileTypeId = fileTypes[i]._id; // Use server's _id

                                    string columnsPath = GetAbsoluteColumnsFilePath();
                                    if (!Directory.Exists(columnsPath))
                                    {
                                        Directory.CreateDirectory(columnsPath);
                                        AssetDatabase.Refresh();
                                    }

                                    string relativePath = GetRelativeColumnsFilePath() + "/VERA_Survey_Responses_ColumnDefinition.asset";
                                    AssetDatabase.CreateAsset(surveyDef, relativePath);
                                    EditorUtility.SetDirty(surveyDef);
                                    AssetDatabase.SaveAssets();

                                    definitionsToAdd.Add("VERAFile_Survey_Responses");
                                    VERADebugger.Log($"Created Survey_Responses column definition with ID: {fileTypes[i]._id}", "VERA Authentication");
                                }
                            }
                            if (normalizedName == "experimenttelemetry")
                            {
                                var baselineColumnDef = Resources.Load<VERAColumnDefinition>(VERAExperimentTelemetrySchema.Name + "ColumnDefinition");
                                if (baselineColumnDef != null)
                                {
                                    baselineColumnDef.fileType.fileTypeId = fileTypes[i]._id;
                                    EditorUtility.SetDirty(baselineColumnDef);
                                    AssetDatabase.SaveAssets();
                                    VERADebugger.Log($"Updated baseline telemetry fileTypeId to \"{fileTypes[i]._id}\".", "VERA Authentication");
                                }
                            }
                        }

                        // Keep the locally-managed telemetry define so ReplaceDefines cannot
                        // remove it and trigger a recompile / InitializeOnLoad add-back cycle.
                        string telemetrySymbol = "VERAFile_" + VERAExperimentTelemetrySchema.Name;
                        if (!definitionsToAdd.Contains(telemetrySymbol))
                            definitionsToAdd.Add(telemetrySymbol);

                        // Generate remaining wrappers (e.g. baseline telemetry). Individual files
                        // are imported only when their contents actually change.
                        FileTypeGenerator.GenerateAllFileTypesCsCode();
                        ReplaceDefines(definitionsToAdd);

                        if (definitionsToAdd.Count > 0)
                        {
                            VERADebugger.Log(
                                $"Synced {definitionsToAdd.Count} file type(s) from the portal: {string.Join(", ", definitionsToAdd)}",
                                "VERA Authentication");
                        }
                        else
                        {
                            VERADebugger.LogWarning(
                                "No file types were generated for this experiment. If you just created a file type on the portal, confirm it is saved, then use VERA -> Refresh File Types.",
                                "VERA Authentication");
                        }
                    }

                    request.Dispose();
                }
            }
        }

        // Deletes existing column definitions in the columns folder, preserving the
        // locally-managed Experiment_Telemetry assets created by VERABaselineDataSetup.
        public static void DeleteExistingColumnDefs()
        {
            string columnsFilePath = GetAbsoluteColumnsFilePath();
            if (Directory.Exists(columnsFilePath))
            {
                string[] files = Directory.GetFiles(columnsFilePath);

                foreach (string file in files)
                {
                    if (Path.GetFileName(file).IndexOf(VERAExperimentTelemetrySchema.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    try
                    {
                        File.Delete(file);
                    }
                    catch (IOException e)
                    {
                        VERADebugger.LogError($"IO Exception deleting file: {file}\n{e.Message}", "VERA Authentication");
                    }
                }
            }

            AssetDatabase.Refresh();
        }


        // Gets the path to the columns folder
        private static string GetAbsoluteColumnsFilePath()
        {
            string absolutePath = Path.Combine(Application.dataPath, "VERA", "Resources");
            return absolutePath;
        }

        // Gets the relative path to the columns folder
        private static string GetRelativeColumnsFilePath()
        {
            string relativePath = "Assets/VERA/Resources";
            return relativePath;
        }

        private static readonly HashSet<string> AutoColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "pid", "conditions", "ts", "timestamp", "timestamp_utc", "eventid"
        };

        // True when local column defs, generated wrappers, and scripting defines already
        // match the portal file types — so regeneration would be a no-op besides churn.
        private static bool FileTypesAreUpToDate(List<FtFileType> fileTypes)
        {
            List<VERAColumnDefinition> existingDefs = LoadLocalColumnDefs();
            HashSet<string> expectedNames = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> expectedDefines = new HashSet<string> { "VERAFile_" + VERAExperimentTelemetrySchema.Name };

            for (int i = 0; i < fileTypes.Count; i++)
            {
                FtFileType ft = fileTypes[i];
                if (string.IsNullOrEmpty(ft.name))
                    return false;

                string normalizedName = (ft.name ?? "").ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
                bool isTelemetry = ft.name == VERAExperimentTelemetrySchema.Name || normalizedName == "experimenttelemetry";
                bool isSurvey = normalizedName == "surveyresponses";

                expectedDefines.Add("VERAFile_" + (isSurvey ? "Survey_Responses" : ft.name));

                if (isTelemetry)
                    continue;

                expectedNames.Add(isSurvey ? "Survey_Responses" : ft.name);

                VERAColumnDefinition local = existingDefs.FirstOrDefault(d =>
                    d.fileType != null &&
                    string.Equals(d.fileType.name, isSurvey ? "Survey_Responses" : ft.name, StringComparison.Ordinal));

                if (local == null || local.fileType == null)
                    return false;

                if (!string.Equals(local.fileType.fileTypeId, ft._id, StringComparison.Ordinal))
                    return false;

                bool remoteIsCsv = string.IsNullOrEmpty(ft.extension) ||
                                   ft.extension.Equals("csv", StringComparison.OrdinalIgnoreCase);
                bool localIsCsv = string.IsNullOrEmpty(local.fileType.extension) ||
                                  local.fileType.extension.Equals("csv", StringComparison.OrdinalIgnoreCase);
                if (remoteIsCsv != localIsCsv)
                    return false;
                if (!remoteIsCsv && !string.Equals(local.fileType.extension, ft.extension, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!isSurvey && !ColumnsMatch(local, ft))
                    return false;

                if (!isSurvey)
                {
                    string generatedPath = FileTypeGenerator.GeneratedCsDirectory + "VERAFile_" + ft.name + ".cs";
                    if (!File.Exists(generatedPath))
                        return false;
                }
            }

            foreach (VERAColumnDefinition def in existingDefs)
            {
                if (def?.fileType == null || string.IsNullOrEmpty(def.fileType.name))
                    continue;
                if (def.fileType.name == VERAExperimentTelemetrySchema.Name)
                    continue;
                if (!expectedNames.Contains(def.fileType.name))
                    return false;
            }

            HashSet<string> currentDefines = new HashSet<string>(
                GetDefineSymbols().Select(s => s.Trim()).Where(s => s.StartsWith("VERAFile_")));
            if (!expectedDefines.SetEquals(currentDefines))
                return false;

            string telemetryGeneratedPath = FileTypeGenerator.GeneratedCsDirectory + "VERAFile_" + VERAExperimentTelemetrySchema.Name + ".cs";
            if (!File.Exists(telemetryGeneratedPath))
                return false;

            return true;
        }

        private static List<VERAColumnDefinition> LoadLocalColumnDefs()
        {
            var result = new List<VERAColumnDefinition>();
            string relativeDir = GetRelativeColumnsFilePath();
            string absoluteDir = GetAbsoluteColumnsFilePath();
            if (!Directory.Exists(absoluteDir))
                return result;

            foreach (string file in Directory.GetFiles(absoluteDir, "*.asset"))
            {
                string relativePath = relativeDir + "/" + Path.GetFileName(file);
                var def = AssetDatabase.LoadAssetAtPath<VERAColumnDefinition>(relativePath);
                if (def != null)
                    result.Add(def);
            }

            return result;
        }

        private static bool ColumnsMatch(VERAColumnDefinition local, FtFileType remote)
        {
            List<string> localKeys = new List<string>();
            if (local.columns != null)
            {
                foreach (VERAColumnDefinition.Column col in local.columns)
                {
                    if (col == null || IsIgnoredColumn(col.name))
                        continue;
                    localKeys.Add(col.name + "\0" + col.type);
                }
            }

            List<string> remoteKeys = new List<string>();
            if (remote.columnDefinition?.columns != null)
            {
                foreach (FtColumn col in remote.columnDefinition.columns.OrderBy(c => c.order))
                {
                    if (col == null || IsIgnoredColumn(col.name))
                        continue;
                    remoteKeys.Add(col.name + "\0" + MapApiDataType(col.dataType));
                }
            }

            if (localKeys.Count != remoteKeys.Count)
                return false;

            localKeys.Sort(StringComparer.Ordinal);
            remoteKeys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < localKeys.Count; i++)
            {
                if (localKeys[i] != remoteKeys[i])
                    return false;
            }

            return true;
        }

        private static bool IsIgnoredColumn(string name)
        {
            return string.IsNullOrEmpty(name) || AutoColumnNames.Contains(name);
        }

        private static VERAColumnDefinition.DataType MapApiDataType(string dataType)
        {
            switch ((dataType ?? "").Trim().ToLowerInvariant())
            {
                case "string":
                    return VERAColumnDefinition.DataType.String;
                case "integer":
                case "int":
                case "number":
                    return VERAColumnDefinition.DataType.Number;
                case "transform":
                    return VERAColumnDefinition.DataType.Transform;
                case "date":
                    return VERAColumnDefinition.DataType.Date;
                case "json":
                    return VERAColumnDefinition.DataType.JSON;
                case "boolean":
                case "bool":
                    return VERAColumnDefinition.DataType.Boolean;
                case "float":
                    return VERAColumnDefinition.DataType.Float;
                default:
                    return VERAColumnDefinition.DataType.Date;
            }
        }


        #endregion


        #region PREPROCESSORS / DEFINE SYMBOLS


        // Gets all define symbols
        private static List<string> GetDefineSymbols()
        {
            BuildTarget activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;

#if UNITY_2023_1_OR_NEWER
            // Use the new API for Unity 2023.1 and newer
            NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(activeBuildTarget));
            List<string> currentSymbols = PlayerSettings
                .GetScriptingDefineSymbols(namedBuildTarget).Split(';')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
#else
        // Use the old API for older Unity versions
        BuildTargetGroup activeBuildTargetGroup = BuildPipeline.GetBuildTargetGroup(activeBuildTarget);
        List<string> currentSymbols = PlayerSettings
            .GetScriptingDefineSymbolsForGroup(activeBuildTargetGroup).Split(';')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
#endif

            return currentSymbols;
        }

        // Saves define symbols as per given list to ALL build target groups
        private static void SaveDefineSymbols(List<string> symbols)
        {
            string symbolsString = string.Join(";", symbols);

#if UNITY_2023_1_OR_NEWER
            // Use the new API for Unity 2023.1 and newer
            // Apply to all relevant build target groups
            BuildTargetGroup[] targetGroups = new BuildTargetGroup[]
            {
                BuildTargetGroup.Standalone,
                BuildTargetGroup.iOS,
                BuildTargetGroup.Android,
                BuildTargetGroup.WebGL,
                BuildTargetGroup.WSA,
                BuildTargetGroup.PS4,
                BuildTargetGroup.XboxOne,
                BuildTargetGroup.tvOS,
                BuildTargetGroup.Switch
            };

            foreach (BuildTargetGroup group in targetGroups)
            {
                if (group == BuildTargetGroup.Unknown)
                    continue;

                try
                {
                    NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
                    PlayerSettings.SetScriptingDefineSymbols(namedTarget, symbolsString);
                }
                catch
                {
                    // Skip unsupported or unavailable build targets
                }
            }
#else
            // Use the old API for older Unity versions
            // Apply to all relevant build target groups
            BuildTargetGroup[] targetGroups = new BuildTargetGroup[]
            {
                BuildTargetGroup.Standalone,
                BuildTargetGroup.iOS,
                BuildTargetGroup.Android,
                BuildTargetGroup.WebGL,
                BuildTargetGroup.WSA,
                BuildTargetGroup.PS4,
                BuildTargetGroup.XboxOne,
                BuildTargetGroup.tvOS,
                BuildTargetGroup.Switch
            };

            foreach (BuildTargetGroup group in targetGroups)
            {
                if (group == BuildTargetGroup.Unknown)
                    continue;

                try
                {
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(group, symbolsString);
                }
                catch
                {
                    // Skip unsupported or unavailable build targets
                }
            }
#endif
        }

        // Replaces all define symbols with the given list;
        // does not replace any define symbols that do not need to be replaced.
        private static void ReplaceDefines(List<string> symbols)
        {
            HashSet<string> newSymbols = new HashSet<string>(symbols);

            // Experiment_Telemetry is created locally by VERABaselineDataSetup, not by this
            // portal file-type list. Dropping it here would recompile, then InitializeOnLoad
            // would add it back and recompile again on every refresh.
            string telemetrySymbol = "VERAFile_" + VERAExperimentTelemetrySchema.Name;
            newSymbols.Add(telemetrySymbol);

            List<string> oldVeraSymbols = GetDefineSymbols().Where(s => s.StartsWith("VERAFile_")).ToList();
            HashSet<string> oldVeraSymbols_HashSet = new HashSet<string>(oldVeraSymbols);

            List<string> stringsToAdd = newSymbols.Except(oldVeraSymbols_HashSet).ToList();
            List<string> stringsToDelete = oldVeraSymbols_HashSet.Except(newSymbols).ToList();

            if (stringsToDelete.Count > 0)
            {
                foreach (string s in stringsToDelete)
                {
                    RemoveDefineSymbol(s);
                }
            }

            if (stringsToAdd.Count > 0)
            {
                foreach (string s in stringsToAdd)
                {
                    AddDefineSymbol(s);
                }
            }
        }

        // Adds a define symbol to the Unity player's settings
        private static void AddDefineSymbol(string symbol)
        {
            List<string> currentSymbols = GetDefineSymbols();

            if (!currentSymbols.Contains(symbol))
            {
                currentSymbols.Add(symbol);
                SaveDefineSymbols(currentSymbols);
            }
        }

        // Removes a define symbol from the Unity player's settings
        private static void RemoveDefineSymbol(string symbol)
        {
            List<string> currentSymbols = GetDefineSymbols();

            if (currentSymbols.Contains(symbol))
            {
                currentSymbols.Remove(symbol);
                SaveDefineSymbols(currentSymbols);
            }
        }

        // Removes all VERA-related define symbols from the Unity player's settings
        public static void ClearFileTypeDefineSymbols()
        {
            List<string> currentSymbols = GetDefineSymbols();

            // Remove all that start with "VERAFile"
            currentSymbols.RemoveAll(symbol => symbol.StartsWith("VERAFile"));

            SaveDefineSymbols(currentSymbols);
        }

        // Adds a condition group define symbol to the Unity player's settings
        public static void AddConditionGroupDefineSymbol(string ivName)
        {
            string symbol = $"VERAIV_{ivName}";
            AddDefineSymbol(symbol);
        }

        // Updates all condition group define symbols based on the provided condition groups
        public static void UpdateConditionGroupDefineSymbols(List<IVGroup> conditionGroups)
        {
            if (conditionGroups == null)
            {
                ClearConditionGroupDefineSymbols();
                return;
            }

            List<string> newSymbols = conditionGroups
                .Where(group => !string.IsNullOrEmpty(group.ivName))
                .Select(group => $"VERAIV_{group.ivName}")
                .ToList();

            List<string> currentSymbols = GetDefineSymbols();
            List<string> oldConditionSymbols = currentSymbols.Where(s => s.StartsWith("VERAIV_")).ToList();

            foreach (string oldSymbol in oldConditionSymbols)
            {
                if (!newSymbols.Contains(oldSymbol))
                {
                    RemoveDefineSymbol(oldSymbol);
                }
            }

            foreach (string newSymbol in newSymbols)
            {
                if (!oldConditionSymbols.Contains(newSymbol))
                {
                    AddDefineSymbol(newSymbol);
                }
            }
        }

        // Removes all condition group define symbols from the Unity player's settings
        public static void ClearConditionGroupDefineSymbols()
        {
            List<string> currentSymbols = GetDefineSymbols();
            currentSymbols.RemoveAll(symbol => symbol.StartsWith("VERAIV_"));

            SaveDefineSymbols(currentSymbols);
        }

        /// <summary>
        /// Gets all VERA-related define symbols from the active build target.
        /// This includes file type symbols (VERAFile_*) and condition group symbols (VERAIV_*).
        /// </summary>
        /// <returns>List of VERA-related preprocessor define symbols</returns>
        public static List<string> GetVERADefineSymbols()
        {
            List<string> currentSymbols = GetDefineSymbols();
            return currentSymbols.Where(s => s.StartsWith("VERAFile_") || s.StartsWith("VERAIV_")).ToList();
        }

        /// <summary>
        /// Applies VERA-related define symbols to the active build target.
        /// Adds any VERA symbols that don't already exist without removing other symbols.
        /// </summary>
        /// <param name="veraSymbols">List of VERA-related preprocessor symbols to apply</param>
        public static void ApplyVERADefineSymbols(List<string> veraSymbols)
        {
            if (veraSymbols == null || veraSymbols.Count == 0)
                return;

            List<string> currentSymbols = GetDefineSymbols();
            HashSet<string> currentSet = new HashSet<string>(currentSymbols);
            bool modified = false;

            foreach (string symbol in veraSymbols)
            {
                if (!currentSet.Contains(symbol))
                {
                    currentSymbols.Add(symbol);
                    modified = true;
                }
            }

            if (modified)
            {
                SaveDefineSymbols(currentSymbols);
            }
        }


        #endregion


        #region OTHER HELPERS


        // String helper
        private static string PadBase64(string base64)
        {
            // Ensure the base64 string is properly padded
            return base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        }


        #endregion


    }
#endif

    // JSON helper classes

    [System.Serializable]
    internal class VERAUserAuthInfo
    {
        public bool authenticated = false;
        public string userAuthToken = String.Empty;
        public string userId = String.Empty;
        public string userName = String.Empty;
        public bool isPreviewAccount = true; // Default to true (restricted) if not set
    }

    [System.Serializable]

    internal class Condition
    {
        public string name;
        public string encoding;
        public string _id;
    }

    [System.Serializable]
    internal class IVGroup
    {
        public string ivName;
        public List<Condition> conditions;
        public string _id;
    }

    [System.Serializable]
    internal class Experiment
    {
        public string _id;
        public string name;
        public string createdBy;
        public List<string> users;
        public List<string> participants;
        public bool isMultiSite;
        public int webXrBuildNumber;
        public List<Site> sites = new List<Site>();
        public List<IVGroup> conditions = new List<IVGroup>();
    }

    [System.Serializable]
    internal class Site
    {
        public string _id;
        public string name;
        public string parentExperiment;
    }

    [System.Serializable]
    internal class GetExperimentsResponse
    {
        public bool success;
        public List<Experiment> experiments;
        public List<string> ids;
    }

    [System.Serializable]
    internal class FileTypesResponse
    {
        public bool success;
        public List<FtFileType> fileTypes;
    }

    [System.Serializable]
    internal class FtFileType
    {
        public string _id;
        public string name;
        public string experimentId;
        public string extension;
        public string description;

        public FtColumnDefinition columnDefinition;
    }

    [System.Serializable]
    internal class FtColumnDefinition
    {
        public string _id;
        public string fileTypeId;
        public List<FtColumn> columns;
    }

    [System.Serializable]
    internal class FtColumn
    {
        public string _id;
        public string columnDefinitionId;
        public string dataType;
        public string name;
        public string description;
        public string transform;
        public int order;
    }

    [System.Serializable]
    internal class UnityTokenResponse
    {
        public UserResponse user;
        public string token;
    }

    [System.Serializable]
    internal class UserResponse
    {
        public string _id;
        public string firstName;
        public string lastName;
        public string email;
        public bool previewAccount;
    }
}
