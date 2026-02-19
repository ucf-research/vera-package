#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using System.Linq;

namespace VERA
{
    /// <summary>
    /// Editor initialization script that ensures baseline data column definition exists
    /// </summary>
    [InitializeOnLoad]
    internal static class VERABaselineDataSetup
    {
        private const string TELEMETRY_SYMBOL = "VERAFile_Experiment_Telemetry";

        static VERABaselineDataSetup()
        {
            // Check if column definition exists (either from baseline setup or authenticator)
            var columnDef = Resources.Load<VERAColumnDefinition>("Experiment_TelemetryColumnDefinition");
            var authColumnDef = Resources.Load<VERAColumnDefinition>("VERA_Experiment_Telemetry_ColumnDefinition");

            if (columnDef == null && authColumnDef == null)
            {
                // No column definition exists from either source; create the baseline one
                EditorApplication.delayCall += () =>
                {
                    CreateBaselineDataColumnDefinition();
                    AddScriptingDefineSymbol();
                    FileTypeGenerator.GenerateAllFileTypesCsCode();
                };
            }
            else
            {
                // A column definition already exists; ensure scripting define symbol is added
                EditorApplication.delayCall += () =>
                {
                    AddScriptingDefineSymbol();
                };
            }
        }

        public static void SetupBaselineDataMenuItem()
        {
            VERADebugger.Log("Starting VERA Baseline Data setup...", "VERABaselineDataSetup");

            CreateBaselineDataColumnDefinition();

            // Ensure the generated code directory exists
            string generatedCodePath = "Assets/VERA/Filetypes/GeneratedCode";
            if (!AssetDatabase.IsValidFolder(generatedCodePath))
            {
                VERADebugger.Log($"Creating directory: {generatedCodePath}", "VERABaselineDataSetup");
                if (!AssetDatabase.IsValidFolder("Assets/VERA"))
                    AssetDatabase.CreateFolder("Assets", "VERA");
                if (!AssetDatabase.IsValidFolder("Assets/VERA/Filetypes"))
                    AssetDatabase.CreateFolder("Assets/VERA", "Filetypes");
                if (!AssetDatabase.IsValidFolder("Assets/VERA/Filetypes/GeneratedCode"))
                    AssetDatabase.CreateFolder("Assets/VERA/Filetypes", "GeneratedCode");
            }

            VERADebugger.Log("Generating file type wrappers...", "VERABaselineDataSetup");
            FileTypeGenerator.GenerateAllFileTypesCsCode();
            AssetDatabase.Refresh();

            VERADebugger.Log("Adding scripting define symbol...", "VERABaselineDataSetup");
            AddScriptingDefineSymbol();

            VERADebugger.Log("✓ VERA Baseline Data setup complete!", "VERABaselineDataSetup");
            VERADebugger.Log($"  - Column definition: Assets/Resources/Experiment_TelemetryColumnDefinition.asset", "VERABaselineDataSetup");
            VERADebugger.Log($"  - Generated code: {generatedCodePath}/VERAFile_Experiment_Telemetry.cs", "VERABaselineDataSetup");
            VERADebugger.Log($"  - Scripting symbol: {TELEMETRY_SYMBOL}", "VERABaselineDataSetup");
            VERADebugger.Log("Unity will now recompile. Please wait...", "VERABaselineDataSetup");
        }

        private static void CreateBaselineDataColumnDefinition()
        {
            VERABaselineDataColumnDefinition.CreateAndSaveBaselineDataColumnDefinition();
        }

        private static void AddScriptingDefineSymbol()
        {
            NamedBuildTarget buildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string defines = PlayerSettings.GetScriptingDefineSymbols(buildTarget);

            if (!defines.Contains(TELEMETRY_SYMBOL))
            {
                if (!string.IsNullOrEmpty(defines))
                {
                    defines += ";";
                }
                defines += TELEMETRY_SYMBOL;
                PlayerSettings.SetScriptingDefineSymbols(buildTarget, defines);
            }
        }
    }
}
#endif
