using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VERA
{
    internal class VERAScreenRecorder : MonoBehaviour
    {
        [ContextMenu("Capture Screenshot")]
        public void TriggerCaptureScreenshot()
        {
            StartCoroutine(CaptureScreenshot());
        }

        private IEnumerator CaptureScreenshot()
        {
            // Wait for the end of the frame to ensure everything is rendered
            yield return new WaitForEndOfFrame();

            // Take a screenshot
            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();

            // Resize the screenshot to 480x480
            Texture2D resizedScreenshot = new Texture2D(480, 480, TextureFormat.RGB24, false);
            // Scale the screenshot to 480x480
            for (int y = 0; y < resizedScreenshot.height; y++)
            {
                for (int x = 0; x < resizedScreenshot.width; x++)
                {
                    Color newColor = screenshot.GetPixelBilinear((float)x / resizedScreenshot.width, (float)y / resizedScreenshot.height);
                    resizedScreenshot.SetPixel(x, y, newColor);
                }
            }
            resizedScreenshot.Apply();

            // Encode the resized screenshot to PNG
            byte[] pngData = resizedScreenshot.EncodeToPNG();
            string directoryPath = Path.Combine(Application.persistentDataPath, "screenshots");
            string timestamp = DateTime.UtcNow.ToString("o");
            string filePath = Path.Combine(directoryPath, $"{((DateTimeOffset)DateTime.Now).ToUnixTimeMilliseconds()}.png");

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
            System.IO.File.WriteAllBytes(filePath, pngData);

            // Clean up
            Destroy(screenshot);
            Destroy(resizedScreenshot);

            Debug.Log("Screenshot saved to: " + filePath);

            VERALogger.Instance.SubmitImageFile(filePath, timestamp, pngData);
        }
    }
}