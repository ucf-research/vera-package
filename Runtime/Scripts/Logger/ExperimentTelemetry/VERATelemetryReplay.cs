using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace VERA
{
    /// <summary>
    /// Replays an Experiment_Telemetry CSV in real time onto a headset and controllers.
    /// Drag a telemetry <see cref="TextAsset"/> onto this component and assign the replay rig transforms.
    /// </summary>
    [AddComponentMenu("VERA/VERA Telemetry Replay")]
    public class VERATelemetryReplay : MonoBehaviour
    {
        private const string CallerName = "VERA Telemetry Replay";

        [Header("Telemetry")]
        [Tooltip("Experiment_Telemetry CSV. Import the file into the project, then drag it here.")]
        [SerializeField] private TextAsset telemetryFile;

        [Header("Replay Rig")]
        [Tooltip("Object that represents the participant headset.")]
        [SerializeField] private Transform headset;

        [Tooltip("Object that represents the participant left controller.")]
        [SerializeField] private Transform leftController;

        [Tooltip("Object that represents the participant right controller.")]
        [SerializeField] private Transform rightController;

        [Header("Playback")]
        [Tooltip("Begin playback automatically on Start.")]
        [SerializeField] private bool playOnStart = true;

        [Tooltip("Restart from the beginning when the recording ends.")]
        [SerializeField] private bool loop;

        [Tooltip("Playback speed. 1 is real-time.")]
        [SerializeField] private float playbackSpeed = 1f;

        [Tooltip("Skip fully untracked samples at the start and end of the file.")]
        [SerializeField] private bool trimUntrackedEnds = true;

        [Tooltip("Disable headset/controller objects while that device was not tracking.")]
        [SerializeField] private bool hideWhenUndetected = true;

        [Header("Status")]
        [SerializeField] private bool isPlaying;
        [SerializeField] private float currentTime;
        [SerializeField] private float duration;
        [SerializeField] private int participantId;
        [SerializeField] private string conditions;

        private readonly List<Sample> samples = new List<Sample>();
        private float playbackTime;
        private bool loaded;

        public bool IsPlaying => isPlaying;
        public float CurrentTime => currentTime;
        public float Duration => duration;
        public int ParticipantId => participantId;
        public string Conditions => conditions;
        public TextAsset TelemetryFile => telemetryFile;

        private void Start()
        {
            loaded = TryLoadTelemetry();
            if (loaded && playOnStart)
                Play();
        }

        private void LateUpdate()
        {
            if (!isPlaying || !loaded || samples.Count == 0)
                return;

            playbackTime += Time.deltaTime * Mathf.Max(0f, playbackSpeed);
            if (playbackTime >= duration)
            {
                if (loop && duration > 0f)
                {
                    playbackTime %= duration;
                }
                else
                {
                    playbackTime = duration;
                    isPlaying = false;
                }
            }

            ApplyAtTime(playbackTime);
        }

        /// <summary>Start or resume playback from the current time.</summary>
        public void Play()
        {
            if (!loaded)
                loaded = TryLoadTelemetry();
            if (!loaded || samples.Count == 0)
                return;

            isPlaying = true;
            ApplyAtTime(playbackTime);
        }

        /// <summary>Pause playback, leaving the rig at its current pose.</summary>
        public void Pause()
        {
            isPlaying = false;
        }

        /// <summary>Pause and return to the start of the recording.</summary>
        public void Stop()
        {
            isPlaying = false;
            Seek(0f);
        }

        /// <summary>Jump to a time in seconds from the start of playback.</summary>
        public void Seek(float time)
        {
            if (!loaded)
                loaded = TryLoadTelemetry();
            if (!loaded || samples.Count == 0)
                return;

            playbackTime = Mathf.Clamp(time, 0f, duration);
            ApplyAtTime(playbackTime);
        }

        [ContextMenu("Play")]
        private void ContextPlay() => Play();

        [ContextMenu("Pause")]
        private void ContextPause() => Pause();

        [ContextMenu("Restart")]
        private void ContextRestart()
        {
            playbackTime = 0f;
            Play();
        }

        private bool TryLoadTelemetry()
        {
            samples.Clear();
            loaded = false;
            duration = 0f;
            playbackTime = 0f;
            currentTime = 0f;
            participantId = 0;
            conditions = string.Empty;

            if (telemetryFile == null)
            {
                VERADebugger.LogError("No telemetry CSV assigned. Drag an Experiment_Telemetry file onto this component.", CallerName);
                return false;
            }

            string text = telemetryFile.text;
            if (string.IsNullOrWhiteSpace(text))
            {
                VERADebugger.LogError("Telemetry CSV is empty.", CallerName);
                return false;
            }

            string[] lines = text.Split(new[] { '\n' }, StringSplitOptions.None);
            if (lines.Length < 2)
            {
                VERADebugger.LogError("Telemetry CSV has no data rows.", CallerName);
                return false;
            }

            List<string> header = ParseCsvLine(lines[0]);
            ColumnMap columns = ColumnMap.FromHeader(header);
            if (!columns.IsValid)
            {
                VERADebugger.LogError("Telemetry CSV is missing required columns (ts and device pose fields).", CallerName);
                return false;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                List<string> fields = ParseCsvLine(line);
                if (fields.Count < 3)
                    continue;

                if (!TryParseSample(fields, columns, out Sample sample))
                    continue;

                samples.Add(sample);
            }

            if (samples.Count == 0)
            {
                VERADebugger.LogError("No valid telemetry samples could be parsed.", CallerName);
                return false;
            }

            samples.Sort((a, b) => a.ts.CompareTo(b.ts));

            if (trimUntrackedEnds)
                TrimUntrackedEnds();

            if (samples.Count == 0)
            {
                VERADebugger.LogError("Telemetry file contains no tracked samples.", CallerName);
                return false;
            }

            float startTs = samples[0].ts;
            for (int i = 0; i < samples.Count; i++)
            {
                Sample sample = samples[i];
                sample.ts -= startTs;
                samples[i] = sample;
            }

            duration = samples[samples.Count - 1].ts;
            participantId = samples[0].participantId;
            conditions = samples[0].conditions ?? string.Empty;

            VERADebugger.Log(
                $"Loaded {samples.Count} samples ({duration:0.00}s) from '{telemetryFile.name}'.",
                CallerName);

            return true;
        }

        private void TrimUntrackedEnds()
        {
            int first = 0;
            while (first < samples.Count && !HasAnyDevice(samples[first]))
                first++;

            int last = samples.Count - 1;
            while (last >= first && !HasAnyDevice(samples[last]))
                last--;

            if (first > last)
            {
                samples.Clear();
                return;
            }

            if (first == 0 && last == samples.Count - 1)
                return;

            samples.RemoveRange(last + 1, samples.Count - last - 1);
            if (first > 0)
                samples.RemoveRange(0, first);
        }

        private void ApplyAtTime(float time)
        {
            currentTime = time;
            Evaluate(time, out Sample pose, out string sampleConditions, out int sampleParticipantId);
            conditions = sampleConditions;
            participantId = sampleParticipantId;

            ApplyDevice(headset, pose.headset);
            ApplyDevice(leftController, pose.left);
            ApplyDevice(rightController, pose.right);
        }

        private void ApplyDevice(Transform target, DevicePose pose)
        {
            if (target == null)
                return;

            if (hideWhenUndetected)
                target.gameObject.SetActive(pose.detected);

            if (!pose.detected)
                return;

            target.SetPositionAndRotation(pose.virtualPos, pose.virtualRot);
        }

        private void Evaluate(float time, out Sample pose, out string sampleConditions, out int sampleParticipantId)
        {
            if (samples.Count == 1 || time <= samples[0].ts)
            {
                pose = samples[0];
                sampleConditions = pose.conditions;
                sampleParticipantId = pose.participantId;
                return;
            }

            Sample last = samples[samples.Count - 1];
            if (time >= last.ts)
            {
                pose = last;
                sampleConditions = pose.conditions;
                sampleParticipantId = pose.participantId;
                return;
            }

            int nextIndex = FindFirstIndexAfter(time);
            Sample a = samples[nextIndex - 1];
            Sample b = samples[nextIndex];
            float span = b.ts - a.ts;
            float t = span > 0.0001f ? Mathf.Clamp01((time - a.ts) / span) : 0f;

            pose = new Sample
            {
                ts = time,
                participantId = t < 1f ? a.participantId : b.participantId,
                conditions = t < 1f ? a.conditions : b.conditions,
                headset = InterpolateDevice(a.headset, b.headset, t),
                left = InterpolateDevice(a.left, b.left, t),
                right = InterpolateDevice(a.right, b.right, t)
            };
            sampleConditions = pose.conditions;
            sampleParticipantId = pose.participantId;
        }

        private int FindFirstIndexAfter(float time)
        {
            int low = 1;
            int high = samples.Count - 1;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (samples[mid].ts <= time)
                    low = mid + 1;
                else
                    high = mid;
            }
            return low;
        }

        private static DevicePose InterpolateDevice(DevicePose a, DevicePose b, float t)
        {
            if (a.detected && b.detected)
            {
                return new DevicePose
                {
                    detected = true,
                    virtualPos = Vector3.Lerp(a.virtualPos, b.virtualPos, t),
                    virtualRot = Slerp(a.virtualRot, b.virtualRot, t)
                };
            }

            if (a.detected && t < 1f)
                return a;
            if (b.detected)
                return b;
            return default;
        }

        private static Quaternion Slerp(Quaternion a, Quaternion b, float t)
        {
            if (Quaternion.Dot(a, b) < 0f)
                b = new Quaternion(-b.x, -b.y, -b.z, -b.w);
            return Quaternion.Slerp(a, b, t);
        }

        private static bool HasAnyDevice(Sample sample)
        {
            return sample.headset.detected || sample.left.detected || sample.right.detected;
        }

        private static bool TryParseSample(List<string> fields, ColumnMap columns, out Sample sample)
        {
            sample = default;
            if (!TryParseFloat(GetField(fields, columns.ts), out float ts))
                return false;

            sample = new Sample
            {
                ts = ts,
                participantId = TryParseInt(GetField(fields, columns.pID), out int pId) ? pId : 0,
                conditions = GetField(fields, columns.conditions),
                headset = ParseDevice(
                    fields,
                    columns.headsetDetected,
                    columns.headsetVirtualPosX, columns.headsetVirtualRotQuatX, columns.headsetVirtualRotEulerX),
                left = ParseDevice(
                    fields,
                    columns.leftDetected,
                    columns.leftVirtualPosX, columns.leftVirtualRotQuatX, columns.leftVirtualRotEulerX),
                right = ParseDevice(
                    fields,
                    columns.rightDetected,
                    columns.rightVirtualPosX, columns.rightVirtualRotQuatX, columns.rightVirtualRotEulerX)
            };
            return true;
        }

        private static DevicePose ParseDevice(
            List<string> fields,
            int detectedIndex,
            int virtualPosX, int virtualQuatX, int virtualEulerX)
        {
            bool detected = ParseBool(GetField(fields, detectedIndex));
            return new DevicePose
            {
                detected = detected,
                virtualPos = ReadVector3(fields, virtualPosX),
                virtualRot = ReadRotation(fields, virtualQuatX, virtualEulerX)
            };
        }

        private static Vector3 ReadVector3(List<string> fields, int xIndex)
        {
            if (xIndex < 0)
                return Vector3.zero;

            float x = ParseFloatOrDefault(GetField(fields, xIndex));
            float y = ParseFloatOrDefault(GetField(fields, xIndex + 1));
            float z = ParseFloatOrDefault(GetField(fields, xIndex + 2));
            return new Vector3(x, y, z);
        }

        private static Quaternion ReadRotation(List<string> fields, int quatXIndex, int eulerXIndex)
        {
            if (quatXIndex >= 0)
            {
                float x = ParseFloatOrDefault(GetField(fields, quatXIndex));
                float y = ParseFloatOrDefault(GetField(fields, quatXIndex + 1));
                float z = ParseFloatOrDefault(GetField(fields, quatXIndex + 2));
                float w = ParseFloatOrDefault(GetField(fields, quatXIndex + 3));
                if ((x * x + y * y + z * z + w * w) > 0.0001f)
                    return new Quaternion(x, y, z, w).normalized;
            }

            if (eulerXIndex >= 0)
            {
                float x = ParseFloatOrDefault(GetField(fields, eulerXIndex));
                float y = ParseFloatOrDefault(GetField(fields, eulerXIndex + 1));
                float z = ParseFloatOrDefault(GetField(fields, eulerXIndex + 2));
                return Quaternion.Euler(x, y, z);
            }

            return Quaternion.identity;
        }

        private static string GetField(List<string> fields, int index)
        {
            if (index < 0 || index >= fields.Count)
                return string.Empty;
            return fields[index];
        }

        private static bool TryParseFloat(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static float ParseFloatOrDefault(string value)
        {
            return TryParseFloat(value, out float result) ? result : 0f;
        }

        private static bool TryParseInt(string value, out int result)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private static bool ParseBool(string value)
        {
            return bool.TryParse(value, out bool result) && result;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            if (line == null)
                return fields;

            line = line.TrimEnd('\r');
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Length = 0;
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString());
            return fields;
        }

        private struct Sample
        {
            public float ts;
            public int participantId;
            public string conditions;
            public DevicePose headset;
            public DevicePose left;
            public DevicePose right;
        }

        private struct DevicePose
        {
            public bool detected;
            public Vector3 virtualPos;
            public Quaternion virtualRot;
        }

        private struct ColumnMap
        {
            public int pID;
            public int conditions;
            public int ts;
            public int headsetDetected;
            public int headsetVirtualPosX;
            public int headsetVirtualRotEulerX;
            public int headsetVirtualRotQuatX;
            public int leftDetected;
            public int leftVirtualPosX;
            public int leftVirtualRotEulerX;
            public int leftVirtualRotQuatX;
            public int rightDetected;
            public int rightVirtualPosX;
            public int rightVirtualRotEulerX;
            public int rightVirtualRotQuatX;

            public bool IsValid => ts >= 0 && headsetVirtualPosX >= 0;

            public static ColumnMap FromHeader(List<string> header)
            {
                return new ColumnMap
                {
                    pID = IndexOf(header, "pID"),
                    conditions = IndexOf(header, "conditions"),
                    ts = IndexOf(header, "ts"),
                    headsetDetected = IndexOf(header, "headsetDetected"),
                    headsetVirtualPosX = IndexOf(header, "headsetVirtualPosX"),
                    headsetVirtualRotEulerX = IndexOf(header, "headsetVirtualRotEulerX"),
                    headsetVirtualRotQuatX = IndexOf(header, "headsetVirtualRotQuatX"),
                    leftDetected = IndexOf(header, "leftDetected"),
                    leftVirtualPosX = IndexOf(header, "leftControllerVirtualPosX"),
                    leftVirtualRotEulerX = IndexOf(header, "leftControllerVirtualRotEulerX"),
                    leftVirtualRotQuatX = IndexOf(header, "leftControllerVirtualRotQuatX"),
                    rightDetected = IndexOf(header, "rightDetected"),
                    rightVirtualPosX = IndexOf(header, "rightControllerVirtualPosX"),
                    rightVirtualRotEulerX = IndexOf(header, "rightControllerVirtualRotEulerX"),
                    rightVirtualRotQuatX = IndexOf(header, "rightControllerVirtualRotQuatX")
                };
            }

            private static int IndexOf(List<string> header, string name)
            {
                for (int i = 0; i < header.Count; i++)
                {
                    if (string.Equals(header[i].Trim(), name, StringComparison.Ordinal))
                        return i;
                }
                return -1;
            }
        }
    }
}
