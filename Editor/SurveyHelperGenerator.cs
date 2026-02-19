#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;

namespace VERA
{
    internal static class SurveyHelperGenerator
    {
        // SurveyHelperGenerator will generate .cs files for surveys, with associated helpers for starting surveys


        #region VARIABLES AND PATHS


        private const string generatedCsPath = "Assets/VERA/Surveys/GeneratedCode/";
        private const string generatedSurveyInfoPath = "Assets/VERA/Surveys/Resources/GeneratedSurveyInfos/";


        #endregion


        #region CLEAR GENERATED FILES


        // Deletes all generated SurveyInfos
        public static void ClearAllSurveyInfos()
        {
            // Do the same for SurveyInfo assets
            if (Directory.Exists(generatedSurveyInfoPath))
            {
                string[] surveyInfoFiles = Directory.GetFiles(generatedSurveyInfoPath, "*.asset");

                foreach (string file in surveyInfoFiles)
                {
                    AssetDatabase.DeleteAsset(file);
                }
            }

            AssetDatabase.Refresh();
        }


        #endregion


        #region FETCH & CONVERT SURVEYS


        // Fetches surveys from the server and generates SurveyInfo ScriptableObjects for each
        public static void FetchAndConvertSurveys()
        {
            VERABuildAuthInfo currentAuthInfo = VERAAuthenticator.GetSavedBuildAuthInfo();
            string activeExpId = currentAuthInfo.activeExperiment;
            string apiKey = currentAuthInfo.buildAuthToken;

            // Ensure the output directory exists
            if (!Directory.Exists(generatedSurveyInfoPath))
            {
                Directory.CreateDirectory(generatedSurveyInfoPath);
            }

            // Send the request to get the surveys for the active experiment
            string url = $"{VERAHost.hostUrl}/api/experiments/{activeExpId}/runnable-surveys";

            UnityWebRequest request = new UnityWebRequest(url, "GET");
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            var operation = request.SendWebRequest();

            EditorApplication.update += EditorUpdate;

            void EditorUpdate()
            {
                if (operation.isDone)
                {
                    EditorApplication.update -= EditorUpdate;

                    // Check for errors
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        VERADebugger.LogError($"Error fetching surveys for experiment {activeExpId}: {request.error}", "SurveyHelperGenerator");
                        request.Dispose();
                        return;
                    }

                    // Parse response
                    string jsonResponse = request.downloadHandler.text;
                    request.Dispose();

                    try
                    {
                        RunnableSurveysResponse response = JsonUtility.FromJson<RunnableSurveysResponse>(jsonResponse);

                        // Create SurveyInfo assets and associated code helpers for each survey
                        List<VERASurveyInfo> createdSurveyInfos = new List<VERASurveyInfo>();
                        foreach (var survey in response.surveys)
                        {
                            VERASurveyInfo surveyInfo = CreateSurveyInfoAsset(survey);
                            createdSurveyInfos.Add(surveyInfo);
                        }

                        // Create the survey helper code file that references all the created SurveyInfo assets
                        GenerateSurveyHelperCode(createdSurveyInfos);
                    }
                    catch (Exception ex)
                    {
                        VERADebugger.LogError($"Error parsing survey {activeExpId}: {ex.Message}", "SurveyHelperGenerator");
                    }
                }
            }
        }


        #endregion


        #region SURVEYINFO ASSET CREATION


