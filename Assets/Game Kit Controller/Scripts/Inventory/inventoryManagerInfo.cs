using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class inventoryManagerInfo : MonoBehaviour
{
    public bool saveCurrentPlayerInventoryToSaveFile = true;
    public bool loadCurrentPlayerInventoryFromSaveFile = true;

    public bool setInfiniteSlotValuesOnSaveLoad = true;

    public int inventorySlotAmount;

    public bool infiniteSlots;

    public List<inventoryInfo> inventoryList = new List<inventoryInfo> ();

    public bool isLoadingGame;

    public string lastWeaponCarriedOnHandsName = "";

    public bool inventoryWasEmptyWhenSaved;


    protected virtual void InitializeAudioElements ()
    {
        foreach (var inventoryInfo in inventoryList)
            inventoryInfo.InitializeAudioElements ();
    }

    public virtual bool isSaveCurrentPlayerInventoryToSaveFileActive ()
    {
        return saveCurrentPlayerInventoryToSaveFile;
    }

    public virtual bool isLoadCurrentPlayerInventoryFromSaveFileActive ()
    {
        return loadCurrentPlayerInventoryFromSaveFile;
    }

    public virtual void setInventorySlotAmountValue (int newValue)
    {
        inventorySlotAmount = newValue;
    }

    public virtual int getInventorySlotAmount ()
    {
        return inventorySlotAmount;
    }

    public virtual void setInfiniteSlotsState (bool state)
    {
        infiniteSlots = state;
    }

    public virtual bool isSetInfiniteSlotValuesOnSaveLoadActive ()
    {
        return setInfiniteSlotValuesOnSaveLoad;
    }

    public virtual bool isInfiniteSlotsActive ()
    {
        return infiniteSlots;
    }

    public virtual void initializeInventoryValues ()
    {

    }

    public virtual List<inventoryInfo> getInventoryList ()
    {
        return inventoryList;
    }

    public virtual List<inventoryListElement> getCurrentInventoryListManagerList ()
    {
        return null;
    }

    public virtual void setNewInventoryListManagerList (List<inventoryListElement> newList)
    {

    }

    public void setIsLoadingGameState (bool state)
    {
        isLoadingGame = state;
    }

    public bool isLoadingGameState ()
    {
        return isLoadingGame;
    }

    public virtual void updateDurabilityAmountStateOnAllInventoryObjects ()
    {

    }

    public virtual string getLastWeaponCarriedOnHandsName ()
    {
        return lastWeaponCarriedOnHandsName;
    }

    public virtual void setLastWeaponCarriedOnHandsName (string objectName)
    {
        lastWeaponCarriedOnHandsName = objectName;
    }

    public virtual void setInventoryWasEmptyWhenSavedState (bool state)
    {
        inventoryWasEmptyWhenSaved = state;
    }

    public virtual bool getInventoryWasEmptyWhenSaved ()
    {
        return inventoryWasEmptyWhenSaved;
    }
}
