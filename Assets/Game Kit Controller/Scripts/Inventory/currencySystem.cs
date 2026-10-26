using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class currencySystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public int currentMoneyAmount;

    public string statName = "Money";
    public string extraStringContent = "$";

    public bool showTotalMoneyAmountOnChange;

    public bool increaseMoneyTextSmoothly;
    public float increaseMoneyTextRate = 0.01f;
    public float delayToStartIncreasMoneyText = 0.5f;

    [Space]

    public float timeToShowTotalMoneyAmount;

    public bool hideTotalAmountMoneyPAnelAfterDelay = true;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public UnityEvent eventOnReceiveMoney;
    public eventParameters.eventToCallWithString eventOnReceiveMoneyWithString;

    [Space]
    [Header ("Components")]
    [Space]

    public GameObject totalMoneyAmountPanel;
    public Text totalMoneyAmountText;
    public playerStatsSystem playerStatsManager;


    int moneyToAdd;
    int previousMoneyAmount;

    bool firstAmountAssigned;

    bool previousMoneyToAddChecked = true;

    Coroutine showTotalMoneyCoroutine;

    bool lastAmountAddedIsPositive = true;

    float customMoneyChangeSpeed;


    public void increaseTotalMoneyAmount (float extraValue, float customMoneyChangeSpeedValue)
    {
        customMoneyChangeSpeed = customMoneyChangeSpeedValue;

        increaseTotalMoneyAmount (extraValue);
    }

    public void increaseTotalMoneyAmount (float extraValue)
    {
        if (increaseMoneyTextSmoothly) {
            if (moneyToAdd == 0) {
                previousMoneyAmount = currentMoneyAmount;

                previousMoneyToAddChecked = false;
            }
        }

        if (extraValue > 0) {
            lastAmountAddedIsPositive = true;
        } else {
            lastAmountAddedIsPositive = false;
        }

        currentMoneyAmount += (int)extraValue;

        eventOnReceiveMoney.Invoke ();

        string newString = "";

        if (extraValue > 0) {
            newString = "+";
        } else {
            newString = "-";
        }

        newString += extraValue + extraStringContent;

        eventOnReceiveMoneyWithString.Invoke (newString);

        playerStatsManager.updateStatValue (statName, currentMoneyAmount);

        if (showTotalMoneyAmountOnChange) {
            if (increaseMoneyTextSmoothly) {
                moneyToAdd += (int)extraValue;
            }

            showTotalMoneyAmountPanel ();
        }
    }

    public void initializeMoneyAmount (float newValue)
    {
        currentMoneyAmount = (int)newValue;

        updateTotayMoneyAmountText (currentMoneyAmount.ToString ());
    }

    public void updateMoneyAmountWithoutUpdatingStatManager (int statId, float amount)
    {
        currentMoneyAmount = (int)amount;

        updateTotayMoneyAmountText (currentMoneyAmount.ToString ());
    }

    public bool useMoney (float amountToUse)
    {
        if (currentMoneyAmount >= (int)amountToUse) {
            currentMoneyAmount -= (int)amountToUse;

            playerStatsManager.updateStatValue (statName, currentMoneyAmount);

            return true;
        } else {
            return false;
        }
    }

    public float getCurrentMoneyAmount ()
    {
        //		print (currentMoneyAmount);

        return (float)currentMoneyAmount;
    }

    public bool canSpendMoneyAmount (float amountToSpend)
    {
        return currentMoneyAmount >= (int)amountToSpend;
    }

    public void showTotalMoneyAmountPanel ()
    {
        stopShowTotalMoneyAmountPanelCoroutine ();

        showTotalMoneyCoroutine = StartCoroutine (showTotalMoneyAmountPanelCoroutine ());
    }

    public void stopShowTotalMoneyAmountPanelCoroutine ()
    {
        if (showTotalMoneyCoroutine != null) {
            StopCoroutine (showTotalMoneyCoroutine);
        }
    }

    IEnumerator showTotalMoneyAmountPanelCoroutine ()
    {
        if (totalMoneyAmountPanel.activeSelf == false) {
            totalMoneyAmountPanel.SetActive (true);
        }

        if (!firstAmountAssigned) {
            updateTotayMoneyAmountText (previousMoneyAmount.ToString ());

            firstAmountAssigned = true;
        }

        if (increaseMoneyTextSmoothly) {

            if (!previousMoneyToAddChecked) {
                yield return new WaitForSeconds (delayToStartIncreasMoneyText);

                previousMoneyToAddChecked = true;
            }

            int moneyIncreaseAmount = 1;

            if (!lastAmountAddedIsPositive) {
                moneyToAdd = Mathf.Abs (moneyToAdd);
            }

            if (moneyToAdd > 900) {
                int extraIncreaseAmount = moneyToAdd / 900;

                if (customMoneyChangeSpeed != 0) {
                    moneyIncreaseAmount += extraIncreaseAmount + (int)customMoneyChangeSpeed;
                } else {
                    moneyIncreaseAmount += extraIncreaseAmount + 12;
                }
            }

            while (moneyToAdd > 0) {
                if (lastAmountAddedIsPositive) {
                    previousMoneyAmount += moneyIncreaseAmount;
                } else {
                    previousMoneyAmount -= moneyIncreaseAmount;
                }

                updateTotayMoneyAmountText (previousMoneyAmount.ToString ());

                moneyToAdd -= moneyIncreaseAmount;

                yield return new WaitForSeconds (increaseMoneyTextRate);
            }

            updateTotayMoneyAmountText (currentMoneyAmount.ToString ());
        } else {
            updateTotayMoneyAmountText (currentMoneyAmount.ToString ());
        }

        lastAmountAddedIsPositive = true;

        customMoneyChangeSpeed = 0;

        yield return new WaitForSeconds (timeToShowTotalMoneyAmount);

        if (hideTotalAmountMoneyPAnelAfterDelay) {
            totalMoneyAmountPanel.SetActive (false);
        }
    }

    void updateTotayMoneyAmountText (string newText)
    {
        totalMoneyAmountText.text = newText + " " + extraStringContent;
    }
}
