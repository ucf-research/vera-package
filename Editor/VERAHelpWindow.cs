#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VERA
{
    /// <summary>
    /// A help window that appears on first package installation and can be reopened from settings.
    /// Uses UI Toolkit for a modern, polished appearance.
    /// </summary>
    public class VERAHelpWindow : EditorWindow
    {

        #region VARIABLES

        private const string FIRST_INSTALL_KEY = "VERA_HelpWindowShown";
        private const string WINDOW_TITLE = "VERA Help Guide";
        private static readonly Color VERA_PURPLE = new Color(106f / 255f, 44f / 255f, 145f / 255f);
        private static readonly Color VERA_PURPLE_LIGHT = new Color(204f / 255f, 165f / 255f, 227f / 255f);

        private ScrollView scrollView;

        #endregion

        #region SHOW DIALOGUE

        /// <summary>
        /// Shows the help window. Called manually from settings or automatically on first install.
        /// </summary>
        [MenuItem("VERA/Help")]
        public static void ShowWindow()
        {
            VERAHelpWindow window = GetWindow<VERAHelpWindow>(WINDOW_TITLE);
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        /// <summary>
        /// Called on editor load to check if this is the first time the package is installed.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            // Delay the check to ensure Unity is fully loaded
            EditorApplication.delayCall += CheckFirstInstall;
        }

        private static void CheckFirstInstall()
        {
            // Check if the help window has ever been shown
            if (!EditorPrefs.GetBool(FIRST_INSTALL_KEY, false))
            {
                // Mark as shown so it doesn't appear again
                EditorPrefs.SetBool(FIRST_INSTALL_KEY, true);
                ShowWindow();
            }
        }

        /// <summary>
        /// Resets the first install flag (useful for testing).
        /// </summary>
        public static void ResetFirstInstallFlag()
        {
            EditorPrefs.DeleteKey(FIRST_INSTALL_KEY);
        }

        #endregion

        #region UI CREATION

        private void CreateGUI()
        {
            // Root container
            VisualElement root = rootVisualElement;
            root.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            root.style.paddingTop = 20;
            root.style.paddingBottom = 20;
            root.style.paddingLeft = 25;
            root.style.paddingRight = 25;

            // Create scrollable content area
            scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            root.Add(scrollView);

            // Add content sections
            AddHeader();
            AddWelcomeSection();
            AddQuickStartSection();
            AddLoggingDataSection();
            AddConditionsSection();
            AddSurveysSection();
            AddSessionSection();
            AddPreprocessorSection();
            AddResourcesSection();
            AddFooter();
        }

        private void AddHeader()
        {
            // Header container with gradient-like effect
            VisualElement headerContainer = new VisualElement();
            headerContainer.style.backgroundColor = VERA_PURPLE;
            headerContainer.style.borderTopLeftRadius = 8;
            headerContainer.style.borderTopRightRadius = 8;
            headerContainer.style.borderBottomLeftRadius = 8;
            headerContainer.style.borderBottomRightRadius = 8;
            headerContainer.style.paddingTop = 25;
            headerContainer.style.paddingBottom = 25;
            headerContainer.style.paddingLeft = 20;
            headerContainer.style.paddingRight = 20;
            headerContainer.style.marginBottom = 20;
            headerContainer.style.alignItems = Align.Center;

            // Title
            Label titleLabel = new Label("VERA Help Guide");
            titleLabel.style.fontSize = 28;
            titleLabel.style.color = Color.white;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 8;
            headerContainer.Add(titleLabel);

            // Subtitle
            Label subtitleLabel = new Label("Virtual Experience Research Accelerator");
            subtitleLabel.style.fontSize = 14;
            subtitleLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            headerContainer.Add(subtitleLabel);

            scrollView.Add(headerContainer);
        }

        private void AddWelcomeSection()
        {
            VisualElement section = CreateSection("Getting Started");

            Label welcomeText = CreateParagraph(
                "VERA is a Unity package designed to streamline the development of virtual reality experiments. " +
                "It provides tools for data collection, participant management, and seamless integration with the VERA web portal."
            );
            section.Add(welcomeText);

            Label setupText = CreateParagraph(
                "To begin using VERA, you'll need to authenticate with your VERA account. " +
                "Navigate to VERA > Settings in the menu bar to log in and select your experiment."
            );
            section.Add(setupText);

            scrollView.Add(section);
        }

        private void AddQuickStartSection()
        {
            VisualElement section = CreateSection("Quick Start Guide");

            // Steps container
            VisualElement stepsContainer = new VisualElement();
            stepsContainer.style.marginTop = 10;

            AddStep(stepsContainer, "1", "Authenticate", "Open VERA > Settings and click 'Authenticate' to log in with your VERA account.");
            AddStep(stepsContainer, "2", "Select Experiment", "Choose your experiment from the dropdown menu in the settings window.");
            AddStep(stepsContainer, "3", "Configure Data Recording", "Set your preferred data recording mode (local, live, or both).");
            AddStep(stepsContainer, "4", "Build Your Experiment", "Design your VR environment and use VERA's logging APIs to record data.");
            AddStep(stepsContainer, "5", "Upload & Deploy", "When ready, use the 'Build and Upload' feature to deploy to the VERA portal.");

            section.Add(stepsContainer);
            scrollView.Add(section);
        }

        private void AddLoggingDataSection()
        {
            VisualElement section = CreateSection("Logging Data With VERA");
            section.Add(CreateParagraph(
                "File types define the types of data files you will collect during your experiment. " +
                "Each file type represents a different kind of measurement or data you want to record for each participant."
            ));
            section.Add(CreateParagraph(
                "A single static C# class will be automatically generated for you for each CSV file type you create on the VERA portal. " +
                "These classes are named according to your CSV file types, and can be used to log new data to the file type from C# scripts."
            ));
            section.Add(CreateParagraph(
                "For example, say you had an \"EyeTracking\" CSV file with \"GazeX\", \"GazeY\", and \"PupilDilation\" columns. " +
                "VERA would generate a static C# class called \"VERAFile_EyeTracking\", which you could use to log data in the following way:"
            ));

            string loggingCode = @"// Example: Logging data to a CSV file type in Unity
using VERA;

public class Example : MonoBehaviour
{
    void Update()
    {
        // Log a row of data to the ""EyeTracking"" CSV file
        VERAFile_EyeTracking.CreateCsvEntry(
            gazeX: 100.5f,
            gazeY: 200.3f,
            pupilDilation: 3.2f
        );
    }
}";
            section.Add(CreateCodeBlock(loggingCode));

            section.Add(CreateTipBox(
                "All data you log to VERA file types will be automatically timestamped and associated with the current experimental conditions. " +
                "The data will both be stored locally (Assets -> VERA -> data) and automatically uploaded and synced to the VERA portal in real-time."
            ));

            scrollView.Add(section);
        }

        private void AddConditionsSection()
        {
            VisualElement section = CreateSection("Managing Conditions With VERA");

            CreateSubHeader(section, "How to use VERA conditions");
            section.Add(CreateParagraph(
                "Once you've defined your experimental conditions on the VERA web portal, you can Get and Set " +
                "the current values of each independent variable in a C# script."
            ));
            section.Add(CreateParagraph(
                "A single static C# class will be automatically generated for you for each independent variable you create in VERA. " +
                "These classes are named according to your independent variables, and can be used to get and set the current value of the independent variable."
            ));
            section.Add(CreateParagraph(
                "For example, say you had a \"Lighting\" independent variable with \"Bright\" and \"Dim\" levels. " +
                "VERA would generate a static C# class called \"VERAIV_Lighting\" which you could use to manage the independent variable in the following way:"
            ));

            string conditionsCode = @"// Example: Accessing experimental conditions in Unity
using VERA;

public class Example : MonoBehaviour
{
    void Start()
    {
        // Get the current value of the ""Lighting"" independent variable
        VERAIV_Lighting.IVValue currentLighting = VERAIV_Lighting.GetSelectedValue();
        Debug.Log(""Current Lighting Condition: "" + currentLighting);

        // Set the ""Lighting"" independent variable to ""Bright""
        VERAIV_Lighting.SetSelectedValue(VERAIV_Lighting.IVValue.Bright);
    }
}";
            section.Add(CreateCodeBlock(conditionsCode));

            section.Add(CreateTipBox(
                "If you're using VERA to manage your experimental conditions, and properly setting the IV values in your Unity project, " +
                "all data you record with VERA will automatically be tagged with the current conditions. This makes it easy to filter and analyze your data by condition later on."
            ));

            scrollView.Add(section);
        }

        private void AddSurveysSection()
        {
            VisualElement section = CreateSection("Starting Surveys With VERA");

            section.Add(CreateParagraph(
                "After you have defined a survey on the VERA web portal, it will be added to the static VERASurveyHelper C# class. " +
                "You can use this class to start surveys at any point during your experiment:"
            ));

            string surveysCode = @"// Example: Starting surveys in Unity
using VERA;

public class Example : MonoBehaviour
{
    void Start()
    {
        // Start a survey with the name ""My Survey""
        VERASurveyHelper.StartSurvey(VERASurveyHelper.VERASurveyReference.S_MySurvey);

        // Optionally provide an action to execute when the survey is completed
        VERASurveyHelper.StartSurvey(
            VERASurveyHelper.VERASurveyReference.S_MySurvey, 
            onSurveyComplete: () => { /* Code here */ }
        );
    }
}";
            section.Add(CreateCodeBlock(surveysCode));

            section.Add(CreateTipBox(
                "When running a survey using VERA, the survey display, navigation, and response recording will be handled automatically for you."
            ));

            scrollView.Add(section);
        }

        private void AddSessionSection()
        {
            VisualElement section = CreateSection("Managing Participant Sessions With VERA");

            section.Add(CreateParagraph(
                "The VERASessionManager static C# class can be used to manage participant sessions, including starting " +
                "and ending sessions, getting the participant's ID, and tracking session data."
            ));

            string sessionCode = @"// Example: Managing participant sessions in Unity
using VERA;

public class Example : MonoBehaviour
{
    void Start()
    {
        // Wait for VERA to finish initializing
        VERASessionManager.onInitialized.AddListener(() =>
        {
            // Get the participant's ID
            int participantID = VERASessionManager.participantID;

            // Finalize the participant's session (e.g. when the experiment is over)
            VERASessionManager.FinalizeSession();
        });
    }
}";
            section.Add(CreateCodeBlock(sessionCode));

            section.Add(CreateTipBox(
                "It is important to finalize the participant's session when the experiment is over to ensure the participant is marked as COMPLETE on the VERA portal."
            ));

            section.Add(CreateTipBox(
                "Any code which interacts with VERA (data logs, surveys, session management) should be executed after VERA has been initialized."
            ));

            scrollView.Add(section);
        }

        private void AddPreprocessorSection()
        {
            VisualElement section = CreateSection("Using VERA's Preprocessor Directives");

            section.Add(CreateParagraph(
                "VERA provides preprocessor directives that can be used to conditionally include or exclude code based on the VERA environment. " +
                "This is useful for ensuring that certain code only compiles when the associated auto-generated classes are available."
            ));

            section.Add(CreateParagraph(
                "There will be a single preprocessor directive available for each CSV file type and independent variable defined on the VERA portal. " +
                "For example, if you have an \"EyeTracking\" CSV file type and a \"Lighting\" independent variable defined on the VERA portal, " +
                "you can use the following preprocessor directives to conditionally compile code that interacts with those features:"
            ));

            string preprocessorCode = @"// Example: Using VERA's preprocessor directives
using VERA;

public class Example : MonoBehaviour
{
    void Start()
    {
        // Only compile this code if the VERAFile_EyeTracking class is available (i.e. if the ""EyeTracking"" CSV file type exists on the VERA portal)
        #if VERAFile_EyeTracking
        VERAFile_EyeTracking.CreateCsvEntry(
            gazeX: 100.5f,
            gazeY: 200.3f,
            pupilDilation: 3.2f
        );
        #endif

        // Only compile this code if the VERAIV_Lighting class is available (i.e. if the ""Lighting"" independent variable exists on the VERA portal)
        #if VERAIV_Lighting
        VERAIV_Lighting.SetSelectedValue(VERAIV_Lighting.IVValue.Bright);
        #endif
    }
}";
            section.Add(CreateCodeBlock(preprocessorCode));

            section.Add(CreateTipBox(
                "Swapping experiments or disabling VERA may result in missing auto-generated classes. Using VERA's preprocessor directives " +
                "can help prevent compile errors in these cases by ensuring that code which relies on those classes is only included when the classes are available."
            ));

            scrollView.Add(section);
        }

        private void AddResourcesSection()
        {
            VisualElement section = CreateSection("Resources & Support");

            // Buttons container
            VisualElement buttonsContainer = new VisualElement();
            buttonsContainer.style.flexDirection = FlexDirection.Row;
            buttonsContainer.style.flexWrap = Wrap.Wrap;
            buttonsContainer.style.marginTop = 10;

            AddLinkButton(buttonsContainer, "Documentation", "https://vera-xr.io/documentation");
            AddLinkButton(buttonsContainer, "VERA Portal", "https://vera-xr.io");
            AddLinkButton(buttonsContainer, "Report Issue", "https://vera-xr.io/contact-us");

            section.Add(buttonsContainer);
            scrollView.Add(section);
        }

        private void AddFooter()
        {
            VisualElement footer = new VisualElement();
            footer.style.marginTop = 25;
            footer.style.paddingTop = 15;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
            footer.style.alignItems = Align.Center;

            Label footerText = new Label("You can access this window anytime from VERA > Help or the Settings window.");
            footerText.style.fontSize = 11;
            footerText.style.color = new Color(0.6f, 0.6f, 0.6f);
            footer.Add(footerText);

            // Close button
            Button closeButton = new Button(() => Close());
            closeButton.text = "Get Started";
            closeButton.style.marginTop = 15;
            closeButton.style.paddingTop = 10;
            closeButton.style.paddingBottom = 10;
            closeButton.style.paddingLeft = 30;
            closeButton.style.paddingRight = 30;
            closeButton.style.backgroundColor = VERA_PURPLE;
            closeButton.style.color = Color.white;
            closeButton.style.borderTopLeftRadius = 5;
            closeButton.style.borderTopRightRadius = 5;
            closeButton.style.borderBottomLeftRadius = 5;
            closeButton.style.borderBottomRightRadius = 5;
            closeButton.style.fontSize = 14;
            closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            footer.Add(closeButton);

            scrollView.Add(footer);
        }

        #endregion

        #region HELPER METHODS

        private VisualElement CreateSection(string title)
        {
            VisualElement section = new VisualElement();
            section.style.marginBottom = 20;
            section.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            section.style.borderTopLeftRadius = 6;
            section.style.borderTopRightRadius = 6;
            section.style.borderBottomLeftRadius = 6;
            section.style.borderBottomRightRadius = 6;
            section.style.paddingTop = 15;
            section.style.paddingBottom = 15;
            section.style.paddingLeft = 15;
            section.style.paddingRight = 15;

            Label titleLabel = new Label(title);
            titleLabel.style.fontSize = 18;
            titleLabel.style.color = VERA_PURPLE_LIGHT;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 10;
            section.Add(titleLabel);

            return section;
        }

        private Label CreateParagraph(string text)
        {
            Label paragraph = new Label(text);
            paragraph.style.fontSize = 13;
            paragraph.style.color = new Color(0.85f, 0.85f, 0.85f);
            paragraph.style.whiteSpace = WhiteSpace.Normal;
            paragraph.style.marginBottom = 10;
            return paragraph;
        }

        private void AddStep(VisualElement container, string number, string title, string description)
        {
            VisualElement stepContainer = new VisualElement();
            stepContainer.style.flexDirection = FlexDirection.Row;
            stepContainer.style.marginBottom = 12;
            stepContainer.style.alignItems = Align.FlexStart;

            // Step number circle
            VisualElement numberCircle = new VisualElement();
            numberCircle.style.width = 28;
            numberCircle.style.height = 28;
            numberCircle.style.backgroundColor = VERA_PURPLE;
            numberCircle.style.borderTopLeftRadius = 14;
            numberCircle.style.borderTopRightRadius = 14;
            numberCircle.style.borderBottomLeftRadius = 14;
            numberCircle.style.borderBottomRightRadius = 14;
            numberCircle.style.alignItems = Align.Center;
            numberCircle.style.justifyContent = Justify.Center;
            numberCircle.style.marginRight = 12;
            numberCircle.style.flexShrink = 0;

            Label numberLabel = new Label(number);
            numberLabel.style.fontSize = 14;
            numberLabel.style.color = Color.white;
            numberLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            numberCircle.Add(numberLabel);
            stepContainer.Add(numberCircle);

            // Step content
            VisualElement contentContainer = new VisualElement();
            contentContainer.style.flexGrow = 1;

            Label titleLabel = new Label(title);
            titleLabel.style.fontSize = 14;
            titleLabel.style.color = Color.white;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 3;
            contentContainer.Add(titleLabel);

            Label descLabel = new Label(description);
            descLabel.style.fontSize = 12;
            descLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            contentContainer.Add(descLabel);

            stepContainer.Add(contentContainer);
            container.Add(stepContainer);
        }

        private void AddLinkButton(VisualElement container, string text, string url)
        {
            Button button = new Button(() => Application.OpenURL(url));
            button.text = text;
            button.style.marginRight = 10;
            button.style.marginBottom = 10;
            button.style.paddingTop = 8;
            button.style.paddingBottom = 8;
            button.style.paddingLeft = 15;
            button.style.paddingRight = 15;
            button.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            button.style.color = VERA_PURPLE_LIGHT;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopColor = VERA_PURPLE_LIGHT;
            button.style.borderBottomColor = VERA_PURPLE_LIGHT;
            button.style.borderLeftColor = VERA_PURPLE_LIGHT;
            button.style.borderRightColor = VERA_PURPLE_LIGHT;
            container.Add(button);
        }

        private void CreateSubHeader(VisualElement container, string text)
        {
            Label subHeader = new Label(text);
            subHeader.style.fontSize = 15;
            subHeader.style.color = VERA_PURPLE_LIGHT;
            subHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            subHeader.style.marginTop = 8;
            subHeader.style.marginBottom = 8;
            container.Add(subHeader);
        }

        private VisualElement CreateCodeBlock(string code)
        {
            VisualElement codeContainer = new VisualElement();
            codeContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            codeContainer.style.borderTopLeftRadius = 6;
            codeContainer.style.borderTopRightRadius = 6;
            codeContainer.style.borderBottomLeftRadius = 6;
            codeContainer.style.borderBottomRightRadius = 6;
            codeContainer.style.borderTopWidth = 1;
            codeContainer.style.borderBottomWidth = 1;
            codeContainer.style.borderLeftWidth = 1;
            codeContainer.style.borderRightWidth = 1;
            codeContainer.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
            codeContainer.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            codeContainer.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
            codeContainer.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
            codeContainer.style.paddingTop = 12;
            codeContainer.style.paddingBottom = 12;
            codeContainer.style.paddingLeft = 15;
            codeContainer.style.paddingRight = 15;
            codeContainer.style.marginTop = 8;
            codeContainer.style.marginBottom = 12;
            codeContainer.style.overflow = Overflow.Hidden;

            // Code header bar
            VisualElement headerBar = new VisualElement();
            headerBar.style.flexDirection = FlexDirection.Row;
            headerBar.style.marginBottom = 8;
            headerBar.style.paddingBottom = 8;
            headerBar.style.borderBottomWidth = 1;
            headerBar.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f);

            Label langLabel = new Label("C#");
            langLabel.style.fontSize = 10;
            langLabel.style.color = VERA_PURPLE_LIGHT;
            langLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerBar.Add(langLabel);
            codeContainer.Add(headerBar);

            // Code text with scroll
            ScrollView codeScroll = new ScrollView(ScrollViewMode.Horizontal);
            codeScroll.style.flexGrow = 1;

            Label codeLabel = new Label(code);
            codeLabel.style.fontSize = 12;
            codeLabel.style.color = new Color(0.8f, 0.9f, 0.8f);
            codeLabel.style.whiteSpace = WhiteSpace.Pre;
            codeLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
            codeScroll.Add(codeLabel);
            codeContainer.Add(codeScroll);

            return codeContainer;
        }

        private VisualElement CreateTipBox(string tipText)
        {
            VisualElement tipContainer = new VisualElement();
            tipContainer.style.backgroundColor = new Color(0.22f, 0.2f, 0.25f);
            tipContainer.style.borderTopLeftRadius = 6;
            tipContainer.style.borderTopRightRadius = 6;
            tipContainer.style.borderBottomLeftRadius = 6;
            tipContainer.style.borderBottomRightRadius = 6;
            tipContainer.style.borderLeftWidth = 3;
            tipContainer.style.borderLeftColor = VERA_PURPLE;
            tipContainer.style.paddingTop = 12;
            tipContainer.style.paddingBottom = 12;
            tipContainer.style.paddingLeft = 15;
            tipContainer.style.paddingRight = 15;
            tipContainer.style.marginTop = 15;
            tipContainer.style.marginBottom = 8;

            // Tip header
            VisualElement headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 6;

            Label iconLabel = new Label("ℹ");
            iconLabel.style.fontSize = 14;
            iconLabel.style.color = VERA_PURPLE_LIGHT;
            iconLabel.style.marginRight = 8;
            headerRow.Add(iconLabel);

            Label titleLabel = new Label("Pro Tip");
            titleLabel.style.fontSize = 13;
            titleLabel.style.color = VERA_PURPLE_LIGHT;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(titleLabel);

            tipContainer.Add(headerRow);

            // Tip text
            Label tipLabel = new Label(tipText);
            tipLabel.style.fontSize = 12;
            tipLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            tipLabel.style.whiteSpace = WhiteSpace.Normal;
            tipContainer.Add(tipLabel);

            return tipContainer;
        }

        private VisualElement CreateDivider()
        {
            VisualElement divider = new VisualElement();
            divider.style.height = 1;
            divider.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            divider.style.marginTop = 15;
            divider.style.marginBottom = 15;
            return divider;
        }

        #endregion

    }
}
#endif
