using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InteliMapPro
{
    /// <summary>
    /// Contains all the data and weights for the InteliMap Pro Generator.
    /// </summary>
    public class GeneratorData : ScriptableObject
    {
        public int layerCount = 1;

        public LayeredTile[] uniqueTiles;
        public GeneratorWeights weights;
        public ConnectivityType connectivityType;

        public bool[] connectivityData;
        public BorderConnectivity borderConnectivity;
    }
}
