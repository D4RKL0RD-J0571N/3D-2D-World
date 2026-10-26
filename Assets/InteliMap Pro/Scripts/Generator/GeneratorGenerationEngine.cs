using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Jobs;

namespace InteliMapPro
{
    public class GeneratorGenerationEngine
    {
        public GeneratorGenerationEngine(
            int[] indicies, 
            SparseSet[,] domains, 
            Priority[,] priorities,
            BoundsInt bounds, 
            GeneratorData data,
            System.Random rand,
            float temperature)
        {
            this.uniqueTileCount = data.uniqueTiles.Length;
            this.indicies = indicies;
            this.domains = domains;
            this.priorities = priorities;

            this.weights = data.weights;
            this.bounds = bounds;

            this.rand = rand;
            this.temperature = temperature;

            // Connectivity
            switch (data.connectivityType)
            {
                case ConnectivityType.FourWay:
                    connectivity = new FourWayConnectivity(uniqueTileCount, data.connectivityData);
                    break;
                case ConnectivityType.EightWay:
                    connectivity = new EightWayConnectivity(uniqueTileCount, data.connectivityData);
                    break;
                case ConnectivityType.Hexagonal:
                    connectivity = new HexagonalConnectivity(uniqueTileCount, data.connectivityData);
                    break;
            }

            this.borderConnectivity = data.borderConnectivity;
        }

        private int uniqueTileCount;
        private int[] indicies;

        private GeneratorEngine engine;
        private LRVPriorityEngine lrvEngine;
        private System.Random rand;
        private int[,] engineCollapsedTo;

        private SparseSet[,] domains;
        private Priority[,] priorities; // priorities are only used when it's impossible to generate, and some assignments must be ignored

        private GeneratorWeights weights;
        private BoundsInt bounds;

        private float temperature;

        private ConnectivityData connectivity;
        private BorderConnectivity borderConnectivity;

        private Vector2Int[] collapseOrder;

        public int[] Generate(bool forceful, float percentRandom)
        {
            engine = new GeneratorEngine(weights, rand, bounds, uniqueTileCount);
            engine.Reset(rand, indicies);

            lrvEngine = new LRVPriorityEngine(domains, priorities, bounds, percentRandom);
            engineCollapsedTo = new int[bounds.size.x, bounds.size.y];

            if (AddPreexistingToMAC())
            {
                for (int i = 0; !engine.IsDone(); i++)
                {
                    Vector2Int next = engine.NextPos();

                    lrvEngine.Add(next);

                    engineCollapsedTo[next.x, next.y] = engine.PredictAndCollapse(next, -temperature);
                }

                DoCSPFilling();
            }
            else if (forceful)
            {
                for (int i = 0; !engine.IsDone(); i++)
                {
                    Vector2Int next = engine.NextPos();

                    engineCollapsedTo[next.x, next.y] = engine.PredictAndCollapse(next, -temperature);
                }

                // Set the entire map to blank and reset domains
                for (int x = 0; x < bounds.size.x; x++)
                {
                    for (int y = 0; y < bounds.size.y; y++)
                    {
                        indicies[x + y * bounds.size.x] = -1;

                        domains[x, y] = new SparseSet(uniqueTileCount, true);

                        lrvEngine.Add(new Vector2Int(x, y));
                    }
                }

                if (AddPreexistingToMAC())
                {
                    DoCSPFilling();
                }
                else
                {
                    Debug.LogError("Completely impossible to generate in the given area. Forceful will not fix this, this is likely due to border connectivity.");
                    return null;
                }
            }
            else
            {
                Debug.LogError("Impossible to generate in the given area. Set forceful to true to attempt to generate anyway.");
                return null;
            }

            return indicies;
        }

        public Vector2Int[] GetCollapseOrder()
        {
            return collapseOrder;
        }

