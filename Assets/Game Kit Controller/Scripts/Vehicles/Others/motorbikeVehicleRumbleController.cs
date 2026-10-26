using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class motorbikeVehicleRumbleController : carVehicleRumbleController
{
    [Space]
    [Header ("Motorbike Settings")]
    [Space]

    [Range (0f, 1f)]
    public float handlebarVibrationMultiplier = 1f;

    [Space]

    public float rpmLowBoost = 0.05f;
    public float rpmHighBoost = 0.25f;

    public float rpmMultiplier = 0.2f;

    [Space]

    public float leanLowBoost = 0.05f;
    public float leanHighBoost = 0.2f;

    [Space]

    public float suspensionLowBoost = 0.35f;
    public float suspensionHighBoost = 0.08f;

    public float suspensioMultiplier = 0.2f;

    [Space]

    public float steeringShakeLowBoost = 0.02f;
    public float steeringShakeHighBoost = 0.35f;

    [Space]
    [Header ("Debug")]
    [Space]

    public float engineRPMInput;

    public float leanInput;

    public float suspensionInput;

    public float steeringShakeInput;


    protected override void updateVehicleStates ()
    {
        base.updateVehicleStates ();

        float speed01 =
            rb.linearVelocity.magnitude / maxSpeed;

        engineRPMInput = mainVehicleController.getCurrentRPM () * rpmMultiplier;

        leanInput = mainVehicleController.getCurrentLean ();

        suspensionInput = mainVehicleController.getCurrentSuspension () * suspensioMultiplier;

        steeringShakeInput = mainVehicleController.getCurrentSteeringShake ();

        if (mainVehicleController.isTurnedOn) {
            currentLow +=
            engineRPMInput * rpmLowBoost;

            currentHigh +=
                engineRPMInput * rpmHighBoost;
        }

        currentLow +=
            leanInput *
            leanLowBoost *
            speed01;

        currentHigh +=
            leanInput *
            leanHighBoost *
            speed01;

        currentLow +=
            suspensionInput *
            suspensionLowBoost;

        currentHigh +=
            suspensionInput *
            suspensionHighBoost;

        currentLow +=
            steeringShakeInput *
            steeringShakeLowBoost;

        currentHigh +=
            steeringShakeInput *
            steeringShakeHighBoost;

        currentLow *=
            handlebarVibrationMultiplier;

        currentHigh *=
            handlebarVibrationMultiplier;
    }
}
