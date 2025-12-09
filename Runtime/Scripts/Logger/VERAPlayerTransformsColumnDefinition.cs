using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VERA
{
    /// Helper class to create a proper column definition for the PlayerTransforms file type.
    /// This fixes the column count mismatch issue with VERADemoTransformsLogger.
    internal static class VERAPlayerTransformsColumnDefinition
    {
        /// Creates a VERA column definition for player transform data logging.
        /// This includes the standard VERA columns plus a transform data column.
        /// <returns>VERAColumnDefinition for PlayerTransforms</returns>
        public static VERAColumnDefinition CreatePlayerTransformsColumnDefinition()
        {
            VERAColumnDefinition columnDef = ScriptableObject.CreateInstance<VERAColumnDefinition>();
            
            // Create file type
            columnDef.fileType = new VERAColumnDefinition.FileType();
            columnDef.fileType.name = "PlayerTransforms";
            columnDef.fileType.fileTypeId = "player-transforms";
            
            // Initialize columns list
            columnDef.columns = new List<VERAColumnDefinition.Column>();
            
            // Add standard VERA columns that are automatically handled
            columnDef.columns.Add(new VERAColumnDefinition.Column
            {
                name = "pID",
                type = VERAColumnDefinition.DataType.Number,
                description = "Participant ID"
            });
            
            columnDef.columns.Add(new VERAColumnDefinition.Column
            {
                name = "conditions",
                type = VERAColumnDefinition.DataType.String,
                description = "Experiment conditions"
            });
            
            columnDef.columns.Add(new VERAColumnDefinition.Column
            {
                name = "ts",
                type = VERAColumnDefinition.DataType.String,
                description = "Timestamp"
            });
            
            columnDef.columns.Add(new VERAColumnDefinition.Column
            {
                name = "eventId",
                type = VERAColumnDefinition.DataType.Number,
                description = "Event ID (1=camera, 2=left controller, 3=right controller)"
            });
            
            // Add transform data column
            columnDef.columns.Add(new VERAColumnDefinition.Column
            {
                name = "transform",
                type = VERAColumnDefinition.DataType.Transform,
                description = "Transform data as JSON (position, rotation, localScale)"
            });
            
            return columnDef;
        }
        
#if UNITY_EDITOR
        public static void CreatePlayerTransformsColumnDefinitionAsset()
        {
            var columnDef = CreatePlayerTransformsColumnDefinition();
            
            // Create directory if it doesn't exist
            string assetPath = "Assets/VERA/ColumnDefinitions";
            if (!AssetDatabase.IsValidFolder(assetPath))
            {
                System.IO.Directory.CreateDirectory(assetPath);
                AssetDatabase.Refresh();
            }
            
            // Save as asset
            string fullPath = assetPath + "/PlayerTransforms_ColumnDefinition.asset";
            AssetDatabase.CreateAsset(columnDef, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // Select the created asset
            Selection.activeObject = columnDef;
            EditorGUIUtility.PingObject(columnDef);
            
            Debug.Log($"[VERA] Created PlayerTransforms column definition at: {fullPath}");
            Debug.Log("[VERA] This column definition has 5 columns total (pID, conditions, ts, eventId, transform) which should fix the column count mismatch.");
        }
#endif
    }
}