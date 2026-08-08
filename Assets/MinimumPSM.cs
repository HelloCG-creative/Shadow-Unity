using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Minimal PSM Pipeline Asset")]
public class MinimalPsmPipelineAsset : RenderPipelineAsset
{
    public int shadowResolution = 1024;
    public float shadowDistance = 30f;

    protected override RenderPipeline CreatePipeline()
        => new MinimalPsmPipeline(this);
}

public class MinimalPsmPipeline : RenderPipeline
{
    private const string kCmdName = "MinimalPSM";

    private readonly MinimalPsmPipelineAsset _asset;

    private readonly int _shadowTexId = Shader.PropertyToID("_ShadowTex");
    private readonly int _psmVPId     = Shader.PropertyToID("_PSMWorldToShadowClip");

    private RenderTargetIdentifier _shadowRT;

    private static readonly ShaderTagId kForwardTag = new ShaderTagId("ForwardBase"); // Standard
    private static readonly ShaderTagId kForwardTag2 = new ShaderTagId("SRPDefaultUnlit"); // 保険
    private static readonly ShaderTagId kShadowCasterTag = new ShaderTagId("ShadowCaster");

    private Material _shadowDebugMat;

    public MinimalPsmPipeline(MinimalPsmPipelineAsset asset)
    {
        _asset = asset;
        _shadowRT = new RenderTargetIdentifier(_shadowTexId);
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (var cam in cameras)
        {
            var cmd = CommandBufferPool.Get(kCmdName);

            context.SetupCameraProperties(cam);

            if (!cam.TryGetCullingParameters(false, out var cullParams))
            {
                CommandBufferPool.Release(cmd);
                continue;
            }
            cullParams.shadowDistance = Mathf.Min(_asset.shadowDistance, cam.farClipPlane);

            var cull = context.Cull(ref cullParams);

            // 1) Directional light を1つ探す
            if (!TryGetMainDirectional(cull, out var dirLight, out var dirLightWS))
            {
                // ライトが無ければ通常描画だけ
                DrawColor(context, cmd, cam, cull);
                CommandBufferPool.Release(cmd);
                continue;
            }

            // 2) Shadow RT 作成
            SetupShadowRT(context, cmd, _asset.shadowResolution);

            // 3) PSM world->shadowClip 行列を作る（parallel固定）
            var worldToShadowClip = CalcPSM_ParallelOnly(cam, dirLightWS);

            // 4) ShadowCaster を描く（View=I, Proj=worldToShadowClip）
            DrawShadow(context, cmd, cam, cull, worldToShadowClip);

            // 5) グローバル送信（メインパスでサンプルする用）
            cmd.Clear();
            cmd.SetGlobalMatrix(_psmVPId, worldToShadowClip);
            cmd.SetGlobalTexture(_shadowTexId, _shadowRT);
            context.ExecuteCommandBuffer(cmd);

            // 6) 通常描画（影は “受け側シェーダ” が対応していれば乗る）
            DrawColor(context, cmd, cam, cull);

            // 7) 後片付け
            CleanupShadowRT(context, cmd);

            CommandBufferPool.Release(cmd);
        }

        context.Submit();
    }

    private bool TryGetMainDirectional(CullingResults cull, out Light light, out Vector3 dirWS)
    {
        light = null;
        dirWS = Vector3.down;

        for (int i = 0; i < cull.visibleLights.Length; i++)
        {
            var vl = cull.visibleLights[i];
            if (vl.lightType != LightType.Directional) continue;
            if (vl.light == null) continue;
            if (vl.light.shadows == LightShadows.None) continue;
            if (vl.light.shadowStrength <= 0f) continue;

            light = vl.light;
            dirWS = -light.transform.forward; // 光が進む方向
            return true;
        }
        return false;
    }

    private void SetupShadowRT(ScriptableRenderContext context, CommandBuffer cmd, int res)
    {
        cmd.Clear();
        cmd.GetTemporaryRT(_shadowTexId, res, res, 32, FilterMode.Bilinear, RenderTextureFormat.Depth);
        cmd.SetRenderTarget(_shadowRT);
        cmd.ClearRenderTarget(true, true, Color.black, 1f); // depth=1 クリア
        context.ExecuteCommandBuffer(cmd);
    }

    private void CleanupShadowRT(ScriptableRenderContext context, CommandBuffer cmd)
    {
        cmd.Clear();
        cmd.ReleaseTemporaryRT(_shadowTexId);
        context.ExecuteCommandBuffer(cmd);
    }