        private bool AddPreexistingToMAC()
        {
            Queue<PosInt> frontier = new Queue<PosInt>();
            List<PosInt> domainValuesRemoved = new List<PosInt>();

            for (int x = 0; x < bounds.size.x; x++)
            {
                for (int y = 0; y < bounds.size.y; y++)
                {
                    EnqueueNext(new Vector2Int(x, y), frontier);
                }
            }

            while (frontier.Count > 0)
            {
                PosInt next = frontier.Dequeue();
                if (indicies[next.pos.x + next.pos.y * bounds.size.x] == -2)
                {
                    continue;
                }

                switch (next.index)
                {
                    case -1: // -1 for left bound
                        if (ReviseBorder(next, domainValuesRemoved, borderConnectivity.leftConnectivity))
                        {
                            if (domains[next.pos.x, next.pos.y].Count == 0)
                            {
                                return false; // a domain became 0, therefore stop
                            }

                            EnqueueNext(next.pos, frontier);
                        }
                        break;
                    case -2: // -2 for bottom bound
                        if (ReviseBorder(next, domainValuesRemoved, borderConnectivity.bottomConnectivity))
                        {
                            if (domains[next.pos.x, next.pos.y].Count == 0)
                            {
                                return false; // a domain became 0, therefore stop
                            }

                            EnqueueNext(next.pos, frontier);
                        }
                        break;
                    case -3: // -3 for right bound
                        if (ReviseBorder(next, domainValuesRemoved, borderConnectivity.rightConnectivity))
                        {
                            if (domains[next.pos.x, next.pos.y].Count == 0)
                            {
                                return false; // a domain became 0, therefore stop
                            }

                            EnqueueNext(next.pos, frontier);
                        }
                        break;
                    case -4: // -4 for top bound
                        if (ReviseBorder(next, domainValuesRemoved, borderConnectivity.topConnectivity))
                        {
                            if (domains[next.pos.x, next.pos.y].Count == 0)
                            {
                                return false; // a domain became 0, therefore stop
                            }

                            EnqueueNext(next.pos, frontier);
                        }
                        break;
                    default:
                        if (Revise(next, domainValuesRemoved))
                        {
                            if (domains[next.pos.x, next.pos.y].Count == 0)
                            {
                                return false; // a domain became 0, therefore stop
                            }

                            EnqueueNext(next.pos, frontier);
                        }
                        break;
                }
            }

            return true;
        }

        struct CSPState
        {
            public CSPState(Vector2Int pos, Priority priority, int enginePrediction, SparseSet[,] domains, ConnectivityData connectivity, BoundsInt bounds, GeneratorEngine engine, float temperature, int[] mapIndicies)
            {
                this.pos = pos;
                this.priority = priority;

                domain = domains[pos.x, pos.y].ToArray();
                toRestoreDomain = new List<PosInt>();

                int[] domainSizes = new int[domain.Length];

                if (priority.priorityLevel == 0)
                {
                    if (System.Array.IndexOf(domain, enginePrediction) == -1) // if the initial engine prediction is not available, make a new prediction
                    {
                        enginePrediction = engine.CalculateAndPredict(pos, -temperature, mapIndicies);
                    }

                    // Sort the domain according to the LCV heuristic
                    for (int i = 0; i < domain.Length; i++)
                    {
                        if (domain[i] == enginePrediction) // if it matches engine prediction, put it first
                        {
                            domainSizes[i] = -999999;
                            continue;
                        }

                        domainSizes[i] = connectivity.GetLCVHeuristic(pos, bounds.min.y, domains, bounds, domain[i]);
                    }
                }
                else
                {
                    for (int i = 0; i < domain.Length; i++)
                    {
                        domainSizes[i] = connectivity.GetLCVHeuristic(pos, bounds.min.y, domains, bounds, domain[i]);

                        if (priority.prioritySet.Contains(domain[i])) // if it matches the high priority set, put it first
                        {
                            domainSizes[i] -= 999999;
                        }
                    }
                }

                System.Array.Sort(domainSizes, domain); // sorts the domain smallest domain sizes first

                iterIndex = 0;
                expectingReturn = false;
            }

            public List<PosInt> toRestoreDomain;
            public Vector2Int pos;
            public Priority priority;
            public int[] domain;
            public int iterIndex;
            public bool expectingReturn;
        }

