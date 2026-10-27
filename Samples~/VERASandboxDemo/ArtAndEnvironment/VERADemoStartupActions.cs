// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

/// <summary>
/// Performs first-import setup checks for the VERA Sandbox Demo sample.
/// Validates render pipeline configuration and shows a welcome window
/// with demo information. Does not change the project's XR loader.
/// </summary>
[InitializeOnLoad]
public class VERADemoStartupActions
{
    private const string FIRST_RUN_KEY = "VERASandboxDemo_FirstRunCompleted";
    private const string URP_RESOURCE_PATH = "VERADemo_PC_RPAsset";

    static VERADemoStartupActions()
    {
        if (!EditorPrefs.GetBool(FIRST_RUN_KEY, false))
        {
            EditorApplication.delayCall += RunStartupChecks;
        }
    }

    private static void RunStartupChecks()
    {
        EditorPrefs.SetBool(FIRST_RUN_KEY, true);
        CheckRenderPipeline();
        VERADemoWelcomeWindow.ShowWindow();
    }

    /// <summary>
    /// Resets the first-run flag so startup checks will run again on the next domain reload.
    /// </summary>
    public static void ResetFirstRunFlag()
    {
        EditorPrefs.DeleteKey(FIRST_RUN_KEY);
    }

    #region Render Pipeline Check

    internal static void CheckRenderPipeline()
    {
        var currentPipeline = GraphicsSettings.defaultRenderPipeline;

        if (currentPipeline == null)
        {
            EditorUtility.DisplayDialog(
                "VERA Sandbox Demo \u2013 Render Pipeline",
                "This project is not currently using a Scriptable Render Pipeline. " +
                "The VERA Sandbox Demo was designed for the Universal Render Pipeline (URP) " +
                "and may not look visually as expected without it.\n\n" +
                "It is recommended to install and configure URP for the best experience.",
                "OK");
            return;
        }

        if (!currentPipeline.GetType().Name.Contains("Universal"))
        {
            EditorUtility.DisplayDialog(
                "VERA Sandbox Demo \u2013 Render Pipeline",
                "This project is using a render pipeline other than URP (Universal Render Pipeline). " +
                "The VERA Sandbox Demo was designed for URP and may not look visually as expected.\n\n" +
                "It is recommended to switch to URP for the best experience.",
                "OK");
            return;
        }

        SuggestCustomURPAsset();
    }

    private static void SuggestCustomURPAsset()
    {
        var urpAsset = Resources.Load<RenderPipelineAsset>(URP_RESOURCE_PATH);

        if (urpAsset == null) return;
        if (GraphicsSettings.defaultRenderPipeline == urpAsset) return;

        bool apply = EditorUtility.DisplayDialog(
            "VERA Sandbox Demo \u2013 URP Configuration",
            "The VERA Sandbox Demo includes a custom URP Asset (VERADemo_PC_RPAsset) " +
            "configured for optimal visuals in this demo.\n\n" +
            "Would you like to set this as the active render pipeline asset " +
            "across all quality levels for a consistent look?",
            "Apply VERADemo_PC_RPAsset",
            "Skip");

        if (apply)
            ApplyURPAssetToAllQualityLevels(urpAsset);
    }

    private static void ApplyURPAssetToAllQualityLevels(RenderPipelineAsset urpAsset)
    {
        GraphicsSettings.defaultRenderPipeline = urpAsset;

        int originalLevel = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = urpAsset;
        }
        QualitySettings.SetQualityLevel(originalLevel, false);

        AssetDatabase.SaveAssets();
        Debug.Log("[VERA Sandbox Demo] PC_RPAsset has been applied as the render pipeline asset for all quality levels.");
    }

    #endregion
}


/// <summary>
/// Welcome window for the VERA Sandbox Demo, displayed on first import
/// and accessible from the VERA menu bar.
/// </summary>
public class VERADemoWelcomeWindow : EditorWindow
{
    private const string DEMO_URL = "https://vera-xr.io/demo";

    private static readonly Color VERA_PURPLE = new Color(106f / 255f, 44f / 255f, 145f / 255f);
    private static readonly Color VERA_PURPLE_LIGHT = new Color(204f / 255f, 165f / 255f, 227f / 255f);
    private static readonly Color BG_DARK = new Color(0.15f, 0.15f, 0.15f);
    private static readonly Color BG_CARD = new Color(0.18f, 0.18f, 0.18f);
    private static readonly Color TEXT_PRIMARY = new Color(0.9f, 0.9f, 0.9f);
    private static readonly Color TEXT_SECONDARY = new Color(0.6f, 0.6f, 0.6f);

