using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodeMonkey.Toolkit.TOpenAIAPI {

    /// <summary>
    /// ** Open AI API **
    /// 
    /// IMPORTANT: You need to input your API_KEY for it to work!
    /// 
    /// Easily use the API to do all kinds actions with AI directly from inside Unity
    /// You can do a simple prompt, you can include instructions, you can use a Chat ID,
    /// you can tell it to search the web, you can feed it images and files.
    /// 
    /// Construct your AskChatGPTData object with the parameters you need
    /// Then call OpenAIAPI.AskChatGPT();
    /// </summary>
    public static class OpenAIAPI {

        // IMPORTANT: You need to input your API_KEY for it to work!
        private const string API_KEY = "sk-proj-AAAAAAAAAAA";

        private const string REPONSES_ENDPOINT = "https://api.openai.com/v1/responses";



        public class AskChatGPTData {
            public string model;
            public string instructions;
            public string chatPromptId;
            public string prompt;
            public Texture2D[] texture2DArray;
            public string[] fileUrlArray;
            public bool useWebSearchTool;
            public ImageGeneration imageGeneration;

            public class ImageGeneration {
                public string size;
                public string quality;
            }
        }



        [Serializable]
        public class ResponsesSend {
            public string model;
            public string instructions;
            public Prompt prompt;
            public Input[] input;
            public Tool_Base[] tools;
            public Text text;

            public string ToJson() {
                string json = "{";

                if (model != null) {
                    json += "\"model\":\"" + model + "\",";
                }
                if (instructions != null) {
                    json += "\"instructions\":\"" + instructions + "\",";
                }
                if (prompt != null) {
                    json += "\"prompt\":" + JsonUtility.ToJson(prompt) + ",";
                }
                if (input != null) {
                    string inputArrayJson = "[";
                    for (int i = 0; i < input.Length; i++) {
                        inputArrayJson += input[i].ToJson() + ",";
                    }
                    inputArrayJson = inputArrayJson.Remove(inputArrayJson.Length - 1); // Remove last ","
                    inputArrayJson += "]";
                    json += "\"input\":" + inputArrayJson + ",";
                }

                if (tools != null) {
                    string toolArrayJson = "[";
                    for (int i = 0; i < tools.Length; i++) {
                        toolArrayJson += JsonUtility.ToJson(tools[i]) + ",";
                    }
                    toolArrayJson = toolArrayJson.Remove(toolArrayJson.Length - 1); // Remove last ","
                    toolArrayJson += "]";
                    json += "\"tools\":" + toolArrayJson + ",";
                }

                if (text != null) {
                    json += "\"text\":" + JsonUtility.ToJson(text) + ",";
                }

                json = json.Remove(json.Length - 1); // Remove last ","
                json += "}";
                return json;
            }
        }



        [Serializable]
        public class Input {

            public string role;
            public Content_Base[] content;

            [Serializable]
            // This class is needed because the API requires optional parameters to only exist if they have data
            public class MinimalJSON {
                public string role;
            }

            // Helper function because JsonUtility does not serialize inheritance
            public string ToJson() {
                string contentArrayJson = "[";
                for (int i = 0; i < content.Length; i++) {
                    contentArrayJson += JsonUtility.ToJson(content[i]);
                    if (i < content.Length - 1) {
                        contentArrayJson += ",";
                    }
                }
                contentArrayJson += "]";
                MinimalJSON inputJson = new MinimalJSON {
                    role = role,
                };

                string json = JsonUtility.ToJson(inputJson);
                json = json.Substring(0, json.Length - 1); // Remove final "}"

                if (content.Length > 0) {
                    json += ",\"content\":" + contentArrayJson;
                }

                json += "}";
                return json;
            }
        }

        [Serializable]
        public class Content_Base {
        }

        [Serializable]
        public class Content_Text : Content_Base {
            public string type = "input_text";
            public string text;
        }

        [Serializable]
        public class Content_Image : Content_Base {
            public string type = "input_image";
            public string image_url;
        }

        [Serializable]
        public class Content_File : Content_Base {
            public string type = "input_file";
            public string file_url;
        }



        [Serializable]
        public class Tool_Base {
        }

        [Serializable]
        public class Tool_WebSearch : Tool_Base {
            public string type = "web_search";
        }

        [Serializable]
        public class Tool_ImageGeneration : Tool_Base {
            public string type = "image_generation";
            public string size;
            public string quality;
        }



        [Serializable]
        public class Prompt {
            public string id;
            public Variables variables;
            public string version;
        }

        [Serializable]
        public class Format {
            public string type;
        }






        [Serializable]
        public class Response {
            public string id;
            public string _object;
            public int created_at;
            public string status;
            public bool background;
            public Billing billing;
            public int completed_at;
            public object error;
            public float frequency_penalty;
            public object incomplete_details;
            public Instruction[] instructions;
            public int max_output_tokens;
            public object max_tool_calls;
            public string model;
            public Output[] output;
            public bool parallel_tool_calls;
            public float presence_penalty;
            public object previous_response_id;
            public Prompt prompt;
            public object prompt_cache_key;
            public object prompt_cache_retention;
            public Reasoning reasoning;
            public object safety_identifier;
            public string service_tier;
            public bool store;
            public float temperature;
            public Text text;
            public string tool_choice;
            public object[] tools;
            public int top_logprobs;
            public float top_p;
            public string truncation;
            public Usage usage;
            public object user;
            public Metadata metadata;
        }

        [Serializable]
        public class Billing {
            public string payer;
        }

        [Serializable]
        public class Variables {
        }

        [Serializable]
        public class Reasoning {
            public object effort;
            public object summary;
        }

        [Serializable]
        public class Text {
            public Format format;
            //public string verbosity;
        }

        [Serializable]
        public class Usage {
            public int input_tokens;
            public Input_Tokens_Details input_tokens_details;
            public int output_tokens;
            public Output_Tokens_Details output_tokens_details;
            public int total_tokens;
        }

        [Serializable]
        public class Input_Tokens_Details {
            public int cached_tokens;
        }

        [Serializable]
        public class Output_Tokens_Details {
            public int reasoning_tokens;
        }

        [Serializable]
        public class Metadata {
        }

        [Serializable]
        public class Instruction {
            public string type;
            public Content_Text[] content;
            public string role;
        }

        [Serializable]
        public class Output {
            public string id;
            public string type;
            public string status;
            public Content[] content;
            public string role;
            public string result;
        }

        [Serializable]
        public class Content {
            public string type;
            public object[] annotations;
            public object[] logprobs;
            public string text;
        }


        private static Dictionary<string, string> GetHeadersDictionary() {
            Dictionary<string, string> headerDictionary = new Dictionary<string, string> {
                { "Authorization", $"Bearer {API_KEY}"},
                { "Content-Type", "application/json"},
            };
            return headerDictionary;
        }

        public static void AskChatGPT(AskChatGPTData askChatGPTData, Action<string> onError, Action<Response, string> onSuccess) {
            ResponsesSend responsesSend = new ResponsesSend {
                model = askChatGPTData.model,
                instructions = askChatGPTData.instructions,
                input = new Input[] {
                    new Input {
                        role = "user",
                        content = new Content_Base[] {
                            new Content_Text {
                                text = askChatGPTData.prompt,
                            },
                        },
                    }
                }
            };

            if (!string.IsNullOrEmpty(askChatGPTData.chatPromptId)) {
                responsesSend.prompt = new Prompt {
                    id = askChatGPTData.chatPromptId,
                };
            }

            if (askChatGPTData.texture2DArray != null && askChatGPTData.texture2DArray.Length > 0) {
                List<Content_Base> contentList = new List<Content_Base>(responsesSend.input[0].content);
                foreach (Texture2D texture2D in askChatGPTData.texture2DArray) {
                    string imageBase64 = System.Convert.ToBase64String(texture2D.EncodeToPNG());
                    contentList.Add(new Content_Image {
                        image_url = "data:image/png;base64," + imageBase64
                    });
                }
                responsesSend.input[0].content = contentList.ToArray();
            }

            if (askChatGPTData.fileUrlArray != null && askChatGPTData.fileUrlArray.Length > 0) {
                List<Content_Base> contentList = new List<Content_Base>(responsesSend.input[0].content);
                foreach (string fileUrl in askChatGPTData.fileUrlArray) {
                    contentList.Add(new Content_File {
                        file_url = fileUrl
                    });
                }
                responsesSend.input[0].content = contentList.ToArray();
            }

            if (askChatGPTData.useWebSearchTool) {
                responsesSend.tools = new Tool_Base[] {
                    new Tool_WebSearch(),
                };
            }

            if (askChatGPTData.imageGeneration != null) {
                responsesSend.tools = new Tool_Base[] {
                    new Tool_ImageGeneration {
                        size = askChatGPTData.imageGeneration.size,
                        quality = askChatGPTData.imageGeneration.quality
                    },
                };
            }

            AskChatGPT(responsesSend, onError, onSuccess);
        }

        public static void AskChatGPT(ResponsesSend responsesSend, Action<string> onError, Action<Response, string> onSuccess) {
            string json = responsesSend.ToJson();
            CodeMonkey.Toolkit.TWebRequests.WebRequests.PostJson(REPONSES_ENDPOINT, json, GetHeadersDictionary(),
                (string error) => {
                    onError(error);
                },
                (string success) => {
                    Debug.Log("ChatGPT Response: " + success);
                    Response response = JsonUtility.FromJson<Response>(success);
                    string outputText = "";
                    if (response != null && response.output != null) {
                        foreach (Output output in response.output) {
                            if (output.type == "message" && output.content.Length > 0) {
                                outputText = output.content[0].text;
                            }
                        }
                        if (outputText == "") {
                            // Output text is still empty, maybe it's not a text reply, image?
                            foreach (Output output in response.output) {
                                if (output.type == "image_generation_call") {
                                    outputText = output.result;
                                }
                            }
                        }
                    }
                    onSuccess(response, outputText);
                }
            );

        }


        /*
        private static void TestingChatMessage() {
            string prompt = $"Give me the number 37" + ", return json";
            Debug.Log(prompt);

            ResponsesSend_Text responsesSend = new ResponsesSend_Text {
                prompt = new Prompt {
                    id = CHAT_ID_TESTING,
                },
                input = new Input_Text[] {
                new Input_Text {
                    role = "user",
                    content = new Content_Text[] {
                        new Content_Text {
                            type = "input_text",
                            text = prompt
                        }
                    }
                }
            },
                text = new Text {
                    format = new Format {
                        type = "json_object",
                    }
                },
            };

            CodeMonkey.Toolkit.TWebRequests.WebRequests.PostJson(REPONSES_ENDPOINT, JsonUtility.ToJson(responsesSend), GetHeadersDictionary(),
                (string error) => {
                    Debug.Log("ERROR: " + error);
                },
                (string success) => {
                    Response response = JsonUtility.FromJson<Response>(success);
                    Debug.Log(response.output[0].content[0].text);
                }
            );

        }
        */



    }

}