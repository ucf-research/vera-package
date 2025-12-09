
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
        }

        public enum DataType
        {
            Date,
            Number,
            Transform,
            String,
            JSON
        }

        public FileType fileType;
        public List<Column> columns = new List<Column>();
    }
}