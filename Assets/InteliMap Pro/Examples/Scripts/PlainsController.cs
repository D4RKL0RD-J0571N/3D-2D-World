using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using InteliMapPro; // Nessesary in order to interface with the InteliMapGenerator

public class PlainsController : MonoBehaviour
{
    public Transform mainCamera;
    public InteliMapGenerator generator;
    public float speed = 2.0f;
    public int chunkWidth = 12;
    public int chunkOverlap = 2;

    private float cameraStart;
    private float lastChunkLocation;

    private void Start()
    {
        cameraStart = mainCamera.position.x;

        // Set the x size of the generators boundsToFill to be equal to the chunk width
        generator.boundsToFill.size = new Vector3Int(chunkWidth, generator.boundsToFill.size.y, 1);
    }

    private void Update()
    {
        // Move the main camera forward
        mainCamera.position += Vector3.right * speed * Time.deltaTime;

        // If the main camera surpasses the chunk bounds
        if (mainCamera.position.x - cameraStart > lastChunkLocation)
        {
            // Generate the next chunk. (Asyncronously so it doesn't cause lag spikes as generation is quite an expensive operation)
            generator.StartGenerationAsync();
            // generator.StartGenerationAsyncWithSeed(1234); can also generate with a seed

            // Move the chunk forward
            generator.boundsToFill.position += Vector3Int.right * (chunkWidth - chunkOverlap);
            lastChunkLocation += chunkWidth - chunkOverlap;
        }
    }
}
