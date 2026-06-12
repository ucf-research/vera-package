using System.Collections.Generic;

namespace VERA
{
    /// Static schema definition for the Experiment_Telemetry baseline data file type.
    /// This is the single source of truth for column structure and the telemetry upload ID.
    internal static class VERAExperimentTelemetrySchema
    {
        /// The file type ID used to identify and upload this telemetry to the VERA server.
        public const string FileTypeId = "Experiment_Telemetry";
        public const string Name = "Experiment_Telemetry";
        public const string Description = "Baseline VR tracking and input data";

        public static readonly IReadOnlyList<VERAColumnDefinition.Column> Columns =
            new List<VERAColumnDefinition.Column>
            {
                // --- Auto-populated columns (handled by VERA automatically) ---
                new VERAColumnDefinition.Column
                {
                    name = "pID",
                    description = "Participant ID (auto-added by VERA)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "conditions",
                    description = "Experiment conditions (auto-added by VERA)",
                    type = VERAColumnDefinition.DataType.String
                },
                new VERAColumnDefinition.Column
                {
                    name = "ts",
                    description = "Time since the event occurred (auto-added by VERA)",
                    type = VERAColumnDefinition.DataType.Float
                },

                // --- Headset ---
                new VERAColumnDefinition.Column
                {
                    name = "headsetDetected",
                    description = "Whether VR headset is connected and tracking",
                    type = VERAColumnDefinition.DataType.Boolean
                },

                // Headset virtual pose (position/rotation in the virtual environment)
                new VERAColumnDefinition.Column
                {
                    name = "headsetVirtualPosX",
                    description = "VR headset virtual horizontal position in the virtual environment (left-right)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetVirtualPosY",
                    description = "VR headset virtual vertical position in the virtual environment (up-down)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetVirtualPosZ",
                    description = "VR headset virtual depth position in the virtual environment (forward-back)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetVirtualRotEulerX",
                    description = "VR headset virtual rotation Euler X angle in the virtual environment (pitch)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetVirtualRotEulerY",
                    description = "VR headset virtual rotation Euler Y angle in the virtual environment (yaw)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetVirtualRotEulerZ",
                    description = "VR headset virtual rotation Euler Z angle in the virtual environment (roll)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetVirtualRotQuatX",
                    description = "VR headset virtual rotation quaternion X component in the virtual environment",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetVirtualRotQuatY",
                    description = "VR headset virtual rotation quaternion Y component in the virtual environment",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetVirtualRotQuatZ",
                    description = "VR headset virtual rotation quaternion Z component in the virtual environment",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetVirtualRotQuatW",
                    description = "VR headset virtual rotation quaternion W component in the virtual environment",
                    type = VERAColumnDefinition.DataType.Float
                },

                // Headset tracking pose (real recorded position/rotation in physical space)
                new VERAColumnDefinition.Column
                {
                    name = "headsetTrackingPosX",
                    description = "VR headset real tracked horizontal position in physical space (left-right)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetTrackingPosY",
                    description = "VR headset real tracked vertical position in physical space (up-down)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetTrackingPosZ",
                    description = "VR headset real tracked depth position in physical space (forward-back)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetTrackingRotEulerX",
                    description = "VR headset real tracked rotation Euler X angle in physical space (pitch)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetTrackingRotEulerY",
                    description = "VR headset real tracked rotation Euler Y angle in physical space (yaw)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetTrackingRotEulerZ",
                    description = "VR headset real tracked rotation Euler Z angle in physical space (roll)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetTrackingRotQuatX",
                    description = "VR headset real tracked rotation quaternion X component in physical space",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetTrackingRotQuatY",
                    description = "VR headset real tracked rotation quaternion Y component in physical space",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetTrackingRotQuatZ",
                    description = "VR headset real tracked rotation quaternion Z component in physical space",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetTrackingRotQuatW",
                    description = "VR headset real tracked rotation quaternion W component in physical space",
                    type = VERAColumnDefinition.DataType.Float
                },

                // --- Left controller ---
                new VERAColumnDefinition.Column
                {
                    name = "leftDetected",
                    description = "Whether left hand controller is connected and tracking",
                    type = VERAColumnDefinition.DataType.Boolean
                },

                // Left controller virtual pose (position/rotation in the virtual environment)
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerVirtualPosX",
                    description = "Left hand controller virtual horizontal position in the virtual environment (left-right)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerVirtualPosY",
                    description = "Left hand controller virtual vertical position in the virtual environment (up-down)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerVirtualPosZ",
                    description = "Left hand controller virtual depth position in the virtual environment (forward-back)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerVirtualRotEulerX",
                    description = "Left hand controller virtual rotation Euler X angle in the virtual environment (pitch)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerVirtualRotEulerY",
                    description = "Left hand controller virtual rotation Euler Y angle in the virtual environment (yaw)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerVirtualRotEulerZ",
                    description = "Left hand controller virtual rotation Euler Z angle in the virtual environment (roll)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerVirtualRotQuatX",
                    description = "Left hand controller virtual rotation quaternion X component in the virtual environment",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerVirtualRotQuatY",
                    description = "Left hand controller virtual rotation quaternion Y component in the virtual environment",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerVirtualRotQuatZ",
                    description = "Left hand controller virtual rotation quaternion Z component in the virtual environment",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerVirtualRotQuatW",
                    description = "Left hand controller virtual rotation quaternion W component in the virtual environment",
                    type = VERAColumnDefinition.DataType.Float
                },

