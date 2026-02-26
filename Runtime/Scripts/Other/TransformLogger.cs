using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VERA
{
    internal class TransformLogger : MonoBehaviour
    {

        // TransformLogger logs the transform of an object every frame, under given eventId

        [Tooltip("The name of the file type this script should record its data to")]
        [SerializeField] private string fileToRecordTo = "DemoFile";
        [Tooltip("The object whose transform will be logged")]
        [SerializeField] private Transform targetObject;
        [Tooltip("The EventID associated with this object's logging")]
        [SerializeField] private int eventId;

        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private Vector3 previousScale;

        // Variables to control logging frequency
        [Tooltip("How frequently logs occur (raise this value for better performance)")]
        [SerializeField] private float logInterval = 0.1f; // Time in seconds between logs
        private float timeSinceLastLog = 0f;

        // First log, for determining initial log
        private bool firstLog = true;

        // Start, set up previous transform
        void Awake()
        {
            if (targetObject != null)
            {
                previousPosition = targetObject.position;
                previousRotation = targetObject.rotation;
                previousScale = targetObject.localScale;
            }
        }

        // Update, make log if pertinent
        void Update()
        {
            if (VERALogger.Instance.collecting && targetObject != null)
            {
                // Accumulate elapsed time
                timeSinceLastLog += Time.deltaTime;

                // If we have never logged before, make a single log, for the initial transform of the object
                if (firstLog)
                {
                    VERALogger.Instance.CreateCsvEntry(fileToRecordTo, eventId, targetObject);
                    firstLog = false;
                    return;
                }

                // Check if the accumulated time exceeds the log interval
                if (timeSinceLastLog >= logInterval)
                {
                    Vector3 currentPosition = targetObject.position;
                    Quaternion currentRotation = targetObject.rotation;
                    Vector3 currentScale = targetObject.localScale;

                    // If the transform has changed, create a log entry
                    if (previousPosition != currentPosition || previousRotation != currentRotation || previousScale != currentScale)
                    {
                        previousPosition = currentPosition;
                        previousRotation = currentRotation;
                        previousScale = currentScale;

                        VERALogger.Instance.CreateCsvEntry(fileToRecordTo, eventId, targetObject);
                    }

                    // Reset the timer
                    timeSinceLastLog = 0f;
                }
            }
        }
    }
}