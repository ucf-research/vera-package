using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VERA
{
    internal static class VERAColumnValidator
    {

        public static void ValidateAndFixColumnDefinitions()
        {
            VERADebugger.Log("Starting column definition validation...", "VERAColumnValidator", DebugPreference.Verbose);

            // Load all column definitions
            VERAColumnDefinition[] columnDefinitions = Resources.LoadAll<VERAColumnDefinition>("");
            if (columnDefinitions == null || columnDefinitions.Length == 0)
            {
                VERADebugger.Log("No column definitions found, skipping validation.", "VERAColumnValidator", DebugPreference.Verbose);
                return;
            }

            // AssetDatabase.SaveAssets/Refresh during play freezes the editor (busy spinner, no further frames).
            // Still apply in-memory fixes while playing so logging can continue; persist/regenerate only in edit mode.
            bool canPersistEditorChanges = !Application.isPlaying;

            bool foundIssues = false;
            List<string> issuesFound = new List<string>();

            foreach (VERAColumnDefinition columnDef in columnDefinitions)
            {
                if (columnDef == null || columnDef.fileType == null)
                    continue;

                // Skip validation for file types that define all columns explicitly
                if (columnDef.skipAutoColumns)
                    continue;

                string fileTypeName = columnDef.fileType.name;
                bool isBaseline = columnDef.fileType.fileTypeId == "baseline-data" || fileTypeName == VERAExperimentTelemetrySchema.Name;

                // Validate that this column definition has the required VERA auto-columns
                if (!ValidateRequiredColumns(columnDef, out string validationError))
                {
                    foundIssues = true;
                    issuesFound.Add($"{fileTypeName}: {validationError}");

                    // Attempt to fix the column definition
                    if (FixColumnDefinition(columnDef, canPersistEditorChanges))
                    {
                        VERADebugger.Log($"Fixed column definition for {fileTypeName}", "VERAColumnValidator", DebugPreference.Verbose);
                    }
                    else
                    {
                        VERADebugger.LogError($"Failed to fix column definition for {fileTypeName}", "VERAColumnValidator");
                    }
                }

                // For Experiment_Telemetry, also validate that data columns match the canonical schema.
                // This catches cases where the saved asset was created with an older version of the logger
                // that had fewer columns, causing a runtime column count mismatch error.
                if (isBaseline && fileTypeName == VERAExperimentTelemetrySchema.Name)
                {
                    if (columnDef.columns.Count != VERAExperimentTelemetrySchema.Columns.Count)
                    {
                        int prevCount = columnDef.columns.Count;
                        columnDef.columns = new List<VERAColumnDefinition.Column>(VERAExperimentTelemetrySchema.Columns);
                        foundIssues = true;
                        issuesFound.Add($"{fileTypeName}: Schema updated from {prevCount} to {VERAExperimentTelemetrySchema.Columns.Count} columns to match current logger");

#if UNITY_EDITOR
                        if (canPersistEditorChanges)
                        {
                            UnityEditor.EditorUtility.SetDirty(columnDef);
                            UnityEditor.AssetDatabase.SaveAssets();
                        }
#endif
                    }
                }
            }

            if (foundIssues)
            {
                VERADebugger.LogWarning($"Found and fixed {issuesFound.Count} column definition issues:", "VERAColumnValidator");
                foreach (string issue in issuesFound)
                {
                    VERADebugger.LogWarning($"  - {issue}", "VERAColumnValidator");
                }

#if UNITY_EDITOR
                if (canPersistEditorChanges)
                {
                    // Regenerate code files to match fixed column definitions (edit mode only)
                    VERADebugger.Log("Regenerating file type code to match corrected column definitions...", "VERAColumnValidator", DebugPreference.Verbose);
                    FileTypeGenerator.GenerateAllFileTypesCsCode();
                    VERADebugger.Log("File types regenerated successfully!", "VERAColumnValidator", DebugPreference.Verbose);
                }
                else
                {
                    VERADebugger.LogWarning(
                        "Column definition fixes were applied in-memory only (play mode). " +
                        "Exit play mode and re-authenticate / regenerate file types so changes persist.",
                        "VERAColumnValidator");
                }
#else
                VERADebugger.LogWarning("Code regeneration is only available in the Unity Editor. " +
                    "Please regenerate file types using the VERA menu in the editor.", "VERAColumnValidator");
#endif
            }
            else
            {
                VERADebugger.Log("All column definitions are valid!", "VERAColumnValidator");
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
            bool isBaseline = columnDef.fileType != null && (columnDef.fileType.fileTypeId == "baseline-data" || columnDef.fileType.name == VERAExperimentTelemetrySchema.Name);
            // Baseline telemetry (Experiment_Telemetry or fileTypeId == "baseline-data")
            // eventId disabled while its necessity is evaluated
            /*
            string[] requiredColumns = isBaseline
                ? new[] { "pID", "conditions", "ts" }
                : new[] { "pID", "conditions", "ts", "eventId" };
                */
            string[] requiredColumns = new[] { "pID", "conditions", "ts" };
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

        private static bool FixColumnDefinition(VERAColumnDefinition columnDef, bool persistEditorChanges = true)
        {
            try
            {
                if (columnDef.columns == null)
                    columnDef.columns = new List<VERAColumnDefinition.Column>();

                // Get existing data columns (non-auto columns)
                List<VERAColumnDefinition.Column> dataColumns = new List<VERAColumnDefinition.Column>();
                // Determine baseline for this columnDef
                bool isBaseline = columnDef.fileType != null && (columnDef.fileType.fileTypeId == "baseline-data" || columnDef.fileType.name == VERAExperimentTelemetrySchema.Name);
                // Baseline doesn't include eventId as an auto column
                // eventId disabled while its necessity is evaluated
                /*
                string[] autoColumns = isBaseline
                    ? new[] { "pID", "conditions", "ts" }
                    : new[] { "pID", "conditions", "ts", "eventId" };*/
                string[] autoColumns = new[] { "pID", "conditions", "ts" };

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
                    type = VERAColumnDefinition.DataType.Float
                });

                // Add eventId only for non-baseline file types
                // eventId disabled while its necessity is evaluated
                /*
                if (!isBaseline)
                {
                    columnDef.columns.Add(new VERAColumnDefinition.Column
                    {
                        name = "eventId",
                        description = "Unique identifier for each event",
                        type = VERAColumnDefinition.DataType.Number
                    });
                }*/

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
                // Never SaveAssets during play mode — it freezes the editor.
                if (persistEditorChanges)
                {
                    UnityEditor.EditorUtility.SetDirty(columnDef);
                    UnityEditor.AssetDatabase.SaveAssets();
                }
#endif

                return true;
            }
            catch (System.Exception e)
            {
                VERADebugger.LogError($"Error fixing column definition: {e.Message}", "VERAColumnValidator");
                return false;
            }
        }
    }
}