#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace VERA
{
    /// <summary>
    /// Editor window that displays the trial workflow execution order from the VERA API.
    /// Accessible from VERA menu -> View Trial Workflow.
    /// </summary>
    internal class VERATrialWorkflowViewer : EditorWindow
    {
        private List<TrialWorkflowItem> trialWorkflow = new List<TrialWorkflowItem>();
        private Vector2 scrollPosition;
        private bool isLoading = false;
        private string errorMessage = null;

        [MenuItem("VERA/View Trial Workflow")]
        public static void ShowWindow()
        {
            VERATrialWorkflowViewer window = GetWindow<VERATrialWorkflowViewer>("Trial Workflow");
            window.minSize = new Vector2(400, 300);
            window.Show();
            window.FetchTrialWorkflow();
        }

        private void OnGUI()
        {
            GUILayout.Label("Trial Workflow Execution Order", EditorStyles.boldLabel);
            GUILayout.Space(5);

            // Check authentication
            if (PlayerPrefs.GetInt("VERA_UserAuthenticated", 0) == 0)
            {
                EditorGUILayout.HelpBox("You must be authenticated to view the trial workflow. Go to VERA -> Settings to authenticate.", MessageType.Warning);
                return;
            }

            // Check if experiment is selected
            string activeExperiment = PlayerPrefs.GetString("VERA_ActiveExperiment", "");
            if (string.IsNullOrEmpty(activeExperiment))
            {
                EditorGUILayout.HelpBox("No experiment selected. Go to VERA -> Settings to select an experiment.", MessageType.Warning);
                return;
            }

            // Refresh button
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                FetchTrialWorkflow();
            }

            GUILayout.Space(10);

            // Loading state
            if (isLoading)
            {
                EditorGUILayout.HelpBox("Loading trial workflow...", MessageType.Info);
                return;
            }

            // Error state
            if (!string.IsNullOrEmpty(errorMessage))
            {
                EditorGUILayout.HelpBox(errorMessage, MessageType.Error);

                // If auth error, show button to open settings
                if (errorMessage.Contains("Authentication failed") || errorMessage.Contains("re-authenticate"))
                {
                    GUILayout.Space(5);
                    if (GUILayout.Button("Open VERA Settings", GUILayout.Width(150)))
                    {
                        VERASettingsWindow.ShowWindow();
                    }
                }

                return;
            }

            // Empty state
            if (trialWorkflow == null || trialWorkflow.Count == 0)
            {
                EditorGUILayout.HelpBox("No trials or surveys found in the workflow. Make sure your experiment has a trial workflow configured in the VERA portal.", MessageType.Info);
                return;
            }

            // Display trial workflow (includes trials and standalone surveys)
            GUILayout.Label($"Total Items: {trialWorkflow.Count}", EditorStyles.boldLabel);
            GUILayout.Space(5);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            for (int i = 0; i < trialWorkflow.Count; i++)
            {
                var item = trialWorkflow[i];
                DrawTrialItem(i + 1, item, 0);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTrialItem(int index, TrialWorkflowItem trial, int indentLevel)
        {
            // Choose background color based on item type (trial or survey)
            Color bgColor = GetTrialTypeColor(trial.type);
            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = originalBg;

            // Header with index and label
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indentLevel * 20);

            string typeLabel = GetTypeLabel(trial.type);
            string headerText = $"{index}. {trial.label}";
            if (!string.IsNullOrEmpty(typeLabel))
            {
                headerText += $" [{typeLabel}]";
            }

            GUILayout.Label(headerText, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            // Trial details
            EditorGUI.indentLevel++;

            // Description (for consent, entry, completion)
            if (!string.IsNullOrEmpty(trial.description))
            {
                EditorGUILayout.LabelField("Description", trial.description, EditorStyles.wordWrappedLabel);
            }

            if (!string.IsNullOrEmpty(trial.id))
            {
                EditorGUILayout.LabelField("ID", trial.id);
            }

            // Display standalone survey-specific fields
            if (trial.type == "survey")
            {
                if (!string.IsNullOrEmpty(trial.surveyName))
                {
                    EditorGUILayout.LabelField("Survey Name", trial.surveyName);
                }
                if (!string.IsNullOrEmpty(trial.surveyId))
                {
                    EditorGUILayout.LabelField("Survey ID", trial.surveyId);
                }
                if (!string.IsNullOrEmpty(trial.instanceId))
                {
                    EditorGUILayout.LabelField("Instance ID", trial.instanceId);
                }

                // Display survey data if available
                if (trial.survey != null)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Survey Details:", EditorStyles.miniBoldLabel);
                    EditorGUI.indentLevel++;

                    if (!string.IsNullOrEmpty(trial.survey.surveyName))
                    {
                        EditorGUILayout.LabelField("Full Name", trial.survey.surveyName);
                    }
                    if (!string.IsNullOrEmpty(trial.survey.surveyDescription))
                    {
                        EditorGUILayout.LabelField("Description", trial.survey.surveyDescription, EditorStyles.wordWrappedLabel);
                    }
                    if (trial.survey.questionCount > 0)
                    {
                        EditorGUILayout.LabelField("Number of Questions", trial.survey.questionCount.ToString());
                    }

                    EditorGUI.indentLevel--;
                }

                // Display instance data if available
                if (trial.surveyInstanceData != null)
                {
                    EditorGUILayout.LabelField("Activated", trial.surveyInstanceData.activated ? "Yes" : "No");
                }
            }

            if (trial.repetitionCount > 1)
            {
                EditorGUILayout.LabelField("Repetitions", trial.repetitionCount.ToString());
            }

            if (!string.IsNullOrEmpty(trial.trialOrdering))
            {
                EditorGUILayout.LabelField("Ordering", trial.trialOrdering);
            }

            if (!string.IsNullOrEmpty(trial.randomizationType))
            {
                EditorGUILayout.LabelField("Randomization Type", trial.randomizationType);
            }

            if (!string.IsNullOrEmpty(trial.randomizationLevel))
            {
                EditorGUILayout.LabelField("Randomization Level", trial.randomizationLevel);
            }

            // Display conditions if present
            if (trial.conditions != null && trial.conditions.Count > 0)
            {
                EditorGUILayout.LabelField("Conditions:", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                foreach (var kvp in trial.conditions)
                {
                    EditorGUILayout.LabelField($"  {kvp.Key}", kvp.Value);
                }
                EditorGUI.indentLevel--;
            }

            // Display within-subjects IVs if present
            if (trial.withinSubjectsIVs != null && trial.withinSubjectsIVs.Count > 0)
            {
                EditorGUILayout.LabelField("Within-Subjects IVs", string.Join(", ", trial.withinSubjectsIVs));
            }

            // Display between-subjects IVs if present
            if (trial.betweenSubjectsIVs != null && trial.betweenSubjectsIVs.Count > 0)
            {
                EditorGUILayout.LabelField("Between-Subjects IVs", string.Join(", ", trial.betweenSubjectsIVs));
            }

            // Display attached survey if present
            if (!string.IsNullOrEmpty(trial.attachedSurveyName))
            {
                string surveyInfo = trial.attachedSurveyName;
                if (!string.IsNullOrEmpty(trial.surveyPosition))
                {
                    surveyInfo += $" ({trial.surveyPosition} trial)";
                }
                EditorGUILayout.LabelField("Attached Survey", surveyInfo);
            }

            EditorGUI.indentLevel--;

            // Draw child trials recursively
            if (trial.childTrials != null && trial.childTrials.Count > 0)
            {
                GUILayout.Space(5);
                GUILayout.Label("Child Trials:", EditorStyles.miniBoldLabel);
                for (int i = 0; i < trial.childTrials.Count; i++)
                {
                    DrawTrialItem(i + 1, trial.childTrials[i], indentLevel + 1);
                }
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        private string GetTypeLabel(string type)
        {
            switch (type)
            {
                case "standalone": return "Standalone";
                case "within": return "Within-Subjects";
                case "between": return "Between-Subjects";
                case "consent": return "Consent";
                case "entry": return "Entry";
                case "completion": return "Completion";
                case "survey": return "Survey";
                default: return type;
            }
        }

        private Color GetTrialTypeColor(string type)
        {
            switch (type)
            {
                case "consent": return new Color(0.7f, 0.9f, 0.7f); // Light green
                case "entry": return new Color(0.7f, 0.8f, 0.9f); // Light blue
                case "completion": return new Color(0.9f, 0.8f, 0.7f); // Light orange
                case "within": return new Color(0.9f, 0.9f, 0.7f); // Light yellow
                case "between": return new Color(0.9f, 0.7f, 0.9f); // Light purple
                case "standalone": return new Color(0.85f, 0.85f, 0.85f); // Light gray
                case "survey": return new Color(0.8f, 0.95f, 0.95f); // Light cyan
                default: return Color.white;
            }
        }

        private void FetchTrialWorkflow()
        {
            string experimentId = PlayerPrefs.GetString("VERA_ActiveExperiment", "");

            if (string.IsNullOrEmpty(experimentId))
            {
                errorMessage = "No experiment selected. Please select an experiment in VERA -> Settings.";
                return;
            }

            isLoading = true;
            errorMessage = null;
            trialWorkflow.Clear();

            string url = $"{VERAHost.hostUrl}/api/experiments/{experimentId}/trials/execution-order";

            UnityWebRequest request = UnityWebRequest.Get(url);

            // The execution-order API is now public, but we still send auth token if available
            string buildAuthToken = PlayerPrefs.GetString("VERA_BuildAuthToken", "");
            if (!string.IsNullOrEmpty(buildAuthToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + buildAuthToken);
            }

            var operation = request.SendWebRequest();

            EditorApplication.update += CheckRequest;

            void CheckRequest()
            {
                if (!operation.isDone) return;

                EditorApplication.update -= CheckRequest;
                isLoading = false;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    errorMessage = $"Failed to fetch trial workflow: {request.error}";
                    if (request.responseCode == 404)
                    {
                        errorMessage = "Trial workflow not found. Make sure your experiment has a trial workflow configured in the VERA portal.";
                    }
                    else if (request.responseCode == 401 || request.responseCode == 403)
                    {
                        errorMessage = "Authentication failed. Please re-authenticate in VERA -> Settings.";

                        // Clear authentication flags to force re-auth
                        PlayerPrefs.SetInt("VERA_UserAuthenticated", 0);
                        PlayerPrefs.Save();

                        Debug.LogWarning("[VERA Trial Workflow] Authentication token expired or invalid. Please re-authenticate in VERA -> Settings.");
                    }
                }
                else
                {
                    try
                    {
                        string json = request.downloadHandler.text;

                        // Log the response for debugging
                        Debug.Log($"[VERA Trial Workflow] Received response: {json.Substring(0, Mathf.Min(500, json.Length))}...");

                        JToken parsed = JToken.Parse(json);

                        // Handle both array response and object response with trials property
                        JArray trialsArray;
                        if (parsed is JArray)
                        {
                            trialsArray = (JArray)parsed;
                        }
                        else if (parsed is JObject obj)
                        {
                            if (obj["trials"] is JArray)
                            {
                                trialsArray = (JArray)obj["trials"];
                            }
                            else if (obj["executionOrder"] is JArray)
                            {
                                // Try executionOrder property as well
                                trialsArray = (JArray)obj["executionOrder"];
                            }
                            else
                            {
                                // Show what we actually received
                                string preview = json.Length > 200 ? json.Substring(0, 200) + "..." : json;
                                errorMessage = $"Received unexpected response format from server. Response: {preview}";
                                Debug.LogError($"[VERA Trial Workflow] Unexpected response format. Full response: {json}");
                                request.Dispose();
                                Repaint();
                                return;
                            }
                        }
                        else
                        {
                            // Show what we actually received
                            string preview = json.Length > 200 ? json.Substring(0, 200) + "..." : json;
                            errorMessage = $"Received unexpected response format from server. Response: {preview}";
                            Debug.LogError($"[VERA Trial Workflow] Unexpected response format. Full response: {json}");
                            request.Dispose();
                            Repaint();
                            return;
                        }

                        trialWorkflow = ParseTrials(trialsArray);
                        Debug.Log($"[VERA Trial Workflow] Successfully parsed {trialWorkflow.Count} top-level items from execution order");

                        // Count total items including child trials
                        int totalItems = CountAllItems(trialWorkflow);
                        Debug.Log($"[VERA Trial Workflow] Total items including children: {totalItems}");
                    }
                    catch (Exception e)
                    {
                        errorMessage = $"Failed to parse response: {e.Message}";
                        Debug.LogError($"[VERA Trial Workflow] Parse error: {e.Message}\n{e.StackTrace}");
                    }
                }

                request.Dispose();
                Repaint();
            }
        }

        private int CountAllItems(List<TrialWorkflowItem> items)
        {
            int count = items.Count;
            foreach (var item in items)
            {
                if (item.childTrials != null && item.childTrials.Count > 0)
                {
                    count += CountAllItems(item.childTrials);
                }
            }
            return count;
        }

        private List<TrialWorkflowItem> ParseTrials(JArray trialsArray)
        {
            List<TrialWorkflowItem> trials = new List<TrialWorkflowItem>();

            foreach (JToken trialToken in trialsArray)
            {
                TrialWorkflowItem trial = new TrialWorkflowItem
                {
                    id = trialToken.Value<string>("id"),
                    type = trialToken.Value<string>("type"),
                    label = trialToken.Value<string>("label"),
                    description = trialToken.Value<string>("description"),
                    order = trialToken.Value<int>("order"),
                    repetitionCount = trialToken.Value<int>("repetitionCount"),
                    randomizationType = trialToken.Value<string>("randomizationType"),
                    trialOrdering = trialToken.Value<string>("trialOrdering"),
                    randomizationLevel = trialToken.Value<string>("randomizationLevel"),
                    isRandomized = trialToken.Value<bool>("isRandomized"),
                    conditionsRandomized = trialToken.Value<bool>("conditionsRandomized"),
                    randomizedOrder = trialToken.Value<int>("randomizedOrder"),
                    completeAllRepetitions = trialToken.Value<bool>("completeAllRepetitions"),
                    // For standalone surveys
                    surveyId = trialToken.Value<string>("surveyId"),
                    surveyName = trialToken.Value<string>("surveyName"),
                    instanceId = trialToken.Value<string>("instanceId")
                };

                // Parse attachedSurvey object (new format)
                JObject attachedSurveyObj = trialToken["attachedSurvey"] as JObject;
                if (attachedSurveyObj != null)
                {
                    trial.attachedSurveyId = attachedSurveyObj.Value<string>("instanceId");
                    trial.attachedSurveyName = attachedSurveyObj.Value<string>("surveyName");
                    trial.surveyPosition = attachedSurveyObj.Value<string>("position");
                }
                // Fallback to old format for backward compatibility
                else
                {
                    trial.attachedSurveyId = trialToken.Value<string>("attachedSurveyId");
                    trial.attachedSurveyName = trialToken.Value<string>("attachedSurveyName");
                    trial.surveyPosition = trialToken.Value<string>("surveyPosition");
                }

                // Parse conditions dictionary
                JObject conditionsObj = trialToken["conditions"] as JObject;
                if (conditionsObj != null)
                {
                    trial.conditions = new Dictionary<string, string>();
                    foreach (var prop in conditionsObj.Properties())
                    {
                        trial.conditions[prop.Name] = prop.Value?.ToString() ?? "";
                    }
                }

                // Parse within-subjects IVs
                JArray withinIVs = trialToken["withinSubjectsIVs"] as JArray;
                if (withinIVs != null)
                {
                    trial.withinSubjectsIVs = new List<string>();
                    foreach (var iv in withinIVs)
                    {
                        trial.withinSubjectsIVs.Add(iv.ToString());
                    }
                }

                // Parse between-subjects IVs
                JArray betweenIVs = trialToken["betweenSubjectsIVs"] as JArray;
                if (betweenIVs != null)
                {
                    trial.betweenSubjectsIVs = new List<string>();
                    foreach (var iv in betweenIVs)
                    {
                        trial.betweenSubjectsIVs.Add(iv.ToString());
                    }
                }

                // Parse per-trial repetitions dictionary
                JObject perTrialRepsObj = trialToken["perTrialRepetitions"] as JObject;
                if (perTrialRepsObj != null)
                {
                    trial.perTrialRepetitions = new Dictionary<string, int>();
                    foreach (var prop in perTrialRepsObj.Properties())
                    {
                        trial.perTrialRepetitions[prop.Name] = prop.Value?.Value<int>() ?? 0;
                    }
                }

                // Parse child trials recursively
                JArray childTrialsArray = trialToken["childTrials"] as JArray;
                if (childTrialsArray != null && childTrialsArray.Count > 0)
                {
                    trial.childTrials = ParseTrials(childTrialsArray);
                }

                // Parse survey data for standalone surveys
                JObject surveyObj = trialToken["survey"] as JObject;
                if (surveyObj != null)
                {
                    trial.survey = new SurveyData
                    {
                        surveyName = surveyObj.Value<string>("surveyName"),
                        surveyDescription = surveyObj.Value<string>("surveyDescription"),
                        questionCount = (surveyObj["questions"] as JArray)?.Count ?? 0
                    };
                }

                // Parse survey instance data
                JObject instanceObj = trialToken["surveyInstanceData"] as JObject;
                if (instanceObj != null)
                {
                    trial.surveyInstanceData = new SurveyInstanceData
                    {
                        instanceId = instanceObj.Value<string>("instanceId"),
                        activated = instanceObj.Value<bool>("activated")
                    };
                }

                trials.Add(trial);

                // Log what we parsed
                string childInfo = trial.childTrials != null && trial.childTrials.Count > 0
                    ? $" with {trial.childTrials.Count} child trials"
                    : "";
                string surveyInfo = trial.survey != null
                    ? $" ({trial.survey.questionCount} questions)"
                    : "";
                Debug.Log($"[VERA Trial Workflow] Parsed {trial.type} item: {trial.label} (ID: {trial.id}){childInfo}{surveyInfo}");
            }

            return trials;
        }

        // Survey-related data classes
        [System.Serializable]
        private class SurveyQuestion
        {
            public string questionNumberInSurvey;
            public string questionText;
            public string questionType;
        }

        [System.Serializable]
        private class SurveyData
        {
            public string surveyName;
            public string surveyDescription;
            public int questionCount;
        }

        [System.Serializable]
        private class SurveyInstanceData
        {
            public string instanceId;
            public bool activated;
        }

        // Data class for trial workflow items
        private class TrialWorkflowItem
        {
            public string id;
            public string type;
            public string label;
            public string description;
            public int order;
            public int repetitionCount;
            public Dictionary<string, string> conditions;
            public List<TrialWorkflowItem> childTrials;
            public List<string> withinSubjectsIVs;
            public List<string> betweenSubjectsIVs;
            public string randomizationType;
            public string trialOrdering;
            public string randomizationLevel;
            public bool isRandomized;
            public bool conditionsRandomized;
            public int randomizedOrder;
            public bool completeAllRepetitions;
            public Dictionary<string, int> perTrialRepetitions;
            // For surveys attached to trials
            public string attachedSurveyId;
            public string attachedSurveyName;
            public string surveyPosition;
            // For standalone surveys
            public string surveyId;
            public string surveyName;
            public string instanceId;
            public SurveyData survey;
            public SurveyInstanceData surveyInstanceData;
        }
    }
}
#endif
