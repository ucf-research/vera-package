// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VERA
{
    /// <summary>
    /// Copies the XRI Spatial Keyboard global manager prefab into Assets/VERA/Resources
    /// so device builds can Resources.Load it when VERA auto-spawns a keyboard for surveys.
    /// </summary>
    [InitializeOnLoad]
    internal static class SurveySpatialKeyboardResourceSync
    {

        private const string ManagerPrefabName = "XRI Global Keyboard Manager";
        private const string DestFolder = "Assets/VERA/Resources";
        private const string DestPrefabPath = DestFolder + "/" + ManagerPrefabName + ".prefab";


        static SurveySpatialKeyboardResourceSync()
        {
            EditorApplication.delayCall += SyncIfNeeded;
        }


        [MenuItem("VERA/Surveys/Sync Spatial Keyboard Resources")]
        private static void SyncFromMenu()
        {
            if (SyncIfNeeded(force: true))
                VERADebugger.Log($"Synced Spatial Keyboard manager to {DestPrefabPath}", "SurveySpatialKeyboardResourceSync", DebugPreference.Informative);
            else
                VERADebugger.LogWarning(
                    "Could not sync Spatial Keyboard manager. Import XR Interaction Toolkit → Spatial Keyboard sample first.",
                    "SurveySpatialKeyboardResourceSync");
        }


        private static void SyncIfNeeded()
        {
            SyncIfNeeded(force: false);
        }


        internal static bool SyncIfNeeded(bool force)
        {
            string sourcePath = FindSampleManagerPrefabPath();
            if (string.IsNullOrEmpty(sourcePath))
                return false;

            if (!force && File.Exists(DestPrefabPath))
            {
                // Keep an existing copy; rebuild users can force via menu
                return true;
            }

            if (!AssetDatabase.IsValidFolder("Assets/VERA"))
                AssetDatabase.CreateFolder("Assets", "VERA");
            if (!AssetDatabase.IsValidFolder(DestFolder))
                AssetDatabase.CreateFolder("Assets/VERA", "Resources");

            if (File.Exists(DestPrefabPath))
                AssetDatabase.DeleteAsset(DestPrefabPath);

            bool copied = AssetDatabase.CopyAsset(sourcePath, DestPrefabPath);
            if (!copied)
            {
                VERADebugger.LogWarning(
                    $"Failed to copy Spatial Keyboard manager from {sourcePath} to {DestPrefabPath}",
                    "SurveySpatialKeyboardResourceSync");
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }


        private static string FindSampleManagerPrefabPath()
        {
            string[] guids = AssetDatabase.FindAssets($"{ManagerPrefabName} t:Prefab");
            string bestPath = null;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    continue;

                // Skip our own Resources copy when searching for the sample source
                if (string.Equals(path, DestPrefabPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(path);
                if (!string.Equals(fileName, ManagerPrefabName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (path.IndexOf("Spatial Keyboard", StringComparison.OrdinalIgnoreCase) >= 0)
                    return path;

                bestPath ??= path;
            }

            return bestPath;
        }


        private class BuildPreprocessor : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(BuildReport report)
            {
                SyncIfNeeded(force: true);
            }
        }
    }
}
#endif
