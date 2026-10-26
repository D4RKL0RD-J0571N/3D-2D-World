using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static initialinventoryListData;

public class characterCustomizationInventoryHelper : inventoryManagerInfo
{
    [Space]
    [Header ("Custom Settings")]
    [Space]

    public bool setCharacterElementsEquipped = true;

    public int amountOnElements = 1;

    [Space]
    [Header ("Extra Inventory Objects")]
    [Space]

    public bool useExtraInventoryObjectsForArmorSet;

    public List<extraInventoryInfo> extraInventoryInfoList = new List<extraInventoryInfo> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    [Space]
    [Header ("Components")]
    [Space]

    public characterCustomizationManager mainCharacterCustomizationManager;

    public inventoryListManager mainInventoryListManager;

    public fullArmorClothTemplateData mainFullArmorClothTemplateData;


    public void storeCustomizationElementsAsInventory ()
    {
        inventoryList.Clear ();

        List<string> currentPiecesList = mainCharacterCustomizationManager.getCurrentPiecesList ();

        for (int i = 0; i < currentPiecesList.Count; i++) {

            inventoryInfo newInventoryInfo = mainInventoryListManager.getInventoryInfoFromName (currentPiecesList [i]);

            if (newInventoryInfo != null) {
                newInventoryInfo.amount = amountOnElements;
                newInventoryInfo.isEquipped = setCharacterElementsEquipped;

                inventoryList.Add (newInventoryInfo);
            }
        }

        //check if the pieces list have a complete set of armor, obtain its name and get the list of extra inventory objects for it

        if (useExtraInventoryObjectsForArmorSet) {
            bool isFullArmorCompleteResult = false;

            string fullArmorClothName = GKC_Utils.checkFullArmorClothState (mainFullArmorClothTemplateData, currentPiecesList, showDebugPrint);

            if (showDebugPrint) {
                print ("checking for extra inventory, and if armor set complete ");
            }

            if (fullArmorClothName == null || fullArmorClothName == "") {
                isFullArmorCompleteResult = false;
            } else {
                isFullArmorCompleteResult = true;
            }

            if (!isFullArmorCompleteResult) {
                if (showDebugPrint) {
                    print ("armor full not found, cancelling");
                }

                return;
            }

            if (showDebugPrint) {
                print ("armor full found " + fullArmorClothName);
            }

            int newIndex = extraInventoryInfoList.FindIndex (s => s.armorSetToUseName.Equals (fullArmorClothName));

            if (newIndex <= -1) {
                return;
            }

            extraInventoryInfo currentExtraInventoryInfo = extraInventoryInfoList [newIndex];

            initialinventoryListData mainInitialinventoryListData = currentExtraInventoryInfo.mainInitialinventoryListData;

            int extraObjectsCount = mainInitialinventoryListData.initialInventoryObjectInfoList.Count;

            for (int i = 0; i < extraObjectsCount; i++) {

                initialInventoryObjectInfo currentExtraObject = mainInitialinventoryListData.initialInventoryObjectInfoList [i];

                if (currentExtraObject.addInventoryObject) {
                    inventoryInfo newInventoryInfo = mainInventoryListManager.getInventoryInfoFromName (currentExtraObject.Name);

                    if (newInventoryInfo != null) {
                        newInventoryInfo.amount = currentExtraObject.amount;
                        newInventoryInfo.isEquipped = currentExtraObject.isEquipped;

                        inventoryList.Add (newInventoryInfo);
                    }
                }
            }
        }
    }

    public void setMainCharacterCustomizationManager (characterCustomizationManager newCharacterCustomizationManager)
    {
        mainCharacterCustomizationManager = newCharacterCustomizationManager;
    }

    public void setMainFullArmorClothTemplateData (fullArmorClothTemplateData newFullArmorClothTemplateData)
    {
        mainFullArmorClothTemplateData = newFullArmorClothTemplateData;
    }

    public void setNewCharacterModel (GameObject newObject)
    {
        characterCustomizationManager currentCharacterCustomizationManager = newObject.GetComponent<characterCustomizationManager> ();

        setMainCharacterCustomizationManager (currentCharacterCustomizationManager);

        characterModelElementInfo currentCharacterModelElementInfo = newObject.GetComponent<characterModelElementInfo> ();

        if (currentCharacterModelElementInfo != null) {
            setMainFullArmorClothTemplateData (currentCharacterModelElementInfo.mainFullArmorClothTemplateData);
        }
    }

    [System.Serializable]
    public class extraInventoryInfo
    {
        public string Name;

        public string armorSetToUseName;

        public initialinventoryListData mainInitialinventoryListData;
    }

}
