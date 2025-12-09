
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
        const string samplesRelativePath = "Samples~/DemoScene";
        const string assetsPath = "Assets/_SamplesWorking/DemoScene";

        [MenuItem("VERA/Export Samples (DEV)")]
        public static void Export()
        {
            string dst = Path.Combine("Packages", packageFolder, samplesRelativePath).Replace("\\", "/");
            string src = assetsPath.Replace("\\", "/");

            // Ensure destination directory is completely removed
            if (Directory.Exists(dst))
            {
                Directory.Delete(dst, true);
            }
            
            // Also try FileUtil method as backup
            if (AssetDatabase.IsValidFolder(dst))
            {
                FileUtil.DeleteFileOrDirectory(dst);
            }

            // Ensure parent directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            
            // Copy the directory
            FileUtil.CopyFileOrDirectory(src, dst);
            AssetDatabase.Refresh();
            Debug.Log($"Successfully exported sample from '{src}' to '{dst}'.");
        }
    }
}
#endif
