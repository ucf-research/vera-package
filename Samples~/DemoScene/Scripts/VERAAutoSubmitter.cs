using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VERA
{
    internal class VERAAutoSubmitter : MonoBehaviour
    {

        // VERAAutoSubmitter automatically submits all CSVs after a certain amount of time

        [Tooltip("The duration to delay, after which the CSVs will be submitted")]
        [SerializeField] private float delayBeforeSubmit = 10f;

        // Start is called before the first frame update
        void Start()
        {
            StartCoroutine(WaitThenSubmitCSV(delayBeforeSubmit));
        }

        // Waits, then submits the stored CSV file
        private IEnumerator WaitThenSubmitCSV(float timeToDelay)
        {
            yield return new WaitForSeconds(timeToDelay);

            if (VERASessionManager.collecting)
            {
                Debug.Log($"[VERA Auto-Submitter] Submitting final CSVs and marking COMPLETE after {timeToDelay} seconds...");
                VERASessionManager.FinalizeSession();
            }
        }
    }
}
