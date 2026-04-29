using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MultipleChoiceOption : MonoBehaviour
{

    // MultipleChoiceOption represents a single option in a multiple choice or selection question.


    #region VARIABLES


    [SerializeField] private Sprite multipleChoiceToggleBgSprite;
    [SerializeField] private Sprite selectionToggleBgSprite;
    [SerializeField] private Image toggleBgImage;
    [SerializeField] private Sprite multipleChoiceCheckmarkSprite;
    [SerializeField] private Sprite selectionCheckmarkSprite;
    [SerializeField] private Image checkmarkImage;
    [SerializeField] private TMP_Text optionText;
    [SerializeField] private Toggle toggle;
    [SerializeField] private PressableButton pressableButton;

    public int OptionIndex { get; private set; }
    public bool IsSelected { get; private set; }

    private bool allowMultiple;
    private Action<int> onOptionClicked;


    #endregion


    #region SETUP


    /// <summary>
    /// Initializes this option with its index, display text, selection mode, and click callback.
    /// </summary>
    /// <param name="index">Zero-based index of this option within its question.</param>
    /// <param name="text">Display text for this option.</param>
    /// <param name="multipleAllowed">True for selection (multi-select), false for multiple choice (single-select).</param>
    /// <param name="onClicked">Callback invoked with this option's index when clicked.</param>
    public void Initialize(int index, string text, bool multipleAllowed, Action<int> onClicked)
    {
        OptionIndex = index;
        allowMultiple = multipleAllowed;
        onOptionClicked = onClicked;
        optionText.text = text;

        toggleBgImage.sprite = allowMultiple ? selectionToggleBgSprite : multipleChoiceToggleBgSprite;
        checkmarkImage.sprite = allowMultiple ? selectionCheckmarkSprite : multipleChoiceCheckmarkSprite;

        SetSelected(false);

        pressableButton.OnPressed.AddListener(OnPressed);
    }


    #endregion


    #region SELECTION


    private void OnPressed()
    {
        if (!allowMultiple && IsSelected) return;

        SetSelected(allowMultiple ? !IsSelected : true);
        onOptionClicked?.Invoke(OptionIndex);
    }


    /// <summary>
    /// Sets the selected state of this option and updates its visuals.
    /// </summary>
    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        toggle.SetIsOnWithoutNotify(selected);
        checkmarkImage.enabled = IsSelected;
        pressableButton.SetSelected(selected);
    }


    #endregion


}
