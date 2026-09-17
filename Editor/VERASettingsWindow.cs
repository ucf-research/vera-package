#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VERA
{
    /// <summary>
    /// Experiment setup, authentication, and recording preferences.
    /// Uses UI Toolkit to match VERA's other editor windows.
    /// </summary>
    internal class VERASettingsWindow : EditorWindow
    {

        #region COLORS

        private static readonly Color VERA_PURPLE = new Color(106f / 255f, 44f / 255f, 145f / 255f);
        private static readonly Color VERA_PURPLE_LIGHT = new Color(204f / 255f, 165f / 255f, 227f / 255f);
        private static readonly Color VERA_PURPLE_HOVER = new Color(126f / 255f, 58f / 255f, 168f / 255f);
        private static readonly Color BG_DARK = new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color BG_CARD = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color BG_CARD_HEADER_HOVER = new Color(0.22f, 0.22f, 0.22f);
        private static readonly Color BG_INPUT = new Color(0.14f, 0.14f, 0.14f);
        private static readonly Color BG_SECONDARY_BTN = new Color(0.25f, 0.25f, 0.25f);
        private static readonly Color BG_SECONDARY_BTN_HOVER = new Color(0.32f, 0.32f, 0.32f);
        private static readonly Color TEXT_PRIMARY = new Color(0.92f, 0.92f, 0.92f);
        private static readonly Color TEXT_SECONDARY = new Color(0.68f, 0.68f, 0.68f);
        private static readonly Color TEXT_MUTED = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color BORDER_SUBTLE = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color COLOR_SUCCESS = new Color(0.35f, 0.78f, 0.48f);
        private static readonly Color COLOR_ERROR = new Color(0.9f, 0.32f, 0.32f);
        private static readonly Color COLOR_INFO_BG = new Color(0.22f, 0.2f, 0.25f);
        private static readonly Color COLOR_ERROR_BG = new Color(0.28f, 0.16f, 0.16f);
        private static readonly Color BG_SETUP_GROUP = new Color(1f, 1f, 1f, 0.035f);
        private static readonly Color BORDER_HAIRLINE = new Color(1f, 1f, 1f, 0.08f);

        #endregion


        #region STATE

        private int selectedExperimentIndex;
        private int selectedSiteIndex;
        private List<Experiment> experimentList = null;
        private string timeExperimentsLastRefreshed = string.Empty;
        private Dictionary<string, IVGroup> ivFetchCache = new Dictionary<string, IVGroup>();

        private bool experimentFoldout = true;
        private bool dataRecordingFoldout = true;
        private bool buildUploadFoldout = true;

        private bool uiReady;
        private int lastAuthState = -1;
        private string lastSetupFingerprint = string.Empty;
        private double lastSetupFingerprintCheck;
        private Label headerSubtitleLabel;
        private VisualElement headerChipRow;
        private VisualElement actionBar;
        private ScrollView scrollView;
        private VisualElement contentContainer;
        private bool isLoadingExperimentSetup;
        private int experimentSetupLoadGeneration;
        private string experimentSetupLoadMessage = "Refreshing…";

        #endregion


        #region SHOW WINDOW

        [MenuItem("VERA/Settings")]
        public static void ShowWindow()
        {
            VERASettingsWindow window = GetWindow<VERASettingsWindow>("VERA Settings");
            window.minSize = new Vector2(480, 420);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += WatchAuthState;
        }

        private void OnDisable()
        {
            EditorApplication.update -= WatchAuthState;
        }

        /// <summary>
        /// Rebuild when login state changes, and when local experiment assets change after a refresh.
        /// </summary>
        private void WatchAuthState()
        {
            if (!uiReady)
                return;

            int auth = PlayerPrefs.GetInt("VERA_UserAuthenticated");
            if (auth != lastAuthState)
            {
                lastAuthState = auth;
                RebuildContent();
                return;
            }

            if (auth != 1)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now - lastSetupFingerprintCheck < 0.5)
                return;

            lastSetupFingerprintCheck = now;
            if (ComputeSetupFingerprint() != lastSetupFingerprint)
                RebuildContent();
        }

        #endregion


        #region UI CREATION

        private void CreateGUI()
        {
            lastAuthState = PlayerPrefs.GetInt("VERA_UserAuthenticated");

            VisualElement root = rootVisualElement;
            root.style.backgroundColor = BG_DARK;
            root.style.paddingTop = 0;
            root.style.paddingBottom = 20;
            root.style.paddingLeft = 24;
            root.style.paddingRight = 24;

            BuildHeader(root);
            BuildActionBar(root);

            scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            root.Add(scrollView);

            contentContainer = new VisualElement();
            contentContainer.style.paddingTop = 4;
            scrollView.Add(contentContainer);

            uiReady = true;
            RebuildContent();
        }

        private void BuildHeader(VisualElement root)
        {
            VisualElement header = new VisualElement();
            header.style.backgroundColor = VERA_PURPLE;
            header.style.marginLeft = -24;
            header.style.marginRight = -24;
            header.style.paddingTop = 22;
            header.style.paddingBottom = 22;
            header.style.paddingLeft = 24;
            header.style.paddingRight = 24;
            header.style.marginBottom = 16;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            Texture2D logoTexture = LoadHeaderLogo();
            if (logoTexture != null)
            {
                Image logo = new Image
                {
                    image = logoTexture,
                    scaleMode = ScaleMode.ScaleToFit
                };
                logo.style.width = 48;
                logo.style.height = 48;
                logo.style.flexShrink = 0;
                logo.style.marginRight = 14;
                logo.style.borderTopLeftRadius = 24;
                logo.style.borderTopRightRadius = 24;
                logo.style.borderBottomLeftRadius = 24;
                logo.style.borderBottomRightRadius = 24;
                logo.style.overflow = Overflow.Hidden;
                header.Add(logo);
            }

            VisualElement titleColumn = new VisualElement();
            titleColumn.style.flexGrow = 1;
            titleColumn.style.flexShrink = 1;

            Label titleLabel = new Label("VERA Settings");
            titleLabel.style.fontSize = 22;
            titleLabel.style.color = Color.white;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 2;
            titleColumn.Add(titleLabel);

            headerSubtitleLabel = new Label();
            headerSubtitleLabel.style.fontSize = 13;
            headerSubtitleLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            headerSubtitleLabel.style.whiteSpace = WhiteSpace.Normal;
            titleColumn.Add(headerSubtitleLabel);

            headerChipRow = new VisualElement();
            headerChipRow.style.flexDirection = FlexDirection.Row;
            headerChipRow.style.flexWrap = Wrap.Wrap;
            headerChipRow.style.marginTop = 8;
            titleColumn.Add(headerChipRow);

            header.Add(titleColumn);
            root.Add(header);
        }

        private void BuildActionBar(VisualElement root)
        {
            actionBar = new VisualElement();
            actionBar.style.flexDirection = FlexDirection.Row;
            actionBar.style.flexWrap = Wrap.Wrap;
            actionBar.style.marginBottom = 8;
            root.Add(actionBar);
        }

        private void ScheduleRebuild()
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                    RebuildContent();
            };
        }

        private void RebuildContent()
        {
            if (!uiReady || contentContainer == null)
                return;

            Vector2 savedScroll = scrollView != null ? scrollView.scrollOffset : Vector2.zero;

            bool authenticated = PlayerPrefs.GetInt("VERA_UserAuthenticated") == 1;
            if (!authenticated)
                isLoadingExperimentSetup = false;
            UpdateHeader(authenticated);
            RebuildActionBar(authenticated);

            contentContainer.Clear();

            if (authenticated)
            {
                if (experimentList == null)
                    LoadSettings();

                BuildAuthenticatedContent();
            }
            else
            {
                BuildUnauthenticatedContent();
            }

            if (scrollView != null)
            {
                Vector2 restore = savedScroll;
                scrollView.schedule.Execute(() =>
                {
                    if (scrollView != null)
                        scrollView.scrollOffset = restore;
                });
            }

            lastSetupFingerprint = ComputeSetupFingerprint();
            lastSetupFingerprintCheck = EditorApplication.timeSinceStartup;
        }

        private void UpdateHeader(bool authenticated)
        {
            headerChipRow.Clear();

            if (authenticated)
            {
                string userName = PlayerPrefs.GetString("VERA_UserName", "User");
                headerSubtitleLabel.text = $"Welcome {userName}!";
                headerChipRow.Add(CreateChip("Signed in", COLOR_SUCCESS));

                if (experimentList != null && selectedExperimentIndex >= 0 && selectedExperimentIndex < experimentList.Count
                    && experimentList[selectedExperimentIndex] != null)
                {
                    Experiment experiment = experimentList[selectedExperimentIndex];
                    headerChipRow.Add(CreateChip(experiment.name, VERA_PURPLE_LIGHT));

                    if (experiment.isMultiSite && experiment.sites != null
                        && selectedSiteIndex >= 0 && selectedSiteIndex < experiment.sites.Count
                        && experiment.sites[selectedSiteIndex] != null)
                    {
                        headerChipRow.Add(CreateChip(experiment.sites[selectedSiteIndex].name, TEXT_SECONDARY));
                    }
                }
            }
            else
            {
                headerSubtitleLabel.text = "Sign in to manage your experiment";
            }
        }

        private void RebuildActionBar(bool authenticated)
        {
            actionBar.Clear();

            if (!authenticated)
            {
                actionBar.style.display = DisplayStyle.None;
                return;
            }

            actionBar.style.display = DisplayStyle.Flex;

            actionBar.Add(CreateSecondaryButton("Log Out", () =>
            {
                VERAAuthenticator.ClearAuthentication();
            }));

            actionBar.Add(CreateSecondaryButton("Am I Connected?", () =>
            {
                TestUserConnection(false);
            }));

            actionBar.Add(CreateSecondaryButton("Open Help Window", () =>
            {
                VERAHelpWindow.ShowWindow();
            }));
        }

        private void BuildUnauthenticatedContent()
        {
            VisualElement card = CreateCard();

            Label title = CreateSectionTitle("Get started");
            card.Add(title);

            card.Add(CreateParagraph(
                "You are not yet authenticated. Click the button below to authenticate, and be able to use VERA's tools." +
                "\nMake sure you are connected to the internet before authenticating."));

            Button authButton = CreatePrimaryButton("Authenticate", () =>
            {
                experimentList = null;
                VERAAuthenticator.StartUserAuthentication();
            });
            authButton.style.marginTop = 8;
            authButton.style.paddingTop = 12;
            authButton.style.paddingBottom = 12;
            authButton.style.paddingLeft = 28;
            authButton.style.paddingRight = 28;
            authButton.style.fontSize = 14;
            card.Add(authButton);

            contentContainer.Add(card);
        }

        private void BuildAuthenticatedContent()
        {
            string[] options = experimentList != null ? new string[experimentList.Count] : new string[0];

            contentContainer.Add(BuildExperimentSection(options));
            contentContainer.Add(BuildDataRecordingSection());

            if (options.Length > 0)
                contentContainer.Add(BuildBuildUploadSection());
        }

        #endregion


        #region EXPERIMENT SECTION

        private VisualElement BuildExperimentSection(string[] options)
        {
            return CreateCollapsibleCard("Active Experiment", experimentFoldout, expanded => experimentFoldout = expanded, body =>
            {
                if (experimentList == null || experimentList.Count == 0)
                {
                    body.Add(CreateCallout(
                        "No experiments could be found associated with your account. If this is not correct, please try refreshing experiments or re-authenticating.",
                        true));

                    body.Add(CreatePrimaryButton("Retry Loading Experiments", () =>
                    {
                        LoadSettings();
                    }));

                    Label troubleTitle = new Label("Troubleshooting:");
                    troubleTitle.style.fontSize = 13;
                    troubleTitle.style.color = TEXT_PRIMARY;
                    troubleTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                    troubleTitle.style.marginTop = 14;
                    troubleTitle.style.marginBottom = 6;
                    body.Add(troubleTitle);

                    body.Add(CreateParagraph("- Ensure you are logged in and have a valid network connection."));
                    body.Add(CreateParagraph("- If the problem persists, contact your system administrator."));
                    return;
                }

                body.Add(CreateParagraph(
                    "Your Unity project can only be linked to a single experiment at a time. This selected experiment will be used for all VERA operations."));
                body.Add(CreateParagraph(
                    "Use the dropdown below to select your active experiment - the dropdown will list all experiments you have created or are collaborating on as shown on the VERA web portal. If you don't see your experiment in the dropdown, use the button below to refresh."));

                for (int i = 0; i < experimentList.Count; i++)
                    options[i] = experimentList[i].name;

                int experimentIndex = Mathf.Clamp(selectedExperimentIndex, 0, experimentList.Count - 1);
                VisualElement experimentDropdown = CreateDropdown("Select Experiment", options.ToList(), experimentIndex, newIndex =>
                {
                    if (newIndex == selectedExperimentIndex)
                        return;

                    selectedExperimentIndex = newIndex;
                    int loadGeneration = BeginExperimentSetupLoad("Loading…");
                    ScheduleAfterPaint(() => ContinueLoadingSelectedExperiment(loadGeneration));
                });
                experimentDropdown.name = ExperimentDropdownName;
                experimentDropdown.SetEnabled(!isLoadingExperimentSetup);
                body.Add(experimentDropdown);

                Button refreshButton = CreateSecondaryButton(
                    isLoadingExperimentSetup && experimentSetupLoadMessage.StartsWith("Refresh", StringComparison.Ordinal)
                        ? "Refreshing..."
                        : "Refresh Experiments",
                    RefreshExperiments);
                refreshButton.name = RefreshExperimentsButtonName;
                refreshButton.SetEnabled(!isLoadingExperimentSetup);
                body.Add(refreshButton);

                if (selectedExperimentIndex < experimentList.Count
                    && experimentList[selectedExperimentIndex] != null
                    && experimentList[selectedExperimentIndex].isMultiSite)
                {
                    List<Site> siteList = experimentList[selectedExperimentIndex].sites;
                    string[] siteOptions = new string[siteList.Count];
                    for (int i = 0; i < siteList.Count; i++)
                        siteOptions[i] = siteList[i].name;

                    int siteIndex = Mathf.Clamp(selectedSiteIndex, 0, Mathf.Max(siteList.Count - 1, 0));
                    body.Add(CreateDropdown("Select Site", siteOptions.ToList(), siteIndex, newSiteIndex =>
                    {
                        if (newSiteIndex == selectedSiteIndex)
                            return;

                        selectedSiteIndex = newSiteIndex;
                        VERAAuthenticator.ChangeActiveSite(
                            experimentList[selectedExperimentIndex].sites[selectedSiteIndex]._id,
                            experimentList[selectedExperimentIndex].sites[selectedSiteIndex].name);
                        SaveSettings();
                        ScheduleRebuild();
                    }));
                }

                body.Add(BuildExperimentSetupSummary(experimentList[experimentIndex]));

                Label updated = CreateMutedLabel("Experiments last updated on " + timeExperimentsLastRefreshed + ".");
                updated.style.marginTop = 8;
                body.Add(updated);
            });
        }

        #endregion


        #region DATA RECORDING OPTIONS

        private static readonly string[] DataRecordingTypeLabels = new string[]
        {
            "Do not record",
            "Only record locally",
            "Record locally and live"
        };

        private static readonly string[] DataRecordingTypeDescriptions = new string[]
        {
            "VERA will not record any data locally, nor will it push any data to the VERA web portal. All calls to VERA's logging functions will be ignored. With this option selected, VERA can be considered effectively \"disabled\" for all sessions.",
            "VERA will save data locally on the device running the experiment. No data will be automatically sent to the VERA web portal.",
            "VERA will save data locally and also push it to the VERA web portal in real-time. This is the recommended setting for most experiments."
        };

        private static readonly string[] RotationFormatLabels = new string[]
        {
            "Quaternion only",
            "Euler angles only",
            "Both (Quaternion + Euler)"
        };

        private static readonly string[] RotationFormatDescriptions = new string[]
        {
            "Transform rotation data will be logged as quaternion values (x, y, z, w). Quaternions are precise and avoid gimbal lock, but are harder for humans to interpret.",
            "Transform rotation data will be logged as Euler angles (x, y, z) in degrees. Euler angles are human-readable but can suffer from gimbal lock at extreme angles.",
            "Transform rotation data will include both quaternion and Euler angles. This provides precision for programmatic use and readability for human inspection."
        };

        private static readonly string[] SessionStartBehaviorLabels = new string[]
        {
            "Automatic",
            "Manual"
        };

        private static readonly string[] DebugPreferenceLabels = new string[]
        {
            "Verbose",
            "Informative",
            "Minimal",
            "None"
        };

        private static readonly string[] DebugPreferenceDescriptions = new string[]
        {
            "VERA will output detailed debug logs to the console, including all internal operations and state changes. Useful for debugging issues during development.",
            "VERA will output informative logs including errors, warnings, and important state changes. This is the recommended setting for most use cases.",
            "VERA will only output essential logs such as errors and critical warnings. Use this setting if you want to minimize console output.",
            "VERA will not output any debug logs, warnings, or errors to the console. Use this setting if you want a completely silent experience."
        };

        private VisualElement BuildDataRecordingSection()
        {
            return CreateCollapsibleCard("Data Recording", dataRecordingFoldout, expanded => dataRecordingFoldout = expanded, body =>
            {
                DataRecordingType currentRecordingType = VERAAuthenticator.GetDataRecordingType();
                int currentIndex = (int)currentRecordingType;
                if (currentIndex < 0 || currentIndex >= DataRecordingTypeLabels.Length)
                    currentIndex = (int)DataRecordingType.RecordLocallyAndLive;

                VisualElement recordingCalloutHost = CreatePreferenceDescriptionHost(DataRecordingTypeDescriptions[currentIndex]);
                body.Add(CreatePreferenceBlock(
                    CreateDropdown("Recording Type", DataRecordingTypeLabels.ToList(), currentIndex, newIndex =>
                    {
                        if (newIndex == (int)VERAAuthenticator.GetDataRecordingType())
                            return;
                        VERAAuthenticator.ChangeDataRecordingType((DataRecordingType)newIndex);
                        ReplacePreferenceDescription(recordingCalloutHost, DataRecordingTypeDescriptions[newIndex]);
                    }),
                    recordingCalloutHost));

                bool currentAutoStart = VERAAuthenticator.GetAutoStartParticipantSessions();
                int currentSessionStartIndex = currentAutoStart ? 0 : 1;
                VisualElement autoStartCalloutHost = CreatePreferenceDescriptionHost(GetAutoStartDescription(currentAutoStart));
                body.Add(CreatePreferenceBlock(
                    CreateDropdown("Session Start Behavior", SessionStartBehaviorLabels.ToList(), currentSessionStartIndex, newIndex =>
                    {
                        bool newAutoStart = newIndex == 0;
                        if (newAutoStart == VERAAuthenticator.GetAutoStartParticipantSessions())
                            return;
                        VERAAuthenticator.ChangeAutoStartParticipantSessions(newAutoStart);
                        ReplacePreferenceDescription(autoStartCalloutHost, GetAutoStartDescription(newAutoStart));
                    }),
                    autoStartCalloutHost));

                RotationFormat currentRotationFormat = VERAAuthenticator.GetRotationFormat();
                int currentRotationIndex = (int)currentRotationFormat;
                if (currentRotationIndex < 0 || currentRotationIndex >= RotationFormatLabels.Length)
                    currentRotationIndex = (int)RotationFormat.Quaternion;

                VisualElement rotationCalloutHost = CreatePreferenceDescriptionHost(RotationFormatDescriptions[currentRotationIndex]);
                body.Add(CreatePreferenceBlock(
                    CreateDropdown("Rotation Format", RotationFormatLabels.ToList(), currentRotationIndex, newRotationIndex =>
                    {
                        if (newRotationIndex == (int)VERAAuthenticator.GetRotationFormat())
                            return;
                        VERAAuthenticator.ChangeRotationFormat((RotationFormat)newRotationIndex);
                        ReplacePreferenceDescription(rotationCalloutHost, RotationFormatDescriptions[newRotationIndex]);
                    }),
                    rotationCalloutHost));

                DebugPreference currentDebugPreference = VERAAuthenticator.GetDebugPreference();
                int currentDebugIndex = (int)currentDebugPreference;
                if (currentDebugIndex < 0 || currentDebugIndex >= DebugPreferenceLabels.Length)
                    currentDebugIndex = (int)DebugPreference.Informative;

                VisualElement debugCalloutHost = CreatePreferenceDescriptionHost(DebugPreferenceDescriptions[currentDebugIndex]);
                VisualElement debugBlock = CreatePreferenceBlock(
                    CreateDropdown("Debug Level", DebugPreferenceLabels.ToList(), currentDebugIndex, newIndex =>
                    {
                        if (newIndex == (int)VERAAuthenticator.GetDebugPreference())
                            return;
                        VERAAuthenticator.ChangeDebugPreference((DebugPreference)newIndex);
                        ReplacePreferenceDescription(debugCalloutHost, DebugPreferenceDescriptions[newIndex]);
                    }),
                    debugCalloutHost);
                debugBlock.style.marginBottom = 0;
                body.Add(debugBlock);
            });
        }

        private VisualElement CreatePreferenceBlock(VisualElement control, VisualElement descriptionHost)
        {
            VisualElement block = new VisualElement();
            block.style.marginBottom = 16;
            control.style.marginTop = 0;
            control.style.marginBottom = 0;
            block.Add(control);
            block.Add(descriptionHost);
            return block;
        }

        private VisualElement CreatePreferenceDescriptionHost(string text)
        {
            VisualElement host = new VisualElement();
            host.Add(CreatePreferenceDescription(text));
            return host;
        }

        private void ReplacePreferenceDescription(VisualElement host, string text)
        {
            host.Clear();
            host.Add(CreatePreferenceDescription(text));
        }

        private VisualElement CreatePreferenceDescription(string text)
        {
            VisualElement callout = CreateCallout(text, false);
            callout.style.marginTop = 6;
            callout.style.marginBottom = 0;
            return callout;
        }

        private static string GetAutoStartDescription(bool autoStart)
        {
            return autoStart
                ? "VERA will automatically create a participant and begin data collection when the application starts. In other words, every time the application begins, a new participant session will be created for collection."
                : "VERA will not create a participant or begin data collection until you call VERASessionManager.StartNewParticipantSession(). In other words, data collection will not start until you explicitly manually start it yourself.";
        }

        #endregion


        #region BUILD OPTIONS

        private VisualElement BuildBuildUploadSection()
        {
            return CreateCollapsibleCard("Build Upload", buildUploadFoldout, expanded => buildUploadFoldout = expanded, body =>
            {
                // Check if the user is on a preview account (default to true/restricted if not set)
#pragma warning disable CS0219
                bool isPreviewAccount = PlayerPrefs.GetInt("VERA_IsPreviewAccount", 1) == 1;
#pragma warning restore CS0219

                body.Add(CreateParagraph(
                    "Once your experiment is completed and you are ready to upload it to the VERA portal, " +
                    "you will need to build for WebXR and send the build to the portal."));
                body.Add(CreateParagraph(
                    "Press the button below to automatically perform this build and upload " +
                    "process. A progress window will show the status of each step."));

                // Disable the button if on a preview account
                //EditorGUI.BeginDisabledGroup(isPreviewAccount);
                Button uploadButton = CreatePrimaryButton("Build and Upload Experiment", () =>
                {
                    if (EditorUtility.DisplayDialog("Build and Upload Experiment",
                        "This will build your experiment for WebXR and upload it to the VERA portal. Any existing upload will be replaced. " +
                        "Make sure you have selected the correct experiment in the settings window before proceeding. " +
                        "\n\nThis process may take a while.",
                        "Proceed", "Cancel"))
                    {
                        VERABuildUploader.BuildAndUploadExperiment();
                    }
                });
                uploadButton.style.marginTop = 4;
                body.Add(uploadButton);
                //EditorGUI.EndDisabledGroup();

                /*
                if (isPreviewAccount)
                {
                    GUILayout.Space(5);
                    EditorGUILayout.HelpBox("You are logged in with an early-access preview account. Preview accounts do not have permission to build and upload experiments for WebXR. " +
                        "Please contact the VERA team if you need to upload experiments.", MessageType.Warning);
                }
                */
            });
        }

        #endregion


        #region EXPERIMENT SETUP SUMMARY

        private const string ProjectColumnDefsPath = "Assets/VERA/Resources";
        private const string RefreshExperimentsButtonName = "vera-refresh-experiments-button";
        private const string RefreshExperimentsIconName = "vera-refresh-experiments-icon";
        private const string ExperimentSetupBoxName = "vera-experiment-setup-box";
        private const string RefreshOverlayName = "vera-refresh-spinner-overlay";
        private const string ExperimentDropdownName = "vera-experiment-dropdown";
        private static readonly Regex SurveyHelperNameRegex = new Regex(
            @"=>\s*""GeneratedSurveyInfos/(?<name>[^""]+)""",
            RegexOptions.Compiled);

        private VisualElement BuildExperimentSetupSummary(Experiment experiment)
        {
            VisualElement box = new VisualElement();
            box.name = ExperimentSetupBoxName;
            box.style.backgroundColor = BG_INPUT;
            box.style.borderTopLeftRadius = 6;
            box.style.borderTopRightRadius = 6;
            box.style.borderBottomLeftRadius = 6;
            box.style.borderBottomRightRadius = 6;
            box.style.paddingTop = 12;
            box.style.paddingBottom = 10;
            box.style.paddingLeft = 12;
            box.style.paddingRight = 10;
            box.style.marginTop = 12;
            box.style.marginBottom = 10;
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = VERA_PURPLE;
            box.style.overflow = Overflow.Hidden;

            VisualElement titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 10;

            Label title = new Label("Experiment setup");
            title.style.fontSize = 12;
            title.style.color = VERA_PURPLE_LIGHT;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexShrink = 1;
            titleRow.Add(title);
            titleRow.Add(CreateRefreshExperimentsIconButton());
            box.Add(titleRow);

            List<(string name, string extension)> fileTypes = LoadProjectFileTypes();
            VisualElement fileGroup = CreateSetupGroup("File types", fileTypes.Count);
            if (fileTypes.Count == 0)
            {
                fileGroup.Add(CreateEmptySummaryLine("None in this project."));
            }
            else
            {
                foreach (var fileType in fileTypes)
                    fileGroup.Add(CreateFileTypeRow(fileType.name, fileType.extension));
            }
            box.Add(fileGroup);

            EnsureConditionEncodings(experiment);
            List<IVGroup> ivGroups = experiment?.conditions?.Where(iv => iv != null).ToList() ?? new List<IVGroup>();
            VisualElement ivGroup = CreateSetupGroup("Independent variables", ivGroups.Count);
            if (ivGroups.Count == 0)
            {
                ivGroup.Add(CreateEmptySummaryLine("None in this project."));
            }
            else
            {
                foreach (IVGroup iv in ivGroups)
                    ivGroup.Add(CreateIndependentVariableRow(iv));
            }
            box.Add(ivGroup);

            List<string> surveys = LoadProjectSurveyNames();
            VisualElement surveysGroup = CreateSetupGroup(
                "Surveys",
                surveys.Count,
                surveys.Count == 0
                    ? null
                    : CreateRevealGeneratedScriptButton(
                        GetGeneratedSurveyHelperScriptPath(),
                        "survey helper",
                        "Refresh experiments to regenerate the survey helper script."));
            surveysGroup.style.marginBottom = 0;
            if (surveys.Count == 0)
            {
                surveysGroup.Add(CreateEmptySummaryLine("None in this project."));
            }
            else
            {
                foreach (string surveyName in surveys)
                    surveysGroup.Add(CreateSurveyRow(surveyName));
            }
            box.Add(surveysGroup);

            if (isLoadingExperimentSetup)
                box.Add(CreateRefreshOverlay());

            return box;
        }

        private void EnsureConditionEncodings(Experiment experiment)
        {
            if (experiment?.conditions == null)
                return;

            foreach (var iv in experiment.conditions)
            {
                if (iv == null || iv.conditions == null)
                    continue;

                string cacheKey = (experiment._id ?? "") + ":" + iv.ivName;
                bool anyMissingEncoding = iv.conditions.Any(c => string.IsNullOrEmpty(c.encoding));
                if (!anyMissingEncoding || ivFetchCache.ContainsKey(cacheKey))
                    continue;

                ivFetchCache[cacheKey] = null;
                VERAAuthenticator.GetIVGroupConditions(PlayerPrefs.GetString("VERA_ActiveExperiment"), iv.ivName, (fetched) =>
                {
                    if (fetched != null && fetched.conditions != null)
                    {
                        var e = experimentList.FirstOrDefault(x => x._id == PlayerPrefs.GetString("VERA_ActiveExperiment"));
                        if (e != null)
                        {
                            var existing = e.conditions.FirstOrDefault(x => x.ivName == fetched.ivName);
                            if (existing != null)
                                existing.conditions = fetched.conditions;
                            else
                                e.conditions.Add(fetched);
                        }
                        ivFetchCache[cacheKey] = fetched;
                    }
                    else
                    {
                        ivFetchCache[cacheKey] = new IVGroup { ivName = iv.ivName, conditions = new List<Condition>() };
                    }

                    ScheduleRebuild();
                });
            }
        }

        private static List<(string name, string extension)> LoadProjectFileTypes()
        {
            var result = new List<(string name, string extension)>();
            string absoluteDir = Path.Combine(Application.dataPath, "VERA", "Resources");
            if (!Directory.Exists(absoluteDir))
                return result;

            foreach (string file in Directory.GetFiles(absoluteDir, "*.asset"))
            {
                string relativePath = ProjectColumnDefsPath + "/" + Path.GetFileName(file);
                VERAColumnDefinition def = AssetDatabase.LoadAssetAtPath<VERAColumnDefinition>(relativePath);
                if (def?.fileType == null || string.IsNullOrEmpty(def.fileType.name))
                    continue;
                if (IsSurveyResponsesFileType(def.fileType.name))
                    continue;

                string extension = string.IsNullOrEmpty(def.fileType.extension) ? "csv" : def.fileType.extension;
                result.Add((def.fileType.name, extension));
            }

            result.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private static List<string> LoadProjectSurveyNames()
        {
            var result = new List<string>();
            string helperPath = Path.Combine(Application.dataPath, "VERA", "Surveys", "GeneratedCode", "VERASurveyHelper.cs");
            if (!File.Exists(helperPath))
                return result;

            string content = File.ReadAllText(helperPath);
            foreach (Match match in SurveyHelperNameRegex.Matches(content))
            {
                string name = match.Groups["name"].Value;
                if (string.IsNullOrEmpty(name) || result.Contains(name))
                    continue;
                result.Add(name);
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private string ComputeSetupFingerprint()
        {
            var sb = new StringBuilder();
            foreach (var fileType in LoadProjectFileTypes())
                sb.Append(fileType.name).Append('.').Append(fileType.extension).Append(';');
            sb.Append('|');
            foreach (string survey in LoadProjectSurveyNames())
                sb.Append(survey).Append(';');
            sb.Append('|');

            if (experimentList != null && selectedExperimentIndex >= 0 && selectedExperimentIndex < experimentList.Count)
            {
                Experiment experiment = experimentList[selectedExperimentIndex];
                if (experiment?.conditions != null)
                {
                    foreach (IVGroup iv in experiment.conditions)
                    {
                        if (iv == null)
                            continue;
                        sb.Append(iv.ivName).Append(':');
                        if (iv.conditions != null)
                        {
                            foreach (Condition condition in iv.conditions)
                            {
                                if (condition == null)
                                    continue;
                                sb.Append(condition.name).Append('(').Append(condition.encoding).Append(')').Append(',');
                            }
                        }
                        sb.Append(';');
                    }
                }
            }

            return sb.ToString();
        }

        private VisualElement CreateSetupGroup(string title, int count, VisualElement headerAction = null)
        {
            VisualElement group = new VisualElement();
            group.style.backgroundColor = BG_SETUP_GROUP;
            group.style.borderTopLeftRadius = 5;
            group.style.borderTopRightRadius = 5;
            group.style.borderBottomLeftRadius = 5;
            group.style.borderBottomRightRadius = 5;
            group.style.paddingTop = 8;
            group.style.paddingBottom = 8;
            group.style.paddingLeft = 10;
            group.style.paddingRight = 8;
            group.style.marginBottom = 8;

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 4;
            header.style.paddingBottom = 5;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = BORDER_HAIRLINE;

            Label heading = new Label($"{title}  ·  {count}");
            heading.style.fontSize = 11;
            heading.style.color = TEXT_SECONDARY;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.flexGrow = 1;
            heading.style.flexShrink = 1;
            header.Add(heading);

            if (headerAction != null)
                header.Add(headerAction);

            group.Add(header);
            return group;
        }

        private Label CreateEmptySummaryLine(string text)
        {
            Label label = CreateMutedLabel(text);
            label.style.paddingTop = 3;
            label.style.paddingBottom = 2;
            return label;
        }

        private Label CreateSetupItemName(string name)
        {
            Label nameLabel = new Label(name);
            nameLabel.style.fontSize = 12;
            nameLabel.style.color = TEXT_PRIMARY;
            nameLabel.style.flexGrow = 1;
            nameLabel.style.flexShrink = 1;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            return nameLabel;
        }

        private VisualElement CreateFileTypeRow(string name, string extension)
        {
            bool isTelemetry = name == VERAExperimentTelemetrySchema.Name;

            VisualElement container = new VisualElement();
            container.style.paddingTop = 3;
            container.style.paddingBottom = isTelemetry ? 6 : 3;

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.Add(CreateSetupItemName(name));

            Label extLabel = new Label(FormatFileExtension(extension));
            extLabel.style.fontSize = 10;
            extLabel.style.color = TEXT_MUTED;
            extLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            extLabel.style.minWidth = 40;
            extLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            extLabel.style.flexShrink = 0;
            extLabel.style.marginRight = 2;
            row.Add(extLabel);

            if (isTelemetry)
                row.Add(CreateScriptButtonSpacer());
            else
            {
                row.Add(CreateRevealGeneratedScriptButton(
                    GetGeneratedFileTypeScriptPath(name),
                    "file type",
                    "Refresh experiments to regenerate file type scripts."));
            }

            container.Add(row);

            if (isTelemetry)
            {
                Label note = CreateMutedLabel("Handled automatically by VERA.");
                note.style.fontSize = 10;
                note.style.marginTop = 1;
                note.style.marginRight = 22;
                container.Add(note);
            }

            return container;
        }

        private VisualElement CreateSurveyRow(string name)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;
            row.Add(CreateSetupItemName(name));
            row.Add(CreateScriptButtonSpacer());
            return row;
        }

        private Button CreateRevealGeneratedScriptButton(string assetPath, string entityLabel, string regenerateHint)
        {
            bool scriptExists = TryLoadGeneratedScript(assetPath, out _);
            Color idle = new Color(0f, 0f, 0f, 0f);

            Button button = new Button(() => HighlightGeneratedScript(assetPath, entityLabel, regenerateHint));
            button.tooltip = scriptExists
                ? $"Highlight generated script ({Path.GetFileName(assetPath)})"
                : $"Generated C# script not found for this {entityLabel}.";

            button.style.width = 18;
            button.style.height = 18;
            button.style.minWidth = 18;
            button.style.minHeight = 18;
            button.style.marginLeft = 4;
            button.style.marginRight = 0;
            button.style.marginTop = 0;
            button.style.marginBottom = 0;
            button.style.paddingTop = 1;
            button.style.paddingBottom = 1;
            button.style.paddingLeft = 1;
            button.style.paddingRight = 1;
            button.style.flexShrink = 0;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.borderTopLeftRadius = 3;
            button.style.borderTopRightRadius = 3;
            button.style.borderBottomLeftRadius = 3;
            button.style.borderBottomRightRadius = 3;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.backgroundImage = StyleKeyword.None;
            button.style.backgroundColor = idle;

            Label defaultLabel = button.Q<Label>();
            Texture icon = EditorGUIUtility.IconContent("cs Script Icon")?.image;
            if (icon != null)
            {
                if (defaultLabel != null)
                    defaultLabel.style.display = DisplayStyle.None;

                Image image = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
                image.style.width = 14;
                image.style.height = 14;
                button.Add(image);
            }
            else
            {
                button.text = "C#";
                button.style.fontSize = 8;
                button.style.color = VERA_PURPLE_LIGHT;
                button.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            if (scriptExists)
                ApplyButtonHover(button, idle, BG_SECONDARY_BTN);
            else
                button.SetEnabled(false);

            return button;
        }

        private static VisualElement CreateScriptButtonSpacer()
        {
            VisualElement spacer = new VisualElement();
            spacer.style.width = 18;
            spacer.style.height = 18;
            spacer.style.minWidth = 18;
            spacer.style.flexShrink = 0;
            spacer.style.marginLeft = 4;
            spacer.style.marginRight = 0;
            return spacer;
        }

        private Button CreateRefreshExperimentsIconButton()
        {
            Color idle = new Color(0f, 0f, 0f, 0f);
            Button button = new Button(RefreshExperiments);
            button.name = RefreshExperimentsIconName;
            button.tooltip = isLoadingExperimentSetup ? experimentSetupLoadMessage : "Refresh experiments";
            button.SetEnabled(!isLoadingExperimentSetup);

            button.style.width = 18;
            button.style.height = 18;
            button.style.minWidth = 18;
            button.style.minHeight = 18;
            button.style.marginLeft = 4;
            button.style.marginRight = 0;
            button.style.marginTop = 0;
            button.style.marginBottom = 0;
            button.style.paddingTop = 1;
            button.style.paddingBottom = 1;
            button.style.paddingLeft = 1;
            button.style.paddingRight = 1;
            button.style.flexShrink = 0;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.borderTopLeftRadius = 3;
            button.style.borderTopRightRadius = 3;
            button.style.borderBottomLeftRadius = 3;
            button.style.borderBottomRightRadius = 3;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.backgroundImage = StyleKeyword.None;
            button.style.backgroundColor = idle;

            Label defaultLabel = button.Q<Label>();
            if (defaultLabel != null)
                defaultLabel.style.display = DisplayStyle.None;

            if (isLoadingExperimentSetup)
            {
                button.Add(CreateLoadingSpinner(12));
            }
            else
            {
                Texture icon = EditorGUIUtility.IconContent("Refresh")?.image
                    ?? EditorGUIUtility.IconContent("d_Refresh")?.image;
                if (icon != null)
                {
                    Image image = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
                    image.style.width = 12;
                    image.style.height = 12;
                    button.Add(image);
                }
                else
                {
                    if (defaultLabel != null)
                        defaultLabel.style.display = DisplayStyle.Flex;
                    button.text = "↻";
                    button.style.fontSize = 12;
                    button.style.color = VERA_PURPLE_LIGHT;
                }

                ApplyButtonHover(button, idle, BG_SECONDARY_BTN);
            }

            return button;
        }

        private VisualElement CreateLoadingSpinner(float size)
        {
            Image image = new Image { scaleMode = ScaleMode.ScaleToFit };
            image.pickingMode = PickingMode.Ignore;
            image.style.width = size;
            image.style.height = size;
            image.style.flexShrink = 0;

            Texture waitFrame = LoadWaitSpinIcon(0);
            if (waitFrame != null)
            {
                image.image = waitFrame;
                int frame = 0;
                image.schedule.Execute(() =>
                {
                    frame = (frame + 1) % 12;
                    Texture next = LoadWaitSpinIcon(frame);
                    if (next != null)
                        image.image = next;
                }).Every(70);
                return image;
            }

            Texture refreshIcon = EditorGUIUtility.IconContent("Refresh")?.image
                ?? EditorGUIUtility.IconContent("d_Refresh")?.image;
            VisualElement spinner = new VisualElement();
            spinner.pickingMode = PickingMode.Ignore;
            spinner.style.width = size;
            spinner.style.height = size;
            spinner.style.flexShrink = 0;
            spinner.style.alignItems = Align.Center;
            spinner.style.justifyContent = Justify.Center;

            if (refreshIcon != null)
            {
                image.image = refreshIcon;
                spinner.Add(image);
            }
            else
            {
                Label fallback = new Label("↻");
                fallback.style.fontSize = size;
                fallback.style.color = VERA_PURPLE_LIGHT;
                fallback.style.unityTextAlign = TextAnchor.MiddleCenter;
                spinner.Add(fallback);
            }

            float angle = 0f;
            spinner.schedule.Execute(() =>
            {
                angle = (angle + 24f) % 360f;
                spinner.style.rotate = new Rotate(angle);
            }).Every(40);
            return spinner;
        }

        private static Texture LoadWaitSpinIcon(int frame)
        {
            string frameName = "WaitSpin" + frame.ToString("00");
            Texture icon = EditorGUIUtility.IconContent(frameName)?.image;
            if (icon != null)
                return icon;
            return EditorGUIUtility.IconContent("d_" + frameName)?.image;
        }

        private VisualElement CreateRefreshOverlay()
        {
            VisualElement overlay = new VisualElement { name = RefreshOverlayName };
            overlay.pickingMode = PickingMode.Position;
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.top = 0;
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            overlay.style.backgroundColor = new Color(BG_INPUT.r, BG_INPUT.g, BG_INPUT.b, 0.72f);
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.flexDirection = FlexDirection.Row;

            overlay.Add(CreateLoadingSpinner(16));

            Label label = new Label(experimentSetupLoadMessage);
            label.style.fontSize = 11;
            label.style.color = TEXT_SECONDARY;
            label.style.marginLeft = 8;
            overlay.Add(label);
            return overlay;
        }

        private void ApplyRefreshingStateToCurrentUi()
        {
            if (rootVisualElement == null)
                return;

            Button labeled = rootVisualElement.Q<Button>(RefreshExperimentsButtonName);
            if (labeled != null)
            {
                if (experimentSetupLoadMessage.StartsWith("Refresh", StringComparison.Ordinal))
                    labeled.text = "Refreshing...";
                labeled.SetEnabled(false);
            }

            Button iconButton = rootVisualElement.Q<Button>(RefreshExperimentsIconName);
            if (iconButton != null)
            {
                iconButton.SetEnabled(false);
                iconButton.tooltip = experimentSetupLoadMessage;
                iconButton.Clear();
                iconButton.Add(CreateLoadingSpinner(12));
            }

            VisualElement experimentDropdown = rootVisualElement.Q(ExperimentDropdownName);
            if (experimentDropdown != null)
                experimentDropdown.SetEnabled(false);

            VisualElement box = rootVisualElement.Q(ExperimentSetupBoxName);
            if (box != null && box.Q(RefreshOverlayName) == null)
            {
                box.Add(CreateRefreshOverlay());
                box.MarkDirtyRepaint();
            }

            rootVisualElement.MarkDirtyRepaint();
            Repaint();
        }

        private void ScheduleAfterPaint(Action action)
        {
            if (action == null)
                return;

            if (rootVisualElement != null)
            {
                rootVisualElement.schedule.Execute(action).StartingIn(16);
                return;
            }

            EditorApplication.delayCall += () =>
            {
                if (this != null)
                    action();
            };
        }

        private void ContinueLoadingSelectedExperiment(int generation)
        {
            if (generation != experimentSetupLoadGeneration || this == null)
                return;

            if (experimentList == null || selectedExperimentIndex < 0 || selectedExperimentIndex >= experimentList.Count)
            {
                EndExperimentSetupLoad(generation);
                return;
            }

            Experiment experiment = experimentList[selectedExperimentIndex];
            if (experiment == null)
            {
                EndExperimentSetupLoad(generation);
                return;
            }

            VERAAuthenticator.ChangeActiveExperiment(
                experiment._id,
                experiment.name,
                experiment.isMultiSite,
                experiment.webXrBuildNumber,
                () => EndExperimentSetupLoad(generation));

            selectedSiteIndex = 0;
            if (experiment.sites != null && experiment.sites.Count > 0 && experiment.sites[0] != null)
            {
                VERAAuthenticator.ChangeActiveSite(experiment.sites[0]._id, experiment.sites[0].name);
            }

            SaveSettings();
            ConditionGenerator.GenerateAllConditionCsCode(experiment);
            ScheduleRebuild();
        }

        private static bool IsSurveyResponsesFileType(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            if (name == "Survey_Responses")
                return true;

            string normalized = name.ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
            return normalized == "surveyresponses";
        }

        private static string GetGeneratedFileTypeScriptPath(string fileTypeName)
        {
            if (string.IsNullOrEmpty(fileTypeName))
                return null;

            return FileTypeGenerator.GeneratedCsDirectory + "VERAFile_" + fileTypeName + ".cs";
        }

        private static string GetGeneratedIndependentVariableScriptPath(string ivName)
        {
            if (string.IsNullOrEmpty(ivName))
                return null;

            return ConditionGenerator.GeneratedCsDirectory + "VERAIV_" + ivName + ".cs";
        }

        private static string GetGeneratedSurveyHelperScriptPath()
        {
            return SurveyHelperGenerator.GeneratedCsDirectory + "VERASurveyHelper.cs";
        }

        private static bool TryLoadGeneratedScript(string assetPath, out MonoScript script)
        {
            script = null;
            if (string.IsNullOrEmpty(assetPath))
                return false;

            script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            return script != null;
        }

        private static void HighlightGeneratedScript(string assetPath, string entityLabel, string regenerateHint)
        {
            if (!TryLoadGeneratedScript(assetPath, out MonoScript script))
            {
                EditorUtility.DisplayDialog(
                    "Generated script not found",
                    $"Could not find the generated C# script for this {entityLabel} at:\n{assetPath}\n\n{regenerateHint}",
                    "OK");
                return;
            }

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = script;
            EditorGUIUtility.PingObject(script);
        }

        private VisualElement CreateIndependentVariableRow(IVGroup iv)
        {
            VisualElement container = new VisualElement();
            container.style.paddingTop = 3;
            container.style.paddingBottom = 6;

            VisualElement nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;
            nameRow.Add(CreateSetupItemName(iv.ivName ?? ""));

            if (!string.IsNullOrEmpty(iv.ivName))
            {
                nameRow.Add(CreateRevealGeneratedScriptButton(
                    GetGeneratedIndependentVariableScriptPath(iv.ivName),
                    "independent variable",
                    "Refresh experiments to regenerate independent variable scripts."));
            }
            else
            {
                nameRow.Add(CreateScriptButtonSpacer());
            }

            container.Add(nameRow);

            var levelNames = new List<string>();
            if (iv.conditions != null)
            {
                foreach (Condition condition in iv.conditions)
                {
                    if (condition == null || string.IsNullOrEmpty(condition.name))
                        continue;

                    string displayName = condition.name;
                    if (!string.IsNullOrEmpty(condition.encoding))
                        displayName = $"{condition.name} ({condition.encoding})";
                    levelNames.Add(displayName);
                }
            }

            Label levels = new Label(levelNames.Count == 0 ? "No levels." : string.Join("   ·   ", levelNames));
            levels.style.fontSize = 11;
            levels.style.color = TEXT_MUTED;
            levels.style.whiteSpace = WhiteSpace.Normal;
            levels.style.marginTop = 1;
            levels.style.marginRight = 22;
            container.Add(levels);
            return container;
        }

        private static string FormatFileExtension(string extension)
        {
            string formatted = (extension ?? "").Trim().TrimStart('.');
            if (string.IsNullOrEmpty(formatted))
                formatted = "csv";
            return formatted.ToUpperInvariant();
        }

        #endregion


        #region REFRESH EXPERIMENTS

        private int BeginExperimentSetupLoad(string message)
        {
            experimentSetupLoadGeneration++;
            isLoadingExperimentSetup = true;
            experimentSetupLoadMessage = message;
            ApplyRefreshingStateToCurrentUi();
            Repaint();
            return experimentSetupLoadGeneration;
        }

        private void EndExperimentSetupLoad(int generation)
        {
            if (generation != experimentSetupLoadGeneration)
                return;

            isLoadingExperimentSetup = false;
            ScheduleRebuild();
        }

        private void RefreshExperiments()
        {
            if (isLoadingExperimentSetup)
                return;

            int generation = BeginExperimentSetupLoad("Refreshing…");

            VERAAuthenticator.GetUserExperiments((result) =>
            {
                try
                {
                    string oldActiveId = PlayerPrefs.GetString("VERA_ActiveExperiment");
                    string oldActiveSiteId = PlayerPrefs.GetString("VERA_ActiveSite");

                    experimentList = result;
                    if (experimentList != null && experimentList.Count != 0)
                    {
                        selectedExperimentIndex = -1;
                        for (int i = 0; i < experimentList.Count; i++)
                        {
                            if (experimentList[i]._id == oldActiveId)
                            {
                                selectedExperimentIndex = i;
                                break;
                            }
                        }

                        bool experimentChanged = selectedExperimentIndex == -1;

                        if (selectedExperimentIndex == -1)
                            selectedExperimentIndex = 0;

                        if (experimentChanged)
                        {
                            if (experimentList[selectedExperimentIndex] != null)
                            {
                                VERAAuthenticator.ChangeActiveExperiment(
                                    experimentList[selectedExperimentIndex]._id,
                                    experimentList[selectedExperimentIndex].name,
                                    experimentList[selectedExperimentIndex].isMultiSite,
                                    experimentList[selectedExperimentIndex].webXrBuildNumber);
                            }
                            else
                            {
                                VERAAuthenticator.ChangeActiveExperiment(null, null, false, -1);
                            }
                        }
                        else
                        {
                            VERAAuthenticator.UpdateColumnDefs();
                            SurveyHelperGenerator.FetchAndConvertSurveys();
                        }

                        selectedSiteIndex = -1;
                        List<Site> siteList = experimentList[selectedExperimentIndex].sites;
                        for (int i = 0; i < siteList.Count; i++)
                        {
                            if (siteList[i]._id == oldActiveSiteId)
                            {
                                selectedSiteIndex = i;
                                break;
                            }
                        }

                        bool siteChanged = selectedSiteIndex == -1;
                        if (selectedSiteIndex == -1)
                            selectedSiteIndex = 0;

                        if (experimentChanged || siteChanged)
                        {
                            VERAAuthenticator.ChangeActiveSite(
                                experimentList[selectedExperimentIndex].sites[selectedSiteIndex]._id,
                                experimentList[selectedExperimentIndex].sites[selectedSiteIndex].name);
                        }
                    }
                    else
                    {
                        VERAAuthenticator.ChangeActiveExperiment(null, null, false, -1);

                        VERADebugger.LogWarning("No experiments could be found associated with your account. Without an active experiment, you will not be able to record data. " +
                            "If this is incorrect, try refreshing experiments or re-authenticating from the VERA Settings window (menu bar -> VERA -> VERA Settings).", "VERA Settings Window");
                    }

                    timeExperimentsLastRefreshed = DateTime.Now.ToString("MMMM dd, h:mm:ss tt");
                    SaveSettings();

                    if (experimentList != null && experimentList.Count > 0 && selectedExperimentIndex >= 0)
                        ConditionGenerator.GenerateAllConditionCsCode(experimentList[selectedExperimentIndex]);
                }
                finally
                {
                    EndExperimentSetupLoad(generation);
                }
            });
        }

        #endregion


        #region SAVE / LOAD SETTINGS

        private void SaveSettings()
        {
            PlayerPrefs.SetInt("VERA_SelectedExperimentIndex", selectedExperimentIndex);
            PlayerPrefs.SetInt("VERA_SelectedSiteIndex", selectedSiteIndex);

            if (experimentList != null)
            {
                string json = JsonUtility.ToJson(new SerializableList<Experiment>(experimentList));
                PlayerPrefs.SetString("VERA_ExperimentList", json);
            }
        }

        private void LoadSettings()
        {
            selectedExperimentIndex = PlayerPrefs.GetInt("VERA_SelectedExperimentIndex", 0);
            selectedSiteIndex = PlayerPrefs.GetInt("VERA_SelectedSiteIndex", 0);

            string experimentListJson = PlayerPrefs.GetString("VERA_ExperimentList", null);
            if (!string.IsNullOrEmpty(experimentListJson))
            {
                SerializableList<Experiment> list = JsonUtility.FromJson<SerializableList<Experiment>>(experimentListJson);
                experimentList = list?.List;
            }

            RefreshExperiments();
        }

        #endregion


        #region CONNECTION STATUS

        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            DebugPreference debugPref = VERAAuthenticator.GetDebugPreference();
            if (debugPref == DebugPreference.Verbose || debugPref == DebugPreference.Informative)
            {
                TestUserConnection(true);
            }
        }

        private static void TestUserConnection(bool canUserDisable)
        {
            string authSuccess = "You are successfully connected to the VERA portal.";
            string unauthError = "You are not connected to the VERA portal, and will not be able " +
                                "to run experiments. Use the \"VERA -> Settings\" menu bar item to connect.";
            if (canUserDisable)
            {
                string disablableMessage = "\n\nYou can disable this message by setting Debug Level to \"Minimal\" or \"None\" in the \"VERA -> Settings\" window.";
                authSuccess += disablableMessage;
                unauthError += disablableMessage;
            }

            VERAAuthenticator.IsUserConnected((isConnected) =>
            {
                if (isConnected)
                {
                    if (canUserDisable)
                    {
                        VERADebugger.Log("You are successfully connected to the VERA portal.", "VERA Settings Window", DebugPreference.Informative);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("VERA Connection Status", authSuccess, "Okay");
                    }
                }
                else
                {
                    if (canUserDisable)
                    {
                        VERADebugger.LogError("You are not connected to the VERA portal, and will not be able " +
                                      "to run experiments. Use the \"VERA -> Settings\" menu bar item to connect.\nYou can disable this message in the \"VERA -> Settings\" window.", "VERA Settings Window");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("VERA Connection Status", unauthError, "Okay");
                    }
                    VERAAuthenticator.ClearAuthentication();
                }
            });
        }

        #endregion


        #region UI HELPERS

        private static Texture2D LoadHeaderLogo()
        {
            const string packagePath = "Packages/com.vera.vera/Editor/Icons/vera-logo.png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(packagePath);
            if (texture != null)
                return texture;

            string[] scriptGuids = AssetDatabase.FindAssets("VERASettingsWindow t:MonoScript");
            foreach (string guid in scriptGuids)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(scriptPath) || !scriptPath.EndsWith("VERASettingsWindow.cs"))
                    continue;

                string directory = Path.GetDirectoryName(scriptPath);
                if (string.IsNullOrEmpty(directory))
                    continue;

                string logoPath = Path.Combine(directory, "Icons", "vera-logo.png").Replace('\\', '/');
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(logoPath);
                if (texture != null)
                    return texture;
            }

            return null;
        }

        private VisualElement CreateCard()
        {
            VisualElement card = new VisualElement();
            card.style.backgroundColor = BG_CARD;
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            card.style.paddingTop = 14;
            card.style.paddingBottom = 16;
            card.style.paddingLeft = 16;
            card.style.paddingRight = 16;
            card.style.marginBottom = 12;
            return card;
        }

        private VisualElement CreateCollapsibleCard(string title, bool expanded, Action<bool> setExpanded, Action<VisualElement> fillBody)
        {
            VisualElement card = CreateCard();
            card.style.paddingTop = 0;
            card.style.paddingBottom = 0;
            card.style.paddingLeft = 0;
            card.style.paddingRight = 0;

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 12;
            header.style.paddingBottom = 12;
            header.style.paddingLeft = 16;
            header.style.paddingRight = 16;
            header.style.borderTopLeftRadius = 8;
            header.style.borderTopRightRadius = 8;

            Label chevron = new Label(expanded ? "▾" : "▸");
            chevron.style.fontSize = 12;
            chevron.style.color = VERA_PURPLE_LIGHT;
            chevron.style.width = 16;
            chevron.style.flexShrink = 0;
            chevron.style.marginRight = 6;
            header.Add(chevron);

            Label titleLabel = CreateSectionTitle(title);
            titleLabel.style.marginBottom = 0;
            titleLabel.style.flexGrow = 1;
            header.Add(titleLabel);

            VisualElement body = new VisualElement();
            body.style.paddingLeft = 16;
            body.style.paddingRight = 16;
            body.style.paddingBottom = 16;
            body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            fillBody(body);

            header.RegisterCallback<ClickEvent>(_ =>
            {
                bool next = body.style.display == DisplayStyle.None;
                setExpanded(next);
                body.style.display = next ? DisplayStyle.Flex : DisplayStyle.None;
                chevron.text = next ? "▾" : "▸";
                header.style.borderBottomLeftRadius = next ? 0 : 8;
                header.style.borderBottomRightRadius = next ? 0 : 8;
            });
            header.RegisterCallback<MouseEnterEvent>(_ => header.style.backgroundColor = BG_CARD_HEADER_HOVER);
            header.RegisterCallback<MouseLeaveEvent>(_ => header.style.backgroundColor = Color.clear);

            if (!expanded)
            {
                header.style.borderBottomLeftRadius = 8;
                header.style.borderBottomRightRadius = 8;
            }

            card.Add(header);
            card.Add(body);
            return card;
        }

        private Label CreateSectionTitle(string text)
        {
            Label title = new Label(text);
            title.style.fontSize = 16;
            title.style.color = VERA_PURPLE_LIGHT;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8;
            return title;
        }

        private Label CreateParagraph(string text)
        {
            Label paragraph = new Label(text);
            paragraph.style.fontSize = 13;
            paragraph.style.color = TEXT_SECONDARY;
            paragraph.style.whiteSpace = WhiteSpace.Normal;
            paragraph.style.marginBottom = 8;
            return paragraph;
        }

        private Label CreateMutedLabel(string text)
        {
            Label label = new Label(text);
            label.style.fontSize = 11;
            label.style.color = TEXT_MUTED;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private VisualElement CreateChip(string text, Color accent)
        {
            VisualElement chip = new VisualElement();
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f);
            chip.style.borderTopLeftRadius = 10;
            chip.style.borderTopRightRadius = 10;
            chip.style.borderBottomLeftRadius = 10;
            chip.style.borderBottomRightRadius = 10;
            chip.style.paddingTop = 3;
            chip.style.paddingBottom = 3;
            chip.style.paddingLeft = 8;
            chip.style.paddingRight = 10;
            chip.style.marginRight = 6;
            chip.style.marginTop = 2;
            chip.style.marginBottom = 2;

            VisualElement dot = new VisualElement();
            dot.style.width = 7;
            dot.style.height = 7;
            dot.style.backgroundColor = accent;
            dot.style.borderTopLeftRadius = 4;
            dot.style.borderTopRightRadius = 4;
            dot.style.borderBottomLeftRadius = 4;
            dot.style.borderBottomRightRadius = 4;
            dot.style.marginRight = 6;
            dot.style.flexShrink = 0;
            chip.Add(dot);

            Label label = new Label(text);
            label.style.fontSize = 11;
            label.style.color = Color.white;
            chip.Add(label);
            return chip;
        }

        private VisualElement CreateCallout(string text, bool isError)
        {
            VisualElement box = new VisualElement();
            box.style.backgroundColor = isError ? COLOR_ERROR_BG : COLOR_INFO_BG;
            box.style.borderTopLeftRadius = 6;
            box.style.borderTopRightRadius = 6;
            box.style.borderBottomLeftRadius = 6;
            box.style.borderBottomRightRadius = 6;
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = isError ? COLOR_ERROR : VERA_PURPLE;
            box.style.paddingTop = 10;
            box.style.paddingBottom = 10;
            box.style.paddingLeft = 12;
            box.style.paddingRight = 12;
            box.style.marginTop = 4;
            box.style.marginBottom = 10;

            Label label = new Label(text);
            label.style.fontSize = 12;
            label.style.color = new Color(0.82f, 0.82f, 0.82f);
            label.style.whiteSpace = WhiteSpace.Normal;
            box.Add(label);
            return box;
        }

        private Label CreateFieldLabel(string text)
        {
            Label label = new Label(text);
            label.style.fontSize = 11;
            label.style.color = TEXT_MUTED;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 4;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private VisualElement CreateDropdown(string labelText, List<string> choices, int index, Action<int> onChanged)
        {
            VisualElement container = new VisualElement();
            container.style.marginBottom = 8;
            container.Add(CreateFieldLabel(labelText));

            if (choices == null || choices.Count == 0)
                return container;

            var options = new List<DropdownOption>(choices.Count);
            for (int i = 0; i < choices.Count; i++)
                options.Add(new DropdownOption { Index = i, Label = choices[i] });

            int safeIndex = Mathf.Clamp(index, 0, options.Count - 1);
            PopupField<DropdownOption> dropdown = new PopupField<DropdownOption>(options, safeIndex);
            dropdown.style.marginLeft = 0;
            dropdown.style.marginRight = 0;
            dropdown.style.flexGrow = 1;
            dropdown.labelElement.style.display = DisplayStyle.None;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != null)
                    onChanged?.Invoke(evt.newValue.Index);
            });
            container.Add(dropdown);
            return container;
        }

        private Button CreatePrimaryButton(string text, Action onClick)
        {
            Button button = new Button(() => onClick?.Invoke()) { text = text };
            ApplyButtonShape(button);
            button.style.backgroundColor = VERA_PURPLE;
            button.style.color = Color.white;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.marginRight = 8;
            button.style.marginBottom = 6;
            ApplyButtonHover(button, VERA_PURPLE, VERA_PURPLE_HOVER);
            return button;
        }

        private Button CreateSecondaryButton(string text, Action onClick)
        {
            Button button = new Button(() => onClick?.Invoke()) { text = text };
            ApplyButtonShape(button);
            button.style.backgroundColor = BG_SECONDARY_BTN;
            button.style.color = TEXT_PRIMARY;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopColor = BORDER_SUBTLE;
            button.style.borderBottomColor = BORDER_SUBTLE;
            button.style.borderLeftColor = BORDER_SUBTLE;
            button.style.borderRightColor = BORDER_SUBTLE;
            button.style.marginRight = 8;
            button.style.marginBottom = 6;
            ApplyButtonHover(button, BG_SECONDARY_BTN, BG_SECONDARY_BTN_HOVER);
            return button;
        }

        private static void ApplyButtonShape(Button button)
        {
            button.style.paddingTop = 8;
            button.style.paddingBottom = 8;
            button.style.paddingLeft = 16;
            button.style.paddingRight = 16;
            button.style.borderTopLeftRadius = 5;
            button.style.borderTopRightRadius = 5;
            button.style.borderBottomLeftRadius = 5;
            button.style.borderBottomRightRadius = 5;
            button.style.fontSize = 13;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.backgroundImage = StyleKeyword.None;
        }

        private static void ApplyButtonHover(Button button, Color normal, Color hover)
        {
            button.RegisterCallback<MouseEnterEvent>(_ => button.style.backgroundColor = hover);
            button.RegisterCallback<MouseLeaveEvent>(_ => button.style.backgroundColor = normal);
        }

        #endregion


        #region PUBLIC API

        public string GetSelectedExperimentConditionsJson()
        {
            if (experimentList == null || selectedExperimentIndex < 0 || selectedExperimentIndex >= experimentList.Count)
                return "";
            var experiment = experimentList[selectedExperimentIndex];
            var wrapper = new IVGroupWrapper { conditions = experiment.conditions };
            return JsonUtility.ToJson(wrapper, true);
        }

        private class DropdownOption
        {
            public int Index;
            public string Label;
            public override string ToString() => Label;
        }

        [System.Serializable]
        private class IVGroupWrapper
        {
            public List<IVGroup> conditions;
        }

        [System.Serializable]
        public class SerializableList<T>
        {
            public List<T> List;

            public SerializableList() => List = new List<T>();
            public SerializableList(List<T> list) => List = list;
        }

        #endregion

    }
}
#endif
