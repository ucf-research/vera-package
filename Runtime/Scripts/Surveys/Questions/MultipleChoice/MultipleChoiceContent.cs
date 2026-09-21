// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;

namespace VERA
{
    internal class MultipleChoiceContent : SurveyQuestionContent
    {

        // MultipleChoiceContent handles the display and response recording for multiple choice and selection questions


        #region VARIABLES


        [SerializeField] private MultipleChoiceOption multipleChoiceOptionPrefab;
        [SerializeField] private RectTransform optionsContainer;
        [Tooltip("Optional container toggled visible when the Other option is selected. Assign on the MC content prefab.")]
        [SerializeField] private GameObject otherInputContainer;
        [Tooltip("Free-text field shown when the Other option is selected. Assign on the MC content prefab.")]
        [SerializeField] private TMP_InputField otherInputField;

        private bool allowMultiple;
        private bool allowOtherOption;
        private string otherOptionLabel;
        private List<MultipleChoiceOption> spawnedOptions = new List<MultipleChoiceOption>();
        private MultipleChoiceOption otherOption;


        #endregion


        #region DISPLAY QUESTION / RESPONSE


        public override void DisplayQuestion(VERASurveyQuestionInfo question)
        {
            base.DisplayQuestion(question);

            allowMultiple = question.questionType == VERASurveyQuestionInfo.VERASurveyQuestionType.Selection;
            allowOtherOption = question.allowOtherOption;
            otherOptionLabel = question.GetOtherOptionLabel();
            otherOption = null;

            foreach (string optionText in question.selectionOptions)
            {
                SpawnOption(optionText, isOtherOption: false);
            }

            if (allowOtherOption)
            {
                otherOption = SpawnOption(otherOptionLabel, isOtherOption: true);

                if (otherInputField == null)
                {
                    VERADebugger.LogError(
                        "allowOtherOption is enabled but otherInputField is not assigned on MultipleChoiceContent. " +
                        "Assign a TMP_InputField on the Multiple Choice content prefab.",
                        "MultipleChoiceContent");
                }
                else
                {
                    otherInputField.characterLimit = VERASurveyQuestionInfo.OPEN_RESPONSE_MAX_LENGTH;
                    otherInputField.text = string.Empty;
                    if (otherInputField.placeholder is TMP_Text placeholder)
                        placeholder.text = otherOptionLabel;

                    SurveyInputFieldVirtualKeyboard.EnsureOn(otherInputField);
                }

                // Keep the Other text field below spawned options when it lives in the same layout container
                if (otherInputContainer != null)
                    otherInputContainer.transform.SetAsLastSibling();
                else if (otherInputField != null)
                    otherInputField.transform.SetAsLastSibling();
            }

            SetOtherInputVisible(false);
        }


        private MultipleChoiceOption SpawnOption(string optionText, bool isOtherOption)
        {
            MultipleChoiceOption option = Instantiate(multipleChoiceOptionPrefab, optionsContainer);
            option.Initialize(spawnedOptions.Count, optionText, allowMultiple, OnOptionClicked, isOtherOption);
            spawnedOptions.Add(option);
            return option;
        }


        private void OnOptionClicked(int clickedIndex)
        {
            if (!allowMultiple)
            {
                foreach (MultipleChoiceOption option in spawnedOptions)
                {
                    if (option.OptionIndex != clickedIndex)
                        option.SetSelected(false);
                }
            }

            SetOtherInputVisible(IsOtherSelected());
        }


        private bool IsOtherSelected()
        {
            return otherOption != null && otherOption.IsSelected;
        }


        private void SetOtherInputVisible(bool visible, bool openKeyboard = true)
        {
            if (otherInputContainer != null)
                otherInputContainer.SetActive(visible);
            else if (otherInputField != null)
                otherInputField.gameObject.SetActive(visible);

            if (!visible && otherInputField != null)
            {
                otherInputField.text = string.Empty;
                return;
            }

            if (visible && otherInputField != null)
            {
                SurveyInputFieldVirtualKeyboard.EnsureOn(otherInputField);
                if (!openKeyboard)
                    return;

                var keyboard = otherInputField.GetComponent<SurveyInputFieldVirtualKeyboard>();
                if (keyboard != null)
                    keyboard.FocusAndOpenKeyboardDeferred();
            }
        }


