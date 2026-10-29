// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace VERA
{
    /// <summary>
    /// HTTP client for experiment-wide key-value memory.
    /// Callers should use <see cref="VERASessionManager"/> rather than this type.
    /// </summary>
    internal static class VERAExperimentMemory
    {
        private const string LogCategory = "VERA Experiment Memory";
        private const int MaxMemoryKeyLength = 128;
        private const int MaxMemoryValueLength = 2 * 1024 * 1024;

        public static IEnumerator SetKey(string keyName, string keyValue, Action onSuccess = null, Action<string> onFailure = null)
        {
            if (!TryPrepareRequest(out string error))
            {
                onFailure?.Invoke(error);
                yield break;
            }

            if (!TryValidateMemoryKey(keyName, out string key, out error))
            {
                onFailure?.Invoke(error);
                yield break;
            }

            if (!TryValidateMemoryValue(keyValue, out error))
            {
                onFailure?.Invoke(error);
                yield break;
            }

            string url = BuildKeyUrl(key);
            string jsonPayload = JsonUtility.ToJson(new VERAMemoryValuePayload { value = keyValue });
            VERADebugger.Log("Setting experiment memory key \"" + key + "\" at " + url, LogCategory, DebugPreference.Verbose);

            UnityWebRequest request = CreateJsonRequest(url, UnityWebRequest.kHttpVerbPUT, jsonPayload);
            VERAHost.ApplyBearerAuth(request, VERALogger.Instance.apiKey);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string failure = FormatRequestError("set experiment memory key \"" + key + "\"", request);
                VERADebugger.LogWarning(failure, LogCategory);
                onFailure?.Invoke(failure);
                request.Dispose();
                yield break;
            }

            string responseText = request.downloadHandler != null ? request.downloadHandler.text : null;
            request.Dispose();

            VERAMemoryEntryResponse response = TryParseEntryResponse(responseText, out error);
            if (response == null)
            {
                onFailure?.Invoke(error);
                yield break;
            }

            if (!response.success || response.entry == null)
            {
                onFailure?.Invoke(GetErrorMessage(response.error, response.message, "Failed to set experiment memory key."));
                yield break;
            }

            VERADebugger.Log("Successfully set experiment memory key \"" + key + "\".", LogCategory, DebugPreference.Informative);
            onSuccess?.Invoke();
        }

        public static IEnumerator GetKey(string keyName, Action<string> onSuccess, Action<string> onFailure = null)
        {
            if (!TryPrepareRequest(out string error))
            {
                onFailure?.Invoke(error);
                yield break;
            }

            if (!TryValidateMemoryKey(keyName, out string key, out error))
            {
                onFailure?.Invoke(error);
                yield break;
            }

            string url = BuildKeyUrl(key);
            VERADebugger.Log("Getting experiment memory key \"" + key + "\" from " + url, LogCategory, DebugPreference.Verbose);

            UnityWebRequest request = UnityWebRequest.Get(url);
            VERAHost.ApplyBearerAuth(request, VERALogger.Instance.apiKey);
            yield return request.SendWebRequest();

            long responseCode = request.responseCode;
            string responseText = request.downloadHandler != null ? request.downloadHandler.text : null;

            if (responseCode == 404)
            {
                request.Dispose();
                onSuccess?.Invoke(null);
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                string failure = FormatRequestError("get experiment memory key \"" + key + "\"", request);
                VERADebugger.LogWarning(failure, LogCategory);
                onFailure?.Invoke(failure);
                request.Dispose();
                yield break;
            }

            request.Dispose();

            VERAMemoryEntryResponse response = TryParseEntryResponse(responseText, out error);
            if (response == null)
            {
                onFailure?.Invoke(error);
                yield break;
            }

            if (!response.success || response.entry == null)
            {
                if (IsKeyNotFoundError(response.error, response.message))
                {
                    onSuccess?.Invoke(null);
                    yield break;
                }

                onFailure?.Invoke(GetErrorMessage(response.error, response.message, "Failed to get experiment memory key."));
                yield break;
            }

            onSuccess?.Invoke(response.entry.value);
        }

        public static IEnumerator AddToKey(string keyName, int delta, Action<string> onSuccess = null, Action<string> onFailure = null)
        {
            if (!TryPrepareRequest(out string error))
            {
                onFailure?.Invoke(error);
                yield break;
            }

            if (!TryValidateMemoryKey(keyName, out string key, out error))
            {
                onFailure?.Invoke(error);
                yield break;
            }

            string url = BuildKeyUrl(key) + "/increment";
            string jsonPayload = JsonUtility.ToJson(new VERAMemoryDeltaPayload { delta = delta });
            VERADebugger.Log("Adding " + delta + " to experiment memory key \"" + key + "\" at " + url, LogCategory, DebugPreference.Verbose);

            UnityWebRequest request = CreateJsonRequest(url, UnityWebRequest.kHttpVerbPOST, jsonPayload);
            VERAHost.ApplyBearerAuth(request, VERALogger.Instance.apiKey);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string failure = FormatRequestError("add to experiment memory key \"" + key + "\"", request);
                VERADebugger.LogWarning(failure, LogCategory);
                onFailure?.Invoke(failure);
                request.Dispose();
                yield break;
            }

            string responseText = request.downloadHandler != null ? request.downloadHandler.text : null;
            request.Dispose();

            VERAMemoryEntryResponse response = TryParseEntryResponse(responseText, out error);
            if (response == null)
            {
                onFailure?.Invoke(error);
                yield break;
            }

            if (!response.success || response.entry == null)
            {
                onFailure?.Invoke(GetErrorMessage(response.error, response.message, "Failed to add to experiment memory key."));
                yield break;
            }

            string newValue = response.entry.value ?? "";
            VERADebugger.Log("Experiment memory key \"" + key + "\" is now \"" + newValue + "\".", LogCategory, DebugPreference.Informative);
            onSuccess?.Invoke(newValue);
        }

        public static IEnumerator ClearKey(string keyName, Action onSuccess = null, Action<string> onFailure = null)
        {
            if (!TryPrepareRequest(out string error))
            {
                onFailure?.Invoke(error);
                yield break;
            }

            if (!TryValidateMemoryKey(keyName, out string key, out error))
            {
                onFailure?.Invoke(error);
                yield break;
            }

            string url = BuildKeyUrl(key);
            VERADebugger.Log("Clearing experiment memory key \"" + key + "\" at " + url, LogCategory, DebugPreference.Verbose);

            UnityWebRequest request = UnityWebRequest.Delete(url);
            request.downloadHandler = new DownloadHandlerBuffer();
            VERAHost.ApplyBearerAuth(request, VERALogger.Instance.apiKey);
            yield return request.SendWebRequest();

            long responseCode = request.responseCode;
            string responseText = request.downloadHandler != null ? request.downloadHandler.text : null;

            if (responseCode == 404)
            {
                request.Dispose();
                onSuccess?.Invoke();
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                string failure = FormatRequestError("clear experiment memory key \"" + key + "\"", request);
                VERADebugger.LogWarning(failure, LogCategory);
                onFailure?.Invoke(failure);
                request.Dispose();
                yield break;
            }

            request.Dispose();

            if (!string.IsNullOrEmpty(responseText))
            {
                VERAMemoryDeleteResponse response = null;
                try
                {
                    response = JsonUtility.FromJson<VERAMemoryDeleteResponse>(responseText);
                }
                catch (Exception ex)
                {
                    onFailure?.Invoke("Failed to parse experiment memory delete response: " + ex.Message);
                    yield break;
                }

                if (response != null && !response.success)
                {
                    if (IsKeyNotFoundError(response.error, response.message))
                    {
                        onSuccess?.Invoke();
                        yield break;
                    }

                    onFailure?.Invoke(GetErrorMessage(response.error, response.message, "Failed to clear experiment memory key."));
                    yield break;
                }
            }

            VERADebugger.Log("Successfully cleared experiment memory key \"" + key + "\".", LogCategory, DebugPreference.Informative);
            onSuccess?.Invoke();
        }

        private static bool TryPrepareRequest(out string error)
        {
            error = null;

            if (VERALogger.Instance == null)
            {
                error = "VERA is not initialized.";
                return false;
            }

            DataRecordingType dataRecordingType = VERALogger.Instance.GetDataRecordingType();
            if (dataRecordingType != DataRecordingType.RecordLocallyAndLive)
            {
                error = "Experiment memory is only available when recording to the server.";
                return false;
            }

            if (string.IsNullOrEmpty(VERALogger.Instance.experimentUUID) || VERALogger.Instance.experimentUUID == "N/A")
            {
                error = "No experiment ID available.";
                return false;
            }

            if (string.IsNullOrEmpty(VERALogger.Instance.apiKey))
            {
                error = "No API authentication token available.";
                return false;
            }

            return true;
        }

        private static bool TryValidateMemoryKey(string keyName, out string trimmedKey, out string error)
        {
            trimmedKey = null;
            error = null;

            if (keyName == null)
            {
                error = "Memory key cannot be null.";
                return false;
            }

            trimmedKey = keyName.Trim();
            if (trimmedKey.Length == 0)
            {
                error = "Memory key cannot be empty.";
                return false;
            }

            if (trimmedKey.Length > MaxMemoryKeyLength)
            {
                error = "Memory key cannot exceed " + MaxMemoryKeyLength + " characters.";
                return false;
            }

            for (int i = 0; i < trimmedKey.Length; i++)
            {
                char c = trimmedKey[i];
                if (c == '/' || c == '\\' || char.IsControl(c))
                {
                    error = "Memory key cannot contain '/', '\\', or control characters.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryValidateMemoryValue(string keyValue, out string error)
        {
            error = null;

            if (keyValue == null)
            {
                error = "Memory value cannot be null.";
                return false;
            }

            if (keyValue.Length > MaxMemoryValueLength)
            {
                error = "Memory value cannot exceed " + MaxMemoryValueLength + " characters (2 MB).";
                return false;
            }

            return true;
        }

        private static string BuildKeyUrl(string key)
        {
            string experimentId = Uri.EscapeDataString(VERALogger.Instance.experimentUUID);
            string encodedKey = Uri.EscapeDataString(key);
            return VERAHost.hostUrl + "/api/experiments/" + experimentId + "/kv/" + encodedKey;
        }

        private static UnityWebRequest CreateJsonRequest(string url, string method, string jsonPayload)
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload ?? "{}");
            UnityWebRequest request = new UnityWebRequest(url, method);
            UploadHandlerRaw uploadHandler = new UploadHandlerRaw(bodyRaw);
            uploadHandler.contentType = "application/json";
            request.uploadHandler = uploadHandler;
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        private static VERAMemoryEntryResponse TryParseEntryResponse(string responseText, out string error)
        {
            error = null;

            if (string.IsNullOrEmpty(responseText))
            {
                error = "Experiment memory response was empty.";
                return null;
            }

            try
            {
                VERAMemoryEntryResponse response = JsonUtility.FromJson<VERAMemoryEntryResponse>(responseText);
                if (response == null)
                {
                    error = "Experiment memory response was invalid.";
                    return null;
                }

                return response;
            }
            catch (Exception ex)
            {
                error = "Failed to parse experiment memory response: " + ex.Message;
                return null;
            }
        }

        private static string FormatRequestError(string action, UnityWebRequest request)
        {
            string responseText = request.downloadHandler != null ? request.downloadHandler.text : null;
            string serverError = TryExtractErrorMessage(responseText);
            string details = "HTTP " + request.responseCode + ", " + request.error;
            if (!string.IsNullOrEmpty(serverError))
                details += ": " + serverError;
            else if (!string.IsNullOrEmpty(responseText))
                details += ", response=" + responseText;

            return "Failed to " + action + " (" + details + ")";
        }

        private static string TryExtractErrorMessage(string responseText)
        {
            if (string.IsNullOrEmpty(responseText))
                return null;

            try
            {
                VERAMemoryEntryResponse response = JsonUtility.FromJson<VERAMemoryEntryResponse>(responseText);
                return GetErrorMessage(
                    response != null ? response.error : null,
                    response != null ? response.message : null,
                    null);
            }
            catch
            {
                return null;
            }
        }

        private static string GetErrorMessage(string error, string message, string fallback)
        {
            if (!string.IsNullOrEmpty(error))
                return error;
            if (!string.IsNullOrEmpty(message))
                return message;
            return fallback;
        }

        private static bool IsKeyNotFoundError(string error, string message)
        {
            string combined = ((error ?? "") + " " + (message ?? "")).ToLowerInvariant();
            return combined.Contains("not found");
        }
    }
}
