// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

#if UNITY_EDITOR
using System.Text;

namespace VERA
{
    /// <summary>
    /// Shared copyright / SPDX header prepended to all VERA-generated C# scripts.
    /// </summary>
    internal static class VERAGeneratedCodeHeader
    {
        internal static void Append(StringBuilder sb)
        {
            sb.AppendLine("// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>");
            sb.AppendLine("// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>");
            sb.AppendLine("// SPDX-License-Identifier: LicenseRef-VERA");
            sb.AppendLine();
        }
    }
}
#endif
