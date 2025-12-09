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
                name = "BaselineData",
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
                
                // Custom baseline data columns
                new VERAColumnDefinition.Column
                {
                    name = "sampleIndex",
                    description = "Data sample number in recording session",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "headset_detected",
                    description = "Whether VR headset is connected and tracking",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "Headset_Pos_X",
                    description = "VR headset horizontal position (left-right)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "Headset_Pos_Y",
                    description = "VR headset vertical position (up-down)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "Headset_Pos_Z",
                    description = "VR headset depth position (forward-back)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "headset_rot",
                    description = "VR headset orientation (how user's head is rotated)",
                    type = VERAColumnDefinition.DataType.String
                },
                new VERAColumnDefinition.Column
                {
                    name = "left_detected",
                    description = "Whether left hand controller is connected and tracking",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "LeftController_Pos_X",
                    description = "Left hand controller horizontal position (left-right)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "LeftController_Pos_Y",
                    description = "Left hand controller vertical position (up-down)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "LeftController_Pos_Z",
                    description = "Left hand controller depth position (forward-back)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "left_rot",
                    description = "Left hand controller orientation (how controller is rotated)",
                    type = VERAColumnDefinition.DataType.String
                },
                new VERAColumnDefinition.Column
                {
                    name = "left_trigger",
                    description = "Left controller trigger pressure (0=not pressed, 1=fully pressed)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "left_grip",
                    description = "Left controller grip pressure (0=not gripped, 1=fully gripped)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "left_primaryButton",
                    description = "Left controller main button (1=pressed, 0=not pressed)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "left_secondaryButton",
                    description = "Left controller secondary button (1=pressed, 0=not pressed)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "left_primary2DAxisClick",
                    description = "Left controller thumbstick click (1=clicked, 0=not clicked)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "right_detected",
                    description = "Whether right hand controller is connected and tracking",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "RightController_Pos_X",
                    description = "Right hand controller horizontal position (left-right)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "RightController_Pos_Y",
                    description = "Right hand controller vertical position (up-down)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "RightController_Pos_Z",
                    description = "Right hand controller depth position (forward-back)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "right_rot",
                    description = "Right hand controller orientation (how controller is rotated)",
                    type = VERAColumnDefinition.DataType.String
                },
                new VERAColumnDefinition.Column
                {
                    name = "right_trigger",
                    description = "Right controller trigger pressure (0=not pressed, 1=fully pressed)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "right_grip",
                    description = "Right controller grip pressure (0=not gripped, 1=fully gripped)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "right_primaryButton",
                    description = "Right controller main button (1=pressed, 0=not pressed)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "right_secondaryButton",
                    description = "Right controller secondary button (1=pressed, 0=not pressed)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "right_primary2DAxisClick",
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
            string resourcesPath = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesPath))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            
            string assetPath = $"{resourcesPath}/BaselineDataColumnDefinition.asset";
            AssetDatabase.CreateAsset(columnDef, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // Create column definition (removed debug logging to keep console clean)
            Selection.activeObject = columnDef;
        }
#endif
    }
}