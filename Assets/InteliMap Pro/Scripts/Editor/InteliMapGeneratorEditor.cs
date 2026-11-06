using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace InteliMapPro
{
    [CustomEditor(typeof(InteliMapGenerator))]
    public class InteliMapGeneratorEditor : Editor
    {
        private InteliMapGenerator mg;

        private void OnEnable()
        {
            mg = (InteliMapGenerator)target;

            EditorApplication.update += () => mg.fillCoroutine?.MoveNext();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUILayout.Space(10.0f);

            if (GUILayout.Button("Clear Bounds"))
            {
                RecordMapUndo();

                mg.ClearBounds();
            }
            if (GUILayout.Button("Generate"))
            {
                RecordMapUndo();

                try
                {
                    mg.StartGeneration();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError(ex.Message);
                }
            }
            if (GUILayout.Button("Clear and Generate"))
            {
                RecordMapUndo();

                mg.ClearBounds();
                try
                {
                    mg.StartGeneration();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError(ex.Message);
                }
            }

            GUILayout.Space(10.0f);

            if (mg.generatorData == null)
            {
                EditorGUILayout.HelpBox($"WARNING: This generator has no generator data assigned to it. You can build generator data using the InteliMapBuilder component.", MessageType.Warning);
            }
            else
            {
                if (mg.mapToFill == null)
                {
                    EditorGUILayout.HelpBox($"WARNING: Empty mapToFill. You must specific the map to fill for generation.", MessageType.Warning);
                }
                else if (mg.mapToFill.Count != mg.generatorData.layerCount)
                {
                    EditorGUILayout.HelpBox($"WARNING: Invalid mapToFill. This generator is built for {mg.generatorData.layerCount} layers, but the mapToFill includes {mg.mapToFill.Count} layers.", MessageType.Warning);
                }

                GUILayout.Label($"Generator Info:");
                GUILayout.Label($"      {mg.generatorData.layerCount} layers.");
                GUILayout.Label($"      {mg.NumUniqueTiles()} unique tiles.");
                GUILayout.Label($"      Neighborhood radius of {mg.GetNeighborhoodRadius()}.");
                GUILayout.Label($"      {mg.GetParameterCount()} total parameters.");
                GUILayout.Label($"      {mg.generatorData.weights.epochsTrained} total epochs trained.");
                GUILayout.Label($"      Connectivity: {ConnectivityData.GetConnectivityTypeString(mg.generatorData.connectivityType)}");
                GUILayout.Label($"      Positional Inputs: X: [" + (mg.generatorData.weights.useXPositionAsInput ? "X" : "_") + "] Y: [" + (mg.generatorData.weights.useYPositionAsInput ? "X" : "_") + "]");
                GUILayout.Label($"      Acknowledges bounds: {mg.generatorData.weights.acknowledgeBounds}.");
                GUILayout.Label($"      Enforces border connectivity: {mg.generatorData.borderConnectivity.enforceConnectivity}.");
            }
        }

        private void RecordMapUndo()
        {
            for (int layer = 0; layer < mg.generatorData.layerCount; layer++)
            {
                Undo.RecordObject(mg.mapToFill[layer], mg.mapToFill[layer].name);
            }
        }
    }
}