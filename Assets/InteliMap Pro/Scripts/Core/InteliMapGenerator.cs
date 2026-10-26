using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Threading;

namespace InteliMapPro
{
    public enum MultiLayeredMode
    {
        Additive, // may add new tiles on additional layers
        Static, // if initially that tile exactly fits a unique tile, it will only collapse to that
    }

    public enum UnrecognizedMode
    {
        Replace,
        Ignore
    }

    public class InteliMapGenerator : MonoBehaviour
    {
        [Header("General")]
        [Tooltip("All the stored data of the generator. This contains information about it's settings, weights, connectivity, etc..")]
        public GeneratorData generatorData;
        [Tooltip("The tilemap to fill by generating. Each seperate entry into the list should be a different layer of the map to fill.")]
        public List<Tilemap> mapToFill;
        [Tooltip("The bounds of the given tilemap to fill by generating. Already placed tiles will be incorporated into the new generation in-place.")]
        public BoundsInt boundsToFill = new BoundsInt(new Vector3Int(0, 0), new Vector3Int(25, 25, 1));

        [Header("Settings")]
        [Tooltip("Wether to start generation upon the scene starting.")]
        public bool generateOnStart = true;
        [Tooltip("When this is set to true, if the generator encounters an area impossible to generate in, instead of throwing an exception, it will forcefully generate by changing some of the previously set tiles.")]
        public bool forceful = true;
        [Tooltip("How this generator will handle existing tiles in a multi-layered tilemap. This setting does not affect single layered tilemaps. Static: if initially that tile exactly fits a unique tile, it will only collapse to that. Additive: may add new tiles on additional layers. ")]
        public MultiLayeredMode multiLayeredMode;
        [Tooltip("How this generator will handle unrecognized tiles. Replace: attempts to replace that tile with one the generator deems valid. Ignore: completely ignores the unrecognized tile and generates around it.")]
        public UnrecognizedMode unrecognizedMode;
        [Tooltip("Positive values of this will likely result in more random generation, while negative values may result in more consistent generation.")]
        public float temperature = 0;
        [Tooltip("What percentage of tiles should be collapsed in a random order instead of collapsing what the AI is most confident in. Increase this number if there are undesireable repeating patterns. Decreasing this number may result in more accurate generation.")]
        [Range(0f, 1f)]
        public float randomOrder = 0.1f;

        [Header("Timeout")]
        [Tooltip("If the generation should be aborted if it takes too long.")]
        public bool useTimeout = true;
        [Tooltip("How long to wait in seconds until the generation is aborted.")]
        public float timeoutSeconds = 20.0f;

        [Header("Animation")]
        [Tooltip("If this is true, the map will be filled one tile at a time instead of all at once.")]
        public bool animated = false;
        [Tooltip("How many tiles will be placed per second if this generator is set to animated.")]
        public float tilesPerSecond = 30.0f;

        public int NumUniqueTiles() { return generatorData.uniqueTiles.Length; }
        public int GetNeighborhoodRadius() { return generatorData.weights.GetNeighborhoodRadius(); }
        public int GetParameterCount() { return generatorData.weights.GetParameterCount(); }

        public IEnumerator fillCoroutine;

        private Thread asyncThread;
        private BoundsInt asyncBounds;
        private GeneratorGenerationEngine asyncGge;
        private int[] asyncMapIndicies;

        private Dictionary<TileBase, SparseSet>[] tileAtLayerToDomain;

        void Start()
        {
            SetupTileAtLayerToDomain();

            if (generateOnStart)
            {
                StartGeneration();
            }
        }