                // Left controller tracking pose (real recorded position/rotation in physical space)
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerTrackingPosX",
                    description = "Left hand controller real tracked horizontal position in physical space (left-right)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerTrackingPosY",
                    description = "Left hand controller real tracked vertical position in physical space (up-down)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerTrackingPosZ",
                    description = "Left hand controller real tracked depth position in physical space (forward-back)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerTrackingRotEulerX",
                    description = "Left hand controller real tracked rotation Euler X angle in physical space (pitch)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerTrackingRotEulerY",
                    description = "Left hand controller real tracked rotation Euler Y angle in physical space (yaw)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerTrackingRotEulerZ",
                    description = "Left hand controller real tracked rotation Euler Z angle in physical space (roll)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerTrackingRotQuatX",
                    description = "Left hand controller real tracked rotation quaternion X component in physical space",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerTrackingRotQuatY",
                    description = "Left hand controller real tracked rotation quaternion Y component in physical space",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerTrackingRotQuatZ",
                    description = "Left hand controller real tracked rotation quaternion Z component in physical space",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerTrackingRotQuatW",
                    description = "Left hand controller real tracked rotation quaternion W component in physical space",
                    type = VERAColumnDefinition.DataType.Float
                },

                // Left controller input
                new VERAColumnDefinition.Column
                {
                    name = "leftTrigger",
                    description = "Left controller trigger pressure (0=not pressed, 1=fully pressed)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftGrip",
                    description = "Left controller grip pressure (0=not gripped, 1=fully gripped)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftPrimaryButton",
                    description = "Left controller main button (1=pressed, 0=not pressed)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftSecondaryButton",
                    description = "Left controller secondary button (1=pressed, 0=not pressed)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftPrimary2DAxisClick",
                    description = "Left controller thumbstick click (1=clicked, 0=not clicked)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftThumbstickX",
                    description = "Left controller thumbstick horizontal axis (-1=left, 1=right)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftThumbstickY",
                    description = "Left controller thumbstick vertical axis (-1=down, 1=up)",
                    type = VERAColumnDefinition.DataType.Float
                },

                // --- Right controller ---
                new VERAColumnDefinition.Column
                {
                    name = "rightDetected",
                    description = "Whether right hand controller is connected and tracking",
                    type = VERAColumnDefinition.DataType.Boolean
                },

                // Right controller virtual pose (position/rotation in the virtual environment)
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerVirtualPosX",
                    description = "Right hand controller virtual horizontal position in the virtual environment (left-right)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerVirtualPosY",
                    description = "Right hand controller virtual vertical position in the virtual environment (up-down)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerVirtualPosZ",
                    description = "Right hand controller virtual depth position in the virtual environment (forward-back)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerVirtualRotEulerX",
                    description = "Right hand controller virtual rotation Euler X angle in the virtual environment (pitch)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerVirtualRotEulerY",
                    description = "Right hand controller virtual rotation Euler Y angle in the virtual environment (yaw)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerVirtualRotEulerZ",
                    description = "Right hand controller virtual rotation Euler Z angle in the virtual environment (roll)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerVirtualRotQuatX",
                    description = "Right hand controller virtual rotation quaternion X component in the virtual environment",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerVirtualRotQuatY",
                    description = "Right hand controller virtual rotation quaternion Y component in the virtual environment",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerVirtualRotQuatZ",
                    description = "Right hand controller virtual rotation quaternion Z component in the virtual environment",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerVirtualRotQuatW",
                    description = "Right hand controller virtual rotation quaternion W component in the virtual environment",
                    type = VERAColumnDefinition.DataType.Float
                },

                // Right controller tracking pose (real recorded position/rotation in physical space)
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerTrackingPosX",
                    description = "Right hand controller real tracked horizontal position in physical space (left-right)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerTrackingPosY",
                    description = "Right hand controller real tracked vertical position in physical space (up-down)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerTrackingPosZ",
                    description = "Right hand controller real tracked depth position in physical space (forward-back)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerTrackingRotEulerX",
                    description = "Right hand controller real tracked rotation Euler X angle in physical space (pitch)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerTrackingRotEulerY",
                    description = "Right hand controller real tracked rotation Euler Y angle in physical space (yaw)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerTrackingRotEulerZ",
                    description = "Right hand controller real tracked rotation Euler Z angle in physical space (roll)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerTrackingRotQuatX",
                    description = "Right hand controller real tracked rotation quaternion X component in physical space",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerTrackingRotQuatY",
                    description = "Right hand controller real tracked rotation quaternion Y component in physical space",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerTrackingRotQuatZ",
                    description = "Right hand controller real tracked rotation quaternion Z component in physical space",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerTrackingRotQuatW",
                    description = "Right hand controller real tracked rotation quaternion W component in physical space",
                    type = VERAColumnDefinition.DataType.Float
                },

                // Right controller input
                new VERAColumnDefinition.Column
                {
                    name = "rightTrigger",
                    description = "Right controller trigger pressure (0=not pressed, 1=fully pressed)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightGrip",
                    description = "Right controller grip pressure (0=not gripped, 1=fully gripped)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightPrimaryButton",
                    description = "Right controller main button (1=pressed, 0=not pressed)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightSecondaryButton",
                    description = "Right controller secondary button (1=pressed, 0=not pressed)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightPrimary2DAxisClick",
                    description = "Right controller thumbstick click (1=clicked, 0=not clicked)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightThumbstickX",
                    description = "Right controller thumbstick horizontal axis (-1=left, 1=right)",
                    type = VERAColumnDefinition.DataType.Float
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightThumbstickY",
                    description = "Right controller thumbstick vertical axis (-1=down, 1=up)",
                    type = VERAColumnDefinition.DataType.Float
                }
            };
    }
}
