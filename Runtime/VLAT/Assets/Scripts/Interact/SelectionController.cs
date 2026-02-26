using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;


namespace VLAT
{
    public class SelectionController : MonoBehaviour
    {

        // SelectionController controls the selection of nearby interactable objects


        #region VARIABLES


        private List<VLAT_Interactable> interactables = new List<VLAT_Interactable>();
        private int counter = 0;
        private VLAT_Interactable previousObj;
        private Outline outline;
        private GameObject lookTarget;
        [System.NonSerialized] public VLAT_Interactable currentObj;
        private bool manualHighlightCancel = false;
        private GrabTracker grabTracker;

        [SerializeField] float selectRadius;
        private Camera playerCam; // The point where all distance calculations are made (may change later)
        [SerializeField] Color outlineColor;
        [SerializeField] float outlineWidth = 5f;
        [SerializeField] float highlightDuration = 3f;
        private Arrow arrow;
        private TextMeshPro Text;
        [SerializeField] bool useCameraSelect = false;

        [SerializeField] private VLAT_ObjectLabel objectLabelPrefab;
        Transform objectLabelParent;

        private GameObject interactSub;


        #endregion


        #region MONOBEHAVIOUR


        // Setup
        //--------------------------------------//
        public void Setup(float _selectRadius)
        //--------------------------------------//
        {
#if UNITY_2023_1_OR_NEWER
            grabTracker = FindAnyObjectByType<GrabTracker>();
#else
        grabTracker = FindObjectOfType<GrabTracker>();
#endif
            interactSub = GameObject.Find("Interact Sub");
            //Debug.Log(interactSub);
            playerCam = Camera.main;
#if UNITY_2023_1_OR_NEWER
            arrow = FindAnyObjectByType<Arrow>();
#else
        arrow = FindObjectOfType<Arrow>();
#endif
            selectRadius = _selectRadius;
            objectLabelParent = new GameObject("VLAT Object Labels").transform;

        } // END Setup

        // Update
        //--------------------------------------//
        private void Update()
        //--------------------------------------//
        {
            if (lookTarget != null && arrow != null)
            {
                arrow.transform.LookAt(lookTarget.transform);
            }
            SelectedOutOfRange();

        } // END Update


        #endregion


        #region SELECTION


        // SelectedOutOfRange
        //--------------------------------------//
        public void SelectedOutOfRange()
        //--------------------------------------//
        {
            // UpdateSelectables();
            if (currentObj != null)
            {
                float outside = Vector3.Distance(playerCam.transform.position, currentObj.transform.position);
                if (outside > selectRadius)
                {
                    // Previous current object is out of range, deselect it
                    // Debug.Log("Entered Deselect");
                    // Debug.Log("preob: " + previousObj);
                    UnHighlightInter(currentObj);
                    //currentObj.GetComponent<Outline>().enabled = false;
                    currentObj = null;
                    lookTarget = null;
#if UNITY_2023_1_OR_NEWER
                    FindAnyObjectByType<NewMenuNavigation>().InteractOutOfRange();
#else
                FindObjectOfType<NewMenuNavigation>().InteractOutOfRange();
#endif
                    //treeBase.back(interactSub, GameObject.Find(currentObj + "1"));
                    //currentObj = null;
                }
            }

        } // END SelectedOutOfRange


