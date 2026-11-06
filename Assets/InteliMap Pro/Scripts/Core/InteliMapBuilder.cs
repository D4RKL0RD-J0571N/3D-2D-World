using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum GeneratorBuildResult
{
    None,
    InProgress,

    // Warning Messages
    Cancelled,

    // Success Messages
    Success,

    // Error Messages
    NanError,
    MismatchedLayers,
    ZeroMaps,
    NullMaps,
    InvalidCommonality
}

namespace InteliMapPro
{
    public class InteliMapBuilder : MonoBehaviour
    {
        [Header("General")]
        [Tooltip("How many times to analyze the build maps. Higher values will result in longer build times and more accurate generation.")]
        public int epochs = 1000;
        [Tooltip("The generator data to train, leave this empty if you want to create a new generator.")]
        public GeneratorData generator;
        [Tooltip("The size of a tiles 'neighborhood'. A tiles neighborhood is all the nearby tiles that are relevent to deciding what that tile is going to be. Ex. A radius of 1 implies a 3x3 area, a radius of 2 implies a 5x5 area, etc...")]
        [Range(1, 10)] public int neighborhoodRadius = 2;

        [Header("Maps")]
        [Tooltip("The list of Tilemaps to analyze and build the generator from.")]
        public List<GeneratorMap> buildMaps;

        [Serializable]
        public class GeneratorAdvanced
        {
            [Tooltip("How to enforce which tiles can connect to which other tiles. " +
                "Four way connectivity means only the orthogonal connections are enforced, " +
                "eight way means diagonal connections are also enforced. Hexagonal connectivity should be used on hexagonal grids." +
                "Extended connectivities not only connect to adjacent tiles, but also one tile further. Thus extended four way " +
                "connects to eight total tiles in the orthogonal directions and extended eight way connects to twenty four total " +
                "tiles in the surrounding area.")]
            public ConnectivityType connectivity = ConnectivityType.EightWay;
            [Tooltip("Wether to enforce what tiles are allowed to be connected to the selected edges of the generation border.")]
            public DirectionalBools enforceBorderConnectivity;
            [Space(10)]
            [Tooltip("Wether to interpret empty tiles as intentionally empty tiles. If this is true, then empty tiles may be placed during generation; if it is false, then empty tiles will never be placed during generation.")]
            public bool interpretEmptyAsTile = false;
            [Space(10)]
            [Tooltip("Wether to use the x position of a tile as an input for the machine learning model. If this is set to true, the model may learn to associate certain x positions with certain tiles or structures.")]
            public bool useXPositionAsInput = false;
            [Tooltip("Wether to use the y position of a tile as an input for the machine learning model. If this is set to true, the model may learn to associate certain y positions with certain tiles or structures.")]
            public bool useYPositionAsInput = false;
            [Tooltip("What boundaries of the generation bounds to use as an input during training. This will cause generators built with this option set to true to correlate the selected boundaries of the generation with structures that are seen around the selected boundaries.")]
            public DirectionalBools acknowledgeBounds;
        }

        [Serializable]
        public class TrainingAdvanced
        {
            [Tooltip("How many threads will be used for the purposes of training the generator. Higher numbers should result in faster training times, each thread essentially trains an epoch concurrently. Note increasing this number is only effective if your system has enough resources for all threads. Set this value to 1 for single threaded training.")]
            public int trainingThreads = 16;
            [Tooltip("The system priority to use for the training threads. Higher values may increase training speed.")]
            public System.Threading.ThreadPriority trainingThreadPriority = System.Threading.ThreadPriority.Normal;
            [Space(10)]
            [Tooltip("The starting learning rate of the machine learning model. Higher values may result in faster generation, but going too high may result in unexpected behaviour. This value is logarithmically interpolated with the End Learning Rate throughout the build.")]
            public float startLearningRate = 0.05f;
            [Tooltip("The ending learning rate of the machine learning model. In most cases this should be lower than the starting learning rate. This value is logarithmically interpolated with the Start Learning Rate throughout the build.")]
            public float endLearningRate = 0.0005f;
        }

        [Header("Advanced")]
        [Tooltip("A collection of all the advanced settings related to the generator for the InteliMapBuilder.")]
        public GeneratorAdvanced generatorSettings;
        [Tooltip("A collection of all the advanced settings related to the training process for the InteliMapBuilder.")]
        public TrainingAdvanced trainingSettings;

