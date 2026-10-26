using CodeMonkey.Toolkit.TBlockerUI;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeMonkey.Toolkit.TOpenAIAPI.OpenAIAPI;


namespace CodeMonkey.Toolkit.TOpenAIAPI.Demo {

    public class Demo_WithChatPromptId : MonoBehaviour {


        [SerializeField] private TMP_InputField promptInputField;
        [SerializeField] private TMP_InputField chatPromptIdInputField;
        [SerializeField] private Button sendPromptButton;
        [SerializeField] private TextMeshProUGUI outputTextMesh;


        private void Awake() {
            sendPromptButton.onClick.AddListener(() => {
                Debug.Log("OpenAI Prompt: " + promptInputField.text);
                outputTextMesh.text = "Asking ChatGPT with ChatPromptId...";
                Demo.Instance.ShowLoading();
                OpenAIAPI.AskChatGPT(
                    new AskChatGPTData {
                        model = "gpt-5",
                        chatPromptId = chatPromptIdInputField.text,
                        prompt = promptInputField.text,
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