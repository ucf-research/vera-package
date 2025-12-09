#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VERA
{
    [InitializeOnLoad]
    internal static class EnsureXRISamples
    {
        private const string XRI_PACKAGE_ID = "com.unity.xr.interaction.toolkit";
        private const string STARTER_ASSETS_ASSEMBLY_NAME = "Unity.XR.Interaction.Toolkit.Samples.StarterAssets";

        static EnsureXRISamples()
        {
            EditorApplication.delayCall += CheckAndEnsureXRISamples;
        }

        private static void CheckAndEnsureXRISamples()
        {
            // Check if XRI assembly definition exists
            if (XRIStarterAssetsAssemblyExists())
            {
                return;
            }

            // Check if samples exist via folder structure; skip if they do
            if (XRISamplesExistInProject())
            {
                return;
            }

            // No XRI samples found, so we need to import XRI samples
            Debug.Log("[VERA XRI Importer] Unable to automatically find XRI samples. Attempting to link / import...");
            TryImportSamples();
        }

        // Direct check: verify if the XRI Starter Assets assembly is loaded
        private static bool XRIStarterAssetsAssemblyExists()
        {
            try
            {
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                return assemblies.Any(assembly => assembly.GetName().Name == STARTER_ASSETS_ASSEMBLY_NAME);
            }
            catch
            {
                return false;
            }
        }

        // Find any version of XRI Starter Assets in the given base directory
        private static string FindXRIStarterAssetsPath(string basePath)
        {
            if (!Directory.Exists(basePath))
                return null;

            // Look for version directories (e.g., "3.0.3", "2.5.2", etc.)
            string[] versionDirs = Directory.GetDirectories(basePath)
                .Where(dir =>
                {
                    string dirName = Path.GetFileName(dir);
                    // Match version pattern (numbers and dots)
                    return System.Text.RegularExpressions.Regex.IsMatch(dirName, @"^\d+\.\d+\.\d+$");
                })
                .OrderByDescending(dir => new System.Version(Path.GetFileName(dir))) // Latest version first
                .ToArray();

            foreach (string versionDir in versionDirs)
            {
                string starterAssetsPath = Path.Combine(versionDir, "Starter Assets");
                string asmdefPath = Path.Combine(starterAssetsPath, STARTER_ASSETS_ASSEMBLY_NAME + ".asmdef");

                if (Directory.Exists(starterAssetsPath) && File.Exists(asmdefPath))
                {
                    return starterAssetsPath;
                }
            }

            return null;
        }

        // Check if XRI samples exist in the main project Assets folder
        private static bool XRISamplesExistInProject()
        {
            // Check for common XRI sample paths in Assets
            string[] possibleBasePaths = new string[]
            {
            Path.Combine(Application.dataPath, "XR Interaction Toolkit"),
            Path.Combine(Application.dataPath, "Samples", "XR Interaction Toolkit"),
            Path.Combine(Application.dataPath, "XRI")
            };

            foreach (string basePath in possibleBasePaths)
            {
                if (FindXRIStarterAssetsPath(basePath) != null)
                {
                    return true;
                }
            }

            // Also check for any assembly definition with the starter assets name
            string[] asmdefFiles = Directory.GetFiles(Application.dataPath, "*.asmdef", SearchOption.AllDirectories);
            foreach (string file in asmdefFiles)
            {
                if (Path.GetFileNameWithoutExtension(file) == STARTER_ASSETS_ASSEMBLY_NAME)
                {
                    return true;
                }
            }

            return false;
        }

        private static void TryImportSamples()
        {
            // Import samples using Unity's AssetDatabase and sample discovery
            try
            {
                // Use Unity's built-in sample import functionality
                var sampleGUIDs = UnityEditor.PackageManager.UI.Sample.FindByPackage(XRI_PACKAGE_ID, null);

                foreach (var sample in sampleGUIDs)
                {
                    if (sample.displayName.Contains("Starter Assets"))
                    {
                        Debug.Log($"[VERA XRI Importer] Found and importing sample: {sample.displayName}");
                        sample.Import();
                        return;
                    }
                }

                Debug.LogWarning("[VERA XRI Importer] Could not find Starter Assets sample to import.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[VERA XRI Importer] Error importing XRI samples: {ex.Message}");
            }
        }

    }
}
#endif
