using UnityEngine;
using System.Collections.Generic;
using System;
using NobleMuffins.LimbHacker.Guts;
using UnityEngine.Events;

namespace NobleMuffins.LimbHacker
{
    public class Hackable : MonoBehaviour, ISliceable
    {
        [Header ("Main Settings")]
        [Space]

        public InfillMode infillMode = InfillMode.Sloppy;

        public bool ignoreUpdateLastObjectSpeed;

        public bool ignoreDestroyOriginalObject;

        public bool setCustomIDOnSliceSpieces;

        public string randomString = "";

        public bool destructionPending = false;

        [Space]
        [Header ("Parts To Slice Settings")]
        [Space]

        public Transform [] severables = new Transform [0];

        [Space]
        [Header ("Events Settings")]
        [Space]

        public UnityEvent eventsOnIgnoreDestroyOriginalObject;

        [Space]
        [Header ("Components")]
        [Space]

        public surfaceToSlice mainSurfaceToSlice;

        public GameObject objectToSlice;

        public UnityEngine.Object alternatePrefab = null;

        public Material infillMaterial = null;


        public Dictionary<string, float> maximumTiltBySeverableName = new Dictionary<string, float> ();

        bool valuesInitialized;

        bool updateLastObjectSpeed;
        Vector3 lastSpeed = Vector3.zero;


        public string getRandomString ()
        {
            return randomString;
        }

        public void setRandomString (string newValue)
        {
            if (randomString.Equals ("")) {
                randomString = newValue;
            }
        }

        public void initializeValues ()
        {
            if (objectToSlice == null) {
                objectToSlice = gameObject;
            }

            foreach (Transform bodyPart in severables) {
                if (bodyPart != null) {
                    ChildOfHackable referencer = bodyPart.GetComponent<ChildOfHackable> ();

                    if (referencer == null)
                        referencer = bodyPart.gameObject.AddComponent<ChildOfHackable> ();

                    referencer.parentHackable = this;
                }
            }

            valuesInitialized = true;
        }

        void checkInitializeValues ()
        {
            if (!valuesInitialized) {
                initializeValues ();
            }
        }

        public event EventHandler<SliceEventArgs> Sliced;

        public void Slice (Vector3 positionInWorldSpace, Vector3 normalInWorldSpace)
        {
            if (destructionPending) {
                return;
            }

            var decisionMaker = objectToSlice.GetComponent<AbstractHackDecisionMaker> ();

            string jointName = null;

            float rootTipProgression = 0f;

            if (LimbHackerAgent.DetermineSlice (this, positionInWorldSpace, ref jointName, ref rootTipProgression) && (decisionMaker == null || decisionMaker.ShouldHack (jointName))) {
                LimbHackerAgent.instance.SeverByJoint (objectToSlice, jointName, rootTipProgression, normalInWorldSpace);
            }
        }

        public void handleSlice (GameObject [] results, Vector4 planeInWorldSpace, Vector3 focalPointInWorldSpace)
        {
            bool originalRemainsAfterSlice = false;

            for (int i = 0; i < results.Length; i++)
                originalRemainsAfterSlice |= results [i] == objectToSlice;

            destructionPending = !originalRemainsAfterSlice;

            AbstractSliceHandler [] handlers = objectToSlice.GetComponents<AbstractSliceHandler> ();

            foreach (AbstractSliceHandler handler in handlers) {
                handler.handleSlice (results);
            }

            if (Sliced != null) {
                Sliced (this, new SliceEventArgs (new Plane (planeInWorldSpace, planeInWorldSpace.w), focalPointInWorldSpace, results));

                if (!ignoreUpdateLastObjectSpeed) {
                    if (updateLastObjectSpeed && lastSpeed != Vector3.zero) {

                        for (int i = 0; i < results.Length; i++) {
                            if (results [i] != null) {

                                Component [] currentRigidbody = results [i].GetComponentsInChildren (typeof (Rigidbody));
                                foreach (Rigidbody child in currentRigidbody) {
                                    if (child.gameObject.activeInHierarchy) {
                                        child.linearVelocity = lastSpeed;
                                    }
                                }

                                //								print (results [i].name);
                            }

                        }
                    }

                    updateLastObjectSpeed = false;

                    lastSpeed = Vector3.zero;
                }
            }
        }

        public bool cloneAlternate (Dictionary<string, bool> hierarchyPresence)
        {
            if (alternatePrefab == null) {
                return false;
            } else {
                AbstractSliceHandler [] handlers = objectToSlice.GetComponents<AbstractSliceHandler> ();

                bool result = false;

                if (handlers.Length == 0) {
                    result = true;
                } else {
                    foreach (AbstractSliceHandler handler in handlers) {
                        result |= handler.cloneAlternate (hierarchyPresence);
                    }
                }

                return result;

            }
        }

        private readonly Queue<PendingSlice> pendingSlices = new Queue<PendingSlice> ();

        bool sliceWaitingToFinish;

        Vector3 normalInWorldSpace;

        private List<GameObject> suppressUntilContactCeases = new List<GameObject> ();

        class PendingSlice
        {
            public PendingSlice (Vector3 _point, ISliceable _target)
            {
                point = _point;
                target = _target;
            }

            public readonly Vector3 point;
            public readonly ISliceable target;
        }


        void LateUpdate ()
        {
            if (sliceWaitingToFinish) {
                while (pendingSlices.Count > 0) {
                    PendingSlice pendingSlice = pendingSlices.Dequeue ();

                    var component = pendingSlice.target as MonoBehaviour;

                    if (component != null) {
                        var targetGameObject = component.gameObject;

                        if (suppressUntilContactCeases.Contains (targetGameObject) == false) {

                            pendingSlice.target.Sliced += PendingSlice_target_Sliced;

                            pendingSlice.target.Slice (pendingSlice.point, normalInWorldSpace);
                        }
                    }
                }

                sliceWaitingToFinish = false;

                ContactCeased (objectToSlice);
            }
        }

        void PendingSlice_target_Sliced (object sender, SliceEventArgs e)
        {
            if (e.Parts.Length > 1) {
                suppressUntilContactCeases.AddRange (e.Parts);
            }
        }

        private void ContactCeased (GameObject other)
        {
            if (suppressUntilContactCeases.Contains (other)) {
                suppressUntilContactCeases.Remove (other);
            }
        }

        public void activateSlice (GameObject objectToSlice, Vector3 point, Vector3 newNormalInWorldSpaceValue,
                                   bool updateLastObjectSpeedValue, Vector3 lastSpeedValue)
        {
            checkInitializeValues ();

            ISliceable sliceable = objectToSlice.GetComponent (typeof (ISliceable)) as ISliceable;

            if (sliceable != null) {
                pendingSlices.Enqueue (new PendingSlice (point, sliceable));

                normalInWorldSpace = newNormalInWorldSpaceValue;

                sliceWaitingToFinish = true;

                updateLastObjectSpeed = updateLastObjectSpeedValue;
                lastSpeed = lastSpeedValue;
            }
        }

        public void setDestructionPending (bool state)
        {
            destructionPending = state;
        }
    }
}