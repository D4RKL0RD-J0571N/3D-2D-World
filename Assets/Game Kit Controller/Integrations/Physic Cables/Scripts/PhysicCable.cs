using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HPhysic
{
    public class PhysicCable : MonoBehaviour
    {
        [Header ("Main Settings")]
        [Space]

        [SerializeField] private int numberOfPoints = 3;
        [SerializeField] private float space = 0.3f;
        [SerializeField] private float size = 0.3f;

        [Space]
        [Header ("Physics Settings")]
        [Space]

        [SerializeField] private float springForce = 200;
        public float dragValueOnCablePieces = 0;
        public float damperOnCablePieces = 0.2f;

        public float cablePointMass = 0.1f;
        public float cableExtremesMass = 1;

        [Space]
        [Header ("Break Settings")]
        [Space]

        public bool cableBreakEnabled = true;
        public bool cableCanBreakOnlyIfConnected = true;
        public float minTimeToBreak = 1;

        public bool canBeBrokenExternallyEnabled = true;

        [Space]
        [Header ("Disconnect Cable Elements")]
        [Space]

        public bool disconnectCableWhenLongEnoughEnabled = true;
        [SerializeField] private float brakeLengthMultiplier = 2f;
        [SerializeField] private float minBrakeTime = 1f;

        [Space]
        [Header ("Cable Elements")]
        [Space]

        public GameObject start;
        public GameObject end;
        public GameObject connector0;
        public GameObject point0;

        [Space]
        [Header ("Debug")]
        [Space]

        public bool isConnected;

        public bool cableBroken;

        public float cableLength;

        public bool startConnected;

        public bool endConnected;

        [Space]

        public List<int> brokenPointsIndex = new List<int> ();

        public List<Transform> points;
        public List<Transform> connectors;

        [Space]
        [Header ("Events Settings")]
        [Space]

        public UnityEvent eventOnBreak;

        public UnityEvent eventOnBreakFromTension;

        [Space]

        public bool useEventToDisconnectBothPoints;
        public UnityEvent eventToDisconnectBothPoints;


        private const string cloneText = "Part";

        private Connector startConnector;
        private Connector endConnector;

        private float brakeLength;
        private float timeToBrake = 1f;

        float timeToBreak = 1;

        public Connector StartConnector => startConnector;
        public Connector EndConnector => endConnector;
        public IReadOnlyList<Transform> Points => points;


        private void Start ()
        {
            startConnector = start.GetComponent<Connector> ();
            endConnector = end.GetComponent<Connector> ();

            brakeLength = space * numberOfPoints * brakeLengthMultiplier + 2f;

            points = new List<Transform> ();

            connectors = new List<Transform> ();

            points.Add (start.transform);
            points.Add (point0.transform);

            connectors.Add (connector0.transform);

            for (int i = 1; i < numberOfPoints; i++) {

                Transform conn = GetConnector (i);

                if (conn == null) {
                    Debug.LogWarning ("Dont found connector number " + i);
                } else {
                    connectors.Add (conn);
                }

                Transform point = GetPoint (i);

                if (conn == null) {
                    Debug.LogWarning ("Dont found point number " + i);
                } else {
                    points.Add (point);
                }
            }

            Transform endConn = GetConnector (numberOfPoints);

            if (endConn == null) {
                Debug.LogWarning ("Dont found connector number " + numberOfPoints);
            } else {
                connectors.Add (endConn);
            }

            points.Add (end.transform);
        }

        private void Update ()
        {
            cableLength = 0f;

            isConnected = startConnected || endConnected;

            //startConnector.IsConnected || endConnector.IsConnected;

            int numOfParts = connectors.Count;

            Transform lastPoint = points [0];

            for (int i = 0; i < numOfParts; i++) {
                bool canCheckPoinResult = true;

                if (cableBroken) {
                    if (brokenPointsIndex.Contains (i + 1)) {
                        canCheckPoinResult = false;
                    }
                }

                Transform nextPoint = points [i + 1];

                Transform connector = connectors [i].transform;

                connector.position = CountConPos (lastPoint.position, nextPoint.position);

                if (lastPoint.position == nextPoint.position || nextPoint.position == connector.position || !canCheckPoinResult) {
                    connector.localScale = Vector3.zero;

                } else {
                    connector.rotation = Quaternion.LookRotation (nextPoint.position - connector.position);
                    connector.localScale = CountSizeOfCon (lastPoint.position, nextPoint.position);
                }

                if (isConnected || !cableCanBreakOnlyIfConnected) {
                    cableLength += (lastPoint.position - nextPoint.position).magnitude;
                }

                lastPoint = nextPoint;
            }

            if (cableBreakEnabled && !cableBroken) {
                if (isConnected || !cableCanBreakOnlyIfConnected) {
                    if (cableLength > brakeLength) {
                        timeToBreak -= Time.deltaTime;

                        if (timeToBreak < 0f) {

                            timeToBreak = minTimeToBreak;

                            cableBroken = true;

                            eventOnBreakFromTension.Invoke ();

                            checkEventOnBreak ();
                        }
                    } else {
                        timeToBreak = minTimeToBreak;
                    }
                }
            }

            if (disconnectCableWhenLongEnoughEnabled) {
                if (isConnected) {
                    if (cableLength > brakeLength) {
                        timeToBrake -= Time.deltaTime;

                        if (timeToBrake < 0f) {
                            //startConnector.Disconnect ();

                            //endConnector.Disconnect ();

                            timeToBrake = minBrakeTime;

                            checkEventToDisconnectBothPoints ();
                        }
                    } else {
                        timeToBrake = minBrakeTime;
                    }
                }
            }
        }

        public void setStartConnectedState (bool state)
        {
            startConnected = state;
        }

        public void setEndConnectedState (bool state)
        {
            endConnected = state;
        }


        public void UpdatePointsFromEditor ()
        {
            UpdatePoints ();

            updatValuesFromEditor ();

            updateComponent ();
        }

        private void UpdatePoints ()
        {
            if (!start || !end || !point0 || !connector0) {
                Debug.LogWarning ("Can't update because one of objects to set is null!");

                return;
            }

            // delete old
            int length = transform.childCount;

            for (int i = 0; i < length; i++) {
                if (transform.GetChild (i).name.StartsWith (cloneText)) {

                    GKC_Utils.checkUnpackPrefabToDestroyObject (gameObject, transform.GetChild (i).gameObject);

                    length--;

                    i--;
                }
            }

            // set new
            Vector3 lastPos = start.transform.position;

            Rigidbody lastBody = start.GetComponent<Rigidbody> ();

            for (int i = 0; i < numberOfPoints; i++) {
                GameObject cConnector = i == 0 ? connector0 : CreateNewCon (i);

                GameObject cPoint = i == 0 ? point0 : CreateNewPoint (i);

                Vector3 newPos = CountNewPointPos (lastPos);

                cPoint.transform.position = newPos;

                cPoint.transform.localScale = Vector3.one * size;

                cPoint.transform.rotation = transform.rotation;


                SetSpring (cPoint.GetComponent<SpringJoint> (), lastBody);

                lastBody = cPoint.GetComponent<Rigidbody> ();

                cConnector.transform.position = CountConPos (lastPos, newPos);

                cConnector.transform.localScale = CountSizeOfCon (lastPos, newPos);

                cConnector.transform.rotation = CountRoationOfCon (lastPos, newPos);

                lastPos = newPos;
            }

            Vector3 endPos = CountNewPointPos (lastPos);

            end.transform.position = endPos;

            SpringJoint lastBodySpringJoint = lastBody.gameObject.AddComponent<SpringJoint> ();

            //if (lastBodySpringJoint == null) {
            //    lastBodySpringJoint = lastBody.gameObject.AddComponent<SpringJoint> ();
            //}

            SetSpring (lastBodySpringJoint, end.GetComponent<Rigidbody> ());

            GameObject endConnector = CreateNewCon (numberOfPoints);

            endConnector.transform.position = CountConPos (lastPos, endPos);

            endConnector.transform.rotation = CountRoationOfCon (lastPos, endPos);
        }

        void updatValuesFromEditor ()
        {
            Component [] components = GetComponentsInChildren (typeof (Rigidbody));

            foreach (Rigidbody child in components) {
                child.linearDamping = dragValueOnCablePieces;

                child.mass = cablePointMass;
            }

            Rigidbody startRigidbody = start.GetComponent<Rigidbody> ();

            if (startRigidbody != null) {
                startRigidbody.mass = cableExtremesMass;
            }

            Rigidbody endRigidbody = end.GetComponent<Rigidbody> ();

            if (endRigidbody != null) {
                endRigidbody.mass = cableExtremesMass;
            }

            components = GetComponentsInChildren (typeof (SpringJoint));

            foreach (SpringJoint child in components) {
                child.spring = springForce;

                child.damper = damperOnCablePieces;
            }

            updateComponent ();
        }

        Vector3 CountNewPointPos (Vector3 lastPos)
        {
            return lastPos + transform.forward * space;
        }

        public void AddPointFromEditor ()
        {
            AddPoint ();

            updateComponent ();
        }

        private void AddPoint ()
        {
            Transform lastprevPoint = GetPoint (numberOfPoints - 1);

            if (lastprevPoint == null) {
                Debug.LogWarning ("Dont found point number " + (numberOfPoints - 1));

                return;
            }

            Rigidbody endRB = end.GetComponent<Rigidbody> ();

            foreach (var spring in lastprevPoint.GetComponents<SpringJoint> ()) {
                if (spring.connectedBody == endRB) {
                    DestroyImmediate (spring);
                }
            }

            GameObject cPoint = CreateNewPoint (numberOfPoints);
            GameObject cConnector = CreateNewCon (numberOfPoints + 1);

            cPoint.transform.position = end.transform.position;
            cPoint.transform.rotation = end.transform.rotation;
            cPoint.transform.localScale = Vector3.one * size;

            SetSpring (cPoint.GetComponent<SpringJoint> (), lastprevPoint.GetComponent<Rigidbody> ());

            SpringJoint cPointSpringJoint = cPoint.gameObject.AddComponent<SpringJoint> ();

            //if (cPointSpringJoint == null) {
            //    cPointSpringJoint = cPoint.gameObject.AddComponent<SpringJoint> ();
            //}

            SetSpring (cPointSpringJoint, endRB);

            // fix end
            end.transform.position += end.transform.forward * space;

            cConnector.transform.position = CountConPos (cPoint.transform.position, end.transform.position);
            cConnector.transform.localScale = CountSizeOfCon (cPoint.transform.position, end.transform.position);
            cConnector.transform.rotation = CountRoationOfCon (cPoint.transform.position, end.transform.position);

            numberOfPoints++;
        }

        public void RemovePointFromEditor ()
        {
            RemovePoint ();

            updateComponent ();
        }

        private void RemovePoint ()
        {
            if (numberOfPoints < 2) {
                Debug.LogWarning ("Cable can't be shorter then 1");

                return;
            }

            Transform lastprevPoint = GetPoint (numberOfPoints - 1);

            if (lastprevPoint == null) {
                Debug.LogWarning ("Dont found point number " + (numberOfPoints - 1));

                return;
            }

            Transform lastprevCon = GetConnector (numberOfPoints);

            if (lastprevCon == null) {
                Debug.LogWarning ("Dont found connector number " + (numberOfPoints));

                return;
            }

            Transform lastlastprevPoint = GetPoint (numberOfPoints - 2);

            if (lastlastprevPoint == null) {
                Debug.LogWarning ("Dont found point number " + (numberOfPoints - 2));

                return;
            }


            Rigidbody endRB = end.GetComponent<Rigidbody> ();

            SpringJoint lastlastprevPointSpringJoint = lastlastprevPoint.gameObject.AddComponent<SpringJoint> ();

            //if (lastlastprevPointSpringJoint == null) {
            //    lastlastprevPointSpringJoint = lastlastprevPoint.gameObject.AddComponent<SpringJoint> ();
            //}

            SetSpring (lastlastprevPointSpringJoint, endRB);

            end.transform.position = lastprevPoint.position;
            end.transform.rotation = lastprevPoint.rotation;

            GKC_Utils.checkUnpackPrefabToDestroyObject (gameObject, lastprevPoint.gameObject);
            GKC_Utils.checkUnpackPrefabToDestroyObject (gameObject, lastprevCon.gameObject);

            numberOfPoints--;
        }


        private Vector3 CountConPos (Vector3 start, Vector3 end) => (start + end) / 2f;
        private Vector3 CountSizeOfCon (Vector3 start, Vector3 end) => new Vector3 (size, size, (start - end).magnitude / 2f);
        private Quaternion CountRoationOfCon (Vector3 start, Vector3 end) => Quaternion.LookRotation (end - start, Vector3.right);
        private string ConnectorName (int index) => $"{cloneText}_{index}_Conn";
        private string PointName (int index) => $"{cloneText}_{index}_Point";
        private Transform GetConnector (int index) => index > 0 ? transform.Find (ConnectorName (index)) : connector0.transform;
        private Transform GetPoint (int index) => index > 0 ? transform.Find (PointName (index)) : point0.transform;


        public void SetSpring (SpringJoint spring, Rigidbody connectedBody)
        {
            spring.connectedBody = connectedBody;
            spring.spring = springForce;
            spring.damper = damperOnCablePieces;
            spring.autoConfigureConnectedAnchor = false;
            spring.anchor = Vector3.zero;
            spring.connectedAnchor = Vector3.zero;
            spring.minDistance = space;
            spring.maxDistance = space;
        }
        private GameObject CreateNewPoint (int index)
        {
            GameObject temp = Instantiate (point0);

            temp.name = PointName (index);

            temp.transform.parent = transform;

            return temp;
        }

        private GameObject CreateNewCon (int index)
        {
            GameObject temp = Instantiate (connector0);

            temp.name = ConnectorName (index);

            temp.transform.parent = transform;

            return temp;
        }

        public void breakRandomPoint ()
        {
            bool randomPointFound = false;

            int counter = 0;

            int randomPointIndex = -1;

            while (!randomPointFound) {
                randomPointIndex = Random.Range (1, numberOfPoints - 1);

                if (!cableBroken) {
                    randomPointFound = true;
                }

                if (!brokenPointsIndex.Contains (randomPointIndex)) {
                    randomPointFound = true;
                }

                counter++;

                if (counter > 100) {
                    randomPointFound = true;

                    randomPointIndex = -1;
                }
            }

            if (randomPointIndex > -1) {
                SpringJoint currentSpringJoint = points [randomPointIndex].GetComponent<SpringJoint> ();

                if (currentSpringJoint != null) {
                    Destroy (currentSpringJoint);
                }

                brokenPointsIndex.Add (randomPointIndex);

                cableBroken = true;

                checkEventOnBreak ();
            }
        }

        public void breakPoint (Transform pointTransform)
        {
            if (canBeBrokenExternallyEnabled) {
                int currentIndex = points.IndexOf (pointTransform);

                if (currentIndex > -1) {
                    SpringJoint currentSpringJoint = points [currentIndex].GetComponent<SpringJoint> ();

                    if (currentSpringJoint != null) {
                        Destroy (currentSpringJoint);
                    }

                    brokenPointsIndex.Add (currentIndex);

                    cableBroken = true;

                    checkEventOnBreak ();
                }
            }
        }

        public void breakAllPoints ()
        {
            if (canBeBrokenExternallyEnabled) {
                int pointsCount = points.Count;

                for (int i = 0; i < pointsCount; i++) {
                    breakPoint (points [i]);
                }
            }
        }

        public void checkEventToDisconnectBothPoints ()
        {
            if (useEventToDisconnectBothPoints) {
                eventToDisconnectBothPoints.Invoke ();
            }
        }

        public void checkEventOnBreak ()
        {
            eventOnBreak.Invoke ();
        }

        public void updateComponent ()
        {
            GKC_Utils.updateComponent (this);

            GKC_Utils.updateDirtyScene ("Update Info Physics Cable" + gameObject.name, gameObject);
        }
    }
}