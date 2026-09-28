// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

using System;
using System.Reflection;
using Unity.XR.CoreUtils;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VERA
{
    /// <summary>
    /// Ensures an XRI Spatial Keyboard global manager exists when VR surveys run.
    /// Reuses an existing GlobalNonNativeKeyboard if present; otherwise instantiates one from the sample prefab.
    /// </summary>
    internal static class SurveySpatialKeyboardBootstrap
    {

        #region CONSTANTS


        private const string SpatialKeyboardAssembly = "Unity.XR.Interaction.Toolkit.Samples.SpatialKeyboard";
        private const string GlobalKeyboardTypeName = "UnityEngine.XR.Interaction.Toolkit.Samples.SpatialKeyboard.GlobalNonNativeKeyboard";
        private const string OptimizerTypeName = "UnityEngine.XR.Interaction.Toolkit.Samples.SpatialKeyboard.KeyboardOptimizer";
        private const string ManagerPrefabName = "XRI Global Keyboard Manager";
        private const string ResourcesPrefabPath = "XRI Global Keyboard Manager";
        private const string ResourcesPrefabPathNested = "VERA/XRI Global Keyboard Manager";


        #endregion


        #region PUBLIC API


        /// <summary>
        /// Ensures a Spatial Keyboard global manager is available in the scene for survey text input.
        /// </summary>
        public static void EnsureInScene()
        {
            Component existing = FindExistingManager();
            if (existing != null)
            {
                FixKeyboardOptimizer(existing);
                VERADebugger.Log(
                    "Using existing XRI Spatial Keyboard manager in the scene.",
                    "SurveySpatialKeyboardBootstrap",
                    DebugPreference.Verbose);
                return;
            }

            GameObject prefab = LoadManagerPrefab();
            if (prefab == null)
            {
                VERADebugger.LogError(
                    "Could not find the XRI Spatial Keyboard manager prefab. " +
                    "Import Package Manager → XR Interaction Toolkit → Samples → Spatial Keyboard, " +
                    "then re-enter Play Mode (VERA will copy it into Assets/VERA/Resources for builds).",
                    "SurveySpatialKeyboardBootstrap");
                return;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = ManagerPrefabName + " (VERA)";

            ConfigureManager(instance);
            FixKeyboardOptimizer(instance.GetComponent(FindType(GlobalKeyboardTypeName, SpatialKeyboardAssembly)));
            SurveyInputFieldVirtualKeyboard.ClearMissingManagerWarning();

            VERADebugger.Log(
                "Spawned XRI Spatial Keyboard manager for survey text input.",
                "SurveySpatialKeyboardBootstrap",
                DebugPreference.Informative);
        }


        #endregion


        #region FIND / LOAD


        private static Component FindExistingManager()
        {
            Type globalType = FindType(GlobalKeyboardTypeName, SpatialKeyboardAssembly);
            if (globalType == null)
                return null;

            // Prefer the singleton instance property when already initialized
            PropertyInfo instanceProp = globalType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProp != null)
            {
                object singleton = instanceProp.GetValue(null);
                if (singleton is Component existingComponent && existingComponent != null)
                    return existingComponent;
            }

#if UNITY_2023_1_OR_NEWER
            UnityEngine.Object[] found = UnityEngine.Object.FindObjectsByType(globalType, FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            UnityEngine.Object[] found = UnityEngine.Object.FindObjectsOfType(globalType, true);
#endif
            if (found != null && found.Length > 0)
                return found[0] as Component;

            return null;
        }


        private static GameObject LoadManagerPrefab()
        {
            GameObject fromResources =
                Resources.Load<GameObject>(ResourcesPrefabPath) ??
                Resources.Load<GameObject>(ResourcesPrefabPathNested);

            if (fromResources != null)
                return fromResources;

#if UNITY_EDITOR
            return LoadManagerPrefabFromAssetDatabase();
#else
            return null;
#endif
        }


#if UNITY_EDITOR
        private static GameObject LoadManagerPrefabFromAssetDatabase()
        {
            string[] guids = AssetDatabase.FindAssets($"{ManagerPrefabName} t:Prefab");
            string bestPath = null;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    continue;

                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!string.Equals(fileName, ManagerPrefabName, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Prefer the Spatial Keyboard sample copy over any VERA Resources duplicate
                if (path.IndexOf("Spatial Keyboard", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    bestPath = path;
                    break;
                }

                bestPath ??= path;
            }

            if (bestPath == null)
                return null;

            return AssetDatabase.LoadAssetAtPath<GameObject>(bestPath);
        }
#endif


        private static void ConfigureManager(GameObject managerInstance)
        {
            Type globalType = FindType(GlobalKeyboardTypeName, SpatialKeyboardAssembly);
            if (globalType == null)
                return;

            Component manager = managerInstance.GetComponent(globalType);
            if (manager == null)
                return;

            XROrigin origin = UnityEngine.Object.FindAnyObjectByType<XROrigin>();
            Transform playerRoot = null;
            if (origin != null)
            {
                playerRoot = origin.CameraFloorOffsetObject != null
                    ? origin.CameraFloorOffsetObject.transform
                    : origin.transform;

                PropertyInfo playerRootProp = globalType.GetProperty("playerRoot", BindingFlags.Public | BindingFlags.Instance);
                playerRootProp?.SetValue(manager, playerRoot);
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null && origin != null)
                mainCamera = origin.Camera;

            if (mainCamera != null)
            {
                PropertyInfo cameraProp = globalType.GetProperty("cameraTransform", BindingFlags.Public | BindingFlags.Instance);
                cameraProp?.SetValue(manager, mainCamera.transform);
            }

            // Awake already instantiated the keyboard under the prefab's (often null) player root.
            // Reparent under the XR origin so scale/pose stay consistent when the keyboard opens.
            if (playerRoot != null)
            {
                PropertyInfo keyboardProp = globalType.GetProperty("keyboard", BindingFlags.Public | BindingFlags.Instance);
                if (keyboardProp?.GetValue(manager) is Component keyboardComponent && keyboardComponent != null)
                    keyboardComponent.transform.SetParent(playerRoot, true);
            }
        }


        /// <summary>
        /// The sample's KeyboardOptimizer reparents key graphics for batching. If it runs before
        /// layout groups finish positioning keys (common when the keyboard first activates from inactive),
        /// highlights/outlines collapse into a pile at the keyboard center. Disable optimize-on-start
        /// and undo any already-applied broken optimize.
        /// </summary>
        private static void FixKeyboardOptimizer(Component globalManager)
        {
            if (globalManager == null)
                return;

            Type globalType = globalManager.GetType();
            PropertyInfo keyboardProp = globalType.GetProperty("keyboard", BindingFlags.Public | BindingFlags.Instance);
            object keyboard = keyboardProp?.GetValue(globalManager);
            if (keyboard is not Component keyboardComponent || keyboardComponent == null)
                return;

            Type optimizerType = FindType(OptimizerTypeName, SpatialKeyboardAssembly);
            if (optimizerType == null)
                return;

            Component optimizer = keyboardComponent.GetComponentInChildren(optimizerType, true);
            if (optimizer == null)
                return;

            PropertyInfo optimizeOnStartProp = optimizerType.GetProperty("optimizeOnStart", BindingFlags.Public | BindingFlags.Instance);
            optimizeOnStartProp?.SetValue(optimizer, false);

            PropertyInfo isOptimizedProp = optimizerType.GetProperty("isCurrentlyOptimized", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo unoptimizeMethod = optimizerType.GetMethod("Unoptimize", BindingFlags.Public | BindingFlags.Instance);
            if (isOptimizedProp != null && unoptimizeMethod != null && (bool)isOptimizedProp.GetValue(optimizer))
            {
                unoptimizeMethod.Invoke(optimizer, null);
                VERADebugger.Log(
                    "Reverted Spatial Keyboard batching optimize so key outlines keep their layout positions.",
                    "SurveySpatialKeyboardBootstrap",
                    DebugPreference.Verbose);
            }

            Canvas.ForceUpdateCanvases();
        }


        private static Type FindType(string fullTypeName, string assemblyName)
        {
            Type type = Type.GetType($"{fullTypeName}, {assemblyName}");
            if (type != null)
                return type;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal))
                    continue;

                type = assembly.GetType(fullTypeName);
                if (type != null)
                    return type;
            }

            return null;
        }


        #endregion


    }
}