    private void DrawShadow(
        ScriptableRenderContext context,
        CommandBuffer cmd,
        Camera cam,
        CullingResults cull,
        Matrix4x4 worldToShadowClip)
    {
        cmd.Clear();
        cmd.SetRenderTarget(_shadowRT);

        // ★ 最重要：PSMは view=Identity / proj=worldToShadowClip
        cmd.SetViewProjectionMatrices(Matrix4x4.identity, worldToShadowClip);

        context.ExecuteCommandBuffer(cmd);

        var sorting = new SortingSettings(cam) { criteria = SortingCriteria.None };
        var draw = new DrawingSettings(kShadowCasterTag, sorting);
        var filter = new FilteringSettings(RenderQueueRange.opaque);

        context.DrawRenderers(cull, ref draw, ref filter);

        // view/proj を戻す
        cmd.Clear();
        cmd.SetViewProjectionMatrices(cam.worldToCameraMatrix, cam.projectionMatrix);
        context.ExecuteCommandBuffer(cmd);
    }

    private void DrawColor(ScriptableRenderContext context, CommandBuffer cmd, Camera cam, CullingResults cull)
    {
        cmd.Clear();
        cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        cmd.ClearRenderTarget(true, true, cam.backgroundColor, 1f);
        context.ExecuteCommandBuffer(cmd);

        // Opaque
        var sorting = new SortingSettings(cam) { criteria = SortingCriteria.CommonOpaque };

        // Standard用のForwardBaseをまず試す。無い場合もあるので SRPDefaultUnlit も保険で描く。
        var draw = new DrawingSettings(kForwardTag, sorting);
        draw.SetShaderPassName(1, kForwardTag2);

        var filter = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(cull, ref draw, ref filter);

        // Skybox
        context.DrawSkybox(cam);
    }

    // -----------------------------
    // PSM (article Case1: parallel only)
    // -----------------------------

    private static Vector3 PickSafeUp(Vector3 dir)
    {
        var up = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(dir.normalized, up)) > 0.95f) up = Vector3.right;
        return up;
    }

    private static void GetNdcZRange(out float zn, out float zf)
    {
        bool isOpenGL =
            SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore ||
            SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 ||
            SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3;

        if (isOpenGL) { zn = -1f; zf = 1f; }
        else         { zn =  0f; zf = 1f; }

        if (SystemInfo.usesReversedZBuffer)
        {
            float t = zn; zn = zf; zf = t;
        }
    }

    private static void GetNdcUnitCubeCorners(Vector3[] out8)
    {
        GetNdcZRange(out float zn, out float zf);

        // 8 corners
        out8[0] = new Vector3(-1, -1, zn);
        out8[1] = new Vector3( 1, -1, zn);
        out8[2] = new Vector3( 1,  1, zn);
        out8[3] = new Vector3(-1,  1, zn);

        out8[4] = new Vector3(-1, -1, zf);
        out8[5] = new Vector3( 1, -1, zf);
        out8[6] = new Vector3( 1,  1, zf);
        out8[7] = new Vector3(-1,  1, zf);
    }

    private static Matrix4x4 BuildOrthoToFitPoints(Matrix4x4 view, Vector3[] pts)
    {
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < pts.Length; i++)
        {
            Vector3 p = view.MultiplyPoint(pts[i]);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        // Unityのビュー空間は -Z が前方。Ortho(l,r,b,t,near,far) は
        // ビュー空間z の [-far, -near] を NDC z の [-1,1] に写す。
        // PSMではライトの目が単位キューブ内側に来て頂点zが正負混在するので、
        // 実測レンジ [min.z, max.z] をそのまま near=-max.z / far=-min.z で丸ごと囲う。
        const float pad = 0.05f;
        float near = -max.z - pad;
        float far  = -min.z + pad;
        if (far - near < 1e-4f) far = near + 1e-4f;

        return Matrix4x4.Ortho(min.x, max.x, min.y, max.y, near, far);
    }

    private static Matrix4x4 CalcPSM_ParallelOnly(Camera cam, Vector3 lightDirWS)
    {
        // cameraVP (world->camera clip)
        Matrix4x4 Vc = cam.worldToCameraMatrix;
        Matrix4x4 PcGpu = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
        Matrix4x4 camVP = PcGpu * Vc;

        // ppsLightDir = camVP * (dir,0)  (article)
        Vector4 Ldir = new Vector4(lightDirWS.x, lightDirWS.y, lightDirWS.z, 0f);
        Vector4 pps = camVP * Ldir;

        // post-space light view (eye=(0,0,0))
        Vector3 dir = new Vector3(-pps.x, -pps.y, -pps.z);
        if (dir.sqrMagnitude < 1e-8f) dir = Vector3.forward;
        dir.Normalize();

        Matrix4x4 Vl = Matrix4x4.LookAt(Vector3.zero, dir, PickSafeUp(dir));

        // post-space light proj that fits NDC unit cube
        var cube = new Vector3[8];
        GetNdcUnitCubeCorners(cube);
        Matrix4x4 Pl = BuildOrthoToFitPoints(Vl, cube);

        // shadow map renderIntoTexture=true
        Matrix4x4 PlGpu = GL.GetGPUProjectionMatrix(Pl, true);
        Matrix4x4 lightVPpost = PlGpu * Vl;

        // final: (LightVPpost) * (CameraVP)
        return lightVPpost * camVP;
    }
}