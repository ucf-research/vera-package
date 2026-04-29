using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VERA
{
    internal class SliderContent : SurveyQuestionContent
    {

        // SliderContent handles the display and response recording for slider questions


        #region VARIABLES


        [SerializeField] private TMP_Text leftText;
        [SerializeField] private TMP_Text rightText;
        [SerializeField] private TMP_Text valueDisplayText;
        [SerializeField] private Slider slider;


        #endregion


        #region DISPLAY QUESTION / RESPONSE


        public override void DisplayQuestion(VERASurveyQuestionInfo question)
        {
            base.DisplayQuestion(question);

            leftText.text = question.leftSliderText;
            rightText.text = question.rightSliderText;

            slider.value = slider.minValue + (slider.maxValue - slider.minValue) / 2f;
            UpdateValueDisplay(slider.value);

            slider.onValueChanged.AddListener(UpdateValueDisplay);
        }


        private void UpdateValueDisplay(float value)
        {
            valueDisplayText.text = Mathf.RoundToInt(value * 100f).ToString();
        }


        public override string GetResponse()
        {
            return slider.value.ToString("F2");
        }


        public override bool IsAnswered()
        {
            // Slider always has a value, so it is always considered answered
            return true;
        }


        #endregion


    }
}
