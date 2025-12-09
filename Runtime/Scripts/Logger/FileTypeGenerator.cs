#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace VERA
{
    internal static class FileTypeGenerator
    {
        // FileTypeGenerator will generate .cs files for associated column definitions, to allow easy logging of data per-file

        private const string generatedCsPath = "Assets/VERA/Filetypes/GeneratedCode/";

        // Deletes all generated file type cs code
        public static void ClearAllFileTypeCsCode()
        {
            // Get all files in the folder
            string[] files = Directory.GetFiles(generatedCsPath);

            // Delete each file
            foreach (string file in files)
            {
                File.Delete(file);
            }

            AssetDatabase.Refresh();
        }

        // Generates .cs files for every column definition currently in the columns folder
        public static void GenerateAllFileTypesCsCode()
        {
            // Get all items
            VERAColumnDefinition[] columnDefinitions = Resources.LoadAll<VERAColumnDefinition>("");

            // Generate code
            foreach (VERAColumnDefinition columnDefinition in columnDefinitions)
            {
                GenerateFileTypeCsCode(columnDefinition, false);
            }

            AssetDatabase.Refresh();
        }

        // Adds a Unity Editor menu item so developers can regenerate file type wrappers from the Editor
        [MenuItem("VERA/Regenerate File Types")]
        public static void MenuGenerateAllFileTypesCsCode()
        {
            GenerateAllFileTypesCsCode();
            Debug.Log("[VERA FileTypeGenerator] Generated all file type wrapper code.");
        }

        // Generates .cs file for a given column definition
        public static void GenerateFileTypeCsCode(VERAColumnDefinition columnDefinition, bool refreshOnFinish)
        {
            string fileName = columnDefinition.fileType.name;
            string filePath = generatedCsPath + "VERAFile_" + fileName + ".cs";

            // Use StringBuilder to create the code
            StringBuilder sb = new StringBuilder();

            // Build the class; for example, for a file named "PlayerTransform", would be VERAFile_PlayerTransform
            sb.AppendLine("#if VERAFile_" + fileName);
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using System;");
            sb.AppendLine("");
            sb.AppendLine("namespace VERA");
            sb.AppendLine("{");
            sb.AppendLine("\t");

            sb.AppendLine("\t/// <summary>");
            sb.AppendLine($"\t/// Static class for recording new entries to the {fileName} CSV file.");
            sb.AppendLine($"\t/// <br/><br/>This class has been generated based on the CSV file you have defined on the VERA portal.");
            sb.AppendLine($"\t/// This class should be the only way you record new CSV entries to the {fileName} file.");
            sb.AppendLine($"\t/// <br/><br/>Notably, use the CreateCsvEntry() method to create new entries in the {fileName} CSV log file.");
            sb.AppendLine($"\t/// </summary>");

            sb.AppendLine("\tpublic static class VERAFile_" + fileName);
            sb.AppendLine("\t{");
            sb.AppendLine("\t\t");
            sb.AppendLine("\t\tprivate const string fileName = \"" + fileName + "\";");
            sb.AppendLine("\t\t");

            // Build logging function
            GenerateCreateCSVEntryFunction(columnDefinition, sb);
            sb.AppendLine("\t\t");

            sb.AppendLine("\t}");
            sb.AppendLine("}");
            sb.AppendLine("#endif");

            string newContents = sb.ToString();
            // if the file exists, read it and compare (normalize line endings to '\n')
            // This way we can prevent a recompile if possible - if text matches, we don't need to write
            if (File.Exists(filePath))
            {
                var oldContents = File.ReadAllText(filePath, Encoding.UTF8);
                string oldNormalized = oldContents.Replace("\r\n", "\n");
                string newNormalized = newContents.Replace("\r\n", "\n");

                if (oldNormalized == newNormalized)
                {
                    // no changes, skip write and recompile
                    return;
                }
            }

            // Ensure the directory exists
            if (!Directory.Exists(generatedCsPath))
            {
                Directory.CreateDirectory(generatedCsPath);
            }

            // Write the file
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

            // Force Unity to refresh so the new/modified code is recognized
            if (refreshOnFinish)
            {
                AssetDatabase.Refresh();
            }
        }

        // Generates the CreateCsvEntry function for a given column definition
        // Will always be in a specific format. For example, for a file type "PlayerTransform" with columns
        //   "Timestamp", "EventId", "Message", "Transform", would have the definition:
        //   public static void CreateEntry(int EventId, string Message, Transform Transform)
        private static void GenerateCreateCSVEntryFunction(VERAColumnDefinition columnDefinition, StringBuilder sb)
        {
            GenerateCSVEntryComments(columnDefinition, sb);

            string functionDefinition = "\t\tpublic static void CreateCsvEntry(";
            List<string> parameterNames = new List<string>();

            // Determine if this is baseline telemetry (no eventId)
            bool isBaseline = columnDefinition.fileType.fileTypeId == "baseline-data" || columnDefinition.fileType.name == "Experiment_Telemetry";

            // Add eventId only for non-baseline
            if (!isBaseline)
            {
                functionDefinition += "int eventId";
            }
            // Determine which columns are auto-populated (pID, conditions, ts, eventId)
            var autoNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pid", "conditions", "ts", "timestamp", "timestamp_utc", "eventid", "event_id" };

            // Build list of columns to include (in order) excluding auto-populated columns
            List<VERAColumnDefinition.Column> columnsToInclude = new List<VERAColumnDefinition.Column>();
            foreach (var col in columnDefinition.columns)
            {
                if (autoNames.Contains((col.name ?? "").ToLowerInvariant())) continue;
                columnsToInclude.Add(col);
            }

            if (columnsToInclude.Count > 0 || !isBaseline)
            {
                if (!isBaseline)
                    functionDefinition += ", ";
            }

            // Loop through filtered columns to add parameters
            for (int i = 0; i < columnsToInclude.Count; i++)
            {
                var col = columnsToInclude[i];
                string colNameLower = (col.name ?? "").ToLower();
                // Only ts/timestamp/timestamp_utc should be DateTime, all _pos/trigger/grip are float, rest as per type
                if (colNameLower == "ts" || colNameLower == "timestamp" || colNameLower == "timestamp_utc")
                {
                    functionDefinition += "DateTime ";
                }
                else if (colNameLower.Contains("_pos") || colNameLower.Contains("trigger") || colNameLower.Contains("grip"))
                {
                    functionDefinition += "float ";
                }
                else
                {
                    switch (col.type)
                    {
                        case VERAColumnDefinition.DataType.Number:
                            functionDefinition += "int ";
                            break;
                        case VERAColumnDefinition.DataType.String:
                            functionDefinition += "string ";
                            break;
                        case VERAColumnDefinition.DataType.Date:
                            functionDefinition += "string "; // treat as string unless it's ts
                            break;
                        case VERAColumnDefinition.DataType.Transform:
                            functionDefinition += "Transform ";
                            break;
                        default:
                            functionDefinition += "string ";
                            break;
                    }
                }

                string parameterName = col.name.Replace(" ", "");
                parameterNames.Add(parameterName);
                functionDefinition += parameterName;

                if (i != columnsToInclude.Count - 1)
                {
                    functionDefinition += ", ";
                }
            }

            functionDefinition += ")";
            sb.AppendLine(functionDefinition);
            sb.AppendLine("\t\t{");

            // Add the actual function code, which calls the VERALogger
            string loggerCall = "\t\t\tVERASessionManager.CreateArbitraryCsvEntry(fileName";
            if (!isBaseline)
            {
                loggerCall += ", eventId";
            }
            if (parameterNames.Count > 0)
                loggerCall += ", ";

            for (int i = 0; i < parameterNames.Count; i++)
            {
                loggerCall += parameterNames[i];
                if (i != parameterNames.Count - 1)
                {
                    loggerCall += ", ";
                }
            }
            loggerCall += "\t);";
            sb.AppendLine(loggerCall);
            sb.AppendLine("\t\t}");
        }

        // Generates XML comments for the CreateCsvEntry function
        private static void GenerateCSVEntryComments(VERAColumnDefinition columnDefinition, StringBuilder sb)
        {
            sb.AppendLine("\t\t/// <summary>");
            sb.AppendLine("\t\t/// Creates a new row entry in the " + columnDefinition.fileType.name + " CSV log file.");

            // If this is an automatic baseline or Experiment_Telemetry file, say such and be less verbose
            if (columnDefinition.fileType.fileTypeId == "baseline-data" || columnDefinition.fileType.name == "Experiment_Telemetry")
            {
                sb.AppendLine("\t\t/// This file is automatically populated and handled by VERA; researchers should NOT need to call this function directly.");

                return;
            }

            // This is a user-defined file type, so include full description
            // Start with base auto-populated columns
            sb.AppendLine("\t\t/// This CSV entry will automatically have the following fields populated:");
            sb.AppendLine("\t\t/// <list type=\"bullet\">");
            sb.AppendLine("\t\t/// <item><description>pID (Participant ID)</description></item>");
            sb.AppendLine("\t\t/// <item><description>TS (Timestamp in milliseconds since application start)</description></item>");
            sb.AppendLine("\t\t/// <item><description>Conditions (Experimental conditions the participant was under during this log, in JSON format)</description></item>");
            sb.AppendLine("\t\t/// </list>");
            sb.AppendLine("\t\t/// This function has been set up according to your configuration and preferences for this file type on the VERA portal.");
            
            // Add additional arbitrary columns for this experiment
            sb.AppendLine("\t\t/// Included in your configuration are the following additional columns:");
            sb.AppendLine("\t\t/// <list type=\"bullet\">");
            sb.AppendLine("\t\t/// <item>eventId: An identifier for this log entry, of type int. Mandatory for each user-generated file type, but may be arbitrarily assigned according to your preferences.</item>");
            foreach (var col in columnDefinition.columns)
            {
                string colNameLower = (col.name ?? "").ToLowerInvariant();
                if (colNameLower == "pid" || colNameLower == "conditions" || colNameLower == "ts" || colNameLower == "timestamp" || colNameLower == "timestamp_utc" || colNameLower == "eventid" || colNameLower == "event_id")
                {
                    // skip auto-populated columns
                    continue;
                }
                string typeString = "string";
                if (colNameLower == "ts" || colNameLower == "timestamp" || colNameLower == "timestamp_utc")
                {
                    typeString = "DateTime";
                }
                else if (colNameLower.Contains("_pos") || colNameLower.Contains("trigger") || colNameLower.Contains("grip"))
                {
                    typeString = "float";
                }
                else
                {
                    switch (col.type)
                    {
                        case VERAColumnDefinition.DataType.Number:
                            typeString = "int";
                            break;
                        case VERAColumnDefinition.DataType.String:
                            typeString = "string";
                            break;
                        case VERAColumnDefinition.DataType.Date:
                            typeString = "string"; // treat as string unless it's ts
                            break;
                        case VERAColumnDefinition.DataType.Transform:
                            typeString = "Transform";
                            break;
                        default:
                            typeString = "string";
                            break;
                    }
                }
                sb.AppendLine($"\t\t/// <item>{col.name.Replace(" ", "")}: Value for the '{col.name}' column, of type {typeString}.</item>");
            }
            sb.AppendLine("\t\t/// </list>");
            sb.AppendLine("\t\t/// </summary>");

            // Now add param comments
            sb.AppendLine("\t\t/// <param name=\"eventId\">eventId: An identifier for this log entry, of type int. Mandatory for each user-generated file type, but may be arbitrarily assigned according to your preferences.</param>");
            foreach (var col in columnDefinition.columns)
            {
                string colNameLower = (col.name ?? "").ToLowerInvariant();
                if (colNameLower == "pid" || colNameLower == "conditions" || colNameLower == "ts" || colNameLower == "timestamp" || colNameLower == "timestamp_utc" || colNameLower == "eventid" || colNameLower == "event_id")
                {
                    // skip auto-populated columns
                    continue;
                }
                string typeString = "string";
                if (colNameLower == "ts" || colNameLower == "timestamp" || colNameLower == "timestamp_utc")
                {
                    typeString = "DateTime";
                }
                else if (colNameLower.Contains("_pos") || colNameLower.Contains("trigger") || colNameLower.Contains("grip"))
                {
                    typeString = "float";
                }
                else
                {
                    switch (col.type)
                    {
                        case VERAColumnDefinition.DataType.Number:
                            typeString = "int";
                            break;
                        case VERAColumnDefinition.DataType.String:
                            typeString = "string";
                            break;
                        case VERAColumnDefinition.DataType.Date:
                            typeString = "string"; // treat as string unless it's ts
                            break;
                        case VERAColumnDefinition.DataType.Transform:
                            typeString = "Transform";
                            break;
                        default:
                            typeString = "string";
                            break;
                    }
                }
                sb.AppendLine($"\t\t/// <param name=\"{col.name.Replace(" ", "")}\">{col.name.Replace(" ", "")}: Value for the '{col.name}' column, of type {typeString}.</param>");
            }
        }
    }
}
#endif