        // SelectionCycle
        //--------------------------------------//
        public void SelectionCycle()
        //--------------------------------------//
        {
            // Cancel highlighting if all highlighted
            PulseCancelHighlights();

            List<VLAT_Interactable> nearbyInters = GetVisibleNearbyInteractables();
            if (nearbyInters.Count == 0) { return; } // If there is nothing to select, skip for now

            // If the nearby interactables are different from that stored from last frame...
            if (!interactables.SequenceEqual(nearbyInters))
            {
                // Update to the new interactables, and reset the counter
                interactables = new List<VLAT_Interactable>(nearbyInters);
                counter = 0;
            }
            // If they are the same, continue with those interactables in the same order.

            // Clamp counter
            if (counter >= interactables.Count) counter = 0;
            // Ensure we aren't re-selecting the same object due to reordering
            if (currentObj == interactables[counter]) counter++;
            // Re-clamp
            if (counter >= interactables.Count) counter = 0;

            // Set current object
            currentObj = interactables[counter];

            // De-highlight previous object
            if (previousObj != null)
                UnHighlightInter(previousObj);

            // Get current object and get / add outline component
            HighlightInter(interactables[counter]);

            // Disable arrow if object is grabbed
            if (arrow != null)
            {
                if (grabTracker.GetGrabbedObject() == interactables[counter])
                    arrow.gameObject.SetActive(false);
                else
                    arrow.gameObject.SetActive(true);
            }

            // Make sure the camera doesn't snap to the grabbed object
            if (useCameraSelect && (grabTracker.GetGrabbedObject() != interactables[counter]))
                CameraSnap(interactables[counter].gameObject);

            // Logic for determining target's object position relative to player
            ObjectInView();

            previousObj = interactables[counter];
            // Loop back around once end of list
            if (counter == interactables.Count - 1)
                counter = 0;
            else counter++;

        } // END SelectionCycle


        // Gets an array of all nearby interactables
        //--------------------------------------//
        private List<VLAT_Interactable> GetVisibleNearbyInteractables()
        //--------------------------------------//
        {
            List<VLAT_Interactable> nearbyInters = new List<VLAT_Interactable>();

            // Get all colliders nearby
            Collider[] colliders = Physics.OverlapSphere(playerCam.transform.position, selectRadius);

            // On each collider, check if there is an interactable component, add to interactables list if yes
            foreach (Collider collider in colliders)
            {
                if (collider.gameObject.GetComponent<IInteractable>() != null)
                {
                    // RaycastHit hit;
                    Vector3 directionToInteractable = collider.gameObject.transform.position - playerCam.transform.position;
                    Collider[] cameraColliders = playerCam.GetComponentsInChildren<Collider>();
                    Collider[] objChildColliders = collider.gameObject.GetComponentsInChildren<Collider>();
                    Collider[] combinedColliders = cameraColliders.Concat(objChildColliders).ToArray();
                    RaycastHit[] hits = Physics.RaycastAll(playerCam.transform.position, directionToInteractable, Vector3.Distance(playerCam.transform.position, collider.gameObject.transform.position));

                    // If hits is 1, then we have direct line of sight on the object; add it to the list
                    if (hits.Length == 1 && hits[0].collider == collider)
                    {
                        nearbyInters.Add(collider.gameObject.GetComponent<VLAT_Interactable>());
                    }
                    else
                    {
                        // More than 1 hit between us and object.
                        // Check for blockers hindering collision with the object
                        bool objectBlocked = false;
                        hits = hits.OrderBy(hit => Vector3.Distance(playerCam.transform.position, hit.point)).ToArray();
                        foreach (RaycastHit hitCollider in hits)
                        {
                            // If collider is a child of the XR rig, ignore it (should not be considered as a "blocker")
                            if (hitCollider.collider.transform.IsChildOf(VLAT_Options.Instance.GetXrPlayerParent().transform))
                                continue;
                            // If collider is a trigger, it is invisible and should be ignored
                            else if (hitCollider.collider.isTrigger)
                                continue;
                            // If collider is the interactable in question, we can break (can ignore objects past, since array is sorted)
                            else if (hitCollider.collider.gameObject == collider.gameObject || hitCollider.collider.transform.IsChildOf(collider.transform))
                                break;
                            // If collider is neither of above, it's a "blocker", and the object cannot be seen
                            else
                            {
                                objectBlocked = true;
                                break;
                            }
                        }
                        // If the object is not blocked, add it to the list
                        if (!objectBlocked)
                            nearbyInters.Add(collider.gameObject.GetComponent<VLAT_Interactable>());
                    }
                }
            }

            // Order by distance to position in front of player
            nearbyInters.Sort((c1, c2) =>
                (c1.transform.position - (playerCam.transform.position + playerCam.transform.forward)).sqrMagnitude
                .CompareTo((c2.transform.position - playerCam.transform.position).sqrMagnitude));

            return nearbyInters;

        } // END GetVisibleNearbyInteractables


