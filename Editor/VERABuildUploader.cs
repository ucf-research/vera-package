#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Linq;
using System;
using System.Threading.Tasks;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.Build.Reporting;
using System.Net.Http;
using System.Net.Http.Headers;
using UnityEngine.XR.Management;

namespace VERA
{
    internal class VERABuildUploader : MonoBehaviour
    {


        #region BUILD AND UPLOAD


        // Builds the experiment for WebXR and uploads to the portal
        public static async void BuildAndUploadExperiment()
        {
            Debug.Log("Building and uploading experiment...");

            // Ensure WebGL build platform exists and is selected
            bool webGLSettingsSuccess = EnsureWebGLSettings();
            if (!webGLSettingsSuccess)
            {
                // Error handled in EnsureWebGLSettings
                return;
            }

            // Ensure the WebXR export and interactions packages are installed
            bool webXRExportPackageSuccess = await EnsurePackageInstalled(
                "https://github.com/De-Panther/unity-webxr-export.git?path=/Packages/webxr",
                "com.de-panther.webxr");
            bool webXRInteractionsPackageSuccess = await EnsurePackageInstalled(
                "https://github.com/De-Panther/unity-webxr-export.git?path=/Packages/webxr-interactions",
                "com.de-panther.webxr-interactions");

            // If failed to ensure packages, show error and return
            if (!webXRExportPackageSuccess || !webXRInteractionsPackageSuccess)
            {
                Debug.LogError("Build failed - could not ensure WebXR packages are installed. " +
                    "Please try manually installing the WebXR packages, then try again. " +
                    "Both the \"WebXR Export\" and \"WebXR Interactions\" packages are required to build the project for WebXR. " +
                    "You can find instructions here: https://openupm.com/packages/com.de-panther.webxr/");

                EditorUtility.DisplayDialog("Build Failed",
                    "Could not ensure WebXR packages are installed. Please check the console for details.",
                    "Okay");
                return;
            }

            // Ensure WebXR settings are properly configured
            EnsureWebXRSettings();

            // Build the project
            bool buildSuccess = await BuildProject();

            if (!buildSuccess)
            {
                Debug.LogError("Build / upload failed - could not build / upload the project for WebXR. " +
                    "Please check the console for details.");
                EditorUtility.DisplayDialog("Build Failed",
                    "Could not build or upload the project for WebXR. This is likely due to a general build error. " +
                    "Please check the console for details.",
                    "Okay");
                return;
            }

            EditorUtility.DisplayDialog("Build Successful",
                "Your experiment has been successfully built and uploaded to the VERA portal! " +
                "This experiment may now be experienced on a headset by visiting your experiment's WebXR link. " +
                "For more information, please visit the VERA documentation.",
                "Okay");

            Debug.Log("Experiment built and uploaded successfully!");
        }


        #endregion


        #region WEBGL SETTINGS


        // Ensures the WebGL build platform is installed and configured
        public static bool EnsureWebGLSettings()
        {
            // Check if WebGL is installed
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                bool openHub = EditorUtility.DisplayDialog(
                    "WebGL Build Support Missing",
                    $"The WebGL Build Support module is not installed for this Unity {Application.unityVersion} editor." +
                    "You can add it in Unity Hub > Installs > Add Modules." +
                    "\n\nPlease install the WebGL Build Support module to build for WebXR, " +
                    "or open Unity Hub to install it now. Once you have installed the module, try building again.",
                    "Open Unity Hub", "Cancel");

                if (openHub)
                {
                    // Opens Unity Hub directly on the Installs page (Hub ≥ 3.6).
                    // Older Hub versions will simply launch the Hub.
                    Application.OpenURL("unityhub://open/installs");
                }

                return false;
            }

            // Check if the active build target is already WebGL
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
            {
                return true;
            }

            Debug.Log("Switching build target to WebGL...");

            // If not, switch to WebGL build target
            bool success = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            if (success)
            {
                Debug.Log("Successfully switched build target to WebGL.");
                return true;
            }
            else
            {
                Debug.LogError("Failed to switch build target to WebGL. Check the console for details.");
                return false;
            }
        }


