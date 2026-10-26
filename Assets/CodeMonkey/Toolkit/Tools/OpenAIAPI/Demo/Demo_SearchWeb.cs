using CodeMonkey.Toolkit.TBlockerUI;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeMonkey.Toolkit.TOpenAIAPI.OpenAIAPI;


namespace CodeMonkey.Toolkit.TOpenAIAPI.Demo {

    public class Demo_SearchWeb : MonoBehaviour {


        [SerializeField] private TMP_InputField promptInputField;
        [SerializeField] private Button sendPromptButton;
        [SerializeField] private TextMeshProUGUI outputTextMesh;


        private void Awake() {
            sendPromptButton.onClick.AddListener(() => {
                Debug.Log("OpenAI Prompt: " + promptInputField.text);
                outputTextMesh.text = "Asking ChatGPT... (this one can take a while, 2-4 mins)";
                Demo.Instance.ShowLoading();
                OpenAIAPI.AskChatGPT(
                    new AskChatGPTData {
                        model = "gpt-5",
                        prompt = promptInputField.text,
                        useWebSearchTool = true
                    },
                    (string error) => {
                        Demo.Instance.HideLoading();
                        Debug.LogError(error);
                        outputTextMesh.text = "ERROR: " + error;
                    },
                    (Response response, string outputText) => {
                        Demo.Instance.HideLoading();
                        Debug.Log(outputText);
                        outputTextMesh.text = outputText;
                    }
                );
            });
        }

    }

}