using TMPro;
using UnityEngine;

namespace VERA
{
    internal class MatrixHeaderOption : MonoBehaviour
    {

        // MatrixHeaderOption represents a single column header in a matrix question (e.g. "Strongly Agree").


        #region VARIABLES


        [SerializeField] private TMP_Text headerText;


        #endregion


        #region SETUP


        /// <summary>
        /// Initializes this header option with the given display text.
        /// </summary>
        /// <param name="text">The column header text to display.</param>
        public void Initialize(string text)
        {
            headerText.text = text;
        }


        #endregion


    }
}