        // Creates a SurveyInfo ScriptableObject from survey data
        private static VERASurveyInfo CreateSurveyInfoAsset(EditorSurvey survey)
        {
            VERASurveyInfo surveyInfo = ScriptableObject.CreateInstance<VERASurveyInfo>();

            // Convert general info
            surveyInfo.surveyName = survey.surveyName;
            surveyInfo.surveyDescription = survey.surveyDescription;
            surveyInfo.surveyEndStatement = survey.surveyEndStatement;
            surveyInfo.surveyId = survey._id;

            List<VERASurveyQuestionInfo> surveyQuestionInfos = new List<VERASurveyQuestionInfo>();

            // Convert questions
            if (survey.questions != null)
            {
                foreach (EditorSurveyQuestion question in survey.questions)
                {
                    VERASurveyQuestionInfo currentQuestion = new VERASurveyQuestionInfo();
                    currentQuestion.questionText = question.questionText;
                    currentQuestion.orderInSurvey = question.questionNumberInSurvey;
                    currentQuestion.questionId = question._id;

                    switch (question.questionType)
                    {
                        case "selection":
                        case "multipleSelection":
                        case "multiple_selection":
                            currentQuestion.questionType = VERASurveyQuestionInfo.VERASurveyQuestionType.Selection;
                            currentQuestion.selectionOptions = question.questionOptions?.ToArray() ?? new string[0];
                            break;
                        case "multiple_choice":
                        case "multipleChoice":
                            currentQuestion.questionType = VERASurveyQuestionInfo.VERASurveyQuestionType.MultipleChoice;
                            currentQuestion.selectionOptions = question.questionOptions?.ToArray() ?? new string[0];
                            break;
                        case "slider":
                            currentQuestion.questionType = VERASurveyQuestionInfo.VERASurveyQuestionType.Slider;
                            currentQuestion.leftSliderText = question.leftSliderText;
                            currentQuestion.rightSliderText = question.rightSliderText;
                            break;
                        case "matrix":
                        case "likert":
                            currentQuestion.questionType = VERASurveyQuestionInfo.VERASurveyQuestionType.Matrix;
                            if (question.matrixColumnNames == null || question.matrixColumnNames.Count == 0)
                            {
                                VERADebugger.LogWarning($"Matrix/Likert question {question._id} is missing column names. Defaulting to standard 5-point Likert scale. (Question text is \"" + question.questionText + "\")", "SurveyHelperGenerator");
                                currentQuestion.matrixColumnTexts = new string[] { "Strongly Disagree", "Disagree", "Neutral", "Agree", "Strongly Agree" };
                            }
                            else
                            {
                                currentQuestion.matrixColumnTexts = question.matrixColumnNames?.ToArray();
                            }

                            if (question.questionOptions == null || question.questionOptions.Count == 0)
                            {
                                VERADebugger.LogWarning($"Matrix/Likert question {question._id} is missing row texts. Defaulting to a single empty row. (Question text is \"" + question.questionText + "\")", "SurveyHelperGenerator");
                                currentQuestion.matrixRowTexts = new string[] { "" };
                            }
                            else
                            {
                                currentQuestion.matrixRowTexts = question.questionOptions?.ToArray();
                            }
                            break;
                        default:
                            VERADebugger.LogError($"Unsupported survey question type: {question.questionType}. Running this survey may result in unexpected behavior. (Question text is \"" + question.questionText + "\")", "SurveyHelperGenerator");
                            break;
                    }

                    surveyQuestionInfos.Add(currentQuestion);
                }
            }

            // Sort questions by order
            surveyQuestionInfos = surveyQuestionInfos.OrderBy(q => q.orderInSurvey).ToList();
            surveyInfo.surveyQuestions = surveyQuestionInfos;

            // Save as asset
            string sanitizedName = SanitizeFileName(survey.surveyName);
            if (string.IsNullOrEmpty(sanitizedName))
            {
                sanitizedName = $"Survey_{survey._id}";
            }

            string assetPath = $"{generatedSurveyInfoPath}{sanitizedName}.asset";

            // Delete existing asset if it exists
            if (AssetDatabase.LoadAssetAtPath<VERASurveyInfo>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            AssetDatabase.CreateAsset(surveyInfo, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            VERADebugger.Log($"Created SurveyInfo asset: {assetPath}", "SurveyHelperGenerator", DebugPreference.Verbose);

            return surveyInfo;
        }


        #endregion


        #region SURVEY HELPER CODE GENERATION


        // Generates the static survey helper class code that references all created SurveyInfo assets
        private static void GenerateSurveyHelperCode(List<VERASurveyInfo> surveyInfos)
        {
            if (surveyInfos == null || surveyInfos.Count == 0)
            {
                VERADebugger.LogWarning("No surveys found to generate helper code for.", "SurveyHelperGenerator");
                return;
            }

            StringBuilder sb = new StringBuilder();

            // Generate class header
            GenerateSurveyHelperClassHeader(sb);

            // Generate enum of survey references
            GenerateSurveyReferenceEnum(sb, surveyInfos);

            // Generate survey ID to reference dictionary
            GenerateSurveyIdToReferenceMap(sb, surveyInfos);

            // Generate GetSurveyReferenceById method
            GenerateGetSurveyReferenceByIdMethod(sb);

            // Generate StartSurvey method
            GenerateStartSurveyMethod(sb, surveyInfos);

            // Close class and namespace
            sb.AppendLine("\t}");
            sb.AppendLine("}");

            // Write to file
            try
            {
                string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", generatedCsPath));
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                }

                string filePath = Path.Combine(fullPath, "VERASurveyHelper.cs");

                // Compare with existing file, only write if changed
                if (File.Exists(filePath))
                {
                    string existingContent = File.ReadAllText(filePath);
                    if (existingContent == sb.ToString())
                    {
                        VERADebugger.Log("Survey helper code unchanged, skipping write.", "SurveyHelperGenerator", DebugPreference.Verbose);
                        return;
                    }
                }

                File.WriteAllText(filePath, sb.ToString());
                AssetDatabase.ImportAsset(Path.Combine(generatedCsPath, "VERASurveyHelper.cs"), ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();

                VERADebugger.Log("Generated VERASurveyHelper.cs successfully.", "SurveyHelperGenerator", DebugPreference.Verbose);
            }
            catch (Exception ex)
            {
                VERADebugger.LogError($"Error generating survey helper code: {ex.Message}\n{ex.StackTrace}", "SurveyHelperGenerator");
            }
        }

        // Generates the class header for the VERASurveyHelper class
        private static void GenerateSurveyHelperClassHeader(StringBuilder sb)
        {
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace VERA");
            sb.AppendLine("{");
            sb.AppendLine("\t/// <summary>");
            sb.AppendLine("\t/// Static helper class for starting surveys in your VERA experiment.");
            sb.AppendLine("\t/// <br/><br/>This class has been automatically generated based on your surveys defined in the VERA portal for your currently selected experiment.");
            sb.AppendLine("\t/// Use the StartSurvey() method to begin a survey for the current participant.");
            sb.AppendLine("\t/// </summary>");
            sb.AppendLine("\tpublic static class VERASurveyHelper");
            sb.AppendLine("\t{");
            sb.AppendLine();
        }

        // Generates the VERASurveyReference enum
        private static void GenerateSurveyReferenceEnum(StringBuilder sb, List<VERASurveyInfo> surveyInfos)
        {
            sb.AppendLine("\t\t/// <summary>");
            sb.AppendLine("\t\t/// Enum of available surveys in this experiment.");
            sb.AppendLine("\t\t/// <br/><br/>This enum has been generated based on your surveys defined in the VERA portal for your currently selected experiment.");
            sb.AppendLine("\t\t/// Each enum value is prefixed with S_ to avoid issues with names starting with numbers or other invalid enum names.");
            sb.AppendLine("\t\t/// </summary>");
            sb.AppendLine("\t\tpublic enum VERASurveyReference");
            sb.AppendLine("\t\t{");

            foreach (var surveyInfo in surveyInfos)
            {
                string enumName = SanitizeForEnum(surveyInfo.surveyName);
                sb.AppendLine($"\t\t\t/// <summary>{surveyInfo.surveyName}</summary>");
                sb.AppendLine($"\t\t\tS_{enumName},");
            }

            sb.AppendLine("\t\t}");
            sb.AppendLine();
        }

        // Generates the survey ID to reference dictionary
        private static void GenerateSurveyIdToReferenceMap(StringBuilder sb, List<VERASurveyInfo> surveyInfos)
        {
            sb.AppendLine("\t\t/// <summary>");
            sb.AppendLine("\t\t/// Mapping of survey IDs to VERASurveyReference enum values for easy lookup when only the survey ID is known (e.g. in trials)");
            sb.AppendLine("\t\t/// </summary>");
            sb.AppendLine("\t\tprivate static Dictionary<string, VERASurveyReference> surveyIdToReferenceMap = new Dictionary<string, VERASurveyReference>");
            sb.AppendLine("\t\t{");

            foreach (var surveyInfo in surveyInfos)
            {
                string enumName = SanitizeForEnum(surveyInfo.surveyName);
                sb.AppendLine($"\t\t\t{{ \"{surveyInfo.surveyId}\", VERASurveyReference.S_{enumName} }},");
            }

            sb.AppendLine("\t\t};");
            sb.AppendLine();
        }

        // Generates the GetSurveyReferenceById method
        private static void GenerateGetSurveyReferenceByIdMethod(StringBuilder sb)
        {
            sb.AppendLine("\t\t/// <summary>");
            sb.AppendLine("\t\t/// Gets the VERASurveyReference enum value corresponding to the given survey ID.");
            sb.AppendLine("\t\t/// <br/><br/>This method can be useful when utilizing trials or other features which reference surveys by their ID.");
            sb.AppendLine("\t\t/// </summary>");
            sb.AppendLine("\t\t/// <param name=\"surveyId\">The ID of the survey.</param>");
            sb.AppendLine("\t\t/// <returns>The corresponding VERASurveyReference enum value, or null if not found.</returns>");
            sb.AppendLine("\t\tpublic static VERASurveyReference? GetSurveyReferenceById(string surveyId)");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\tif (surveyIdToReferenceMap.TryGetValue(surveyId, out var reference))");
            sb.AppendLine("\t\t\t{");
            sb.AppendLine("\t\t\t\treturn reference;");
            sb.AppendLine("\t\t\t}");
            sb.AppendLine();
            sb.AppendLine("\t\t\tDebug.LogWarning($\"[VERASurveyHelper] No survey reference found for survey ID: {surveyId}\");");
            sb.AppendLine("\t\t\treturn null;");
            sb.AppendLine("\t\t}");
            sb.AppendLine();
        }

        // Generates the StartSurvey method
        private static void GenerateStartSurveyMethod(StringBuilder sb, List<VERASurveyInfo> surveyInfos)
        {
            sb.AppendLine("\t\t/// <summary>");
            sb.AppendLine("\t\t/// Starts the specified survey for the current participant.");
            sb.AppendLine("\t\t/// </summary>");
            sb.AppendLine("\t\t/// <param name=\"surveyToStart\">The survey to start, specified using the VERASurveyReference enum</param>");
            sb.AppendLine("\t\t/// <param name=\"transportToLobby\">Whether to temporarily transport the participant to a survey lobby while the survey is active. Default is true.</param>");
            sb.AppendLine("\t\t/// <param name=\"dimEnvironment\">Whether to dim the environment when transporting to the survey lobby. Default is true.</param>");
            sb.AppendLine("\t\t/// <param name=\"heightOffset\">How far the survey will be offset vertically from the user's head position. Default is 0.</param>");
            sb.AppendLine("\t\t/// <param name=\"distanceOffset\">How far the survey will be offset horizontally from the user's head position. Default is 3.</param>");
            sb.AppendLine("\t\t/// <param name=\"onSurveyComplete\">An optional callback Action that will be invoked when the survey is completed by the participant.</param>");
            sb.AppendLine("\t\tpublic static void StartSurvey(VERASurveyReference surveyToStart, bool transportToLobby = true, bool dimEnvironment = true, float heightOffset = 0f, float distanceOffset = 3f, Action onSurveyComplete = null)");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\t// Get the resource path for the selected survey");
            sb.AppendLine("\t\t\tstring resourcePath = surveyToStart switch");
            sb.AppendLine("\t\t\t{");

            foreach (var surveyInfo in surveyInfos)
            {
                string enumName = SanitizeForEnum(surveyInfo.surveyName);
                string sanitizedFileName = SanitizeFileName(surveyInfo.surveyName);
                if (string.IsNullOrEmpty(sanitizedFileName))
                {
                    sanitizedFileName = $"Survey_{surveyInfo.surveyId}";
                }
                sb.AppendLine($"\t\t\t\tVERASurveyReference.S_{enumName} => \"GeneratedSurveyInfos/{sanitizedFileName}\",");
            }

            sb.AppendLine("\t\t\t\t_ => throw new ArgumentException($\"Unknown survey reference: {surveyToStart}\")");
            sb.AppendLine("\t\t\t};");
            sb.AppendLine();
            sb.AppendLine("\t\t\t// Load the survey info from Resources");
            sb.AppendLine("\t\t\tVERASurveyInfo surveyInfo = Resources.Load<VERASurveyInfo>(resourcePath);");
            sb.AppendLine();
            sb.AppendLine("\t\t\tif (surveyInfo == null)");
            sb.AppendLine("\t\t\t{");
            sb.AppendLine("\t\t\t\tDebug.LogError($\"[VERASurveyHelper] Failed to load survey info at path: {resourcePath}\");");
            sb.AppendLine("\t\t\t\treturn;");
            sb.AppendLine("\t\t\t}");
            sb.AppendLine();
            sb.AppendLine("\t\t\t// Start the survey using VERASessionManager");
            sb.AppendLine("\t\t\tVERASessionManager.StartSurvey(surveyInfo, transportToLobby, dimEnvironment, heightOffset, distanceOffset, onSurveyComplete);");
            sb.AppendLine("\t\t}");
        }

        // Sanitizes a survey name for use in an enum
        private static string SanitizeForEnum(string surveyName)
        {
            if (string.IsNullOrEmpty(surveyName))
            {
                return "Unknown";
            }

            // Remove all non-alphanumeric characters and replace with nothing
            StringBuilder sb = new StringBuilder();
            foreach (char c in surveyName)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
            }

            string result = sb.ToString();

            // If the result is empty or starts with a digit, prepend something
            if (string.IsNullOrEmpty(result) || char.IsDigit(result[0]))
            {
                result = "Survey" + result;
            }

            return result;
        }


        #endregion


        #region PARSING HELPERS


        // Sanitizes a file name by removing invalid characters
        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return string.Empty;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder();
            foreach (char c in fileName)
            {
                if (Array.IndexOf(invalidChars, c) < 0)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Trim();
        }


        // Editor-only response class for parsing the runnable surveys endpoint
        [System.Serializable]
        private class RunnableSurveysResponse
        {
            public List<EditorSurvey> surveys;
        }


        // Editor-only survey class for JSON parsing
        [System.Serializable]
        private class EditorSurvey
        {
            public string _id;
            public string surveyName;
            public string surveyDescription;
            public string surveyEndStatement;
            public List<EditorSurveyQuestion> questions;
            public string createdAt;
            public int __v;
        }


        // Editor-only survey question class for JSON parsing
        [System.Serializable]
        private class EditorSurveyQuestion
        {
            public string _id;
            public string surveyParent;
            public int questionNumberInSurvey;
            public string questionText;
            public string questionType;
            public List<string> questionOptions;
            public List<string> matrixColumnNames;
            public string leftSliderText;
            public string rightSliderText;
            public string createdAt;
            public int __v;
        }


        #endregion


    }
}
#endif