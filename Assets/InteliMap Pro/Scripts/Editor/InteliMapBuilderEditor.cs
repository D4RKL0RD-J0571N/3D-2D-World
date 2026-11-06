using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;

namespace InteliMapPro
{
    [CustomEditor(typeof(InteliMapBuilder))]
    public class InteliMapBuilderEditor : Editor
    {
        private InteliMapBuilder sb;

        private void OnEnable()
        {
            sb = (InteliMapBuilder)target;
        }

        public override void OnInspectorGUI()
        {
            if (sb.buildResult == GeneratorBuildResult.InProgress)
            {
                if (GUILayout.Button("Cancel Build"))
                {
                    sb.CancelBuild();
                }
                if (GUILayout.Button("Save and Quit Build"))
                {
                    sb.SaveAndQuitBuild();
                }
            }
            else
            {
                if (sb.generator != null &&
                    (sb.neighborhoodRadius != sb.generator.weights.GetNeighborhoodRadius() ||
                    sb.generatorSettings.acknowledgeBounds != sb.generator.weights.acknowledgeBounds ||
                    sb.generatorSettings.useXPositionAsInput != sb.generator.weights.useXPositionAsInput ||
                    sb.generatorSettings.useYPositionAsInput != sb.generator.weights.useYPositionAsInput))
                {
                    EditorGUILayout.HelpBox("Note that some settings such as neighborhood radius, positional input, and acknowledge bounds cannot be changed once the generator is created. They will not be changed upon training the generator.", MessageType.Info);

                    if (GUILayout.Button("Restore Generator Settings"))
                    {
                        sb.neighborhoodRadius = sb.generator.weights.GetNeighborhoodRadius();
                        sb.generatorSettings.acknowledgeBounds = sb.generator.weights.acknowledgeBounds;
                        sb.generatorSettings.useXPositionAsInput = sb.generator.weights.useXPositionAsInput;
                        sb.generatorSettings.useYPositionAsInput = sb.generator.weights.useYPositionAsInput;
                    }

                    GUILayout.Space(20.0f);
                }

                base.OnInspectorGUI();

                GUILayout.Space(20.0f);

                if (GUILayout.Button("Build Generator"))
                {
                    sb.Build();
                }

                if (sb.generator != null && GUILayout.Button("Create Generator Component"))
                {
                    InteliMapGenerator gen = sb.gameObject.AddComponent<InteliMapGenerator>();
                    gen.Build(sb.buildMaps[0].mapLayers, sb.generator);
                }

                InteliMapGenerator generator = sb.gameObject.GetComponent<InteliMapGenerator>();

                if (sb.generator != null && generator != null && GUILayout.Button("Replace Generator Component"))
                {
                    generator.Build(sb.buildMaps[0].mapLayers, sb.generator);
                }
            }

            switch (sb.buildResult)
            {
                case GeneratorBuildResult.None:
                    GUILayout.Space(20.0f);
                    EditorGUILayout.HelpBox("Generator not yet built. Input some build maps then click 'Build Generator' to build the generator.", MessageType.None);
                    break;
                case GeneratorBuildResult.InProgress:
                    GUILayout.Space(20.0f);
                    EditorGUILayout.HelpBox(
                        "Build Stats:" +
                        "\nBuild in progress. " + sb.epoch + " / " + sb.epochs + " epochs." +
                        "\nTotal Epochs: " + sb.totalEpochs + " epochs." +
                        "\nLast 20 Epoch AVG Loss: " + sb.avgLossLast20Epochs +
                        "\nLast Iteration Loss: " + sb.lossLastIteration +
                        "\nTime Started: " + sb.startTime.ToString("dddd MMMM dd, HH:mm:ss") +
                        "\nTime Elapsed: " + DateTime.Now.Subtract(sb.startTime).ToString("hh\\:mm\\:ss") +
                        "\nCurrent Learning Rate: " + sb.currentLearningRate +
                        "\n\nSettings:" +
                        "\nUnique Tile Count: " + sb.generator.uniqueTiles.Length +
                        "\nConnectivity: " + ConnectivityData.GetConnectivityTypeString(sb.generatorSettings.connectivity) +
                        "\nNeighborhood Radius: " + sb.generator.weights.GetNeighborhoodRadius() + " [" + (sb.generator.weights.GetNeighborhoodRadius() * 2 + 1) + "x" + (sb.generator.weights.GetNeighborhoodRadius() * 2 + 1) + "]" +
                        "\nPositional Inputs: X: [" + (sb.generator.weights.useXPositionAsInput ? "X" : "_") + "] Y: [" + (sb.generator.weights.useYPositionAsInput ? "X" : "_") + "]" +
                        "\nAcknowledge Bounds: " + sb.generator.weights.acknowledgeBounds +
                        "\nEnforce Border Connectivity: " + sb.generator.borderConnectivity.enforceConnectivity, MessageType.None);
                    break;
                case GeneratorBuildResult.Cancelled:
                    GUILayout.Space(20.0f);
                    EditorGUILayout.HelpBox("WARNING. Build cancelled.", MessageType.Warning);
                    break;
                case GeneratorBuildResult.Success:
                    if (sb.endTime.Subtract(sb.startTime).Milliseconds > 0)
                    {
                        GUILayout.Space(20.0f);
                        EditorGUILayout.HelpBox("Successfully built generator.\n" +
                            "Time taken: " + sb.endTime.Subtract(sb.startTime).ToString("hh\\:mm\\:ss") +
                            "\nEpochs trained: " + sb.epoch + " epochs.", MessageType.Info);
                    }
                    break;
                case GeneratorBuildResult.NanError:
                    GUILayout.Space(20.0f);
                    EditorGUILayout.HelpBox("ERROR. BUILD TERMINATED. One of the internal learning mechanisms encountered a NaN value. This is likely a result of the learning rate being too high, lower it and retry.", MessageType.Error);
                    break;
                case GeneratorBuildResult.MismatchedLayers:
                    GUILayout.Space(20.0f);
                    EditorGUILayout.HelpBox("ERROR. Not all build maps have the same amount of layers. All build maps must have the same amount of layers.", MessageType.Error);
                    break;
                case GeneratorBuildResult.NullMaps:
                    GUILayout.Space(20.0f);
                    EditorGUILayout.HelpBox("ERROR. Some of the maps in the build maps are null, these must not be null.", MessageType.Error);
                    break;
                case GeneratorBuildResult.ZeroMaps:
                    GUILayout.Space(20.0f);
                    EditorGUILayout.HelpBox("ERROR. Zero maps inputted, there must be at least one map.", MessageType.Error);
                    break;
                case GeneratorBuildResult.InvalidCommonality:
                    GUILayout.Space(20.0f);
                    EditorGUILayout.HelpBox("ERROR. One or more of the build maps has an invalid commonality. No commonalities can be negative and at least one must be positive.", MessageType.Error);
                    break;
            }
        }
    }
}