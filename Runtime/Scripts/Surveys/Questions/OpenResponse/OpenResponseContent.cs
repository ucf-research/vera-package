// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

using TMPro;
using UnityEngine;

namespace VERA
{
    internal class OpenResponseContent : SurveyQuestionContent
    {

        // OpenResponseContent handles the display and response recording for free-text / open response questions


        #region VARIABLES


        [SerializeField] private TMP_InputField responseInputField;


        #endregion


        #region DISPLAY QUESTION / RESPONSE


        public override void DisplayQuestion(VERASurveyQuestionInfo question)
        {
            base.DisplayQuestion(question);

            if (responseInputField == null)
            {
                VERADebugger.LogError(
                    "OpenResponseContent.responseInputField is not assigned. Assign a multiline TMP_InputField on the Open Response content prefab.",
                    "OpenResponseContent");
                return;
            }

            if (question.answerInputMode == VERASurveyQuestionInfo.VERASurveyAnswerInputMode.Voice)
            {
                VERADebugger.LogWarning(
                    "Open response answerInputMode is \"voice\", which is not yet supported in Unity. Falling back to text input.",
                    "OpenResponseContent");
            }

            responseInputField.lineType = TMP_InputField.LineType.MultiLineNewline;
            responseInputField.characterLimit = VERASurveyQuestionInfo.OPEN_RESPONSE_MAX_LENGTH;
            responseInputField.text = string.Empty;

            if (responseInputField.placeholder is TMP_Text placeholder)
            {
                placeholder.text = string.IsNullOrWhiteSpace(question.answerPlaceholder)
                    ? "Type your response..."
                    : question.answerPlaceholder;
            }

            SurveyInputFieldVirtualKeyboard.EnsureOn(responseInputField);
        }


        public override void ApplySavedAnswer(SurveyQuestionAnswer savedAnswer)
        {
            if (responseInputField == null || savedAnswer == null || string.IsNullOrEmpty(savedAnswer.answer))
                return;

            responseInputField.text = savedAnswer.answer;
        }


        public override string GetResponse()
        {
            if (responseInputField == null)
                return string.Empty;

            string text = responseInputField.text ?? string.Empty;
            if (text.Length > VERASurveyQuestionInfo.OPEN_RESPONSE_MAX_LENGTH)
                text = text.Substring(0, VERASurveyQuestionInfo.OPEN_RESPONSE_MAX_LENGTH);

            return text.Trim();
        }


        public override bool IsAnswered()
        {
            // Require non-empty free text to proceed
            return !string.IsNullOrWhiteSpace(GetResponse());
        }


        #endregion


    }
}
