using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace InteliMapPro
{
    [CreateAssetMenu(fileName = "New RandomTile", menuName = "InteliMap Pro/Random Tile", order = 1)]
    public class RandomTile : Tile
    {
        public Sprite[] possibleSprites;

        public override void GetTileData(Vector3Int location, ITilemap tilemap, ref TileData tileData)
        {
            System.Random random = new System.Random(Mathf.Abs(location.x) << 16 | Mathf.Abs(location.y));

            tileData.sprite = possibleSprites[random.Next(possibleSprites.Length)];
            tileData.colliderType = colliderType;
        }
    }
}