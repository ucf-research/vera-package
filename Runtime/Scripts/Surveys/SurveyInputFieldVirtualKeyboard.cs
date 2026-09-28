// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

using System;
using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace VERA
{
    /// <summary>
    /// Bridges survey TMP_InputFields to the XR Interaction Toolkit Spatial Keyboard sample.
    /// Drives GlobalNonNativeKeyboard directly and syncs text locally — avoids XRKeyboardDisplay,
    /// which can NRE when opened in the same frame it is created (m_ActiveKeyboard not set until Start).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_InputField))]
    internal class SurveyInputFieldVirtualKeyboard : MonoBehaviour, IPointerClickHandler
    {

        #region CONSTANTS / CACHE


        private const string SpatialKeyboardAssembly = "Unity.XR.Interaction.Toolkit.Samples.SpatialKeyboard";
        private const string GlobalKeyboardTypeName = "UnityEngine.XR.Interaction.Toolkit.Samples.SpatialKeyboard.GlobalNonNativeKeyboard";
        private const string KeyboardTypeName = "UnityEngine.XR.Interaction.Toolkit.Samples.SpatialKeyboard.XRKeyboard";
        private const string DisplayTypeName = "UnityEngine.XR.Interaction.Toolkit.Samples.SpatialKeyboard.XRKeyboardDisplay";

        private static bool typesResolved;
        private static bool spatialKeyboardAvailable;
        private static Type globalKeyboardType;
        private static Type keyboardType;
        private static Type displayType;
        private static PropertyInfo globalInstanceProperty;
        private static PropertyInfo globalKeyboardProperty;
        private static MethodInfo showKeyboardMethod;
        private static PropertyInfo keyboardTextProperty;
        private static PropertyInfo keyboardIsOpenProperty;
        private static PropertyInfo keyboardCaretProperty;
        private static bool warnedMissingSample;
        private static bool warnedMissingManager;


        #endregion


        #region VARIABLES


        private TMP_InputField inputField;
        private bool isOpeningKeyboard;
        private object observedKeyboard;
        private string lastSyncedText;
        private bool hasSeenKeyboardOpen;
        private int openGraceFramesRemaining;
        private Coroutine deferredOpenCoroutine;


        #endregion


        #region LIFECYCLE


        private void Awake()
        {
            inputField = GetComponent<TMP_InputField>();
            inputField.shouldHideSoftKeyboard = true;
            inputField.shouldHideMobileInput = true;
            inputField.resetOnDeActivation = false;

            // XRKeyboardDisplay races with Start() and can NRE; we sync text ourselves instead.
            RemoveConflictingSpatialDisplay();
        }


        private void OnDestroy()
        {
            CancelDeferredOpen();
            StopObservingKeyboard();
        }


        private void OnDisable()
        {
            CancelDeferredOpen();
            // Keep observing if the keyboard is already open; only clear when this field is being torn down.
            // Stopping here breaks Other auto-open: the field enables, opens the keyboard, then a same-frame
            // enable/disable cycle can wipe observation before the first key press.
        }


        private void Update()
        {
            if (observedKeyboard == null)
                return;

            if (openGraceFramesRemaining > 0)
                openGraceFramesRemaining--;

            bool isOpen = keyboardIsOpenProperty != null && (bool)keyboardIsOpenProperty.GetValue(observedKeyboard);
            if (isOpen)
                hasSeenKeyboardOpen = true;

            if (!isOpen)
            {
                // Keyboard may report closed for a frame or two while activating; don't drop observation yet.
                if (!hasSeenKeyboardOpen || openGraceFramesRemaining > 0)
                    return;

                ApplyKeyboardText();
                StopObservingKeyboard();
                return;
            }

            string keyboardText = keyboardTextProperty?.GetValue(observedKeyboard) as string ?? string.Empty;
            if (keyboardText == lastSyncedText)
                return;

            lastSyncedText = keyboardText;
            ApplyTextToField(keyboardText);
        }


        #endregion


        #region PUBLIC API


        /// <summary>
        /// Ensures a survey input field has Spatial Keyboard support attached.
        /// </summary>
        public static void EnsureOn(TMP_InputField field)
        {
            if (field == null)
                return;

            if (field.GetComponent<SurveyInputFieldVirtualKeyboard>() == null)
                field.gameObject.AddComponent<SurveyInputFieldVirtualKeyboard>();
        }


        /// <summary>
        /// Clears the one-shot "manager missing" warning so a later auto-spawn can report cleanly.
        /// </summary>
        public static void ClearMissingManagerWarning()
        {
            warnedMissingManager = false;
        }


        /// <summary>
        /// Activates the input field and opens the XRI Spatial Keyboard.
        /// </summary>
        public void FocusAndOpenKeyboard()
        {
            if (isOpeningKeyboard || inputField == null || !inputField.isActiveAndEnabled)
                return;

            SurveySpatialKeyboardBootstrap.EnsureInScene();

            if (!EnsureSpatialKeyboardReady())
                return;

            isOpeningKeyboard = true;
            try
            {
                // Avoid EventSystem.SetSelectedGameObject here — it can re-enter TMP onSelect paths.
                inputField.ActivateInputField();
                ShowGlobalKeyboardAndObserve();
            }
            finally
            {
                isOpeningKeyboard = false;
            }
        }


        /// <summary>
        /// Opens the keyboard after a short delay so newly-enabled Other fields and XR UI click
        /// handling can finish before the Spatial Keyboard is shown.
        /// </summary>
        public void FocusAndOpenKeyboardDeferred(int framesToWait = 2)
        {
            CancelDeferredOpen();
            if (!isActiveAndEnabled)
                return;

            deferredOpenCoroutine = StartCoroutine(FocusAndOpenKeyboardDeferredCoroutine(framesToWait));
        }


        #endregion


        #region EVENT HANDLERS


        public void OnPointerClick(PointerEventData eventData)
        {
            FocusAndOpenKeyboard();
        }


        #endregion


        #region SPATIAL KEYBOARD


        private IEnumerator FocusAndOpenKeyboardDeferredCoroutine(int framesToWait)
        {
            // Wait until end of frame so the current XR UI click finishes, then a couple of frames
            // for the newly activated input field to finish enable/layout.
            yield return new WaitForEndOfFrame();
            for (int i = 0; i < framesToWait; i++)
                yield return null;

            deferredOpenCoroutine = null;

            if (inputField == null || !inputField.isActiveAndEnabled || !isActiveAndEnabled)
                yield break;

            FocusAndOpenKeyboard();
        }


        private void CancelDeferredOpen()
        {
            if (deferredOpenCoroutine == null)
                return;

            StopCoroutine(deferredOpenCoroutine);
            deferredOpenCoroutine = null;
        }


        private void RemoveConflictingSpatialDisplay()
        {
            ResolveSpatialKeyboardTypes();
            if (displayType == null)
                return;

            Component existing = GetComponent(displayType);
            if (existing != null)
                Destroy(existing);
        }


        private bool EnsureSpatialKeyboardReady()
        {
            ResolveSpatialKeyboardTypes();

            if (!spatialKeyboardAvailable)
            {
                if (!warnedMissingSample)
                {
                    warnedMissingSample = true;
                    VERADebugger.LogError(
                        "XR Interaction Toolkit Spatial Keyboard sample was not found. " +
                        "Import it via Package Manager → XR Interaction Toolkit → Samples → Spatial Keyboard.",
                        "SurveyInputFieldVirtualKeyboard");
                }
                return false;
            }

            object instance = globalInstanceProperty?.GetValue(null);
            if (instance == null)
            {
                if (!warnedMissingManager)
                {
                    warnedMissingManager = true;
                    VERADebugger.LogError(
                        "Spatial Keyboard sample is imported, but no GlobalNonNativeKeyboard is available. " +
                        "VERA tried to auto-spawn 'XRI Global Keyboard Manager' and failed — " +
                        "use menu VERA → Surveys → Sync Spatial Keyboard Resources, then try again.",
                        "SurveyInputFieldVirtualKeyboard");
                }
                return false;
            }

            return true;
        }


        private void ShowGlobalKeyboardAndObserve()
        {
            if (!EnsureSpatialKeyboardReady())
                return;

            object instance = globalInstanceProperty.GetValue(null);
            if (instance == null || showKeyboardMethod == null)
                return;

            // Disable/undo KeyboardOptimizer before first activation so key outlines keep layout positions.
            SurveySpatialKeyboardBootstrap.EnsureInScene();

            showKeyboardMethod.Invoke(instance, new object[] { inputField, true });
            Canvas.ForceUpdateCanvases();

            object keyboard = globalKeyboardProperty?.GetValue(instance);
            if (keyboard == null)
            {
                VERADebugger.LogWarning(
                    "GlobalNonNativeKeyboard.keyboard is null after ShowKeyboard. Check that the manager's Keyboard Prefab is assigned.",
                    "SurveyInputFieldVirtualKeyboard");
                return;
            }

            observedKeyboard = keyboard;
            hasSeenKeyboardOpen = false;
            openGraceFramesRemaining = 10;
            lastSyncedText = keyboardTextProperty?.GetValue(keyboard) as string ?? string.Empty;
            ApplyTextToField(lastSyncedText);
        }


        private void ApplyKeyboardText()
        {
            if (observedKeyboard == null || keyboardTextProperty == null)
                return;

            string keyboardText = keyboardTextProperty.GetValue(observedKeyboard) as string ?? string.Empty;
            ApplyTextToField(keyboardText);
        }


        private void ApplyTextToField(string text)
        {
            if (inputField == null)
                return;

            if (inputField.characterLimit > 0 && text != null && text.Length > inputField.characterLimit)
                text = text.Substring(0, inputField.characterLimit);

            inputField.SetTextWithoutNotify(text ?? string.Empty);

            int caret = text?.Length ?? 0;
            if (keyboardCaretProperty != null && observedKeyboard != null)
            {
                try
                {
                    caret = (int)keyboardCaretProperty.GetValue(observedKeyboard);
                }
                catch
                {
                    // Fall back to end of string
                }
            }

            caret = Mathf.Clamp(caret, 0, inputField.text.Length);
            inputField.caretPosition = caret;
            inputField.stringPosition = caret;
        }


        private void StopObservingKeyboard()
        {
            observedKeyboard = null;
            lastSyncedText = null;
            hasSeenKeyboardOpen = false;
            openGraceFramesRemaining = 0;
        }


        private static void ResolveSpatialKeyboardTypes()
        {
            if (typesResolved)
                return;

            typesResolved = true;

            globalKeyboardType = FindType(GlobalKeyboardTypeName, SpatialKeyboardAssembly);
            keyboardType = FindType(KeyboardTypeName, SpatialKeyboardAssembly);
            displayType = FindType(DisplayTypeName, SpatialKeyboardAssembly);

            if (globalKeyboardType == null || keyboardType == null)
            {
                spatialKeyboardAvailable = false;
                return;
            }

            globalInstanceProperty = globalKeyboardType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
            globalKeyboardProperty = globalKeyboardType.GetProperty("keyboard", BindingFlags.Public | BindingFlags.Instance);
            showKeyboardMethod = globalKeyboardType.GetMethod("ShowKeyboard", new[] { typeof(TMP_InputField), typeof(bool) });

            keyboardTextProperty = keyboardType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            keyboardIsOpenProperty = keyboardType.GetProperty("isOpen", BindingFlags.Public | BindingFlags.Instance);
            keyboardCaretProperty = keyboardType.GetProperty("caretPosition", BindingFlags.Public | BindingFlags.Instance);

            spatialKeyboardAvailable =
                globalInstanceProperty != null &&
                globalKeyboardProperty != null &&
                showKeyboardMethod != null &&
                keyboardTextProperty != null &&
                keyboardIsOpenProperty != null;
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
