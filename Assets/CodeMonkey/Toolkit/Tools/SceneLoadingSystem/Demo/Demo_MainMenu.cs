using UnityEngine;
using UnityEngine.UI;

namespace CodeMonkey.Toolkit.TSceneLoadingSystem.Demo {

    public class Demo_MainMenu : MonoBehaviour {


        [SerializeField] private Button playButton;


        private void Awake() {
            playButton.onClick.AddListener(() => {
                SceneLoader.Load(SceneLoader.Scene.Demo_GameScene);
            });
        }

    }

}