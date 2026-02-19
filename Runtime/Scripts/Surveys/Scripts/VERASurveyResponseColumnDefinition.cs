using UnityEngine;
using System.Collections.Generic;

namespace VERA
{
    internal static class VERASurveyResponseColumnDefinition
    {
        public static VERAColumnDefinition Create()
        {
            var columnDefinition = ScriptableObject.CreateInstance<VERAColumnDefinition>();

            columnDefinition.fileType = new VERAColumnDefinition.FileType
            {
                fileTypeId = "survey-responses", // Placeholder - will be fetched from server
                name = "Survey_Responses",
                description = "Survey response data collected from participants",
                skipUpload = true // Per-instance files are uploaded immediately; this shared file is just a local backup
            };

            columnDefinition.skipAutoColumns = true;

            columnDefinition.columns = new List<VERAColumnDefinition.Column>
            {
                new VERAColumnDefinition.Column
                {
                    name = "pID",
                    description = "Participant ID",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "ts",
                    description = "Seconds since session start (realtimeSinceStartup)",
                    type = VERAColumnDefinition.DataType.Number
                },
                new VERAColumnDefinition.Column
                {
                    name = "studyId",
                    description = "Experiment UUID",
                    type = VERAColumnDefinition.DataType.String
                },
                new VERAColumnDefinition.Column
                {
                    name = "surveyId",
                    description = "Survey UUID from the portal",
                    type = VERAColumnDefinition.DataType.String
                },
                new VERAColumnDefinition.Column
                {
                    name = "surveyName",
                    description = "Human-readable survey name",
                    type = VERAColumnDefinition.DataType.String
                },
                new VERAColumnDefinition.Column
                {
                    name = "instanceId",
                    description = "Unique ID for this survey completion",
                    type = VERAColumnDefinition.DataType.String
                },
                new VERAColumnDefinition.Column
                {
                    name = "questionId",
                    description = "Question ID from the portal",
                    type = VERAColumnDefinition.DataType.String
                },
                new VERAColumnDefinition.Column
                {
                    name = "questionText",
                    description = "The question text",
                    type = VERAColumnDefinition.DataType.String
                },
                new VERAColumnDefinition.Column
                {
                    name = "answer",
                    description = "The participant's response",
                    type = VERAColumnDefinition.DataType.String
                }
            };

            return columnDefinition;
        }
    }
}
