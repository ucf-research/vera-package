using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VLAT
{
    public class VLAT_Interactable : MonoBehaviour, IInteractable
    {

        // VLAT_Interactable provides a base for all interactable objects


        #region VARIABLES


        [Tooltip("The name of this interactable, written as it should be displayed to the player")]
        public string interactableName;
        [Tooltip("The object's possible interactions")]
        public List<InteractionInfo> interactions;

        [Header("Labels")]
        [Tooltip("(Optional) The location at which the object's label will spawn")]
        public Transform labelSpawnLocation;
        [Tooltip("Whether the label should be affected by local transform; for example, if a label's spawn location " +
            "is on the end of a door, this should be set to true, such that the label rotates with the door; however, " +
            "if a label's spawn location is above a grabbable ball, this should be set to false, such that the label " +
            "does not crazily rotate around the ball as it is rolled across the ground.")]
        public bool labelAffectedByLocalTransform = false;

        private VLAT_ObjectLabel currentObjectLabel;
        private Vector3 initialLabelOffset;


        #endregion


        #region INIT


        // Awake
        //--------------------------------------//
        private void Awake()
        //--------------------------------------//
        {
            if (labelSpawnLocation != null)
                initialLabelOffset = labelSpawnLocation.position - transform.position;
            else
                initialLabelOffset = Vector3.zero;

            ConfirmCollider();

        } // END Awake


        // Spawns a dummy collider if no collider is present (for finding interactables by colliders)
        //--------------------------------------//
        private void ConfirmCollider()
        //--------------------------------------//
        {
            // If no collider present, add a trigger collider
            if (GetComponent<Collider>() == null)
            {
                SphereCollider newCollider = gameObject.AddComponent<SphereCollider>();
                newCollider.isTrigger = true;
                newCollider.radius = .01f;
            }

        } // END ConfirmCollider


        #endregion


        #region IINTERACTABLE FUNCTIONS


        // Gets all interactions this interactable can be used with
        //--------------------------------------//
        public List<string> GetInteractions()
        //--------------------------------------//
        {
            // Build a list of strings consisting of the names of interactables
            List<string> ret = new List<string>();

            foreach (InteractionInfo i in interactions)
            {
                ret.Add(i.interactionName);
            }

            // Return list
            return ret;

        } // END GetInteractions


        // Triggers a given interaction
        //--------------------------------------//
        public void TriggerInteraction(string interaction)
        //--------------------------------------//
        {
            // Find interaction in list by given name
            foreach (InteractionInfo i in interactions)
            {
                // If we find an interaction matching the given name...
                if (i.interactionName == interaction)
                {
                    // Trigger interaction and exit function
                    i.onTriggerInteraction.Invoke();
                    return;
                }
            }

            // If no interaction was found by given name, log an error
            Debug.LogError("VERA_Interactable (" + gameObject.name + "): Attempted to invoke interaction \"" +
                interaction + "\", but no such interaction was found.");

        } // END TriggerInteraction


        #endregion


        #region ADDITIONAL FUNCTIONALITY


        // Triggers an interaction of this interactable by index (if possible)
        //--------------------------------------//
        public void TriggerInteractionByIndex(int index)
        //--------------------------------------//
        {
            if (index < interactions.Count)
            {
                interactions[index].onTriggerInteraction.Invoke();
            }

        } // END TriggerInteractionByIndex


        #endregion


        #region LABELS


        // Sets the interactable's label, removes existing labels
        //--------------------------------------//
        public void SetLabel(VLAT_ObjectLabel label)
        //--------------------------------------//
        {
            RemoveLabel();

            currentObjectLabel = label;
            label.SetTrackParams(labelSpawnLocation, transform, initialLabelOffset, labelAffectedByLocalTransform);
            label.SetLabelText(interactableName);

        } // END SetLabel


        // Removes the interactable's label, if applicable
        //--------------------------------------//
        public void RemoveLabel()
        //--------------------------------------//
        {
            if (currentObjectLabel != null)
            {
                GameObject.Destroy(currentObjectLabel.gameObject);
                currentObjectLabel = null;
            }

        } // END RemoveLabel


        #endregion


    } // END VLAT_Interactable.cs


    [System.Serializable]
    public class InteractionInfo
    {

        // InteractionInfo holds information necessary to trigger and display an interaction


        #region VARIABLES


        [Tooltip("The name of the interaction, written as it should be displayed to the player")]
        public string interactionName;
        [Tooltip("A UnityEvent which is called when the player attempts to perform this interaction " +
            "(link this event to your desired interaction functionality, such as a function which performs the interaction)")]
        public UnityEvent onTriggerInteraction;


        #endregion


    } // END InteractionInfo.cs
}