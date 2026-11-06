using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InteliMapPro
{
    [Serializable]
    public class BorderConnectivity
    {
        public BorderConnectivity(int tileCount, DirectionalBools enforceConnectivity)
        {
            topConnectivity = new bool[tileCount];
            bottomConnectivity = new bool[tileCount];
            leftConnectivity = new bool[tileCount];
            rightConnectivity = new bool[tileCount];

            this.enforceConnectivity = enforceConnectivity;
        }

        [SerializeField] public bool[] topConnectivity;
        [SerializeField] public bool[] bottomConnectivity;
        [SerializeField] public bool[] leftConnectivity;
        [SerializeField] public bool[] rightConnectivity;
        [SerializeField] public DirectionalBools enforceConnectivity;

        // Getters
        public bool GetTopConnectivity(int index)
        {
            return topConnectivity[index];
        }

        public bool GetBottomConnectivity(int index)
        {
            return bottomConnectivity[index];
        }

        public bool GetLeftConnectivity(int index)
        {
            return leftConnectivity[index];
        }

        public bool GetRightConnectivity(int index)
        {
            return rightConnectivity[index];
        }

        // Setters
        public void SetTopConnectivity(int index, bool value)
        {
            topConnectivity[index] = value;
        }

        public void SetBottomConnectivity(int index, bool value)
        {
            bottomConnectivity[index] = value;
        }

        public void SetLeftConnectivity(int index, bool value)
        {
            leftConnectivity[index] = value;
        }

        public void SetRightConnectivity(int index, bool value)
        {
            rightConnectivity[index] = value;
        }
    }
}
