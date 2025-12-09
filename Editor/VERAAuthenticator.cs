using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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

        private const string userAuthFileName = "VERAUserAuthentication.json";
        private const string buildAuthFileName = "VERABuildAuthentication.json";


        #region AUTHENTICATION SERVER CALLS


        public static void StartUserAuthentication()
        {

            // Start the server
            StartUserAuthServer();
            // Open the authentication URL in the default browser
            Application.OpenURL(VERAHost.hostUrl + "/Authenticate");
        }

        // Starts the server
        private static void StartUserAuthServer()
        {
            if (listener == null)
            {
                listener = new HttpListener();
                listener.Prefixes.Add(listenUrl + "/");
            }

            if (!listener.IsListening)
            {
                listener.Start();
                isRunning = true;
                listener.BeginGetContext(HandleAuthenticationRequest, listener);
            }
        }

        // Stops the server
        private static void StopUserAuthServer()
        {
            if (listener != null && listener.IsListening)
            {
                isRunning = false;
                listener.Stop();
                listener.Close();
                listener = null;
            }
        }

        // Handles the authentication request
        private static void HandleAuthenticationRequest(IAsyncResult result)
        {
            if (!isRunning) return;

            Debug.Log($"[VERA Authentication] Sending request for authentication to the VERA portal...");

            var context = listener.EndGetContext(result);
            var request = context.Request;

            // Enable CORS
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            // Handle CORS Preflight
            if (request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.Close();
                listener.BeginGetContext(HandleAuthenticationRequest, listener);
                return;
            }

            // Process POST request
            if (request.HttpMethod == "POST")
            {
                try
                {
                    using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        string read = reader.ReadToEnd();
                        Debug.Log($"[VERA Authentication] Received response from VERA portal...");

                        UnityTokenResponse response = JsonUtility.FromJson<UnityTokenResponse>(read);

                        if (response == null || response.user == null || string.IsNullOrEmpty(response.token))
                        {
                            Debug.LogError("[VERA Authentication] Invalid authentication response received");
                            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                            byte[] errorBytes = Encoding.UTF8.GetBytes("Invalid authentication data");
                            context.Response.ContentLength64 = errorBytes.Length;
                            context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
                            context.Response.Close();
                            return;
                        }

                        string token = response.token;
                        string userId = response.user._id;
                        string userName = response.user.firstName + " " + response.user.lastName;

                        Debug.Log($"[VERA Authentication] Parsed data; name: {userName}. Returning success to VERA portal...");

                        // Save
                        EditorApplication.delayCall += () =>
                        {
                            try
                            {
                                SaveUserAuthentication(token, userId, userName);
                                Debug.Log("[VERA Connection] You are successfully authenticated and connected to the VERA portal.\n");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"[VERA Authentication] Failed to save authentication: {ex.Message}");
                            }
                        };
                    }

                    // Respond with success
                    byte[] responseBytes = Encoding.UTF8.GetBytes("Token received");
                    context.Response.ContentLength64 = responseBytes.Length;
                    context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                    context.Response.Close();

                    // Stop the server after receiving the token
                    StopUserAuthServer();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[VERA Authentication] Error processing authentication request: {ex.Message}");
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    byte[] errorBytes = Encoding.UTF8.GetBytes("Authentication processing failed");
                    context.Response.ContentLength64 = errorBytes.Length;
                    context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
                    context.Response.Close();
                }
            }

            // Listen for the next request if still running
            if (isRunning)
            {
                listener.BeginGetContext(HandleAuthenticationRequest, listener);
            }
        }


        #endregion


        #region SAVING AUTHENTICATION


        // Saves incoming user authentication info
        private static void SaveUserAuthentication(string token, string userId, string userName)
        {
            try
            {
                // Validate input parameters
                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userName))
                {
                    Debug.LogError($"[VERA Authentication] Invalid parameters - Token: {string.IsNullOrEmpty(token)}, UserID: {string.IsNullOrEmpty(userId)}, UserName: {string.IsNullOrEmpty(userName)}");
                    return;
                }

                // Get current auth info, to not overwrite other existing info
                VERAUserAuthInfo newAuthInfo = new VERAUserAuthInfo();

                // Set info
                newAuthInfo.authenticated = true;
                newAuthInfo.userAuthToken = token;
                newAuthInfo.userId = userId;
                newAuthInfo.userName = userName;

                // Push to file (updates PlayerPrefs as well)
                SetSavedUserAuthInfo(newAuthInfo);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VERA Authentication] Error in SaveUserAuthentication: {ex.Message}\nStack trace: {ex.StackTrace}");
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
                buildDeauthInfo.activeSite = currentBuildInfo.activeSite;
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

                // Force save PlayerPrefs
                PlayerPrefs.Save();

                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VERA Authentication] Failed to save user authentication: {ex.Message}\nStack trace: {ex.StackTrace}");
            }
        }


        // Gets saved build authentication info (file in StreamingAssets)
        private static VERABuildAuthInfo GetSavedBuildAuthInfo()
        {
            // File paths
            string directoryPath = GetBuildAuthPath();
            string filePath = Path.Combine(directoryPath, buildAuthFileName);

            // Ensure file exists
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                return JsonUtility.FromJson<VERABuildAuthInfo>(json);
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
            request.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("VERA_UserAuthToken"));

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
            request.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("VERA_UserAuthToken"));

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
                            Debug.LogError("[VERA Authentication] There was an issue connecting to the VERA portal to get build authentication. " +
                                "Please check your connection and try again.");
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
                                Debug.LogError($"[VERA Authenticator] Failed to parse authentication response: {jsonResponse}");
                                ClearBuildAuthentication();
                                onComplete?.Invoke(false);
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[VERA Authenticator] Failed to parse authentication response: {e.Message}");
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
            request.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("VERA_UserAuthToken"));

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
                            Debug.LogError("VERA: There was an issue connecting to the VERA portal, and you have been logged out. " +
                                "Please re-authenticate using the \"VERA -> Settings\" menu item.");
                        }
                        else if (request.result == UnityWebRequest.Result.ProtocolError)
                        {
                            Debug.LogError("VERA: You are not authenticated, and will not be able to run experiments. " +
                                "Use the \"VERA -> Settings\" menu bar item to authenticate.");
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
                            Debug.LogError("VERA: Received an unexpected response from the VERA portal when fetching experiments. " +
                                "Please try again later.");

                            onComplete?.Invoke(null);
                        }
                    }

                    // Dispose of the request
                    request.Dispose();
                }
            }
        }

        // Changes the currently active experiment
        public static void ChangeActiveExperiment(string activeExperimentId)
        {
            // Get current auth info, to not overwrite other existing info
            VERABuildAuthInfo currentAuthInfo = GetSavedBuildAuthInfo();

            // Set info
            currentAuthInfo.activeExperiment = activeExperimentId;

            // Push to file (updates PlayerPrefs as well)
            SetSavedBuildAuthInfo(currentAuthInfo);

            // Update session state for dev tools sim participant to avoid inter-experiment conflicts
            SessionState.SetBool("VERA_SimParticipant", false);

            // Update all column definition assets to this new experiment
            UpdateColumnDefs();

            // Generate condition code for the new experiment
            if (!string.IsNullOrEmpty(activeExperimentId))
            {
                GetUserExperiments((experiments) =>
                {
                    if (experiments != null)
                    {
                        var activeExperiment = experiments.Find(e => e._id == activeExperimentId);
                        if (activeExperiment != null)
                        {
                            ConditionGenerator.ClearAllConditionCsCode();
                            ConditionGenerator.GenerateAllConditionCsCode(activeExperiment);
                        }
                    }
                });
            }
            else
            {
                // Clear generated condition code if no active experiment
                ConditionGenerator.ClearAllConditionCsCode();
            }

            // Clear build authentication if no active experiment
            if (string.IsNullOrEmpty(activeExperimentId))
            {
                ClearBuildAuthentication();
                return;
            }

            // Update authentication token for the new experiment to allow data collection
            GetBuildAuthToken(activeExperimentId, (success) =>
            {
                if (!success)
                {
                    Debug.LogError("[VERA Authenticator] Failed to authenticate for experiment. Cannot change active experiment. " +
                        "Please check your internet connection, refresh experiments, and try again.");
                }
            });
        }

        // Changes the currently active site
        public static void ChangeActiveSite(string activeSiteId)
        {
            // Get current auth info, to not overwrite other existing info
            VERABuildAuthInfo currentAuthInfo = GetSavedBuildAuthInfo();

            // Set info
            currentAuthInfo.activeSite = activeSiteId;

            // Push to file (updates PlayerPrefs as well)
            SetSavedBuildAuthInfo(currentAuthInfo);
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
            request.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("VERA_UserAuthToken"));

            var operation = request.SendWebRequest();

            EditorApplication.update += EditorUpdate;

            void EditorUpdate()
            {
                if (!operation.isDone) return;
                EditorApplication.update -= EditorUpdate;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"VERA: failed to fetch IV group conditions for {ivName}: {request.error}");
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
                    Debug.LogWarning($"VERA: Failed parsing IV group conditions for {ivName}: {e.Message}");
                    onComplete?.Invoke(null);
                }
                finally
                {
                    request.Dispose();
                }
            }
        }


        #region FILE TYPE / COLUMN MANAGEMENT


        // Updates the column definition to the current experiment's column definition
        public static void UpdateColumnDefs()
        {
            // Clear old column definitions
            DeleteExistingColumnDefs();

            // If there is no active experiment, we cannot do anything with the columns
            if (PlayerPrefs.GetString("VERA_ActiveExperiment", null) == null || PlayerPrefs.GetString("VERA_ActiveExperiment", null) == "")
            {
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
            request.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("VERA_UserAuthToken"));

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
                        Debug.LogError("VERA - Unexpected response from server; could not get column definitions. " +
                                "Please try refreshing your experiments and trying again.");
                        return;
                    }
                    else
                    {
                        // Parse the response
                        string jsonResponse = request.downloadHandler.text;
                        FileTypesResponse fileTypesResponse = JsonUtility.FromJson<FileTypesResponse>(jsonResponse);

                        if (fileTypesResponse == null || !fileTypesResponse.success)
                        {
                            Debug.LogError("VERA - Unexpected response from server; could not get column definitions. " +
                                "Please try refreshing your experiments and trying again.");
                            return;
                        }

                        // Loop through each file type and get column definition, if the file type is a CSV
                        List<FtFileType> fileTypes = fileTypesResponse.fileTypes;
                        List<VERAColumnDefinition> columnDefs = new List<VERAColumnDefinition>();
                        List<string> definitionsToAdd = new List<string>();
                        for (int i = 0; i < fileTypes.Count; i++)
                        {
                            if (fileTypes[i].extension == "csv" && fileTypes[i].columnDefinition != null)
                            {
                                // This file type is a CSV file with an associated column definition.
                                // Create the column definition asset for this filetype, for use by VERALogger
                                columnDefs.Add(ScriptableObject.CreateInstance<VERAColumnDefinition>());
                                int idx = columnDefs.Count - 1;

                                // Ensure the directory exists in the Packages folder before creating the asset
                                string columnsPath = GetAbsoluteColumnsFilePath();
                                if (!Directory.Exists(columnsPath))
                                {
                                    Directory.CreateDirectory(columnsPath);
                                    AssetDatabase.Refresh();
                                }

                                // Convert absolute path to relative path for AssetDatabase.CreateAsset
                                // Sanitize the filename to remove any invalid characters
                                string sanitizedName = Regex.Replace(fileTypes[i].name, @"[<>:""/\\|?*]", "_");
                                string relativePath = GetRelativeColumnsFilePath() + "/VERA_" + sanitizedName + "_ColumnDefinition.asset";

                                try
                                {
                                    AssetDatabase.CreateAsset(columnDefs[idx], relativePath);
                                }
                                catch (System.Exception e)
                                {
                                    Debug.LogError($"[VERA Authentication] Failed to create asset at path '{relativePath}': {e.Message}");
                                    continue;
                                }

                                // Sort the columns based on order
                                List<FtColumn> sortedCols = fileTypes[i].columnDefinition.columns.OrderBy(col => col.order).ToList();

                                // Set columns
                                columnDefs[idx].columns.Clear();
                                foreach (FtColumn col in sortedCols)
                                {
                                    VERAColumnDefinition.Column newCol = new VERAColumnDefinition.Column();
                                    newCol.name = col.name;
                                    newCol.description = col.description;
                                    switch (col.dataType)
                                    {
                                        case "String":
                                            newCol.type = VERAColumnDefinition.DataType.String;
                                            break;
                                        case "Integer":
                                            newCol.type = VERAColumnDefinition.DataType.Number;
                                            break;
                                        case "Transform":
                                            newCol.type = VERAColumnDefinition.DataType.Transform;
                                            break;
                                        case "Date":
                                            newCol.type = VERAColumnDefinition.DataType.Date;
                                            break;
                                        case "JSON":
                                            newCol.type = VERAColumnDefinition.DataType.JSON;
                                            break;
                                    }

                                    // Enforce numeric type for known numeric telemetry columns regardless of server dataType
                                    string lowerName = (newCol.name ?? "").ToLower();
                                    if (lowerName.Contains("_pos") || lowerName.Contains("trigger") || lowerName.Contains("grip"))
                                    {
                                        newCol.type = VERAColumnDefinition.DataType.Number;
                                    }

                                    columnDefs[idx].columns.Add(newCol);
                                }

                                // Save column def
                                columnDefs[idx].fileType = new VERAColumnDefinition.FileType();
                                columnDefs[idx].fileType.fileTypeId = fileTypes[i]._id;
                                columnDefs[idx].fileType.name = fileTypes[i].name;
                                columnDefs[idx].fileType.description = fileTypes[i].description;

                                EditorUtility.SetDirty(columnDefs[idx]);
                                AssetDatabase.SaveAssets();

                                // Add define symbol for this column definition
                                definitionsToAdd.Add("VERAFile_" + fileTypes[i].name);
                            }
                        }

                        AssetDatabase.Refresh();

                        // Generate code for all file types
                        FileTypeGenerator.GenerateAllFileTypesCsCode();
                        ReplaceDefines(definitionsToAdd);
                    }

                    request.Dispose();
                }
            }
        }

        // Deletes all existing column definitions in the columns folder
        public static void DeleteExistingColumnDefs()
        {
            string columnsFilePath = GetAbsoluteColumnsFilePath();
            if (Directory.Exists(columnsFilePath))
            {
                string[] files = Directory.GetFiles(columnsFilePath);

                foreach (string file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (IOException e)
                    {
                        Debug.LogError($"IO Exception deleting file: {file}\n{e.Message}");
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
                .GetScriptingDefineSymbols(namedBuildTarget).Split(';').ToList();
#else
        // Use the old API for older Unity versions
        BuildTargetGroup activeBuildTargetGroup = BuildPipeline.GetBuildTargetGroup(activeBuildTarget);
        List<string> currentSymbols = PlayerSettings
            .GetScriptingDefineSymbolsForGroup(activeBuildTargetGroup).Split(';').ToList();
#endif

            return currentSymbols;
        }

        // Saves define symbols as per given list
        private static void SaveDefineSymbols(List<string> symbols)
        {
            BuildTarget activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;

#if UNITY_2023_1_OR_NEWER
            // Use the new API for Unity 2023.1 and newer
            NamedBuildTarget namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(activeBuildTarget));
            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, string.Join(";", symbols));
#else
        // Use the old API for older Unity versions
        BuildTargetGroup activeBuildTargetGroup = BuildPipeline.GetBuildTargetGroup(activeBuildTarget);
        PlayerSettings.SetScriptingDefineSymbolsForGroup(activeBuildTargetGroup, string.Join(";", symbols));
#endif
        }

        // Replaces all define symbols with the given list;
        // does not replace any define symbols that do not need to be replaced.
        private static void ReplaceDefines(List<string> symbols)
        {
            HashSet<string> newSymbols = new HashSet<string>(symbols);
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
    }
}