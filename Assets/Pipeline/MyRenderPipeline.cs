using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// レンダーパイプライン
/// </summary>
public class MyRenderPipeline : UnityEngine.Rendering.RenderPipeline
{
    /// <summary>
    /// パイプライン名
    /// </summary>
    private const string PipelineName = "RenderPipeline";

    /// <summary>
    /// レンダーパイプラインアセット
    /// </summary>
    private readonly MyRenderPipelineAsset Asset;

    /// <summary>
    /// 描画用レンダーテクスチャのハッシュ値
    /// </summary>
    private readonly int RenderTarget;

    /// <summary>
    /// 描画用レンダーテクスチャのID
    /// </summary>
    private readonly RenderTargetIdentifier RenderTargetId;

    /// <summary>
    /// カメラのレンダーターゲットのID
    /// </summary>
    private readonly RenderTargetIdentifier CameraTargetId;

    /// <summary>
    /// 描画に使うパスのID
    /// </summary>
    private readonly ShaderTagId RenderTagId;

    /// <summary>
    /// シャドウマップのハッシュ値
    /// </summary>
    private readonly int LightShadow;

    /// <summary>
    /// シャドウマップのID
    /// </summary>
    private readonly RenderTargetIdentifier LightShadowId;

    /// <summary>
    /// LVP行列のハッシュ値
    /// </summary>
    private readonly int LightVP;

    /// <summary>
    /// ライトの向きのハッシュ値
    /// </summary>
    private readonly int LightDir;

    /// <summary>
    /// ライトの色のハッシュ値
    /// </summary>
    private readonly int LightColor;

    /// <summary>
    /// シャドウバイアスのハッシュ値
    /// </summary>
    private readonly int ShadowBias;

    /// <summary>
    /// シャドウの法線バイアスのハッシュ値
    /// </summary>
    private readonly int ShadowNormalBias;

    /// <summary>
    /// シャドウの最大距離の2乗のハッシュ値
    /// </summary>
    private readonly int ShadowDistanceSqrt;
    
    private static readonly ShaderTagId ShadowCasterTag = new ShaderTagId("ShadowCaster");

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public MyRenderPipeline(MyRenderPipelineAsset asset)
    {
        Asset = asset;

        RenderTarget = Shader.PropertyToID("_RenderTarget");
        RenderTargetId = new RenderTargetIdentifier(RenderTarget);
        CameraTargetId = new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
        RenderTagId = new ShaderTagId("Forward");

        LightShadow = Shader.PropertyToID("_LightShadow");
        LightShadowId = new RenderTargetIdentifier(LightShadow);

        LightVP = Shader.PropertyToID("_LightVP");
        LightDir = Shader.PropertyToID("_LightDir");
        LightColor = Shader.PropertyToID("_LightColor");
        ShadowBias = Shader.PropertyToID("_ShadowBias");
        ShadowNormalBias = Shader.PropertyToID("_ShadowNormalBias");
        ShadowDistanceSqrt = Shader.PropertyToID("_ShadowDistanceSqrt");
    }

    /// <summary>
    /// このレンダーパイプラインを使って描画する
    /// </summary>
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        var shadowResolution = Asset.ShadowResolution;
        var shadowDistance = Asset.ShadowDistance;

