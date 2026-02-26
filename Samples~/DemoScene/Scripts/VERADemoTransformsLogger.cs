using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace VERA
{
    internal class VERADemoTransformsLogger : MonoBehaviour
    {

        // VERADemoTransformsLogger is a demonstrative script, meant to be used alongside the auto-generated "demo experiment".
        // The script records the transform of the main camera, left controller, and right controller of the XR rig.
        // This occurs at a certain record rate, as defined below, to prevent excessive logs, if desired.
        // "Repeat" positions (such as an object staying completely still between entries) are not repeat-logged.

        [Tooltip("The main camera of the XR rig, which will have its transform recorded")]
        public Camera mainCamera;
        [Tooltip("The XR rig's left controller, which will have its transform recorded")]
        public Transform leftController;
        [Tooltip("The XR rig's right controller, which will have its transform recorded")]
        public Transform rightController;

        // Only record at a rate of 30 hz
        // Adjust as desired
        private float recordRate = 1f / 30f;
        private float elapsedTime = 0f;

        // The "last" recorded position of the objects; for use in preventing duplicate entries
        private Vector3 mainCameraLast = Vector3.zero;
        private Vector3 leftControllerLast = Vector3.zero;
        private Vector3 rightControllerLast = Vector3.zero;

        private void Start()
        {
            // Perform an initial log if the VERA Logger is already initialized.
            // Otherwise, wait for the VERA Logger to be initialized before performing the initial log.
            if (VERASessionManager.initialized)
                PerformInitialLog();
            else
                VERASessionManager.onInitialized.AddListener(PerformInitialLog);
        }

        // Performs the initial log of the main camera and controllers' transforms.
        public void PerformInitialLog()
        {
            // Record initial positions of the objects
            mainCameraLast = mainCamera.transform.position;
            leftControllerLast = leftController.transform.position;
            rightControllerLast = rightController.transform.position;

            // Record a CSV row entry for the objects' initial transforms
            // Use preprocessor directives to ensure the VERAFile generated code exists for this file type
#if VERAFile_PlayerTransforms
            VERAFile_PlayerTransforms.CreateCsvEntry(1, mainCamera.transform);
            VERAFile_PlayerTransforms.CreateCsvEntry(2, leftController.transform);
            VERAFile_PlayerTransforms.CreateCsvEntry(3, rightController.transform);
#else
        Debug.LogWarning("[VERA Default Logging] Cannot log player transforms, as your experiment does not have a " +
            "defined file type named \"PlayerTransforms\". To use this default logging script, please define a file " +
            "type named \"PlayerTransforms\".");
        this.enabled = false;
#endif
        }

        private void Update()
        {
            if (!VERASessionManager.initialized || !VERASessionManager.collecting)
            {
                // If the VERA Logger is not collecting or initialized, do not log anything
                return;
            }

            // Only record an entry if enough time has passed, according to our record rate
            if (elapsedTime >= recordRate)
            {
                // Log the main camera's transform under eventId 1
                if (mainCamera && Vector3.Distance(mainCamera.transform.position, mainCameraLast) > 0.01f)
                {
#if VERAFile_PlayerTransforms
                    VERAFile_PlayerTransforms.CreateCsvEntry(1, mainCamera.transform);
#endif
                    mainCameraLast = mainCamera.transform.position;
                }

                // Log left controller transform under eventId 2
                if (leftController && Vector3.Distance(leftController.position, leftControllerLast) > 0.01f)
                {
#if VERAFile_PlayerTransforms
                    VERAFile_PlayerTransforms.CreateCsvEntry(2, leftController.transform);
#endif
                    leftControllerLast = leftController.position;
                }

                // Log right controller transform under eventId 3
                if (rightController && Vector3.Distance(rightController.position, rightControllerLast) > 0.01f)
                {
#if VERAFile_PlayerTransforms
                    VERAFile_PlayerTransforms.CreateCsvEntry(3, rightController.transform);
#endif
                    rightControllerLast = rightController.position;
                }

                // Reset elapsed time
            }

            // Update elapsed time
            elapsedTime += Time.deltaTime;
        }
    }
}