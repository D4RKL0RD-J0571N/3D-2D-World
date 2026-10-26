using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using MagicPigGames.Northstar.Demo;

namespace MagicPigGames.Northstar
{
    public class SimplePlayerActions : MonoBehaviour
    {
        public static SimplePlayerActions Instance;

        [Header("Options")] public Key interactionKey = Key.Space;
        public float interactionDistance = 1f;
        public LayerMask interactionLayerMask;
        public float interactionAngle = 60f;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(this);
        }

        private void Start()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void Update()
        {
            CheckInteraction();
        }

        private void CheckInteraction()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[interactionKey].wasPressedThisFrame)
                return;

            var hits = Physics.OverlapSphere(transform.position, interactionDistance, interactionLayerMask);
            if (hits.Length == 0)
                return;

            var closestInteractable = GetClosestColliderInView(hits);
            if (closestInteractable == null)
                return;

            closestInteractable.Interact(interactionKey);
        }

        private IInteractable GetClosestColliderInView(Collider[] hits)
        {
            var closestDistance = 9999999f;
            IInteractable closestInteractable = null;
            foreach (var hitCollider in hits)
            {
                var colliderTransform = transform;
                var direction = hitCollider.transform.position - colliderTransform.position;
                var angle = Vector3.Angle(colliderTransform.forward, direction);
                if (angle > interactionAngle)
                    continue;

                var distance = DistanceTo(hitCollider.transform);
                if (!(distance < closestDistance)) continue;

                var interactable = hitCollider.GetComponent<IInteractable>();
                if (interactable == null)
                    continue;

                closestDistance = distance;
                closestInteractable = interactable;
            }

            return closestInteractable;
        }

        public float DistanceTo(Transform targetTransform)
            => Vector3.Distance(transform.position, targetTransform.position);
        
        public bool CloseEnoughToInteractWith(Transform targetTransform)
            => DistanceTo(targetTransform) < interactionDistance;
    }
}