        foreach (var camera in cameras)
        {
            // コマンドバッファの取得
            var cmd = CommandBufferPool.Get(PipelineName);

            // カメラプロパティの設定(View行列、Projection行列の設定など)
            context.SetupCameraProperties(camera);

            // カリング
            if (!TryCulling(context, camera, shadowDistance, out var cullingResults))
            {
                continue;
            }

            // 視界内で有効なディレクショナルライトのインデックスを取得
            var lightIndexes = SearchLightIndexes(cullingResults, LightType.Directional);

            // 有効なライトが存在するかどうか
            var existValidLight = lightIndexes != null && lightIndexes.Count > 0;

            // 有効なライトが存在する時
            if (existValidLight)
            {
                // 1つ目のライトを取得
                var lightIndex = lightIndexes[0];
                var light = cullingResults.visibleLights[lightIndex].light;

                Vector3 lightDirWS = -light.transform.forward;

                // WorldToShadowClip 行列を作る。モードで PSM / 普通のOrtho を切り替える。
                // どちらも同じ _LightVP / _LightShadow の仕組みで描く＆サンプルするので比較しやすい。
                Matrix4x4 worldToShadowClip;
                if (Asset.ShadowMode == MyRenderPipelineAsset.ShadowProjectionMode.PSM)
                {
                    worldToShadowClip =
                        CalcPSM_WorldToShadowClip_Article(camera, lightDirWS, shadowDistance, zNearMin: 0.1f);
                }
                else
                {
                    worldToShadowClip = CalcUniformShadow(cullingResults, lightIndex, shadowResolution);
                }

                // グローバル送信（メインパスの影計算でも同じ行列を使う）
                SetupLightProperties(context, cmd, light, worldToShadowClip, shadowDistance);

                // Shadow RT
                SetupLightRT(context, cmd, shadowResolution);

                // ShadowCaster を worldToShadowClip 行列で描く（PSM/Uniform共通）
                DrawShadowPSM(context, cmd, cullingResults, worldToShadowClip);
            }

            // PSMのために行列を戻す
            context.SetupCameraProperties(camera);

            // 描画用レンダーテクスチャのセットアップ
            SetupMainRT(context, cmd, camera);

            // 不透明物体の描画
            DrawOpaque(context, cmd, camera, cullingResults);

            // Skyboxの描画
            DrawSkybox(context, camera);

            // レンダーテクスチャからカメラのフレームバッファへのコピー
            Restore(context, cmd);

            // 描画用レンダーテクスチャのクリーンアップ
            CleanupMainRT(context, cmd);

            // 有効なライトが存在する時
            if (existValidLight)
            {
                // シャドウマップ用レンダーテクスチャのクリーンアップ
                CleanupLightRT(context, cmd);
            }

            // コマンドバッファの解放
            CommandBufferPool.Release(cmd);
        }

        // 今までの全ての処理のリクエストを実行
        context.Submit();
    }

    private static Vector3 PickSafeUp(Vector3 dir)
    {
        // dir と平行になりにくい up を選ぶ
        Vector3 up = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(dir.normalized, up)) > 0.95f) up = Vector3.right;
        return up;
    }

