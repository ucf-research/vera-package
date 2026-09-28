// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

using UnityEngine;

namespace VERA
{
    internal static class VERATweenExtensions
    {
        public static VERATween.TweenHandle<float> TweenAlpha(this CanvasGroup canvasGroup, float to, float duration)
        {
            return VERATween.Value(canvasGroup.gameObject, canvasGroup.alpha, to, duration)
                .SetOnUpdate(alpha => canvasGroup.alpha = alpha);
        }
    }
}
