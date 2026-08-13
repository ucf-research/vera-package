using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
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
        // XR device tracking (legacy InputDevices API — often empty under WebXR)
        private List<UnityEngine.XR.InputDevice> leftHandDevices = new List<UnityEngine.XR.InputDevice>();
        private List<UnityEngine.XR.InputDevice> rightHandDevices = new List<UnityEngine.XR.InputDevice>();
        private List<UnityEngine.XR.InputDevice> headDevices = new List<UnityEngine.XR.InputDevice>();

        // Reused buffer for InputTracking.GetNodeStates fallback
        private readonly List<XRNodeState> xrNodeStates = new List<XRNodeState>();

        // Cached XR display subsystem list for IsXrDisplayRunning()
        private readonly List<XRDisplaySubsystem> xrDisplaySubsystems = new List<XRDisplaySubsystem>();

        // Device detection cache
        private bool headsetDetected = false;
        private bool leftControllerDetected = false;
        private bool rightControllerDetected = false;

        // WebXR / transform fallback: only treat scene transforms as live after they move
        // while an XR display is running (avoids always-detected rest poses).
        private bool hasLastHeadsetPose;
        private Vector3 lastHeadsetPos;
        private Quaternion lastHeadsetRot;
        private bool headsetTransformLive;

        private bool hasLastLeftPose;
        private Vector3 lastLeftPos;
        private Quaternion lastLeftRot;
        private bool leftTransformLive;

        private bool hasLastRightPose;
        private Vector3 lastRightPos;
        private Quaternion lastRightRot;
        private bool rightTransformLive;

        private const float TransformMotionPosEpsilonSqr = 1e-8f;
        private const float TransformMotionRotEpsilonDeg = 0.05f;

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

            // Re-evaluate detection now that controller/camera refs are assigned.
            RefreshDeviceLists();
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

#if ENABLE_INPUT_SYSTEM
        private static InputActionProperty PreferFloatAction(InputActionProperty preferred, InputActionProperty fallback)
        {
            return preferred.action != null ? preferred : fallback;
        }
#endif

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

                // Auto-assign left controller input actions if not manually set.
                // Prefer *ActionValue (float axis) over button actions when available.
                if (isLeftController)
                {
                    if (leftTriggerAction.action == null)
                        leftTriggerAction = PreferFloatAction(controller.selectActionValue, controller.selectAction);
                    if (leftGripAction.action == null)
                        leftGripAction = PreferFloatAction(controller.activateActionValue, controller.activateAction);
                    if (leftPrimaryButtonAction.action == null)
                        leftPrimaryButtonAction = controller.uiPressAction;
                    // Note: ActionBasedController doesn't have direct references to all buttons
                    // Secondary button and joystick click would need to be read from the device directly
                }

                // Auto-assign right controller input actions if not manually set
                if (isRightController)
                {
                    if (rightTriggerAction.action == null)
                        rightTriggerAction = PreferFloatAction(controller.selectActionValue, controller.selectAction);
                    if (rightGripAction.action == null)
                        rightGripAction = PreferFloatAction(controller.activateActionValue, controller.activateAction);
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

            // Refresh presence each frame while logging so WebXR Enter-VR / device
            // attachment is picked up immediately (InputDevices + transform fallbacks).
            RefreshDeviceLists();

            // Log data every frame for maximum fidelity
            if (logEveryFrame)
            {
                LogBaselineData();
            }
        }

        private void RefreshDeviceLists()
        {
            leftHandDevices.Clear();
            rightHandDevices.Clear();
            headDevices.Clear();

            InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftHandDevices);
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightHandDevices);
            InputDevices.GetDevicesAtXRNode(XRNode.Head, headDevices);

            // Transform motion fallback is WebXR-oriented: Tracked Pose Driver can move
            // scene transforms without InputDevices. On editor/native XR, rely on device APIs.
#if UNITY_WEBGL && !UNITY_EDITOR
            bool displayRunning = IsXrDisplayRunning() || XRSettings.isDeviceActive;
            UpdateTransformLiveTracking(
                headsetCamera != null ? headsetCamera.transform : null,
                displayRunning,
                ref hasLastHeadsetPose, ref lastHeadsetPos, ref lastHeadsetRot, ref headsetTransformLive);
            UpdateTransformLiveTracking(
                leftController,
                displayRunning,
                ref hasLastLeftPose, ref lastLeftPos, ref lastLeftRot, ref leftTransformLive);
            UpdateTransformLiveTracking(
                rightController,
                displayRunning,
                ref hasLastRightPose, ref lastRightPos, ref lastRightRot, ref rightTransformLive);