        [HideInInspector] public int epoch;
        [HideInInspector] public int totalEpochs;
        [HideInInspector] public float lossLastIteration;
        [HideInInspector] public float avgLossLast20Epochs;
        [HideInInspector] public float currentLearningRate;
        [HideInInspector] public DateTime startTime;
        [HideInInspector] public DateTime endTime;
        [HideInInspector] public GeneratorBuildResult buildResult = GeneratorBuildResult.None;

        [HideInInspector] private Thread trainThread;
        [HideInInspector] private bool shouldSaveAndQuit;

        public void OnEnable()
        {
            buildResult = GeneratorBuildResult.None;
        }

        /**
         * Cancels the current build if there is one running. Note that if a generator was already overwritten it can not be retrieved by canceling the build.
         */
        public void CancelBuild()
        {
            if (trainThread != null)
            {
                trainThread.Abort();
            }

            buildResult = GeneratorBuildResult.Cancelled;
        }

        /**
         * Save and quits the current build if there is one running. It will wait until the end of the current epoch to stop.
         */
        public void SaveAndQuitBuild()
        {
            shouldSaveAndQuit = true;
        }

        /**
         * Builds a generator according to the attributes of this builder.
         */
        public void Build()
        {
#if UNITY_EDITOR
            shouldSaveAndQuit = false;

            if (buildMaps.Count == 0)
            {
                buildResult = GeneratorBuildResult.ZeroMaps;
                return;
            }

            int layerCount = 0;
            float totalCommonality = 0.0f;
            for (int i = 0; i < buildMaps.Count; i++)
            {
                if (buildMaps[i].mapLayers == null || buildMaps[i].mapLayers.Count == 0)
                {
                    buildResult = GeneratorBuildResult.NullMaps;
                    return;
                }
                if (buildMaps[i].commonality < 0.0f)
                {
                    buildResult = GeneratorBuildResult.InvalidCommonality;
                    return;
                }

                if (buildMaps[i].bounds.size.z == 0)
                {
                    buildMaps[i].bounds = new BoundsInt(buildMaps[i].bounds.position, new Vector3Int(buildMaps[i].bounds.size.x, buildMaps[i].bounds.size.y, 1));
                }

                if (i == 0)
                {
                    layerCount = buildMaps[i].mapLayers.Count;
                }
                else
                {
                    if (buildMaps[i].mapLayers.Count != layerCount)
                    {
                        buildResult = GeneratorBuildResult.MismatchedLayers;
                        return;
                    }
                }

                totalCommonality += buildMaps[i].commonality;
            }
            if (totalCommonality <= 0.0f)
            {
                buildResult = GeneratorBuildResult.InvalidCommonality;
                return;
            }

            // Collect all the tile data
            BoundsInt[] bounds = new BoundsInt[buildMaps.Count];
            int[][] tileIndicies = new int[buildMaps.Count][];

            for (int i = 0; i < buildMaps.Count; i++)
            {
                bounds[i] = GetBoundsOfBuildMap(buildMaps[i]);
                tileIndicies[i] = new int[bounds[i].size.x * bounds[i].size.y];
            }

            GeneratorData generatorData;

            if (generator == null)
            {
                generatorData = ScriptableObject.CreateInstance<GeneratorData>();

                string generatorFolderPath = Application.dataPath;

                string[] generatorsGuids = AssetDatabase.FindAssets("t:GeneratorData");
                if (generatorsGuids.Length > 0)
                {
                    generatorFolderPath = AssetDatabase.GUIDToAssetPath(generatorsGuids[0]);
                    generatorFolderPath = generatorFolderPath.Substring(0, generatorFolderPath.Length - AssetDatabase.LoadAssetAtPath<GeneratorData>(generatorFolderPath).name.Length - 6); // 6 for .asset
                }

                string filename = EditorUtility.SaveFilePanelInProject("Select where to save the Generator Data", "New Generator.asset", "asset", "Enter a file name for the generator to be saved to.", generatorFolderPath);
                if (filename.Length == 0)
                {
                    return;
                }

                generatorData.layerCount = layerCount;
                generatorData.uniqueTiles = GetUniqueTilesAndIndicies(bounds, tileIndicies, layerCount);
                generatorData.weights = new GeneratorWeights(generatorData.uniqueTiles.Length, neighborhoodRadius, generatorSettings.useXPositionAsInput, generatorSettings.useYPositionAsInput, generatorSettings.acknowledgeBounds);
                totalEpochs = 0;

                generator = generatorData;

                AssetDatabase.CreateAsset(generatorData, filename);
                EditorUtility.SetDirty(generatorData);
            }
            else
            {
                generatorData = generator;
                totalEpochs = generatorData.weights.epochsTrained;

                LayeredTile[] tileArray = GetUniqueTilesAndIndicies(bounds, tileIndicies, layerCount);

                if (tileArray.Length != generatorData.uniqueTiles.Length)
                {
                    // Reset weights if unique tiles have changed
                    generatorData.weights = new GeneratorWeights(tileArray.Length, neighborhoodRadius, generatorSettings.useXPositionAsInput, generatorSettings.useYPositionAsInput, generatorSettings.acknowledgeBounds);
                    totalEpochs = 0;
                }
                else
                {
                    for (int i = 0; i < tileArray.Length; i++)
                    {
                        if (!tileArray[i].Equals(generatorData.uniqueTiles[i]))
                        {
                            // Reset weights if unique tiles have changed
                            generatorData.weights = new GeneratorWeights(tileArray.Length, neighborhoodRadius, generatorSettings.useXPositionAsInput, generatorSettings.useYPositionAsInput, generatorSettings.acknowledgeBounds);
                            totalEpochs = 0;
                            break;
                        }
                    }
                }

                generatorData.uniqueTiles = tileArray;
            }

            ConnectivityData connectivityData;
            GetConnectivityData(bounds, tileIndicies, generatorData.uniqueTiles.Length, out connectivityData, out generatorData.borderConnectivity);
            generatorData.connectivityType = generatorSettings.connectivity;
            generatorData.connectivityData = connectivityData.GetConnectivityArray();

            buildResult = GeneratorBuildResult.InProgress;
            startTime = DateTime.Now;

            trainThread = new Thread(() =>
            {
                TrainWeights(bounds, tileIndicies, totalCommonality, generatorData);
            });
            trainThread.Priority = System.Threading.ThreadPriority.Highest;
            trainThread.Start();
#endif
        }

