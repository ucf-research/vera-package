using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

namespace VLAT
{
    public class AccessibilitySetup : MonoBehaviour
    {

        // AccessibilitySetup handles the VLAT setup process, and the corresponding UI.


        #region VARIABLES


        [SerializeField] private Transform displayAreaParent;
        [SerializeField] private Transform mockMenuParent;
        [SerializeField] private VLAT_MenuNavigator mockMenuTopRowNavigator;
        [SerializeField] private VLAT_MenuNavigator mockMenuBottomRowNavigator;
        [SerializeField] private Button mockMenuContinueButton;
        [SerializeField] private List<Transform> rebindButtonDisplays = new List<Transform>();
        [SerializeField] private List<TMP_Text> rebindInputTexts = new List<TMP_Text>();
        [SerializeField] private InputActionReference[] rebindActions = new InputActionReference[4];
        private int currentDisplayIndex = 0;

        private bool transitioning = false;
        private float transitionTime = .25f;

        private InputControl anyButtonControl = null;
        private InputAction rebindAction = null;


        #endregion


        #region START / END


        // Begins setup of the accessibility options and shows canvas
        //--------------------------------------//
        public void BeginAccessibilitySetup()
        //--------------------------------------//
        {
            currentDisplayIndex = 0;
            VLAT_Options.Instance.HideVlatMenu();

            // Ensure all items in display area are hidden
            foreach (Transform child in displayAreaParent)
            {
                LeanTween.cancel(child.gameObject);
                child.GetComponent<CanvasGroup>().alpha = 0f;
                child.gameObject.SetActive(false);
            }

            // Enable first item
            CanvasGroup firstItemCanvGroup = displayAreaParent.GetChild(0).GetComponent<CanvasGroup>();
            firstItemCanvGroup.gameObject.SetActive(true);
            firstItemCanvGroup.alpha = 1f;

            // Fade in menu
            LeanTween.cancel(gameObject);
            GetComponent<CanvasGroup>().LeanAlpha(1f, transitionTime);
            HandleNewDisplay();

            // Set up "any button press" listening
            StartCoroutine(WaitThenSubscribeInput());

        } // END BeginAccessibilitySetup


        // Waits a bit for initial inputs to arrive, then subscribes to input
        //--------------------------------------//
        private IEnumerator WaitThenSubscribeInput()
        //--------------------------------------//
        {
            yield return new WaitForSeconds(1f);
            InputSystem.onAnyButtonPress.Call(OnAnyButton);

        } // END WaitThenSubscribeInput


        // Ends the accessibility setup and closes the menu
        //--------------------------------------//
        private IEnumerator EndAccessibilitySetup()
        //--------------------------------------//
        {
            // Hide this menu, and show main VLAT menu
            transitioning = true;
            GetComponent<CanvasGroup>().LeanAlpha(0f, transitionTime);
            VLAT_Options.Instance.ShowVlatMenu();

            yield return new WaitForSeconds(transitionTime);

            // Deactivate
            gameObject.SetActive(false);

        } // END EndAccessibilitySetup


        // Cancels the setup and disables VLAT
        //--------------------------------------//
        public void CancelSetup()
        //--------------------------------------//
        {
            StartCoroutine(CancelSetupCoroutine());

        } // END CancelSetup


        // Coroutine for above
        //--------------------------------------//
        private IEnumerator CancelSetupCoroutine()
        //--------------------------------------//
        {
            // Hide this menu
            transitioning = true;
            GetComponent<CanvasGroup>().LeanAlpha(0f, transitionTime);

            yield return new WaitForSeconds(transitionTime);

            // Deactivate
            gameObject.SetActive(false);

        } // END CancelSetupCoroutine


        #endregion


        #region DISPLAY TRANSITIONING


        // Transitions view to a specific display by index
        //--------------------------------------//
        private void TransitionToDisplay(int displayToTransitionTo)
        //--------------------------------------//
        {
            StartCoroutine(TransitionToDisplayCoroutine(displayToTransitionTo));

        } // END TransitionToNextDisplay


