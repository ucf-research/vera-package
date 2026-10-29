// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

using System;

namespace VERA
{
    /// <summary>
    /// A single memory field stored on the VERA server.
    /// Participant memory is scoped to one participant. Experiment memory is shared by the whole experiment.
    /// </summary>
    [Serializable]
    public class VERAMemoryEntry
    {
        public string key;
        public string value;
        public int size;
        public bool storedExternally;
        public int pID;
        public string participantId;
        public string experimentId;
        public string createdAt;
        public string updatedAt;
    }

    [Serializable]
    internal class VERAMemoryValuePayload
    {
        public string value;
    }

    [Serializable]
    internal class VERAMemoryDeltaPayload
    {
        public int delta;
    }

    [Serializable]
    internal class VERAMemoryEntryResponse
    {
        public bool success;
        public string error;
        public string message;
        public VERAMemoryEntry entry;
    }

    [Serializable]
    internal class VERAMemoryListResponse
    {
        public bool success;
        public string error;
        public int pID;
        public string participantId;
        public string experimentId;
        public int count;
        public VERAMemoryEntry[] entries;
    }

    [Serializable]
    internal class VERAMemoryDeleteResponse
    {
        public bool success;
        public string error;
        public string message;
    }
}
