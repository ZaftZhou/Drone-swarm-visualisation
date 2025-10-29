using UnityEngine;

/// <summary>
/// 最简单稳定的 CPU 累积绘制。
/// 相机渲染 -> currentRT
/// CPU 读取 currentRT 像素 -> 累加到 accumTex
/// 再上传回 accumulatedRT (用于地面显示)
/// </summary>
[RequireComponent(typeof(Camera))]
public class CoverageAccumulator_CPU_Final : MonoBehaviour
{
    [Header("相机当前帧输出")]
    public RenderTexture currentRT;

    [Header("最终累积结果 (显示用)")]
    public RenderTexture accumulatedRT;

    [Header("每隔多少帧更新一次 (降低CPU压力)")]
    public int updateInterval = 1;

    private Camera cam;
    private Texture2D frameTex;
    private Texture2D accumTex;
    private int frameCount;

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

        int w = accumulatedRT.width;
        int h = accumulatedRT.height;

        // 用 RGBA32 防止 Unity 预览时显示蓝色
        frameTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        accumTex = new Texture2D(w, h, TextureFormat.RGBA32, false);

        ClearRT(accumulatedRT, Color.black);
    }

    void LateUpdate()
    {
        frameCount++;
        if (frameCount % updateInterval != 0) return;

        // 1️⃣ 渲染当前帧
        ClearRT(currentRT, Color.black);
        cam.Render();

        // 2️⃣ 读取当前帧像素到 Texture2D
        RenderTexture.active = currentRT;
        frameTex.ReadPixels(new Rect(0, 0, frameTex.width, frameTex.height), 0, 0);
        frameTex.Apply();

        // 3️⃣ CPU 灰度累加 (max 模式)
        Color32[] src = frameTex.GetPixels32();
        Color32[] dst = accumTex.GetPixels32();

        for (int i = 0; i < src.Length; i++)
        {
            byte v = src[i].r; // 当前帧亮度
            if (v > dst[i].r)
            {
                dst[i].r = dst[i].g = dst[i].b = v;
                dst[i].a = 255;
            }
        }

        accumTex.SetPixels32(dst);
        accumTex.Apply();

        // 4️⃣ 上传回 RenderTexture (供地面显示)
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
