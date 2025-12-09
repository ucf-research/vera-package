using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VERA
{
    internal class VERAGenericFileUploaderDemo : MonoBehaviour
    {
        // This class demonstrates how to upload an arbitrary file to the VERA server.
        [ContextMenu("Test")] // Right-click on the script in the inspector in Play Mode and click "Test" to run this method
        private void TestFileCreationAndUpload()
        {
            string file;
            // In the editor, you use Application.dataPath to get the path to the Assets folder.
            // In a build, you use Application.persistentDataPath to get the path to the persistent data directory.
#if UNITY_EDITOR
            file = Path.Combine(Application.dataPath, "VERA", "data", "test.txt");
#else
            file = Path.Combine(Application.persistentDataPath, "test.txt");
#endif
            FileStream f = File.Create(file);
            Debug.Log($"file created at: {file}");
            f.Close();
            VERALogger.Instance.SubmitGenericFile(file);
        }
    }
}