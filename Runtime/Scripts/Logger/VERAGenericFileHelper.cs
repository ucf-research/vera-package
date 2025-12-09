using MimeTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace VERA
{
    internal class VERAGenericFileHelper : MonoBehaviour
    {

        // VERAGenericFileHelper aids in uploading and managing generic and image files (unassociated with file types)


        #region GENERIC


        // Submits a generic file for the participant, unassociated with any file type
        public IEnumerator SubmitGenericFileCoroutine(string filePath, string timestamp = null, byte[] fileData = null, bool moveFileToUploadDirectory = true)
        {
            string fileExtension = Path.GetExtension(filePath);
            string fileName = Path.GetFileName(filePath);

            string experimentUUID = VERALogger.Instance.experimentUUID;
            string siteUUID = VERALogger.Instance.siteUUID;
            string participantUUID = VERALogger.Instance.activeParticipant.participantUUID;
            string apiKey = VERALogger.Instance.apiKey;

            // In the editor, ignore any .meta files
#if UNITY_EDITOR
            if (fileExtension == ".meta")
                yield break;
#endif

            // If the file has no extension, add a generic ".file" extension
            if (string.IsNullOrEmpty(fileExtension))
                fileExtension = ".file";

            // Set up the data
            string host = VERAHost.hostUrl;
            string url = $"{host}/api/participants/files/{experimentUUID}/{siteUUID}/{participantUUID}";
            Debug.Log("[VERA Logger] Submitting generic file \"" + filePath + "\" to the active participant...");
            if (fileData == null)
            {
                yield return VERALogger.Instance.ReadBinaryDataFile(filePath, (result) => fileData = result);
            }

            if (fileData == null)
            {
                Debug.Log("[VERA Logger] No file data was found to submit.");
                yield break;
            }

            // Set up the request
            WWWForm form = new WWWForm();
            form.AddField("experiment_UUID", experimentUUID);
            form.AddField("participant_UUID", participantUUID);
            form.AddField("ts", timestamp ?? DateTime.UtcNow.ToString("o"));
            form.AddBinaryData("file", fileData, fileName, MimeTypeMap.GetMimeType(fileExtension));
            form.AddField("extension", fileExtension);

            // Send the request
            UnityWebRequest request = UnityWebRequest.Post(url, form);
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return request.SendWebRequest();

            // Check success
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[VERA Logger] Successfully uploaded the file.");
                OnGenericFullyUploaded(filePath);
                if (moveFileToUploadDirectory)
                {
                    MoveFileToGenericDataPath(filePath);
                }
            }
            else
            {
                Debug.LogError("[VERA Logger] Failed to upload the file: " + request.error);
            }
        }


        // Called when a generic file is fully uploaded
        // Append the generic file's name to the uploaded records so we know to not upload it again
        public void OnGenericFullyUploaded(string filePath)
        {
            // Append the uploaded file name to the "uploaded.txt" file as a new line
            string uploadRecordFilePath = Path.Combine(VERALogger.Instance.dataPath, "uploadedGeneric.txt");
            var uploaded = File.ReadAllLines(uploadRecordFilePath);
            if (!Array.Exists(uploaded, element => element == Path.GetFileName(filePath)))
            {
                File.AppendAllText(uploadRecordFilePath,
                Path.GetFileName(filePath) + Environment.NewLine);
            }
        }


        #endregion


        #region IMAGES


        // Submits a generic file for the participant, unassociated with any file type
        public IEnumerator SubmitImageFileCoroutine(string imageFilePath, string timestamp = null, byte[] imageData = null)
        {
            string fileName = Path.GetFileName(imageFilePath);

            string experimentUUID = VERALogger.Instance.experimentUUID;
            string siteUUID = VERALogger.Instance.siteUUID;
            string participantUUID = VERALogger.Instance.activeParticipant.participantUUID;
            string apiKey = VERALogger.Instance.apiKey;

            // Set up the data
            string host = VERAHost.hostUrl;
            string url = host + "/api/participants/images/" + experimentUUID + "/" + participantUUID;
            Debug.Log("[VERA Logger] Submitting image file \"" + imageFilePath + "\" to the active participant...");
            if (imageData == null)
            {
                yield return VERALogger.Instance.ReadBinaryDataFile(imageFilePath, (result) => imageData = result);
            }

            if (imageData == null)
            {
                Debug.Log("[VERA Logger] No file data was found to submit.");
                yield break;
            }

            // Set up the request
            WWWForm form = new WWWForm();
            form.AddField("experiment_UUID", experimentUUID);
            form.AddField("participant_UUID", participantUUID);
            form.AddField("ts", timestamp ?? DateTime.UtcNow.ToString("o"));
            form.AddBinaryData("file", imageData, experimentUUID + "-" + participantUUID + ".png", "image/png");

            // Send the request
            UnityWebRequest request = UnityWebRequest.Post(url, form);
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return request.SendWebRequest();

            // Check success
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[VERA Logger] Successfully uploaded the image.");
                OnImageFullyUploaded(imageFilePath);
            }
            else
            {
                Debug.LogError("[VERA Logger] Failed to upload the file: " + request.error);
            }
        }


        // Called when an image file is fully uploaded
        // Append the image file's name to the uploaded records so we know to not upload it again
        public void OnImageFullyUploaded(string filePath)
        {
            // Append the uploaded file name to the "uploaded.txt" file as a new line
            string uploadRecordFilePath = Path.Combine(VERALogger.Instance.dataPath, "uploadedImages.txt");
            var uploaded = File.ReadAllLines(uploadRecordFilePath);
            if (!Array.Exists(uploaded, element => element == Path.GetFileName(filePath)))
            {
                File.AppendAllText(uploadRecordFilePath,
                Path.GetFileName(filePath) + Environment.NewLine);
            }
        }


        #endregion


        #region OTHER HELPERS


        // Moves a file from wherever it is to the generic data path
        public void MoveFileToGenericDataPath(string fileToMove)
        {
            string genericDataPath = VERALogger.Instance.genericDataPath;

            // Create the directory if necessary
            if (!Directory.Exists(genericDataPath))
            {
                Debug.Log($"[VERA Logger] Directory [{genericDataPath}] does not exist, creating it");
                Directory.CreateDirectory(genericDataPath);
            }

            // If the file to move is already in the generic data directory, do nothing
            if (Path.GetFullPath(Path.GetDirectoryName(fileToMove)) == Path.GetFullPath(genericDataPath))
            {
                return;
            }


            string fileName = Path.GetFileName(fileToMove);
            string newPath = Path.Combine(genericDataPath, fileName);

            // If the filename already exists in the generic data directory, delete it in order to overwrite
            if (File.Exists(newPath))
                File.Delete(newPath);

            File.Move(fileToMove, newPath);
        }


        #endregion


    }
}