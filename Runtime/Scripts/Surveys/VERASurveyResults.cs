// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VERA
{
    /// <summary>
    /// Completed survey responses for the current participant.
    /// results are ordered by run, then by question number.
    /// sessionNumber and instanceNumber are set only when those filters were included in the request.
    /// </summary>
    public class VERASurveyResults
    {
        public string experimentId;
        public string surveyId;
        public string surveyName;
        public string participantId;
        public int pID;
        public int? sessionNumber;
        public int? instanceNumber;
        public VERASurveyResponse[] results;
    }

    /// <summary>
    /// One answered question from a survey completion.
    /// answer is always a string. A number or choice is text such as "4".
    /// A matrix, ranking, or multi-select answer is a JSON string such as {"row1":"Agree"}.
    /// otherText is the free-text Other box, or an empty string.
    /// ts is the client timestamp as digits or an ISO-8601 string.
    /// sessionNumber is null when that completion was not stored against a visit.
    /// </summary>
    public class VERASurveyResponse
    {
        public int pID;
        public string ts;
        public string studyId;
        public string surveyId;
        public string surveyName;
        public string instanceId;
        public string questionId;
        public string questionText;
        public string answer;
        public string otherText;
        public int? instanceNumber;
        public int? sessionNumber;
    }

    internal static class VERASurveyResultsParser
    {
        internal static bool TryParse(string responseText, out VERASurveyResults results, out string error)
        {
            results = null;
            error = null;

            if (string.IsNullOrEmpty(responseText))
            {
                error = "Survey results response was empty.";
                return false;
            }

            JObject json;
            try
            {
                json = JObject.Parse(responseText);
            }
            catch (Exception ex)
            {
                error = "Failed to parse survey results response: " + ex.Message;
                return false;
            }

            if (!IsSuccess(json))
            {
                error = ReadError(json) ?? "Failed to get survey responses.";
                return false;
            }

            JObject data = json["data"] as JObject;
            if (data == null)
            {
                error = "Survey results response did not include data.";
                return false;
            }

            results = new VERASurveyResults
            {
                experimentId = ReadString(data["experimentId"]),
                surveyId = ReadString(data["surveyId"]),
                surveyName = ReadString(data["surveyName"]),
                participantId = ReadString(data["participantId"]),
                pID = ReadParticipantNumber(data["pID"]),
                sessionNumber = ReadNullableInt(data["sessionNumber"]),
                instanceNumber = ReadNullableInt(data["instanceNumber"]),
                results = ReadRows(data["results"])
            };
            return true;
        }

        internal static string ReadErrorMessage(string responseText)
        {
            if (string.IsNullOrEmpty(responseText))
                return null;

            try
            {
                return ReadError(JObject.Parse(responseText));
            }
            catch
            {
                return null;
            }
        }

        private static bool IsSuccess(JObject json)
        {
            JToken success = json["success"];
            return success != null
                && success.Type == JTokenType.Boolean
                && success.Value<bool>();
        }

        private static string ReadError(JObject json)
        {
            JToken error = json["error"];
            if (error == null || error.Type == JTokenType.Null)
                return null;

            string message = error.Type == JTokenType.String ? error.Value<string>() : error.ToString();
            return string.IsNullOrEmpty(message) ? null : message;
        }

        private static VERASurveyResponse[] ReadRows(JToken token)
        {
            JArray rows = token as JArray;
            if (rows == null || rows.Count == 0)
                return new VERASurveyResponse[0];

            List<VERASurveyResponse> results = new List<VERASurveyResponse>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                JObject row = rows[i] as JObject;
                if (row == null)
                    continue;

                results.Add(new VERASurveyResponse
                {
                    pID = ReadParticipantNumber(row["pID"]),
                    ts = ReadTimestamp(row["ts"]),
                    studyId = ReadString(row["studyId"]),
                    surveyId = ReadString(row["surveyId"]),
                    surveyName = ReadString(row["surveyName"]),
                    instanceId = ReadString(row["instanceId"]),
                    questionId = ReadString(row["questionId"]),
                    questionText = ReadString(row["questionText"]),
                    answer = ReadString(row["answer"]),
                    otherText = ReadString(row["otherText"]),
                    instanceNumber = ReadNullableInt(row["instanceNumber"]),
                    sessionNumber = ReadNullableInt(row["sessionNumber"])
                });
            }

            return results.ToArray();
        }

        private static string ReadString(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return "";

            if (token.Type == JTokenType.String)
                return token.Value<string>() ?? "";

            return token.ToString(Formatting.None);
        }

        private static string ReadTimestamp(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return "";

            if (token.Type == JTokenType.String)
                return token.Value<string>() ?? "";

            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
                return token.ToString(Formatting.None);

            return token.ToString();
        }

        private static int ReadParticipantNumber(JToken token)
        {
            int? value = ReadNullableInt(token);
            if (value.HasValue)
                return value.Value;

            if (token != null && token.Type == JTokenType.String
                && VERAParticipantManager.TryParseParticipantShortId(token.Value<string>(), out int numericId))
            {
                return numericId;
            }

            return 0;
        }

        private static int? ReadNullableInt(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;

            if (token.Type == JTokenType.Integer)
                return token.Value<int>();

            if (token.Type == JTokenType.Float)
                return (int)token.Value<double>();

            if (token.Type == JTokenType.String && int.TryParse(token.Value<string>(), out int parsed))
                return parsed;

            return null;
        }
    }
}
