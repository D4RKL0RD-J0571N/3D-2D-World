using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CodeMonkey.Toolkit.TSceneLoadingSystem {

    public class SceneLoaderCallback : MonoBehaviour {


        private bool isFirstLateUpdate = true;


        private void LateUpdate() {
            if (isFirstLateUpdate) {
                isFirstLateUpdate = false;
                SceneLoader.LoaderCallback();
            }
        }

    }

}