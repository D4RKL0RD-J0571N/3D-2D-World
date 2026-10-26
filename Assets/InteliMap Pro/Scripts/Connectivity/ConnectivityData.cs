using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InteliMapPro
{
    public enum ConnectivityType
    {
        FourWay,
        EightWay,
        Hexagonal,
        ExtendedFourWay,
        ExtendedEightWay
    }

    public abstract class ConnectivityData
    {
        public abstract ConnectivityType Type { get; }

        public abstract int GetDirectionCount();
        public abstract int GetOppositeDirection(int direction);

        public abstract bool[] GetConnectivityArray();

        public abstract Vector2Int GetConnectionOffset(int direction, Vector2Int pos, int startY);

        public abstract bool GetConnectivity(int indexA, int indexB, int direction);
        public abstract void SetConnectivity(int indexA, int indexB, int direction, bool value);

        public int GetLCVHeuristic(Vector2Int pos, int startY, SparseSet[,] domains, BoundsInt bounds, int index)
        {
            int size = 0;

            for (int d = 0; d < GetDirectionCount(); d++)
            {
                Vector2Int adjacentPosition = pos + GetConnectionOffset(d, pos, startY);

                if (adjacentPosition.x >= 0 && adjacentPosition.y >= 0 && adjacentPosition.x < bounds.size.x && adjacentPosition.y < bounds.size.y)
                {
                    SparseSet domain = domains[adjacentPosition.x, adjacentPosition.y];
                    for (int i = 0; i < domain.Count; i++)
                    {
                        if (!GetConnectivity(index, domain.GetDense(i), d))
                        {
                            size++;
                        }
                    }
                }
            }

            return size;
        }

        public static string GetConnectivityTypeString(ConnectivityType type)
        {
            switch (type)
            {
                case ConnectivityType.FourWay:
                    return "Four way";
                case ConnectivityType.EightWay:
                    return "Eight way";
                case ConnectivityType.ExtendedFourWay:
                    return "Extended four way";
                case ConnectivityType.ExtendedEightWay:
                    return "Extende eight way";
                case ConnectivityType.Hexagonal:
                    return "Hexagonal";
                default:
                    return "Invalid connectivity type";
            }
        }
    }
}