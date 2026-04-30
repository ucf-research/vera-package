using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace VERA
{
    internal class MatrixStatement : MonoBehaviour
    {

        // MatrixStatement represents a single row in a matrix question.
        // It displays a statement/question and a set of selectable options (one per column).


        #region VARIABLES


        [SerializeField] private TMP_Text statementText;
        [SerializeField] private RectTransform statementOptionsContainer;

        private List<MatrixStatementOption> spawnedOptions = new List<MatrixStatementOption>();
        private int selectedOptionIndex = -1;


        #endregion


        #region SETUP


        /// <summary>
        /// Initializes this row with the statement text and spawns one option per column.
        /// </summary>
        /// <param name="text">The statement/question text to display for this row.</param>
        /// <param name="columnCount">The number of columns (options) to create.</param>
        /// <param name="optionPrefab">The prefab to instantiate for each option cell.</param>
        public void Initialize(string text, int columnCount, MatrixStatementOption optionPrefab)
        {
            statementText.text = text;

            for (int i = 0; i < columnCount; i++)
            {
                MatrixStatementOption option = Object.Instantiate(optionPrefab, statementOptionsContainer);
                option.Initialize(i, OnOptionClicked);
                spawnedOptions.Add(option);
            }
        }


        #endregion


        #region SELECTION


        private void OnOptionClicked(int clickedIndex)
        {
            selectedOptionIndex = clickedIndex;

            foreach (MatrixStatementOption option in spawnedOptions)
            {
                if (option.OptionIndex != clickedIndex)
                    option.SetSelected(false);
            }
        }


        /// <summary>
        /// Returns the index of the currently selected column for this row, or -1 if none is selected.
        /// </summary>
        public int GetSelectedIndex()
        {
            return selectedOptionIndex;
        }


        #endregion


    }
}
