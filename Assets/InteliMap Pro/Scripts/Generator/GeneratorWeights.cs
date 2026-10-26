using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InteliMapPro
{
    [Serializable]
    public class GeneratorWeights
    {
        public GeneratorWeights(int uniqueTileCount, int neightborhoodRadius, bool useXPositionAsInput, bool useYPositionAsInput, DirectionalBools acknowledgeBounds)
        {
            this.acknowledgeBounds = acknowledgeBounds;

            this.neightborhoodRadius = neightborhoodRadius;

            this.useXPositionAsInput = useXPositionAsInput;
            this.useYPositionAsInput = useYPositionAsInput;

            int neighborhoodSideLength = (neightborhoodRadius * 2 + 1);
            int neighborhoodArea = neighborhoodSideLength * neighborhoodSideLength;

            dim3multi = uniqueTileCount + 4;
            dim2multi = (neightborhoodRadius * 2 + 1) * dim3multi;
            dim1multi = (neightborhoodRadius * 2 + 1) * dim2multi;
            weights = new float[uniqueTileCount * dim1multi];

            // there are <neighborhoodArea> inputs, and each input is a one-hot vector of size <uniqueTileCount>
            // uniform xavier initialization
            float bound = 1.0f / Mathf.Sqrt(neighborhoodArea);
            for (int i = 0; i < uniqueTileCount; i++)
            {
                for (int j = 0; j < dim1multi; j++)
                {
                    weights[i * dim1multi + j] = UnityEngine.Random.Range(-bound, bound);
                }
            }

            // biases is initialized to all zeros
            biases = new float[uniqueTileCount];
            for (int i = 0; i < uniqueTileCount; i++)
            {
                biases[i] = 0.0f;
            }

            xPositionWeights = new float[uniqueTileCount];
            yPositionWeights = new float[uniqueTileCount];

            for (int i = 0; i < uniqueTileCount; i++)
            {
                xPositionWeights[i] = UnityEngine.Random.Range(-bound, bound);
                yPositionWeights[i] = UnityEngine.Random.Range(-bound, bound);
            }

            epochsTrained = 0;
        }

        [SerializeField] private float[] weights; // indexed by tileToPlace, nX, nY, tileAtLocation (tileAtLocation can also be an uncollapsed tile)
        [SerializeField] private float[] biases;
        [HideInInspector][SerializeField] private int dim1multi;
        [HideInInspector][SerializeField] private int dim2multi;
        [HideInInspector][SerializeField] private int dim3multi;

        [SerializeField] public int epochsTrained;
        [SerializeField] private int neightborhoodRadius;

        [SerializeField] public bool useXPositionAsInput;
        [SerializeField] public float[] xPositionWeights; // indexed by tileToPlace
        [SerializeField] public bool useYPositionAsInput;
        [SerializeField] public float[] yPositionWeights; // indexed by tileToPlace

        [SerializeField] public DirectionalBools acknowledgeBounds;

        public int GetParameterCount()
        {
            return weights.Length + biases.Length + xPositionWeights.Length + yPositionWeights.Length;
        }

        public int GetNeighborhoodRadius()
        {
            return neightborhoodRadius;
        }

        public float GetWeight(int tileToPlace, int nX, int nY, int tileAtLocation)
        {
            return weights[tileToPlace * dim1multi + nX * dim2multi + nY * dim3multi + tileAtLocation];
        }

        public void SetWeight(int tileToPlace, int nX, int nY, int tileAtLocation, float val)
        {
            weights[tileToPlace * dim1multi + nX * dim2multi + nY * dim3multi + tileAtLocation] = val;
        }

        public void AddToWeight(int tileToPlace, int nX, int nY, int tileAtLocation, float val)
        {
            weights[tileToPlace * dim1multi + nX * dim2multi + nY * dim3multi + tileAtLocation] += val;
        }

        public float GetBias(int tileToPlace)
        {
            return biases[tileToPlace];
        }

        public void AddToBias(int tileToPlace, float val)
        {
            biases[tileToPlace] += val;
        }
    }
}