        private void DoCSPFilling()
        {
            Stack<CSPState> stack = new Stack<CSPState>();

            if (lrvEngine.IsDone())
            {
                return;
            }

            (Vector2Int nextPos, int nextLevel) = lrvEngine.Next(rand);

            stack.Push(new CSPState(nextPos, priorities[nextPos.x, nextPos.y], engineCollapsedTo[nextPos.x, nextPos.y], domains, connectivity, bounds, engine, temperature, indicies));

            bool done = false;
            while (!done)
            {
                CSPState curr = stack.Pop();

                if (curr.expectingReturn)
                {
                    foreach (PosInt d in curr.toRestoreDomain)
                    {
                        domains[d.pos.x, d.pos.y].Add(d.index);
                    }
                    curr.toRestoreDomain.Clear();
                }

                bool shouldReturn = false;
                while (curr.iterIndex < curr.domain.Length)
                {
                    int value = curr.domain[curr.iterIndex];
                    curr.iterIndex++;

                    indicies[curr.pos.x + curr.pos.y * bounds.size.x] = value;

                    if (AC3Modified(curr.pos, curr.toRestoreDomain))
                    {
                        shouldReturn = true;
                        curr.expectingReturn = true;

                        stack.Push(curr);

                        if (lrvEngine.IsDone())
                        {
                            done = true;
                        }
                        else
                        {
                            (nextPos, nextLevel) = lrvEngine.Next(rand);

                            stack.Push(new CSPState(nextPos, priorities[nextPos.x, nextPos.y], engineCollapsedTo[nextPos.x, nextPos.y], domains, connectivity, bounds, engine, temperature, indicies));
                        }

                        break;
                    }

                    foreach (PosInt d in curr.toRestoreDomain)
                    {
                        domains[d.pos.x, d.pos.y].Add(d.index);
                    }
                    curr.toRestoreDomain.Clear();
                }
                if (shouldReturn)
                {
                    continue;
                }

                // if it reached here, that means this is an inconsistent assignment
                // easier to just restart from scratch 

                lrvEngine.Add(curr.pos);
                indicies[curr.pos.x + curr.pos.y * bounds.size.x] = -1;

                foreach (CSPState step in stack)
                {
                    lrvEngine.Add(step.pos);
                    indicies[step.pos.x + step.pos.y * bounds.size.x] = -1;

                    foreach (PosInt d in step.toRestoreDomain)
                    {
                        domains[d.pos.x, d.pos.y].Add(d.index);
                    }
                }

                DoCSPFilling();
                return;
            }

            // If it reached this point, that means it has achieved a fully consistent assignment and the generation is completed.

            collapseOrder = new Vector2Int[stack.Count];

            CSPState[] stackArray = stack.ToArray();
            for (int i = 0; i < stackArray.Length; i++)
            {
                collapseOrder[i] = stackArray[i].pos;
            }
        }

        public struct PosInt
        {
            public PosInt(Vector2Int pos, int index)
            {
                this.pos = pos;
                this.index = index;
            }

            public Vector2Int pos;
            public int index;
        }

        // Returns true if the assignment at pos is arc consistent. False otherwise.
        bool AC3Modified(Vector2Int pos, List<PosInt> domainValuesRemoved)
        {
            int initialIdx = indicies[pos.x + pos.y * bounds.size.x];
            Queue<PosInt> frontier = new Queue<PosInt>();

            EnqueueNext(pos, frontier);

            while (frontier.Count > 0)
            {
                PosInt next = frontier.Dequeue();
                if (indicies[next.pos.x + next.pos.y * bounds.size.x] == -2)
                {
                    continue;
                }

                switch (next.index)
                {
                    case -1: // -1 for left bound
                        if (ReviseBorder(next, domainValuesRemoved, borderConnectivity.leftConnectivity))
                        {
                            if (domains[next.pos.x, next.pos.y].Count == 0)
                            {
                                return false; // a domain became 0, therefore stop
                            }

                            EnqueueNext(next.pos, frontier);
                        }
                        break;
                    case -2: // -2 for bottom bound
                        if (ReviseBorder(next, domainValuesRemoved, borderConnectivity.bottomConnectivity))
                        {
                            if (domains[next.pos.x, next.pos.y].Count == 0)
                            {
                                return false; // a domain became 0, therefore stop
                            }

                            EnqueueNext(next.pos, frontier);
                        }
                        break;
                    case -3: // -3 for right bound
                        if (ReviseBorder(next, domainValuesRemoved, borderConnectivity.rightConnectivity))
                        {
                            if (domains[next.pos.x, next.pos.y].Count == 0)
                            {
                                return false; // a domain became 0, therefore stop
                            }

                            EnqueueNext(next.pos, frontier);
                        }
                        break;
                    case -4: // -4 for top bound
                        if (ReviseBorder(next, domainValuesRemoved, borderConnectivity.topConnectivity))
                        {
                            if (domains[next.pos.x, next.pos.y].Count == 0)
                            {
                                return false; // a domain became 0, therefore stop
                            }

                            EnqueueNext(next.pos, frontier);
                        }
                        break;
                    default:
                        if (Revise(next, domainValuesRemoved))
                        {
                            if (domains[next.pos.x, next.pos.y].Count == 0)
                            {
                                return false; // a domain became 0, therefore stop
                            }

                            EnqueueNext(next.pos, frontier);
                        }
                        break;
                }
            }

            return true;
        }

