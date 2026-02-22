#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VERA
{
    /// <summary>
    /// Editor utility for running mock participant tests via the VERA menu.
    /// </summary>
    internal static class VERAMockParticipantTestRunner
    {
        private const string PREFS_KEY = "VERA_RunMockTestOnPlay";

        [MenuItem("VERA/Run Mock Participant Test")]
        public static void RunMockParticipantTest()
        {
            // Check if we're already in Play mode
            if (EditorApplication.isPlaying)
            {
                VERADebugger.LogWarning("Already in Play mode. Stop Play mode first, then run the test again.", "VERA Mock Participant Test Runner");
                return;
            }

            // Check if experiment is selected
            string activeExperiment = PlayerPrefs.GetString("VERA_ActiveExperiment", "");
            if (string.IsNullOrEmpty(activeExperiment))
            {
                EditorUtility.DisplayDialog(
                    "No Experiment Selected",
                    "Please select an experiment in VERA → Settings before running the mock participant test.",
                    "OK"
                );
                return;
            }

            // Find or create the test runner GameObject
            GameObject testerObject = FindOrCreateTestRunner();

            // Mark the scene as dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene()
            );

            // Set a flag to indicate we want to run the test
            EditorPrefs.SetBool(PREFS_KEY, true);

            // Enter Play mode
            VERADebugger.Log("Starting mock participant test in Play mode...", "VERA Mock Participant Test Runner");
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // Subscribe to Play mode state changes
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // When entering Play mode, check if we should run the test
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (EditorPrefs.GetBool(PREFS_KEY, false))
                {
                    EditorPrefs.DeleteKey(PREFS_KEY);
                    VERADebugger.Log("Mock participant test is now running. Check Console for progress.", "VERA Mock Participant Test Runner");
                }
            }
        }

        private static GameObject FindOrCreateTestRunner()
        {
            // Look for existing test runner in the scene
            var existingRunner = UnityEngine.Object.FindAnyObjectByType<VERAMockTestRunner>();
            if (existingRunner != null)
            {
                VERADebugger.Log($"Found existing test runner on '{existingRunner.gameObject.name}'.", "VERA Mock Participant Test Runner");
                return existingRunner.gameObject;
            }

            // Create a new GameObject with the test runner component
            GameObject newObject = new GameObject("VERA Mock Participant Test Runner");
            newObject.AddComponent<VERAMockTestRunner>();

            VERADebugger.Log("Created new test runner GameObject in the scene.", "VERA Mock Participant Test Runner");
            return newObject;
        }
    }
}
#endif
