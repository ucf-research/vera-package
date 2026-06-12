using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_XR_INTERACTION_TOOLKIT
using UnityEngine.XR.Interaction.Toolkit;
#endif

namespace VERA
{
    internal class VERABaselineDataLogger : MonoBehaviour
    {
        [Header("Logging Options")]
        [Tooltip("Automatically start logging when VERA Logger is initialized")]
        [SerializeField] public bool autoStartLogging = true;

        [Header("Tracking Settings")]
        [Tooltip("Log baseline data every frame for maximum fidelity")]
        [SerializeField] private bool logEveryFrame = true;

        [Header("XR Components")]
        [Tooltip("Main camera representing the headset")]
        [SerializeField] private Camera headsetCamera;

        [Tooltip("Left controller transform")]
        [SerializeField] private Transform leftController;

        [Tooltip("Right controller transform")]
        [SerializeField] private Transform rightController;

        [Header("Input Actions (Optional)")]
        [Tooltip("Input actions for left controller buttons - leave empty to auto-detect from ActionBasedController")]
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private InputActionProperty leftTriggerAction;
        [SerializeField] private InputActionProperty leftGripAction;
        [SerializeField] private InputActionProperty leftPrimaryButtonAction;
        [SerializeField] private InputActionProperty leftSecondaryButtonAction;
        [SerializeField] private InputActionProperty leftPrimary2DAxisClickAction;
        [SerializeField] private InputActionProperty leftThumbstickAction;

        [Tooltip("Input actions for right controller buttons - leave empty to auto-detect from ActionBasedController")]
        [SerializeField] private InputActionProperty rightTriggerAction;
        [SerializeField] private InputActionProperty rightGripAction;
        [SerializeField] private InputActionProperty rightPrimaryButtonAction;
        [SerializeField] private InputActionProperty rightSecondaryButtonAction;
        [SerializeField] private InputActionProperty rightPrimary2DAxisClickAction;
        [SerializeField] private InputActionProperty rightThumbstickAction;
#endif

        // Internal variables
        private int currentSampleIndex = 0;
        private bool isLogging = false;
        // Separate timer to refresh device lists at a fixed interval (1s)
        private float refreshTimer = 0f;

        // XR device tracking
        private List<UnityEngine.XR.InputDevice> leftHandDevices = new List<UnityEngine.XR.InputDevice>();
        private List<UnityEngine.XR.InputDevice> rightHandDevices = new List<UnityEngine.XR.InputDevice>();
        private List<UnityEngine.XR.InputDevice> headDevices = new List<UnityEngine.XR.InputDevice>();

        // Device detection cache
        private bool headsetDetected = false;
        private bool leftControllerDetected = false;
        private bool rightControllerDetected = false;

        private void Start()
        {
            // Initialize XR device tracking
            RefreshDeviceLists();

            // Start logging if VERA Logger is ready and auto-start is enabled
            if (autoStartLogging && VERALogger.Instance != null && VERALogger.Instance.initialized)
            {
                StartLogging();
            }
            else if (autoStartLogging && VERALogger.Instance != null)
            {
                VERALogger.Instance.onLoggerInitialized.AddListener(StartLogging);
            }
            else if (!autoStartLogging)
            {
                // Auto-start disabled - call StartBaselineLogging() manually to begin
            }
            else
            {
                // VERALogger.Instance is null - baseline logging will not work
            }

            // Auto-find components if not assigned
            AutoAssignComponents();
        }

        private void AutoAssignComponents()
        {
            // Auto-assign headset camera if not set
            if (headsetCamera == null)
            {
                headsetCamera = FindHeadsetCamera();
            }

            // Try to find XR controller components using robust XRI-based detection
            if (leftController == null || rightController == null)
            {
                FindControllersUsingXRComponents();
            }

            // Auto-detect input actions from ActionBasedController components
            AutoDetectInputActions();
        }

        private Camera FindHeadsetCamera()
        {
            // First try Camera.main
            Camera cam = Camera.main;
            if (cam != null)
                return cam;

#if UNITY_XR_INTERACTION_TOOLKIT
            // Try to find camera in XR Origin
#if UNITY_2023_1_OR_NEWER
            var xrOrigin = FindAnyObjectByType<XROrigin>();
#else
            var xrOrigin = FindObjectOfType<XROrigin>();
#endif
            if (xrOrigin != null && xrOrigin.Camera != null)
            {
                return xrOrigin.Camera;
            }
#endif

            // Fallback to any camera
#if UNITY_2023_1_OR_NEWER
            return FindAnyObjectByType<Camera>();
#else
            return FindObjectOfType<Camera>();
#endif
        }