        #endregion


        #region PACKAGE INSTALLATION


        // Ensures a package is installed by git URL.
        // Returns true if successfully installed or already exists, false otherwise.
        public static async Task<bool> EnsurePackageInstalled(string gitUrl, string packageName)
        {
            // Create a package request to check if the package is already installed
            ListRequest list = Client.List(true);
            while (!list.IsCompleted)
                await Task.Yield();

            if (list.Status == StatusCode.Failure)
            {
                Debug.LogError($"Package-list failed: {list.Error.message}");
                return false;
            }

            // Check if the package is already installed
            if (list.Result.Any(p => p.name == packageName))
                return true;

            // Add the package if it doesn't exist yet
            Debug.Log($"Installing package {packageName} from {gitUrl}...");

            AddRequest add = Client.Add(gitUrl);
            while (!add.IsCompleted)
                await Task.Yield();

            if (add.Status == StatusCode.Success)
            {
                Debug.Log($"Successfully installed {packageName}");
                return true;
            }

            Debug.LogError($"Failed to install {packageName}: {add.Error.message}");
            return false;
        }


        #endregion


        #region WEBXR SETTINGS


        // Ensures WebXR settings are properly configured
        public static void EnsureWebXRSettings()
        {
            const BuildTargetGroup targetGroup = BuildTargetGroup.WebGL;        // Ensure the XR Management package is configured
            if (!EditorBuildSettings.TryGetConfigObject(
                XRGeneralSettings.k_SettingsKey,
                out XRGeneralSettingsPerBuildTarget perBT))
            {
                perBT = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();

                // Create the asset in the Assets folder first to make it persistent
                string assetPath = "Assets/XRGeneralSettingsPerBuildTarget.asset";
                AssetDatabase.CreateAsset(perBT, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Now add the persisted object to build settings
                EditorBuildSettings.AddConfigObject(
                    XRGeneralSettings.k_SettingsKey, perBT, true);

                // Clean up the temporary asset
                //AssetDatabase.DeleteAsset(assetPath);
            }

            // Ensure WebGL options exist
            if (!perBT.HasSettingsForBuildTarget(targetGroup))
            {
                perBT.CreateDefaultSettingsForBuildTarget(targetGroup);
            }

            XRGeneralSettings general = perBT.SettingsForBuildTarget(targetGroup);
            XRManagerSettings manager = perBT.ManagerSettingsForBuildTarget(targetGroup);

            // Create manager settings if they don't exist
            if (manager == null)
            {
                manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                general.AssignedSettings = manager;
                EditorUtility.SetDirty(general);
            }

            // Assign loader and disable compression
            bool added = XRPackageMetadataStore.AssignLoader(manager, "WebXR.WebXRLoader", targetGroup);
            general.InitManagerOnStart = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;

            // Persist changes
            if (added)
            {
                EditorUtility.SetDirty(perBT);
                EditorUtility.SetDirty(general);
                EditorUtility.SetDirty(manager);
                AssetDatabase.SaveAssets();
            }

            // Disable Anti-Aliasing for WebXR builds
            DisableAA();
        }


        // Disables Anti-Aliasing for WebXR builds
        public static void DisableAA()
        {
            Debug.Log("Disabling anti-aliasing for WebXR build...");

            // First, handle built-in render pipeline quality settings
            DisableBuiltinAA();

            // Then, handle Scriptable Render Pipeline assets
            DisableSRPAA();

            Debug.Log("Anti-aliasing disabled for all render pipelines.");
        }

        // Disables AA in built-in render pipeline quality settings
        private static void DisableBuiltinAA()
        {
            int originalLevel = QualitySettings.GetQualityLevel();
            string[] names = QualitySettings.names;

            // Disable AA for each quality level
            for (int i = 0; i < names.Length; ++i)
            {
                QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);

                if (QualitySettings.antiAliasing != 0)
                {
                    Debug.Log($"Disabling built-in AA for quality level '{names[i]}' (was {QualitySettings.antiAliasing}x)");
                    QualitySettings.antiAliasing = 0;
                }
            }

            // Restore previous quality level
            QualitySettings.SetQualityLevel(originalLevel, applyExpensiveChanges: false);

            // Persist the modification
            var qsAsset = AssetDatabase
                .LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")
                .FirstOrDefault();

            if (qsAsset != null)
            {
                EditorUtility.SetDirty(qsAsset);
                AssetDatabase.SaveAssets();
            }
        }

