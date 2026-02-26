using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VERA
{
    internal class VERADemoCubeLogger : MonoBehaviour
    {

        // VERADemoCubeLogger is a demonstrative script used to showcase how to record logs to the VERA portal.
        // Meant to be used alongside the "demo experiment" created when registering for VERA.
        // The script causes a single cube to move and rotate in a pattern, logging its rotation along the way.
        // A demonstrative message is also given for each log to say what entry number the log is.
        // This continues for every frame.

        private int logCount = 0;

        private Vector3 moveDirection = Vector3.right;
        private float moveSpeed = 1f;

        private void Start()
        {
            // Below is a precompiler directive - it can be used to guarantee a VERAFile exists for your file types.
            // The demo experiment should have a file called "CubeRotation" which this script records to.
            // This precompiler directive ensures the file exists, and can be used to log data entries.
            // One precompiler directive is automatically defined for each VERAFile, named as the file is named (VERAFile_FileName)
#if !VERAFile_CubeRotation
        Debug.LogWarning("[VERA Demo Logger] Cannot log cube telemetry, as your experiment does not have a " +
            "defined file type named \"CubeRotation\". To use this demo logger script, please define a file " +
            "type named \"CubeRotation\".");
        this.enabled = false;
#endif
            StartCoroutine(DirectionChangeCoroutine());
        }

        void Update()
        {
            MoveObject();

            if (VERASessionManager.collecting == false || VERASessionManager.initialized == false)
            {
                // If the VERA Logger is not collecting or initialized, do not log anything
                return;
            }

            // Each frame, log the cube's current rotation, then move the object in a pattern
            LogRotation();
        }

        private void LogRotation()
        {
            // In order to log an entry to the demonstrative experiment,
            // we can use the generated code for the experiment's file types.
            // Below, the VERAFile_CubeRotation class is used; this is a generated file created for the CubeRotation file type.
            // There is one of these files generated for each FileType present in the experiment we are recording data to.
            // Note the usage of the precompiler directive to firstly ensure the file exists before using it.
            // This avoids compiler issues which may arise when the file has not yet been generated.

            // The CreateCsvEntry function of this class will create one row entry into the corresponding CSV file.
            // The "ts" / Timestamp column is handled automatically;
            // We must then provide an integer for the eventId column, and any other columns which have been defined.
            // The CubeRotation file type has two additional defined columns, a string for the demonstrative "message"
            // and a string for the cube's rotation. These are strictly typed as according to the corresponding file type.
#if VERAFile_CubeRotation
            VERAFile_CubeRotation.CreateCsvEntry(4, "Data Entry " + logCount, transform.rotation.ToString());
#endif
            logCount++;
        }

        private void MoveObject()
        {
            // Move the cube in a pattern, for demonstrative purposes.
            transform.position = transform.position + (moveDirection * moveSpeed * Time.deltaTime);
            transform.Rotate(moveDirection, moveSpeed * 50f * Time.deltaTime);
        }

        private IEnumerator DirectionChangeCoroutine()
        {
            // Change direction every 2 seconds to add a bit of variation to the pattern.
            while (true)
            {
                moveDirection = Vector3.right;
                yield return new WaitForSeconds(2f);
                moveDirection = Vector3.forward;
                yield return new WaitForSeconds(2f);
                moveDirection = Vector3.left;
                yield return new WaitForSeconds(2f);
                moveDirection = Vector3.back;
                yield return new WaitForSeconds(2f);
            }
        }
    }
}