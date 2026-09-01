#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VERA
{
    [CustomEditor(typeof(VERATelemetryReplay))]
    internal class VERATelemetryReplayEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "isPlaying", "currentTime", "duration", "participantId", "conditions");
            serializedObject.ApplyModifiedProperties();

            var replay = (VERATelemetryReplay)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Is Playing", replay.IsPlaying);
                EditorGUILayout.FloatField("Current Time", replay.CurrentTime);
                EditorGUILayout.FloatField("Duration", replay.Duration);
                EditorGUILayout.IntField("Participant Id", replay.ParticipantId);
                EditorGUILayout.TextField("Conditions", replay.Conditions ?? string.Empty);
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Import the Experiment_Telemetry CSV into the project, assign it above, then enter Play Mode to replay.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(replay.IsPlaying ? "Pause" : "Play"))
                {
                    if (replay.IsPlaying)
                        replay.Pause();
                    else
                        replay.Play();
                }

                if (GUILayout.Button("Restart"))
                {
                    replay.Seek(0f);
                    replay.Play();
                }
            }

            if (replay.Duration > 0f)
            {
                EditorGUI.BeginChangeCheck();
                float newTime = EditorGUILayout.Slider("Scrub", replay.CurrentTime, 0f, replay.Duration);
                if (EditorGUI.EndChangeCheck())
                    replay.Seek(newTime);
            }
        }
    }
}
#endif
