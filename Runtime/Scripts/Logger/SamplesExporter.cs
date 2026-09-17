
#if UNITY_EDITOR && VERA_DEV_MODE
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VERA
{
    internal static class SamplesExporter
    {

        // SamplesExporter is a utility script used to export all working sample files (_SamplesWorking folder) 
        // into the hidden Samples~ folder used by the VERA package.

        const string packageFolder = "VERA";
        const string samplesRoot = "Samples~";
        const string assetsRoot = "Assets/_SamplesWorking";

        static readonly string[] SampleFolders =
        {
            "DemoScene",
            "VERASandboxDemo",
            "VERATelemetryReplay",
        };

        [MenuItem("VERA/Export Samples (DEV)")]
        public static void Export()
        {
            string dstRoot = Path.Combine("Packages", packageFolder, samplesRoot).Replace("\\", "/");
            string srcRoot = assetsRoot.Replace("\\", "/");

            foreach (string sampleFolder in SampleFolders)
            {
                string src = Path.Combine(srcRoot, sampleFolder).Replace("\\", "/");
                string dst = Path.Combine(dstRoot, sampleFolder).Replace("\\", "/");

                if (!Directory.Exists(src))
                {
                    VERADebugger.LogWarning($"Sample folder not found, skipping: {src}", "SamplesExporter");
                    continue;
                }

                if (Directory.Exists(dst))
                {
                    Directory.Delete(dst, true);
                }

                if (AssetDatabase.IsValidFolder(dst))
                {
                    FileUtil.DeleteFileOrDirectory(dst);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                FileUtil.CopyFileOrDirectory(src, dst);
            }

            AssetDatabase.Refresh();
            VERADebugger.Log($"Successfully exported samples from '{srcRoot}' to '{dstRoot}'.", "SamplesExporter", DebugPreference.None);
        }
    }
}
#endif