        // Transitions view to the next display
        //--------------------------------------//
        private void TransitionToNextDisplay()
        //--------------------------------------//
        {
            StartCoroutine(TransitionToDisplayCoroutine(currentDisplayIndex + 1));

        } // END TransitionToNextDisplay


        // Transitions view from current display to a given display
        // Begins control of specific area or end accessibility setup, depending on new display index
        //--------------------------------------//
        private IEnumerator TransitionToDisplayCoroutine(int newDisplayIndex)
        //--------------------------------------//
        {
            // If we are already transitioning, stop
            if (transitioning)
                yield break;

            transitioning = true;

            // If the current display index is the last item in the display area, we have reached the end
            // End the setup, since we are done
            if (currentDisplayIndex == displayAreaParent.childCount - 1)
            {
                currentDisplayIndex++;
                StartCoroutine(EndAccessibilitySetup());
                yield break;
            }

            // Fade out old display and wait for it to finish fading, then fade in new display
            yield return StartCoroutine(FadeOutDisplay(currentDisplayIndex));
            currentDisplayIndex = newDisplayIndex;
            StartCoroutine(FadeInDisplay(currentDisplayIndex));

            // Call to handle this new display and setup interaction events with the display
            HandleNewDisplay();

            // Wait for transition to complete
            yield return new WaitForSeconds(transitionTime);
            transitioning = false;

        } // END TransitionToNextDisplay


        // Fades out a given display by index, then deactivates it
        //--------------------------------------//
        private IEnumerator FadeOutDisplay(int displayIndex)
        //--------------------------------------//
        {
            CanvasGroup canvGroup = displayAreaParent.GetChild(displayIndex).gameObject.GetComponent<CanvasGroup>();
            canvGroup.LeanAlpha(0f, transitionTime);

            yield return new WaitForSeconds(transitionTime);

            canvGroup.gameObject.SetActive(false);

        } // END FadeOutDisplay


        // Activates and then fades in a given display by index
        //--------------------------------------//
        private IEnumerator FadeInDisplay(int displayIndex)
        //--------------------------------------//
        {
            CanvasGroup canvGroup = displayAreaParent.GetChild(displayIndex).gameObject.GetComponent<CanvasGroup>();
            canvGroup.gameObject.SetActive(true);
            canvGroup.LeanAlpha(1f, transitionTime);

            yield return new WaitForSeconds(transitionTime);

        } // END FadeInDisplay


        #endregion


        #region HANDLE NEW DISPLAY


        // Handles a new display screen, and begins the corresponding detection events (e.g., "press any button to continue")
        //--------------------------------------//
        private void HandleNewDisplay()
        //--------------------------------------//
        {
            // Check for "special" screens first (e.g., rebind, mock menu...)
            // if it's a special screen, begin corresponding events for that special screen
            // If it's not a "special" screen, it's a "press any button to continue" screen

            // Check for rebind screen
            if (rebindButtonDisplays.Contains(displayAreaParent.GetChild(currentDisplayIndex)))
            {
                // Get specific rebind screen
                for (int i = 0; i < rebindButtonDisplays.Count; i++)
                {
                    if (rebindButtonDisplays[i] == displayAreaParent.GetChild(currentDisplayIndex))
                    {
                        StartCoroutine(HandleRebindDisplay(i));
                    }
                }
            }
            // Check for "mock menu" screen
            else if (mockMenuParent == displayAreaParent.GetChild(currentDisplayIndex))
            {
                StartCoroutine(HandleMockMenu());
            }
            // It's not a rebind or mock menu screen, meaning its a "press any button to continue" screen
            else
            {
                StartCoroutine(HandleWaitForInputDisplay());
            }

        } // END HandleNewDisplay


        // Handles a "press any button to continue" display, waiting until a button is pressed to continue
        //--------------------------------------//
        private IEnumerator HandleWaitForInputDisplay()
        //--------------------------------------//
        {
            // Wait for transition to finish
            while (transitioning)
                yield return null;

            // Wait for any button press before continuing
            yield return StartCoroutine(WaitForAnyButton());

            TransitionToNextDisplay();

        } // END HandleWaitForInputDisplay


