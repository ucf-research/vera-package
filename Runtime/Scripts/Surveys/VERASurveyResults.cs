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
    /// Fetched results set sessionNumber and instanceNumber only when those filters were included in the request.
    /// A live submission from onSurveySubmitted sets sessionNumber to the current visit when one is assigned,
    /// and instanceNumber to the 1-based run of this survey during the current play session.
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

        /// <summary>
        /// Builds a results object from the answers the participant just submitted in the survey interface.
        /// sessionNumber is the current visit when one is assigned.
        /// instanceNumber is the 1-based run of this survey during the current play session.
        /// </summary>
        internal static VERASurveyResults FromLiveSubmission(VERASurveyInfo survey, SurveyQuestionAnswer[] answers, int instanceNumber, string instanceId)
        {
            string experimentId = "";
            string participantId = "";
            int pID = 0;
            int? sessionNumber = null;

            VERALogger logger = VERALogger.Instance;
            if (logger != null)
            {
                experimentId = logger.experimentUUID ?? "";
                VERAParticipantManager participant = logger.activeParticipant;
                if (participant != null)
                {
                    participantId = FirstNonEmpty(
                        participant.participantDatabaseId,
                        participant.participantShortId,
                        participant.sessionId,
                        participant.participantUUID);

                    int numericId = participant.GetNumericParticipantShortId();
                    if (numericId >= 0)
                        pID = numericId;

                    if (participant.sessionNumber >= 1)
                        sessionNumber = participant.sessionNumber;
                }
            }

            Dictionary<string, string> questionTextById = new Dictionary<string, string>();
            if (survey != null && survey.surveyQuestions != null)
            {
                for (int i = 0; i < survey.surveyQuestions.Count; i++)
                {
                    VERASurveyQuestionInfo question = survey.surveyQuestions[i];
                    if (question == null || string.IsNullOrEmpty(question.questionId))
                        continue;

                    questionTextById[question.questionId] = question.questionText ?? "";
                }
            }

            string timestamp = DateTime.UtcNow.ToString("o");
            string surveyId = survey != null ? survey.surveyId ?? "" : "";
            string surveyName = survey != null ? survey.surveyName ?? "" : "";
            string resolvedInstanceId = instanceId ?? "";
            int? resolvedInstanceNumber = instanceNumber >= 1 ? instanceNumber : (int?)null;

            List<VERASurveyResponse> rows = new List<VERASurveyResponse>();
            if (answers != null)
            {
                for (int i = 0; i < answers.Length; i++)
                {
                    SurveyQuestionAnswer answer = answers[i];
                    if (answer == null)
                        continue;

                    string questionText = "";
                    if (!string.IsNullOrEmpty(answer.questionId)
                        && questionTextById.TryGetValue(answer.questionId, out string lookedUpText))
                    {
                        questionText = lookedUpText ?? "";
                    }

                    rows.Add(new VERASurveyResponse
                    {
                        pID = pID,
                        ts = timestamp,
                        studyId = experimentId,
                        surveyId = surveyId,
                        surveyName = surveyName,
                        instanceId = resolvedInstanceId,
                        questionId = answer.questionId ?? "",
                        questionText = questionText ?? "",
                        answer = answer.answer ?? "",
                        otherText = answer.otherText ?? "",
                        instanceNumber = resolvedInstanceNumber,
                        sessionNumber = sessionNumber
                    });
                }
            }

            return new VERASurveyResults
            {
                experimentId = experimentId,
                surveyId = surveyId,
                surveyName = surveyName,
                participantId = participantId,
                pID = pID,
                sessionNumber = sessionNumber,
                instanceNumber = resolvedInstanceNumber,
                results = rows.ToArray()
            };
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
                return "";

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                    return values[i];
            }

            return "";
        }

        /// <summary>
        /// Builds results for a survey completed in the browser.
        /// Uses the completion payload when it contains response rows. Otherwise returns the survey identity with an empty response list.
        /// </summary>
        internal static VERASurveyResults FromWebCompletion(VERASurveyInfo survey, string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                if (VERASurveyResultsParser.TryParse(data, out VERASurveyResults parsed, out _))
                    return parsed;

                if (TryReadWebAnswers(data, out SurveyQuestionAnswer[] answers))
                    return FromLiveSubmission(survey, answers, 1, survey != null ? survey.surveyInstanceId : null);
            }

            return FromLiveSubmission(survey, null, 0, survey != null ? survey.surveyInstanceId : null);
        }

        private static bool TryReadWebAnswers(string data, out SurveyQuestionAnswer[] answers)
        {
            answers = null;

            JToken token;
            try
            {
                token = JToken.Parse(data);
            }
            catch
            {
                return false;
            }

            JArray rows = token as JArray;
            if (rows == null && token is JObject obj)
            {
                JObject dataObject = obj["data"] as JObject;
                rows = obj["results"] as JArray
                    ?? obj["answers"] as JArray
                    ?? obj["responses"] as JArray
                    ?? (dataObject != null ? dataObject["results"] as JArray : null)
                    ?? (dataObject != null ? dataObject["answers"] as JArray : null)
                    ?? (dataObject != null ? dataObject["responses"] as JArray : null);
            }

            if (rows == null || rows.Count == 0)
                return false;

            List<SurveyQuestionAnswer> parsed = new List<SurveyQuestionAnswer>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                JObject row = rows[i] as JObject;
                if (row == null)
                    continue;

                string questionId = ReadWebString(row["questionId"]);
                if (string.IsNullOrEmpty(questionId))
                    questionId = ReadWebString(row["question"]);

                string answer = ReadWebString(row["answer"]);
                if (string.IsNullOrEmpty(questionId) && string.IsNullOrEmpty(answer))
                    continue;

                parsed.Add(new SurveyQuestionAnswer
                {
                    questionId = questionId,
                    answer = answer,
                    otherText = ReadWebString(row["otherText"])
                });
            }

            if (parsed.Count == 0)
                return false;

            answers = parsed.ToArray();
            return true;
        }

        private static string ReadWebString(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return "";

            if (token.Type == JTokenType.String)
                return token.Value<string>() ?? "";

            return token.ToString(Formatting.None);
        }
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