#else
            headsetTransformLive = false;
            leftTransformLive = false;
            rightTransformLive = false;
            hasLastHeadsetPose = false;
            hasLastLeftPose = false;
            hasLastRightPose = false;
#endif

            headsetDetected =
                AnyInputDeviceTracked(headDevices)
                || HasTrackedNodeState(XRNode.Head)
                || IsInputSystemNodeTracked(XRNode.Head)
                || headsetTransformLive;

            leftControllerDetected =
                AnyInputDeviceTracked(leftHandDevices)
                || HasTrackedNodeState(XRNode.LeftHand)
                || IsInputSystemNodeTracked(XRNode.LeftHand)
                || leftTransformLive;

            rightControllerDetected =
                AnyInputDeviceTracked(rightHandDevices)
                || HasTrackedNodeState(XRNode.RightHand)
                || IsInputSystemNodeTracked(XRNode.RightHand)
                || rightTransformLive;
        }

        /// <summary>
        /// True only when an XR display subsystem is actively running (immersive session).
        /// </summary>
        private bool IsXrDisplayRunning()
        {
            xrDisplaySubsystems.Clear();
            SubsystemManager.GetSubsystems(xrDisplaySubsystems);
            for (int i = 0; i < xrDisplaySubsystems.Count; i++)
            {
                if (xrDisplaySubsystems[i] != null && xrDisplaySubsystems[i].running)
                    return true;
            }
            return false;
        }

        private static bool AnyInputDeviceTracked(List<UnityEngine.XR.InputDevice> devices)
        {
            if (devices == null || devices.Count == 0)
                return false;

            bool sawUnsupportedIsTracked = false;
            for (int i = 0; i < devices.Count; i++)
            {
                UnityEngine.XR.InputDevice device = devices[i];
                if (!device.isValid)
                    continue;

                if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool isTracked))
                {
                    if (isTracked)
                        return true;
                }
                else
                {
                    // Provider does not expose isTracked — preserve legacy "device present" behavior.
                    sawUnsupportedIsTracked = true;
                }
            }

            return sawUnsupportedIsTracked;
        }

        private bool HasTrackedNodeState(XRNode node)
        {
            xrNodeStates.Clear();
            InputTracking.GetNodeStates(xrNodeStates);
            for (int i = 0; i < xrNodeStates.Count; i++)
            {
                XRNodeState state = xrNodeStates[i];
                if (state.nodeType == node && state.tracked)
                    return true;
            }
            return false;
        }

        private static void UpdateTransformLiveTracking(
            Transform t,
            bool displayRunning,
            ref bool hasLastPose,
            ref Vector3 lastPos,
            ref Quaternion lastRot,
            ref bool transformLive)
        {
            if (t == null || !displayRunning)
            {
                hasLastPose = false;
                transformLive = false;
                return;
            }

            Vector3 pos = t.position;
            Quaternion rot = t.rotation;

            if (hasLastPose)
            {
                float posDeltaSqr = (pos - lastPos).sqrMagnitude;
                float rotDeltaDeg = Quaternion.Angle(rot, lastRot);
                if (posDeltaSqr > TransformMotionPosEpsilonSqr || rotDeltaDeg > TransformMotionRotEpsilonDeg)
                {
                    // Sticky while the XR display keeps running so held-still poses still count.
                    transformLive = true;
                }
            }

            lastPos = pos;
            lastRot = rot;
            hasLastPose = true;
        }

        private bool IsInputSystemNodeTracked(XRNode node)
        {
#if ENABLE_INPUT_SYSTEM
            TrackedDevice tracked = FindTrackedDeviceForNode(node);
            if (tracked == null || !tracked.added)
                return false;

            // isTracked is the reliable signal; do not treat an added-but-untracked device as present.
            return tracked.isTracked.isPressed || tracked.isTracked.ReadValue() > 0.5f;
#else
            return false;
#endif
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

            // Presence uses the broadened detection from RefreshDeviceLists (InputDevices,
            // XRNodeState, or scene transform while XR is active). Virtual pose always
            // comes from scene transforms when present — do not gate on InputDevices alone
            // (WebXR commonly drives Tracked Pose Driver transforms without InputDevices).

            // --- Headset ---
            bool isHeadsetPresent = headsetDetected;
            data.headsetDetected = isHeadsetPresent;

            if (isHeadsetPresent && headsetCamera != null)
            {
                PopulateVirtualPose(headsetCamera.transform,
                    out data.headsetVirtualPosX, out data.headsetVirtualPosY, out data.headsetVirtualPosZ,
                    out data.headsetVirtualRotEulerX, out data.headsetVirtualRotEulerY, out data.headsetVirtualRotEulerZ,
                    out data.headsetVirtualRotQuatX, out data.headsetVirtualRotQuatY, out data.headsetVirtualRotQuatZ, out data.headsetVirtualRotQuatW);
            }

            PopulateTrackingPose(XRNode.Head, headDevices,
                out data.headsetTrackingPosX, out data.headsetTrackingPosY, out data.headsetTrackingPosZ,
                out data.headsetTrackingRotEulerX, out data.headsetTrackingRotEulerY, out data.headsetTrackingRotEulerZ,
                out data.headsetTrackingRotQuatX, out data.headsetTrackingRotQuatY, out data.headsetTrackingRotQuatZ, out data.headsetTrackingRotQuatW);

            // --- Left controller ---
            bool isLeftPresent = leftControllerDetected;
            data.leftDetected = isLeftPresent;

            if (isLeftPresent && leftController != null)
            {
                PopulateVirtualPose(leftController,
                    out data.leftControllerVirtualPosX, out data.leftControllerVirtualPosY, out data.leftControllerVirtualPosZ,
                    out data.leftControllerVirtualRotEulerX, out data.leftControllerVirtualRotEulerY, out data.leftControllerVirtualRotEulerZ,
                    out data.leftControllerVirtualRotQuatX, out data.leftControllerVirtualRotQuatY, out data.leftControllerVirtualRotQuatZ, out data.leftControllerVirtualRotQuatW);
            }

            PopulateTrackingPose(XRNode.LeftHand, leftHandDevices,
                out data.leftControllerTrackingPosX, out data.leftControllerTrackingPosY, out data.leftControllerTrackingPosZ,
                out data.leftControllerTrackingRotEulerX, out data.leftControllerTrackingRotEulerY, out data.leftControllerTrackingRotEulerZ,
                out data.leftControllerTrackingRotQuatX, out data.leftControllerTrackingRotQuatY, out data.leftControllerTrackingRotQuatZ, out data.leftControllerTrackingRotQuatW);

            // Left controller inputs: Input Actions -> InputDevices -> Input System XR controls
#if ENABLE_INPUT_SYSTEM
            data.leftTrigger = ReadFloatControllerInput(
                leftTriggerAction, leftHandDevices, UnityEngine.XR.CommonUsages.trigger, XRNode.LeftHand,
                "trigger", "triggerButton");
            data.leftGrip = ReadFloatControllerInput(
                leftGripAction, leftHandDevices, UnityEngine.XR.CommonUsages.grip, XRNode.LeftHand,
                "grip", "gripButton", "gripPressed");
            data.leftPrimaryButton = ReadButtonControllerInput(
                leftPrimaryButtonAction, leftHandDevices, UnityEngine.XR.CommonUsages.primaryButton, XRNode.LeftHand,
                "primaryButton", "primaryPress");
            data.leftSecondaryButton = ReadButtonControllerInput(
                leftSecondaryButtonAction, leftHandDevices, UnityEngine.XR.CommonUsages.secondaryButton, XRNode.LeftHand,
                "secondaryButton", "secondaryPress");
            data.leftPrimary2DAxisClick = ReadButtonControllerInput(
                leftPrimary2DAxisClickAction, leftHandDevices, UnityEngine.XR.CommonUsages.primary2DAxisClick, XRNode.LeftHand,
                "thumbstickClicked", "joystickClicked", "primary2DAxisClick");
            Vector2 leftAxis = ReadVector2ControllerInput(
                leftThumbstickAction, leftHandDevices, UnityEngine.XR.CommonUsages.primary2DAxis, XRNode.LeftHand,
                "thumbstick", "joystick", "primary2DAxis");
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
            bool isRightPresent = rightControllerDetected;
            data.rightDetected = isRightPresent;

            if (isRightPresent && rightController != null)
            {
                PopulateVirtualPose(rightController,
                    out data.rightControllerVirtualPosX, out data.rightControllerVirtualPosY, out data.rightControllerVirtualPosZ,
                    out data.rightControllerVirtualRotEulerX, out data.rightControllerVirtualRotEulerY, out data.rightControllerVirtualRotEulerZ,
                    out data.rightControllerVirtualRotQuatX, out data.rightControllerVirtualRotQuatY, out data.rightControllerVirtualRotQuatZ, out data.rightControllerVirtualRotQuatW);
            }

            PopulateTrackingPose(XRNode.RightHand, rightHandDevices,
                out data.rightControllerTrackingPosX, out data.rightControllerTrackingPosY, out data.rightControllerTrackingPosZ,
                out data.rightControllerTrackingRotEulerX, out data.rightControllerTrackingRotEulerY, out data.rightControllerTrackingRotEulerZ,
                out data.rightControllerTrackingRotQuatX, out data.rightControllerTrackingRotQuatY, out data.rightControllerTrackingRotQuatZ, out data.rightControllerTrackingRotQuatW);

            // Right controller inputs: Input Actions -> InputDevices -> Input System XR controls
#if ENABLE_INPUT_SYSTEM
            data.rightTrigger = ReadFloatControllerInput(
                rightTriggerAction, rightHandDevices, UnityEngine.XR.CommonUsages.trigger, XRNode.RightHand,
                "trigger", "triggerButton");
            data.rightGrip = ReadFloatControllerInput(
                rightGripAction, rightHandDevices, UnityEngine.XR.CommonUsages.grip, XRNode.RightHand,
                "grip", "gripButton", "gripPressed");
            data.rightPrimaryButton = ReadButtonControllerInput(
                rightPrimaryButtonAction, rightHandDevices, UnityEngine.XR.CommonUsages.primaryButton, XRNode.RightHand,
                "primaryButton", "primaryPress");
            data.rightSecondaryButton = ReadButtonControllerInput(
                rightSecondaryButtonAction, rightHandDevices, UnityEngine.XR.CommonUsages.secondaryButton, XRNode.RightHand,
                "secondaryButton", "secondaryPress");
            data.rightPrimary2DAxisClick = ReadButtonControllerInput(
                rightPrimary2DAxisClickAction, rightHandDevices, UnityEngine.XR.CommonUsages.primary2DAxisClick, XRNode.RightHand,
                "thumbstickClicked", "joystickClicked", "primary2DAxisClick");
            Vector2 rightAxis = ReadVector2ControllerInput(
                rightThumbstickAction, rightHandDevices, UnityEngine.XR.CommonUsages.primary2DAxis, XRNode.RightHand,
                "thumbstick", "joystick", "primary2DAxis");
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
            XRNode node,
            List<UnityEngine.XR.InputDevice> devices,
            out float posX, out float posY, out float posZ,
            out float eulerX, out float eulerY, out float eulerZ,
            out float quatX, out float quatY, out float quatZ, out float quatW)
        {
            posX = posY = posZ = 0f;
            eulerX = eulerY = eulerZ = 0f;
            quatX = quatY = quatZ = 0f; quatW = 1f;

            // Prefer legacy InputDevices when available (editor / native XR).
            foreach (var device in devices)
            {
                if (!device.isValid)
                    continue;

                // Skip explicitly untracked devices so default/identity poses do not win.
                if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool isTracked) && !isTracked)
                    continue;

                bool gotPos = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 pos);
                bool gotRot = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion q);
                if (gotPos)
                {
                    posX = pos.x; posY = pos.y; posZ = pos.z;
                }
                if (gotRot)
                {
                    Vector3 euler = q.eulerAngles;
                    eulerX = euler.x; eulerY = euler.y; eulerZ = euler.z;
                    quatX = q.x; quatY = q.y; quatZ = q.z; quatW = q.w;
                }
                if (gotPos || gotRot)
                    return;
            }

            // Fallback: InputTracking node states (sometimes populated when InputDevices is not).
            if (TryPopulateTrackingPoseFromNodeState(node,
                out posX, out posY, out posZ,
                out eulerX, out eulerY, out eulerZ,
                out quatX, out quatY, out quatZ, out quatW))
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            // Fallback: Input System XR controller / HMD devices.
            TryPopulateTrackingPoseFromInputSystem(node,
                out posX, out posY, out posZ,
                out eulerX, out eulerY, out eulerZ,
                out quatX, out quatY, out quatZ, out quatW);
