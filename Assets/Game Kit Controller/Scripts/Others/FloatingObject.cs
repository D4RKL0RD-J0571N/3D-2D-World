using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class FloatingObject : objectOnWater
{
    [Header ("Main Settings")]
    [Space]

    [SerializeField]
    private bool calculateDensity = false;

    public float density = 0.75f;

    [SerializeField]
    private float maxDensity = 0;

    [SerializeField]
    private float minDensity = 0;

    [SerializeField]
    [Range (0f, 1f)]
    private float normalizedVoxelSize = 0.5f;

    [SerializeField]
    private float dragInWater = 1f;

    [SerializeField]
    private float angularDragInWater = 1f;

    public bool updateBoundsValuesFromCollider = true;

    public Bounds bounds;

    [Space]
    [Header ("Gravity Settings")]
    [Space]

    public bool updateGravityFroceFromWaterVolume = true;

    public Vector3 gravityForce = new Vector3 (0, -9.8f, 0);

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showGizmo;

    public bool objectOnWaterActive;

    public bool externalForcesActive;

    public Vector3 externalForcesValue;

    public bool externalRotationForceActive;

    public float externalRotationForce;

    public Vector3 externalRotationForcePoint;

    public Vector3 currentRotationAxis;

    public bool initialValuesAssigned;

    public bool applyBuoyancyActive = true;

    public Vector3 maxBuoyancyForce;

    [Space]

    public waterSurfaceSystem mainWaterSurfaceSystem;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventsOnEnterExitWater;
    public UnityEvent eventonEnterWater;
    public UnityEvent eventOnExitWater;

    [Space]
    [Header ("Components")]
    [Space]

    public Collider mainCollider;
    public Rigidbody mainRigidbody;

    public MeshFilter mainMeshFilter;

    [HideInInspector] public Transform mainWaterSurfaceSystemTransform;


    private float initialDrag;
    private float initialAngularDrag;
    private Vector3 voxelSize;
    private Vector3 [] voxels;

    int voxelsLength;

    Coroutine updateCoroutine;

    float originalDensity;


    public void stopUpdateCoroutine ()
    {
        if (updateCoroutine != null) {
            StopCoroutine (updateCoroutine);
        }
    }

    IEnumerator updateSystemCoroutine ()
    {
        var waitTime = new WaitForFixedUpdate ();

        while (true) {
            updateSystem ();

            yield return waitTime;
        }
    }

    void updateSystem ()
    {
        if (objectOnWaterActive) {
            if (applyBuoyancyActive && voxelsLength > 0) {
                updateMaxBuoyancyForce ();

                Vector3 forceAtSingleVoxel = maxBuoyancyForce / voxelsLength;

                float voxelHeight = bounds.size.y * normalizedVoxelSize;

                float submergedVolume = 0f;

                Vector3 upDirection = mainWaterSurfaceSystemTransform.up;

                for (int i = 0; i < voxelsLength; i++) {
                    Vector3 worldPoint = transform.TransformPoint (voxels [i]);

                    float waterLevel = mainWaterSurfaceSystem.GetWaterLevel (worldPoint);

                    float deepLevel = waterLevel - worldPoint.y + (voxelHeight / 2f); // How deep is the voxel                    
                   
                    float submergedFactor = Mathf.Clamp (deepLevel / voxelHeight, 0f, 1f); // 0 - voxel is fully out of the water, 1 - voxel is fully submerged
                  
                    submergedVolume += submergedFactor;

                    Vector3 surfaceNormal = mainWaterSurfaceSystem.GetSurfaceNormal (worldPoint);
                   
                    Quaternion surfaceRotation = Quaternion.FromToRotation (upDirection, surfaceNormal);
                    
                    surfaceRotation = Quaternion.Slerp (surfaceRotation, Quaternion.identity, submergedFactor);

                    Vector3 finalVoxelForce = surfaceRotation * (submergedFactor * forceAtSingleVoxel);
                   
                    mainRigidbody.AddForceAtPosition (finalVoxelForce, worldPoint);

                    if (showGizmo) {
                        Debug.DrawLine (worldPoint, worldPoint + finalVoxelForce.normalized, Color.blue);
                    }
                }

                submergedVolume /= voxelsLength; // 0 - object is fully out of the water, 1 - object is fully submerged

                mainRigidbody.linearDamping = Mathf.Lerp (initialDrag, dragInWater, submergedVolume);
                mainRigidbody.angularDamping = Mathf.Lerp (initialAngularDrag, angularDragInWater, submergedVolume);

                if (externalForcesActive) {
                    mainRigidbody.AddForce (externalForcesValue);

                    externalForcesActive = false;
                }

                if (externalRotationForceActive) {
                    if (currentRotationAxis.magnitude != 0) {
                        transform.RotateAround (externalRotationForcePoint, currentRotationAxis, externalRotationForce);
                    }

                    //					mainRigidbody.AddTorque (externalRotationForce * currentRotationAxis);

                    externalRotationForceActive = false;
                }
            }

            checkObjectStateOnWater ();
        }
    }

    public void setWaterVolumeState (waterSurfaceSystem newWaterSurfaceSystem, bool state)
    {
        objectOnWaterActive = state;

        if (state) {
            mainWaterSurfaceSystem = newWaterSurfaceSystem;

            if (!initialValuesAssigned) {
                initialDrag = mainRigidbody.linearDamping;

                initialAngularDrag = mainRigidbody.angularDamping;

                if (calculateDensity) {
                    if (mainMeshFilter == null) {
                        mainMeshFilter = GetComponent<MeshFilter> ();
                    }

                    float objectVolume = mainWaterSurfaceSystem.CalculateVolume_Mesh (mainMeshFilter.mesh, mainMeshFilter.transform);

                    setNewDensity (mainRigidbody.mass / objectVolume);
                }

                if (originalDensity == 0) {
                    originalDensity = density;
                }

                initialValuesAssigned = true;
            }

            if (updateGravityFroceFromWaterVolume) {
                gravityForce = newWaterSurfaceSystem.getGravityForce ();
            }

            if (updateBoundsValuesFromCollider) {
                bounds = mainCollider.bounds;
            }

            mainWaterSurfaceSystemTransform = mainWaterSurfaceSystem.transform;

            if (voxels == null) {
                voxels = CutIntoVoxels ();
            }

            voxelsLength = voxels.Length;

            updateMaxBuoyancyForce ();

            updateCoroutine = StartCoroutine (updateSystemCoroutine ());
        } else {
            stopUpdateCoroutine ();

            mainWaterSurfaceSystem = null;
        }

        checkObjectStateOnWaterEnterOrExit (state);

        if (useEventsOnEnterExitWater) {
            if (state) {
                eventonEnterWater.Invoke ();
            } else {
                eventOnExitWater.Invoke ();
            }
        }
    }

    public void setNewGravityForce (Vector3 newValues)
    {
        gravityForce = newValues;
    }

    public override bool isObjectOnWaterActive ()
    {
        return objectOnWaterActive;
    }

    public override void updateExternalForces (Vector3 newValues, bool externalForcesActiveValue)
    {
        externalForcesActive = externalForcesActiveValue;

        externalForcesValue = newValues;
    }

    public override void updateExternalRotationForces (float rotationAmount, Vector3 rotationAxis,
                                                       Vector3 externalRotationForcePointValue)
    {
        externalRotationForceActive = true;

        externalRotationForce = rotationAmount;

        externalRotationForcePoint = externalRotationForcePointValue;

        currentRotationAxis = rotationAxis;
    }

    public override void setNewDensity (float newValue)
    {
        density = newValue;

        if (maxDensity != 0) {
            density = Mathf.Clamp (density, minDensity, maxDensity);
        }
    }

    public override void addOrRemoveDensity (float newValue)
    {
        density += newValue;

        if (maxDensity != 0) {
            density = Mathf.Clamp (density, minDensity, maxDensity);
        }
    }

    public override float getDensity ()
    {
        return density;
    }

    public override void setOriginalDensity ()
    {
        setNewDensity (originalDensity);
    }

    private Vector3 CalculateMaxBuoyancyForce ()
    {
        float objectVolume = mainRigidbody.mass / density;

        return mainWaterSurfaceSystem.getDensity () * objectVolume * -gravityForce;
    }

    public void updateMaxBuoyancyForce ()
    {
        maxBuoyancyForce = CalculateMaxBuoyancyForce ();
    }

    private Vector3 [] CutIntoVoxels ()
    {
        Quaternion initialRotation = transform.rotation;
        transform.rotation = Quaternion.identity;

        Bounds bounds = mainCollider.bounds;
        voxelSize.x = bounds.size.x * normalizedVoxelSize;
        voxelSize.y = bounds.size.y * normalizedVoxelSize;
        voxelSize.z = bounds.size.z * normalizedVoxelSize;

        int voxelsCountForEachAxis = Mathf.RoundToInt (1f / normalizedVoxelSize);
        List<Vector3> voxels = new List<Vector3> (voxelsCountForEachAxis * voxelsCountForEachAxis * voxelsCountForEachAxis);

        for (int i = 0; i < voxelsCountForEachAxis; i++) {
            for (int j = 0; j < voxelsCountForEachAxis; j++) {
                for (int k = 0; k < voxelsCountForEachAxis; k++) {
                    float pX = bounds.min.x + voxelSize.x * (0.5f + i);
                    float pY = bounds.min.y + voxelSize.y * (0.5f + j);
                    float pZ = bounds.min.z + voxelSize.z * (0.5f + k);

                    Vector3 point = new Vector3 (pX, pY, pZ);

                    if (mainWaterSurfaceSystem.IsPointInsideCollider (point, mainCollider, ref bounds)) {
                        voxels.Add (transform.InverseTransformPoint (point));
                    }
                }
            }
        }

        transform.rotation = initialRotation;

        return voxels.ToArray ();
    }

    public void setApplyBuoyancyActiveState (bool state)
    {
        applyBuoyancyActive = state;
    }

    public virtual void checkObjectStateOnWater ()
    {

    }

    public virtual void checkObjectStateOnWaterEnterOrExit (bool state)
    {

    }

    public Collider getMainCollider ()
    {
        return mainCollider;
    }

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
            if (voxelsLength > 0) {
                for (int i = 0; i < voxelsLength; i++) {
                    Gizmos.color = Color.magenta - new Color (0f, 0f, 0f, 0.75f);
                    Gizmos.DrawCube (transform.TransformPoint (voxels [i]), 0.8f * voxelSize);
                }
            }
        }
    }
}