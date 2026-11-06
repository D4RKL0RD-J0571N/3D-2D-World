using UnityEngine;
using UnityEngine.UI;

namespace CodeMonkey.Toolkit.TSceneLoadingSystem.Demo {

    public class Demo_GameScene : MonoBehaviour {


        [SerializeField] private Button mainMenuButton;


        private void Awake() {
            mainMenuButton.onClick.AddListener(() => {
                SceneLoader.Load(SceneLoader.Scene.Demo_MainMenu);
            });
        }

    }

}