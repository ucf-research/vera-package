using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace VLAT
{
    public class AccessibilityNotification : MonoBehaviour
    {

        // AccessibilityNotification handles the showing and hiding of an interaction


        #region VARIABLES


        public static AccessibilityNotification Instance;

        [SerializeField] private TMP_Text notificationText;
        private CanvasGroup canvGroup;
        private Coroutine notificationCoroutine;
        private Vector3 startPosLocal;
        private Vector3 endPosLocal;

        private float moveDist = 50f;
        private float animTime = .25f;
        private float notificationDuration = 3f;


        #endregion


        #region MONOBEHAVIOUR


        // Awake, sets up components
        //--------------------------------------//
        void Awake()
        //--------------------------------------//
        {
            if (Instance != null)
                Destroy(this);

            Instance = this;

            canvGroup = GetComponent<CanvasGroup>();
            startPosLocal = transform.localPosition;
            endPosLocal = startPosLocal + Vector3.up * moveDist;

        } // END Awake


        #endregion


        #region SHOW NOTIF


        // Cancel any active animations and notification showing; then, show the notification
        //--------------------------------------//
        public void ShowNotification(string notificationToShow)
        //--------------------------------------//
        {
            // Reset coroutine and animations
            if (notificationCoroutine != null)
                StopCoroutine(notificationCoroutine);

            LeanTween.cancel(gameObject);

            canvGroup.alpha = 0f;
            transform.localPosition = startPosLocal;

            // Update text and start coroutine
            notificationText.text = notificationToShow;
            notificationCoroutine = StartCoroutine(ShowNotifCoroutine());

        } // END ShowNotification


        // Shows the notification with animations, then after a bit hides the notification
        //--------------------------------------//
        private IEnumerator ShowNotifCoroutine()
        //--------------------------------------//
        {
            transform.LeanMoveLocal(endPosLocal, animTime).setEaseOutQuad();
            canvGroup.LeanAlpha(1f, animTime);

            yield return new WaitForSeconds(notificationDuration);

            canvGroup.LeanAlpha(0f, animTime);

        } // END ShowNotifCoroutine


        #endregion


    } // END NoInteractionsNotif.cs
}