        #endregion


        #region REBIND DISPLAY


        // Handles a rebind display, including the rebind process and re-rebinding
        //--------------------------------------//
        private IEnumerator HandleRebindDisplay(int targetRebind)
        //--------------------------------------//
        {
            rebindInputTexts[targetRebind].text = "Waiting for input...";

            // Wait for transition to finish
            while (transitioning)
                yield return null;

            // Perform interactive rebind
            rebindAction = rebindActions[targetRebind].action;
            yield return StartCoroutine(PerformRebind());
            string currentPath = TrimPath(rebindAction.bindings[0].effectivePath);

            // Check for duplicate inputs
            if (IsRebindDuplicate(targetRebind))
            {
                rebindInputTexts[targetRebind].text = "Button has been detected (" + currentPath + ").\n" +
                        "This input is already being used; press any button to begin choosing a different button.";

                // Wait for any input, then restart
                // (Additionally wait for 1 second, to prevent double-inputs)
                yield return new WaitForSeconds(1f);
                yield return StartCoroutine(WaitForAnyButton());
                StartCoroutine(HandleRebindDisplay(targetRebind));
                yield break;
            }

            rebindInputTexts[targetRebind].text = "Button has been detected (" + currentPath + ").\n" +
                "Press this button again to continue, or press any other button to redo selection.";

            // Confirm the binding using another interactive rebind
            ConfirmRebindAndContinue(targetRebind);

        } // END HandleRebindDisplay


        // Performs an interactive rebind, using rebindAction
        //--------------------------------------//
        private IEnumerator PerformRebind()
        //--------------------------------------//
        {
            // Start the rebinding process
            bool rebinding = true;

            rebindAction.Disable();

            // Set up the rebinding operation
            rebindAction.PerformInteractiveRebinding()
                .WithControlsExcluding("*/userpresence")
                .WithControlsExcluding("*/triggertouched")
                .WithControlsExcluding("*/griptouched")
                .WithControlsExcluding("*/thumbresttouched")
                .WithControlsExcluding("*/primarytouched")
                .WithControlsExcluding("*/secondarytouched")
                .WithControlsExcluding("*/thumbsticktouched")
                .WithControlsExcluding("*/thumbstick")
                .WithControlsExcluding("*/istracked")
                .OnComplete(operation =>
                {
                    // Re-enable the action
                    rebindAction.Enable();
                    rebinding = false;

                    operation.Dispose();
                })
                .Start();

            // Wait for rebinding to complete
            while (rebinding)
                yield return null;

        } // END PerformRebind


        // Confirms the binding in rebindAction via interactive rebind
        // (if the user presses the same button as in rebindAction, it has been confirmed)
        // If confirmed, continue to next display; otherwise, restart
        private void ConfirmRebindAndContinue(int currentRebindIndex)
        {
            string currentPath = TrimPathFull(rebindAction.bindings[0].effectivePath);

            // Confirm the binding using another interactive rebind
            rebindAction.Disable();
            rebindAction.PerformInteractiveRebinding()
                .WithControlsExcluding("*/userpresence")
                .WithControlsExcluding("*/triggertouched")
                .WithControlsExcluding("*/griptouched")
                .WithControlsExcluding("*/thumbresttouched")
                .WithControlsExcluding("*/primarytouched")
                .WithControlsExcluding("*/secondarytouched")
                .WithControlsExcluding("*/thumbsticktouched")
                .WithControlsExcluding("*/thumbstick")
                .WithControlsExcluding("*/istracked")
                .OnComplete(operation =>
                {
                    // If the path of the newly pressed button is equal to that stored previously, user has confirmed input
                    string pressedPath = TrimPathFull(operation.selectedControl.path);
                    if (pressedPath == currentPath)
                    {
                        // Push to VLAT menu's display
#if UNITY_2023_1_OR_NEWER
                        NewMenuNavigation menuNav = FindAnyObjectByType<NewMenuNavigation>();
#else
                    NewMenuNavigation menuNav = FindObjectOfType<NewMenuNavigation>();
#endif
                        if (menuNav != null)
                            menuNav.ResetBindingDisplayName(currentRebindIndex, PrettifyPath(rebindActions[currentRebindIndex].action.bindings[0].effectivePath));

                        // Transition to next display
                        anyButtonControl = null;
                        rebindAction.Enable();
                        TransitionToNextDisplay();
                    }
                    // If the path of the newly pressed button is equal to that stored previously, user has de-confirmed input
                    else
                    {
                        // Restart rebind process for this index
                        anyButtonControl = null;
                        rebindAction.Enable();
                        StartCoroutine(HandleRebindDisplay(currentRebindIndex));
                    }

                    operation.Dispose();
                })
                .Start();

        } // END ConfirmRebindAndContinue


