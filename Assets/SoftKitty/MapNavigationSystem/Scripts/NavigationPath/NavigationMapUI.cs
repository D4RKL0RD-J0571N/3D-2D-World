using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace SoftKitty.MasterNavigationMap
{
    public class NavigationMapUI : MonoBehaviour
    {
        public UILineRenderer Line;
        void Start()
        {
            MapManager.NavigationPathCallback += OnNavigationPathUpdate;
            if(NavigationPath.instance!=null && NavigationPath.instance.TaskOnGoing)
            {
                OnNavigationPathUpdate(NavigationPath.instance.mPath,true);
            }
        }

       

        public void OnNavigationPathUpdate(List<Vector3> _path, bool _updateAll)
        {
            if (_path.Count == 0)
            {
                Line.enabled = false;
                Line.points = new Vector2[0];
                return;
            }
            Line.enabled = true;
            if (_updateAll == true || Line.points.Length==0)
            {
                Line.points = new Vector2[ _path.Count];
                Line.SetPositions(_path.ToArray());
            }
            else
            {
                Line.SetPosition(0, _path[0]);
                Line.SetPosition(Line.points.Length - 1, _path[_path.Count - 1]);
            }
        }

        private void OnDestroy()
        {
            MapManager.NavigationPathCallback -= OnNavigationPathUpdate;
        }
    }
}
