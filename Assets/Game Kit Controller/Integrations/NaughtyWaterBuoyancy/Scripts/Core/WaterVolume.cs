using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NaughtyWaterBuoyancy
{
    [RequireComponent (typeof (BoxCollider))]
    [RequireComponent (typeof (MeshFilter))]
    public class WaterVolume : waterSurfaceSystem
    {
        [Header ("Main Settings")]
        [Space]

        public float waterHeight = 5;

        public float density = 1f;

        public int rows = 10;

        public int columns = 10;

        public float quadSegmentSize = 1f;

        public bool useCustomGravityForce = true;

        public Vector3 gravityForce = new Vector3 (0, -9.8f, 0);

        [Space]
        [Header ("Object Detection Settings")]
        [Space]

        public LayerMask objectLayermask;

        public bool checkObjectByTag;
        public List<string> tagListToCheck = new List<string> ();

        public bool checkForFloatingObjectSocketEnabled = true;

        [Space]
        [Header ("Gizmo Settings")]
        [Space]

        public bool showDebugPrint;
        public bool showGizmo;


        private Mesh mesh;
        private Vector3 [] meshLocalVertices;
        private Vector3 [] meshWorldVertices;

        bool meshLocated;

        BoxCollider mainBoxCollider;

        public List<GameObject> objectsOnWaterList = new List<GameObject> ();

        public float Density
        {
            get
            {
                return density;
            }
        }

        public int Rows
        {
            get
            {
                return rows;
            }
        }

        public int Columns
        {
            get
            {
                return columns;
            }
        }

        public float QuadSegmentSize
        {
            get
            {
                return quadSegmentSize;
            }
        }

        public Mesh Mesh
        {
            get
            {
                if (!meshLocated) {
                    if (mesh == null) {
                        mesh = GetComponent<MeshFilter> ().mesh;
                    }

                    meshLocated = mesh != null;
                }

                return mesh;
            }
        }

        protected virtual void Awake ()
        {
            CacheMeshVertices ();
        }

        protected virtual void Update ()
        {
            CacheMeshVertices ();
        }

        public Vector3 [] GetSurroundingTrianglePolygon (Vector3 worldPoint)
        {
            Vector3 localPoint = transform.InverseTransformPoint (worldPoint);

            int x = Mathf.CeilToInt (localPoint.x / QuadSegmentSize);
            int z = Mathf.CeilToInt (localPoint.z / QuadSegmentSize);

            if (x <= 0 || z <= 0 || x >= (Columns + 1) || z >= (Rows + 1)) {
                return null;
            }

            Vector3 [] trianglePolygon = new Vector3 [3];

            int index1 = GetIndex (z, x);
            int index2 = GetIndex (z - 1, x - 1);

            int meshWorldVerticesLength = meshWorldVertices.Length;

            if (index1 < meshWorldVerticesLength && index2 < meshWorldVerticesLength) {
                if ((worldPoint - meshWorldVertices [index1]).sqrMagnitude <
                    ((worldPoint - meshWorldVertices [index2]).sqrMagnitude)) {
                    trianglePolygon [0] = meshWorldVertices [index1];
                } else {
                    trianglePolygon [0] = meshWorldVertices [index2];
                }
            }

            int index3 = GetIndex (z - 1, x);
            int index4 = GetIndex (z, x - 1);

            if (index3 < meshWorldVerticesLength && index4 < meshWorldVerticesLength) {
                trianglePolygon [1] = meshWorldVertices [index3];
                trianglePolygon [2] = meshWorldVertices [index4];
            }

            return trianglePolygon;
        }

        public Vector3 [] GetClosestPointsOnWaterSurface (Vector3 worldPoint, int pointsCount)
        {
            MinHeap<Vector3> allPoints = new MinHeap<Vector3> (new Vector3HorizontalDistanceComparer (worldPoint));

            int meshWorldVerticesLength = meshWorldVertices.Length;

            for (int i = 0; i < meshWorldVerticesLength; i++) {
                allPoints.Add (meshWorldVertices [i]);
            }

            Vector3 [] closestPoints = new Vector3 [pointsCount];

            int closestPointsLength = closestPoints.Length;

            for (int i = 0; i < closestPointsLength; i++) {
                closestPoints [i] = allPoints.Remove ();
            }

            return closestPoints;
        }

        public override Vector3 GetSurfaceNormal (Vector3 worldPoint)
        {
            Vector3 [] meshPolygon = GetSurroundingTrianglePolygon (worldPoint);

            if (meshPolygon != null) {
                Vector3 meshPolygon0 = meshPolygon [0];

                Vector3 planeV1 = meshPolygon [1] - meshPolygon0;
                Vector3 planeV2 = meshPolygon [2] - meshPolygon0;

                Vector3 planeNormal = Vector3.Cross (planeV1, planeV2).normalized;

                if (planeNormal.y < 0f) {
                    planeNormal = -1f * planeNormal;
                }

                return planeNormal;
            }

            return transform.up;
        }

        public override float GetWaterLevel (Vector3 worldPoint)
        {
            Vector3 [] meshPolygon = GetSurroundingTrianglePolygon (worldPoint);

            if (meshPolygon != null) {
                Vector3 meshPolygon0 = meshPolygon [0];

                Vector3 planeV1 = meshPolygon [1] - meshPolygon0;
                Vector3 planeV2 = meshPolygon [2] - meshPolygon0;

                Vector3 planeNormal = Vector3.Cross (planeV1, planeV2).normalized;

                if (planeNormal.y < 0f) {
                    planeNormal = -1f * planeNormal;
                }

                // Plane equation
                float yOnWaterSurface = (-(worldPoint.x * planeNormal.x) - (worldPoint.z * planeNormal.z)
                    + Vector3.Dot (meshPolygon0, planeNormal)) / planeNormal.y;

                //Vector3 pointOnWaterSurface = new Vector3(point.x, yOnWaterSurface, point.z);
                //DebugUtils.DrawPoint(pointOnWaterSurface, Color.magenta);

                return yOnWaterSurface;
            }

            return transform.position.y;
        }

        public bool IsPointUnderWater (Vector3 worldPoint)
        {
            return GetWaterLevel (worldPoint) - worldPoint.y > 0f;
        }

        private int GetIndex (int row, int column)
        {
            return row * (Columns + 1) + column;
        }

        private void CacheMeshVertices ()
        {
            meshLocalVertices = Mesh.vertices;

            meshWorldVertices = ConvertPointsToWorldSpace (meshLocalVertices);
        }

        private Vector3 [] ConvertPointsToWorldSpace (Vector3 [] localPoints)
        {
            int localPointsLength = localPoints.Length;

            Vector3 [] worldPoints = new Vector3 [localPointsLength];

            for (int i = 0; i < localPointsLength; i++) {
                worldPoints [i] = transform.TransformPoint (localPoints [i]);
            }

            return worldPoints;
        }

        private class Vector3HorizontalDistanceComparer : IComparer<Vector3>
        {
            private Vector3 distanceToVector;

            public Vector3HorizontalDistanceComparer (Vector3 distanceTo)
            {
                distanceToVector = distanceTo;
            }

            public int Compare (Vector3 v1, Vector3 v2)
            {
                v1.y = 0;
                v2.y = 0;

                float v1Distance = (v1 - distanceToVector).sqrMagnitude;
                float v2Distance = (v2 - distanceToVector).sqrMagnitude;

                if (v1Distance < v2Distance) {
                    return -1;
                } else if (v1Distance > v2Distance) {
                    return 1;
                } else {
                    return 0;
                }
            }
        }

        public override Vector3 getGravityForce ()
        {
            if (useCustomGravityForce) {
                return gravityForce;
            } else {
                return Physics.gravity;
            }
        }

        public override float getDensity ()
        {
            return density;
        }

        public void setNewGravityForce (Vector3 newValues)
        {
            gravityForce = newValues;

            for (int i = 0; i < objectsOnWaterList.Count; i++) {
                FloatingObject currentFloatingObject = objectsOnWaterList [i].GetComponent<FloatingObject> ();

                if (currentFloatingObject != null) {
                    currentFloatingObject.setNewGravityForce (gravityForce);
                }
            }
        }

        public void setUseCustomGravityForceState (bool state)
        {
            useCustomGravityForce = state;
        }

        void OnTriggerEnter (Collider col)
        {
            checkTriggerInfo (col, true);
        }

        void OnTriggerExit (Collider col)
        {
            checkTriggerInfo (col, false);
        }

        public void checkTriggerInfo (Collider col, bool isEnter)
        {
            bool checkResult = true;

            if ((1 << col.gameObject.layer & objectLayermask.value) != 1 << col.gameObject.layer) {
                checkResult = false;
            }

            if (!checkResult) {
                if (checkObjectByTag) {
                    if (tagListToCheck.Contains (col.gameObject.tag)) {
                        checkResult = true;
                    } else {
                        checkResult = false;
                    }
                }
            }

            if (!checkResult) {
                if (checkForFloatingObjectSocketEnabled) {
                    FloatingObjectSocket currentFloatingObjectSocket = col.gameObject.GetComponent<FloatingObjectSocket> ();

                    if (currentFloatingObjectSocket != null) {
                        checkResult = true;
                    }
                }
            }

            if (!checkResult) {
                return;
            }

            FloatingObject currentFloatingObject = col.gameObject.GetComponent<FloatingObject> ();

            if (currentFloatingObject == null) {
                FloatingObjectSocket currentFloatingObjectSocket = col.gameObject.GetComponent<FloatingObjectSocket> ();

                if (currentFloatingObjectSocket != null) {
                    currentFloatingObject = currentFloatingObjectSocket.getFloatingObject ();
                }
            }

            if (currentFloatingObject != null) {
                if (showDebugPrint) {
                    print ("setting floating object state " + currentFloatingObject.name + " " + isEnter);
                }

                currentFloatingObject.setWaterVolumeState (this, isEnter);

                if (isEnter) {
                    if (!objectsOnWaterList.Contains (currentFloatingObject.gameObject)) {
                        objectsOnWaterList.Add (currentFloatingObject.gameObject);
                    }
                } else {
                    if (objectsOnWaterList.Contains (currentFloatingObject.gameObject)) {
                        objectsOnWaterList.Remove (currentFloatingObject.gameObject);
                    }
                }

                for (int i = objectsOnWaterList.Count - 1; i >= 0; i--) {
                    if (objectsOnWaterList [i] == null) {
                        objectsOnWaterList.RemoveAt (i);
                    }
                }
            }
        }

        public override float CalculateVolume_Mesh (Mesh mesh, Transform trans)
        {
            return MathfUtils.CalculateVolume_Mesh (mesh, trans);
        }

        public override bool IsPointInsideCollider (Vector3 point, Collider collider, ref Bounds colliderBounds)
        {
            return ColliderUtils.IsPointInsideCollider (point, collider, ref colliderBounds);
        }

        //Draw gizmos
#if UNITY_EDITOR
        void OnDrawGizmos ()
        {
            if (!showGizmo) {
                return;
            }

            if (GKC_Utils.isCurrentSelectionActiveGameObject (gameObject)) {
                DrawGizmos ();
            }
        }

        void OnDrawGizmosSelected ()
        {
            DrawGizmos ();
        }

        void DrawGizmos ()
        {
            if (showGizmo) {
                if (!Application.isPlaying) {
                    Gizmos.color = Color.green;
                    Gizmos.matrix = transform.localToWorldMatrix;

                    if (mainBoxCollider == null) {
                        mainBoxCollider = GetComponent<BoxCollider> ();
                    }

                    if (mainBoxCollider != null) {
                        Gizmos.DrawWireCube (mainBoxCollider.center, mainBoxCollider.size);

                        Gizmos.color = Color.cyan - new Color (0f, 0f, 0f, 0.75f);
                        Gizmos.matrix = transform.localToWorldMatrix;

                        Gizmos.DrawCube (mainBoxCollider.center - 0.01f * Vector3.up, mainBoxCollider.size);

                        Gizmos.color = Color.cyan - new Color (0f, 0f, 0f, 0.5f);
                        Gizmos.DrawWireCube (mainBoxCollider.center, mainBoxCollider.size);

                        Gizmos.matrix = Matrix4x4.identity;
                    }
                }
            }
        }
#endif
    }
}