using System;
using UnityEngine;
using UnityEngine.Networking;

namespace VERA
{
    internal static class VERAHost
    {

        private const string localHost = "http://localhost:4000/vera-portal";
        private const string testHost = "https://sherlock.gaim.ucf.edu/vera-portal";
        private const string liveHost = "https://vera-xr.io";

        // Global host URL - all other scripts which reference the host URL should use a reference to this
        // Set to localHost, testHost, or liveHost according to current needs.

        public const string hostUrl = liveHost;

        /// <summary>
        /// User-Agent sent on VERA API requests. Required so AWS WAF CommonRuleSet
        /// (NoUserAgent_HEADER) does not block uploads and other portal calls.
        /// </summary>
        public static string UserAgent => $"VERA-Unity/{Application.unityVersion}";

        /// <summary>
        /// Sets a User-Agent header when the platform allows it.
        /// </summary>
        public static void ApplyUserAgent(UnityWebRequest request)
        {
            if (request == null) return;

            try
            {
                request.SetRequestHeader("User-Agent", UserAgent);
            }
            catch (InvalidOperationException)
            {
                // Some platforms (notably WebGL) disallow overriding User-Agent.
            }
        }

        /// <summary>
        /// Applies User-Agent and Bearer Authorization headers for authenticated VERA API calls.
        /// </summary>
        public static void ApplyBearerAuth(UnityWebRequest request, string bearerToken)
        {
            ApplyUserAgent(request);
            if (request == null || string.IsNullOrEmpty(bearerToken)) return;
            request.SetRequestHeader("Authorization", "Bearer " + bearerToken);
        }

    }
}
