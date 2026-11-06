using CodeMonkey.Toolkit.TBlockerUI;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeMonkey.Toolkit.TOpenAIAPI.OpenAIAPI;


namespace CodeMonkey.Toolkit.TOpenAIAPI.Demo {

    public class Demo_GenerateImage : MonoBehaviour {


        [SerializeField] private TMP_InputField promptInputField;
        [SerializeField] private Button sendPromptButton;
        [SerializeField] private TextMeshProUGUI outputTextMesh;
        [SerializeField] private RawImage rawImage;


        private void Awake() {
            sendPromptButton.onClick.AddListener(() => {
                Debug.Log("OpenAI Prompt: " + promptInputField.text);
                outputTextMesh.text = "Asking ChatGPT... (this one can take a while, 2 mins)";
                Demo.Instance.ShowLoading();
                OpenAIAPI.AskChatGPT(
                    new AskChatGPTData {
                        model = "gpt-5",
                        prompt = promptInputField.text,
                        imageGeneration = new AskChatGPTData.ImageGeneration {
                            size = "1024x1024",
                            quality = "low",
                        }
                    },
                    (string error) => {
                        Demo.Instance.HideLoading();
                        Debug.LogError(error);
                        outputTextMesh.text = "ERROR: " + error;
                    },
                    (Response response, string outputText) => {
                        Demo.Instance.HideLoading();
                        outputTextMesh.text = outputText;

                        string imageBase64 = outputText;
                        byte[] imageBytes = System.Convert.FromBase64String(imageBase64);
                        Texture2D texture = new Texture2D(2, 2);
                        texture.LoadImage(imageBytes);
                        rawImage.texture = texture;
                    }
                );
            });
        }

    }

}