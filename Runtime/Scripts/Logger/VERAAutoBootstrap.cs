// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

using UnityEngine;
using VERA;

internal static class VERAAutoBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitializeVERALogger()
    {
        if (GameObject.FindAnyObjectByType<VERALogger>() == null)
        {
            var go = new GameObject("VERALogger");
            go.AddComponent<VERALogger>();
            go.AddComponent<VERAWebXRHookup>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
