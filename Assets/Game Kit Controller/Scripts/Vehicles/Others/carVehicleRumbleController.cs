using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class carVehicleRumbleController : vehicleRumbleController
{
    [Header ("Car Settings")]
    [Space]

    public float maxSpeed = 200f;

    [Space]
    [Header ("Engine Feel")]
    [Space]

    public float idleLow = 0.03f;
    public float idleHigh = 0.01f;
    public float accelerationLow = 0.35f;
    public float accelerationHigh = 0.08f;

    [Space]
    [Header ("Throttle / Brake")]
    [Space]

    public float throttleLowBoost = 0.15f;
    public float throttleHighBoost = 0.05f;
    public float brakeLowBoost = 0.1f;
    public float brakeHighBoost = 0.2f;

    public float reverseLow = 0.05f;
    public float reverseHigh = 0.03f;

    [Space]
    [Header ("Drift")]
    [Space]

    public float driftLow = 0.25f;
    public float driftHigh = 0.6f;

    [Space]
    [Header ("Offroad")]
    [Space]

    public float offroadLow = 0.2f;
    public float offroadHigh = 0.35f;

    [Space]
    [Header ("Boost")]
    [Space]

    public float boostLowBoost = 0.35f;
    public float boostHighBoost = 0.6f;

    public float boostSmoothing = 8f;

    [Space]
    [Header ("Debug")]
    [Space]

    public float throttleInput;
    public float reverseInput;
    public float brakeInput;

    public bool isBraking;
    public bool isDrifting;
    public bool isBoosting;
    public float boostInput;

    public float driftAmount;

    [Space]
    [Header ("Components")]
    [Space]

    public vehicleController mainVehicleController;

    protected override void updateVehicleStates ()
    {
        float motorInput =
            mainVehicleController.getMotorInput ();

        throttleInput =
            Mathf.Clamp01 (motorInput);

        reverseInput =
            Mathf.Clamp01 (-motorInput);

        isBraking =
            mainVehicleController.isBraking ();

        brakeInput = isBraking
            ? Mathf.Lerp (
                brakeInput,
                1f,
                Time.deltaTime * 12f)
            : Mathf.Lerp (
                brakeInput,
                0f,
                Time.deltaTime * 12f);

        isGrounded =
            mainVehicleController.isVehicleOnGround ();

        isDrifting =
            mainVehicleController.isDrifting ();

        isBoosting =
            mainVehicleController.isUsingBoosting ();

        driftAmount =
            mainVehicleController.getDriftingAmount ();

        float speed01 =
            rb.linearVelocity.magnitude / maxSpeed;

        float low = 0f;
        float high = 0f;

        if (mainVehicleController.isTurnedOn) {
            low += Mathf.Lerp (
                idleLow,
                accelerationLow,
                speed01
            );

            high += Mathf.Lerp (
                idleHigh,
                accelerationHigh,
                speed01
            );


            low += throttleInput * throttleLowBoost;
            high += throttleInput * throttleHighBoost;

            if (brakeInput > 0.01f) {

                low += brakeInput * brakeLowBoost;

                high += brakeInput * brakeHighBoost;
            }

            if (reverseInput > 0.01f) {

                low += reverseInput * reverseLow;

                high += reverseInput * reverseHigh;
            }
        }

        if (driftAmount > 0.01f) {

            low += driftAmount * driftLow;

            high += driftAmount * driftHigh;
        }

        float offroad =
            mainVehicleController.getOffroadAmount ();

        if (offroad > 0.01f) {

            low += offroad * offroadLow;

            high += offroad * offroadHigh;
        }

        // =================================================
        // BOOST LAYER (CONTROLLED BLENDING)
        // =================================================

        float targetBoost = isBoosting
                ? 1f
                : 0f;

        boostInput = Mathf.Lerp (
            boostInput,
            targetBoost,
            Time.deltaTime * boostSmoothing
        );

        float boostWeight = boostInput;

        float boostLow =
            boostInput * boostLowBoost;

        float boostHigh =
            boostInput * boostHighBoost;

        low += boostLow * boostWeight;
        high += boostHigh * boostWeight;

        setCurrentRumble (
            low,
            high
        );
    }
}