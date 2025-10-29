using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CoverageAccumulator_CPU : MonoBehaviour
{
    [Header("1️⃣ 相机当前帧输出")]
    public RenderTexture currentRT;

    [Header("2️⃣ 最终叠加结果（显示用）")]
    public RenderTexture accumulatedRT;

    [Header("采样精度（1.0 = 全分辨率）")]
    [Range(0.25f, 1f)] public float sampleScale = 1f;

    private Camera cam;
    private Texture2D frameTex;
    private Texture2D accumTex;

    void Start()
    {
        cam = GetComponent<Camera>();

        if (!currentRT || !accumulatedRT)
        {
            Debug.LogError("❌ 请指定 currentRT 和 accumulatedRT");
            enabled = false;
            return;
        }

        cam.targetTexture = currentRT;
        cam.enabled = false;

        int w = Mathf.RoundToInt(accumulatedRT.width * sampleScale);
        int h = Mathf.RoundToInt(accumulatedRT.height * sampleScale);

        frameTex = new Texture2D(w, h, TextureFormat.R8, false);
        accumTex = new Texture2D(w, h, TextureFormat.R8, false);

        ClearRT(accumulatedRT, Color.black);
    }

    void LateUpdate()
    {
        // 1️⃣ 渲染当前帧
        ClearRT(currentRT, Color.black);
        cam.Render();

        // 2️⃣ 读取当前帧像素到 Texture2D
        RenderTexture.active = currentRT;
        frameTex.ReadPixels(new Rect(0, 0, frameTex.width, frameTex.height), 0, 0);
        frameTex.Apply();

        // 3️⃣ CPU 累加（只取灰度通道，取最大值叠加）
        Color32[] src = frameTex.GetPixels32();
        Color32[] dst = accumTex.GetPixels32();

        for (int i = 0; i < src.Length; i++)
        {
            byte a = src[i].r;
            if (a > dst[i].r) dst[i].r = a;
        }
        accumTex.SetPixels32(dst);
        accumTex.Apply();

        // 4️⃣ 上传结果回 RenderTexture（显示用）
        RenderTexture.active = accumulatedRT;
        Graphics.Blit(accumTex, accumulatedRT);
        RenderTexture.active = null;
    }

    static void ClearRT(RenderTexture rt, Color c)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, c);
        RenderTexture.active = prev;
    }

    void OnDestroy()
    {
        if (frameTex) Destroy(frameTex);
        if (accumTex) Destroy(accumTex);
    }
}
