using UnityEngine;
using VERA;

/// <summary>
/// Connects WebXR initialization parameters to VERA session management.
/// This script should not be touched directly by users; it serves as a bridge for WebXR setups, automatically handled internally by VERA.
/// </summary>
public class VERAWebXRHookup : MonoBehaviour
{

    // VERAWebXRHookup is a connection point for initialization from WebXR


    /// <summary>
    /// Connects WebXR initialization parameters to VERA session management.
    /// This function should not be touched directly by users; it serves as a bridge for WebXR setups, automatically handled internally by VERA.
    /// </summary>
    public void InitializeFromWebXR(string incomingParams)
    {
        // Parse the incoming parameters, site ID and participant ID
        // incomingParams is a string in the format "siteId=XYZ&participantId=ABC"
        var queryParams = System.Web.HttpUtility.ParseQueryString(incomingParams);
        string siteId = queryParams["siteId"];
        string participantId = queryParams["participantId"];

        VERADebugger.Log("Initializing VERA with Site ID: " + siteId + " and Participant ID: " + participantId, "VERAWebXRHookup", DebugPreference.Informative);
        VERASessionManager.ManualInitialization(siteId, participantId);
    }
}
