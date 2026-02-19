using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VERA
{
    internal static class VERAHost
    {

        private const string localHost = "http://localhost:4000/vera-portal";
        private const string testHost = "https://sherlock.gaim.ucf.edu/vera-portal";
        private const string liveHost = "https://vera-xr.io/vera-portal";

        // Global host URL - all other scripts which reference the host URL should use a reference to this
        // Set to localHost, testHost, or liveHost according to current needs.

        public const string hostUrl = localHost;

    }
}