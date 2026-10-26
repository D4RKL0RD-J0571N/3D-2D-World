using UnityEditor;

namespace SoftKitty.MasterNavigationMap
{
    [CustomEditor(typeof(NavigationBar))]
    public class NavigationBar_Inspector : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            NavigationBar _t = (NavigationBar)target;
            _t.SetArrow();
        }


    }
}