    private ScrollView scrollView;

    [MenuItem("VERA/Sandbox Demo Info")]
    public static void ShowWindow()
    {
        var window = GetWindow<VERADemoWelcomeWindow>(true, "VERA Sandbox Demo", true);
        window.minSize = new Vector2(500, 480);
        window.maxSize = new Vector2(580, 650);
        CenterOnMainWindow(window);
        window.Show();
    }

    private static void CenterOnMainWindow(EditorWindow window)
    {
        var main = EditorGUIUtility.GetMainWindowPosition();
        var size = window.position.size;
        window.position = new Rect(
            main.x + (main.width - size.x) * 0.5f,
            main.y + (main.height - size.y) * 0.5f,
            size.x, size.y);
    }

    private void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.backgroundColor = BG_DARK;
        root.style.paddingTop = 0;
        root.style.paddingBottom = 20;
        root.style.paddingLeft = 24;
        root.style.paddingRight = 24;

        AddHeader(root);

        scrollView = new ScrollView(ScrollViewMode.Vertical);
        scrollView.style.flexGrow = 1;

        AddWelcomeSection();
        AddGettingStartedSection();
        AddDocumentationSection();

        root.Add(scrollView);

        AddFooter(root);
    }

    private void AddHeader(VisualElement root)
    {
        var header = new VisualElement();
        header.style.backgroundColor = VERA_PURPLE;
        header.style.marginLeft = -24;
        header.style.marginRight = -24;
        header.style.paddingTop = 22;
        header.style.paddingBottom = 22;
        header.style.paddingLeft = 24;
        header.style.paddingRight = 24;
        header.style.marginBottom = 18;
        header.style.alignItems = Align.Center;

        var title = new Label("VERA Sandbox Demo");
        title.style.fontSize = 22;
        title.style.color = Color.white;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.Add(title);

        var subtitle = new Label("A hands-on introduction to VERA");
        subtitle.style.fontSize = 12;
        subtitle.style.color = new Color(0.85f, 0.85f, 0.85f);
        subtitle.style.marginTop = 4;
        header.Add(subtitle);

        root.Add(header);
    }

    private void AddWelcomeSection()
    {
        var card = CreateCard();

        var title = new Label("Welcome");
        title.style.fontSize = 15;
        title.style.color = VERA_PURPLE_LIGHT;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginBottom = 8;
        card.Add(title);

        var body = new Label(
            "The VERA Sandbox Demo is a simple mock experiment designed to showcase " +
            "the tools VERA provides in a hands-on environment.\n\n" +
            "To get the most out of this sandbox demo, please use Unity version 6000 " +
            "or up, and the most recent version of the VERA Unity package.");
        body.style.fontSize = 13;
        body.style.color = TEXT_PRIMARY;
        body.style.whiteSpace = WhiteSpace.Normal;
        card.Add(body);

        scrollView.Add(card);
    }

    private void AddGettingStartedSection()
    {
        var card = CreateCard();

        var title = new Label("Getting Started");
        title.style.fontSize = 15;
        title.style.color = VERA_PURPLE_LIGHT;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginBottom = 8;
        card.Add(title);

        var body = new Label(
            "Upon fresh import, this sandbox demo does NOT have VERA fully integrated. " +
            "Follow the steps in the documentation to fully transform this experiment " +
            "to use VERA.\n\n" +
            "Visit the documentation link below for detailed instructions on how to " +
            "set up and use this demo.");
        body.style.fontSize = 13;
        body.style.color = TEXT_PRIMARY;
        body.style.whiteSpace = WhiteSpace.Normal;
        card.Add(body);

        scrollView.Add(card);
    }

    private void AddDocumentationSection()
    {
        var card = CreateCard();

        var title = new Label("Documentation");
        title.style.fontSize = 15;
        title.style.color = VERA_PURPLE_LIGHT;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginBottom = 8;
        card.Add(title);

        var urlBtn = new Button(() => Application.OpenURL(DEMO_URL));
        urlBtn.text = "vera-xr.io/demo";
        urlBtn.style.paddingTop = 8;
        urlBtn.style.paddingBottom = 8;
        urlBtn.style.paddingLeft = 16;
        urlBtn.style.paddingRight = 16;
        urlBtn.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
        urlBtn.style.color = VERA_PURPLE_LIGHT;
        urlBtn.style.borderTopLeftRadius = 4;
        urlBtn.style.borderTopRightRadius = 4;
        urlBtn.style.borderBottomLeftRadius = 4;
        urlBtn.style.borderBottomRightRadius = 4;
        urlBtn.style.borderTopWidth = 1;
        urlBtn.style.borderBottomWidth = 1;
        urlBtn.style.borderLeftWidth = 1;
        urlBtn.style.borderRightWidth = 1;
        urlBtn.style.borderTopColor = VERA_PURPLE_LIGHT;
        urlBtn.style.borderBottomColor = VERA_PURPLE_LIGHT;
        urlBtn.style.borderLeftColor = VERA_PURPLE_LIGHT;
        urlBtn.style.borderRightColor = VERA_PURPLE_LIGHT;
        urlBtn.style.fontSize = 13;
        card.Add(urlBtn);

        var note = new Label("See the full documentation for step-by-step setup instructions.");
        note.style.fontSize = 11;
        note.style.color = TEXT_SECONDARY;
        note.style.marginTop = 8;
        note.style.whiteSpace = WhiteSpace.Normal;
        card.Add(note);

        scrollView.Add(card);

        // Validation checks card
        var validationCard = CreateCard();

        var validationTitle = new Label("Project Setup Validation");
        validationTitle.style.fontSize = 15;
        validationTitle.style.color = VERA_PURPLE_LIGHT;
        validationTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        validationTitle.style.marginBottom = 8;
        validationCard.Add(validationTitle);

        var validationDesc = new Label(
            "Re-run the render pipeline check to ensure your " +
            "project is configured correctly for this demo.");
        validationDesc.style.fontSize = 13;
        validationDesc.style.color = TEXT_PRIMARY;
        validationDesc.style.whiteSpace = WhiteSpace.Normal;
        validationDesc.style.marginBottom = 10;
        validationCard.Add(validationDesc);

        var rerunBtn = new Button(() =>
        {
            VERADemoStartupActions.ResetFirstRunFlag();
            VERADemoStartupActions.CheckRenderPipeline();
        });
        rerunBtn.text = "Re-run Validation Checks";
        rerunBtn.style.paddingTop = 8;
        rerunBtn.style.paddingBottom = 8;
        rerunBtn.style.paddingLeft = 16;
        rerunBtn.style.paddingRight = 16;
        rerunBtn.style.backgroundColor = VERA_PURPLE;
        rerunBtn.style.color = Color.white;
        rerunBtn.style.borderTopLeftRadius = 4;
        rerunBtn.style.borderTopRightRadius = 4;
        rerunBtn.style.borderBottomLeftRadius = 4;
        rerunBtn.style.borderBottomRightRadius = 4;
        rerunBtn.style.fontSize = 13;
        rerunBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        validationCard.Add(rerunBtn);

        scrollView.Add(validationCard);
    }

    private void AddFooter(VisualElement root)
    {
        var footer = new VisualElement();
        footer.style.marginTop = 16;
        footer.style.paddingTop = 12;
        footer.style.borderTopWidth = 1;
        footer.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
        footer.style.alignItems = Align.Center;

        var footerText = new Label("You can reopen this window anytime from VERA > Sandbox Demo Info.");
        footerText.style.fontSize = 11;
        footerText.style.color = TEXT_SECONDARY;
        footerText.style.marginBottom = 10;
        footer.Add(footerText);

        var closeBtn = new Button(() => Close());
        closeBtn.text = "Get Started";
        closeBtn.style.paddingTop = 10;
        closeBtn.style.paddingBottom = 10;
        closeBtn.style.paddingLeft = 30;
        closeBtn.style.paddingRight = 30;
        closeBtn.style.backgroundColor = VERA_PURPLE;
        closeBtn.style.color = Color.white;
        closeBtn.style.borderTopLeftRadius = 5;
        closeBtn.style.borderTopRightRadius = 5;
        closeBtn.style.borderBottomLeftRadius = 5;
        closeBtn.style.borderBottomRightRadius = 5;
        closeBtn.style.fontSize = 14;
        closeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        footer.Add(closeBtn);

        root.Add(footer);
    }

    private VisualElement CreateCard()
    {
        var card = new VisualElement();
        card.style.backgroundColor = BG_CARD;
        card.style.borderTopLeftRadius = 6;
        card.style.borderTopRightRadius = 6;
        card.style.borderBottomLeftRadius = 6;
        card.style.borderBottomRightRadius = 6;
        card.style.paddingTop = 14;
        card.style.paddingBottom = 14;
        card.style.paddingLeft = 16;
        card.style.paddingRight = 16;
        card.style.marginBottom = 12;
        return card;
    }
}
#endif