        private void FindControllersUsingXRComponents()
        {
#if UNITY_XR_INTERACTION_TOOLKIT
            // Method 1: Use XRBaseController components (works with both ActionBased and DeviceBased)
            if (leftController == null || rightController == null)
            {
#if UNITY_2023_1_OR_NEWER
                var baseControllers = FindObjectsByType<XRBaseController>(FindObjectsSortMode.None);
#else
                var baseControllers = FindObjectsOfType<XRBaseController>();
#endif
                foreach (var controller in baseControllers)
                {
                    if (controller.controllerNode == XRNode.LeftHand && leftController == null)
                    {
                        leftController = controller.transform;
                    }
                    else if (controller.controllerNode == XRNode.RightHand && rightController == null)
                    {
                        rightController = controller.transform;
                    }
                }
            }

            // Method 2: Search within XROrigin hierarchy
            if (leftController == null || rightController == null)
            {
#if UNITY_2023_1_OR_NEWER
                var xrOrigin = FindAnyObjectByType<XROrigin>();
#else
                var xrOrigin = FindObjectOfType<XROrigin>();
#endif
                if (xrOrigin != null)
                {
                    // Look for XRController or similar components in the hierarchy
                    var controllersInHierarchy = xrOrigin.GetComponentsInChildren<XRBaseController>();
                    foreach (var controller in controllersInHierarchy)
                    {
                        if (controller.controllerNode == XRNode.LeftHand && leftController == null)
                        {
                            leftController = controller.transform;
                        }
                        else if (controller.controllerNode == XRNode.RightHand && rightController == null)
                        {
                            rightController = controller.transform;
                        }
                    }
                }
            }
#endif

            // Fallback: Name-based detection
            if (leftController == null || rightController == null)
            {
                FindControllersUsingNameDetection();
            }
        }

        private void FindControllersUsingNameDetection()
        {
            // Look for any GameObject that might be an XR rig
#if UNITY_2023_1_OR_NEWER
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
#else
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
#endif
            Transform xrRig = null;

            // Look for common XR rig names
            foreach (GameObject obj in allObjects)
            {
                string name = obj.name.ToLower();
                if (name.Contains("xr") && (name.Contains("rig") || name.Contains("origin") || name.Contains("player")))
                {
                    xrRig = obj.transform;
                    break;
                }
            }

            if (xrRig != null)
            {
                // Try to find left and right controller transforms
                Transform[] allTransforms = xrRig.GetComponentsInChildren<Transform>();
                foreach (Transform t in allTransforms)
                {
                    string name = t.name.ToLower();
                    if (leftController == null && (name.Contains("left") && (name.Contains("controller") || name.Contains("hand"))))
                    {
                        leftController = t;
                    }
                    else if (rightController == null && (name.Contains("right") && (name.Contains("controller") || name.Contains("hand"))))
                    {
                        rightController = t;
                    }
                }
            }

            // If still not found, try other common naming patterns
            if (leftController == null || rightController == null)
            {
#if UNITY_2023_1_OR_NEWER
                var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
#else
                var allTransforms = FindObjectsOfType<Transform>();
#endif
                foreach (Transform t in allTransforms)
                {
                    string name = t.name.ToLower();
                    if (leftController == null &&
                        (name.Contains("left") &&
                         (name.Contains("controller") || name.Contains("hand") || name.Contains("grip"))))
                    {
                        leftController = t;
                    }
                    else if (rightController == null &&
                                (name.Contains("right") &&
                                (name.Contains("controller") || name.Contains("hand") || name.Contains("grip"))))
                    {
                        rightController = t;
                    }
                }
            }
        }

