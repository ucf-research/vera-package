using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VERA
{
    internal class ReorderOnActivate : MonoBehaviour
    {

        // Upon being activated, this game object will reorder a target game object to be the last sibling.
        //     (Allows newly activated canvases to render above other canvases)

        [Tooltip("Whether to track activation (e.g., upon this object's activation, trigger reorder)")]
        [SerializeField] private bool trackActivation = false;

        [Tooltip("Whether to track CanvasGroup alpha changes (e.g., upon this object becoming visible via adjustments of its CanvasGroup alpha, trigger reorder)")]
        [SerializeField] private bool trackCanvasGroupAlpha = false;
        private float lastAlpha = 0f;
        private CanvasGroup canvasGroup;

        [Tooltip("The TARGET object, which will be reordered upon THIS object's activation")]
        [SerializeField] private GameObject targetObject;

        // Awake, set up canvas group
        private void Awake()
        {
            if (trackCanvasGroupAlpha)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        // LateUpdate, track canvas group for any new changes past 0f
        private void LateUpdate()
        {
            // Detect change from hidden to visible, e.g., should be reordered
            if (trackCanvasGroupAlpha)
            {
                if (lastAlpha == 0f && canvasGroup.alpha > 0f)
                {
                    SetAsLastSibling();
                }

                lastAlpha = 0f;
            }
        }

        // OnEnable, reorder the transform to be the last sibling (render above others)
        private void OnEnable()
        {
            if (trackActivation)
                SetAsLastSibling();
        }

        // Sets the target object as the last sibling, making it visible above others
        private void SetAsLastSibling()
        {
            targetObject.transform.SetAsLastSibling();
        }
    }
}