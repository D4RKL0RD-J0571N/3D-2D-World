using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class vehicleLaser : laser
{
    [Space]
    [Header ("Main Settings")]
    [Space]

    public float laserDamage = 0.3f;
    public float laserDamageRate = 0.5f;
    public bool ignoreShield;

    public int damageTypeID = -1;

    public bool damageCanBeBlocked = true;

    public bool usingCustomRayPosition;

    [Space]
    [Header ("Components")]
    [Space]

    public LayerMask layer;
    public GameObject hitParticles;
    public GameObject hitSparks;
    public GameObject vehicle;
    public GameObject vehicleCamera;

    [Space]

    public Transform mainCameraTransform;

    public vehicleCameraController vehicleCameraManager;
    public vehicleHUDManager vehicleHUD;

    public GameObject currentDriver;


    RaycastHit hit;
    bool laserActive;

    Vector3 laserPosition;

    float lastTimeDamageActive;

    Vector3 customRayPosition;

    Vector3 customRayDirection;

    bool vehicleCameraManagerLocated;




    void Start ()
    {
        changeLaserState (false);
    }

    void Update ()
    {
        if (laserActive) {
            //check the hit collider of the raycast
            if (vehicleCameraManagerLocated) {
                mainCameraTransform = vehicleCameraManager.getCurrentCameraTransform ();
            }

            Vector3 raycastPosition = mainCameraTransform.position;
            Vector3 raycastDirection = mainCameraTransform.forward;

            if (usingCustomRayPosition) {
                raycastPosition = customRayPosition - customRayDirection * 0.2f;
                raycastDirection = customRayDirection;
            }

            if (Physics.Raycast (raycastPosition, raycastDirection, out hit, Mathf.Infinity, layer)) {
                //				Debug.DrawRay (transform.position, hit.distance * raycastDirection, Color.red);

                Vector3 surfacePoint = hit.point;

                //				if (usingCustomRayPosition) {
                //					surfacePoint = customRayPosition;
                //				} 

                transform.LookAt (surfacePoint);

                if (Time.time > laserDamageRate + lastTimeDamageActive) {
                    applyDamage.checkHealth (gameObject, hit.collider.gameObject, laserDamage, -transform.forward, (surfacePoint - (hit.normal / 4)),
                        currentDriver, true, true, ignoreShield, false, damageCanBeBlocked, false, -1, damageTypeID);

                    lastTimeDamageActive = Time.time;
                }

                //set the sparks and .he smoke in the hit point
                laserDistance = hit.distance;

                if (!hitSparks.activeSelf) {
                    hitSparks.SetActive (true);
                }

                if (!hitParticles.activeSelf) {
                    hitParticles.SetActive (true);
                }

                hitParticles.transform.position = surfacePoint + 0.02f * (transform.position - surfacePoint);
                hitParticles.transform.rotation = Quaternion.identity;
                hitSparks.transform.rotation = Quaternion.LookRotation (hit.normal, transform.up);
                laserPosition = surfacePoint;
            } else {
                //if the laser does not hit anything, disable the particles and set the hit point
                if (hitSparks.activeSelf) {
                    hitSparks.SetActive (false);
                }

                if (hitParticles.activeSelf) {
                    hitParticles.SetActive (false);
                }

                laserDistance = 1000;

                Quaternion lookDir = Quaternion.LookRotation (raycastDirection);

                transform.rotation = lookDir;
                laserPosition = (laserDistance * transform.forward);
            }

            usingCustomRayPosition = false;

            //set the size of the laser, according to the hit position
            lRenderer.positionCount = 2;
            lRenderer.SetPosition (0, transform.position);
            lRenderer.SetPosition (1, laserPosition);

            animateLaser ();
        }
    }

    //enable or disable the vehicle laser
    public void changeLaserState (bool state)
    {
        lRenderer.enabled = state;

        laserActive = state;

        if (state) {
            StartCoroutine (laserAnimation ());

            lastTimeDamageActive = 0;
        } else {
            hitSparks.SetActive (false);
            hitParticles.SetActive (false);
        }

        vehicleCameraManagerLocated = vehicleCameraManager != null;

        if (state) {
            if (vehicleHUD != null) {
                currentDriver = vehicleHUD.getCurrentDriver ();
            }
        }
    }

    public void updateCustomRayPosition (Vector3 newPosition, Vector3 newDirection)
    {
        usingCustomRayPosition = true;

        customRayPosition = newPosition;

        customRayDirection = newDirection;
    }
}