using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

namespace InteliMapPro
{
    public class LRVPriorityEngine
    {
        public LRVPriorityEngine(SparseSet[,] domains, Priority[,] priorities, BoundsInt bounds, float randomChance)
        {
            this.domains = domains;
            this.priorities = priorities;
            this.randomChance = randomChance;

            count = 0;

            remainingPositions = new Vector2Int[bounds.size.x * bounds.size.y];
        }

        private SparseSet[,] domains;
        private Priority[,] priorities;

        private Vector2Int[] remainingPositions;
        private float randomChance;
        private int count;

        public bool IsDone()
        {
            return count == 0;
        }

        public void Add(Vector2Int pos)
        {
            remainingPositions[count] = pos;
            count++;
        }

        public (Vector2Int, int) Next(System.Random rand)
        {
            int smallestSize = 99999999;
            int highestPriority = 0;
            List<int> smallestIndicies = new List<int>();

            for (int j = 0; j < count; j++)
            {
                Vector2Int pos = remainingPositions[j];
                int level = priorities[pos.x, pos.y].priorityLevel;

                if (level > 0)
                {
                    int size = domains[pos.x, pos.y].OverlapCount(priorities[pos.x, pos.y].prioritySet);

                    if (level > highestPriority)
                    {
                        highestPriority = level;
                        smallestSize = size;

                        smallestIndicies.Clear();
                        smallestIndicies.Add(j);
                    }
                    else
                    {
                        if (size < smallestSize && size != 0)
                        {
                            smallestSize = size;

                            smallestIndicies.Clear();
                            smallestIndicies.Add(j);
                        }
                        else if (size == smallestSize)
                        {
                            smallestIndicies.Add(j);
                        }
                    }
                }
                else if (highestPriority == 0) // no non-zero priorities yet
                {
                    if (domains[pos.x, pos.y].Count < smallestSize)
                    {
                        smallestSize = domains[pos.x, pos.y].Count;

                        smallestIndicies.Clear();
                        smallestIndicies.Add(j);
                    }
                    else if (domains[pos.x, pos.y].Count == smallestSize)
                    {
                        smallestIndicies.Add(j);
                    }
                }
            }

            int randIdx;
            Vector2Int val;

            if (highestPriority == 0 && (float)rand.NextDouble() < randomChance)
            {
                randIdx = rand.Next(count);
                val = remainingPositions[randIdx];

                count--;
                remainingPositions[randIdx] = remainingPositions[count];

                return (val, 0);
            }

            randIdx = smallestIndicies[rand.Next(smallestIndicies.Count)];
            val = remainingPositions[randIdx];

            count--;
            remainingPositions[randIdx] = remainingPositions[count];

            return (val, highestPriority);
        }
    }
}
