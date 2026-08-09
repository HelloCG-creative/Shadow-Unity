using UnityEngine;

/// <summary>
/// レンダーパイプラインアセット
/// </summary>
[ExecuteInEditMode]
[CreateAssetMenu(menuName = "Shadowing/RenderPipelineAsset", fileName = "render_pipeline_asset.asset")]
public class MyRenderPipelineAsset : UnityEngine.Rendering.RenderPipelineAsset 
{
    /// <summary>
    /// シャドウの投影方式
    /// </summary>
    public enum ShadowProjectionMode {
        // Perspective Shadow Map（カメラのpost-perspective空間で焼く）
        PSM,
        // 普通の平行投影シャドウマップ（比較用）
        Uniform,
    }

    /// <summary>
    /// シャドウの投影方式（PSM / Uniform を切り替えて比較）
    /// </summary>
    [SerializeField]
    private ShadowProjectionMode shadowMode = ShadowProjectionMode.PSM;

    public ShadowProjectionMode ShadowMode => shadowMode;

    /// <summary>
    /// シャドウマップの解像度
    /// </summary>
    [SerializeField]
    private int shadowResolution;

    public int ShadowResolution => shadowResolution;

    /// <summary>
    /// シャドウを投影する最大距離
    /// </summary>
    [SerializeField]
    private float shadowDistance;

    public float ShadowDistance => shadowDistance;

    /// <summary>
    /// レンダーパイプラインを作る
    /// </summary>
    protected override UnityEngine.Rendering.RenderPipeline CreatePipeline() 
    {
        return new MyRenderPipeline(this);
    }
}

