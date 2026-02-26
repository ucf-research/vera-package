using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace VLAT
{
    public class VLAT_ObjectLabel : MonoBehaviour
    {

        // VLAT_ObjectLabel controls a simple display of a label on an object and billboards


        #region VARIABLES


        [SerializeField] private TMP_Text labelText;
        [SerializeField] private CanvasGroup canvasGroup;
        private Transform transToTrack;
        private Transform labelSpawnLoc;
        private Vector3 labelOffset;
        private bool tracking = false;
        private bool labelAffectedByLocalTransform;


        #endregion


        #region MONOBEHAVIOUR


        // Update
        //--------------------------------------//
        private void Update()
        //--------------------------------------//
        {
            BillboardLabel();
            HideIfClose();

        } // END Update


        // LateUpdate
        //--------------------------------------//
        private void LateUpdate()
        //--------------------------------------//
        {
            if (tracking)
                TrackObject();

        } // END LateUpdate


        #endregion


        #region LABEL


        // Sets the label's text to a given string
        //--------------------------------------//
        public void SetLabelText(string text)
        //--------------------------------------//
        {
            labelText.text = text;

        } // END SetLabelText


        #endregion


        #region BILLBOARDING / CAMERA


        // Billboards the label to face the camera
        //--------------------------------------//
        private void BillboardLabel()
        //--------------------------------------//
        {
            transform.LookAt(Camera.main.transform);
            transform.forward = -transform.forward;

        } // END BillboardLabel


        // Hides the label if it is too close to the camera
        //--------------------------------------//
        private void HideIfClose()
        //--------------------------------------//
        {
            float distance = Vector3.Distance(transform.position, Camera.main.transform.position);
            float alpha = Mathf.InverseLerp(.5f, 1f, distance);
            canvasGroup.alpha = alpha;

        } // END HideIfClose


        #endregion


        #region TRACKING


        // Sets the transform to track
        //--------------------------------------//
        public void SetTrackParams(Transform labelSpawnLoc, Transform transToTrack, Vector3 labelOffset, bool labelAffectedByLocalTransform)
        //--------------------------------------//
        {
            tracking = true;

            this.transToTrack = transToTrack;
            this.labelSpawnLoc = labelSpawnLoc;
            this.labelOffset = labelOffset;
            this.labelAffectedByLocalTransform = labelAffectedByLocalTransform;

            TrackObject();

        } // END SetTrackedTransform


        // Tracks the object which is being tracked
        //--------------------------------------//
        private void TrackObject()
        //--------------------------------------//
        {
            if (transToTrack != null)
            {
                if (labelAffectedByLocalTransform)
                    transform.position = labelSpawnLoc.position;
                else
                    transform.position = transToTrack.position + labelOffset;
            }
            else
            {
                Destroy(gameObject);
            }

        } // END TrackObject


        #endregion


    } // END VLAT_ObjectLabel.cs
}