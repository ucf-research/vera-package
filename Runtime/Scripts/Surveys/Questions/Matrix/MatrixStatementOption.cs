using System;
using UnityEngine;
using UnityEngine.UI;

namespace VERA
{
    internal class MatrixStatementOption : MonoBehaviour
    {

        // MatrixStatementOption represents a single selectable cell in a matrix row.
        // Only one option per row can be selected at a time.


        #region VARIABLES


        [SerializeField] private Toggle toggle;
        [SerializeField] private PressableButton pressableButton;

        public int OptionIndex { get; private set; }
        public bool IsSelected { get; private set; }

        private Action<int> onOptionClicked;


        #endregion


        #region SETUP


        /// <summary>
        /// Initializes this option with its column index and click callback.
        /// </summary>
        /// <param name="index">Zero-based column index of this option within its row.</param>
        /// <param name="onClicked">Callback invoked with this option's index when clicked.</param>
        public void Initialize(int index, Action<int> onClicked)
        {
            OptionIndex = index;
            onOptionClicked = onClicked;

            SetSelected(false);

            pressableButton.OnPressed.AddListener(OnPressed);
        }


        #endregion


        #region SELECTION


        private void OnPressed()
        {
            if (IsSelected) return;

            SetSelected(true);
            onOptionClicked?.Invoke(OptionIndex);
        }


        /// <summary>
        /// Sets the selected state of this option and updates its visuals.
        /// </summary>
        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            toggle.SetIsOnWithoutNotify(selected);
            pressableButton.SetSelected(selected);
        }


        #endregion


    }
}
