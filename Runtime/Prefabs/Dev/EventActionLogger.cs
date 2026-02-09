using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VERA
{
    internal class EventActionLogger : MonoBehaviour
    {

        [SerializeField] private GameObject targetObject;
        [SerializeField] private string fileToRecordTo = "DemoFile";
        [SerializeField] private bool onlyAllowOneLogForEntry = false;
        private bool eventAlreadyLogged = false;

        // Logs an event
        public void LogEvent(int eventId)
        {
            if (onlyAllowOneLogForEntry)
                if (eventAlreadyLogged)
                    return;

            if (VERALogger.Instance.collecting)
            {
                VERALogger.Instance.CreateCsvEntry(
                  // File name
                  fileToRecordTo,
                  // Event ID
                  eventId,
                  // Target transform
                  targetObject.transform
                );

                eventAlreadyLogged = true;
            }
        }

    }
}