        // Updates highlightable selectables; returns true if there are selectables, false if there are none
        //--------------------------------------//
        bool UpdateSelectables()
        //--------------------------------------//
        {
            // Honestly a suspicious way of doing this: Get all objs in radius > Loop through and find all objs with IInteractable
            interactables.Clear(); // Clear the list first to avoid dupes

            // Get all colliders nearby
            Collider[] colliders = Physics.OverlapSphere(playerCam.transform.position, selectRadius);

            // Order nearby colliders by distance to player
            colliders = colliders.OrderBy(collider => Vector3.Distance(collider.transform.position, playerCam.transform.position)).ToArray();

            // On each collider, check if there is an interactable component, add to interactables list if yes
            foreach (Collider collider in colliders)
            {
                if (collider.gameObject.GetComponent<IInteractable>() != null)
                {
                    // RaycastHit hit;
                    Vector3 directionToInteractable = collider.gameObject.transform.position - playerCam.transform.position;
                    Collider[] cameraColliders = playerCam.GetComponentsInChildren<Collider>();
                    Collider[] objChildColliders = collider.gameObject.GetComponentsInChildren<Collider>();
                    Collider[] combinedColliders = cameraColliders.Concat(objChildColliders).ToArray();
                    RaycastHit[] hits = Physics.RaycastAll(playerCam.transform.position, directionToInteractable, Vector3.Distance(playerCam.transform.position, collider.gameObject.transform.position));

                    // If hits is 1, then we have direct line of sight on the object; add it to the list
                    if (hits.Length == 1 && hits[0].collider == collider)
                    {
                        interactables.Add(collider.gameObject.GetComponent<VLAT_Interactable>());
                    }
                    else
                    {
                        // More than 1 hit between us and object.
                        // Check for blockers hindering collision with the object
                        bool objectBlocked = false;
                        hits = hits.OrderBy(hit => Vector3.Distance(playerCam.transform.position, hit.point)).ToArray();
                        foreach (RaycastHit hitCollider in hits)
                        {
                            // If collider is a child of the XR rig, ignore it (should not be considered as a "blocker")
                            if (hitCollider.collider.transform.IsChildOf(VLAT_Options.Instance.GetXrPlayerParent().transform))
                                continue;
                            // If collider is a trigger, it is invisible and should be ignored
                            else if (hitCollider.collider.isTrigger)
                                continue;
                            // If collider is the interactable in question, we can break (can ignore objects past, since array is sorted)
                            else if (hitCollider.collider.gameObject == collider.gameObject || hitCollider.collider.transform.IsChildOf(collider.transform))
                                break;
                            // If collider is neither of above, it's a "blocker", and the object cannot be seen
                            else
                            {
                                objectBlocked = true;
                                break;
                            }
                        }
                        // If the object is not blocked, add it to the list
                        if (!objectBlocked)
                            interactables.Add(collider.gameObject.GetComponent<VLAT_Interactable>());
                    }
                }
            }

            // Return whether there are interactables nearby or not
            // Debug.Log("I see " + interactables.Count);
            return (interactables.Count > 0) ? true : false;

        } // END UpdateSelectables


        // Grabs all the selectables in the scene, is used to create associating buttons in tree UI
        //--------------------------------------//
        public List<GameObject> grabAllSelectables()
        //--------------------------------------//
        {
            // Honestly a suspicious way of doing this, not to mention expensive, do it once please
            List<GameObject> interactables = new List<GameObject>();

            // Get all GameObjects nearby
            //Collider[] colliders = Physics.OverlapSphere(playerCam.transform.position, selectRadius);
            GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            // On each object, check if there is an interactable component, add to interactables list if yes
            foreach (GameObject obj in objects)
            {
                if (obj.GetComponent<IInteractable>() != null)
                    interactables.Add(obj);
            }

            return interactables;

        } // END grabAllSelectables


        #endregion


        #region OBJECT IN VIEW


