using UnityEngine;

namespace VERA
{
    /// Manager that automatically creates and configures the VERABaselineDataLogger.
    /// Add this to any GameObject in the scene to enable baseline data logging.
    internal class VERABaselineDataLoggerManager : MonoBehaviour
    {
        [Header("Auto-Setup")]
        [Tooltip("Automatically create VERABaselineDataLogger if none exists")]
        [SerializeField] private bool autoCreateLogger = true;
        
        [Header("Logging Settings")]
        [Tooltip("Logging rate in Hz")]
        [SerializeField] private float logRate = 30f;
        
        private VERABaselineDataLogger baselineLogger;
        
        private void Awake()
        {
            if (autoCreateLogger)
            {
                SetupBaselineLogger();
            }
        }
        
        private void SetupBaselineLogger()
        {
            // Check if VERABaselineDataLogger already exists in the scene
#if UNITY_2023_1_OR_NEWER
            baselineLogger = FindAnyObjectByType<VERABaselineDataLogger>();
#else
            baselineLogger = FindObjectOfType<VERABaselineDataLogger>();
#endif
            
            if (baselineLogger == null)
            {
                // Create new GameObject for the baseline logger
                GameObject loggerObject = new GameObject("VERABaselineDataLogger");
                baselineLogger = loggerObject.AddComponent<VERABaselineDataLogger>();
                
                // Configure the logger
                baselineLogger.SetLogRate(logRate);
            }
            else
            {
                // Use existing logger
            }
        }

        [ContextMenu("Start Baseline Logging")]
        public void StartLogging()
        {
            if (baselineLogger != null && !baselineLogger.IsLogging())
            {
                // The logger should auto-start when VERA is ready
            }
            else if (baselineLogger == null)
            {
                SetupBaselineLogger();
            }
        }
        
        [ContextMenu("Stop Baseline Logging")]
        public void StopLogging()
        {
            if (baselineLogger != null)
            {
                baselineLogger.StopLogging();
            }
        }

        public bool IsLogging()
        {
            return baselineLogger != null && baselineLogger.IsLogging();
        }
        
        public int GetCurrentSampleIndex()
        {
            return baselineLogger != null ? baselineLogger.GetCurrentSampleIndex() : 0;
        }
    }
}