using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VERA
{
    /// Helper class to create and configure the VERAColumnDefinition for baseline data logging.
    /// This ensures the baseline data CSV has the correct column structure.
    internal static class VERABaselineDataColumnDefinition
    {
        /// Creates a VERAColumnDefinition asset configured for baseline data logging
        public static VERAColumnDefinition CreateBaselineDataColumnDefinition()
        {
            var columnDefinition = ScriptableObject.CreateInstance<VERAColumnDefinition>();

            // Set up file type
            columnDefinition.fileType = new VERAColumnDefinition.FileType
            {
                fileTypeId = "baseline-data",
                name = "Experiment_Telemetry",
                description = "Baseline VR tracking and input data"
            };

            // Create columns according to the specification
            columnDefinition.columns = new List<VERAColumnDefinition.Column>
            {
                // Auto-populated columns (these are handled by VERA automatically)
                new VERAColumnDefinition.Column
                {
                    name = "pID",
                    description = "Participant ID",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "conditions",
                    description = "Current experiment conditions",
                    type = VERAColumnDefinition.DataType.String
                },
                new VERAColumnDefinition.Column
                {
                    name = "ts",
                    description = "Time when data was recorded",
                    type = VERAColumnDefinition.DataType.Date
                },
                
                // VR tracking and input data columns
                new VERAColumnDefinition.Column
                {
                    name = "headsetDetected",
                    description = "Whether VR headset is connected and tracking",
                    type = VERAColumnDefinition.DataType.Boolean
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetPosX",
                    description = "VR headset horizontal position (left-right)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetPosY",
                    description = "VR headset vertical position (up-down)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetPosZ",
                    description = "VR headset depth position (forward-back)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "headsetRot",
                    description = "VR headset rotation (yaw, pitch, roll)",
                    type = VERAColumnDefinition.DataType.String
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftDetected",
                    description = "Whether left hand controller is connected and tracking",
                    type = VERAColumnDefinition.DataType.Boolean
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerPosX",
                    description = "Left hand controller horizontal position (left-right)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerPosY",
                    description = "Left hand controller vertical position (up-down)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerPosZ",
                    description = "Left hand controller depth position (forward-back)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftControllerRot",
                    description = "Left hand controller rotation (yaw, pitch, roll)",
                    type = VERAColumnDefinition.DataType.String
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftTrigger",
                    description = "Left controller trigger pressure (0=not pressed, 1=fully pressed)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "leftGrip",
                    description = "Left controller grip pressure (0=not gripped, 1=fully gripped)",
                    type = VERAColumnDefinition.DataType.Number
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
                    name = "rightDetected",
                    description = "Whether right hand controller is connected and tracking",
                    type = VERAColumnDefinition.DataType.Boolean
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerPosX",
                    description = "Right hand controller horizontal position (left-right)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerPosY",
                    description = "Right hand controller vertical position (up-down)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerPosZ",
                    description = "Right hand controller depth position (forward-back)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightControllerRot",
                    description = "Right hand controller rotation (yaw, pitch, roll)",
                    type = VERAColumnDefinition.DataType.String
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightTrigger",
                    description = "Right controller trigger pressure (0=not pressed, 1=fully pressed)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "rightGrip",
                    description = "Right controller grip pressure (0=not gripped, 1=fully gripped)",
                    type = VERAColumnDefinition.DataType.Number
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
                }
            };

            return columnDefinition;
        }

#if UNITY_EDITOR
        public static void CreateAndSaveBaselineDataColumnDefinition()
        {
            var columnDef = CreateBaselineDataColumnDefinition();

            // Save to Resources folder so it can be loaded by VERALogger
            string resourcesPath = "Assets/VERA/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesPath))
            {
                AssetDatabase.CreateFolder("Assets/VERA", "Resources");
            }

            string assetPath = $"{resourcesPath}/Experiment_TelemetryColumnDefinition.asset";
            AssetDatabase.CreateAsset(columnDef, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Create column definition (removed debug logging to keep console clean)
            Selection.activeObject = columnDef;
        }
#endif
    }
}