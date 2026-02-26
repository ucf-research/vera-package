using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VERA
{
    internal class FollowGaze : MonoBehaviour
    {

        // FollowGaze allows a game object to follow the user's gaze at a set distance

        [Tooltip("The desired distance from the object to the player's headset")]
        [SerializeField] private float distance = 1f;
        [Tooltip("An offset to the vertical position of the object")]
        [SerializeField] private float heightOffset;

        // Update
        void Update()
        {
            MatchGaze();
        }

        // Transform the game object to follow the user's gaze
        private void MatchGaze()
        {
            Transform target = Camera.main.transform;

            // Get the player's gaze direction on the XZ plane
            Vector3 gazeDirection = target.forward;
            gazeDirection.y = 0; // Ignore vertical gaze
            gazeDirection.Normalize();

            // Calculate the desired position of the game object
            Vector3 targetPosition = target.position + gazeDirection * distance + Vector3.up * heightOffset;

            // Set the new position and rotation
            transform.position = targetPosition;
            transform.LookAt(target);
            transform.rotation = Quaternion.LookRotation(-transform.forward, transform.up);
        }
    }
}