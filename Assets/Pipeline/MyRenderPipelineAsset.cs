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
        // Light Space Perspective Shadow Map（ライトに垂直な透視ワープ）
        LiSPSM,
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
    /// LiSPSM のワープ強さ。n_opt に掛ける係数。
    /// 小さい → 強ワープ（近く綺麗・PSM寄り／遠く荒れる）
    /// 大きい → 弱ワープ（Uniform寄り）
    /// 1.0 = 論文の最適値 n_opt
    /// </summary>
    [SerializeField, Range(0.1f, 5f)]
    private float lispsmWarpScale = 1f;

    public float LiSPSMWarpScale => lispsmWarpScale;

    /// <summary>
    /// レンダーパイプラインを作る
    /// </summary>
    protected override UnityEngine.Rendering.RenderPipeline CreatePipeline() 
    {
        return new MyRenderPipeline(this);
    }
}

