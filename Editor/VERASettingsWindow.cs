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
        private static readonly Color BG_CHIP = new Color(0.22f, 0.2f, 0.25f);
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

        #endregion


        #region STATE

        private int selectedExperimentIndex;
        private int selectedSiteIndex;
        private List<Experiment> experimentList = null;
        private string timeExperimentsLastRefreshed = string.Empty;
        private Dictionary<string, IVGroup> ivFetchCache = new Dictionary<string, IVGroup>();

        private bool experimentFoldout = true;
        private bool dataRecordingFoldout = true;
        private bool debugPreferencesFoldout = true;
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
            contentContainer.Add(BuildDebugPreferencesSection());

            if (options.Length > 0)
                contentContainer.Add(BuildBuildUploadSection());
        }

        #endregion


        #region EXPERIMENT SECTION

        private VisualElement BuildExperimentSection(string[] options)
        {
            return CreateCollapsibleCard("Your Experiment", experimentFoldout, expanded => experimentFoldout = expanded, body =>
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
                    "Use the dropdown below to select from your experiments. Your Unity project can only be linked to a single experiment at a time."));
                body.Add(CreateParagraph(
                    "If you don't see your experiment in the dropdown, or you recently added file types or conditions on the VERA portal, use the button below to refresh."));

                for (int i = 0; i < experimentList.Count; i++)
                    options[i] = experimentList[i].name;

                int experimentIndex = Mathf.Clamp(selectedExperimentIndex, 0, experimentList.Count - 1);
                body.Add(CreateDropdown("Select Experiment", options.ToList(), experimentIndex, newIndex =>
                {
                    if (newIndex == selectedExperimentIndex)
                        return;

                    selectedExperimentIndex = newIndex;
                    VERAAuthenticator.ChangeActiveExperiment(
                        experimentList[selectedExperimentIndex]._id,
                        experimentList[selectedExperimentIndex].name,
                        experimentList[selectedExperimentIndex].isMultiSite,
                        experimentList[selectedExperimentIndex].webXrBuildNumber);
                    selectedSiteIndex = 0;
                    VERAAuthenticator.ChangeActiveSite(
                        experimentList[selectedExperimentIndex].sites[selectedSiteIndex]._id,
                        experimentList[selectedExperimentIndex].sites[selectedSiteIndex].name);
                    SaveSettings();
                    ConditionGenerator.GenerateAllConditionCsCode(experimentList[selectedExperimentIndex]);
                    ScheduleRebuild();
                }));

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

                body.Add(CreateSecondaryButton("Refresh Experiments", () =>
                {
                    RefreshExperiments();
                }));

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
            "VERA will not record any data locally, nor will it push any data to the VERA web portal. All calls to VERA's logging functions will be ignored.",
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

        private VisualElement BuildDataRecordingSection()
        {
            return CreateCollapsibleCard("Data Recording", dataRecordingFoldout, expanded => dataRecordingFoldout = expanded, body =>
            {
                body.Add(CreateParagraph("Select how VERA should handle data recording for this experiment."));

                DataRecordingType currentRecordingType = VERAAuthenticator.GetDataRecordingType();
                int currentIndex = (int)currentRecordingType;
                if (currentIndex < 0 || currentIndex >= DataRecordingTypeLabels.Length)
                    currentIndex = (int)DataRecordingType.RecordLocallyAndLive;

                VisualElement recordingCalloutHost = new VisualElement();
                recordingCalloutHost.Add(CreateCallout(DataRecordingTypeDescriptions[currentIndex], false));

                body.Add(CreateDropdown("Recording Type", DataRecordingTypeLabels.ToList(), currentIndex, newIndex =>
                {
                    if (newIndex == (int)VERAAuthenticator.GetDataRecordingType())
                        return;
                    VERAAuthenticator.ChangeDataRecordingType((DataRecordingType)newIndex);
                    recordingCalloutHost.Clear();
                    recordingCalloutHost.Add(CreateCallout(DataRecordingTypeDescriptions[newIndex], false));
                }));
                body.Add(recordingCalloutHost);

                body.Add(CreateSubHeader("Participant Sessions"));
                body.Add(CreateParagraph(
                    "Choose whether VERA should start a participant session automatically when the application starts."));

                bool currentAutoStart = VERAAuthenticator.GetAutoStartParticipantSessions();
                VisualElement autoStartCalloutHost = new VisualElement();
                autoStartCalloutHost.Add(CreateCallout(GetAutoStartDescription(currentAutoStart), false));

                body.Add(CreateStyledToggle("Auto-Start Participant Sessions", currentAutoStart, newAutoStart =>
                {
                    if (newAutoStart == VERAAuthenticator.GetAutoStartParticipantSessions())
                        return;
                    VERAAuthenticator.ChangeAutoStartParticipantSessions(newAutoStart);
                    autoStartCalloutHost.Clear();
                    autoStartCalloutHost.Add(CreateCallout(GetAutoStartDescription(newAutoStart), false));
                }));
                body.Add(autoStartCalloutHost);

                body.Add(CreateSubHeader("Transform Rotation Format"));
                body.Add(CreateParagraph("Select how rotation data should be formatted when logging transforms."));

                RotationFormat currentRotationFormat = VERAAuthenticator.GetRotationFormat();
                int currentRotationIndex = (int)currentRotationFormat;
                if (currentRotationIndex < 0 || currentRotationIndex >= RotationFormatLabels.Length)
                    currentRotationIndex = (int)RotationFormat.Quaternion;

                VisualElement rotationCalloutHost = new VisualElement();
                rotationCalloutHost.Add(CreateCallout(RotationFormatDescriptions[currentRotationIndex], false));

                body.Add(CreateDropdown("Rotation Format", RotationFormatLabels.ToList(), currentRotationIndex, newRotationIndex =>
                {
                    if (newRotationIndex == (int)VERAAuthenticator.GetRotationFormat())
                        return;
                    VERAAuthenticator.ChangeRotationFormat((RotationFormat)newRotationIndex);
                    rotationCalloutHost.Clear();
                    rotationCalloutHost.Add(CreateCallout(RotationFormatDescriptions[newRotationIndex], false));
                }));
                body.Add(rotationCalloutHost);
            });
        }

        private static string GetAutoStartDescription(bool autoStart)
        {
            return autoStart
                ? "VERA will automatically create a participant and begin data collection when the application starts (or, in WebXR, as soon as the portal provides the site and participant IDs). This is the recommended setting for most experiments."
                : "VERA will not create a participant or begin data collection until you call VERASessionManager.StartNewParticipantSession(). In WebXR builds, the portal still supplies the site and participant IDs; those IDs are used when the session starts, but recording does not begin until you start it manually.";
        }

        #endregion


        #region DEBUG PREFERENCES

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

        private VisualElement BuildDebugPreferencesSection()
        {
            return CreateCollapsibleCard("Debug Preferences", debugPreferencesFoldout, expanded => debugPreferencesFoldout = expanded, body =>
            {
                body.Add(CreateParagraph("Select the level of debug logging VERA should output to the console."));

                DebugPreference currentDebugPreference = VERAAuthenticator.GetDebugPreference();
                int currentIndex = (int)currentDebugPreference;
                if (currentIndex < 0 || currentIndex >= DebugPreferenceLabels.Length)
                    currentIndex = (int)DebugPreference.Informative;

                VisualElement debugCalloutHost = new VisualElement();
                debugCalloutHost.Add(CreateCallout(DebugPreferenceDescriptions[currentIndex], false));

                body.Add(CreateDropdown("Debug Level", DebugPreferenceLabels.ToList(), currentIndex, newIndex =>
                {
                    if (newIndex == (int)VERAAuthenticator.GetDebugPreference())
                        return;
                    VERAAuthenticator.ChangeDebugPreference((DebugPreference)newIndex);
                    debugCalloutHost.Clear();
                    debugCalloutHost.Add(CreateCallout(DebugPreferenceDescriptions[newIndex], false));
                }));
                body.Add(debugCalloutHost);
            });
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
        private static readonly Regex SurveyHelperNameRegex = new Regex(
            @"=>\s*""GeneratedSurveyInfos/(?<name>[^""]+)""",
            RegexOptions.Compiled);

        private VisualElement BuildExperimentSetupSummary(Experiment experiment)
        {
            VisualElement box = new VisualElement();
            box.style.backgroundColor = BG_INPUT;
            box.style.borderTopLeftRadius = 6;
            box.style.borderTopRightRadius = 6;
            box.style.borderBottomLeftRadius = 6;
            box.style.borderBottomRightRadius = 6;
            box.style.paddingTop = 10;
            box.style.paddingBottom = 8;
            box.style.paddingLeft = 12;
            box.style.paddingRight = 12;
            box.style.marginTop = 12;
            box.style.marginBottom = 10;
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = VERA_PURPLE;

            Label title = new Label("Experiment setup");
            title.style.fontSize = 12;
            title.style.color = VERA_PURPLE_LIGHT;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8;
            box.Add(title);

            List<(string name, string extension)> fileTypes = LoadProjectFileTypes();
            box.Add(CreateSummaryHeading("File types", fileTypes.Count));
            if (fileTypes.Count == 0)
            {
                box.Add(CreateEmptySummaryLine("None in this project."));
            }
            else
            {
                foreach (var fileType in fileTypes)
                    box.Add(CreateFileTypeRow(fileType.name, fileType.extension));
            }

            EnsureConditionEncodings(experiment);
            List<IVGroup> ivGroups = experiment?.conditions?.Where(iv => iv != null).ToList() ?? new List<IVGroup>();
            box.Add(CreateSummaryHeading("Independent variables", ivGroups.Count));
            if (ivGroups.Count == 0)
            {
                box.Add(CreateEmptySummaryLine("None in this project."));
            }
            else
            {
                foreach (IVGroup iv in ivGroups)
                    box.Add(CreateIndependentVariableRow(iv));
            }

            List<string> surveys = LoadProjectSurveyNames();
            box.Add(CreateSummaryHeading("Surveys", surveys.Count));
            if (surveys.Count == 0)
            {
                box.Add(CreateEmptySummaryLine("None in this project."));
            }
            else
            {
                VisualElement surveyPills = new VisualElement();
                surveyPills.style.flexDirection = FlexDirection.Row;
                surveyPills.style.flexWrap = Wrap.Wrap;
                surveyPills.style.marginBottom = 4;
                foreach (string surveyName in surveys)
                    surveyPills.Add(CreateCompactPill(surveyName));
                box.Add(surveyPills);
            }

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

        private Label CreateSummaryHeading(string text, int count)
        {
            Label heading = new Label($"{text}  ·  {count}");
            heading.style.fontSize = 10;
            heading.style.color = TEXT_MUTED;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.marginTop = 6;
            heading.style.marginBottom = 4;
            return heading;
        }

        private Label CreateEmptySummaryLine(string text)
        {
            Label label = CreateMutedLabel(text);
            label.style.marginBottom = 4;
            return label;
        }

        private VisualElement CreateFileTypeRow(string name, string extension)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3;

            Label nameLabel = new Label(name);
            nameLabel.style.fontSize = 12;
            nameLabel.style.color = TEXT_PRIMARY;
            nameLabel.style.flexGrow = 1;
            nameLabel.style.flexShrink = 1;
            nameLabel.style.overflow = Overflow.Hidden;
            row.Add(nameLabel);

            string formattedExtension = FormatFileExtension(extension);
            VisualElement badge = new VisualElement();
            badge.style.backgroundColor = BG_CHIP;
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;
            badge.style.paddingTop = 1;
            badge.style.paddingBottom = 1;
            badge.style.paddingLeft = 6;
            badge.style.paddingRight = 6;
            badge.style.flexShrink = 0;

            Label extLabel = new Label(formattedExtension);
            extLabel.style.fontSize = 10;
            extLabel.style.color = VERA_PURPLE_LIGHT;
            extLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.Add(extLabel);
            row.Add(badge);
            return row;
        }

        private VisualElement CreateIndependentVariableRow(IVGroup iv)
        {
            VisualElement row = new VisualElement();
            row.style.marginBottom = 4;

            Label nameLabel = new Label(iv.ivName ?? "");
            nameLabel.style.fontSize = 12;
            nameLabel.style.color = TEXT_PRIMARY;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.marginBottom = 2;
            row.Add(nameLabel);

            VisualElement pills = new VisualElement();
            pills.style.flexDirection = FlexDirection.Row;
            pills.style.flexWrap = Wrap.Wrap;

            if (iv.conditions == null || iv.conditions.Count == 0)
            {
                pills.Add(CreateEmptySummaryLine("No levels."));
            }
            else
            {
                foreach (Condition condition in iv.conditions)
                {
                    if (condition == null)
                        continue;

                    string displayName = condition.name;
                    if (!string.IsNullOrEmpty(condition.encoding))
                        displayName = $"{condition.name} ({condition.encoding})";
                    pills.Add(CreateCompactPill(displayName));
                }
            }

            row.Add(pills);
            return row;
        }

        private VisualElement CreateCompactPill(string text)
        {
            VisualElement pill = new VisualElement();
            pill.style.backgroundColor = BG_CHIP;
            pill.style.borderTopLeftRadius = 10;
            pill.style.borderTopRightRadius = 10;
            pill.style.borderBottomLeftRadius = 10;
            pill.style.borderBottomRightRadius = 10;
            pill.style.paddingTop = 2;
            pill.style.paddingBottom = 2;
            pill.style.paddingLeft = 7;
            pill.style.paddingRight = 7;
            pill.style.marginRight = 4;
            pill.style.marginBottom = 3;
            pill.style.borderTopWidth = 1;
            pill.style.borderBottomWidth = 1;
            pill.style.borderLeftWidth = 1;
            pill.style.borderRightWidth = 1;
            Color border = new Color(VERA_PURPLE_LIGHT.r, VERA_PURPLE_LIGHT.g, VERA_PURPLE_LIGHT.b, 0.35f);
            pill.style.borderTopColor = border;
            pill.style.borderBottomColor = border;
            pill.style.borderLeftColor = border;
            pill.style.borderRightColor = border;

            Label label = new Label(text);
            label.style.fontSize = 11;
            label.style.color = TEXT_PRIMARY;
            pill.Add(label);
            return pill;
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

        private void RefreshExperiments()
        {
            VERAAuthenticator.GetUserExperiments((result) =>
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

                ScheduleRebuild();
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

        private Label CreateSubHeader(string text)
        {
            Label subHeader = new Label(text);
            subHeader.style.fontSize = 13;
            subHeader.style.color = TEXT_PRIMARY;
            subHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            subHeader.style.marginTop = 16;
            subHeader.style.marginBottom = 6;
            return subHeader;
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

        private VisualElement CreateDropdown(string labelText, List<string> choices, int index, Action<int> onChanged)
        {
            VisualElement container = new VisualElement();
            container.style.marginBottom = 8;

            Label label = new Label(labelText);
            label.style.fontSize = 11;
            label.style.color = TEXT_MUTED;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 4;
            container.Add(label);

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

        private VisualElement CreateStyledToggle(string labelText, bool value, Action<bool> onChanged)
        {
            Toggle toggle = new Toggle(labelText) { value = value };
            toggle.style.marginTop = 4;
            toggle.style.marginBottom = 8;

            Label toggleLabel = toggle.Q<Label>();
            if (toggleLabel != null)
            {
                toggleLabel.style.color = TEXT_PRIMARY;
                toggleLabel.style.fontSize = 13;
                toggleLabel.style.whiteSpace = WhiteSpace.Normal;
            }

            toggle.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            return toggle;
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
