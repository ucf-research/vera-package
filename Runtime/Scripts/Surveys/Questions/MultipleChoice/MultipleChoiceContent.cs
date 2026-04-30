using System.Collections.Generic;
using UnityEngine;

namespace VERA
{
    internal class MultipleChoiceContent : SurveyQuestionContent
    {

        // MultipleChoiceContent handles the display and response recording for multiple choice and selection questions


        #region VARIABLES


        [SerializeField] private MultipleChoiceOption multipleChoiceOptionPrefab;
        [SerializeField] private RectTransform optionsContainer;

        private bool allowMultiple;
        private List<MultipleChoiceOption> spawnedOptions = new List<MultipleChoiceOption>();


        #endregion


        #region DISPLAY QUESTION / RESPONSE


        public override void DisplayQuestion(VERASurveyQuestionInfo question)
        {
            base.DisplayQuestion(question);

            allowMultiple = question.questionType == VERASurveyQuestionInfo.VERASurveyQuestionType.Selection;

            foreach (string optionText in question.selectionOptions)
            {
                MultipleChoiceOption option = Instantiate(multipleChoiceOptionPrefab, optionsContainer);
                option.Initialize(spawnedOptions.Count, optionText, allowMultiple, OnOptionClicked);
                spawnedOptions.Add(option);
            }
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
        }


        public override string GetResponse()
        {
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
            else
            {
                foreach (MultipleChoiceOption option in spawnedOptions)
                {
                    if (option.IsSelected)
                        return option.OptionIndex.ToString();
                }
                return "-1";
            }
        }


        public override bool IsAnswered()
        {
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