        private void SetupTileAtLayerToDomain()
        {
            if (tileAtLayerToDomain == null && generatorData != null)
            {
                tileAtLayerToDomain = new Dictionary<TileBase, SparseSet>[generatorData.layerCount];
                for (int layer = 0; layer < generatorData.layerCount; layer++)
                {
                    tileAtLayerToDomain[layer] = new Dictionary<TileBase, SparseSet>();

                    for (int i = 0; i < generatorData.uniqueTiles.Length; i++)
                    {
                        if (generatorData.uniqueTiles[i].tiles[layer] != null)
                        {
                            SparseSet outSet;

                            if (tileAtLayerToDomain[layer].TryGetValue(generatorData.uniqueTiles[i].tiles[layer], out outSet))
                            {
                                outSet.Add(i);
                            }
                            else
                            {
                                outSet = new SparseSet(generatorData.uniqueTiles.Length, false);
                                outSet.Add(i);
                                tileAtLayerToDomain[layer].Add(generatorData.uniqueTiles[i].tiles[layer], outSet);
                            }
                        }
                    }
                }
            }
        }

        /**
         * Clears the area of the mapToFill within the boundsToFill.
         */
        public void ClearBounds()
        {
            foreach (Tilemap map in mapToFill)
            {
                map.SetTilesBlock(boundsToFill, new TileBase[boundsToFill.size.x * boundsToFill.size.y]);
            }
        }

        /**
         * Starts the map generation with the given seed syncronously.
         */
        public void StartGenerationWithSeed(int seed)
        {
            Random.State before = Random.state;

            Random.InitState(seed);
            StartGeneration();

            Random.state = before;
        }

        /**
         * Starts the map generation with the given seed asynchronously.
         */
        public void StartGenerationAsyncWithSeed(int seed)
        {
            Random.State before = Random.state;

            Random.InitState(seed);
            StartGenerationAsync();

            Random.state = before;
        }

        /**
         * Starts the map generation syncronously.
         */
        public void StartGeneration()
        {
            if (!GenerationChecks())
            {
                return;
            }

            // maps each position in the grid to a dictionary containing a position of what else is in conflict, and that positions current index. If the index at that position isn't the corresponding value in the dictionary, it is not in conflict.
            SparseSet[,] domains = new SparseSet[boundsToFill.size.x, boundsToFill.size.y];

            Priority[,] priorities = new Priority[boundsToFill.size.x, boundsToFill.size.y];
            int[] mapIndicies = GetMapIndiciesAndSetDomains(domains, priorities);

            GeneratorGenerationEngine gge = new GeneratorGenerationEngine(mapIndicies, domains, priorities, boundsToFill, generatorData, new System.Random(Random.Range(int.MinValue, int.MaxValue)), temperature);

            Thread generateThread = new Thread(() => {
                mapIndicies = gge.Generate(forceful, randomOrder);
            });
            generateThread.Priority = System.Threading.ThreadPriority.Highest;
            generateThread.Start();

            System.DateTime start = System.DateTime.Now;

            while (generateThread.IsAlive)
            {
                if (useTimeout && System.DateTime.Now.Subtract(start).TotalSeconds > timeoutSeconds)
                {
                    generateThread.Abort();
                    Debug.LogError($"ABORTED. Generation time exceeded timeout maximum of {timeoutSeconds} seconds.");
                    return;
                }
            }

            if (mapIndicies != null)
            {
                if (!animated)
                {
                    FillEntireMap(mapIndicies, boundsToFill);
                }
                else
                {
                    fillCoroutine = FillMapTileByTile(mapIndicies, gge.GetCollapseOrder(), boundsToFill);
                    StartCoroutine(fillCoroutine);
                }
            }
        }

        /**
         * Starts the map generation asyncronously.
         */
        public void StartGenerationAsync()
        {
            if (!GenerationChecks())
            {
                return;
            }

            asyncBounds = boundsToFill;

            // maps each position in the grid to a dictionary containing a position of what else is in conflict, and that positions current index. If the index at that position isn't the corresponding value in the dictionary, it is not in conflict.
            SparseSet[,] domains = new SparseSet[asyncBounds.size.x, asyncBounds.size.y];

            Priority[,] priorities = new Priority[asyncBounds.size.x, asyncBounds.size.y];
            int[] mapIndicies = GetMapIndiciesAndSetDomains(domains, priorities);

            asyncGge = new GeneratorGenerationEngine(mapIndicies, domains, priorities, asyncBounds, generatorData, new System.Random(Random.Range(int.MinValue, int.MaxValue)), temperature);

            asyncThread = new Thread(() => {
                asyncMapIndicies = asyncGge.Generate(forceful, randomOrder);
            });
            asyncThread.Start();
        }

