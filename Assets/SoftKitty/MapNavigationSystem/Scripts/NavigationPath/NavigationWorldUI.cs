using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty.MasterNavigationMap
{

    public class NavigationWorldUI : MonoBehaviour
    {
        public LineRenderer Line;
        void Awake()
        {
            MapManager.NavigationPathCallback += OnNavigationPathUpdate;
            Line.material.SetColor("_TintColor", MapManager.CurrentSetting.NavigationLineColor);
        }

  
        public void OnNavigationPathUpdate(List<Vector3> _path, bool _updateAll)
        {
            if(_path.Count == 0)
            {
                Line.enabled = false;
                Line.positionCount = 0;
                return;
            }
           
            if (_updateAll == true)
            {
                Line.positionCount = _path.Count;
                Line.SetPositions(_path.ToArray());
            }
            else
            {
                Line.SetPosition(0, _path[0]);
                Line.SetPosition(Line.positionCount - 1, _path[_path.Count - 1]);
            }
            Line.enabled = true;
        }


        private void OnDestroy()
        {
            MapManager.NavigationPathCallback -= OnNavigationPathUpdate;
        }
    }
}