#endif
        }

        private bool TryPopulateTrackingPoseFromNodeState(
            XRNode node,
            out float posX, out float posY, out float posZ,
            out float eulerX, out float eulerY, out float eulerZ,
            out float quatX, out float quatY, out float quatZ, out float quatW)
        {
            posX = posY = posZ = 0f;
            eulerX = eulerY = eulerZ = 0f;
            quatX = quatY = quatZ = 0f; quatW = 1f;

            xrNodeStates.Clear();
            InputTracking.GetNodeStates(xrNodeStates);
            for (int i = 0; i < xrNodeStates.Count; i++)
            {
                XRNodeState state = xrNodeStates[i];
                if (state.nodeType != node || !state.tracked)
                    continue;

                bool gotPos = state.TryGetPosition(out Vector3 pos);
                bool gotRot = state.TryGetRotation(out Quaternion q);
                if (!gotPos && !gotRot)
                    continue;

                if (gotPos)
                {
                    posX = pos.x; posY = pos.y; posZ = pos.z;
                }
                if (gotRot)
                {
                    Vector3 euler = q.eulerAngles;
                    eulerX = euler.x; eulerY = euler.y; eulerZ = euler.z;
                    quatX = q.x; quatY = q.y; quatZ = q.z; quatW = q.w;
                }
                return true;
            }

            return false;
        }

