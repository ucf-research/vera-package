// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VERA
{
    [CustomEditor(typeof(VERALogger))]
    internal class VERALoggerEditor : UnityEditor.Editor
    {

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            VERALogger csvWriter = (VERALogger)target;

            // Ensure user is authenticated
            if (!Application.isPlaying && string.IsNullOrEmpty(PlayerPrefs.GetString("VERA_UserAuthToken")))
            {
                EditorGUILayout.HelpBox("You are not authenticated. In the Menu Bar at the top of the editor, please click on the 'VERA' dropdown, then select 'Settings'.", MessageType.Warning);
            }
            // Ensure user has an active experiment selected
            else if (!Application.isPlaying && (string.IsNullOrEmpty(PlayerPrefs.GetString("VERA_ActiveExperiment")) || PlayerPrefs.GetString("VERA_ActiveExperiment") == "N/A"))
            {
                EditorGUILayout.HelpBox("No active experiment has been selected in the VERA settings. In the Menu Bar at the top of the editor, please click on the 'VERA' dropdown, then select 'Settings'.", MessageType.Warning);
            }
            else
            {
                if (Application.isPlaying && GUILayout.Button("Submit all CSVs"))
                {
                    csvWriter.SubmitAllCSVs();
                }
                if (Application.isPlaying && GUILayout.Button("Finalize Session"))
                {
                    csvWriter.FinalizeSession();
                }
            }

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(csvWriter);
            }
        }
    }
}
#endif