        private void AutoDetectInputActions()
        {
#if ENABLE_INPUT_SYSTEM && UNITY_XR_INTERACTION_TOOLKIT
            // Only auto-detect if not manually assigned
            
            // Try to find ActionBasedController components
#if UNITY_2023_1_OR_NEWER
            var controllers = FindObjectsByType<ActionBasedController>(FindObjectsSortMode.None);
#else
            var controllers = FindObjectsOfType<ActionBasedController>();
#endif

            foreach (var controller in controllers)
            {
                // Determine if this is a left or right controller by checking the transform or node
                bool isLeftController = false;
                bool isRightController = false;

                // Check by XR node
                if (controller.controllerNode == XRNode.LeftHand)
                {
                    isLeftController = true;
                }
                else if (controller.controllerNode == XRNode.RightHand)
                {
                    isRightController = true;
                }
                else
                {
                    // Fallback to name-based detection
                    string name = controller.gameObject.name.ToLower();
                    if (name.Contains("left"))
                    {
                        isLeftController = true;
                    }
                    else if (name.Contains("right"))
                    {
                        isRightController = true;
                    }
                }

                // Auto-assign left controller input actions if not manually set
                if (isLeftController)
                {
                    if (leftTriggerAction.action == null)
                        leftTriggerAction = controller.selectAction;
                    if (leftGripAction.action == null)
                        leftGripAction = controller.activateAction;
                    if (leftPrimaryButtonAction.action == null)
                        leftPrimaryButtonAction = controller.uiPressAction;
                    // Note: ActionBasedController doesn't have direct references to all buttons
                    // Secondary button and joystick click would need to be read from the device directly
                }

                // Auto-assign right controller input actions if not manually set
                if (isRightController)
                {
                    if (rightTriggerAction.action == null)
                        rightTriggerAction = controller.selectAction;
                    if (rightGripAction.action == null)
                        rightGripAction = controller.activateAction;
                    if (rightPrimaryButtonAction.action == null)
                        rightPrimaryButtonAction = controller.uiPressAction;
                }
            }
#endif
        }

        private void StartLogging()
        {
            if (!isLogging)
            {
                isLogging = true;
                currentSampleIndex = 0;
            }
        }

        public void StopLogging()
        {
            isLogging = false;
        }

        public void StartBaselineLogging()
        {
            StartLogging();
        }

        private void Update()
        {
            // Check if we should be logging baseline data
            // We only need VERALogger to exist and be initialized, not necessarily collecting
            if (!isLogging || VERALogger.Instance == null || !VERALogger.Instance.initialized)
            {
                return;
            }

            // Log data every frame for maximum fidelity
            if (logEveryFrame)
            {
                LogBaselineData();
            }

            // Update refresh timer independently so device lists refresh roughly once per second
            refreshTimer += Time.deltaTime;
            if (refreshTimer >= 1f)
            {
                RefreshDeviceLists();
                refreshTimer = 0f;
            }
        }

        private void RefreshDeviceLists()
        {
            // Refresh device lists
            leftHandDevices.Clear();
            rightHandDevices.Clear();
            headDevices.Clear();

            InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftHandDevices);
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightHandDevices);
            InputDevices.GetDevicesAtXRNode(XRNode.Head, headDevices);