        private void Update()
        {
            if (asyncThread != null && asyncThread.Join(System.TimeSpan.Zero))
            {
                if (!animated)
                {
                    FillEntireMap(asyncMapIndicies, asyncBounds);
                }
                else
                {
                    fillCoroutine = FillMapTileByTile(asyncMapIndicies, asyncGge.GetCollapseOrder(), asyncBounds);
                    StartCoroutine(fillCoroutine);
                }

                asyncThread = null;
                asyncGge = null;
            }
        }

        public void Build(List<Tilemap> mapToFill, GeneratorData data)
        {
            this.mapToFill = new List<Tilemap>(mapToFill);
            this.generatorData = data;

            tileAtLayerToDomain = null;
        }

        /**
         * Preforms a series of checks to see if the generator is in a valid state to begin generation.
         * Returns true if the generator is good to generate, returns false if the generator is unable to generate.
         */
        private bool GenerationChecks()
        {
            if (generatorData == null)
            {
                Debug.LogError("ERROR. This generator has no generator data assigned to it.");
                return false;
            }

            if (generatorData.uniqueTiles == null || generatorData.uniqueTiles.Length == 0)
            {
                Debug.LogError("ERROR. The internal list of unique tiles is empty.");
                return false;
            }

            if (mapToFill.Count != generatorData.layerCount)
            {
                Debug.LogError($"ERROR. This generator was built with {generatorData.layerCount} layers. But this generator's mapToFill has {mapToFill.Count} layers. These layer counts must match.");
                return false;
            }

            return true;
        }

        private int[] GetMapIndiciesAndSetDomains(SparseSet[,] domains, Priority[,] priorities)
        {
            SetupTileAtLayerToDomain();

            int[] mapIndicies = new int[boundsToFill.size.x * boundsToFill.size.y];

            Dictionary<LayeredTile, int> map = new Dictionary<LayeredTile, int>(new LayeredTileComparer());
            for (int i = 0; i < generatorData.uniqueTiles.Length; i++)
            {
                map[generatorData.uniqueTiles[i]] = i;
            }

            TileBase[][] mapTiles = new TileBase[generatorData.layerCount][];
            for (int layer = 0; layer < generatorData.layerCount; layer++)
            {
                mapTiles[layer] = mapToFill[layer].GetTilesBlock(boundsToFill);
            }

            for (int x = 0; x < boundsToFill.size.x; x++)
            {
                for (int y = 0; y < boundsToFill.size.y; y++)
                {
                    SparseSet domain = null;
                    priorities[x, y] = new Priority();

                    bool foundStatic = false;
                    if (multiLayeredMode == MultiLayeredMode.Static)
                    {
                        // if this tile exists exactly as a layered tile, match it to that
                        LayeredTile thisTile = new LayeredTile(generatorData.layerCount);

                        for (int layer = 0; layer < generatorData.layerCount; layer++)
                        {
                            thisTile.tiles[layer] = mapTiles[layer][x + y * boundsToFill.size.x];
                        }

                        int tileIdx;
                        if (map.TryGetValue(thisTile, out tileIdx))
                        {
                            domain = new SparseSet(generatorData.uniqueTiles.Length, false);
                            domain.Add(tileIdx);
                            foundStatic = true;
                        }
                    }

                    int uncollapsed = -1;
                    if (!foundStatic)
                    {
                        for (int layer = 0; layer < generatorData.layerCount; layer++)
                        {
                            TileBase tile = mapTiles[layer][x + y * boundsToFill.size.x];

                            SparseSet outDomain;
                            if (tile != null && tileAtLayerToDomain[layer].TryGetValue(tile, out outDomain))
                            {
                                if (domain == null)
                                {
                                    domain = outDomain.Clone();
                                }
                                else
                                {
                                    domain.Intersect(outDomain);
                                }
                            }
                            else if (tile != null && unrecognizedMode == UnrecognizedMode.Ignore)
                            {
                                uncollapsed = -2; // -2 indicates the tile is uncollapsed, but not to be collapsed
                                if (domain == null)
                                {
                                    domain = new SparseSet(generatorData.uniqueTiles.Length, false);
                                }
                                else
                                {
                                    domain.Clear();
                                }
                            }
                        }
                    }

                    if (domain == null || domain.Count == 0)
                    {
                        if (unrecognizedMode == UnrecognizedMode.Ignore && domain != null)
                        {
                            uncollapsed = -2; // -2 indicates the tile is uncollapsed, but not to be collapsed
                        }
                        else
                        {
                            domain = new SparseSet(generatorData.uniqueTiles.Length, true);
                        }
                    }
                    else
                    {
                        priorities[x, y].SetPriority((x == 0 || y == 0 || x == boundsToFill.size.x - 1 || y == boundsToFill.size.y - 1) ? 2 : 1, domain.Clone());
                    }

                    domains[x, y] = domain;

                    if (domain.Count == 1)
                    {
                        mapIndicies[x + y * boundsToFill.size.x] = domain.GetDense(0);

                        for (int layer = 0; layer < generatorData.layerCount; layer++)
                        {
                            mapToFill[layer].SetTile(new Vector3Int(x + boundsToFill.position.x, y + boundsToFill.position.y), generatorData.uniqueTiles[domain.GetDense(0)].tiles[layer]);
                        }
                    }
                    else
                    {
                        mapIndicies[x + y * boundsToFill.size.x] = uncollapsed;
                    }
                }
            }

            return mapIndicies;
        }

