using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class energyStationSystem : craftingStationSystem
{
    [Header ("Main Settings")]
    [Space]

    public bool energyEnabled = true;

    public string energyName;

    public float maxEnergyAmount;

    public float currentEnergyAmount;

    [Space]

    public bool useEnergyOverTime;
    public float useEnergyRate;
    public float energyAmountToUseOverTime;
    public bool refillEnergyAfterTime;
    public float timeToRefillEnergy;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool energyActive;

    public bool energyInUse;

    public bool refillingEnergyInProcess;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool checkEventsOnEnergyStateChangeOnlyIfOutputSockedConnected;

    public bool useEventsOnEmptyEnergy;
    public UnityEvent eventsOnEmptyEnergy;

    public bool useEventsOnRefilledEnergy;
    public UnityEvent eventsOnRefilledEnergy;

    Coroutine updateCoroutine;

    float lastTimeEnergyUsed = 0;
    float lastTimeEnergyRefilled = 0;

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
        if (energyInUse) {
            if (useEnergyOverTime) {
                if (Time.time > lastTimeEnergyUsed + useEnergyRate) {
                    currentEnergyAmount -= energyAmountToUseOverTime;

                    lastTimeEnergyUsed = Time.time;
                }
            }

            if (currentEnergyAmount <= 0) {
                currentEnergyAmount = 0;

                energyInUse = false;

                lastTimeEnergyRefilled = Time.time;

                checkEventOnEmptyEnergy ();

                if (refillEnergyAfterTime) {
                    refillingEnergyInProcess = true;
                }
            }
        } else {
            if (refillEnergyAfterTime) {
                if (refillingEnergyInProcess) {
                    if (Time.time > lastTimeEnergyRefilled + timeToRefillEnergy) {
                        currentEnergyAmount += energyAmountToUseOverTime;

                        lastTimeEnergyRefilled = Time.time;
                    }

                    if (currentEnergyAmount >= maxEnergyAmount) {
                        currentEnergyAmount = maxEnergyAmount;

                        energyInUse = true;

                        refillingEnergyInProcess = false;

                        checkEventOnRefilledEnergy ();
                    }
                }
            }
        }
    }

    public float getCurrentEnergyAmount ()
    {
        return currentEnergyAmount;
    }

    public string getEnergyName ()
    {
        return energyName;
    }

    public void setEnergyActiveState (bool state)
    {
        if (!energyEnabled) {
            return;
        }

        energyActive = state;

        lastTimeEnergyUsed = Time.time;

        lastTimeEnergyRefilled = Time.time;

        if (energyActive) {
            stopUpdateCoroutine ();
        } else {
            updateCoroutine = StartCoroutine (updateSystemCoroutine ());
        }

        if (!energyActive) {
            refillingEnergyInProcess = false;

            energyInUse = false;
        }
    }

    public void refillAllEnergy ()
    {
        setCurrentEnergyAmount (maxEnergyAmount);
    }

    public void removeAllEnergy ()
    {
        setCurrentEnergyAmount (0);
    }

    public void setCurrentEnergyAmount (float newAmount)
    {
        currentEnergyAmount = newAmount;

        checkEnergyAmountState ();
    }

    public void addOrRemoveToCurrentEnergyAmount (float newAmount)
    {
        currentEnergyAmount += newAmount;

        checkEnergyAmountState ();
    }

    void checkEnergyAmountState ()
    {
        if (currentEnergyAmount >= maxEnergyAmount) {
            currentEnergyAmount = maxEnergyAmount;

            refillingEnergyInProcess = false;
        }

        if (currentEnergyAmount < 0) {
            currentEnergyAmount = 0;
        }

        if (currentEnergyAmount > 0) {
            checkEventOnRefilledEnergy ();
        } else {
            checkEventOnEmptyEnergy ();
        }
    }

    public void setEnergyInUseState (bool state)
    {
        energyInUse = state;
    }

    public override void checkStateOnSetOuput ()
    {
        if (outputSocket != null) {
            if (outputSocket.currentCraftingStationSystemAssigned != null) {
                outputSocket.currentCraftingStationSystemAssigned.sendEnergyValue (currentEnergyAmount);

                outputSocket.currentCraftingStationSystemAssigned.setInfiniteEnergyState (useInfiniteEnergy);

                outputSocket.currentCraftingStationSystemAssigned.setCurrentEnergyStationSystem (this);
            }
        }
    }

    public override void checkStateOnRemoveOuput ()
    {
        if (outputSocket != null) {
            if (outputSocket.currentCraftingStationSystemAssigned != null) {
                outputSocket.currentCraftingStationSystemAssigned.setInfiniteEnergyState (false);

                outputSocket.currentCraftingStationSystemAssigned.setCurrentEnergyStationSystem (null);
            }
        }
    }

    public void checkEventOnEmptyEnergy ()
    {
        if (checkEventsOnEnergyStateChangeOnlyIfOutputSockedConnected) {
            if (outputSocket != null) {

            } else {
                return;
            }
        }

        if (useEventsOnEmptyEnergy) {
            eventsOnEmptyEnergy.Invoke ();
        }
    }
    public void checkEventOnRefilledEnergy ()
    {
        if (checkEventsOnEnergyStateChangeOnlyIfOutputSockedConnected) {
            if (outputSocket != null) {

            } else {
                return;
            }
        }

        if (useEventsOnRefilledEnergy) {
            eventsOnRefilledEnergy.Invoke ();
        }
    }
}
