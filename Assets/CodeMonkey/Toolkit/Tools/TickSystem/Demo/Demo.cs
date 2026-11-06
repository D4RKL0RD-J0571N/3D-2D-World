using TMPro;
using UnityEngine;


namespace CodeMonkey.Toolkit.TTickSystem.Demo {

    public class Demo : MonoBehaviour {


        [SerializeField] private TextMeshProUGUI tickTextMesh;
        [SerializeField] private TextMeshProUGUI tick2TextMesh;
        [SerializeField] private TextMeshProUGUI tick5TextMesh;
        [SerializeField] private TextMeshProUGUI tick10TextMesh;


        private void Awake() {
            TickSystem.OnTick += (object send, TickSystem.OnTickEventArgs e) => {
                tickTextMesh.text = "TICK " + e.tick + "\n" + tickTextMesh.text;
            };
            TickSystem.OnTick_2 += (object send, TickSystem.OnTickEventArgs e) => {
                tick2TextMesh.text = "TICK2 " + e.tick + "\n" + tick2TextMesh.text;
            };
            TickSystem.OnTick_5 += (object send, TickSystem.OnTickEventArgs e) => {
                tick5TextMesh.text = "TICK5 " + e.tick + "\n" + tick5TextMesh.text;
            };
            TickSystem.OnTick_10 += (object send, TickSystem.OnTickEventArgs e) => {
                tick10TextMesh.text = "TICK10 " + e.tick + "\n" + tick10TextMesh.text;
            };
        }


    }

}