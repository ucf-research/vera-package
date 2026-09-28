// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

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