        // Disables AA in Scriptable Render Pipeline assets (URP/HDRP)
        private static void DisableSRPAA()
        {
            // Find all render pipeline assets in the project
            string[] renderPipelineAssetGuids = AssetDatabase.FindAssets("t:RenderPipelineAsset");

            foreach (string guid in renderPipelineAssetGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var renderPipelineAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(assetPath);

                if (renderPipelineAsset != null)
                {
                    DisableAAForRenderPipelineAsset(renderPipelineAsset, assetPath);
                }
            }

            // Also check the current graphics settings render pipeline asset
            var currentRenderPipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            if (currentRenderPipeline != null)
            {
                string currentAssetPath = AssetDatabase.GetAssetPath(currentRenderPipeline);
                DisableAAForRenderPipelineAsset(currentRenderPipeline, currentAssetPath);
            }
        }

        // Disables AA for a specific render pipeline asset using reflection
        private static void DisableAAForRenderPipelineAsset(UnityEngine.Rendering.RenderPipelineAsset asset, string assetPath)
        {
            if (asset == null) return;

            System.Type assetType = asset.GetType();
            string typeName = assetType.Name;

            Debug.Log($"Processing render pipeline asset: {assetPath} (Type: {typeName})");

            bool modified = false;

            // Handle Universal Render Pipeline (URP)
            if (typeName.Contains("Universal") || typeName.Contains("URP"))
            {
                modified = DisableURPAntiAliasing(asset);
            }
            // Handle High Definition Render Pipeline (HDRP)
            else if (typeName.Contains("HDRenderPipeline") || typeName.Contains("HDRP"))
            {
                modified = DisableHDRPAntiAliasing(asset);
            }

            if (modified)
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                Debug.Log($"Disabled anti-aliasing for {typeName} asset: {assetPath}");
            }
        }

        // Disables anti-aliasing for URP assets using reflection
        private static bool DisableURPAntiAliasing(UnityEngine.Rendering.RenderPipelineAsset asset)
        {
            try
            {
                System.Type assetType = asset.GetType();
                bool modified = false;

                // Try to disable MSAA
                var msaaField = assetType.GetField("m_MSAA", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (msaaField != null)
                {
                    var currentValue = msaaField.GetValue(asset);
                    if (!currentValue.Equals(1)) // 1 = disabled in URP
                    {
                        msaaField.SetValue(asset, 1);
                        modified = true;
                        Debug.Log($"URP MSAA disabled (was {currentValue})");
                    }
                }

                // Try to disable anti-aliasing quality
                var aaQualityField = assetType.GetField("m_AntiAliasing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (aaQualityField != null)
                {
                    var currentValue = aaQualityField.GetValue(asset);
                    if (!currentValue.Equals(0)) // 0 = disabled
                    {
                        aaQualityField.SetValue(asset, 0);
                        modified = true;
                        Debug.Log($"URP Anti-aliasing disabled (was {currentValue})");
                    }
                }

                return modified;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Could not disable URP anti-aliasing via reflection: {e.Message}");
                return false;
            }
        }

        // Disables anti-aliasing for HDRP assets using reflection
        private static bool DisableHDRPAntiAliasing(UnityEngine.Rendering.RenderPipelineAsset asset)
        {
            try
            {
                System.Type assetType = asset.GetType();
                bool modified = false;

                // HDRP typically uses different anti-aliasing methods
                // Try to find and disable common AA fields
                var fields = assetType.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                foreach (var field in fields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("antialiasing") || fieldName.Contains("msaa") || fieldName.Contains("smaa") || fieldName.Contains("taa"))
                    {
                        try
                        {
                            var currentValue = field.GetValue(asset);

                            // Try to set to disabled value (usually 0 or false)
                            if (field.FieldType == typeof(int) && !currentValue.Equals(0))
                            {
                                field.SetValue(asset, 0);
                                modified = true;
                                Debug.Log($"HDRP {field.Name} disabled (was {currentValue})");
                            }
                            else if (field.FieldType == typeof(bool) && (bool)currentValue)
                            {
                                field.SetValue(asset, false);
                                modified = true;
                                Debug.Log($"HDRP {field.Name} disabled");
                            }
                            else if (field.FieldType.IsEnum)
                            {
                                // For enums, try to set to first value (often "None" or "Disabled")
                                var enumValues = System.Enum.GetValues(field.FieldType);
                                if (enumValues.Length > 0 && !currentValue.Equals(enumValues.GetValue(0)))
                                {
                                    field.SetValue(asset, enumValues.GetValue(0));
                                    modified = true;
                                    Debug.Log($"HDRP {field.Name} set to {enumValues.GetValue(0)} (was {currentValue})");
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"Could not modify HDRP field {field.Name}: {ex.Message}");
                        }
                    }
                }

                return modified;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Could not disable HDRP anti-aliasing via reflection: {e.Message}");
                return false;
            }
        }


        #endregion


        #region BUILD


        // Builds the project for WebXR
        public static async Task<bool> BuildProject()
        {
            // Build to a temporary directory, so the build doesn't actually exist in the end
            string tempBuildDir = Path.Combine(Path.GetTempPath(), $"VERA_WebXRBuild_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempBuildDir);

            try
            {
                // Build the project (using current build settings)
                Debug.Log($"Building project into temporary directory: {tempBuildDir}...");
                BuildPlayerOptions bp = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = tempBuildDir,
                    target = EditorUserBuildSettings.activeBuildTarget,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(bp);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new Exception($"Build failed: {report.summary.result}");

                // Zip the build to a memory stream
                Debug.Log("Build succeeded! Zipping build files...");
                byte[] zipBytes;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                    {
                        foreach (string filePath in Directory.GetFiles(tempBuildDir, "*", SearchOption.AllDirectories))
                        {
                            string entryName = filePath.Substring(tempBuildDir.Length + 1).Replace("\\", "/");
                            zip.CreateEntryFromFile(filePath, entryName, System.IO.Compression.CompressionLevel.Optimal);
                        }
                    }
                    zipBytes = ms.ToArray();
                }

                // Upload the zip to the VERA portal
                string url = $"{VERAHost.hostUrl}/api/experiments/{PlayerPrefs.GetString("VERA_ActiveExperiment")}/webxr";
                string jwtToken = PlayerPrefs.GetString("VERA_UserAuthToken");
                Debug.Log($"Zip complete! Uploading to {url}, file size: {zipBytes.Length / 1024f:F1} KB...");

                using (HttpClient http = new HttpClient())
                using (MultipartFormDataContent content = new MultipartFormDataContent())
                {
                    // File part
                    ByteArrayContent zipContent = new ByteArrayContent(zipBytes);
                    zipContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                    content.Add(zipContent, "webxrZip", "webxr.zip");

                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);

                    HttpResponseMessage resp = await http.PostAsync(url, content);
                    string body = await resp.Content.ReadAsStringAsync();

                    if (!resp.IsSuccessStatusCode)
                        throw new Exception($"Upload failed ({(int)resp.StatusCode}): {body}");

                    Debug.Log("Upload complete! Response: " + body);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to build and upload project. {ex}");
                return false;
            }
            finally
            {
                // Clean up the temporary build directory
                try
                {
                    if (Directory.Exists(tempBuildDir))
                        Directory.Delete(tempBuildDir, true);

                    Debug.Log($"Temporary directory deleted: {tempBuildDir}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Could not delete temporary directory: {e.Message}");
                }
            }

            return true;
        }


        #endregion


    }
}
#endif