using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class playerOptionsEditorSystem : MonoBehaviour
{
    public bool playerOptionsEditorEnabled = true;

    public bool saveCurrentPlayerOptionsToSaveFile = true;

    public bool initializeOptionsOnlyWhenLoadingGame;

    public List<optionInfo> optionInfoList = new List<optionInfo> ();

    optionInfo currentOptionInfo;

    public bool isLoadingGame;

    public bool valuesInitialized;

    void Start ()
    {
        StartCoroutine (initializeOptionValuesCoroutine ());
    }

    IEnumerator initializeOptionValuesCoroutine ()
    {
        yield return new WaitForSeconds (0.01f);

        initializeOptionValues ();
    }

    void initializeOptionValues ()
    {
        if (!playerOptionsEditorEnabled) {
            return;
        }

        for (int i = 0; i < optionInfoList.Count; i++) {

            currentOptionInfo = optionInfoList [i];

            if (currentOptionInfo.optionEnabled) {
                bool callInitializeValueEventResult = (!initializeOptionsOnlyWhenLoadingGame || isLoadingGame || currentOptionInfo.initializeAlwaysValueOnStart);

                if (currentOptionInfo.useScrollBar) {
                    if (currentOptionInfo.scrollBar != null) {
                        if (currentOptionInfo.currentScrollBarValue) {
                            currentOptionInfo.scrollBar.value = 1;
                        } else {
                            currentOptionInfo.scrollBar.value = 0;
                        }
                    }

                    if (callInitializeValueEventResult) {
                        if (currentOptionInfo.useOppositeBoolValue) {
                            currentOptionInfo.optionEvent.Invoke (!currentOptionInfo.currentScrollBarValue);
                        } else {
                            currentOptionInfo.optionEvent.Invoke (currentOptionInfo.currentScrollBarValue);
                        }
                    }
                }

                if (currentOptionInfo.useSlider) {
                    if (currentOptionInfo.slider != null) {
                        currentOptionInfo.slider.value = currentOptionInfo.currentSliderValue;
                    }

                    if (currentOptionInfo.showSliderText) {
                        if (currentOptionInfo.sliderText != null) {
                            currentOptionInfo.sliderText.text = currentOptionInfo.currentSliderValue.ToString ("0.#");
                        }
                    }

                    if (callInitializeValueEventResult) {
                        currentOptionInfo.floatOptionEvent.Invoke (currentOptionInfo.currentSliderValue);
                    }
                }

                if (currentOptionInfo.useToggle) {
                    if (currentOptionInfo.toggle != null) {
                        currentOptionInfo.toggle.isOn = currentOptionInfo.currentToggleValue;
                    }

                    if (callInitializeValueEventResult) {
                        if (currentOptionInfo.useOppositeBoolValue) {
                            currentOptionInfo.optionEvent.Invoke (!currentOptionInfo.currentToggleValue);
                        } else {
                            currentOptionInfo.optionEvent.Invoke (currentOptionInfo.currentToggleValue);
                        }
                    }
                }

                if (currentOptionInfo.useDropDown) {
                    if (currentOptionInfo.dropDown != null) {
                        currentOptionInfo.dropDown.value = currentOptionInfo.currentDropDownValue;
                    }

                    if (callInitializeValueEventResult) {
                        currentOptionInfo.stringOptionEvent.Invoke (currentOptionInfo.dropDown.options [currentOptionInfo.currentDropDownValue].text);

                        if (currentOptionInfo.useDropDownIntegerValueEvent) {
                            currentOptionInfo.dropDownIntegerValueEvent.Invoke (currentOptionInfo.currentDropDownValue);
                        }
                    }
                }
            }
        }

        valuesInitialized = true;
    }

    public void setIsLoadingGameState (bool state)
    {
        isLoadingGame = state;
    }

    public bool isLoadingGameState ()
    {
        return isLoadingGame;
    }

    public void setOptionByScrollBar (Scrollbar scrollBarToSearch)
    {
        if (!valuesInitialized) {
            return;
        }

        for (int i = 0; i < optionInfoList.Count; i++) {
            currentOptionInfo = optionInfoList [i];

            if (currentOptionInfo.optionEnabled) {
                if (currentOptionInfo.useScrollBar && currentOptionInfo.scrollBar == scrollBarToSearch) {
                    if (currentOptionInfo.scrollBar != null) {
                        if (currentOptionInfo.scrollBar.value < 0.5f) {
                            if (currentOptionInfo.scrollBar.value > 0) {
                                currentOptionInfo.scrollBar.value = 0;
                            }

                            currentOptionInfo.currentScrollBarValue = false;
                        } else {
                            if (currentOptionInfo.scrollBar.value < 1) {
                                currentOptionInfo.scrollBar.value = 1;
                            }

                            currentOptionInfo.currentScrollBarValue = true;
                        }
                    }

                    if (currentOptionInfo.useOppositeBoolValue) {
                        currentOptionInfo.optionEvent.Invoke (!currentOptionInfo.currentScrollBarValue);
                    } else {
                        currentOptionInfo.optionEvent.Invoke (currentOptionInfo.currentScrollBarValue);
                    }

                    return;
                }
            }
        }
    }

    public void setOptionBySlider (Slider sliderToSearch)
    {
        if (!valuesInitialized) {
            return;
        }

        for (int i = 0; i < optionInfoList.Count; i++) {
            currentOptionInfo = optionInfoList [i];

            if (currentOptionInfo.optionEnabled) {
                if (currentOptionInfo.useSlider && currentOptionInfo.slider == sliderToSearch) {
                    currentOptionInfo.floatOptionEvent.Invoke (sliderToSearch.value);

                    currentOptionInfo.currentSliderValue = sliderToSearch.value;

                    if (currentOptionInfo.showSliderText) {
                        if (currentOptionInfo.sliderText != null) {
                            currentOptionInfo.sliderText.text = currentOptionInfo.currentSliderValue.ToString ("0.#");
                        }
                    }

                    return;
                }
            }
        }
    }

    public void setOptionByToggle (Toggle toggleToSearch)
    {
        if (!valuesInitialized) {
            return;
        }

        for (int i = 0; i < optionInfoList.Count; i++) {
            currentOptionInfo = optionInfoList [i];

            if (currentOptionInfo.optionEnabled) {
                if (currentOptionInfo.toggle != null) {
                    if (currentOptionInfo.useToggle && currentOptionInfo.toggle == toggleToSearch) {
                        if (currentOptionInfo.useOppositeBoolValue) {
                            currentOptionInfo.optionEvent.Invoke (!toggleToSearch.isOn);
                        } else {
                            currentOptionInfo.optionEvent.Invoke (toggleToSearch.isOn);
                        }

                        currentOptionInfo.currentToggleValue = toggleToSearch.isOn;

                        return;
                    }
                }
            }
        }
    }

    public void setOptionByDropDown (Dropdown dropDownToSearch)
    {
        if (!valuesInitialized) {
            return;
        }

        for (int i = 0; i < optionInfoList.Count; i++) {
            currentOptionInfo = optionInfoList [i];

            if (currentOptionInfo.optionEnabled) {
                if (currentOptionInfo.dropDown != null) {
                    if (currentOptionInfo.useDropDown && currentOptionInfo.dropDown == dropDownToSearch) {
                        currentOptionInfo.stringOptionEvent.Invoke (dropDownToSearch.options [dropDownToSearch.value].text);

                        currentOptionInfo.currentDropDownValue = dropDownToSearch.value;

                        if (currentOptionInfo.useDropDownIntegerValueEvent) {
                            currentOptionInfo.dropDownIntegerValueEvent.Invoke (currentOptionInfo.currentDropDownValue);
                        }

                        return;
                    }
                }
            }
        }
    }

    public void setDefaultValues ()
    {
        if (!valuesInitialized) {
            return;
        }

        for (int i = 0; i < optionInfoList.Count; i++) {
            currentOptionInfo = optionInfoList [i];

            if (currentOptionInfo.optionEnabled) {
                if (optionInfoList [i].useScrollBar) {
                    if (optionInfoList [i].defaultScrollerbarValue) {
                        optionInfoList [i].scrollBar.value = 1;
                    } else {
                        optionInfoList [i].scrollBar.value = 0;
                    }

                    setOptionByScrollBar (optionInfoList [i].scrollBar);
                }

                if (optionInfoList [i].useSlider) {
                    if (optionInfoList [i].slider != null) {
                        optionInfoList [i].slider.value = optionInfoList [i].defaultSliderValue;

                        setOptionBySlider (optionInfoList [i].slider);
                    }
                }

                if (optionInfoList [i].useToggle) {
                    if (optionInfoList [i].toggle != null) {
                        optionInfoList [i].toggle.isOn = optionInfoList [i].defaultToggleValue;

                        setOptionByToggle (optionInfoList [i].toggle);
                    }
                }

                if (optionInfoList [i].useDropDown) {
                    if (optionInfoList [i].dropDown != null) {
                        optionInfoList [i].dropDown.value = optionInfoList [i].defaultDropDownValue;

                        setOptionByDropDown (optionInfoList [i].dropDown);
                    }
                }
            }
        }
    }

    public void enableOptionByName (string optionName)
    {
        enableOrDisableOptionByName (optionName, true);
    }

    public void disableOptionByName (string optionName)
    {
        enableOrDisableOptionByName (optionName, false);
    }

    public void enableOrDisableOptionByName (string optionName, bool state)
    {
        int newIndex = optionInfoList.FindIndex (s => s.Name.Equals (optionName));

        if (newIndex > -1) {
            currentOptionInfo = optionInfoList [newIndex];

            currentOptionInfo.optionEnabled = state;

            updateComponent ();
        }
    }

    public void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Player Options Editor System " + gameObject.name, gameObject);
    }

    [System.Serializable]
    public class optionInfo
    {
        public string Name;

        public bool optionEnabled = true;

        public bool useScrollBar = true;
        public Scrollbar scrollBar;
        public bool currentScrollBarValue = true;
        public bool defaultScrollerbarValue;

        public bool useSlider;
        public Slider slider;
        public float currentSliderValue;
        public float defaultSliderValue;

        public bool showSliderText;
        public Text sliderText;

        public bool useToggle;
        public Toggle toggle;
        public bool currentToggleValue;
        public bool defaultToggleValue;

        public bool useOppositeBoolValue;

        public bool useDropDown;
        public Dropdown dropDown;
        public int currentDropDownValue;
        public int defaultDropDownValue;

        public eventParameters.eventToCallWithBool optionEvent;

        public eventParameters.eventToCallWithAmount floatOptionEvent;

        public eventParameters.eventToCallWithString stringOptionEvent;

        public bool useDropDownIntegerValueEvent;

        public eventParameters.eventToCallWithInteger dropDownIntegerValueEvent;

        public bool initializeAlwaysValueOnStart;
    }
}
