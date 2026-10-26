using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GKC.Localization
{
    public class languageCheckerUIElement : languageElementChecker
    {
        [Header ("Custom Settings")]
        [Space]

        public Text mainText;

        [Space]

        public bool useDropDown;
        public Dropdown mainDropDown;

        [Space]

        public bool useUIElementsLocalizationManager = true;
        public bool useInteractionObjectsLocalizationManager;

        [Space]
        [Header ("Text Settings")]
        [Space]

        public bool useLanguageLocalizationManager = true;
        public string localizationKey;

        public bool checkEmptyKey = true;

        public bool setFullTextWithCapsEnabled;

        [Space]

        public List<string> dropDownLocalizationKey = new List<string> ();

        [Space]
        [Header ("Debug")]
        [Space]

        public bool showDebugPrint;


        bool localizationKeyAssigned;


        public override void updateLanguageOnElement (string currentLanguage)
        {
            if (checkEmptyKey) {
                if (!localizationKeyAssigned) {
                    if (useDropDown) {
                        if (mainDropDown == null) {
                            mainDropDown = GetComponentInChildren<Dropdown> ();
                        }

                        if (dropDownLocalizationKey.Count == 0) {
                            int dropDownOptionsCount = mainDropDown.options.Count;

                            for (var i = 0; i < dropDownOptionsCount; i++) {
                                dropDownLocalizationKey.Add (mainDropDown.options [i].text.ToString ());
                            }
                        }
                    } else {
                        if (localizationKey == "" || localizationKey == null) {
                            if (mainText == null) {
                                mainText = GetComponentInChildren<Text> ();
                            }

                            localizationKey = mainText.text;
                        }
                    }

                    localizationKeyAssigned = true;
                }

                if (showDebugPrint) {
                    print ("language to set " + currentLanguage + " " + localizationKeyAssigned);

                    if (useDropDown) {
                        print ("drop down value adjusted");
                    } else {
                        print (localizationKey + " " + mainText.text);
                    }
                }
            }

            if (useLanguageLocalizationManager) {
                if (useDropDown) {
                    if (mainDropDown != null) {
                        int dropDownOptionsCount = mainDropDown.options.Count;

                        for (var i = 0; i < dropDownOptionsCount; i++) {
                            string newText = "";

                            if (useUIElementsLocalizationManager) {
                                newText = UIElementsLocalizationManager.GetLocalizedValue (dropDownLocalizationKey [i]);
                            } else if (useInteractionObjectsLocalizationManager) {
                                newText = interactionObjectsLocalizationManager.GetLocalizedValue (dropDownLocalizationKey [i]);
                            }

                            if (setFullTextWithCapsEnabled) {
                                newText = newText.ToUpper ();
                            }

                            mainDropDown.options [i].text = newText;
                        }

                        mainDropDown.RefreshShownValue ();
                    }
                } else {
                    string newText = "";

                    if (useUIElementsLocalizationManager) {
                        newText = UIElementsLocalizationManager.GetLocalizedValue (localizationKey);
                    } else if (useInteractionObjectsLocalizationManager) {
                        newText = interactionObjectsLocalizationManager.GetLocalizedValue (localizationKey);
                    }

                    if (setFullTextWithCapsEnabled) {
                        newText = newText.ToUpper ();
                    }

                    mainText.text = newText;
                }

                if (showDebugPrint) {
                    if (useDropDown) {
                        int dropDownOptionsCount = mainDropDown.options.Count;

                        print ("drop down elements " + dropDownOptionsCount);

                        for (var i = 0; i < dropDownOptionsCount; i++) {
                            print (mainDropDown.options [i].text.ToString ());
                        }
                    } else {
                        print (localizationKey + " " + mainText.text);
                    }
                }
            } else {
                for (int i = 0; i < UIElementLanguageInfoList.Count; i++) {
                    if (UIElementLanguageInfoList [i].language.Equals (currentLanguage)) {
                        mainText.text = UIElementLanguageInfoList [i].textContent;

                        if (showDebugPrint) {
                            print (currentLanguage + " " + mainText.text);
                        }

                        return;
                    }
                }
            }
        }
    }
}
