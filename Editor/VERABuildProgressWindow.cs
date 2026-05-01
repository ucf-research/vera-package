#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VERA
{
    /// <summary>
    /// A professional progress window that displays the status of the build and upload process.
    /// Uses UI Toolkit for a modern, polished appearance matching VERA's visual identity.
    /// </summary>
    internal class VERABuildProgressWindow : EditorWindow
    {

        #region CONSTANTS

        private static readonly Color VERA_PURPLE = new Color(106f / 255f, 44f / 255f, 145f / 255f);
        private static readonly Color VERA_PURPLE_LIGHT = new Color(204f / 255f, 165f / 255f, 227f / 255f);
        private static readonly Color VERA_PURPLE_DIM = new Color(106f / 255f, 44f / 255f, 145f / 255f, 0.3f);
        private static readonly Color BG_DARK = new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color BG_CARD = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color TEXT_PRIMARY = new Color(0.9f, 0.9f, 0.9f);
        private static readonly Color TEXT_SECONDARY = new Color(0.6f, 0.6f, 0.6f);
        private static readonly Color COLOR_SUCCESS = new Color(0.3f, 0.8f, 0.4f);
        private static readonly Color COLOR_FAIL = new Color(0.9f, 0.3f, 0.3f);
        private static readonly Color COLOR_IN_PROGRESS = new Color(0.4f, 0.7f, 1f);
        private static readonly Color PROGRESS_BAR_BG = new Color(0.12f, 0.12f, 0.12f);

        #endregion


        #region VARIABLES

        private bool cancelled;
        private bool isRunning;
        private string errorMessage;

        // UI references
        private VisualElement stepsContainer;
        private VisualElement progressBarFill;
        private Label progressLabel;
        private Label statusLabel;
        private Button cancelButton;
        private Button closeButton;

        // Step tracking
        private List<StepEntry> steps = new List<StepEntry>();
        private int animFrame;
        private double lastAnimTime;

        private class StepEntry
        {
            public string name;
            public StepStatus status;
            public VisualElement row;
            public Label iconLabel;
            public Label nameLabel;
            public Label statusLabel;
        }

        internal enum StepStatus
        {
            Pending,
            InProgress,
            Completed,
            Failed,
            Skipped
        }

        #endregion


        #region SHOW / LIFECYCLE

        public static VERABuildProgressWindow ShowProgressWindow()
        {
            var window = GetWindow<VERABuildProgressWindow>(true, "VERA Build & Upload", true);
            window.minSize = new Vector2(480, 420);
            window.maxSize = new Vector2(560, 600);
            window.cancelled = false;
            window.isRunning = true;
            window.errorMessage = null;
            window.CenterOnMainWin();
            window.Show();
            return window;
        }

        /// <summary>Whether the user has pressed cancel.</summary>
        public bool IsCancelled => cancelled;

        private void OnEnable()
        {
            EditorApplication.update += AnimationTick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= AnimationTick;
        }

        /// <summary>Prevent the user from closing via the X button while a build is running.</summary>
        private void OnDestroy()
        {
            if (isRunning)
                cancelled = true;
        }

        private void CenterOnMainWin()
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            var size = position.size;
            position = new Rect(
                main.x + (main.width - size.x) * 0.5f,
                main.y + (main.height - size.y) * 0.5f,
                size.x, size.y);
        }

        #endregion


        #region UI CREATION

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.backgroundColor = BG_DARK;
            root.style.paddingTop = 0;
            root.style.paddingBottom = 20;
            root.style.paddingLeft = 24;
            root.style.paddingRight = 24;

            AddHeader(root);
            AddProgressBar(root);
            AddStepsContainer(root);
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

            var title = new Label("Building & Uploading Experiment");
            title.style.fontSize = 20;
            title.style.color = Color.white;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            statusLabel = new Label("Preparing...");
            statusLabel.style.fontSize = 12;
            statusLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
            statusLabel.style.marginTop = 4;
            header.Add(statusLabel);

            root.Add(header);
        }

        private void AddProgressBar(VisualElement root)
        {
            var barContainer = new VisualElement();
            barContainer.style.marginBottom = 6;

            // Progress text
            progressLabel = new Label("0%");
            progressLabel.style.fontSize = 11;
            progressLabel.style.color = TEXT_SECONDARY;
            progressLabel.style.marginBottom = 4;
            progressLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            barContainer.Add(progressLabel);

            // Bar background
            var barBg = new VisualElement();
            barBg.style.height = 6;
            barBg.style.backgroundColor = PROGRESS_BAR_BG;
            barBg.style.borderTopLeftRadius = 3;
            barBg.style.borderTopRightRadius = 3;
            barBg.style.borderBottomLeftRadius = 3;
            barBg.style.borderBottomRightRadius = 3;

            // Bar fill
            progressBarFill = new VisualElement();
            progressBarFill.style.height = 6;
            progressBarFill.style.width = new StyleLength(new Length(0, LengthUnit.Percent));
            progressBarFill.style.backgroundColor = VERA_PURPLE;
            progressBarFill.style.borderTopLeftRadius = 3;
            progressBarFill.style.borderTopRightRadius = 3;
            progressBarFill.style.borderBottomLeftRadius = 3;
            progressBarFill.style.borderBottomRightRadius = 3;
            progressBarFill.style.position = Position.Absolute;
            progressBarFill.style.left = 0;
            progressBarFill.style.top = 0;

            barBg.Add(progressBarFill);
            barContainer.Add(barBg);
            root.Add(barContainer);
        }

        private void AddStepsContainer(VisualElement root)
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
            card.style.marginTop = 10;
            card.style.flexGrow = 1;

            var sectionTitle = new Label("Steps");
            sectionTitle.style.fontSize = 14;
            sectionTitle.style.color = VERA_PURPLE_LIGHT;
            sectionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            sectionTitle.style.marginBottom = 10;
            card.Add(sectionTitle);

            stepsContainer = new VisualElement();
            card.Add(stepsContainer);

            root.Add(card);
        }

        private void AddFooter(VisualElement root)
        {
            var footer = new VisualElement();
            footer.style.marginTop = 16;
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.Center;

            cancelButton = new Button(() => OnCancelClicked());
            cancelButton.text = "Cancel";
            cancelButton.style.paddingTop = 8;
            cancelButton.style.paddingBottom = 8;
            cancelButton.style.paddingLeft = 28;
            cancelButton.style.paddingRight = 28;
            cancelButton.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            cancelButton.style.color = Color.white;
            cancelButton.style.borderTopLeftRadius = 5;
            cancelButton.style.borderTopRightRadius = 5;
            cancelButton.style.borderBottomLeftRadius = 5;
            cancelButton.style.borderBottomRightRadius = 5;
            cancelButton.style.fontSize = 13;
            cancelButton.style.borderTopWidth = 1;
            cancelButton.style.borderBottomWidth = 1;
            cancelButton.style.borderLeftWidth = 1;
            cancelButton.style.borderRightWidth = 1;
            cancelButton.style.borderTopColor = new Color(0.35f, 0.35f, 0.35f);
            cancelButton.style.borderBottomColor = new Color(0.35f, 0.35f, 0.35f);
            cancelButton.style.borderLeftColor = new Color(0.35f, 0.35f, 0.35f);
            cancelButton.style.borderRightColor = new Color(0.35f, 0.35f, 0.35f);
            footer.Add(cancelButton);

            closeButton = new Button(() => Close());
            closeButton.text = "Close";
            closeButton.style.paddingTop = 8;
            closeButton.style.paddingBottom = 8;
            closeButton.style.paddingLeft = 28;
            closeButton.style.paddingRight = 28;
            closeButton.style.backgroundColor = VERA_PURPLE;
            closeButton.style.color = Color.white;
            closeButton.style.borderTopLeftRadius = 5;
            closeButton.style.borderTopRightRadius = 5;
            closeButton.style.borderBottomLeftRadius = 5;
            closeButton.style.borderBottomRightRadius = 5;
            closeButton.style.fontSize = 13;
            closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            closeButton.style.display = DisplayStyle.None;
            footer.Add(closeButton);

            root.Add(footer);
        }

        #endregion


        #region PUBLIC API

        /// <summary>Register all the steps that will be tracked.</summary>
        public void SetSteps(string[] stepNames)
        {
            steps.Clear();
            stepsContainer?.Clear();

            foreach (string name in stepNames)
            {
                var entry = new StepEntry { name = name, status = StepStatus.Pending };
                BuildStepRow(entry);
                steps.Add(entry);
            }
        }

        /// <summary>Mark a step as in-progress.</summary>
        public void BeginStep(int index)
        {
            if (index < 0 || index >= steps.Count) return;
            steps[index].status = StepStatus.InProgress;
            RefreshStepUI(steps[index]);
            UpdateOverallProgress();
            statusLabel.text = steps[index].name + "...";
        }

        /// <summary>Mark a step as completed.</summary>
        public void CompleteStep(int index)
        {
            if (index < 0 || index >= steps.Count) return;
            steps[index].status = StepStatus.Completed;
            RefreshStepUI(steps[index]);
            UpdateOverallProgress();
        }

        /// <summary>Mark a step as failed.</summary>
        public void FailStep(int index, string reason = null)
        {
            if (index < 0 || index >= steps.Count) return;
            steps[index].status = StepStatus.Failed;
            RefreshStepUI(steps[index]);
            UpdateOverallProgress();
            errorMessage = reason;
        }

        /// <summary>Mark a step as skipped (already satisfied).</summary>
        public void SkipStep(int index)
        {
            if (index < 0 || index >= steps.Count) return;
            steps[index].status = StepStatus.Skipped;
            RefreshStepUI(steps[index]);
            UpdateOverallProgress();
        }

        /// <summary>Called when the entire process finishes (success or failure).</summary>
        public void Finish(bool success, string message = null)
        {
            isRunning = false;

            if (success)
            {
                statusLabel.text = "Build & upload completed successfully!";
                statusLabel.style.color = COLOR_SUCCESS;
                progressBarFill.style.backgroundColor = COLOR_SUCCESS;
            }
            else
            {
                statusLabel.text = message ?? errorMessage ?? "Process failed.";
                statusLabel.style.color = COLOR_FAIL;
                progressBarFill.style.backgroundColor = COLOR_FAIL;
            }

            // Swap cancel → close
            if (cancelButton != null)
                cancelButton.style.display = DisplayStyle.None;
            if (closeButton != null)
                closeButton.style.display = DisplayStyle.Flex;
        }

        #endregion


        #region INTERNALS

        private void BuildStepRow(StepEntry entry)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 8;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.backgroundColor = new Color(0.14f, 0.14f, 0.14f);
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;

            // Icon
            var icon = new Label(GetIcon(StepStatus.Pending));
            icon.style.fontSize = 16;
            icon.style.color = TEXT_SECONDARY;
            icon.style.width = 24;
            icon.style.unityTextAlign = TextAnchor.MiddleCenter;
            icon.style.flexShrink = 0;
            row.Add(icon);

            // Step name
            var nameLabel = new Label(entry.name);
            nameLabel.style.fontSize = 13;
            nameLabel.style.color = TEXT_SECONDARY;
            nameLabel.style.flexGrow = 1;
            nameLabel.style.marginLeft = 8;
            row.Add(nameLabel);

            // Status label (right side)
            var sl = new Label("Pending");
            sl.style.fontSize = 11;
            sl.style.color = TEXT_SECONDARY;
            sl.style.flexShrink = 0;
            row.Add(sl);

            entry.row = row;
            entry.iconLabel = icon;
            entry.nameLabel = nameLabel;
            entry.statusLabel = sl;

            stepsContainer.Add(row);
        }

        private void RefreshStepUI(StepEntry entry)
        {
            if (entry.row == null) return;

            switch (entry.status)
            {
                case StepStatus.Pending:
                    entry.iconLabel.text = GetIcon(StepStatus.Pending);
                    entry.iconLabel.style.color = TEXT_SECONDARY;
                    entry.nameLabel.style.color = TEXT_SECONDARY;
                    entry.statusLabel.text = "Pending";
                    entry.statusLabel.style.color = TEXT_SECONDARY;
                    entry.row.style.borderLeftWidth = 0;
                    break;

                case StepStatus.InProgress:
                    entry.iconLabel.text = GetIcon(StepStatus.InProgress);
                    entry.iconLabel.style.color = COLOR_IN_PROGRESS;
                    entry.nameLabel.style.color = TEXT_PRIMARY;
                    entry.nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    entry.statusLabel.text = "In Progress";
                    entry.statusLabel.style.color = COLOR_IN_PROGRESS;
                    entry.row.style.borderLeftWidth = 3;
                    entry.row.style.borderLeftColor = VERA_PURPLE;
                    break;

                case StepStatus.Completed:
                    entry.iconLabel.text = GetIcon(StepStatus.Completed);
                    entry.iconLabel.style.color = COLOR_SUCCESS;
                    entry.nameLabel.style.color = TEXT_PRIMARY;
                    entry.nameLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
                    entry.statusLabel.text = "Done";
                    entry.statusLabel.style.color = COLOR_SUCCESS;
                    entry.row.style.borderLeftWidth = 3;
                    entry.row.style.borderLeftColor = COLOR_SUCCESS;
                    break;

                case StepStatus.Failed:
                    entry.iconLabel.text = GetIcon(StepStatus.Failed);
                    entry.iconLabel.style.color = COLOR_FAIL;
                    entry.nameLabel.style.color = COLOR_FAIL;
                    entry.nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    entry.statusLabel.text = "Failed";
                    entry.statusLabel.style.color = COLOR_FAIL;
                    entry.row.style.borderLeftWidth = 3;
                    entry.row.style.borderLeftColor = COLOR_FAIL;
                    break;

                case StepStatus.Skipped:
                    entry.iconLabel.text = "\u2014"; // em-dash
                    entry.iconLabel.style.color = TEXT_SECONDARY;
                    entry.nameLabel.style.color = TEXT_SECONDARY;
                    entry.nameLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
                    entry.statusLabel.text = "Skipped";
                    entry.statusLabel.style.color = TEXT_SECONDARY;
                    entry.row.style.borderLeftWidth = 0;
                    break;
            }
        }

        private void UpdateOverallProgress()
        {
            if (steps.Count == 0) return;

            int done = 0;
            foreach (var s in steps)
            {
                if (s.status == StepStatus.Completed || s.status == StepStatus.Skipped)
                    done++;
                else if (s.status == StepStatus.InProgress)
                    done++; // count half? No — count as partial: use 0.5
            }

            // More nuanced: completed/skipped = full, in-progress = half
            float progress = 0f;
            foreach (var s in steps)
            {
                if (s.status == StepStatus.Completed || s.status == StepStatus.Skipped)
                    progress += 1f;
                else if (s.status == StepStatus.InProgress)
                    progress += 0.5f;
            }

            float pct = Mathf.Clamp01(progress / steps.Count) * 100f;
            progressBarFill.style.width = new StyleLength(new Length(pct, LengthUnit.Percent));
            progressLabel.text = $"{Mathf.RoundToInt(pct)}%";
        }

        private static string GetIcon(StepStatus status)
        {
            switch (status)
            {
                case StepStatus.Pending: return "\u25CB";      // ○
                case StepStatus.InProgress: return "\u25CF";    // ●
                case StepStatus.Completed: return "\u2713";     // ✓
                case StepStatus.Failed: return "\u2717";        // ✗
                default: return "\u25CB";
            }
        }

        private void OnCancelClicked()
        {
            if (!isRunning) return;

            if (EditorUtility.DisplayDialog("Cancel Build",
                "Are you sure you want to cancel the build and upload process?",
                "Yes, Cancel", "Continue"))
            {
                cancelled = true;
                statusLabel.text = "Cancelling...";
                statusLabel.style.color = COLOR_FAIL;
            }
        }

        /// <summary>Animate the in-progress icon to give visual feedback.</summary>
        private void AnimationTick()
        {
            if (!isRunning) return;

            double now = EditorApplication.timeSinceStartup;
            if (now - lastAnimTime < 0.4) return;
            lastAnimTime = now;
            animFrame++;

            string[] frames = { "\u25CF", "\u25D0", "\u25D1", "\u25D2" }; // ● ◐ ◑ ◒

            foreach (var s in steps)
            {
                if (s.status == StepStatus.InProgress && s.iconLabel != null)
                {
                    s.iconLabel.text = frames[animFrame % frames.Length];
                }
            }

            Repaint();
        }

        #endregion
    }
}
#endif
