using CodeMonkey.Toolkit.TTickSystem;
using UnityEngine;

namespace CodeMonkey.Toolkit.TTickSystem {

    public class TickSystemMonoBehaviour : MonoBehaviour {


        private void Awake() {
            TickSystem.ResetTick();
        }

        private void Update() {
            TickSystem.Update(Time.unscaledDeltaTime);
        }

    }

}