        void EnqueueNext(Vector2Int pos, Queue<PosInt> frontier)
        {
            for (int dir = 0; dir < connectivity.GetDirectionCount(); dir++)
            {
                Vector2Int nextPos = pos + connectivity.GetConnectionOffset(dir, pos, bounds.position.y);

                if (nextPos.x >= 0 && nextPos.y >= 0 && nextPos.x < bounds.size.x && nextPos.y < bounds.size.y)
                {
                    frontier.Enqueue(new PosInt(nextPos, connectivity.GetOppositeDirection(dir)));

                    if (nextPos.x == 0 && borderConnectivity.enforceConnectivity.left)
                    {
                        frontier.Enqueue(new PosInt(nextPos, -1)); // -1 for left bound
                    }
                    if (nextPos.y == 0 && borderConnectivity.enforceConnectivity.bottom)
                    {
                        frontier.Enqueue(new PosInt(nextPos, -2)); // -2 for bottom bound
                    }
                    if (nextPos.x == bounds.size.x - 1 && borderConnectivity.enforceConnectivity.right)
                    {
                        frontier.Enqueue(new PosInt(nextPos, -3)); // -3 for right bound
                    }
                    if (nextPos.y == bounds.size.y - 1 && borderConnectivity.enforceConnectivity.top)
                    {
                        frontier.Enqueue(new PosInt(nextPos, -4)); // -4 for top bound
                    }
                }
            }
        }

        // Returns true iff we revise the domain of the given position
        bool Revise(PosInt next, List<PosInt> domainValuesRemoved)
        {
            bool revised = false;

            Vector2Int adjacentPos = next.pos + connectivity.GetConnectionOffset(next.index, next.pos, bounds.position.y);

            if (adjacentPos.x >= 0 && adjacentPos.y >= 0 && adjacentPos.x < bounds.size.x && adjacentPos.y < bounds.size.y)
            {
                int adjacentIndex = indicies[adjacentPos.x + adjacentPos.y * bounds.size.x];

                if (adjacentIndex >= 0) // already collapsed
                {
                    for (int i = 0; i < domains[next.pos.x, next.pos.y].Count; i++)
                    {
                        int idx = domains[next.pos.x, next.pos.y].GetDense(i);

                        if (!connectivity.GetConnectivity(idx, adjacentIndex, next.index))
                        {
                            revised = true;

                            domainValuesRemoved.Add(new PosInt(next.pos, idx));
                            domains[next.pos.x, next.pos.y].RemoveAt(i);
                            i--;
                        }
                    }
                }
                else if (adjacentIndex != -2) // not a tile to not be collapsed
                {
                    for (int i = 0; i < domains[next.pos.x, next.pos.y].Count; i++)
                    {
                        int idx = domains[next.pos.x, next.pos.y].GetDense(i);
                        bool support = false;

                        for (int j = 0; j < domains[adjacentPos.x, adjacentPos.y].Count; j++)
                        {
                            if (connectivity.GetConnectivity(idx, domains[adjacentPos.x, adjacentPos.y].GetDense(j), next.index))
                            {
                                support = true;
                                break;
                            }
                        }

                        if (!support)
                        {
                            revised = true;

                            domainValuesRemoved.Add(new PosInt(next.pos, idx));
                            domains[next.pos.x, next.pos.y].RemoveAt(i);
                            i--;
                        }
                    }
                }
            }

            return revised;
        }

        private bool ReviseBorder(PosInt next, List<PosInt> domainValuesRemoved, bool[] borderConnectivity)
        {
            bool revised = false;

            for (int i = 0; i < domains[next.pos.x, next.pos.y].Count; i++)
            {
                int idx = domains[next.pos.x, next.pos.y].GetDense(i);

                if (!borderConnectivity[idx])
                {
                    revised = true;

                    domainValuesRemoved.Add(new PosInt(next.pos, idx));
                    domains[next.pos.x, next.pos.y].RemoveAt(i);
                    i--;
                }
            }

            return revised;
        }
    }
}