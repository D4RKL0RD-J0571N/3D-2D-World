using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace InteliMapPro
{
    [CreateAssetMenu(fileName = "New PrefabTile", menuName = "InteliMap Pro/Prefab Tile", order = 1)]
    public class PrefabTile : TileBase
    {
        [Tooltip("The sprite to be used for this tile. This can be null if you only want to use the prefab.")]
        public Sprite tileSprite;
        [Tooltip("The prefab to be used for this tile. This can be null if you only want to use the sprite. Note that the prefab may not appear in the tile pallete menu.")]
        public GameObject tilePrefab;

        public override void GetTileData(Vector3Int location, ITilemap tilemap, ref TileData tileData)
        {
            tileData.sprite = tileSprite;
            tileData.gameObject = tilePrefab;
        }
    }
}