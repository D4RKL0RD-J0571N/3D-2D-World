using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodeMonkey.Toolkit.TSceneLoadingSystem {

    /// <summary>
    /// ** Scene Loading System **
    /// 
    /// Load Scenes easily with a Loading screen
    /// 
    /// Add all of your scenes to the SceneLoader.Scene enum, make sure the name matches PERFECTLY and is unique
    /// Also remember to add your scenes to the Build Scene list (plus the Loading scene), under File -> Build Profiles -> Scene List
    /// 
    /// If you want to make your own Loading scene you just need to attach the SceneLoaderCallback component
    /// </summary>
    public static class SceneLoader {


        private class SceneLoadingMonoBehaviour : MonoBehaviour { }


        // Add your scenes here, make sure the name matches perfectly
        public enum Scene {
            Loading,
            Demo_MainMenu,
            Demo_GameScene,
        }


        private static Action onLoaderCallback;
        private static AsyncOperation loadingAsyncOperation;


        public static void Load(Scene scene) {
            // Set the loader callback action to load the target scene
            onLoaderCallback = () => {
                GameObject loadingGameObject = new GameObject("Scene Loading Game Object");
                loadingGameObject.AddComponent<SceneLoadingMonoBehaviour>().StartCoroutine(LoadSceneAsync(scene));
            };

            // Load the loading scene
            SceneManager.LoadScene(Scene.Loading.ToString());
        }

        private static IEnumerator LoadSceneAsync(Scene scene) {
            yield return null;

            loadingAsyncOperation = SceneManager.LoadSceneAsync(scene.ToString());

            while (!loadingAsyncOperation.isDone) {
                yield return null;
            }
        }

        public static float GetLoadingProgress() {
            if (loadingAsyncOperation != null) {
                return loadingAsyncOperation.progress;
            } else {
                return 1f;
            }
        }

        public static void LoaderCallback() {
            // Triggered after the first LateUpdate which lets the screen refresh
            // Execute the loader callback action which will load the target scene
            if (onLoaderCallback != null) {
                onLoaderCallback();
                onLoaderCallback = null;
            }
        }
    }

}