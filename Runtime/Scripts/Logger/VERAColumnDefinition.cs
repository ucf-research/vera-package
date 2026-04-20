
using System;
using System.Collections.Generic;
using UnityEngine;

namespace VERA
{
    [CreateAssetMenu(fileName = "VERAColumnDefinition", menuName = "ScriptableObjects/VERAColumnDefinition", order = 1)]
    internal class VERAColumnDefinition : ScriptableObject
    {
        [Serializable]
        public class Column
        {
            public string name;
            public string description;
            public DataType type;
        }

        [Serializable]
        public class FileType
        {
            public string fileTypeId;
            public string name;
            public string description;
            public string extension; // The expected file extension for this file type (e.g., "csv", "json", "txt")
            public bool skipUpload; // When true, this file type is NOT uploaded via the standard file type API (e.g., survey responses use a dedicated API)
        }

        public enum DataType
        {
            Date,
            Number,
            Transform,
            String,
            JSON,
            Boolean,
            Float
        }

        public FileType fileType;
        public bool skipAutoColumns; // When true, no auto-columns (pID, conditions, ts) are prepended; all columns are user-defined
        public List<Column> columns = new List<Column>();
    }
}
