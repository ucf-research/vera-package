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

        private SurveyInterfaceIO surveyInterface;

        [Tooltip("The ID of the survey you wish to start")]
        [SerializeField] private string surveyId;
        [Tooltip("Whether the survey should begin immediately upon Start()")]
        [SerializeField] private bool beginOnStart;

        // Start
        void Start()
        {
            surveyInterface = GetComponent<SurveyInterfaceIO>();

            if (beginOnStart)
                StartSurvey();
        }

        // Starts survey
        public void StartSurvey()
        {
            surveyInterface.StartSurveyById(surveyId);
        }
    }
}