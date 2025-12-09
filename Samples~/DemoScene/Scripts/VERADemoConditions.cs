using System.Collections;
using UnityEngine;

namespace VERA
{
    internal class VERADemoConditions : MonoBehaviour
    {

        // This demo shows how to use condition groups in your own scripts.
        // The code below is a stand-in example, you can adapt it to your needs
        // (i.e. change "MyIndependentVariable" and "MySelectedValue" to your own independent variable and value names).
        // For example, say you had a "Weather" independent variable with values "Sunny" and "Rainy",
        // You could replace "MyIndependentVariable" with "Weather" and "MySelectedValue" with "Sunny" or "Rainy".

        // How conditions will be assigned (simple or by participant ID)
        private bool useSimpleConditionSetting = true;

        void Start()
        {
            // In this demo, we set the participant's conditions on Start().
            // Change useSimpleConditionSetting to demonstrate two different example methods of assigning conditions:
            //   1) Simple condition setting to a specific value
            //   2) "Automatic" condition setting based on participant ID (e.g. 50/50 split)
            // To set conditions, the VERA session must be fully initialized.
            // To make sure our code runs after initialization, we use the onInitialized event.
            if (useSimpleConditionSetting)
            {
                // This line makes sure "SetConditionsSimple" is called right after the VERA session is initialized
                VERASessionManager.onInitialized.AddListener(SetConditionsSimple);
            }
            else
            {
                // This line makes sure "SetConditionsByParticipantID" is called right after the VERA session is initialized
                VERASessionManager.onInitialized.AddListener(SetConditionsByParticipantID);
            }
        }

        // Example method 1: Simple condition setting
        // Sets a specific independent variable to a specific value
        private void SetConditionsSimple()
        {
            // By encompassing the code in a #if statement, we ensure that it only runs if the condition group is part of the current experiment.
            // This prevents compilation errors in case the condition group is not included in the experiment.
            // There will always be a define symbol for each condition group, in the format VERAIV_[IndependentVariableName].
            // You can check the define symbols in Edit > Project Settings > Player > Other Settings > Scripting Define Symbols.
#if VERAIV_MyIndependentVariable

            // This line sets the independent variable "MyIndependentVariable" to the value "MySelectedValue".
            // For example, if your independent variable is "Weather" and you want to set it to "Sunny",
            // you would use: VERAIV_Weather.SetSelectedValue(VERAIV_Weather.IVValue.V_Sunny);
            VERAIV_MyIndependentVariable.SetSelectedValue(VERAIV_MyIndependentVariable.IVValue.V_MySelectedValue);
#endif
        }


        // Example method 2: Condition setting based on participant ID
        // Sets independent variable values based on the participant's ID (e.g. to create balanced groups)
        // In this example, we use a simple 50/50 split based on whether the participant ID is even or odd
        private void SetConditionsByParticipantID()
        {
#if VERAIV_MyIndependentVariable
            // Get the participant ID from the VERA session manager
            int participantID = VERASessionManager.participantID;

            // If the participant ID is even, assign one value; if odd, assign the other value.
            if (participantID % 2 == 0)
            {
                // This line sets the independent variable "MyIndependentVariable" to the value "ValueA".
                // For example, if your independent variable is "Weather" and you want to set it to "Sunny",
                // you would use: VERAIV_Weather.SetSelectedValue(VERAIV_Weather.IVValue.V_Sunny);
                VERAIV_MyIndependentVariable.SetSelectedValue(VERAIV_MyIndependentVariable.IVValue.V_ValueA);
            }
            else
            {
                // This line sets the independent variable "MyIndependentVariable" to the value "ValueB".
                // For example, if your independent variable is "Weather" and you want to set it to "Rainy",
                // you would use: VERAIV_Weather.SetSelectedValue(VERAIV_Weather.IVValue.V_Rainy);
                VERAIV_MyIndependentVariable.SetSelectedValue(VERAIV_MyIndependentVariable.IVValue.V_ValueB);
            }
#endif
        }
    }
}