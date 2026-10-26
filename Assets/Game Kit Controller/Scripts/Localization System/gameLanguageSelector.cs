using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using GKC.Localization;

public class gameLanguageSelector : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool languageSelectionEnabled = true;

    public List<string> languageNameList = new List<string> ();

    [Space]
    [Header ("Language Settings")]
    [Space]

    public string currentLanguage = "English";
    public bool updateElementsOnLanguageChange = true;

    public bool checkLanguageActive = true;

    private static bool checkLanguageActiveValue;

    [Space]

    public bool checkUpdateCurrentLanguageByPlayerPrefsOnStart;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;

    public bool languageUpdatedBySettings;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool checkSystemLanguageOnStart;

    public bool setDefaultLanguageOnStart;
    public string defaultLanguageOnStart;

    [Space]

    public eventParameters.eventToCallWithString eventOnLanguageCheckAtStart;

    public bool updateUIDropDownOnLanguageOnStart;
    public Dropdown languageDropDown;


    public const string mainManagerName = "Main Game Language Selector";

    public static string getMainManagerName ()
    {
        return mainManagerName;
    }

    private static gameLanguageSelector _gameLanguageSelectorInstance;

    public static gameLanguageSelector Instance { get { return _gameLanguageSelectorInstance; } }

    bool instanceInitialized;




    public void updateCurrentLanguageByPlayerPrefs ()
    {
        if (PlayerPrefs.HasKey ("Current Language")) {
            string currentLanguagePlayerPrefsValue = PlayerPrefs.GetString ("Current Language");

            if (currentLanguagePlayerPrefsValue != "") {
                setGameLanguage (currentLanguagePlayerPrefsValue);
            }
        }
    }

    public void setCurrentLanguagePlayerPrefsValue ()
    {
        if (currentLanguage != "") {
            PlayerPrefs.SetString ("Current Language", currentLanguage);
        }
    }

    public void getComponentInstance ()
    {
        if (instanceInitialized) {
            return;
        }

        if (_gameLanguageSelectorInstance != null && _gameLanguageSelectorInstance != this) {
            Destroy (this.gameObject);

            return;
        }

        _gameLanguageSelectorInstance = this;

        instanceInitialized = true;
    }

    void Awake ()
    {
        getComponentInstance ();

        checkLanguageActiveValue = checkLanguageActive;
    }

    void Start ()
    {
        if (checkSystemLanguageOnStart) {
            StartCoroutine (checkInitialSystemLanguage ());
        }

        if (checkUpdateCurrentLanguageByPlayerPrefsOnStart) {
            updateCurrentLanguageByPlayerPrefs ();
        }
    }

    IEnumerator checkInitialSystemLanguage ()
    {
        yield return new WaitForSeconds (0.4f);

        if (!languageUpdatedBySettings) {

            string currentSystemLanguage = Application.systemLanguage.ToString ();

            if (setDefaultLanguageOnStart) {
                currentSystemLanguage = defaultLanguageOnStart;
            }

            if (currentLanguage != currentSystemLanguage) {

                if (updateUIDropDownOnLanguageOnStart) {
                    if (showDebugPrint) {
                        print ("setting default language " + currentSystemLanguage);
                    }

                    int languageIndex = languageNameList.IndexOf (currentSystemLanguage);

                    if (languageIndex != -1) {
                        if (showDebugPrint) {
                            print ("language found on list, setting " + currentSystemLanguage);
                        }

                        languageDropDown.value = languageIndex;
                    }
                }

                eventOnLanguageCheckAtStart.Invoke (currentSystemLanguage);
            }
        }
    }

    public void setGameLanguageByIndex (int languageIndex)
    {
        if (languageIndex < languageNameList.Count) {
            setGameLanguage (languageNameList [languageIndex]);
        }
    }

    public void setGameLanguage (string languageSelected)
    {
        if (!languageSelectionEnabled) {
            return;
        }

        if (languageSelected != "") {

            GKC_Utils.setCurrentLanguage (languageSelected);

            languageUpdatedBySettings = true;

            updateLocalizationFile ();

            setCurrentLanguagePlayerPrefsValue ();
        }
    }

    void updateLocalizationFile ()
    {
        if (showDebugPrint) {
            print ("UPDATE LOCALIZATION " + currentLanguage);
        }

        //languageLocalizationManager [] languageLocalizationManagerList = FindObjectsOfType<languageLocalizationManager> ();

        languageLocalizationManager [] languageLocalizationManagerList = GKC_Utils.findObjectsByComponent<languageLocalizationManager> ();

        //print ("languageLocalizationManagerList " + languageLocalizationManagerList.Length);

        foreach (languageLocalizationManager currentLanguageLocalizationManager in languageLocalizationManagerList) {
            currentLanguageLocalizationManager.updateLocalizationFileExternally ();
        }

        updateAllLanguageElementCheckerOnScene ();
    }

    public void updateAllLanguageElementCheckerOnScene ()
    {
        if (GKC_Utils.isUpdateElementsOnLanguageChangeActive ()) {
            string currentLanguage = GKC_Utils.getCurrentLanguage ();

            //List<languageElementChecker> languageElementCheckerList = GKC_Utils.FindObjectsOfTypeAll<languageElementChecker> ();

            //if (languageElementCheckerList != null) {

            //    print ("languageElementCheckerList " + languageElementCheckerList.Count);

            //    for (var i = 0; i < languageElementCheckerList.Count; i++) {
            //        languageElementCheckerList [i].updateLanguageOnElement (currentLanguage);
            //    }
            //}

            languageElementChecker [] languageElementCheckerList = GKC_Utils.findObjectsByComponent<languageElementChecker> (true);

            //print ("languageElementCheckerList " + languageElementCheckerList.Length);

            foreach (languageElementChecker currentLanguageElementChecker in languageElementCheckerList) {
                currentLanguageElementChecker.updateLanguageOnElement (currentLanguage);
            }
        }
    }

    public void addLanguage (string newName)
    {
        if (!languageNameList.Contains (newName)) {
            languageNameList.Add (newName);
        }
    }

    public void removeLanguage (string newName)
    {
        if (languageNameList.Contains (newName)) {
            languageNameList.Remove (newName);
        }
    }

    public void setNewLanguageList (List<string> newNameList)
    {
        languageNameList.Clear ();

        languageNameList.AddRange (newNameList);
    }

    public void updateLanguageDropDown ()
    {
        if (languageDropDown != null) {
            //languageDropDown.ClearOptions ();

            int languageDropDownOptionsCount = languageDropDown.options.Count;

            for (var i = 0; i < languageDropDownOptionsCount; i++) {
                string currentLanguageElement = UIElementsLocalizationManager.GetLocalizedValue (languageNameList [i]);

                languageDropDown.options [i].text = currentLanguageElement;
            }

            languageDropDown.RefreshShownValue ();

            //languageDropDown.AddOptions (translatedLanguageNameList);
        }
    }

    public void assignNewLanguageDropDown (Dropdown newDropDown)
    {
        languageDropDown = newDropDown;
    }

    public void checkIfLanguageDropDownAssignedAndUpdate (Dropdown newDropDown)
    {
        if (languageDropDown == null) {
            assignNewLanguageDropDown (newDropDown);
        }

        updateLanguageDropDown ();
    }

    public List<string> getCurrentLanguageList ()
    {
        return languageNameList;
    }

    public void setCurrentLanguage (string newLanguage)
    {
        currentLanguage = newLanguage;
    }

    public string getCurrentLanguage ()
    {
        return currentLanguage;
    }

    public void setCurrentLanguageFromEditor (string newLanguage)
    {
        setCurrentLanguage (newLanguage);

        updateComponent ();
    }

    public static bool isCheckLanguageActive ()
    {
        return checkLanguageActiveValue;
    }

    public void toggleLocalizationForPCorMobile (bool state)
    {
        languageLocalizationManager [] languageLocalizationManagerList = GKC_Utils.findObjectsByComponent<languageLocalizationManager> ();

        foreach (languageLocalizationManager currentLanguageLocalizationManager in languageLocalizationManagerList) {
            currentLanguageLocalizationManager.setUseResourcesPathStateFromEditor (state);
        }
    }

    public void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Game Language Selector " + gameObject.name, gameObject);
    }
}