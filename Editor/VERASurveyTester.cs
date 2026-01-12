using UnityEngine;
using UnityEditor;
using System.Linq;

namespace VERA
{
    /// <summary>
    /// Unity Editor window for testing VERA surveys.
    /// Provides a quick way to test survey displays without setting up a full experiment.
    /// Access via VERA menu > Quick Start Survey
    /// </summary>
    public class VERASurveyTester : EditorWindow
    {
        [MenuItem("VERA/Quick Start Survey", false, 100)]
        public static void QuickStartSurvey()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Survey Tester",
                    "You must be in Play mode to test surveys.\n\nPress Play and try again.",
                    "OK");
                return;
            }

            // Auto-find or create SurveyInterface
            SurveyInterfaceIO surveyInterface = Object.FindObjectOfType<SurveyInterfaceIO>();
            if (surveyInterface == null)
            {
                // Try to find the SurveyInterface prefab
                string[] guids = AssetDatabase.FindAssets("t:Prefab SurveyInterface");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        GameObject instance = Object.Instantiate(prefab);
                        surveyInterface = instance.GetComponent<SurveyInterfaceIO>();
                        Debug.Log("[Survey Tester] Auto-instantiated SurveyInterface prefab.");
                    }
                }

                if (surveyInterface == null)
                {
                    EditorUtility.DisplayDialog("Survey Tester",
                        "Could not find SurveyInterface in the scene or prefabs.\n\nPlease add a SurveyInterface prefab to the scene.",
                        "OK");
                    return;
                }
            }

            // Prompt for survey ID with remembered default
            string defaultSurveyId = EditorPrefs.GetString("VERA_LastSurveyId", "");
            string surveyId = EditorInputDialog.Show("Enter Survey ID", "Survey ID:", defaultSurveyId);

            if (!string.IsNullOrEmpty(surveyId))
            {
                EditorPrefs.SetString("VERA_LastSurveyId", surveyId);
                Debug.Log($"[Survey Tester] Starting survey with ID: {surveyId}");
                surveyInterface.StartSurveyById(surveyId);
            }
        }
    }

    /// <summary>
    /// Simple input dialog for Unity Editor
    /// </summary>
    public class EditorInputDialog : EditorWindow
    {
        private string description = "Please enter a value:";
        private string inputText = "";
        private string labelText = "Value:";
        private System.Action<string> onComplete;

        public static string Show(string title, string label, string defaultValue = "")
        {
            string result = defaultValue;
            bool completed = false;

            EditorInputDialog window = ScriptableObject.CreateInstance<EditorInputDialog>();
            window.titleContent = new GUIContent(title);
            window.labelText = label;
            window.inputText = defaultValue;
            window.onComplete = (value) => { result = value; completed = true; };

            window.position = new Rect(Screen.width / 2, Screen.height / 2, 400, 100);
            window.ShowModalUtility();

            // Wait for the window to close
            while (!completed && window != null)
            {
                System.Threading.Thread.Sleep(100);
            }

            return result;
        }

        void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(labelText, EditorStyles.boldLabel);
            inputText = EditorGUILayout.TextField(inputText);
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("OK", GUILayout.Width(80)))
            {
                onComplete?.Invoke(inputText);
                Close();
            }

            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                onComplete?.Invoke("");
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