        private void FillEntireMap(int[] mapIndicies, BoundsInt bounds)
        {
            for (int layer = 0; layer < generatorData.layerCount; layer++)
            {
                TileBase[] block = new TileBase[bounds.size.x * bounds.size.y];
                for (int x = 0; x < bounds.size.x; x++)
                {
                    for (int y = 0; y < bounds.size.y; y++)
                    {
                        int idx = mapIndicies[x + y * bounds.size.x];

                        if (idx >= 0)
                        {
                            block[x + y * bounds.size.x] = generatorData.uniqueTiles[idx].tiles[layer];
                        }
                        else if (idx == -2)
                        {
                            block[x + y * bounds.size.x] = mapToFill[layer].GetTile(bounds.min + new Vector3Int(x, y, 0));
                        }
                    }
                }
                mapToFill[layer].SetTilesBlock(bounds, block);
            }
        }

        private IEnumerator FillMapTileByTile(int[] mapIndicies, Vector2Int[] ordering, BoundsInt bounds)
        {
            WaitForSeconds wait = new WaitForSeconds(1.0f / tilesPerSecond);

            for (int i = 0; i < ordering.Length; i++)
            {
                Vector2Int pos = ordering[i];
                Vector3Int coordinate = new Vector3Int(pos.x + bounds.position.x, pos.y + bounds.position.y, bounds.position.z);

                for (int layer = 0; layer < generatorData.layerCount; layer++)
                {
                    int idx = mapIndicies[pos.x + pos.y * bounds.size.x];

                    if (idx >= 0)
                    {
                        mapToFill[layer].SetTile(
                            coordinate,
                            generatorData.uniqueTiles[idx].tiles[layer]);
                        mapToFill[layer].RefreshTile(coordinate);
                    }
                    else if (idx == -2)
                    {
                        mapToFill[layer].SetTile(
                            coordinate,
                            mapToFill[layer].GetTile(new Vector3Int(pos.x, pos.y, 0)));
                        mapToFill[layer].RefreshTile(coordinate);
                    }
                }
                yield return wait;
            }
        }

        void OnDrawGizmosSelected()
        {
            if (mapToFill != null)
            {
                Gizmos.color = Color.blue;

                if (boundsToFill.size.z == 0)
                {
                    boundsToFill = new BoundsInt(boundsToFill.position, new Vector3Int(boundsToFill.size.x, boundsToFill.size.y, 1));
                }

                foreach (Tilemap map in mapToFill)
                {
                    if (map != null)
                    {
                        TileSelectionGizmos.DrawBounds(map, boundsToFill);
                    }
                }
            }
        }
    }
}