        // Returns true if rebindAction's currently bound input is a duplicate input of previously bound inputs
        // Checks all inputs before currentRebindIndex, as to not use fake inputs from beyond current rebind index
        //--------------------------------------//
        private bool IsRebindDuplicate(int currentRebindIndex)
        //--------------------------------------//
        {
            string currentPath = TrimPath(rebindActions[currentRebindIndex].action.bindings[0].effectivePath);
            for (int i = 0; i < currentRebindIndex; i++)
            {
                string p = TrimPath(rebindActions[i].action.bindings[0].effectivePath);
                if (currentPath == p)
                {
                    // Attempted rebind is a duplicate input
                    return true;
                }
            }

            return false;

        } // END IsRebindDuplicate


        #endregion


        #region MOCK MENU


        // Handles the mock menu screen, and the navigation of it
        //--------------------------------------//
        private IEnumerator HandleMockMenu()
        //--------------------------------------//
        {
            // Wait for transition to finish
            while (transitioning)
                yield return null;

            // Activate VLAT navigation of the mock menu
            mockMenuTopRowNavigator.StartMenuNavigation();

        } // END HandleMockMenu


        // On mock menu re-choose controls button, go back to rebind menus
        //--------------------------------------//
        public void OnButtonReChoose()
        //--------------------------------------//
        {
            mockMenuTopRowNavigator.StopMenuNavigation(false);
            mockMenuBottomRowNavigator.StopMenuNavigation(false);

            // Index of rebind is 5 before current index
            TransitionToDisplay(currentDisplayIndex - 5);

        } // END OnButtonReChoose


        // On mock menu navigate bottom menu button, swap navigation
        //--------------------------------------//
        public void OnButtonNavigateBottom()
        //--------------------------------------//
        {
            mockMenuTopRowNavigator.StopMenuNavigation(false);
            mockMenuBottomRowNavigator.StartMenuNavigation();

        } // END OnButtonNavigateBottom


        // On mock menu navigate top menu button, swap navigation
        //--------------------------------------//
        public void OnButtonNavigateTop()
        //--------------------------------------//
        {
            mockMenuBottomRowNavigator.StopMenuNavigation(false);
            mockMenuTopRowNavigator.StartMenuNavigation();

        } // END OnButtonNavigateTop


        // On mock menu continue button, go to next display screen
        //--------------------------------------//
        public void OnButtonContinue()
        //--------------------------------------//
        {
            mockMenuTopRowNavigator.StopMenuNavigation(false);
            mockMenuBottomRowNavigator.StopMenuNavigation(false);

            TransitionToNextDisplay();

        } // END OnButtonContinue


        // On mock menu activate continue button, activate the continue button
        //--------------------------------------//
        public void OnButtonActivateContinue()
        //--------------------------------------//
        {
            mockMenuContinueButton.interactable = true;

        } // END OnButtonActivateContinue


        // On mock menu deactivate continue button, deactivate the continue button
        //--------------------------------------//
        public void OnButtonDeactivateContinue()
        //--------------------------------------//
        {
            mockMenuContinueButton.interactable = false;

        } // END OnButtonDeactivateContinue


        #endregion


        #region ANY BUTTON


        // Called when any button is pressed
        //--------------------------------------//
        private void OnAnyButton(InputControl control)
        //--------------------------------------//
        {
            // Exclude specific VR events which are irrelevant
            if (control.name.ToLower().Contains("touched") || control.name.ToLower().Contains("presence"))
                return;

            anyButtonControl = control;

        } // END OnAnyButton