#if ENABLE_INPUT_SYSTEM
        private bool TryPopulateTrackingPoseFromInputSystem(
            XRNode node,
            out float posX, out float posY, out float posZ,
            out float eulerX, out float eulerY, out float eulerZ,
            out float quatX, out float quatY, out float quatZ, out float quatW)
        {
            posX = posY = posZ = 0f;
            eulerX = eulerY = eulerZ = 0f;
            quatX = quatY = quatZ = 0f; quatW = 1f;

            TrackedDevice tracked = FindTrackedDeviceForNode(node);
            if (tracked == null || !tracked.added)
                return false;
            if (!(tracked.isTracked.isPressed || tracked.isTracked.ReadValue() > 0.5f))
                return false;

            Vector3 pos = tracked.devicePosition.ReadValue();
            Quaternion q = tracked.deviceRotation.ReadValue();
            Vector3 euler = q.eulerAngles;
            posX = pos.x; posY = pos.y; posZ = pos.z;
            eulerX = euler.x; eulerY = euler.y; eulerZ = euler.z;
            quatX = q.x; quatY = q.y; quatZ = q.z; quatW = q.w;
            return true;
        }

        private static TrackedDevice FindTrackedDeviceForNode(XRNode node)
        {
            switch (node)
            {
                case XRNode.LeftHand:
                    return UnityEngine.InputSystem.XR.XRController.leftHand;
                case XRNode.RightHand:
                    return UnityEngine.InputSystem.XR.XRController.rightHand;
                case XRNode.Head:
                    // XRHMD.current is not available on all Input System versions.
                    return FindFirstInputSystemDevice<XRHMD>();
                default:
                    return null;
            }
        }

        private static TDevice FindFirstInputSystemDevice<TDevice>()
            where TDevice : UnityEngine.InputSystem.InputDevice
        {
            foreach (UnityEngine.InputSystem.InputDevice device in InputSystem.devices)
            {
                if (device is TDevice typed && typed.added)
                    return typed;
            }
            return null;
        }
