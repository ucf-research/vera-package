using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VERA
{
    [RequireComponent(typeof(SurveyInterfaceIO))]
    internal class DemoSurveyStarter : MonoBehaviour
    {

        // DemoSurveyStarter provides example starting of a VERA survey via the SurveyInterfaceIO
        // Should be attached to a SurveyInterface prefab to properly work
        //
        // NOTE: For easier usage, consider using the new VERASurveyManager API instead:
        //   VERASurveyManager.ShowSurvey(surveyId, onCompleted);
        // See ExperimentSurveyExample.cs for more examples.

        private SurveyInterfaceIO surveyInterface;

        [Header("Mode Selection")]
        [Tooltip("Use instance ID (production mode) or survey ID (testing mode)")]
        [SerializeField] private bool useInstanceId = false;

        [Header("Survey Configuration")]
        [Tooltip("The ID of the survey template (testing mode)")]
        [SerializeField] private string surveyId;
        [Tooltip("The ID of the survey instance (production mode)")]
        [SerializeField] private string surveyInstanceId;

        [Header("Behavior")]
        [Tooltip("Whether the survey should begin immediately upon Start()")]
        [SerializeField] private bool beginOnStart;

        // Start
        void Start()
        {
            surveyInterface = GetComponent<SurveyInterfaceIO>();

            if (beginOnStart)
                StartSurvey();
        }

        // Starts survey using either instance ID (production) or survey ID (testing)
        public void StartSurvey()
        {
            if (useInstanceId)
            {
                // Production mode: use pre-created instance
                surveyInterface.StartSurveyByInstanceId(surveyInstanceId);
            }
            else
            {
                // Testing mode: create new instance from template
                surveyInterface.StartSurveyById(surveyId);
            }
        }
    }
}