            // Update detection status
            headsetDetected = headDevices.Count > 0;
            leftControllerDetected = leftHandDevices.Count > 0;
            rightControllerDetected = rightHandDevices.Count > 0;
        }

        private void LogBaselineData()
        {
            try
            {
                // Device list refresh is handled by a time-based refreshTimer in Update()

                // Generate unique event ID for this sample
                //string eventId = System.Guid.NewGuid().ToString();

                // Collect all baseline data
                var baselineData = CollectBaselineData();

                // Log to VERA CSV system using the baseline data file type
                LogToVERASystem(baselineData);

                currentSampleIndex++;
            }
            catch (Exception e)
            {
                VERADebugger.LogError($"Error logging baseline data: {e.Message}", "VERABaselineDataLogger");
                VERADebugger.LogError($"Stack trace: {e.StackTrace}", "VERABaselineDataLogger");
            }
        }

        private BaselineDataEntry CollectBaselineData()
        {
            var data = new BaselineDataEntry
            {
                ts = DateTime.UtcNow
            };

            // --- Headset ---
            bool isHeadsetPresent = headDevices.Count > 0;
            data.headsetDetected = isHeadsetPresent;

            // Virtual pose from scene transform
            if (isHeadsetPresent && headsetCamera != null)
            {
                PopulateVirtualPose(headsetCamera.transform,
                    out data.headsetVirtualPosX, out data.headsetVirtualPosY, out data.headsetVirtualPosZ,
                    out data.headsetVirtualRotEulerX, out data.headsetVirtualRotEulerY, out data.headsetVirtualRotEulerZ,
                    out data.headsetVirtualRotQuatX, out data.headsetVirtualRotQuatY, out data.headsetVirtualRotQuatZ, out data.headsetVirtualRotQuatW);
            }

            // Tracking pose from XR device (physical space)
            PopulateTrackingPose(headDevices,
                out data.headsetTrackingPosX, out data.headsetTrackingPosY, out data.headsetTrackingPosZ,
                out data.headsetTrackingRotEulerX, out data.headsetTrackingRotEulerY, out data.headsetTrackingRotEulerZ,
                out data.headsetTrackingRotQuatX, out data.headsetTrackingRotQuatY, out data.headsetTrackingRotQuatZ, out data.headsetTrackingRotQuatW);

            // --- Left controller ---
            bool isLeftPresent = leftHandDevices.Count > 0;
            data.leftDetected = isLeftPresent;

            if (isLeftPresent && leftController != null)
            {
                PopulateVirtualPose(leftController,
                    out data.leftControllerVirtualPosX, out data.leftControllerVirtualPosY, out data.leftControllerVirtualPosZ,
                    out data.leftControllerVirtualRotEulerX, out data.leftControllerVirtualRotEulerY, out data.leftControllerVirtualRotEulerZ,
                    out data.leftControllerVirtualRotQuatX, out data.leftControllerVirtualRotQuatY, out data.leftControllerVirtualRotQuatZ, out data.leftControllerVirtualRotQuatW);
            }

            PopulateTrackingPose(leftHandDevices,
                out data.leftControllerTrackingPosX, out data.leftControllerTrackingPosY, out data.leftControllerTrackingPosZ,
                out data.leftControllerTrackingRotEulerX, out data.leftControllerTrackingRotEulerY, out data.leftControllerTrackingRotEulerZ,
                out data.leftControllerTrackingRotQuatX, out data.leftControllerTrackingRotQuatY, out data.leftControllerTrackingRotQuatZ, out data.leftControllerTrackingRotQuatW);

            // Left controller input states - try Input System first, fallback to XR devices
#if ENABLE_INPUT_SYSTEM
            if (leftTriggerAction != null && leftTriggerAction.action != null)
                data.leftTrigger = GetFloatInputState(leftTriggerAction);
            else
                data.leftTrigger = GetFloatInputStateFromDevice(leftHandDevices, UnityEngine.XR.CommonUsages.trigger);
            if (leftGripAction != null && leftGripAction.action != null)
                data.leftGrip = GetFloatInputState(leftGripAction);
            else
                data.leftGrip = GetFloatInputStateFromDevice(leftHandDevices, UnityEngine.XR.CommonUsages.grip);
            if (leftPrimaryButtonAction != null && leftPrimaryButtonAction.action != null)
                data.leftPrimaryButton = GetInputState(leftPrimaryButtonAction);
            else
                data.leftPrimaryButton = GetInputStateFromDevice(leftHandDevices, UnityEngine.XR.CommonUsages.primaryButton);
            if (leftSecondaryButtonAction != null && leftSecondaryButtonAction.action != null)
                data.leftSecondaryButton = GetInputState(leftSecondaryButtonAction);
            else
                data.leftSecondaryButton = GetInputStateFromDevice(leftHandDevices, UnityEngine.XR.CommonUsages.secondaryButton);
            if (leftPrimary2DAxisClickAction != null && leftPrimary2DAxisClickAction.action != null)
                data.leftPrimary2DAxisClick = GetInputState(leftPrimary2DAxisClickAction);
            else
                data.leftPrimary2DAxisClick = GetInputStateFromDevice(leftHandDevices, UnityEngine.XR.CommonUsages.primary2DAxisClick);
            Vector2 leftAxis;
            if (leftThumbstickAction != null && leftThumbstickAction.action != null)
                leftAxis = GetVector2InputState(leftThumbstickAction);
            else
                leftAxis = GetVector2InputStateFromDevice(leftHandDevices, UnityEngine.XR.CommonUsages.primary2DAxis);
            data.leftThumbstickX = leftAxis.x;
            data.leftThumbstickY = leftAxis.y;
#else
            data.leftTrigger = GetFloatInputStateFromDevice(leftHandDevices, UnityEngine.XR.CommonUsages.trigger);
            data.leftGrip = GetFloatInputStateFromDevice(leftHandDevices, UnityEngine.XR.CommonUsages.grip);
            data.leftPrimaryButton = GetInputStateFromDevice(leftHandDevices, UnityEngine.XR.CommonUsages.primaryButton);
            data.leftSecondaryButton = GetInputStateFromDevice(leftHandDevices, UnityEngine.XR.CommonUsages.secondaryButton);
            data.leftPrimary2DAxisClick = GetInputStateFromDevice(leftHandDevices, UnityEngine.XR.CommonUsages.primary2DAxisClick);
            Vector2 leftAxisFallback = GetVector2InputStateFromDevice(leftHandDevices, UnityEngine.XR.CommonUsages.primary2DAxis);
            data.leftThumbstickX = leftAxisFallback.x;
            data.leftThumbstickY = leftAxisFallback.y;
#endif

            // --- Right controller ---
            bool isRightPresent = rightHandDevices.Count > 0;
            data.rightDetected = isRightPresent;

            if (isRightPresent && rightController != null)
            {
                PopulateVirtualPose(rightController,
                    out data.rightControllerVirtualPosX, out data.rightControllerVirtualPosY, out data.rightControllerVirtualPosZ,
                    out data.rightControllerVirtualRotEulerX, out data.rightControllerVirtualRotEulerY, out data.rightControllerVirtualRotEulerZ,
                    out data.rightControllerVirtualRotQuatX, out data.rightControllerVirtualRotQuatY, out data.rightControllerVirtualRotQuatZ, out data.rightControllerVirtualRotQuatW);
            }

            PopulateTrackingPose(rightHandDevices,
                out data.rightControllerTrackingPosX, out data.rightControllerTrackingPosY, out data.rightControllerTrackingPosZ,
                out data.rightControllerTrackingRotEulerX, out data.rightControllerTrackingRotEulerY, out data.rightControllerTrackingRotEulerZ,
                out data.rightControllerTrackingRotQuatX, out data.rightControllerTrackingRotQuatY, out data.rightControllerTrackingRotQuatZ, out data.rightControllerTrackingRotQuatW);

            // Right controller input states - try Input System first, fallback to XR devices
#if ENABLE_INPUT_SYSTEM
            if (rightTriggerAction != null && rightTriggerAction.action != null)
                data.rightTrigger = GetFloatInputState(rightTriggerAction);
            else
                data.rightTrigger = GetFloatInputStateFromDevice(rightHandDevices, UnityEngine.XR.CommonUsages.trigger);
            if (rightGripAction != null && rightGripAction.action != null)
                data.rightGrip = GetFloatInputState(rightGripAction);
            else
                data.rightGrip = GetFloatInputStateFromDevice(rightHandDevices, UnityEngine.XR.CommonUsages.grip);
            if (rightPrimaryButtonAction != null && rightPrimaryButtonAction.action != null)
                data.rightPrimaryButton = GetInputState(rightPrimaryButtonAction);
            else
                data.rightPrimaryButton = GetInputStateFromDevice(rightHandDevices, UnityEngine.XR.CommonUsages.primaryButton);
            if (rightSecondaryButtonAction != null && rightSecondaryButtonAction.action != null)
                data.rightSecondaryButton = GetInputState(rightSecondaryButtonAction);
            else
                data.rightSecondaryButton = GetInputStateFromDevice(rightHandDevices, UnityEngine.XR.CommonUsages.secondaryButton);
            if (rightPrimary2DAxisClickAction != null && rightPrimary2DAxisClickAction.action != null)
                data.rightPrimary2DAxisClick = GetInputState(rightPrimary2DAxisClickAction);
            else
                data.rightPrimary2DAxisClick = GetInputStateFromDevice(rightHandDevices, UnityEngine.XR.CommonUsages.primary2DAxisClick);
            Vector2 rightAxis;
            if (rightThumbstickAction != null && rightThumbstickAction.action != null)
                rightAxis = GetVector2InputState(rightThumbstickAction);
            else
                rightAxis = GetVector2InputStateFromDevice(rightHandDevices, UnityEngine.XR.CommonUsages.primary2DAxis);
            data.rightThumbstickX = rightAxis.x;
            data.rightThumbstickY = rightAxis.y;
#else
            data.rightTrigger = GetFloatInputStateFromDevice(rightHandDevices, UnityEngine.XR.CommonUsages.trigger);
            data.rightGrip = GetFloatInputStateFromDevice(rightHandDevices, UnityEngine.XR.CommonUsages.grip);
            data.rightPrimaryButton = GetInputStateFromDevice(rightHandDevices, UnityEngine.XR.CommonUsages.primaryButton);
            data.rightSecondaryButton = GetInputStateFromDevice(rightHandDevices, UnityEngine.XR.CommonUsages.secondaryButton);
            data.rightPrimary2DAxisClick = GetInputStateFromDevice(rightHandDevices, UnityEngine.XR.CommonUsages.primary2DAxisClick);
            Vector2 rightAxisFallback = GetVector2InputStateFromDevice(rightHandDevices, UnityEngine.XR.CommonUsages.primary2DAxis);
            data.rightThumbstickX = rightAxisFallback.x;
            data.rightThumbstickY = rightAxisFallback.y;
#endif

            return data;
        }

        private void LogToVERASystem(BaselineDataEntry data)
        {
            // Only log to VERA server - no local CSV fallback
            if (VERALogger.Instance == null || !VERALogger.Instance.collecting)
            {
                return; // Skip logging if VERA is not collecting
            }

            try
            {
                // Log baseline data directly to VERA - column order must match VERAExperimentTelemetrySchema
                VERASessionManager.CreateArbitraryCsvEntry(
                    VERAExperimentTelemetrySchema.Name,
                    // Headset
                    data.headsetDetected,
                    // Headset virtual pose
                    data.headsetVirtualPosX,
                    data.headsetVirtualPosY,
                    data.headsetVirtualPosZ,
                    data.headsetVirtualRotEulerX,
                    data.headsetVirtualRotEulerY,
                    data.headsetVirtualRotEulerZ,
                    data.headsetVirtualRotQuatX,
                    data.headsetVirtualRotQuatY,
                    data.headsetVirtualRotQuatZ,
                    data.headsetVirtualRotQuatW,
                    // Headset tracking pose
                    data.headsetTrackingPosX,
                    data.headsetTrackingPosY,
                    data.headsetTrackingPosZ,
                    data.headsetTrackingRotEulerX,
                    data.headsetTrackingRotEulerY,
                    data.headsetTrackingRotEulerZ,
                    data.headsetTrackingRotQuatX,
                    data.headsetTrackingRotQuatY,
                    data.headsetTrackingRotQuatZ,
                    data.headsetTrackingRotQuatW,
                    // Left controller
                    data.leftDetected,
                    // Left virtual pose
                    data.leftControllerVirtualPosX,
                    data.leftControllerVirtualPosY,
                    data.leftControllerVirtualPosZ,
                    data.leftControllerVirtualRotEulerX,
                    data.leftControllerVirtualRotEulerY,
                    data.leftControllerVirtualRotEulerZ,
                    data.leftControllerVirtualRotQuatX,
                    data.leftControllerVirtualRotQuatY,
                    data.leftControllerVirtualRotQuatZ,
                    data.leftControllerVirtualRotQuatW,
                    // Left tracking pose
                    data.leftControllerTrackingPosX,
                    data.leftControllerTrackingPosY,
                    data.leftControllerTrackingPosZ,
                    data.leftControllerTrackingRotEulerX,
                    data.leftControllerTrackingRotEulerY,
                    data.leftControllerTrackingRotEulerZ,
                    data.leftControllerTrackingRotQuatX,
                    data.leftControllerTrackingRotQuatY,
                    data.leftControllerTrackingRotQuatZ,
                    data.leftControllerTrackingRotQuatW,
                    // Left input
                    data.leftTrigger,
                    data.leftGrip,
                    data.leftPrimaryButton,
                    data.leftSecondaryButton,
                    data.leftPrimary2DAxisClick,
                    data.leftThumbstickX,
                    data.leftThumbstickY,
                    // Right controller
                    data.rightDetected,
                    // Right virtual pose
                    data.rightControllerVirtualPosX,
                    data.rightControllerVirtualPosY,
                    data.rightControllerVirtualPosZ,
                    data.rightControllerVirtualRotEulerX,
                    data.rightControllerVirtualRotEulerY,
                    data.rightControllerVirtualRotEulerZ,
                    data.rightControllerVirtualRotQuatX,
                    data.rightControllerVirtualRotQuatY,
                    data.rightControllerVirtualRotQuatZ,
                    data.rightControllerVirtualRotQuatW,
                    // Right tracking pose
                    data.rightControllerTrackingPosX,
                    data.rightControllerTrackingPosY,
                    data.rightControllerTrackingPosZ,
                    data.rightControllerTrackingRotEulerX,
                    data.rightControllerTrackingRotEulerY,
                    data.rightControllerTrackingRotEulerZ,
                    data.rightControllerTrackingRotQuatX,
                    data.rightControllerTrackingRotQuatY,
                    data.rightControllerTrackingRotQuatZ,
                    data.rightControllerTrackingRotQuatW,
                    // Right input
                    data.rightTrigger,
                    data.rightGrip,
                    data.rightPrimaryButton,
                    data.rightSecondaryButton,
                    data.rightPrimary2DAxisClick,
                    data.rightThumbstickX,
                    data.rightThumbstickY
                );
            }
            catch (System.Exception e)
            {
                VERADebugger.LogError($"Exception in LogToVERASystem: {e.Message}", "VERABaselineDataLogger");
                VERADebugger.LogError($"Stack trace: {e.StackTrace}", "VERABaselineDataLogger");
            }
        }

        private void PopulateVirtualPose(
            Transform t,
            out float posX, out float posY, out float posZ,
            out float eulerX, out float eulerY, out float eulerZ,
            out float quatX, out float quatY, out float quatZ, out float quatW)
        {
            Vector3 pos = t.position;
            Vector3 euler = t.eulerAngles;
            Quaternion q = t.rotation;
            posX = pos.x; posY = pos.y; posZ = pos.z;
            eulerX = euler.x; eulerY = euler.y; eulerZ = euler.z;
            quatX = q.x; quatY = q.y; quatZ = q.z; quatW = q.w;
        }

        private void PopulateTrackingPose(
            List<UnityEngine.XR.InputDevice> devices,
            out float posX, out float posY, out float posZ,
            out float eulerX, out float eulerY, out float eulerZ,
            out float quatX, out float quatY, out float quatZ, out float quatW)
        {
            posX = posY = posZ = 0f;
            eulerX = eulerY = eulerZ = 0f;
            quatX = quatY = quatZ = 0f; quatW = 1f;

            if (devices.Count == 0) return;

            foreach (var device in devices)
            {
                if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 pos))
                {
                    posX = pos.x; posY = pos.y; posZ = pos.z;
                }
                if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion q))
                {
                    Vector3 euler = q.eulerAngles;
                    eulerX = euler.x; eulerY = euler.y; eulerZ = euler.z;
                    quatX = q.x; quatY = q.y; quatZ = q.z; quatW = q.w;
                }
                break; // use the first device
            }
        }

        private float GetFloatInputState(InputActionProperty actionProperty)
        {
#if ENABLE_INPUT_SYSTEM
            if (actionProperty.action == null)
                return -1f; // NA when unknown

            try
            {
                if (actionProperty.action.activeControl?.valueType == typeof(float))
                {
                    float value = actionProperty.action.ReadValue<float>();
                    return value;
                }
                else
                {
                    return -1f; // NA for non-float inputs
                }
            }
            catch
            {
                return -1f; // NA when error occurs
            }
#else
            // Fallback to legacy XR input or return NA
            return -1f;
#endif
        }

        private int GetInputState(InputActionProperty actionProperty)
        {
#if ENABLE_INPUT_SYSTEM
            if (actionProperty.action == null)
                return -1; // NA when unknown

            try
            {
                if (actionProperty.action.activeControl?.valueType == typeof(float))
                {
                    float value = actionProperty.action.ReadValue<float>();
                    return value > 0.5f ? 1 : 0;
                }
                else if (actionProperty.action.activeControl?.valueType == typeof(bool))
                {
                    bool value = actionProperty.action.ReadValue<bool>();
                    return value ? 1 : 0;
                }
                else
                {
                    // For button inputs, check if pressed
                    return actionProperty.action.IsPressed() ? 1 : 0;
                }
            }
            catch
            {
                return -1; // NA when error occurs
            }
#else
            // Fallback to legacy XR input or return NA
            return -1;
#endif
        }

        private int GetInputStateFromDevice(List<UnityEngine.XR.InputDevice> devices, InputFeatureUsage<bool> buttonUsage)
        {
            if (devices.Count == 0)
                return -1; // NA when no device

            foreach (var device in devices)
            {
                if (device.TryGetFeatureValue(buttonUsage, out bool buttonState))
                {
                    return buttonState ? 1 : 0;
                }
            }

            return -1; // NA when unable to read
        }

        private int GetInputStateFromDevice(List<UnityEngine.XR.InputDevice> devices, InputFeatureUsage<float> floatUsage)
        {
            if (devices.Count == 0)
                return -1; // NA when no device

            foreach (var device in devices)
            {
                if (device.TryGetFeatureValue(floatUsage, out float value))
                {
                    return value > 0.5f ? 1 : 0;
                }
            }

            return -1; // NA when unable to read
        }

        private float GetFloatInputStateFromDevice(List<UnityEngine.XR.InputDevice> devices, InputFeatureUsage<float> floatUsage)
        {
            if (devices.Count == 0)
                return -1f; // NA when no device

            foreach (var device in devices)
            {
                if (device.TryGetFeatureValue(floatUsage, out float value))
                {
                    return value;
                }
            }

            return -1f; // NA when unable to read
        }

        private Vector2 GetVector2InputState(InputActionProperty actionProperty)
        {
#if ENABLE_INPUT_SYSTEM
            if (actionProperty.action == null)
                return new Vector2(-2f, -2f); // NA when unknown

            try
            {
                return actionProperty.action.ReadValue<Vector2>();
            }
            catch
            {
                return new Vector2(-2f, -2f); // NA when error occurs
            }
#else
            return new Vector2(-2f, -2f);
#endif
        }

        private Vector2 GetVector2InputStateFromDevice(List<UnityEngine.XR.InputDevice> devices, InputFeatureUsage<Vector2> v2Usage)
        {
            if (devices.Count == 0)
                return new Vector2(-2f, -2f); // NA when no device

            foreach (var device in devices)
            {
                if (device.TryGetFeatureValue(v2Usage, out Vector2 value))
                {
                    return value;
                }
            }

            return new Vector2(-2f, -2f); // NA when unable to read
        }

        [System.Serializable]
        public class BaselineDataEntry
        {
            public DateTime ts;

            // Headset
            public bool headsetDetected;
            // Virtual pose (scene transform)
            public float headsetVirtualPosX, headsetVirtualPosY, headsetVirtualPosZ;
            public float headsetVirtualRotEulerX, headsetVirtualRotEulerY, headsetVirtualRotEulerZ;
            public float headsetVirtualRotQuatX, headsetVirtualRotQuatY, headsetVirtualRotQuatZ, headsetVirtualRotQuatW;
            // Tracking pose (physical space)
            public float headsetTrackingPosX, headsetTrackingPosY, headsetTrackingPosZ;
            public float headsetTrackingRotEulerX, headsetTrackingRotEulerY, headsetTrackingRotEulerZ;
            public float headsetTrackingRotQuatX, headsetTrackingRotQuatY, headsetTrackingRotQuatZ, headsetTrackingRotQuatW;

            // Left controller
            public bool leftDetected;
            // Virtual pose
            public float leftControllerVirtualPosX, leftControllerVirtualPosY, leftControllerVirtualPosZ;
            public float leftControllerVirtualRotEulerX, leftControllerVirtualRotEulerY, leftControllerVirtualRotEulerZ;
            public float leftControllerVirtualRotQuatX, leftControllerVirtualRotQuatY, leftControllerVirtualRotQuatZ, leftControllerVirtualRotQuatW;
            // Tracking pose
            public float leftControllerTrackingPosX, leftControllerTrackingPosY, leftControllerTrackingPosZ;
            public float leftControllerTrackingRotEulerX, leftControllerTrackingRotEulerY, leftControllerTrackingRotEulerZ;
            public float leftControllerTrackingRotQuatX, leftControllerTrackingRotQuatY, leftControllerTrackingRotQuatZ, leftControllerTrackingRotQuatW;
            // Input
            public float leftTrigger;
            public float leftGrip;
            public int leftPrimaryButton;
            public int leftSecondaryButton;
            public int leftPrimary2DAxisClick;
            public float leftThumbstickX, leftThumbstickY;

            // Right controller
            public bool rightDetected;
            // Virtual pose
            public float rightControllerVirtualPosX, rightControllerVirtualPosY, rightControllerVirtualPosZ;
            public float rightControllerVirtualRotEulerX, rightControllerVirtualRotEulerY, rightControllerVirtualRotEulerZ;
            public float rightControllerVirtualRotQuatX, rightControllerVirtualRotQuatY, rightControllerVirtualRotQuatZ, rightControllerVirtualRotQuatW;
            // Tracking pose
            public float rightControllerTrackingPosX, rightControllerTrackingPosY, rightControllerTrackingPosZ;
            public float rightControllerTrackingRotEulerX, rightControllerTrackingRotEulerY, rightControllerTrackingRotEulerZ;
            public float rightControllerTrackingRotQuatX, rightControllerTrackingRotQuatY, rightControllerTrackingRotQuatZ, rightControllerTrackingRotQuatW;
            // Input
            public float rightTrigger;
            public float rightGrip;
            public int rightPrimaryButton;
            public int rightSecondaryButton;
            public int rightPrimary2DAxisClick;
            public float rightThumbstickX, rightThumbstickY;
        }

        #region Public API

        public void SetLogEveryFrame(bool enabled)
        {
            logEveryFrame = enabled;
        }

        public bool GetLogEveryFrame() => logEveryFrame;

        public int GetCurrentSampleIndex() => currentSampleIndex;

        public bool IsLogging() => isLogging;

        public void SetControllerTransforms(Transform left, Transform right)
        {
            leftController = left;
            rightController = right;
        }

        public void SetHeadsetCamera(Camera camera)
        {
            headsetCamera = camera;
        }

        #endregion
    }
}


