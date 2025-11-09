//Created by Pirani Dev and available in blacave.com/store
//Grasser is a tool to add grass to the terrain globally to avoid manually grass painting.
using UnityEngine;

public class Grasser : MonoBehaviour
{
    public Terrain terrain;
    public int[] detailLayerIndices; // Indices for multiple grass types
    public int[] densities; // Density per grass type
    public int selectedTextureIndex; // Index of the terrain texture to use as a mask
	public int[] grassIndexes;

    void Start()
    {
        ApplyGrassUsingBrushMask();
    }

    void ApplyGrassUsingBrushMask()
    {
        TerrainData terrainData = terrain.terrainData;
        int detailRes = terrainData.detailResolution;
        int alphamapResX = terrainData.alphamapWidth;
        int alphamapResY = terrainData.alphamapHeight;

        if (detailLayerIndices.Length == 0 || densities.Length != detailLayerIndices.Length)
        {
            Debug.LogError("Assign at least one detail layer and ensure densities match.");
            return;
        }

        // Get the alphamaps (texture layers)
        float[,,] alphamaps = terrainData.GetAlphamaps(0, 0, alphamapResX, alphamapResY);

        for (int i = 0; i < detailLayerIndices.Length; i++)
        {
            int[,] detailMap = new int[detailRes, detailRes];

            for (int x = 0; x < detailRes; x++)
            {
                for (int y = 0; y < detailRes; y++)
                {
                    // Scale coordinates to match alphamap resolution
                    int texX = Mathf.RoundToInt((x / (float)detailRes) * alphamapResX);
                    int texY = Mathf.RoundToInt((y / (float)detailRes) * alphamapResY);

                    // Ensure we are within valid bounds
                    texX = Mathf.Clamp(texX, 0, alphamapResX - 1);
                    texY = Mathf.Clamp(texY, 0, alphamapResY - 1);

					//for(int k = 0; k < grassIndexes.Length; k++){
					//	float maskValue = alphamaps[texX, texY, grassIndexes[k]];
						
					//	if (maskValue > 0.1f) // Adjust threshold as needed
					//	{
					//		detailMap[x, y] = Mathf.RoundToInt(maskValue * densities[i]);
					//	}
					//}
					
                    // Get the alpha value for the selected texture index
                    float maskValue = alphamaps[texX, texY, selectedTextureIndex];

                    // Apply grass based on the mask
                    if (maskValue > 0.1f) // Adjust threshold as needed
                    {
                        detailMap[x, y] = Mathf.RoundToInt(maskValue * densities[i]);
                    }
                }
            }

            // Apply the detail layer to the terrain
            terrainData.SetDetailLayer(0, 0, detailLayerIndices[i], detailMap);
        }
    }
}
