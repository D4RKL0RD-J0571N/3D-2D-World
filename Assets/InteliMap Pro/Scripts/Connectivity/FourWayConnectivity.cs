using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InteliMapPro
{
    public class FourWayConnectivity : ConnectivityData
    {
        public FourWayConnectivity(int tileCount)
        {
            this.tileCount = tileCount;

            connectivity = new bool[4 * tileCount * tileCount];
        }

        public FourWayConnectivity(int tileCount, bool[] connectivity)
        {
            this.tileCount = tileCount;

            this.connectivity = connectivity;
        }

        public override ConnectivityType Type => ConnectivityType.FourWay;

        private bool[] connectivity;
        private int tileCount;

        public readonly static Vector2Int[] directions = new Vector2Int[4] { Vector2Int.down, Vector2Int.left, Vector2Int.up, Vector2Int.right };

        public override int GetDirectionCount()
        {
            return 4;
        }

        public override int GetOppositeDirection(int direction)
        {
            return (direction + 2) % 4;
        }

        public override bool[] GetConnectivityArray()
        {
            return connectivity;
        }

        public override Vector2Int GetConnectionOffset(int direction, Vector2Int pos, int startY)
        {
            return directions[direction];
        }

        public override bool GetConnectivity(int indexA, int indexB, int direction)
        {
            return connectivity[direction * tileCount * tileCount + indexA * tileCount + indexB];
        }

        public override void SetConnectivity(int indexA, int indexB, int direction, bool value)
        {
            connectivity[direction * tileCount * tileCount + indexA * tileCount + indexB] = value;
        }
    }
}
