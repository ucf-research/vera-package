// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

using System.Collections.Generic;
using UnityEngine.Networking;

namespace VERA
{
    /// <summary>
    /// Parses the query string forwarded from the WebXR hosting page.
    /// URLSearchParams percent-encodes reserved characters, so values are decoded with UnityWebRequest.UnEscapeURL.
    /// </summary>
    internal static class WebXRQuery
    {
        public static Dictionary<string, string> Parse(string query)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(query))
                return result;

            foreach (var pair in query.Split('&'))
            {
                var parts = pair.Split(new[] { '=' }, 2);
                if (parts.Length != 2 || parts[0].Length == 0)
                    continue;

                var key = UnityWebRequest.UnEscapeURL(parts[0].Replace('+', ' '));
                var value = UnityWebRequest.UnEscapeURL(parts[1].Replace('+', ' '));
                result[key] = value;
            }

            return result;
        }
    }
}
