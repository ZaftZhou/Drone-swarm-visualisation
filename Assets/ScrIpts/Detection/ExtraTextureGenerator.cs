using System.IO;
using UnityEngine;

public class ExtraTextureGenerator : MonoBehaviour
{
    public bool GenerateTextures = false;
    public Terrain Terrain;
    public int sideLength;

    private void OnValidate()
    {
        if (GenerateTextures)
        {
            GenerateTextures = false;
            if (Terrain == null) return;
            GenerateAndExportTextures();
        }
    }

    void GenerateAndExportTextures()
    {
        var td = Terrain.terrainData;
        var treedata = td.treeInstances;
        var pixels = new Color32[sideLength * sideLength];
        Texture2D tex = new (sideLength, sideLength, TextureFormat.R16, false);
        for (int i = 0; i < sideLength*sideLength; i++)
        {
            pixels[i] = Color.black;
        }
        for (int i = 0; i < treedata.Length; i++)
        {
            var tree = treedata[i];

            var pixelIndex = pixels.Length - 1 - ((int)Mathf.Clamp(tree.position.x * sideLength, 0, sideLength - 1) + ((int)Mathf.Clamp(tree.position.z * sideLength, 0, sideLength - 1)) * sideLength);
            pixels[pixelIndex] = Color.white;
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        var simulationFolderExtraDataPath = Path.Combine(Application.persistentDataPath, "ExtraData");
        Directory.CreateDirectory(simulationFolderExtraDataPath);
        Debug.Log($"baking data to {simulationFolderExtraDataPath}");
        var png = tex.EncodeToPNG();
        var path = Path.Combine(simulationFolderExtraDataPath, $"trees.png");
        File.WriteAllBytes(path, png);
    }
}
