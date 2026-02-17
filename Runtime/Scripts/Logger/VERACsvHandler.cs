using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace VERA
{
    // Serializable wrapper classes for WebGL-compatible JSON serialization
    [System.Serializable]
    internal class SerializableVector3
    {
        public float x, y, z;
        public SerializableVector3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
    }

    [System.Serializable]
    internal class SerializableQuaternion
    {
        public float x, y, z, w;
        public SerializableQuaternion(Quaternion q) { x = q.x; y = q.y; z = q.z; w = q.w; }
    }

    [System.Serializable]
    internal class SerializableTransform
    {
        public SerializableVector3 position;
        public SerializableQuaternion rotation;
        public SerializableVector3 localScale;

        public SerializableTransform(Transform t)
        {
            position = new SerializableVector3(t.position);
            rotation = new SerializableQuaternion(t.rotation);
            localScale = new SerializableVector3(t.localScale);
        }
    }

    internal class VERACsvHandler : MonoBehaviour
    {

        // VERACsvHandler handles the entry logging for a single defined CSV file type

        public string fullCsvFilePath { get; private set; } // Where the full CSV file (all rows) is recorded and stored
        public string partialCsvFilePath { get; private set; } // Where the partial CSV file (unsynced rows) is recorded and stored

        public VERAColumnDefinition columnDefinition { get; private set; } // The column definition of this CSV
        public UnityWebRequest activeWebRequest { get; private set; }

        // Unwritten entries and flushing
        private List<string> unwrittenEntries = new List<string>(); // A cache of unwritten log entries
        private int unwrittenEntryLimit = 100; // If unwritten entries exceeds this limit, a flush will occur
        private float timeSinceLastFlush = 0f;
        private float flushInterval = 5f; // How frequently a flush of unwritten entries will occur
        public bool finalEntryUploaded { get; private set; } = false;


        #region MONOBEHAVIOUR


        // Update, check if we need to flush unwritten entries
        private void Update()
        {
            if (!VERALogger.Instance.collecting)
                return;

            timeSinceLastFlush += Time.deltaTime;
            CheckFlushUnwritten();
        }


        // OnDestroy, flush any unwritten entries
        private void OnDestroy()
        {
            FlushUnwrittenEntries(true);
        }


        // OnApplicationQuit, flush any unwritten entries
        private void OnApplicationQuit()
        {
            FlushUnwrittenEntries(true);
        }


        #endregion


        #region INIT


        /// <summary>
        /// Builds the hierarchical directory path based on experiment, site, build version, and participant.
        /// Creates the directory if it doesn't already exist.
        /// </summary>
        private string BuildHierarchicalDirectoryPath()
        {
            VERABuildAuthInfo authInfo = VERALogger.Instance.buildAuthInfo;
            string participantShortId = VERALogger.Instance.activeParticipant.participantShortId.ToString();

            // Start with the base directory from baseFilePath
            string baseDirectory = Path.GetDirectoryName(VERALogger.Instance.baseFilePath);

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


        public void Initialize(VERAColumnDefinition columnDef)
        {
            DataRecordingType dataRecordingType = VERALogger.Instance.GetDataRecordingType();
            if (dataRecordingType == DataRecordingType.DoNotRecord)
            {
                VERADebugger.Log("Data recording is disabled; CSV handler for file type \"" + columnDef.fileType.name + "\" will not be initialized.", "VERACsvHandler", DebugPreference.Informative);
                return;
            }

            string participantUUID = VERALogger.Instance.activeParticipant.participantUUID;
            columnDefinition = columnDef;

            // Build the hierarchical directory path and get the directory for CSV storage
            string csvDirectory = BuildHierarchicalDirectoryPath();

            // Construct file paths within the hierarchical directory
            string baseFileName = columnDefinition.fileType.name.Replace(" ", "-");
            fullCsvFilePath = Path.Combine(csvDirectory, baseFileName + ".csv");
            partialCsvFilePath = Path.Combine(csvDirectory, baseFileName + "-partial.csv");

            // Set up the column names for the header row
            List<string> columnNames = new List<string>();
            foreach (VERAColumnDefinition.Column column in columnDefinition.columns)
            {
                columnNames.Add(column.name);
            }

            // Write the initial files using StreamWriter
            using (StreamWriter writer = new StreamWriter(fullCsvFilePath))
            {
                writer.WriteLine(string.Join(",", columnNames));
                writer.Flush();
            }

            using (StreamWriter writer = new StreamWriter(partialCsvFilePath))
            {
                writer.WriteLine(string.Join(",", columnNames));
                writer.Flush();
            }

            VERADebugger.Log("CSV File (and partial sync file) for file type \"" + columnDefinition.fileType.name + ".csv\" created and saved at " + fullCsvFilePath, "VERACsvHandler", DebugPreference.Verbose);
        }


        #endregion


        #region ENTRY LOGGING


        // Logs an entry to the file. Doesn't write yet, only writes on flush.
        public void CreateEntry(int eventId, params object[] values)
        {
            if (!VERALogger.Instance.collecting || VERALogger.Instance.sessionFinalized)
            {
                return;
            }

            // check baseline data file (omits eventId column)
            bool isBaselineTelemetry = columnDefinition.fileType.fileTypeId == "baseline-data";
            int autoColumnCount = isBaselineTelemetry ? 3 : 4; // 3 for baseline telemetry (no eventId), 4 otherwise

            if (values.Length != columnDefinition.columns.Count - autoColumnCount)
            {
                VERADebugger.LogError("You are attempting to create a log entry with " + (values.Length + autoColumnCount).ToString() +
                    " columns. The file type \"" + columnDefinition.fileType.name + "\" expects " + columnDefinition.columns.Count +
                    " columns. Cannot log entry as desired.", "VERACsvHandler");
                return;
            }

            List<string> entry = new List<string>();
            // Add pID, conditions, timestamp, and eventId (except for baseline telemetry)
            entry.Add(Convert.ToString(VERALogger.Instance.activeParticipant.participantShortId));
            entry.Add(FormatValueForCsv(VERALogger.Instance.GetExperimentConditions()));
            entry.Add(Convert.ToString(Time.realtimeSinceStartup));

            // Only add eventId for non-baseline telemetry file types
            if (autoColumnCount == 4)
            {
                entry.Add(Convert.ToString(eventId));
            }

            for (int i = 0; i < values.Length; i++)
            {
                object value = values[i];
                VERAColumnDefinition.Column column = columnDefinition.columns[i + autoColumnCount];

                string formattedValue = "";
                switch (column.type)
                {
                    case VERAColumnDefinition.DataType.Number:
                        // If a numeric value is null, default to -1 to indicate NA/unknown for detection flags
                        // (headset_detected/left_detected/right_detected use -1 for unknown by convention).
                        if (value == null)
                        {
                            formattedValue = "-1";
                        }
                        else
                        {
                            formattedValue = Convert.ToString(value);
                        }
                        break;
                    case VERAColumnDefinition.DataType.String:
                        formattedValue = FormatValueForCsv(value.ToString());
                        break;
                    case VERAColumnDefinition.DataType.JSON:
                        // Use Unity's JsonUtility for WebGL compatibility (no reflection emit)
                        string json = SerializeToJson(value);
                        formattedValue = FormatValueForCsv(json);
                        break;
                    case VERAColumnDefinition.DataType.Transform:
                        var transform = value as Transform;
                        if (transform != null)
                        {
                            // Use serializable wrapper class for WebGL compatibility
                            var serializableTransform = new SerializableTransform(transform);
                            formattedValue = FormatValueForCsv(JsonUtility.ToJson(serializableTransform));
                        }
                        else
                        {
                            var newObject = value;
                            if (newObject != null)
                            {
                                formattedValue = FormatValueForCsv(SerializeToJson(newObject));
                            }
                        }
                        break;
                    default:
                        formattedValue = FormatValueForCsv(value.ToString());
                        break;
                }

                entry.Add(formattedValue);
            }

            unwrittenEntries.Add(string.Join(",", entry));
            CheckFlushUnwritten();
        }

        // Formats given value for CSV, and returns
        private string FormatValueForCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            value = value.Replace("\"", "\"\"");

            // If it contains a quote, wrap in quotes
            if (value.Contains(",") || value.Contains("\n") || value.Contains("\""))
            {
                return $"\"{value}\"";
            }
            else
            {
                return value;
            }
        }

        // WebGL-compatible JSON serialization helper
        // Uses Unity's JsonUtility which doesn't use reflection emit
        private string SerializeToJson(object value)
        {
            if (value == null)
                return "null";

            // Try JsonUtility first (works for classes with [Serializable] attribute)
            try
            {
                return JsonUtility.ToJson(value);
            }
            catch
            {
                // Fallback: manual serialization for simple types
                if (value is string str)
                    return $"\"{str}\"";

                if (value is bool || value is int || value is float || value is double || value is long)
                    return value.ToString();

                // For complex objects, use ToString as last resort
                return value.ToString();
            }
        }


        #endregion


        #region FLUSH


        // Checks if we need to flush unwritten entries; flushes if we do need to.
        private void CheckFlushUnwritten()
        {
            // Only automatically flush if collecting and not finalized
            if (!VERALogger.Instance.collecting || VERALogger.Instance.sessionFinalized)
                return;

            // If the number of unwritten entries is too large or enough time has passed since last flush, flush
            if (unwrittenEntries.Count >= unwrittenEntryLimit || timeSinceLastFlush >= flushInterval)
            {
                // Do not flush while an active web request is happening
                if (activeWebRequest != null)
                {
                    return;
                }
                FlushUnwrittenEntries();
            }
        }

        // Flushes all unwritten entries and writes them to the file
        private void FlushUnwrittenEntries(bool ignorePartial = false)
        {
            // Do not flush while an active web request is happening
            if (activeWebRequest != null)
            {
                return;
            }

            timeSinceLastFlush = 0f;

            if (unwrittenEntries.Count == 0)
                return;

            // Append to both full and partial CSV files
            using (StreamWriter writer = new StreamWriter(fullCsvFilePath, true))
            {
                foreach (string entry in unwrittenEntries)
                {
                    writer.WriteLine(entry);
                }
            }

            if (!ignorePartial)
            {
                using (StreamWriter writer = new StreamWriter(partialCsvFilePath, true))
                {
                    foreach (string entry in unwrittenEntries)
                    {
                        writer.WriteLine(entry);
                    }
                }
            }

            unwrittenEntries.Clear();
        }


        #endregion


        #region SUBMISSION

        public IEnumerator SubmitFileWithRetry(bool finalUpload = false, bool usePartial = false)
        {
            DataRecordingType dataRecordingType = VERALogger.Instance.GetDataRecordingType();
            if (dataRecordingType == DataRecordingType.DoNotRecord || dataRecordingType == DataRecordingType.OnlyRecordLocally)
            {
                yield break;
            }

            // If there is an active web request, skip simple syncs or queue final sync to wait until active request completes
            if (activeWebRequest != null)
            {
                if (!finalUpload)
                {
                    VERADebugger.Log("Attempted non-final upload for file \"" + columnDefinition.fileType.name +
                        "\", but another upload is already in progress; ignoring this request, as it will be handled when the active one completes.", "VERACsvHandler", DebugPreference.Verbose);
                    yield break;
                }
                else
                {
                    VERADebugger.Log("Attempted final upload for file \"" + columnDefinition.fileType.name +
                        "\", but another upload is already in progress; waiting for it to complete before proceeding.", "VERACsvHandler", DebugPreference.Verbose);
                    while (activeWebRequest != null)
                    {
                        yield return null;
                    }
                    VERADebugger.Log("Previous upload completed; proceeding with final upload for file \"" + columnDefinition.fileType.name + "\".", "VERACsvHandler", DebugPreference.Verbose);
                }
            }

            int maxAttempts = 5;
            float delay = 2f;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // Check if we should even be uploading
                if (VERALogger.Instance.sessionFinalized)
                {
                    if (!finalUpload)
                    {
                        VERADebugger.LogWarning($"Session has been finalized, but an attempt to upload a partial file was made; skipping partial upload for {columnDefinition.fileType.name}.", "VERACsvHandler");
                        yield break;
                    }
                    else if (finalEntryUploaded)
                    {
                        VERADebugger.LogWarning($"Session finalized and final entry already uploaded, but an attempt to upload a final file was made; skipping additional final upload for {columnDefinition.fileType.name}.", "VERACsvHandler");
                        yield break;
                    }
                }

                // Flush all unwritten entries before upload
                FlushUnwrittenEntries();

                // If this is a partial sync and we are already up-to-date, skip
                if (usePartial && !PartialFileHasUnsyncedData())
                {
                    VERADebugger.Log($"No new unsynced data for partial upload of {columnDefinition.fileType.name}; skipping upload.", "VERACsvHandler", DebugPreference.Verbose);
                    if (finalUpload)
                        OnFinalUploadComplete();
                    yield break;
                }

                // Try upload
                yield return StartCoroutine(SubmitFileCoroutine(usePartial));

                // Check result of last UnityWebRequest
                // (Note: If activeWebRequest is null, submission is likely okay - early exit)
                if (activeWebRequest == null || activeWebRequest.result == UnityWebRequest.Result.Success)
                {
                    // On success, log detailed outcome
                    string serverResponse = "";
                    try { serverResponse = activeWebRequest.downloadHandler != null ? activeWebRequest.downloadHandler.text : ""; } catch { serverResponse = "(no response body)"; }
                    VERADebugger.Log("Successful upload of \"" + columnDefinition.fileType.name + "\". Server response: " + serverResponse, "VERACsvHandler", DebugPreference.Verbose);

                    // If this was a partial upload and succeeded, clear the partial rows so we don't re-upload them
                    if (usePartial)
                    {
                        try
                        {
                            ClearPartialFileRows();
                        }
                        catch (Exception e)
                        {
                            VERADebugger.LogWarning("Failed to clear partial file rows after successful partial upload: " + e.Message, "VERACsvHandler");
                        }
                    }

                    activeWebRequest = null; // Clear the active web request
                    if (finalUpload)
                        OnFinalUploadComplete();
                    yield break;
                }
                else
                {
                    // failure → log and back off
                    string err = activeWebRequest != null ? activeWebRequest.error : "unknown error";
                    VERADebugger.LogWarning($"Attempt {attempt} failed for {columnDefinition.fileType.name}: {err}", "VERACsvHandler");
                    if (attempt < maxAttempts)
                    {
                        VERADebugger.Log($"Retrying in {delay}s...", "VERACsvHandler", DebugPreference.Informative);
                        activeWebRequest = null;
                        yield return new WaitForSeconds(delay);
                        delay *= 2f; // exponential backoff
                    }
                    else
                    {
                        // If all attempts fail, log such
                        VERADebugger.LogError($"All {maxAttempts} attempts failed for {columnDefinition.fileType.name}.", "VERACsvHandler");
                        activeWebRequest = null;
                        if (finalUpload)
                            OnFinalUploadComplete();
                        yield break;
                    }
                }
            }
        }

        // Submits the file to the server
        // Sets the activeWebRequest property while the request is active
        // Does NOT handle overlapping requests - these should be handled elsewhere and ensure this coroutine only runs one at a time
        private IEnumerator SubmitFileCoroutine(bool partial)
        {
            DataRecordingType dataRecordingType = VERALogger.Instance.GetDataRecordingType();
            if (dataRecordingType == DataRecordingType.DoNotRecord || dataRecordingType == DataRecordingType.OnlyRecordLocally)
            {
                yield break;
            }

            if (partial)
            {
                VERADebugger.Log("Submitting to the server all new data entries for file associated with file type \"" +
                    columnDefinition.fileType.name + "\" (" + fullCsvFilePath + ")", "VERACsvHandler", DebugPreference.Verbose);
            }
            else
            {
                VERADebugger.Log("Submitting full CSV file associated with file type \"" +
                    columnDefinition.fileType.name + "\" (" + fullCsvFilePath + ")", "VERACsvHandler", DebugPreference.Verbose);
            }

            // Paths, keys, and IDs
            string basename = Path.GetFileName(fullCsvFilePath);
            string apiKey = VERALogger.Instance.apiKey;
            string experimentUUID = VERALogger.Instance.experimentUUID;
            string siteUUID = VERALogger.Instance.siteUUID;
            string participantUUID = VERALogger.Instance.activeParticipant.participantUUID;
            string fileTypeId = columnDefinition.fileType.fileTypeId;

            string host = VERAHost.hostUrl;
            string url = $"{host}/api/participants/{participantUUID}/filetypes/{fileTypeId}/files";

            // Add mode parameter for partial uploads
            if (partial)
            {
                url += "?mode=partial";
            }

            byte[] fileData = null;

            // If this is a partial sync, use the partial file path instead; if there is no data to sync, skip
            string filePath = fullCsvFilePath;
            if (partial)
            {
                filePath = partialCsvFilePath;
                if (!PartialFileHasUnsyncedData())
                {
                    VERADebugger.Log("Attempted to upload partial CSV for file type \"" + columnDefinition.fileType.name +
                        "\", but there is no new unsynced data to submit. Skipping upload.", "VERACsvHandler", DebugPreference.Verbose);
                    yield break;
                }
            }

            // Read the data associated with the file
            yield return VERALogger.Instance.ReadBinaryDataFile(filePath, (result) => fileData = result);
            if (fileData == null)
            {
                VERADebugger.Log("Attempted to upload CSV for file type \"" + columnDefinition.fileType.name +
                    "\", but there is no file data to submit.", "VERACsvHandler", DebugPreference.Verbose);
                yield break;
            }

            // Set up the request
            WWWForm form = new WWWForm();
            form.AddField("participant_UUID", participantUUID);
            form.AddBinaryData("fileUpload", fileData, experimentUUID + "-" + siteUUID + "-" + participantUUID + "-" + fileTypeId + ".csv", "text/csv");

            // Send the request
            activeWebRequest = UnityWebRequest.Post(url, form);
            activeWebRequest.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return activeWebRequest.SendWebRequest();

            // Check success and log server response
            if (activeWebRequest == null)
            {
                yield break;
            }

            if (activeWebRequest.result == UnityWebRequest.Result.Success)
            {
                VERADebugger.Log("Successful upload of \"" + columnDefinition.fileType.name + "\".", "VERACsvHandler", DebugPreference.Verbose);
            }
            else
            {
                VERADebugger.LogError($"Failed to upload \"{columnDefinition.fileType.name}\". result={activeWebRequest.result}, error={activeWebRequest.error}", "VERACsvHandler");
            }
        }


        private void OnFinalUploadComplete()
        {
            finalEntryUploaded = true;
            DeletePartialFile();
        }


        #endregion


        #region PERIODIC SYNC


        // Clears all rows (except header) in the partial file
        private void ClearPartialFileRows()
        {
            if (!File.Exists(partialCsvFilePath)) return;

            // Read all lines
            var lines = File.ReadAllLines(partialCsvFilePath).ToList();

            // Keep only the header
            if (lines.Count > 0)
            {
                lines = lines.Take(1).ToList();
            }

            // Write the cleared content back
            File.WriteAllLines(partialCsvFilePath, lines);
        }


        // Deletes the partial file entirely (cleanup when session is completed)
        public void DeletePartialFile()
        {
            if (File.Exists(partialCsvFilePath))
            {
                File.Delete(partialCsvFilePath);
            }
        }


        // Gets whether the partial file has any unsynced data (rows beyond the header)
        private bool PartialFileHasUnsyncedData()
        {
            if (!File.Exists(partialCsvFilePath)) return false;

            var lines = File.ReadAllLines(partialCsvFilePath);
            return lines.Length > 1; // More than just the header
        }


        #endregion


    }
}