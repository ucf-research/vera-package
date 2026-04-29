using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PressableButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{

    // PressableButton makes an Image hoverable and clickable, updating its color to reflect its current state,
    // and raises OnPressed when clicked.


    #region VARIABLES


    [SerializeField] private Image targetImage;

    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoveredColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField] private Color pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
    [SerializeField] private Color selectedColor = new Color(0.6f, 0.8f, 1f, 1f);
    [SerializeField] private Color selectedHoveredColor = new Color(0.75f, 0.9f, 1f, 1f);
    [SerializeField] private Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Range(0f, 0.5f)]
    [SerializeField] private float colorTransitionDuration = 0.08f;

    public UnityEvent OnPressed = new UnityEvent();

    private bool _interactable = true;
    public bool Interactable
    {
        get => _interactable;
        set
        {
            _interactable = value;
            isHovered = false;
            isPressed = false;
            ApplyColor();
        }
    }

    private bool isHovered = false;
    private bool isPressed = false;
    private bool _isSelected = false;
    public bool IsSelected => _isSelected;

    public Image TargetImage => targetImage;


    #endregion


    #region UNITY LIFECYCLE


    private void Start()
    {
        ApplyColor();
    }


    #endregion


    #region POINTER EVENTS


    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_interactable) return;
        isHovered = true;
        ApplyColor();
    }


    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
        ApplyColor();
    }


    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_interactable) return;
        isPressed = true;
        ApplyColor();
    }


    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        ApplyColor();
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_interactable) return;
        OnPressed.Invoke();
    }


    #endregion


    #region COLOR


    /// <summary>
    /// Sets the selected state of this button and updates its color.
    /// </summary>
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        ApplyColor();
    }


    /// <summary>
    /// Sets the colors for each interaction state and immediately re-applies the current state color.
    /// </summary>
    public void SetColors(Color normal, Color hovered, Color pressed, Color selected, Color selectedHovered, Color disabled)
    {
        normalColor = normal;
        hoveredColor = hovered;
        pressedColor = pressed;
        selectedColor = selected;
        selectedHoveredColor = selectedHovered;
        disabledColor = disabled;
        ApplyColor();
    }


    private void ApplyColor()
    {
        Color target;

        if (!_interactable)
            target = disabledColor;
        else if (isPressed)
            target = pressedColor;
        else if (isHovered && _isSelected)
            target = selectedHoveredColor;
        else if (isHovered)
            target = hoveredColor;
        else if (_isSelected)
            target = selectedColor;
        else
            target = normalColor;

        if (colorTransitionDuration > 0f)
            LeanTween.value(targetImage.gameObject, targetImage.color, target, colorTransitionDuration).setOnUpdate((Color val) => targetImage.color = val);
        else
            targetImage.color = target;
    }


    #endregion


}