#endif

#if ENABLE_INPUT_SYSTEM
        private float ReadFloatControllerInput(
            InputActionProperty actionProperty,
            List<UnityEngine.XR.InputDevice> devices,
            InputFeatureUsage<float> deviceUsage,
            XRNode node,
            params string[] inputSystemControlNames)
        {
            float fromAction = GetFloatInputState(actionProperty);
            if (fromAction >= 0f)
                return fromAction;

            float fromDevice = GetFloatInputStateFromDevice(devices, deviceUsage);
            if (fromDevice >= 0f)
                return fromDevice;

            if (TryReadFloatFromInputSystemController(node, out float fromXr, inputSystemControlNames))
                return fromXr;

            // Controller is present but this axis is unavailable — log idle 0, not NA.
            return IsControllerNodePresent(node) ? 0f : -1f;
        }

        private int ReadButtonControllerInput(
            InputActionProperty actionProperty,
            List<UnityEngine.XR.InputDevice> devices,
            InputFeatureUsage<bool> deviceUsage,
            XRNode node,
            params string[] inputSystemControlNames)
        {
            int fromAction = GetInputState(actionProperty);
            if (fromAction >= 0)
                return fromAction;

            int fromDevice = GetInputStateFromDevice(devices, deviceUsage);
            if (fromDevice >= 0)
                return fromDevice;

            if (TryReadFloatFromInputSystemController(node, out float fromXr, inputSystemControlNames))
                return fromXr > 0.5f ? 1 : 0;

            return IsControllerNodePresent(node) ? 0 : -1;
        }

        private Vector2 ReadVector2ControllerInput(
            InputActionProperty actionProperty,
            List<UnityEngine.XR.InputDevice> devices,
            InputFeatureUsage<Vector2> deviceUsage,
            XRNode node,
            params string[] inputSystemControlNames)
        {
            Vector2 fromAction = GetVector2InputState(actionProperty);
            if (fromAction.x > -1.5f && fromAction.y > -1.5f)
                return fromAction;

            Vector2 fromDevice = GetVector2InputStateFromDevice(devices, deviceUsage);
            if (fromDevice.x > -1.5f && fromDevice.y > -1.5f)
                return fromDevice;

            if (TryReadVector2FromInputSystemController(node, out Vector2 fromXr, inputSystemControlNames))
                return fromXr;

            return IsControllerNodePresent(node) ? Vector2.zero : new Vector2(-2f, -2f);
        }

        private bool IsControllerNodePresent(XRNode node)
        {
            switch (node)
            {
                case XRNode.LeftHand: return leftControllerDetected;
                case XRNode.RightHand: return rightControllerDetected;
                case XRNode.Head: return headsetDetected;
                default: return false;
            }
        }

        private bool TryReadFloatFromInputSystemController(XRNode node, out float value, params string[] controlNames)
        {
            value = 0f;
            TrackedDevice tracked = FindTrackedDeviceForNode(node);
            if (tracked == null || !tracked.added)
                return false;

            for (int i = 0; i < controlNames.Length; i++)
            {
                InputControl control = tracked.TryGetChildControl(controlNames[i]);
                if (control == null)
                    continue;

                if (control is AxisControl axis)
                {
                    value = axis.ReadValue();
                    return true;
                }
                if (control is ButtonControl button)
                {
                    value = button.ReadValue();
                    return true;
                }
            }

            return false;
        }

        private bool TryReadVector2FromInputSystemController(XRNode node, out Vector2 value, params string[] controlNames)
        {
            value = Vector2.zero;
            TrackedDevice tracked = FindTrackedDeviceForNode(node);
            if (tracked == null || !tracked.added)
                return false;

            for (int i = 0; i < controlNames.Length; i++)
            {
                InputControl control = tracked.TryGetChildControl(controlNames[i]);
                if (control is Vector2Control stick)
                {
                    value = stick.ReadValue();
                    return true;
                }
            }

            return false;
        }
