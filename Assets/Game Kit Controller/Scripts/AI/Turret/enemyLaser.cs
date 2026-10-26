using UnityEngine;
using System.Collections;

public class enemyLaser : laser
{
    [Space]
    [Header ("Main Settings")]
    [Space]

    public LayerMask layer;

    public float laserDamage = 0.2f;
    public bool ignoreShield;

    public int damageTypeID = -1;

    public bool damageCanBeBlocked = true;

    public bool useDamageRate;
    public float damageRate = 0.3f;

    [Space]
    [Header ("Other Settings")]
    [Space]

    public GameObject hitParticles;
    public GameObject hitSparks;

    public bool useCustomLaserPosition;
    public Transform customLaserPosition;

    public GameObject owner;

    RaycastHit hit;

    Transform currentLaserPosition;

    float lastTimeDamageActive;

    void Start ()
    {
        StartCoroutine (laserAnimation ());

        if (useCustomLaserPosition) {
            currentLaserPosition = customLaserPosition;
        } else {
            currentLaserPosition = transform;
        }
    }

    void Update ()
    {
        //check the hit collider of the raycast
        if (Physics.Raycast (currentLaserPosition.position, currentLaserPosition.forward, out hit, Mathf.Infinity, layer)) {
            bool canApplyDamageResult = true;

            if (useDamageRate) {
                if (lastTimeDamageActive > 0 && Time.time < damageRate + lastTimeDamageActive) {
                    canApplyDamageResult = false;
                }
            }

            if (canApplyDamageResult) {
                applyDamage.checkHealth (gameObject, hit.collider.gameObject, laserDamage, -currentLaserPosition.forward, (hit.point - (hit.normal / 4)),
                    owner, true, true, ignoreShield, false, damageCanBeBlocked, false, -1, damageTypeID);

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

            hitParticles.transform.position = hit.point + (currentLaserPosition.position - hit.point) * 0.02f;

            hitParticles.transform.rotation = Quaternion.identity;

            hitSparks.transform.rotation = Quaternion.LookRotation (hit.normal, currentLaserPosition.up);
        } else {
            //if the laser does not hit anything, disable the particles and set the hit point
            if (hitSparks.activeSelf) {
                hitSparks.SetActive (false);
            }

            if (hitParticles.activeSelf) {
                hitParticles.SetActive (false);
            }

            laserDistance = 1000;

            lastTimeDamageActive = Time.time;
        }

        //set the size of the laser, according to the hit position
        lRenderer.SetPosition (1, (laserDistance * Vector3.forward));

        animateLaser ();
    }

    public void setOwner (GameObject laserOwner)
    {
        owner = laserOwner;
    }
}