// Unityの投影行列は「-Z前方」のビュー空間を前提にする。
// Matrix4x4.LookAt は +Z前方の camera-to-world（モデル行列）を返すので、
// inverse で world-to-camera にし、Z を反転して -Z前方へ合わせた「ビュー行列」を返す。
    private static Matrix4x4 MakeViewMatrix(Vector3 eye, Vector3 target, Vector3 up)
    {
        Matrix4x4 view = Matrix4x4.LookAt(eye, target, up).inverse;
        Matrix4x4 flipZ = Matrix4x4.identity;
        flipZ.m22 = -1f;
        return flipZ * view;
    }

    private static Matrix4x4 BuildOrthoToFitPoints(Matrix4x4 view, Vector3[] pointsPost)
    {
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < pointsPost.Length; i++)
        {
            Vector3 pLS = view.MultiplyPoint(pointsPost[i]);
            min = Vector3.Min(min, pLS);
            max = Vector3.Max(max, pLS);
        }

        // Unity の LookAt は「前方 = -Z」前提で考えると扱いやすい
        // view空間で “前方” にある点は z < 0 になりがち → depth = -z
        float near = Mathf.Max(0.001f, -max.z); // 一番手前（depth最小）
        float far = Mathf.Max(near + 0.001f, -min.z);

        return Matrix4x4.Ortho(min.x, max.x, min.y, max.y, near, far);
    }

    private static Matrix4x4 BuildFrustumToFitPoints(ref Matrix4x4 view, ref Vector3 eyePost, Vector3[] pointsPost)
    {
        // 点光源（post空間）用：off-center frustum で unit cube を確実に包む
        const float MIN_NEAR = 0.01f;

        // view 空間での各点の x,y,z から frustum を決める
        float minDepth = float.PositiveInfinity;
        float maxDepth = 0f;

        // depth = -z（前方が -z と仮定）
        for (int i = 0; i < pointsPost.Length; i++)
        {
            Vector3 pLS = view.MultiplyPoint(pointsPost[i]);
            float depth = -pLS.z;
            minDepth = Mathf.Min(minDepth, depth);
            maxDepth = Mathf.Max(maxDepth, depth);
        }

        // もし unit cube が eye の後ろ（depth<=0）に食い込んでいたら、eye を少し後ろへ押し出す
        // （記事の “安定化” 的な扱い。ここは実装上の保険）
        if (minDepth < MIN_NEAR)
        {
            float push = (MIN_NEAR - minDepth) + 0.1f;
            // eye を原点から遠ざける（direction = normalize(eye)）
            Vector3 dir = (eyePost.sqrMagnitude < 1e-6f) ? Vector3.forward : eyePost.normalized;
            eyePost += dir * push;

            // view を作り直して再計算（1回で十分なことが多い）
            Vector3 up = PickSafeUp(-eyePost); // origin を見るので forward は -eye
            view = MakeViewMatrix(eyePost, Vector3.zero, up);

            minDepth = float.PositiveInfinity;
            maxDepth = 0f;
            for (int i = 0; i < pointsPost.Length; i++)
            {
                Vector3 pLS = view.MultiplyPoint(pointsPost[i]);
                float depth = -pLS.z;
                minDepth = Mathf.Min(minDepth, depth);
                maxDepth = Mathf.Max(maxDepth, depth);
            }
        }

        float near = Mathf.Max(MIN_NEAR, minDepth);
        float far = Mathf.Max(near + 0.01f, maxDepth);

        float left = float.PositiveInfinity, right = float.NegativeInfinity;
        float bottom = float.PositiveInfinity, top = float.NegativeInfinity;

        for (int i = 0; i < pointsPost.Length; i++)
        {
            Vector3 pLS = view.MultiplyPoint(pointsPost[i]);
            float depth = -pLS.z;
            depth = Mathf.Max(depth, 1e-6f);

            // near面に投影したときの extents
            float sx = pLS.x * (near / depth);
            float sy = pLS.y * (near / depth);

            left = Mathf.Min(left, sx);
            right = Mathf.Max(right, sx);
            bottom = Mathf.Min(bottom, sy);
            top = Mathf.Max(top, sy);
        }

        return Matrix4x4.Frustum(left, right, bottom, top, near, far);
    }

    private static void GetNdcZRange(out float zNear, out float zFar)
    {
        // OpenGL系は -1..1、それ以外は 0..1 が基本
        bool isOpenGL =
            SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore ||
            SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 ||
            SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3;

        if (isOpenGL)
        {
            zNear = -1f;
            zFar = 1f;
        }
        else
        {
            zNear = 0f;
            zFar = 1f;
        }

        // Reversed-Z の場合は near/far が反転する
        if (SystemInfo.usesReversedZBuffer)
        {
            float tmp = zNear;
            zNear = zFar;
            zFar = tmp;
        }
    }

    private static void GetNdcUnitCubeCorners(Vector3[] out8)
    {
        // out8: 8 corners in NDC (w=1 扱い)
        GetNdcZRange(out float zn, out float zf);

        int i = 0;
        for (int y = 0; y <= 1; y++)
        for (int x = 0; x <= 1; x++)
        for (int z = 0; z <= 1; z++)
        {
            float fx = (x == 0) ? -1f : 1f;
            float fy = (y == 0) ? -1f : 1f;
            float fz = (z == 0) ? zn : zf;
            out8[i++] = new Vector3(fx, fy, fz);
        }
    }

    private Matrix4x4 CalcPSM_WorldToShadowClip_Article(Camera cam, Vector3 lightDirWS, float shadowDistance,
        float zNearMin = 0.1f)
    {
        // ---- 1) Camera View/Proj（PSM計算用） ----
        float near = Mathf.Max(cam.nearClipPlane, zNearMin);
        float far = Mathf.Min(cam.farClipPlane, shadowDistance);

        // oblique 等を考えない「標準perspective」として扱う（記事準拠）
        Matrix4x4 Pc = Matrix4x4.Perspective(cam.fieldOfView, cam.aspect, near, far);
        Matrix4x4 Vc = cam.worldToCameraMatrix;

        // GPU projection（renderIntoTexture=false：メインカメラと同じ系）
        Matrix4x4 PcGpu = GL.GetGPUProjectionMatrix(Pc, false);
        Matrix4x4 camVP = PcGpu * Vc;

        // ---- 2) ppsLight（記事の “透視変換後ライト”） ----
        // 方向ベクトルなので w=0
        Vector4 L = new Vector4(lightDirWS.x, lightDirWS.y, lightDirWS.z, 0f);

        Vector4 ppsLight = camVP * L; // 透視変換後のライト（homogeneous）
        // 記事の分岐：w==0 → parallel、w!=0 → point
        bool isParallel = Mathf.Abs(ppsLight.w) < 1e-6f;

        // ---- 3) post空間で Light View/Proj を構築 ----
        var cube = new Vector3[8];
        GetNdcUnitCubeCorners(cube); // unit cube in NDC/post space

        Matrix4x4 VlPost;
        Matrix4x4 PlPost;

        if (isParallel)
        {
            // 平行光（記事 Case1）
            Vector3 dir = new Vector3(-ppsLight.x, -ppsLight.y, -ppsLight.z);
            if (dir.sqrMagnitude < 1e-8f) dir = Vector3.forward;
            dir.Normalize();

            Vector3 up = PickSafeUp(dir);
            // 視点は(0,0,0)、見る方向は “光が見える方向” = (-ppsLight.x, -ppsLight.y, -ppsLight.z)
            VlPost = MakeViewMatrix(Vector3.zero, dir, up);

            // unit cube を包む Ortho（記事の説明そのもの）
            PlPost = BuildOrthoToFitPoints(VlPost, cube);
        }
        else
        {
            // 点（記事 Case2/3）
            // ppLight = ppsLight / w
            Vector3 pp = new Vector3(ppsLight.x, ppsLight.y, ppsLight.z) * (1.0f / ppsLight.w);

            // w<0 の場合は消失点 → z を反転（記事に寄せる）
            if (ppsLight.w < 0f) pp.z *= -1f;

            Vector3 eye = pp;
            Vector3 up = PickSafeUp(-eye);
            VlPost = MakeViewMatrix(eye, Vector3.zero, up);

            // pullback で目を後ろに引く補正を VlPost へ反映させる（ref）
            PlPost = BuildFrustumToFitPoints(ref VlPost, ref eye, cube);
        }

        // ---- 4) Shadow map に描くので renderIntoTexture=true ----
        Matrix4x4 PlGpu = GL.GetGPUProjectionMatrix(PlPost, true);
        Matrix4x4 lightVPpost = PlGpu * VlPost;

        // ---- 5) 記事の最終： (LightVPpost) * (CameraVP) ----
        Matrix4x4 worldToShadowClip = lightVPpost * camVP;

        // ===== DEBUG: 分岐と、視界中央の点がシャドウクリップ空間のどこに落ちるか =====
        {
            Vector3 wp = cam.transform.position + cam.transform.forward * ((near + far) * 0.5f);
            Vector4 sc = worldToShadowClip * new Vector4(wp.x, wp.y, wp.z, 1f);
            Vector3 ndc = new Vector3(sc.x, sc.y, sc.z) / sc.w;
            Vector3 vp = new Vector3(ppsLight.x, ppsLight.y, ppsLight.z) / ppsLight.w; // 消失点
            Debug.Log($"[PSM v3] isParallel={isParallel} ppsLight.w={ppsLight.w:F4}\n" +
                      $"     消失点pp={vp} |pp|={vp.magnitude:F3} (キューブ内={vp.magnitude < 1.732f})\n" +
                      $"     clip.w={sc.w:F4}  ndc=({ndc.x:F3},{ndc.y:F3},{ndc.z:F3})");
        }

        return worldToShadowClip;
    }

    private void DrawShadowPSM(
        ScriptableRenderContext context,
        CommandBuffer cmd,
        CullingResults cullingResults,
        Matrix4x4 worldToShadowClip
    )
    {
        cmd.Clear();
        cmd.SetRenderTarget(LightShadowId);

        // ★ PSMは view=Identity / proj=WorldToShadowClip
        cmd.SetViewProjectionMatrices(Matrix4x4.identity, worldToShadowClip);
        context.ExecuteCommandBuffer(cmd);

        var sorting = new SortingSettings() { criteria = SortingCriteria.None };
        var draw = new DrawingSettings(ShadowCasterTag, sorting);
        var filter = new FilteringSettings(RenderQueueRange.opaque);

        context.DrawRenderers(cullingResults, ref draw, ref filter);

        cmd.Clear();
        cmd.SetGlobalTexture(LightShadow, LightShadowId);
        context.ExecuteCommandBuffer(cmd);
    }

    /// <summary>
    /// 描画用レンダーテクスチャのセットアップ
    /// </summary>
    private void SetupMainRT(ScriptableRenderContext context, CommandBuffer cmd, Camera camera)
    {
        cmd.Clear();
        cmd.GetTemporaryRT(RenderTarget, Screen.width, Screen.height, 32);
        cmd.SetRenderTarget(RenderTarget);
        cmd.ClearRenderTarget(true, false, camera.backgroundColor, 1);
        context.ExecuteCommandBuffer(cmd);
    }

    /// <summary>
    /// 描画用レンダーテクスチャのクリーンアップ
    /// </summary>
    private void CleanupMainRT(ScriptableRenderContext context, CommandBuffer cmd)
    {
        cmd.Clear();
        cmd.ReleaseTemporaryRT(RenderTarget);
        context.ExecuteCommandBuffer(cmd);
    }

    /// <summary>
    /// カメラのカリング
    /// </summary>
    /// <param name="shadowDistance">シャドウを投影する最大距離</param>
    /// <param name="cullingResults">CullingResults(取得用)</param>
    private bool TryCulling(ScriptableRenderContext context, Camera camera, float shadowDistance,
        out CullingResults cullingResults)
    {
        cullingResults = default;
        if (!camera.TryGetCullingParameters(false, out var cullingParameters))
        {
            return false;
        }

        cullingParameters.shadowDistance = Mathf.Clamp(shadowDistance, camera.nearClipPlane, camera.farClipPlane);
        cullingResults = context.Cull(ref cullingParameters);
        return true;
    }

    /// <summary>
    /// レンダーテクスチャからカメラのフレームバッファへのコピー
    /// </summary>
    private void Restore(ScriptableRenderContext context, CommandBuffer cmd)
    {
        cmd.Clear();
        cmd.Blit(RenderTargetId, CameraTargetId);
        context.ExecuteCommandBuffer(cmd);
    }

    /// <summary>
    /// 不透明物体の描画
    /// </summary>
    private void DrawOpaque(ScriptableRenderContext context, CommandBuffer cmd, Camera camera,
        CullingResults cullingResults)
    {
        // 描画用レンダーテクスチャにレンダーターゲットを切り替える
        cmd.Clear();
        cmd.SetRenderTarget(RenderTargetId);
        context.ExecuteCommandBuffer(cmd);

        // 描画順序とフィルタのデータの設定
        var opaqueSortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
        var opaqueDrawSettings = new DrawingSettings(RenderTagId, opaqueSortingSettings);
        var opaqueRenderQueueRange = new RenderQueueRange(0, (int)RenderQueue.GeometryLast);
        var opaqueFilterSettings = new FilteringSettings(opaqueRenderQueueRange, camera.cullingMask);

        // 不透明物体の描画
        context.DrawRenderers(cullingResults, ref opaqueDrawSettings, ref opaqueFilterSettings);
    }

    /// <summary>
    /// Skyboxの描画
    /// </summary>
    private void DrawSkybox(ScriptableRenderContext context, Camera camera)
    {
        context.DrawSkybox(camera);
    }

    /// <summary>
    /// 指定したタイプのライトのインデックスリストを取得する
    /// </summary>
    /// <param name="lightType">取得したいライトの種類</param>
    private List<int> SearchLightIndexes(CullingResults cullingResults, LightType lightType)
    {
        var lights = new List<int>();

        // カメラから見える範囲にあるライトの中から指定したタイプのライトを探す
        for (var i = 0; i < cullingResults.visibleLights.Length; i++)
        {
            var visibleLight = cullingResults.visibleLights[i];

            // 指定したタイプと異なればスキップ
            if (visibleLight.lightType != lightType)
            {
                continue;
            }

            var light = visibleLight.light;

            // シャドウが無効ならばスキップ
            if (light == null || light.shadows == LightShadows.None || light.shadowStrength <= 0)
            {
                continue;
            }

            // ライトに照らされる範囲にシャドウキャスターが存在しないならばスキップ
            if (!cullingResults.GetShadowCasterBounds(i, out var bounds))
            {
                continue;
            }

            lights.Add(i);
        }

        return lights;
    }

    /// <summary>
    /// 普通の平行投影シャドウ用の WorldToShadowClip 行列（比較用）。
    /// Unity 内製の ComputeDirectionalShadowMatricesAndCullingPrimitives で
    /// 視錐台にフィットした view/proj を作り、GPU射影を合成して返す。
    /// PSM と同じ _LightVP の枠組みに乗せるため、返り値は worldToShadowClip。
    /// </summary>
    private Matrix4x4 CalcUniformShadow(CullingResults cull, int lightIndex, int resolution)
    {
        var light = cull.visibleLights[lightIndex].light;
        cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(
            lightIndex,
            0, 1, Vector3.zero,
            resolution,
            light.shadowNearPlane,
            out var view, out var proj, out _);
        return GL.GetGPUProjectionMatrix(proj, true) * view;
    }

    /// <summary>
    /// ライトビュープロジェクション行列の計算
    /// </summary>
    /// <param name="lightIndex">ライトのインデックス</param>
    private Matrix4x4 CalcLightViewProjection(CullingResults cullingResults, int lightIndex)
    {
        var light = cullingResults.visibleLights[lightIndex].light;

        // ライトのビュー行列とプロジェクション行列を取得する
        cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
            lightIndex,
            0,
            1,
            Vector3.zero,
            0,
            light.shadowNearPlane,
            out var viewMatrix,
            out var projMatrix,
            out var shadowSplitData);

        // プロジェクション行列を描画ライブラリに適合した状態にする
        projMatrix = GL.GetGPUProjectionMatrix(projMatrix, true);

        // ビュー行列とプロジェクション行列を乗算して返す
        return projMatrix * viewMatrix;
    }

    /// <summary>
    /// ライトプロパティの設定(ViewProjection行列、ライトパラメータの設定など)
    /// </summary>
    /// <param name="light">プロパティを設定するライト</param>
    /// <param name="lightVP">ライトビュープロジェクション行列</param>
    /// <param name="shadowDistance">シャドウを投影する最大距離</param>
    private void SetupLightProperties(ScriptableRenderContext context, CommandBuffer cmd, Light light,
        Matrix4x4 lightVP, float shadowDistance)
    {
        cmd.Clear();
        // LVP行列をシェーダーに送信
        cmd.SetGlobalMatrix(LightVP, lightVP);
        // ライトの向きをシェーダーに送信
        cmd.SetGlobalVector(LightDir, -light.transform.forward);
        // ライトの色をシェーダーに送信
        cmd.SetGlobalColor(LightColor, light.color * light.intensity);
        // シャドウバイアスをシェーダーに送信
        cmd.SetGlobalFloat(ShadowBias, light.shadowBias);
        // シャドウ法線バイアスをシェーダーに送信
        cmd.SetGlobalFloat(ShadowNormalBias, light.shadowNormalBias);
        // シャドウを投影する最大距離をシェーダーに送信
        cmd.SetGlobalFloat(ShadowDistanceSqrt, shadowDistance * shadowDistance);
        context.ExecuteCommandBuffer(cmd);
    }

    /// <summary>
    /// シャドウマップ用レンダーテクスチャのセットアップ
    /// </summary>
    /// <param name="shadowResolution">シャドウマップの解像度</param>
    private void SetupLightRT(ScriptableRenderContext context, CommandBuffer cmd, int shadowResolution)
    {
        cmd.Clear();
        // 色を1チャネルの32bit、深度を32bitでシャドウマップを取得
        //cmd.GetTemporaryRT(LightShadow, shadowResolution, shadowResolution, 32, FilterMode.Bilinear,
        //  RenderTextureFormat.RFloat);
        // Depthだけ確保（24 or 32）
        cmd.GetTemporaryRT(
            LightShadow,
            shadowResolution, shadowResolution,
            32,
            FilterMode.Bilinear,
            RenderTextureFormat.Depth
        );

        cmd.SetRenderTarget(LightShadowId);
        cmd.ClearRenderTarget(true, true, Color.white, 1);
        context.ExecuteCommandBuffer(cmd);
    }

    /// <summary>
    /// シャドウマップ用レンダーテクスチャのクリーンアップ
    /// </summary>
    private void CleanupLightRT(ScriptableRenderContext context, CommandBuffer cmd)
    {
        cmd.Clear();
        //cmd.ReleaseTemporaryRT(RenderTarget);
        cmd.ReleaseTemporaryRT(LightShadow);
        context.ExecuteCommandBuffer(cmd);
    }
}