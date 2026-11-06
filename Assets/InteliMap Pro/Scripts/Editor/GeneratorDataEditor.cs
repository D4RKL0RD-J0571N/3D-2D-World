using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace InteliMapPro
{
    [CustomEditor(typeof(GeneratorData))]
    public class GeneratorDataEditor : Editor
    {
        private GeneratorData gd;

        private void OnEnable()
        {
            gd = (GeneratorData)target;
        }

        public override void OnInspectorGUI()
        {
            GUILayout.Label($"Generator Info:");
            GUILayout.Label($"      {gd.layerCount} layers.");
            GUILayout.Label($"      {gd.uniqueTiles.Length} unique tiles.");
            GUILayout.Label($"      Neighborhood radius of {gd.weights.GetNeighborhoodRadius()}.");
            GUILayout.Label($"      {gd.weights.GetParameterCount()} total parameters.");
            GUILayout.Label($"      {gd.weights.epochsTrained} total epochs trained.");
            GUILayout.Label($"      Connectivity: {ConnectivityData.GetConnectivityTypeString(gd.connectivityType)}");
            GUILayout.Label($"      Positional Inputs: X: [" + (gd.weights.useXPositionAsInput ? "X" : "_") + "] Y: [" + (gd.weights.useYPositionAsInput ? "X" : "_") + "]");
            GUILayout.Label($"      Acknowledges bounds: {gd.weights.acknowledgeBounds}.");
            GUILayout.Label($"      Enforces border connectivity: {gd.borderConnectivity.enforceConnectivity}.");

            GUILayout.Space(20);

            base.OnInspectorGUI();
        }
    }
}