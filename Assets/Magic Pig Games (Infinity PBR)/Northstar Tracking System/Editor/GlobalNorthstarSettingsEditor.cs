#if UNITY_EDITOR
using UnityEditor;
using MagicPigGames.Northstar;
using static MagicPigGames.Shared.MagicPigEditor;

namespace MagicPigGames.Northstar
{
    [CustomEditor(typeof(GlobalNorthstarSettings))]
    public class GlobalNorthstarSettingsEditor : Editor
    {
        private GlobalNorthstarSettings Target;
        void OnEnable() => Target = (GlobalNorthstarSettings)target;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            Header1("Global Northstar Settings");

            Target.northAngle = FloatSlider("North Angle", Target.northAngle, -180f, 180f);
            Target.worldNorth = FloatSlider("World North", Target.worldNorth, 0f, 359.9f);

            EditorUtility.SetDirty(target);
        }
    }
}

#endif