        // ObjectInView
        //--------------------------------------//
        void ObjectInView()
        //--------------------------------------//
        {
            lookTarget = interactables[counter].gameObject;
            Vector3 target = lookTarget.transform.position;

            // Normally this would have the player's position relative to camera but not yet!
            Vector3 playerScreenPos = playerCam.WorldToScreenPoint(playerCam.transform.position);
            Vector3 targetScreenPos = playerCam.WorldToScreenPoint(target);

            // Grab the vector to the target
            Vector3 targetPosition = new Vector3(target.x, target.y, target.z);

            // 10 hardcoded, goofy code
            bool isOffScreen = targetScreenPos.x <= 10 || targetScreenPos.x >= Screen.width || targetScreenPos.y <= 10
                                || targetScreenPos.y >= Screen.height;

            if (arrow != null || Text != null) // REFACTOR
            {
                if (isOffScreen)
                {
                    //Text.text = "OFF SCREEN";
                    //Text.color = Color.red;

                }
                else
                {
                    //Text.text = "ON SCREEN";
                    //Text.color = Color.green;
                }
            }

        } // END ObjectInView


        // CameraSnap
        //--------------------------------------//
        void CameraSnap(GameObject target)
        //--------------------------------------//
        {
            playerCam.transform.parent.LookAt(target.transform);

        } // END CameraSnap


        #endregion


        #region HIGHLIGHTING


        // Highlights the given interactable
        //--------------------------------------//
        private void HighlightInter(VLAT_Interactable interactable)
        //--------------------------------------//
        {
            // Activate outline
            GameObject obj = interactable.gameObject;
            Outline outl = obj.GetComponent<Outline>();

            if (outl == null)
                outl = obj.AddComponent<Outline>();


            outl.enabled = true;
            outl.OutlineMode = Outline.Mode.OutlineVisible;
            outl.OutlineColor = outlineColor;
            outl.OutlineWidth = outlineWidth;

            // Add label
            VLAT_ObjectLabel label = GameObject.Instantiate(objectLabelPrefab, objectLabelParent);
            interactable.SetLabel(label);


        } // END HighlightInter


        // Unhighlights given interactable object
        //--------------------------------------//
        private void UnHighlightInter(VLAT_Interactable interactable)
        //--------------------------------------//
        {
            Outline outl = interactable.gameObject.GetComponent<Outline>();
            if (outl != null)
                outl.enabled = false;

            // Remove label
            interactable.RemoveLabel();

        } // END UnHighlightInter


        // Highlights all nearby selectables
        //--------------------------------------//
        public void HighlightAll()
        //--------------------------------------//
        {
            if (!UpdateSelectables()) return; // If there is nothing to select, skip for now
            StartCoroutine(highlight());

        } // END HighlightAll


        // Coroutine to highlight all interactables
        //--------------------------------------//
        IEnumerator highlight()
        //--------------------------------------//
        {
            manualHighlightCancel = false;

            foreach (VLAT_Interactable inter in interactables)
            {
                HighlightInter(inter);
            }
            yield return new WaitForSeconds(highlightDuration);

            // If we manually cancelled highlighting during coroutine, don't cancel highlighting again
            if (!manualHighlightCancel)
            {
                foreach (VLAT_Interactable inter in interactables)
                {
                    UnHighlightInter(inter);
                }
            }

        } // END highlight


        // Pulse cancels all highlighted components
        //--------------------------------------//
        public void PulseCancelHighlights()
        //--------------------------------------//
        {
            manualHighlightCancel = true;

            foreach (VLAT_Interactable inter in interactables)
            {
                if (inter == null)
                    continue;

                UnHighlightInter(inter);
            }

        } // END PulseCancelHighlights


        #endregion


        #region OTHER


        // OnDrawGizmosSelected
        //--------------------------------------//
        private void OnDrawGizmosSelected()
        //--------------------------------------//
        {
            // This assumes that the radius is drawn from player's camera, may not be true later!
            Gizmos.color = Color.red;
            if (playerCam != null)
            {
                Gizmos.DrawWireSphere(playerCam.transform.position, selectRadius);
            }

        } // END OnDrawGizmosSelected


        #endregion


    } // END SelectionController.cs
}