        // Returns an array of all the unique tiles. Also fills the tileIndicies with the indicies of each tile in the unique list.
        // Also generates an array of how many times each tile occured. And generates CSP Connectivity.
        private LayeredTile[] GetUniqueTilesAndIndicies(BoundsInt[] bounds, int[][] tileIndicies, int layerCount)
        {
            // Maps each tile to its corresponding index in the eventually returned unique array
            Dictionary<LayeredTile, int> uniqueTiles = new Dictionary<LayeredTile, int>(new LayeredTileComparer());

            for (int i = 0; i < buildMaps.Count; i++)
            {
                TileBase[][] tiles = new TileBase[layerCount][];
                for (int layer = 0; layer < layerCount; layer++)
                {
                    tiles[layer] = buildMaps[i].mapLayers[layer].GetTilesBlock(bounds[i]);
                }

                for (int x = 0; x < bounds[i].size.x; x++)
                {
                    for (int y = 0; y < bounds[i].size.y; y++)
                    {
                        TileBase[] tileArr = new TileBase[layerCount];
                        for (int layer = 0; layer < layerCount; layer++)
                        {
                            tileArr[layer] = tiles[layer][x + y * bounds[i].size.x];
                        }
                        LayeredTile tile = new LayeredTile(tileArr);

                        if (!tile.IsEmpty())
                        {
                            int outIndex;
                            if (uniqueTiles.TryGetValue(tile, out outIndex))
                            {
                                tileIndicies[i][x + y * bounds[i].size.x] = outIndex;
                            }
                            else
                            {
                                tileIndicies[i][x + y * bounds[i].size.x] = uniqueTiles.Count;

                                uniqueTiles.Add(tile, uniqueTiles.Count);
                            }
                        }
                        else
                        {
                            tileIndicies[i][x + y * bounds[i].size.x] = -1;
                        }
                    }
                }
            }

            LayeredTile[] uniques = new LayeredTile[generatorSettings.interpretEmptyAsTile ? uniqueTiles.Count + 1 : uniqueTiles.Count];

            foreach (var pair in uniqueTiles)
            {
                uniques[pair.Value] = pair.Key;
            }

            if (generatorSettings.interpretEmptyAsTile)
            {
                uniques[uniqueTiles.Count] = new LayeredTile(layerCount);

                for (int i = 0; i < buildMaps.Count; i++)
                {
                    for (int x = 0; x < bounds[i].size.x; x++)
                    {
                        for (int y = 0; y < bounds[i].size.y; y++)
                        {
                            if (tileIndicies[i][x + y * bounds[i].size.x] == -1)
                            {
                                tileIndicies[i][x + y * bounds[i].size.x] = uniqueTiles.Count;
                            }
                        }
                    }
                }
            }

            return uniques;
        }

