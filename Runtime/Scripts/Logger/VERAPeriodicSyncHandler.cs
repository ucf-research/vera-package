using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace VERA
{
    internal class VERAPeriodicSyncHandler : MonoBehaviour
    {

        // VERAPeriodicSyncHandler will periodically sync all participant data to the site


        private Coroutine periodicSyncRoutineHandle;
        private bool isFinalSynced = false;

        private float baseInterval = 2f;      // initial polling interval
        private float maxInterval = 200f;     // cap exponential backoff

        private float currentInterval;
        private int failureCount = 0;


        #region SYNC MANAGEMENT


        // Starts the periodic sync of data
        public void StartPeriodicSync()
        {
            currentInterval = baseInterval;
            periodicSyncRoutineHandle = StartCoroutine(PeriodicSyncRoutine());
        }

        private IEnumerator PeriodicSyncRoutine()
        {
            while (true)
            {
                // 1) Check network
                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    Debug.LogWarning("[VERA Sync] Network unreachable; backing off.");
                    yield return new WaitForSeconds(currentInterval);
                    Backoff();
                    continue;
                }

                if (VERALogger.Instance == null)
                {
                    Debug.LogWarning("[VERA Sync] VERALogger.Instance is null; skipping this sync iteration and waiting.");
                    yield return new WaitForSeconds(currentInterval);
                    continue;
                }

                if (VERALogger.Instance.sessionFinalized)
                {
                    Debug.Log("[VERA Sync] Session finalized; stopping periodic sync.");
                    yield break;
                }

                // 2) Attempt upload
                Debug.Log($"[VERA Sync] Attempting sync (interval={currentInterval}s) at {DateTime.Now}");
                yield return StartCoroutine(UploadAllPending());

                // 3) Wait and reset backoff on success
                yield return new WaitForSeconds(currentInterval);
                ResetBackoff();
            }
        }

        public void StopPeriodicSync()
        {
            if (periodicSyncRoutineHandle != null)
            {
                StopCoroutine(periodicSyncRoutineHandle);
                periodicSyncRoutineHandle = null;
                Debug.Log("[VERA Sync] Stopped periodic sync coroutine.");
            }
        }

        private void Backoff()
        {
            failureCount++;
            currentInterval = Mathf.Min(baseInterval * Mathf.Pow(2, failureCount), maxInterval);
        }

        private void ResetBackoff()
        {
            failureCount = 0;
            currentInterval = baseInterval;
        }

        public IEnumerator UploadAllPending()
        {
            // Get all CSV handlers from VERA Logger
            if (VERALogger.Instance == null)
            {
                Debug.LogWarning("[VERA Sync] VERALogger.Instance is null; skipping UploadAllPending.");
                yield break;
            }

            var handlers = VERALogger.Instance.csvHandlers;
            if (handlers == null)
            {
                Debug.LogWarning("[VERA Sync] VERALogger.Instance.csvHandlers is null; nothing to upload.");
                yield break;
            }

            // Upload unsynced portions of all CSV handlers
            bool finalFlag = VERALogger.Instance.sessionFinalized;
            foreach (var csvHandler in handlers)
            {
                if (csvHandler == null) continue;
                yield return StartCoroutine(csvHandler.SubmitFileWithRetry(finalUpload: finalFlag, usePartial: true));
            }
        }


        #endregion


        #region FINAL SYNC


        // Performs a final sync of all data, ensuring everything is uploaded
        public IEnumerator FinalSync()
        {
            if (isFinalSynced) yield break;
            isFinalSynced = true;

            // Stop the periodic sync
            if (periodicSyncRoutineHandle != null)
            {
                StopCoroutine(periodicSyncRoutineHandle);
                Debug.Log("[VERA Sync] Stopped periodic sync coroutine after final upload.");
            }

            // Upload all CSVs
            if (VERALogger.Instance != null && VERALogger.Instance.csvHandlers != null)
            {
                foreach (VERACsvHandler csvHandler in VERALogger.Instance.csvHandlers)
                {
                    yield return csvHandler?.SubmitFileWithRetry(finalUpload: true, usePartial: true);
                }
            }

            yield return null;
        }


        // Deletes any partial sync files that may exist
        public void CleanupPartialSyncFiles()
        {
            if (VERALogger.Instance == null || VERALogger.Instance.csvHandlers == null) return;

            foreach (var csvHandler in VERALogger.Instance.csvHandlers)
            {
                if (csvHandler == null) continue;

                csvHandler.DeletePartialFile();
            }
        }


        #endregion


    }
}