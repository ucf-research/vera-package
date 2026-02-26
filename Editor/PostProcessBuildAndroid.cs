#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Build;

namespace VERA
{
    internal class PostProcessBuildAndroid : IPostprocessBuildWithReport
    {
        public int callbackOrder => 999;

        // OnPostprocessBuild, add the internet permissions manually to the manifest file
        public void OnPostprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
        {
            // Ensure we are on Android build
            if (report.summary.platform != UnityEditor.BuildTarget.Android)
                return;

            // Get the manifest path
            var manifestPath = GetPathToGeneratedManifest(report.summary.outputPath);

            // If a file exists, add the internet permissions to it
            if (File.Exists(manifestPath))
            {
                var text = File.ReadAllText(manifestPath);

                // Check that the permissions are missing
                if (!text.Contains("android.permission.INTERNET"))
                {
                    // Insert the permission in the <manifest> element
                    var pattern = "(<manifest.*?>)";
                    var replacement = "$1\n    <uses-permission android:name=\"android.permission.INTERNET\"/>";
                    text = Regex.Replace(text, pattern, replacement);

                    File.WriteAllText(manifestPath, text);
                }
            }
        }

        // Gets the path to the generated manifest file
        private string GetPathToGeneratedManifest(string buildOutputPath)
        {
            var path = Path.Combine(Path.GetDirectoryName(buildOutputPath), "launcher", "src", "main", "AndroidManifest.xml");
            return path;
        }
    }
}
#endif