using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VERA
{
    internal static class VERAColumnValidator
    {

        public static void ValidateAndFixColumnDefinitions()
        {
            Debug.Log("[VERA Column Validator] Starting column definition validation...");

            // Load all column definitions
            VERAColumnDefinition[] columnDefinitions = Resources.LoadAll<VERAColumnDefinition>("");
            if (columnDefinitions == null || columnDefinitions.Length == 0)
            {
                Debug.Log("[VERA Column Validator] No column definitions found, skipping validation.");
                return;
            }

            bool foundIssues = false;
            List<string> issuesFound = new List<string>();

            foreach (VERAColumnDefinition columnDef in columnDefinitions)
            {
                if (columnDef == null || columnDef.fileType == null)
                    continue;

                string fileTypeName = columnDef.fileType.name;
                bool isBaseline = columnDef.fileType.fileTypeId == "baseline-data" || fileTypeName == "Experiment_Telemetry";

                // Validate that this column definition has the required VERA auto-columns
                if (!ValidateRequiredColumns(columnDef, out string validationError))
                {
                    foundIssues = true;
                    issuesFound.Add($"{fileTypeName}: {validationError}");

                    // Attempt to fix the column definition
                    if (FixColumnDefinition(columnDef))
                    {
                        Debug.Log($"[VERA Column Validator] Fixed column definition for {fileTypeName}");
                    }
                    else
                    {
                        Debug.LogError($"[VERA Column Validator] Failed to fix column definition for {fileTypeName}");
                    }
                }
            }

            if (foundIssues)
            {
                Debug.LogWarning($"[VERA Column Validator] Found and fixed {issuesFound.Count} column definition issues:");
                foreach (string issue in issuesFound)
                {
                    Debug.LogWarning($"  - {issue}");
                }

                // Regenerate code files to match fixed column definitions
                Debug.Log("[VERA Column Validator] Regenerating file type code to match corrected column definitions...");

#if UNITY_EDITOR
                FileTypeGenerator.GenerateAllFileTypesCsCode();
                Debug.Log("[VERA Column Validator] File types regenerated successfully!");
#else
            Debug.LogWarning("[VERA Column Validator] Code regeneration is only available in the Unity Editor. " +
                "Please regenerate file types using the VERA menu in the editor.");
#endif
            }
            else
            {
                Debug.Log("[VERA Column Validator] All column definitions are valid!");
            }
        }

        private static bool ValidateRequiredColumns(VERAColumnDefinition columnDef, out string error)
        {
            error = "";

            if (columnDef.columns == null || columnDef.columns.Count == 0)
            {
                error = "No columns defined";
                return false;
            }

            // Check for required auto-columns that VERA always adds
            // Determine baseline here as this method may be called independently
            bool isBaseline = columnDef.fileType != null && (columnDef.fileType.fileTypeId == "baseline-data" || columnDef.fileType.name == "Experiment_Telemetry");
            // Baseline telemetry (Experiment_Telemetry or fileTypeId == "baseline-data") does not include eventId
            string[] requiredColumns = isBaseline
                ? new[] { "pID", "conditions", "ts" }
                : new[] { "pID", "conditions", "ts", "eventId" };
            List<string> columnNames = columnDef.columns.Select(c => c.name).ToList();

            List<string> missingColumns = new List<string>();
            foreach (string required in requiredColumns)
            {
                if (!columnNames.Contains(required))
                {
                    missingColumns.Add(required);
                }
            }

            if (missingColumns.Count > 0)
            {
                error = $"Missing required columns: {string.Join(", ", missingColumns)}";
                return false;
            }

            // Validate column order - auto-columns should come first
            for (int i = 0; i < requiredColumns.Length; i++)
            {
                if (i >= columnDef.columns.Count || columnDef.columns[i].name != requiredColumns[i])
                {
                    error = $"Required columns not in correct order (should be: {string.Join(", ", requiredColumns)})";
                    return false;
                }
            }

            return true;
        }

        private static bool FixColumnDefinition(VERAColumnDefinition columnDef)
        {
            try
            {
                if (columnDef.columns == null)
                    columnDef.columns = new List<VERAColumnDefinition.Column>();

                // Get existing data columns (non-auto columns)
                List<VERAColumnDefinition.Column> dataColumns = new List<VERAColumnDefinition.Column>();
                // Determine baseline for this columnDef
                bool isBaseline = columnDef.fileType != null && (columnDef.fileType.fileTypeId == "baseline-data" || columnDef.fileType.name == "Experiment_Telemetry");
                // Baseline doesn't include eventId as an auto column
                string[] autoColumns = isBaseline
                    ? new[] { "pID", "conditions", "ts" }
                    : new[] { "pID", "conditions", "ts", "eventId" };

                foreach (var column in columnDef.columns)
                {
                    if (!autoColumns.Contains(column.name))
                    {
                        dataColumns.Add(column);
                    }
                }

                // Rebuild column list with correct order
                columnDef.columns.Clear();

                // Add required auto-columns first
                columnDef.columns.Add(new VERAColumnDefinition.Column
                {
                    name = "pID",
                    description = "Participant ID",
                    type = VERAColumnDefinition.DataType.Number
                });

                columnDef.columns.Add(new VERAColumnDefinition.Column
                {
                    name = "conditions",
                    description = "Experimental conditions",
                    type = VERAColumnDefinition.DataType.String
                });

                columnDef.columns.Add(new VERAColumnDefinition.Column
                {
                    name = "ts",
                    description = "Time when the event occurred",
                    type = VERAColumnDefinition.DataType.Date
                });

                // Add eventId only for non-baseline file types
                if (!isBaseline)
                {
                    columnDef.columns.Add(new VERAColumnDefinition.Column
                    {
                        name = "eventId",
                        description = "Unique identifier for each event",
                        type = VERAColumnDefinition.DataType.Number
                    });
                }

                // Add back the data columns, ensuring numeric types for position/trigger/grip columns
                foreach (var col in dataColumns)
                {
                    string lower = (col.name ?? "").ToLower();
                    if (lower.Contains("_pos") || lower.Contains("trigger") || lower.Contains("grip"))
                    {
                        col.type = VERAColumnDefinition.DataType.Number;
                    }
                    columnDef.columns.Add(col);
                }

#if UNITY_EDITOR
                // Mark the asset as dirty so Unity saves the changes
                UnityEditor.EditorUtility.SetDirty(columnDef);
                UnityEditor.AssetDatabase.SaveAssets();
#endif

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VERA Column Validator] Error fixing column definition: {e.Message}");
                return false;
            }
        }
    }
}