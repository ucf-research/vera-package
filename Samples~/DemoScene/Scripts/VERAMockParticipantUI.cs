using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace VERA
{
    /// <summary>
    /// Simple UI controller for VERAMockParticipantTester.
    /// Wire Unity UI buttons to this script to control mock participant testing.
    /// </summary>
    internal class VERAMockParticipantUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the mock participant tester component")]
        public VERAMockParticipantTester mockTester;

        [Header("UI Elements (Optional)")]
        [Tooltip("Button to create mock participant")]
        public Button createParticipantButton;

        [Tooltip("Button to advance to next trial")]
        public Button nextTrialButton;

        [Tooltip("Button to complete current trial")]
        public Button completeTrialButton;

        [Tooltip("Button to toggle auto-advance")]
        public Button toggleAutoAdvanceButton;

        [Tooltip("Button to log current state")]
        public Button logStateButton;

        [Tooltip("Text field to display current trial info")]
        public TextMeshProUGUI currentTrialText;

        [Tooltip("Text field to display participant info")]
        public TextMeshProUGUI participantInfoText;

        [Header("Settings")]
        [Tooltip("Update UI display every N seconds")]
        public float uiUpdateInterval = 0.5f;

        private float lastUpdateTime;

        void Start()
        {
            // Auto-find the mock tester if not assigned
            if (mockTester == null)
            {
                mockTester = FindObjectOfType<VERAMockParticipantTester>();
            }

            if (mockTester == null)
            {
                Debug.LogWarning("[Mock Participant UI] No VERAMockParticipantTester found in scene. Add one to use this UI.");
                return;
            }

            // Wire up button events
            if (createParticipantButton != null)
            {
                createParticipantButton.onClick.AddListener(OnCreateParticipantClicked);
            }

            if (nextTrialButton != null)
            {
                nextTrialButton.onClick.AddListener(OnNextTrialClicked);
            }

            if (completeTrialButton != null)
            {
                completeTrialButton.onClick.AddListener(OnCompleteTrialClicked);
            }

            if (toggleAutoAdvanceButton != null)
            {
                toggleAutoAdvanceButton.onClick.AddListener(OnToggleAutoAdvanceClicked);
            }

            if (logStateButton != null)
            {
                logStateButton.onClick.AddListener(OnLogStateClicked);
            }
        }

        void Update()
        {
            // Update UI displays periodically
            if (Time.time - lastUpdateTime >= uiUpdateInterval)
            {
                UpdateUI();
                lastUpdateTime = Time.time;
            }
        }

        private void UpdateUI()
        {
            if (mockTester == null || VERALogger.Instance == null)
            {
                return;
            }

            // Update participant info
            if (participantInfoText != null)
            {
                var participant = VERALogger.Instance.activeParticipant;
                if (participant != null && !string.IsNullOrEmpty(participant.participantUUID))
                {
                    participantInfoText.text = $"Participant ID: {participant.participantShortId}\n" +
                                              $"UUID: {participant.participantUUID.Substring(0, 8)}...\n" +
                                              $"State: {participant.currentParticipantProgressState}";
                }
                else
                {
                    participantInfoText.text = "No participant created";
                }
            }

            // Update current trial info
            if (currentTrialText != null)
            {
                var currentTrial = mockTester.GetCurrentTrial();
                if (currentTrial != null)
                {
                    string conditionsText = "";
                    if (currentTrial.conditions != null && currentTrial.conditions.Count > 0)
                    {
                        conditionsText = "\nConditions:";
                        foreach (var kvp in currentTrial.conditions)
                        {
                            conditionsText += $"\n  • {kvp.Key}: {kvp.Value}";
                        }
                    }

                    string surveyText = "";
                    if (!string.IsNullOrEmpty(currentTrial.attachedSurveyName))
                    {
                        surveyText = $"\nSurvey: {currentTrial.attachedSurveyName} ({currentTrial.surveyPosition})";
                    }

                    currentTrialText.text = $"<b>{currentTrial.label}</b>\n" +
                                           $"Type: {currentTrial.type}\n" +
                                           $"Order: {currentTrial.order}{conditionsText}{surveyText}";
                }
                else
                {
                    currentTrialText.text = "No active trial\n(Workflow may be complete or not started)";
                }
            }

            // Update toggle button text if using TextMeshProUGUI
            if (toggleAutoAdvanceButton != null)
            {
                var buttonText = toggleAutoAdvanceButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = mockTester.autoAdvanceTrials ? "Stop Auto-Advance" : "Start Auto-Advance";
                }
            }
        }

        #region Button Event Handlers

        private void OnCreateParticipantClicked()
        {
            if (mockTester != null)
            {
                mockTester.CreateMockParticipant();
                Debug.Log("[Mock Participant UI] Create participant button clicked");
            }
        }

        private void OnNextTrialClicked()
        {
            if (mockTester != null)
            {
                mockTester.AdvanceToNextTrial();
                Debug.Log("[Mock Participant UI] Next trial button clicked");
            }
        }

        private void OnCompleteTrialClicked()
        {
            if (mockTester != null)
            {
                mockTester.CompleteCurrentTrial();
                Debug.Log("[Mock Participant UI] Complete trial button clicked");
            }
        }

        private void OnToggleAutoAdvanceClicked()
        {
            if (mockTester != null)
            {
                mockTester.ToggleAutoAdvance();
                Debug.Log($"[Mock Participant UI] Auto-advance toggled: {mockTester.autoAdvanceTrials}");
            }
        }

        private void OnLogStateClicked()
        {
            if (mockTester != null)
            {
                mockTester.LogWorkflowState();
                Debug.Log("[Mock Participant UI] Log state button clicked");
            }
        }

        #endregion

        #region Public API for Custom UI

        /// <summary>
        /// Set a custom condition value from UI input fields.
        /// Example: Call this from an InputField with OnValueChanged event.
        /// </summary>
        public void SetConditionFromUI(string conditionName, string valueString)
        {
            if (mockTester != null && int.TryParse(valueString, out int value))
            {
                mockTester.SetCondition(conditionName, value);
            }
        }

        /// <summary>
        /// Get formatted text for current trial (useful for custom UI).
        /// </summary>
        public string GetCurrentTrialDisplayText()
        {
            var currentTrial = mockTester?.GetCurrentTrial();
            if (currentTrial != null)
            {
                return $"{currentTrial.label} (Order {currentTrial.order})";
            }
            return "No active trial";
        }

        /// <summary>
        /// Get formatted text for participant info (useful for custom UI).
        /// </summary>
        public string GetParticipantDisplayText()
        {
            var participant = VERALogger.Instance?.activeParticipant;
            if (participant != null && !string.IsNullOrEmpty(participant.participantUUID))
            {
                return $"P{participant.participantShortId} ({participant.currentParticipantProgressState})";
            }
            return "No participant";
        }

        #endregion
    }
}
