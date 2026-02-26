using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VLAT
{
    public class VLAT_Options : MonoBehaviour
    {

        // VLAT_Options stores and distributes the various options for VLAT customization.


        #region VARIABLES


        public static VLAT_Options Instance;

        [Tooltip("Whether the user should be presented with a control setup screen upon launch (HIGHLY RECOMMENDED)")]
        [SerializeField] private bool showSetupOnStart = true;

        [Header("XR Player")]
        [Tooltip("The XR player's parent game object (e.g., for the XR Interaction Toolkit, the XR Origin game object)")]
        [SerializeField] private GameObject xrPlayerParent;
        [Tooltip("The radius of the player's movement collider")]
        [SerializeField] private float xrPlayerRadius = 0.2f;
        [Tooltip("The height of the player's movement collider")]
        [SerializeField] private float xrPlayerHeight = 2.0f;

        [Header("Movement")]
        [Tooltip("The player's default movement speed, when using VLAT movement")]
        [SerializeField] private float playerMoveSpeed = 2f;

        [Header("Interaction")]
        [Tooltip("The maximum distance from which interactable objects may be interacted with")]
        [SerializeField] private float interactionRadius = 10f;

        [Header("Virtual Hands")]
        [Tooltip("Whether the player has virtual hands present while playing")]
        [SerializeField] private bool playerHasVirtualHands = true;
        [Tooltip("A reference to the player's left virtual hand (only required if playerHasVirtualHands = true)")]
        [SerializeField] private GameObject leftVirtualHand;
        [Tooltip("A reference to the player's right virtual hand (only required if playerHasVirtualHands = true)")]
        [SerializeField] private GameObject rightVirtualHand;

        private NewMenuNavigation menuNav;


        #endregion


        #region SETUP


        // Start, distribute settings
        //--------------------------------------//
        void Start()
        //--------------------------------------//
        {
            SetupSingleton();
            SetupMovement();
            SetupInteraction();
            SetupSettings();
            SetupMenu();

        } // END Start


        // Sets up as singleton
        //---------------------------------------//
        private void SetupSingleton()
        //---------------------------------------//
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

        } // END SetupSingleton


        // Sets up the movement / character controller based on options
        //--------------------------------------//
        private void SetupMovement()
        //--------------------------------------//
        {
            if (xrPlayerParent == null)
            {
                Debug.LogError("No XR player parent given for VLAT options. Please provide one for movement to work.");
                return;
            }

#if UNITY_2023_1_OR_NEWER
            MovementController moveControl = FindAnyObjectByType<MovementController>();
#else
        MovementController moveControl = FindObjectOfType<MovementController>();
#endif

            if (moveControl != null)
                moveControl.Setup(xrPlayerParent, xrPlayerRadius, xrPlayerHeight, playerMoveSpeed);
            else
                Debug.LogError("No VLAT MovementController could be found in scene; VLAT movement will not work.");

        } // END SetupMovement


        // Sets up interaction based on options
        //--------------------------------------//
        private void SetupInteraction()
        //--------------------------------------//
        {
#if UNITY_2023_1_OR_NEWER
            SelectionController selectControl = FindAnyObjectByType<SelectionController>();
#else
        SelectionController selectControl = FindObjectOfType<SelectionController>();
#endif

            if (selectControl != null)
                selectControl.Setup(interactionRadius);
            else
                Debug.LogError("No VLAT SelectionController could be found in scene; VLAT interaction will not work.");

        } // END SetupInteraction


        // Sets up Settings based on options
        //--------------------------------------//
        private void SetupSettings()
        //--------------------------------------//
        {
#if UNITY_2023_1_OR_NEWER
            SettingsManager settingsControl = FindAnyObjectByType<SettingsManager>();
#else
        SettingsManager settingsControl = FindObjectOfType<SettingsManager>();
#endif

            if (settingsControl != null)
                settingsControl.Setup(playerHasVirtualHands, leftVirtualHand, rightVirtualHand);
            else
                Debug.LogError("No VLAT SettingsManager could be found in scene; VLAT settings functionality will not work.");

        } // END SetupSettings


        // Sets up menu
        //--------------------------------------//
        private void SetupMenu()
        //--------------------------------------//
        {
#if UNITY_2023_1_OR_NEWER
            menuNav = FindAnyObjectByType<NewMenuNavigation>();
#else
        menuNav = FindObjectOfType<NewMenuNavigation>();
#endif
            menuNav.Setup();

            if (showSetupOnStart)
                ShowSetupOptions();

        } // END SetupMenu


        #endregion


        #region ON / OFF


        // Hides the VLAT menu
        //--------------------------------------//
        public void HideVlatMenu()
        //--------------------------------------//
        {
#if UNITY_2023_1_OR_NEWER
            FindAnyObjectByType<NewMenuNavigation>().HideVlatMenu();
#else
        FindObjectOfType<NewMenuNavigation>().HideVlatMenu();
#endif

        } // END HideVlatMenu


        // Shows the VLAT menu
        //--------------------------------------//
        public void ShowVlatMenu()
        //--------------------------------------//
        {
#if UNITY_2023_1_OR_NEWER
            FindAnyObjectByType<NewMenuNavigation>().ShowVlatMenu();
#else
        FindObjectOfType<NewMenuNavigation>().ShowVlatMenu();
#endif

        } // END ShowVlatMenu


        #endregion


        #region OTHER


        // Gets the XR player parent
        //--------------------------------------//
        public GameObject GetXrPlayerParent()
        //--------------------------------------//
        {
            return xrPlayerParent;

        } // END GetXrPlayerParent


        // Shows the accessibility setup and rebind screen
        //--------------------------------------//
        public void ShowSetupOptions()
        //--------------------------------------//
        {
            menuNav.ShowSetupOptions();

        } // END ShowSetupOptions


        #endregion


    } // END VLAT_Options.cs
}