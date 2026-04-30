using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VERA
{
    internal class VERASurveyColorSetter : MonoBehaviour
    {

        public enum ColorOverrideType
        {
            None,
            Background,
            BackgroundHigh,
            Element,
            Primary,
            Secondary,
            Highlight,
        }

        [SerializeField] private ColorOverrideType overrideType = ColorOverrideType.None;

        private void Awake()
        {
            ApplyColors();
        }


        /// <summary>
        /// Applies SurveyDisplay colors to all relevant UI elements on this GameObject and its children.
        /// Safe to call at runtime to re-apply colors after dynamic UI changes.
        /// </summary>
        public void ApplyColors()
        {
            var handledImages = new HashSet<Image>();
            var handledTexts = new HashSet<TMP_Text>();

            // Buttons
            foreach (var button in GetComponents<Button>())
            {
                SetSelectableGraphic(button, Color.white, handledImages);
                button.colors = MakeColorBlock(
                    SurveyDisplay.BUTTON_ACTIVE_COLOR,
                    SurveyDisplay.BUTTON_HOVER_COLOR,
                    SurveyDisplay.BUTTON_SELECTED_COLOR,
                    SurveyDisplay.BUTTON_INACTIVE_COLOR);

            }

            // Toggles (radio buttons, checkboxes)
            foreach (var toggle in GetComponents<Toggle>())
            {
                SetSelectableGraphic(toggle, SurveyDisplay.ELEMENT_COLOR, handledImages);
                toggle.colors = MakeColorBlock(
                    Color.white,
                    SurveyDisplay.BUTTON_HOVER_COLOR,
                    SurveyDisplay.BUTTON_SELECTED_COLOR,
                    SurveyDisplay.BUTTON_INACTIVE_COLOR);

                // Checkmark / indicator graphic
                if (toggle.graphic is Image checkmark)
                {
                    checkmark.color = SurveyDisplay.HIGHLIGHT_COLOR;
                    handledImages.Add(checkmark);
                }
            }

            // PressableButtons
            foreach (var pressableButton in GetComponents<PressableButton>())
            {
                pressableButton.SetColors(
                    SurveyDisplay.BUTTON_ACTIVE_COLOR,
                    SurveyDisplay.BUTTON_HOVER_COLOR,
                    SurveyDisplay.BUTTON_SELECTED_COLOR,
                    SurveyDisplay.BUTTON_SELECTED_COLOR,
                    SurveyDisplay.BUTTON_SELECTED_HOVER_COLOR,
                    SurveyDisplay.BUTTON_INACTIVE_COLOR);

                if (pressableButton.TargetImage != null)
                    handledImages.Add(pressableButton.TargetImage);
            }

            // TMP Dropdowns
            foreach (var dropdown in GetComponents<TMP_Dropdown>())
            {
                SetSelectableGraphic(dropdown, Color.white, handledImages);
                dropdown.colors = MakeColorBlock(
                    SurveyDisplay.ELEMENT_COLOR,
                    SurveyDisplay.BUTTON_HOVER_COLOR,
                    SurveyDisplay.BUTTON_SELECTED_COLOR,
                    SurveyDisplay.BUTTON_INACTIVE_COLOR);

                if (dropdown.captionText != null)
                {
                    dropdown.captionText.color = SurveyDisplay.TEXT_PRIMARY_COLOR;
                    handledTexts.Add(dropdown.captionText);
                }
                if (dropdown.itemText != null)
                {
                    dropdown.itemText.color = SurveyDisplay.TEXT_PRIMARY_COLOR;
                    handledTexts.Add(dropdown.itemText);
                }
            }

            // TMP InputFields
            foreach (var inputField in GetComponents<TMP_InputField>())
            {
                SetSelectableGraphic(inputField, Color.white, handledImages);
                inputField.colors = MakeColorBlock(
                    SurveyDisplay.ELEMENT_COLOR,
                    SurveyDisplay.BUTTON_HOVER_COLOR,
                    SurveyDisplay.BUTTON_SELECTED_COLOR,
                    SurveyDisplay.BUTTON_INACTIVE_COLOR);

                if (inputField.textComponent != null)
                {
                    inputField.textComponent.color = SurveyDisplay.TEXT_PRIMARY_COLOR;
                    handledTexts.Add(inputField.textComponent);
                }
                if (inputField.placeholder is TMP_Text placeholder)
                {
                    placeholder.color = SurveyDisplay.TEXT_SECONDARY_COLOR;
                    handledTexts.Add(placeholder);
                }
            }

            // Scrollbars — targetGraphic is the handle; track is an unregistered Image
            foreach (var scrollbar in GetComponents<Scrollbar>())
            {
                SetSelectableGraphic(scrollbar, Color.white, handledImages);
                scrollbar.colors = MakeColorBlock(
                    SurveyDisplay.BUTTON_HOVER_COLOR,
                    SurveyDisplay.HIGHLIGHT_COLOR,
                    SurveyDisplay.BUTTON_SELECTED_HOVER_COLOR,
                    SurveyDisplay.BUTTON_INACTIVE_COLOR);
            }

            // Sliders
            foreach (var slider in GetComponents<Slider>())
            {
                SetSelectableGraphic(slider, Color.white, handledImages);
                slider.colors = MakeColorBlock(
                    Color.white,
                    SurveyDisplay.BUTTON_HOVER_COLOR,
                    SurveyDisplay.HIGHLIGHT_COLOR,
                    SurveyDisplay.BUTTON_INACTIVE_COLOR);

                if (slider.fillRect != null)
                {
                    var fillImg = slider.fillRect.GetComponent<Image>();
                    if (fillImg != null)
                    {
                        fillImg.color = SurveyDisplay.HIGHLIGHT_COLOR;
                        handledImages.Add(fillImg);
                    }
                }
                if (slider.handleRect != null)
                {
                    var handleImg = slider.handleRect.GetComponent<Image>();
                    if (handleImg != null)
                    {
                        handleImg.color = SurveyDisplay.HIGHLIGHT_COLOR;
                        handledImages.Add(handleImg);
                    }
                }
            }

            // All TMP_Text — primary color, except previously handled (e.g. input field placeholder)
            foreach (var text in GetComponents<TMP_Text>())
            {
                if (!handledTexts.Contains(text))
                    text.color = overrideType == ColorOverrideType.Secondary ? SurveyDisplay.TEXT_SECONDARY_COLOR : SurveyDisplay.TEXT_PRIMARY_COLOR;
            }

            // All remaining Images → element background color
            foreach (var img in GetComponents<Image>())
            {
                if (!handledImages.Contains(img))
                {
                    switch (overrideType)
                    {
                        case ColorOverrideType.Background:
                            img.color = SurveyDisplay.BACKGROUND_COLOR;
                            break;
                        case ColorOverrideType.BackgroundHigh:
                            img.color = SurveyDisplay.HIGH_BACKGROUND_COLOR;
                            break;
                        case ColorOverrideType.Element:
                            img.color = SurveyDisplay.ELEMENT_COLOR;
                            break;
                        case ColorOverrideType.Highlight:
                            img.color = SurveyDisplay.HIGHLIGHT_COLOR;
                            break;
                        case ColorOverrideType.Primary:
                            img.color = SurveyDisplay.TEXT_PRIMARY_COLOR;
                            break;
                        case ColorOverrideType.Secondary:
                            img.color = SurveyDisplay.TEXT_SECONDARY_COLOR;
                            break;
                        default:
                            img.color = SurveyDisplay.ELEMENT_COLOR;
                            break;
                    }
                }
            }
        }


        /// <summary>
        /// Sets the targetGraphic Image of a Selectable to the given color and marks it as handled.
        /// </summary>
        private static void SetSelectableGraphic(Selectable selectable, Color color, HashSet<Image> handled)
        {
            if (selectable.targetGraphic is Image img)
            {
                img.color = color;
                handled.Add(img);
            }
        }


        /// <summary>
        /// Creates a ColorBlock with the given colors for each interaction state.
        /// </summary>
        private static ColorBlock MakeColorBlock(Color normal, Color highlighted, Color pressed, Color disabled)
        {
            ColorBlock block = ColorBlock.defaultColorBlock;
            block.normalColor = normal;
            block.highlightedColor = highlighted;
            block.pressedColor = pressed;
            block.selectedColor = highlighted;
            block.disabledColor = disabled;
            block.colorMultiplier = 1f;
            return block;
        }

    }
}