#endif

        private float GetFloatInputState(InputActionProperty actionProperty)
        {
#if ENABLE_INPUT_SYSTEM
            InputAction action = actionProperty.action;
            if (action == null)
                return -1f; // NA when unknown

            try
            {
                if (!action.enabled)
                    action.Enable();

                // Do not require activeControl — it is null when the control is at rest,
                // which previously made every sample look like "no input" (-1).
                return action.ReadValue<float>();
            }
            catch
            {
                try
                {
                    return action.IsPressed() ? 1f : 0f;
                }
                catch
                {
                    return -1f; // NA when error occurs
                }
            }
#else
            return -1f;
#endif
        }

        private int GetInputState(InputActionProperty actionProperty)
        {
#if ENABLE_INPUT_SYSTEM
            InputAction action = actionProperty.action;
            if (action == null)
                return -1; // NA when unknown

            try
            {
                if (!action.enabled)
                    action.Enable();

                if (action.IsPressed())
                    return 1;

                try
                {
                    return action.ReadValue<float>() > 0.5f ? 1 : 0;
                }
                catch
                {
                    return 0;
                }
            }
            catch
            {
                return -1; // NA when error occurs
            }
#else
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
            InputAction action = actionProperty.action;
            if (action == null)
                return new Vector2(-2f, -2f); // NA when unknown

            try
            {
                if (!action.enabled)
                    action.Enable();
                return action.ReadValue<Vector2>();
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