        public override void ApplySavedAnswer(SurveyQuestionAnswer savedAnswer)
        {
            if (savedAnswer == null)
                return;

            bool restoredAny = false;

            if (allowOtherOption)
                restoredAny = RestoreLabelBasedSelection(savedAnswer.answer);
            else
                restoredAny = RestoreIndexBasedSelection(savedAnswer.answer);

            bool otherSelected = IsOtherSelected();
            SetOtherInputVisible(otherSelected, openKeyboard: false);

            if (otherSelected && otherInputField != null && !string.IsNullOrEmpty(savedAnswer.otherText))
            {
                otherInputField.text = savedAnswer.otherText;
                restoredAny = true;
            }

            if (restoredAny)
            {
                VERADebugger.Log(
                    "Restored previously saved multiple-choice answer.",
                    "MultipleChoiceContent",
                    DebugPreference.Verbose);
            }
        }


        private bool RestoreLabelBasedSelection(string savedAnswer)
        {
            if (string.IsNullOrEmpty(savedAnswer) || savedAnswer == "[]" || savedAnswer == "-1")
                return false;

            HashSet<string> labelsToSelect = new HashSet<string>();

            if (allowMultiple)
            {
                try
                {
                    List<string> labels = JsonConvert.DeserializeObject<List<string>>(savedAnswer);
                    if (labels != null)
                    {
                        foreach (string label in labels)
                        {
                            if (!string.IsNullOrEmpty(label))
                                labelsToSelect.Add(label);
                        }
                    }
                }
                catch (JsonException)
                {
                    VERADebugger.LogWarning(
                        $"Could not parse saved multi-select answer for restore: {savedAnswer}",
                        "MultipleChoiceContent");
                    return false;
                }
            }
            else
            {
                labelsToSelect.Add(savedAnswer);
            }

            if (labelsToSelect.Count == 0)
                return false;

            bool restored = false;
            foreach (MultipleChoiceOption option in spawnedOptions)
            {
                if (labelsToSelect.Contains(option.OptionText))
                {
                    option.SetSelected(true);
                    restored = true;
                }
            }

            return restored;
        }


        private bool RestoreIndexBasedSelection(string savedAnswer)
        {
            if (string.IsNullOrEmpty(savedAnswer) || savedAnswer == "-1")
                return false;

            HashSet<int> indicesToSelect = new HashSet<int>();

            if (allowMultiple)
            {
                string[] parts = savedAnswer.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string part in parts)
                {
                    if (int.TryParse(part.Trim(), out int index))
                        indicesToSelect.Add(index);
                }
            }
            else if (int.TryParse(savedAnswer.Trim(), out int singleIndex))
            {
                indicesToSelect.Add(singleIndex);
            }

            if (indicesToSelect.Count == 0)
                return false;

            bool restored = false;
            foreach (MultipleChoiceOption option in spawnedOptions)
            {
                if (indicesToSelect.Contains(option.OptionIndex))
                {
                    option.SetSelected(true);
                    restored = true;
                }
            }

            return restored;
        }


        public override string GetResponse()
        {
            // When Other is enabled, submit option labels to match the backend / web client wire format.
            if (allowOtherOption)
                return GetLabelBasedResponse();

            if (allowMultiple)
            {
                List<int> selected = new List<int>();
                foreach (MultipleChoiceOption option in spawnedOptions)
                {
                    if (option.IsSelected)
                        selected.Add(option.OptionIndex);
                }
                return selected.Count > 0 ? string.Join(", ", selected) : "-1";
            }

            foreach (MultipleChoiceOption option in spawnedOptions)
            {
                if (option.IsSelected)
                    return option.OptionIndex.ToString();
            }
            return "-1";
        }


        private string GetLabelBasedResponse()
        {
            if (allowMultiple)
            {
                List<string> selectedLabels = new List<string>();
                foreach (MultipleChoiceOption option in spawnedOptions)
                {
                    if (option.IsSelected)
                        selectedLabels.Add(option.OptionText);
                }
                return selectedLabels.Count > 0 ? JsonConvert.SerializeObject(selectedLabels) : "[]";
            }

            foreach (MultipleChoiceOption option in spawnedOptions)
            {
                if (option.IsSelected)
                    return option.OptionText;
            }
            return string.Empty;
        }


        public override string GetOtherText()
        {
            if (!IsOtherSelected() || otherInputField == null)
                return null;

            string text = otherInputField.text?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }


        public override bool IsAnswered()
        {
            if (IsOtherSelected())
            {
                // Other requires non-empty free text
                return !string.IsNullOrWhiteSpace(otherInputField != null ? otherInputField.text : null);
            }

            // Selection (multi-select) does not require any selection to proceed
            if (allowMultiple) return true;

            // Multiple choice requires at least one option selected
            foreach (MultipleChoiceOption option in spawnedOptions)
            {
                if (option.IsSelected) return true;
            }
            return false;
        }


        #endregion


    }
}
