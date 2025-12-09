#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VERA
{
    internal static class ConditionGenerator
    {
        private const string generatedCsPath = "Assets/VERA/Conditions/GeneratedCode/";

        // Clears all condition code files and removes define symbols
        public static void ClearAllConditionCsCode()
        {
            try
            {
                string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", generatedCsPath));

                if (Directory.Exists(fullPath))
                {
                    // For package directories, just delete files directly since AssetDatabase 
                    // cannot delete assets from package folders
                    Directory.Delete(fullPath, true);
                }
                Directory.CreateDirectory(fullPath);

                VERAAuthenticator.ClearConditionGroupDefineSymbols();
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error clearing condition code: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static void RegenerateConditionsForActiveExperiment()
        {
            string activeExperimentId = UnityEngine.PlayerPrefs.GetString("VERA_ActiveExperiment", "");
            if (string.IsNullOrEmpty(activeExperimentId))
            {
                Debug.LogWarning("VERA: No active experiment set. Open VERA Settings and select an experiment first.");
                return;
            }

            // Fetch experiments and find the active one, then regenerate
            VERAAuthenticator.GetUserExperiments((experiments) =>
            {
                if (experiments == null)
                {
                    Debug.LogError("VERA: Failed to fetch experiments while regenerating conditions.");
                    return;
                }

                var active = experiments.Find(e => e._id == activeExperimentId);
                if (active == null)
                {
                    Debug.LogError($"VERA: Active experiment '{activeExperimentId}' not found in fetched experiments.");
                    return;
                }

                ClearAllConditionCsCode();
                GenerateAllConditionCsCode(active);
                Debug.Log("VERA: Condition generation complete.");
            });
        }

        // Generates C# code files for all condition groups in the given experiment and sets up define symbols
        public static void GenerateAllConditionCsCode(Experiment experiment)
        {
            if (experiment == null || experiment.conditions == null)
            {
                Debug.LogError("No experiment or condition groups found");
                return;
            }

            ClearAllConditionCsCode();

            foreach (var group in experiment.conditions)
            {
                GenerateConditionGroupCode(group);
            }
            VERAAuthenticator.UpdateConditionGroupDefineSymbols(experiment.conditions);
            AssetDatabase.Refresh();
        }

        // Generates a C# code file for the given condition group
        private static void GenerateConditionGroupCode(IVGroup group)
        {
            if (group == null || string.IsNullOrEmpty(group.ivName))
            {
                Debug.LogError("IV group has no ivName");
                return;
            }

            string ivName = group.ivName;
            StringBuilder sb = new StringBuilder();

            // Build a list of the names of values this IV can have
            // (ex., for "Targets", might be "Bunnies" or "Pumpkins")
            List<string> valueNames = new List<string>();

            foreach (var condition in group.conditions)
            {
                if (string.IsNullOrEmpty(condition.name))
                {
                    Debug.LogWarning($"[VERA Condition] Skipping condition with no name in IV group {group.ivName}");
                    continue;
                }

                valueNames.Add(condition.name);
            }

            // Emit class header
            GenerateConditionsGroupClassHeader(sb, ivName);

            // Emit enum of possible values
            GenerateConditionsGroupEnum(sb, ivName, valueNames);

            // Emit method to get the currently selected value from cache
            GenerateConditionsGroupGetMethod(sb, ivName, valueNames);

            // Emit method to set the currently selected value in cache and sync to server
            GenerateConditionsGroupSetMethod(sb, ivName, valueNames);

            sb.AppendLine();
            sb.AppendLine("\t}");
            sb.AppendLine("}");
            sb.AppendLine($"#endif");

            try
            {
                string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", generatedCsPath));
                string filePath = Path.Combine(fullPath, $"VERAIV_{ivName}.cs");

                // Compare with existing file, only write if changed
                if (File.Exists(filePath))
                {
                    string existingContent = File.ReadAllText(filePath);
                    if (existingContent == sb.ToString())
                    {
                        // No changes, skip writing
                        return;
                    }
                }

                File.WriteAllText(filePath, sb.ToString());

                AssetDatabase.ImportAsset(Path.Combine(generatedCsPath, $"VERAIV_{ivName}.cs"), ImportAssetOptions.ForceUpdate);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating condition code: {ex.Message}\n{ex.StackTrace}");
            }
        }


        // Emits the class header and declaration for the given condition group
        private static void GenerateConditionsGroupClassHeader(StringBuilder sb, string groupName)
        {
            // Generate import statements and namespace
            sb.AppendLine($"#if VERAIV_{groupName}");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.Networking;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections;");
            sb.AppendLine();
            sb.AppendLine("namespace VERA");
            sb.AppendLine("{");

            // Generate class XML comments
            sb.AppendLine("\t/// <summary>");
            sb.AppendLine($"\t/// Static class for accessing and modifying the {groupName} independent variable and its possible values.");
            sb.AppendLine($"\t/// <br/><br/>This class has been generated based on the condition values defined for this variable in the VERA portal.");
            sb.AppendLine($"\t/// This class should be the only way you access or modify the {groupName} independent variable.");
            sb.AppendLine($"\t/// <br/><br/>Notably, use the GetSelectedValue() method to get the currently selected value of this independent variable, and the SetSelectedValue() method to change it.");
            sb.AppendLine($"\t/// </summary>");

            // Generate class declaration
            sb.AppendLine($"\tpublic static class VERAIV_{groupName}");
            sb.AppendLine("\t{");
            sb.AppendLine("");
        }

        // Emits an enum declaration for the given condition group and its possible values
        private static void GenerateConditionsGroupEnum(StringBuilder sb, string groupName, List<string> valueNames)
        {
            // Emit enum's XML comments
            sb.AppendLine("\t\t/// <summary>");
            sb.AppendLine($"\t\t/// Enum of possible values for the {groupName} independent variable.");
            sb.AppendLine($"\t\t/// <br/><br/>This enum has been generated based on the condition values defined for this variable in the VERA portal.");
            sb.AppendLine($"\t\t/// Each enum value is prefixed with V_ to avoid issues with names starting with numbers or other invalid enum names.");
            sb.AppendLine($"\t\t/// <br/><br/>For this particular independent variable ({groupName}), the possible values are:<list type=\"bullet\">");
            foreach (var valueName in valueNames)
            {
                sb.AppendLine($"\t\t/// <item><description>{valueName}</description></item>");
            }
            sb.AppendLine($"\t\t/// </list>");
            sb.AppendLine($"\t\t/// </summary>");

            // Emit enum declaration and values
            sb.AppendLine($"\t\tpublic enum IVValue");
            sb.AppendLine("\t\t{");

            foreach (var valueName in valueNames)
            {
                sb.AppendLine($"\t\t\tV_{valueName},");
            }

            sb.AppendLine("\t\t}");
            sb.AppendLine();
        }

        // Emits a method to get the currently selected value of the given condition group from cache
        private static void GenerateConditionsGroupGetMethod(StringBuilder sb, string groupName, List<string> valueNames)
        {
            // Emit XML comments
            sb.AppendLine("\t\t/// <summary>");
            sb.AppendLine($"\t\t/// Gets the currently selected condition value of the {groupName} independent variable.");
            sb.AppendLine($"\t\t/// (Note - This method uses a cached value stored locally on the client. As such, it may be out of date if you have manually changed the condition value on the server externally.)");
            sb.AppendLine($"\t\t/// <br/><br/>This method has been generated based on the condition values defined for this variable in the VERA portal.");
            sb.AppendLine($"\t\t/// </summary>");
            sb.AppendLine($"\t\t/// <returns>The current selected value of the {groupName} independent variable</returns>");

            // Emit method declaration and body
            sb.AppendLine($"\t\tpublic static IVValue GetSelectedValue()");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\t// Get the value - will be a string, needs to be converted to enum");
            sb.AppendLine($"\t\t\tstring selectedValue = VERASessionManager.GetSelectedIVValue(\"{groupName}\");");
            sb.AppendLine();
            sb.AppendLine("\t\t\tif (string.IsNullOrEmpty(selectedValue))");
            sb.AppendLine("\t\t\t{");
            sb.AppendLine($"\t\t\t\tDebug.LogError(\"[VERA IVGroup_{groupName}] Error while getting selected IV value, got empty or null string as response\");");
            sb.AppendLine("\t\t\t\tthrow new InvalidOperationException(\"Unknown selected condition value\");");
            sb.AppendLine("\t\t\t}");
            sb.AppendLine();
            sb.AppendLine("\t\t\t// Direct conversion from string value to enum");
            sb.AppendLine("\t\t\treturn selectedValue switch");
            sb.AppendLine("\t\t\t{");

            foreach (var valueName in valueNames)
            {
                sb.AppendLine($"\t\t\t\t\"{valueName}\" => IVValue.V_{valueName},");
            }

            sb.AppendLine($"\t\t\t\t_ => throw new InvalidOperationException($\"Unknown selected condition value: {{selectedValue}}\")");
            sb.AppendLine("\t\t\t};");
            sb.AppendLine("\t\t}");
            sb.AppendLine();
        }

        // Emits a method to set the currently selected value of the given condition group in cache and sync to server
        private static void GenerateConditionsGroupSetMethod(StringBuilder sb, string groupName, List<string> valueNames)
        {
            // Emit XML comments
            sb.AppendLine("\t\t/// <summary>");
            sb.AppendLine($"\t\t/// Sets the currently selected condition value of the {groupName} independent variable.");
            sb.AppendLine($"\t\t/// <br/><br/>This method has been generated based on the condition values defined for this variable in the VERA portal.");
            sb.AppendLine($"\t\t/// </summary>");
            sb.AppendLine($"\t\t/// <param name=\"value\">The new selected value for the {groupName} independent variable</param>");

            // Emit method declaration and body
            sb.AppendLine($"\t\tpublic static void SetSelectedValue(IVValue value)");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\t// Convert enum to server value");
            sb.AppendLine("\t\t\tstring valueStr = value switch");
            sb.AppendLine("\t\t\t{");

            foreach (var valueName in valueNames)
            {
                sb.AppendLine($"\t\t\t\tIVValue.V_{valueName} => \"{valueName}\",");
            }

            sb.AppendLine($"\t\t\t\t_ => throw new InvalidOperationException($\"Unknown enum value: {{value}}\")");
            sb.AppendLine("\t\t\t};");
            sb.AppendLine();
            sb.AppendLine($"\t\t\tVERASessionManager.SetSelectedIVValue(\"{groupName}\", valueStr);");
            sb.AppendLine("\t\t}");
        }
    }
}
#endif