        private void GetConnectivityData(BoundsInt[] bounds, int[][] tileIndicies, int uniqueTileCount, out ConnectivityData connectivity, out BorderConnectivity borderConnectivity)
        {
            // Generate connectivitity
            switch (generatorSettings.connectivity)
            {
                case ConnectivityType.FourWay:
                    connectivity = new FourWayConnectivity(uniqueTileCount);
                    break;
                case ConnectivityType.EightWay:
                    connectivity = new EightWayConnectivity(uniqueTileCount);
                    break;
                case ConnectivityType.Hexagonal:
                    connectivity = new HexagonalConnectivity(uniqueTileCount);
                    break;
                default:
                    Debug.LogError("Invalid connectivity type, defaulting to FourWay");
                    connectivity = new FourWayConnectivity(uniqueTileCount);
                    break;
            }
            borderConnectivity = new BorderConnectivity(uniqueTileCount, generatorSettings.enforceBorderConnectivity);

            for (int i = 0; i < buildMaps.Count; i++)
            {
                for (int x = 0; x < bounds[i].size.x; x++)
                {
                    for (int y = 0; y < bounds[i].size.y; y++)
                    {
                        Vector2Int pos = new Vector2Int(x, y);
                        int index = tileIndicies[i][x + y * bounds[i].size.x];

                        if (index != -1)
                        {
                            for (int d = 0; d < connectivity.GetDirectionCount(); d++)
                            {
                                Vector2Int otherPos = pos + connectivity.GetConnectionOffset(d, pos, bounds[i].position.y);

                                if (IsInBounds(otherPos, bounds[i]))
                                {
                                    int otherIndex = tileIndicies[i][otherPos.x + otherPos.y * bounds[i].size.x];

                                    if (otherIndex != -1)
                                    {
                                        connectivity.SetConnectivity(index, otherIndex, d, true);
                                    }
                                }
                                else
                                {
                                    if (otherPos.x < 0)
                                    {
                                        borderConnectivity.SetLeftConnectivity(index, true);
                                    }
                                    if (otherPos.y < 0)
                                    {
                                        borderConnectivity.SetBottomConnectivity(index, true);
                                    }
                                    if (otherPos.x >= bounds[i].size.x)
                                    {
                                        borderConnectivity.SetRightConnectivity(index, true);
                                    }
                                    if (otherPos.y >= bounds[i].size.y)
                                    {
                                        borderConnectivity.SetTopConnectivity(index, true);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool IsInBounds(Vector2Int pos, BoundsInt bounds)
        {
            return pos.x >= 0 && pos.x < bounds.size.x && pos.y >= 0 && pos.y < bounds.size.y;
        }

        private float Logerp(float a, float b, float t)
        {
            return a * Mathf.Pow(b / a, t);
        }

        private void TrainWeights(BoundsInt[] bounds, int[][] tileIndicies, float totalCommonality, GeneratorData data)
        {
            float[] lastTwenty = new float[20];

            lossLastIteration = 0.0f;
            avgLossLast20Epochs = 0.0f;

            System.Random threadRand = new System.Random();

            for (epoch = 0; epoch < epochs && !shouldSaveAndQuit; epoch += trainingSettings.trainingThreads)
            {
                float epochT = (float)epoch / (float)epochs;
                currentLearningRate = Logerp(trainingSettings.startLearningRate, trainingSettings.endLearningRate, epochT);

                // Pick random map based on commonality
                float val = (float)threadRand.NextDouble() * totalCommonality;
                int chosenMap = buildMaps.Count - 1;
                for (int i = 0; i < buildMaps.Count; i++)
                {
                    val -= buildMaps[i].commonality;
                    if (val < 0.0f)
                    {
                        chosenMap = i;
                        break;
                    }
                }

                float[] lossArray = new float[trainingSettings.trainingThreads];

                Thread[] threads = new Thread[trainingSettings.trainingThreads];
                for (int i = 0; i < trainingSettings.trainingThreads; i++)
                {
                    int threadIndex = i;
                    threads[i] = new Thread(() =>
                    {
                        TrainSingleThread(chosenMap, bounds[chosenMap], tileIndicies[chosenMap], data, lossArray, threadIndex);
                    });
                    threads[i].Priority = trainingSettings.trainingThreadPriority;
                    threads[i].Start();
                }

                float iterationLoss = 0.0f;
                for (int i = 0; i < trainingSettings.trainingThreads; i++)
                {
                    threads[i].Join();
                    iterationLoss += lossArray[i];

                    lastTwenty[(epoch + i) % 20] = lossArray[i] / (bounds[chosenMap].size.x * bounds[chosenMap].size.y);
                }

                iterationLoss /= bounds[chosenMap].size.x * bounds[chosenMap].size.y * trainingSettings.trainingThreads;
                if (float.IsNaN(iterationLoss)) // returned an invalid loss value, therefore the training has been broken and should terminate
                {
                    buildResult = GeneratorBuildResult.NanError;
                    return;
                }

                lossLastIteration = iterationLoss;

                int cap = Mathf.Min(20, epoch + 1);
                float sum = 0.0f;
                for (int i = 0; i < cap; i++)
                {
                    sum += lastTwenty[i];
                }

                avgLossLast20Epochs = sum / cap;

                data.weights.epochsTrained += trainingSettings.trainingThreads;
                totalEpochs += trainingSettings.trainingThreads;
            }

            buildResult = GeneratorBuildResult.Success;

            endTime = DateTime.Now;
        }

        private void TrainSingleThread(int chosenMap, BoundsInt bounds, int[] tileIndicies, GeneratorData data, float[] lossArray, int threadIndex)
        {
            System.Random threadRand = new System.Random(threadIndex);

            GeneratorEngine engine = new GeneratorEngine(data.weights, threadRand, bounds, data.uniqueTiles.Length);

            int[] chosenIndicies = new int[bounds.size.x * bounds.size.y];
            for (int i = 0; i < chosenIndicies.Length; i++)
            {
                chosenIndicies[i] = -1;
            }

            engine.Reset(threadRand, chosenIndicies);

            float threadLoss = 0.0f;

            // Train the main mode
            while (!engine.IsDone())
            {
                Vector2Int pos = engine.NextPos();

                int idx = tileIndicies[pos.x + pos.y * bounds.size.x];
                if (idx != -1)
                {
                    threadLoss += engine.Train(pos, chosenIndicies, idx, currentLearningRate);
                    chosenIndicies[pos.x + pos.y * bounds.size.x] = idx;
                }
            }

            lossArray[threadIndex] = threadLoss;
        }

        private BoundsInt GetBoundsOfBuildMap(GeneratorMap map)
        {
            if (map.manualBounds)
            {
                return map.bounds;
            }

            // otherwise get the maximum extent of all the layers
            BoundsInt firstBounds = map.mapLayers[0].cellBounds;
            int furthestLeft = firstBounds.xMin;
            int furthestRight = firstBounds.xMax;
            int furthestDown = firstBounds.yMin;
            int furthestUp = firstBounds.yMax;
            for (int i = 1; i < map.mapLayers.Count; i++)
            {
                BoundsInt bounds = map.mapLayers[i].cellBounds;

                if (bounds.xMin < furthestLeft)
                {
                    furthestLeft = bounds.xMin;
                }
                if (bounds.xMax > furthestRight)
                {
                    furthestRight = bounds.xMax;
                }
                if (bounds.yMin < furthestDown)
                {
                    furthestDown = bounds.yMin;
                }
                if (bounds.yMax > furthestUp)
                {
                    furthestUp = bounds.yMax;
                }
            }

            return new BoundsInt(furthestLeft, furthestDown, 0, furthestRight - furthestLeft + 1, furthestUp - furthestDown + 1, 1);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            if (buildMaps != null)
            {
                foreach (GeneratorMap map in buildMaps)
                {
                    BoundsInt bounds = GetBoundsOfBuildMap(map);
                    foreach (Tilemap tilemap in map.mapLayers)
                    {
                        TileSelectionGizmos.DrawBounds(tilemap, bounds);
                    }
                }
            }
        }
    }
}