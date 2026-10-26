using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace SoftKitty.MasterNavigationMap
{
    public class DemoControl : MonoBehaviour
    {
        public Transform Player;
        public Transform TestTarget;
        public GameObject WorldMap;
        public MapPoint QuestNPC;

        private void Awake()
        {
            MapManager.SetPlayer(Player);
        }
        void Start()
        {
            Application.targetFrameRate = 60;
            QuestNPC.State = 1;
            if (File.Exists(Application.dataPath+ "/CustomMarkerSave.txt"))
            {
                string _json = File.ReadAllText(Application.dataPath + "/CustomMarkerSave.txt", System.Text.Encoding.UTF8);
                MapManager.ImportCustomMarkerJsonData(_json);
            }
        }

         
        void Update()
        {
            if (InputProxy.GetKeyDown(KeyCode.Tab))WorldMap.SetActive(!WorldMap.activeSelf);
        }

        private void OnDestroy()
        {
            string _json = MapManager.ExportCustomMarkerJsonData();
            File.WriteAllText(Application.dataPath + "/CustomMarkerSave.txt", _json, System.Text.Encoding.UTF8);
        }

    }
}
