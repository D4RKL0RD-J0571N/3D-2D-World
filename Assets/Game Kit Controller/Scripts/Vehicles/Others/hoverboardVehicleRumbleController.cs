using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class hoverboardVehicleRumbleController : vehicleRumbleController
{
    [Header ("Hoverboard Settings")]
    [Space]

    public float maxSpeed = 120f;

    public float hoverLowBoost = 0.25f;
    public float hoverHighBoost = 0.4f;

    public float hoverInstabilityBoost = 0.3f;

    [Space]
    [Header ("Debug")]
    [Space]

    public float throttleInput;

    public float hoverHeightInput;

    public float hoverInstabilityInput;

    [Space]
    [Header ("Components")]
    [Space]

    public vehicleController mainVehicleController;

    protected override void updateVehicleStates ()
    {
        throttleInput =
            Mathf.Clamp01 (
                mainVehicleController
                    .getMotorInput ()
            );

        float speed01 =
            rb.linearVelocity.magnitude / maxSpeed;

        hoverHeightInput =
            mainVehicleController
                .getHoverHeight ();

        hoverInstabilityInput =
            mainVehicleController
                .getHoverInstability ();

        float low = 0f;
        float high = 0f;

        if (mainVehicleController.isTurnedOn) {
            low += throttleInput * 0.15f;

            high += throttleInput * 0.05f;
        }

        low +=
            hoverHeightInput *
            hoverLowBoost;

        high +=
            hoverHeightInput *
            hoverHighBoost;

        high +=
            hoverInstabilityInput *
            hoverInstabilityBoost *
            speed01;

        setCurrentRumble (
            low,
            high
        );
    }
}
