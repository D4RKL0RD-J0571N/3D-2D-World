using CodeMonkey.Toolkit.TBlockerUI;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeMonkey.Toolkit.TOpenAIAPI.OpenAIAPI;


namespace CodeMonkey.Toolkit.TOpenAIAPI.Demo {

    public class Demo_AnalyzeImage : MonoBehaviour {


        [SerializeField] private TMP_InputField promptInputField;
        [SerializeField] private Button sendPromptButton;
        [SerializeField] private TextMeshProUGUI outputTextMesh;
        [SerializeField] private Image image;


        private void Awake() {
            sendPromptButton.onClick.AddListener(() => {
                Debug.Log("OpenAI Prompt: " + promptInputField.text);
                outputTextMesh.text = "Asking ChatGPT...";
                Demo.Instance.ShowLoading();
                OpenAIAPI.AskChatGPT(
                    new AskChatGPTData {
                        model = "gpt-5",
                        prompt = promptInputField.text,
                        texture2DArray = new Texture2D[] { (Texture2D)image.mainTexture },
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