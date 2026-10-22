// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

using UnityEngine;
using VERA;

/// <summary>
/// Connects WebXR callbacks to VERA session management.
/// This script should not be touched directly by users; it serves as a bridge for WebXR setups, automatically handled internally by VERA.
/// The hosting page calls InitializeFromWebXR on the VERALogger object after the build loads.
/// </summary>
public class VERAWebXRHookup : MonoBehaviour
{
    /// <summary>
    /// Receives the WebXR hosting-page query string. Called by the page on the VERALogger object
    /// after the build loads. siteId and participantId are included when the landing page resolved
    /// them; every other query key is forwarded as well.
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public void InitializeFromWebXR(string incomingParams)
    {
        VERALogger logger = GetComponent<VERALogger>();
        if (logger == null)
        {
            VERADebugger.LogError("VERALogger is missing; WebXR URL parameters were not applied.", "VERAWebXRHookup");
            return;
        }

        logger.ApplyWebXRQuery(incomingParams);
    }

    /// <summary>
    /// This function is called from WebXR when the web survey that was launched from the WebXR
    /// environment has completed - this call allows VERA to respond to the survey completion
    /// (for example to resume the experience / unpause / advance the experiment flow).
    /// </summary>
    public void OnWebSurveyCompleted(string data)
    {
        VERADebugger.Log("Web survey completed, resuming experience...", "VERAWebXRHookup", DebugPreference.Informative);
        VERALogger.Instance.OnWebSurveyCompleted(data);
    }
}
