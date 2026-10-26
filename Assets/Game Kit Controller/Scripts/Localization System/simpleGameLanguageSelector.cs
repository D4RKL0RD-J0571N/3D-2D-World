using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class simpleGameLanguageSelector : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public gameLanguageSelector mainGameLanguageSelector;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool gameLanguageSelectorLocated;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public bool useEventOnLanguageChange;
    public UnityEvent eventOnLanguageChange;

    [Space]
    [Header ("Components")]
    [Space]

    public Dropdown languageDropDown;


    public void setGameLanguageByIndex (int languageIndex)
    {
        checkSetGameLanguage (false, "", languageIndex);
    }

    public void setGameLanguage (string languageSelected)
    {
        checkSetGameLanguage (true, languageSelected, 0);
    }

    void checkSetGameLanguage (bool languageSelectedByName, string languageSelected, int languageIndex)
    {
        checkGetMainGameLanguageSelector ();

        if (gameLanguageSelectorLocated) {
            if (languageSelectedByName) {
                mainGameLanguageSelector.setGameLanguage (languageSelected);
            } else {
                mainGameLanguageSelector.setGameLanguageByIndex (languageIndex);
            }

            mainGameLanguageSelector.checkIfLanguageDropDownAssignedAndUpdate (languageDropDown);

            if (useEventOnLanguageChange) {
                eventOnLanguageChange.Invoke ();
            }
        }
    }

    void checkGetMainGameLanguageSelector ()
    {
        if (!gameLanguageSelectorLocated) {
            gameLanguageSelectorLocated = mainGameLanguageSelector != null;

            if (!gameLanguageSelectorLocated) {
                mainGameLanguageSelector = gameLanguageSelector.Instance;

                gameLanguageSelectorLocated = mainGameLanguageSelector != null;
            }

            if (!gameLanguageSelectorLocated) {
                GKC_Utils.instantiateMainManagerOnSceneWithTypeOnApplicationPlaying (gameLanguageSelector.getMainManagerName (), typeof (gameLanguageSelector), true);

                mainGameLanguageSelector = gameLanguageSelector.Instance;

                gameLanguageSelectorLocated = (mainGameLanguageSelector != null);
            }

            if (!gameLanguageSelectorLocated) {
                mainGameLanguageSelector = GKC_Utils.findObjectByComponent<gameLanguageSelector> ();

                gameLanguageSelectorLocated = mainGameLanguageSelector != null;
            }
        }
    }
}