        // Waits for any button to be pressed
        //--------------------------------------//
        private IEnumerator WaitForAnyButton()
        //--------------------------------------//
        {
            anyButtonControl = null;

            while (anyButtonControl == null)
                yield return null;

            anyButtonControl = null;

        } // END WaitForAnyButton


        #endregion


        #region STRING HELPERS


        // Prettifies display name (e.g., "primarybuttonpressed" to "Primary Button")
        //--------------------------------------//
        private string PrettifyDisplayName(string name)
        //--------------------------------------//
        {
            if (string.IsNullOrEmpty(name))
                return "Unknown Control";

            // Remove "pressed" (e.g., "primarybuttonpressed" to "primarybutton")
            if (name.Contains("pressed"))
                name = name.Replace("pressed", "");

            // Replace "secondary" with "second" (for space constraints)
            if (name.Contains("secondary"))
                name = name.Replace("secondary", "second");

            // Add space before button, trigger, or grip (e.g., "primarybuttonpressed" to "primary button")
            string pattern = @"(?<!\s)(button|trigger|grip)";
            name = Regex.Replace(name, pattern, " $1");

            // Insert spaces before uppercase letters or numbers (e.g., PrimaryButton to Primary Button)
            name = Regex.Replace(name, "(\\B[A-Z])", " $1");

            // Capitalize the first letter of each word (e.g., primary button to Primary Button)
            name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());

            return name;

        } // END PrettifyDisplayName


        // Trims path name (e.g., "<Keyboard>/space" to "Keyboard/space", or "/Keyboard/space" to "Keyboard/space")
        //--------------------------------------//
        private string TrimPath(string path)
        //--------------------------------------//
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // Remove '<' and '>' characters
            path = path.Replace("<", "").Replace(">", "");

            // Remove trailing '/' if present
            if (path.EndsWith("/"))
                path = path.TrimEnd('/');

            // Remove leading '/' if present
            if (path.StartsWith("/"))
                path = path.TrimStart('/');

            return path;

        } // END TrimPath


        // Fully trims path name (e.g., "OculusTouchController{RightHand}/triggerpressed" to "triggerpressed")
        // If it doesn't include "Oculus", simplifies to normal TrimPath.
        //--------------------------------------//
        private string TrimPathFull(string path)
        //--------------------------------------//
        {
            if (path.ToLower().Contains("oculus"))
            {
                int lastSlashIndex = path.LastIndexOf('/');
                if (lastSlashIndex != -1)
                    return path.Substring(lastSlashIndex + 1);
            }

            return TrimPath(path);

        } // END TrimPathFull


        // Prettifies path name (e.g., "<Mouse>/leftButton" to "Left Button")
        //--------------------------------------//
        private string PrettifyPath(string path)
        //--------------------------------------//
        {
            // Remove everything before the final "/" (e.g., "<Mouse>/leftButton" becomes "leftButton")
            int lastSlashIndex = path.LastIndexOf('/');
            if (lastSlashIndex != -1)
                path = path.Substring(lastSlashIndex + 1);

            // Remove "pressed" (e.g., "primarybuttonpressed" to "primarybutton")
            if (path.Contains("pressed"))
                path = path.Replace("pressed", "");

            // Replace "secondary" with "second" (for space constraints)
            if (path.Contains("secondary"))
                path = path.Replace("secondary", "second");

            // Add space before button, trigger, or grip (e.g., "primarybuttonpressed" to "primary button")
            string pattern = @"(?<!\s)(button|trigger|grip)";
            path = Regex.Replace(path, pattern, " $1");

            // Insert spaces before uppercase letters or numbers (e.g., "leftButton" to "left Button")
            path = Regex.Replace(path, "(\\B[A-Z])", " $1");

            // Capitalize the first letter of each word (e.g., "left Button" to "Left Button")
            path = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(path.ToLower());

            return path;

        } // END PrettifyPath


        #endregion


    } // END AccessibilitySetup.cs
}