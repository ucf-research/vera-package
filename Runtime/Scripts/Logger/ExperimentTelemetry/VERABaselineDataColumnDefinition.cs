using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VERA
{
    /// Bridges VERAExperimentTelemetrySchema to the VERAColumnDefinition ScriptableObject system.
    /// All column data and the upload ID live in VERAExperimentTelemetrySchema.
    internal static class VERABaselineDataColumnDefinition
    {
        /// Creates a VERAColumnDefinition instance populated from the static schema.
        public static VERAColumnDefinition CreateBaselineDataColumnDefinition()
        {
            var columnDefinition = ScriptableObject.CreateInstance<VERAColumnDefinition>();

            columnDefinition.fileType = new VERAColumnDefinition.FileType
            {
                fileTypeId = VERAExperimentTelemetrySchema.FileTypeId,
                name = VERAExperimentTelemetrySchema.Name,
                description = VERAExperimentTelemetrySchema.Description
            };

            columnDefinition.columns = new List<VERAColumnDefinition.Column>(VERAExperimentTelemetrySchema.Columns);

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

            string assetPath = $"{resourcesPath}/{VERAExperimentTelemetrySchema.Name}ColumnDefinition.asset";
            AssetDatabase.CreateAsset(columnDef, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = columnDef;
        }
#endif
    }
}
