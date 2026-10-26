using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InteliMapPro
{
    public class EightWayConnectivity : ConnectivityData
    {
        public EightWayConnectivity(int tileCount)
        {
            this.tileCount = tileCount;

            connectivity = new bool[8 * tileCount * tileCount];
        }

        public EightWayConnectivity(int tileCount, bool[] connectivity)
        {
            this.tileCount = tileCount;

            this.connectivity = connectivity;
        }

        public override ConnectivityType Type => ConnectivityType.EightWay;

        private bool[] connectivity;
        private int tileCount;

        public readonly static Vector2Int[] directions = new Vector2Int[8] {
            new Vector2Int(-1, -1), Vector2Int.left, new Vector2Int(-1, 1), Vector2Int.up,
            new Vector2Int(1, 1), Vector2Int.right, new Vector2Int(1, -1), Vector2Int.down
        };

        public override int GetDirectionCount()
        {
            return 8;
        }

        public override int GetOppositeDirection(int direction)
        {
            return (direction + 4) % 8;
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
