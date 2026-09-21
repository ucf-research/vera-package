// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VERA
{
    [InitializeOnLoad]
    internal static class EnsureXRISamples
    {
        private const string XRI_PACKAGE_ID = "com.unity.xr.interaction.toolkit";
        private const string STARTER_ASSETS_ASSEMBLY_NAME = "Unity.XR.Interaction.Toolkit.Samples.StarterAssets";
        private const string STARTER_ASSETS_FOLDER_NAME = "Starter Assets";
        private const string STARTER_ASSETS_SAMPLE_DISPLAY_NAME = "Starter Assets";
        private const string SPATIAL_KEYBOARD_ASSEMBLY_NAME = "Unity.XR.Interaction.Toolkit.Samples.SpatialKeyboard";
        private const string SPATIAL_KEYBOARD_FOLDER_NAME = "Spatial Keyboard";
        private const string SPATIAL_KEYBOARD_SAMPLE_DISPLAY_NAME = "Spatial Keyboard";

        static EnsureXRISamples()
        {
            EditorApplication.delayCall += CheckAndEnsureXRISamples;
        }

        private static void CheckAndEnsureXRISamples()
        {
            bool needsStarterAssets = !XRISamplePresent(STARTER_ASSETS_ASSEMBLY_NAME, STARTER_ASSETS_FOLDER_NAME);
            bool needsSpatialKeyboard = !XRISamplePresent(SPATIAL_KEYBOARD_ASSEMBLY_NAME, SPATIAL_KEYBOARD_FOLDER_NAME);

            if (!needsStarterAssets && !needsSpatialKeyboard)
                return;

            VERADebugger.Log(
                "Unable to automatically find required XRI samples. Attempting to link / import...",
                "VERA XRI Importer",
                DebugPreference.Informative);

            if (needsStarterAssets)
                TryImportSample(STARTER_ASSETS_SAMPLE_DISPLAY_NAME);

            if (needsSpatialKeyboard)
                TryImportSample(SPATIAL_KEYBOARD_SAMPLE_DISPLAY_NAME);
        }

        private static bool XRISamplePresent(string assemblyName, string sampleFolderName)
        {
            if (AssemblyExists(assemblyName))
                return true;

            if (SampleFolderExistsInProject(sampleFolderName, assemblyName))
                return true;

            // Fallback: any matching asmdef under Assets
            if (!Directory.Exists(Application.dataPath))
                return false;

            string[] asmdefFiles = Directory.GetFiles(Application.dataPath, "*.asmdef", SearchOption.AllDirectories);
            foreach (string file in asmdefFiles)
            {
                if (Path.GetFileNameWithoutExtension(file) == assemblyName)
                    return true;
            }

            return false;
        }

        private static bool AssemblyExists(string assemblyName)
        {
            try
            {
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                return assemblies.Any(assembly => assembly.GetName().Name == assemblyName);
            }
            catch
            {
                return false;
            }
        }

        private static bool SampleFolderExistsInProject(string sampleFolderName, string assemblyName)
        {
            string[] possibleBasePaths =
            {
                Path.Combine(Application.dataPath, "XR Interaction Toolkit"),
                Path.Combine(Application.dataPath, "Samples", "XR Interaction Toolkit"),
                Path.Combine(Application.dataPath, "XRI")
            };

            foreach (string basePath in possibleBasePaths)
            {
                if (FindSamplePath(basePath, sampleFolderName, assemblyName) != null)
                    return true;
            }

            return false;
        }

        private static string FindSamplePath(string basePath, string sampleFolderName, string assemblyName)
        {
            if (!Directory.Exists(basePath))
                return null;

            // Look for version directories (e.g., "3.0.3", "2.5.2", etc.)
            string[] versionDirs = Directory.GetDirectories(basePath)
                .Where(dir =>
                {
                    string dirName = Path.GetFileName(dir);
                    return System.Text.RegularExpressions.Regex.IsMatch(dirName, @"^\d+\.\d+\.\d+$");
                })
                .OrderByDescending(dir => new System.Version(Path.GetFileName(dir)))
                .ToArray();

            foreach (string versionDir in versionDirs)
            {
                string samplePath = Path.Combine(versionDir, sampleFolderName);
                string asmdefPath = Path.Combine(samplePath, assemblyName + ".asmdef");

                if (Directory.Exists(samplePath) && File.Exists(asmdefPath))
                    return samplePath;
            }

            return null;
        }

        private static void TryImportSample(string sampleDisplayName)
        {
            try
            {
                var samples = UnityEditor.PackageManager.UI.Sample.FindByPackage(XRI_PACKAGE_ID, null);

                foreach (var sample in samples)
                {
                    if (!sample.displayName.Contains(sampleDisplayName))
                        continue;

                    VERADebugger.Log(
                        $"Found and importing sample: {sample.displayName}",
                        "VERA XRI Importer",
                        DebugPreference.Informative);
                    sample.Import();
                    return;
                }

                VERADebugger.LogWarning(
                    $"Could not find \"{sampleDisplayName}\" sample to import from XR Interaction Toolkit.",
                    "VERA XRI Importer");
            }
            catch (System.Exception ex)
            {
                VERADebugger.LogError(
                    $"Error importing XRI \"{sampleDisplayName}\" sample: {ex.Message}",
                    "VERA XRI Importer");
            }
        }
    }
}
#endif
