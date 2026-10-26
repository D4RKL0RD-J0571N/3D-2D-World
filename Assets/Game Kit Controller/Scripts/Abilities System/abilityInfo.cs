using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class abilityInfo : MonoBehaviour
{
    [Header ("Ability Settings")]
    [Space]

    public string Name;

    public bool abilityEnabled;

    [Space]

    public Texture abilityTexture;
    public Sprite abilitySprite;

    [Space]

    public bool addAbilityToUIWheelActive = true;

    public bool updateAbilityActive;

    public bool deactivateAbilityWhenChangeToAnother;

    public bool disableAbilityInputInUseStateOnPressDown;

    public bool canBeUsedOnlyOnGround;

    [Space]

    public bool showAbilityDescription;
    [TextArea (3, 10)] public string abilityDescription;

    [Space]
    [Header ("Ability UI Settings")]
    [Space]

    public bool abilityVisibleOnWheelSelection = true;

    public bool abilityCanBeShownOnWheelSelection = true;

    [Space]
    [Header ("Ability Limit/Duration Settings")]
    [Space]

    public bool useTimeLimit;
    public float timeLimit;
    public bool useLimitWhenAbilityCurrentActiveFromPress;
    public bool callInputToDeactivateAbilityOnTimeLimit;

    [Space]

    public UnityEvent eventOnTimeLimitBefore;
    public UnityEvent eventOnTimeLimitAfter;

    [Space]

    public bool useTimeLimitOnPressDown;
    public bool useTimeLimitOnPressUp;

    public bool avoidAbilityInputWhileLimitActive;

    public bool resetAbilityCurrentlyActiveFromPressStateOnTimeLimit;

    public bool avoidToUseOtherAbilitiesWhileLimitActive;

    [Space]
    [Header ("Ability Cool Down Settings")]
    [Space]

    public bool useCoolDown;
    public float coolDownDuration;
    public bool useCoolDownWhenAbilityCurrentlyActiveFromPress;
    public UnityEvent eventOnCoolDownActivate;
    public bool useCoolDownOnPressDown;
    public bool useCoolDownOnPressUp;

    public bool activateCoolDownAfterTimeLimit;

    [HideInInspector] public float lastTimeActive;

    public float coolDownMultiplier = 1;

    [Space]
    [Header ("Ability Input Settings")]
    [Space]

    public bool useInputOnPressDown;
    public bool useInputOnPressHold;
    public bool useInputOnPressUp;

    public bool checkIfInputPressDownBeforeActivateUp;

    public bool canUsePressDownInputIfPlayerBusy;

    public bool canStopAbilityIfActionActive;

    [Space]

    public bool ignoreSetAbilityCurrentlyActiveFromPressDown;

    [Space]
    [Header ("Ability Energy Settings")]
    [Space]

    public bool useEnergyOnAbility;
    public bool useEnergyWithRate;
    public float energyAmountUsed;
    public bool useEnergyOnPressDown;
    public bool useEnergyOnPressHold;
    public bool useEnergyOnPressUp;

    public bool useEnergyOnceOnPressDown;
    public bool useEnergyOnceOnPressUp;

    [Space]
    [Header ("Animation Settings")]
    [Space]

    public bool useAnimationWithActionSystem;
    public string animationActionSystemName;
    public bool useAnimationOnPressDown;
    public bool useAnimationOnPressUp;

    [Space]
    [Header ("Ability Components")]
    [Space]

    public playerAbilitiesSystem mainPlayerAbilitiesSystem;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventsOnSetCurrentAbility;
    public UnityEvent eventOnSetCurrentAbility;

    [Space]

    public bool useEventOnUseAbility;
    public UnityEvent eventOnUseAbility;

    [Space]

    public UnityEvent eventToDeactivateAbility;

    [Space]

    public bool useConditionCheckToActivateAbilityOnPressDown;
    public UnityEvent eventToCallToCheckConditionOnPressDown;

    [Space]

    public bool useConditionCheckToActivateAbilityOnPressUp;
    public UnityEvent eventToCallToCheckConditionOnPressUp;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool abilityCurrentlyActiveFromPressDown;
    public bool abilityCurrentlyActiveFromPressUp;

    public bool isCurrentAbility;

    public bool coolDownInProcess;

    public bool timeLimitInProcess;

    public float currentCoolDownDuration;

    public bool updateAbilityInProcess;



    Coroutine updateAbilityCoroutine;

    Coroutine coolDownCoroutine;
    Coroutine timeLimitCoroutine;


    public void disableAbilityCurrentActiveFromPressState ()
    {
        abilityCurrentlyActiveFromPressDown = false;
        abilityCurrentlyActiveFromPressUp = false;
    }

    public void enableOrDisableAbilityExternally (bool state)
    {
        if (state) {
            enableAbilityExternally ();
        } else {
            disableAbilityExternally ();
        }
    }

    public void setAbilityEnabledState (bool state)
    {
        abilityEnabled = state;
    }

    public bool isAbilityEnabled ()
    {
        return abilityEnabled;
    }

    public void enableAbilityExternally ()
    {
        setAbilityEnabledState (true);

        enableAbility ();
    }

    public void disableAbilityExternally ()
    {
        setAbilityEnabledState (false);

        disableAbility ();

        isCurrentAbility = false;
        stopActivateUpdateAbility ();

        disableAbilityCurrentActiveFromPressState ();
    }

    public void setAbilityVisibleOnWheelSelectionState (bool state)
    {
        abilityVisibleOnWheelSelection = state;
    }

    public void setAbilityCanBeShownOnWheelSelectionState (bool state)
    {
        abilityCanBeShownOnWheelSelection = state;
    }

    public virtual void updateAbilityState ()
    {

    }

    public virtual void enableAbility ()
    {

    }

    public virtual void disableAbility ()
    {

    }

    public virtual void deactivateAbility ()
    {

    }

    public virtual void activateSecondaryActionOnAbility ()
    {

    }

    public void checkIfUseAnimationWithActionSystemOnPressDown ()
    {
        if (useAnimationWithActionSystem) {
            if (useAnimationOnPressDown) {
                mainPlayerAbilitiesSystem.activateCustomAction (animationActionSystemName);
            }
        }
    }

    public void checkIfUseAnimationWithActionSystemOnPressUp ()
    {
        if (useAnimationWithActionSystem) {
            if (useAnimationOnPressUp) {
                mainPlayerAbilitiesSystem.activateCustomAction (animationActionSystemName);
            }
        }
    }

    public void checkEventOnSetCurrentAbility ()
    {
        if (useEventsOnSetCurrentAbility) {
            eventOnSetCurrentAbility.Invoke ();
        }
    }

    public void checkUseEventOnUseAbility ()
    {
        if (useEventOnUseAbility) {
            eventOnUseAbility.Invoke ();
        }
    }

    public void checkEventToDeactivateAbility ()
    {
        eventToDeactivateAbility.Invoke ();
    }

    public virtual void useAbilityPressDown ()
    {

    }

    public virtual void useAbilityPressHold ()
    {

    }

    public virtual void useAbilityPressUp ()
    {

    }

    public void activateCoolDown ()
    {
        if (!useCoolDown) {
            return;
        }

        stopActivateCoolDown ();

        stopActivateTimeLimit ();

        coolDownCoroutine = StartCoroutine (activateCoolDownCoroutine ());
    }

    public void stopActivateCoolDown ()
    {
        if (coolDownCoroutine != null) {
            StopCoroutine (coolDownCoroutine);
        }
    }

    IEnumerator activateCoolDownCoroutine ()
    {
        eventOnCoolDownActivate.Invoke ();

        coolDownInProcess = true;

        //		WaitForSeconds delay = new WaitForSeconds (coolDownDuration);
        //
        //		yield return delay;

        bool coolDownFinished = false;

        currentCoolDownDuration = coolDownDuration;

        while (!coolDownFinished) {

            currentCoolDownDuration -= Time.deltaTime * coolDownMultiplier;

            if (currentCoolDownDuration <= 0) {
                coolDownFinished = true;
            }

            yield return null;
        }

        coolDownInProcess = false;
    }

    public void activateTimeLimit ()
    {
        if (!useTimeLimit) {
            return;
        }

        stopActivateTimeLimit ();

        timeLimitCoroutine = StartCoroutine (activateTimeLimitCoroutine ());
    }

    public void stopActivateTimeLimit ()
    {
        if (timeLimitCoroutine != null) {
            StopCoroutine (timeLimitCoroutine);
        }

        timeLimitInProcess = false;
    }

    IEnumerator activateTimeLimitCoroutine ()
    {
        eventOnTimeLimitBefore.Invoke ();

        timeLimitInProcess = true;

        WaitForSeconds delay = new WaitForSeconds (timeLimit);

        yield return delay;

        timeLimitInProcess = false;

        eventOnTimeLimitAfter.Invoke ();

        if (callInputToDeactivateAbilityOnTimeLimit) {
            if (abilityCurrentlyActiveFromPressDown) {
                abilityCurrentlyActiveFromPressDown = false;

                useAbilityPressDown ();
            }

            if (abilityCurrentlyActiveFromPressUp) {
                abilityCurrentlyActiveFromPressUp = false;

                useAbilityPressUp ();
            }
        }

        if (resetAbilityCurrentlyActiveFromPressStateOnTimeLimit) {
            disableAbilityCurrentActiveFromPressState ();
        }

        if (activateCoolDownAfterTimeLimit) {
            activateCoolDown ();
        }
    }

    public void activateUpdateAbility ()
    {
        if (!updateAbilityActive) {
            return;
        }

        stopActivateUpdateAbility ();

        updateAbilityCoroutine = StartCoroutine (activateUpdateAbilityCoroutine ());

        updateAbilityInProcess = true;
    }

    public void stopActivateUpdateAbility ()
    {
        if (updateAbilityCoroutine != null) {
            StopCoroutine (updateAbilityCoroutine);
        }

        updateAbilityInProcess = false;
    }

    IEnumerator activateUpdateAbilityCoroutine ()
    {
        var waitTime = new WaitForFixedUpdate ();

        while (true) {
            yield return waitTime;

            updateAbilityState ();
        }
    }

    public void setCoolDownDurationValue (float newValue)
    {
        coolDownDuration = newValue;
    }

    public float getCoolDownDurationValue ()
    {
        return coolDownDuration;
    }

    public void setTimeLimitValue (float newValue)
    {
        timeLimit = newValue;
    }

    public float getTimeLimitValue ()
    {
        return timeLimit;
    }

    public void setUseCoolDownValue (bool state)
    {
        useCoolDown = state;
    }

    public void setUseTimeLimitValue (bool state)
    {
        useTimeLimit = state;
    }

    public void setUseEnergyOnAbilityValue (bool state)
    {
        useEnergyOnAbility = state;
    }

    public void setEnergyAmountUsedValue (float newValue)
    {
        energyAmountUsed = newValue;
    }

    public void setUseInputOnPressDownState (bool state)
    {
        useInputOnPressDown = state;
    }

    public void setUseInputOnPressHoldState (bool state)
    {
        useInputOnPressHold = state;
    }

    public void setUseInputOnPressUpState (bool state)
    {
        useInputOnPressUp = state;
    }

    public bool isCoolDownInProcess ()
    {
        return coolDownInProcess;
    }

    public bool isTimeLimitInProcess ()
    {
        return timeLimitInProcess;
    }

    //EDITOR FUNCTIONS
    public void enableOrDisableAbilityFromEditor (bool state)
    {
        setAbilityEnabledState (state);

        updateComponent ();
    }

    public void enableOrDisableAllAbilitiesCanBeShownOnWheelFromEditor (bool state)
    {
        abilityCanBeShownOnWheelSelection = state;

        updateComponent ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Abilities Enabled State " + Name, gameObject);
    }
}
