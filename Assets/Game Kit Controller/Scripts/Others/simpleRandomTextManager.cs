using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GKC.Localization;

public class simpleRandomTextManager : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool randomTextManagerEnabled = true;

    public List<randomTextInfo> randomTextInfoList = new List<randomTextInfo> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public int currentTextIndexSelected = -1;

    [Space]
    [Header ("Components")]
    [Space]

    public Text mainText;


    public void setRandomText (int randomTextID)
    {
        if (!randomTextManagerEnabled) {
            return;
        }

        bool textFound = false;

        int currentIndex = -1;

        int loopCounter = 0;

        while (!textFound) {
            if (randomTextID > -1) {
                currentIndex = randomTextInfoList.FindIndex (s => s.randomTextID == randomTextID);
            } else {
                currentIndex = Random.Range (0, randomTextInfoList.Count);
            }

            if (currentIndex >= 0) {
                textFound = true;

                if (currentTextIndexSelected >= 0) {
                    if (currentTextIndexSelected == currentIndex) {
                        textFound = false;
                    }
                }
            }

            loopCounter++;

            if (loopCounter >= randomTextInfoList.Count) {
                textFound = true;

                currentIndex = -1;
            }
        }

        if (currentIndex <= -1) {
            return;
        }

        currentTextIndexSelected = currentIndex;

        randomTextInfo currentRandomTextInfo = randomTextInfoList [currentTextIndexSelected];

        if (currentRandomTextInfo.randomTextEnabled) {
            string currentText = "";

            if (gameLanguageSelector.isCheckLanguageActive ()) {
                currentText = UIElementsLocalizationManager.GetLocalizedValue (currentRandomTextInfo.randomTextContent);
            } else {
                currentText = currentRandomTextInfo.randomTextContent;
            }

            mainText.text = currentText;
        }
    }

    [System.Serializable]
    public class randomTextInfo
    {
        public int randomTextID;

        public bool randomTextEnabled = true;

        [TextArea (3, 10)] public string randomTextContent;
    }
}
