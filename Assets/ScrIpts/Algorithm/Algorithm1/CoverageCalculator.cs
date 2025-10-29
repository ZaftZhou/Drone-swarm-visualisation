using UnityEngine;

public class CoverageCalculator : MonoBehaviour
{
    public RenderTexture coverageRT;

    void Update()
    {
        Texture2D tex = new Texture2D(coverageRT.width, coverageRT.height, TextureFormat.R8, false);
        RenderTexture.active = coverageRT;
        tex.ReadPixels(new Rect(0, 0, coverageRT.width, coverageRT.height), 0, 0);
        tex.Apply();

        Color32[] pixels = tex.GetPixels32();
        int whiteCount = 0;
        for (int i = 0; i < pixels.Length; i++)
            if (pixels[i].r > 10) whiteCount++;

        float percent = (float)whiteCount / pixels.Length * 100f;
        Debug.Log($"Coverage: {percent:F2}%");

        Destroy(tex);
    }
}
