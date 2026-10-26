using CodeMonkey.Toolkit.TBlockerUI;
using UnityEngine;
using UnityEngine.UI;


namespace CodeMonkey.Toolkit.TOpenAIAPI.Demo {

    public class Demo : MonoBehaviour {


        public static Demo Instance { get; private set; }



        [SerializeField] private RectTransform loadingImageRectTransform;
        [SerializeField] private Button simplePromptButton;
        [SerializeField] private Button instructionsButton;
        [SerializeField] private Button searchWebButton;
        [SerializeField] private Button analyzeImageButton;
        [SerializeField] private Button askAboutFileButton;
        [SerializeField] private Button generateImageButton;
        [SerializeField] private Button withChatPromptIdButton;
        [SerializeField] private GameObject simplePromptContainerGameObject;
        [SerializeField] private GameObject instructionsContainerGameObject;
        [SerializeField] private GameObject searchWebContainerGameObject;
        [SerializeField] private GameObject analyzeImageContainerGameObject;
        [SerializeField] private GameObject askAboutFileContainerGameObject;
        [SerializeField] private GameObject generateImageContainerGameObject;
        [SerializeField] private GameObject withChatPromptIdContainerGameObject;


        private void Awake() {
            Instance = this;

            simplePromptButton.onClick.AddListener(() => {
                HideAllContainers();
                simplePromptContainerGameObject.SetActive(true);
                UpdateSelectedTabButtonVisual();
            });
            instructionsButton.onClick.AddListener(() => {
                HideAllContainers();
                instructionsContainerGameObject.SetActive(true);
                UpdateSelectedTabButtonVisual();
            });
            searchWebButton.onClick.AddListener(() => {
                HideAllContainers();
                searchWebContainerGameObject.SetActive(true);
                UpdateSelectedTabButtonVisual();
            });
            analyzeImageButton.onClick.AddListener(() => {
                HideAllContainers();
                analyzeImageContainerGameObject.SetActive(true);
                UpdateSelectedTabButtonVisual();
            });
            askAboutFileButton.onClick.AddListener(() => {
                HideAllContainers();
                askAboutFileContainerGameObject.SetActive(true);
                UpdateSelectedTabButtonVisual();
            });
            generateImageButton.onClick.AddListener(() => {
                HideAllContainers();
                generateImageContainerGameObject.SetActive(true);
                UpdateSelectedTabButtonVisual();
            });
            withChatPromptIdButton.onClick.AddListener(() => {
                HideAllContainers();
                withChatPromptIdContainerGameObject.SetActive(true);
                UpdateSelectedTabButtonVisual();
            });
        }

        private void Start() {
            HideLoading();
            UpdateSelectedTabButtonVisual();
        }

        private void Update() {
            float rotationSpeed = 360f;
            loadingImageRectTransform.eulerAngles += new Vector3(0, 0, Time.deltaTime * -rotationSpeed);
        }

        private void HideAllContainers() {
            simplePromptContainerGameObject.SetActive(false);
            instructionsContainerGameObject.SetActive(false);
            searchWebContainerGameObject.SetActive(false);
            analyzeImageContainerGameObject.SetActive(false);
            askAboutFileContainerGameObject.SetActive(false);
            generateImageContainerGameObject.SetActive(false);
            withChatPromptIdContainerGameObject.SetActive(false);
        }

        private void UpdateSelectedTabButtonVisual() {
            Color normalColor = Color.black;
            Color selectedColor = new Color(0, .5f, 1f);
            simplePromptButton.GetComponent<Image>().color = simplePromptContainerGameObject.activeSelf ? selectedColor : normalColor;
            instructionsButton.GetComponent<Image>().color = instructionsContainerGameObject.activeSelf ? selectedColor : normalColor;
            searchWebButton.GetComponent<Image>().color = searchWebContainerGameObject.activeSelf ? selectedColor : normalColor;
            analyzeImageButton.GetComponent<Image>().color = analyzeImageContainerGameObject.activeSelf ? selectedColor : normalColor;
            askAboutFileButton.GetComponent<Image>().color = askAboutFileContainerGameObject.activeSelf ? selectedColor : normalColor;
            generateImageButton.GetComponent<Image>().color = generateImageContainerGameObject.activeSelf ? selectedColor : normalColor;
            withChatPromptIdButton.GetComponent<Image>().color = withChatPromptIdContainerGameObject.activeSelf ? selectedColor : normalColor;
        }

        public void ShowLoading() {
            loadingImageRectTransform.gameObject.SetActive(true);
            BlockerUI.Show();
        }

        public void HideLoading() {
            loadingImageRectTransform.gameObject.SetActive(false);
            BlockerUI.Hide();
        }


    }

}