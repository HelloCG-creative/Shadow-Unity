using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PostTransparentPass : ScriptableRenderPass
{
    //FrameDebugerやProfiler用の名前
    private const string ProfilerTag = nameof(PostTransparentPass);
    private readonly ProfilingSampler _profilingSampler = new ProfilingSampler(ProfilerTag);

    //どのタイミングでレンダリングするか
    private readonly RenderPassEvent _renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

    //対象とするRenderQueue
    private readonly RenderQueueRange _renderQueueRange = RenderQueueRange.all;

    //ShaderのTagsでLightModeがこれになっているシェーダーのみレンダリング対象
    private readonly ShaderTagId _shaderTagId = new ShaderTagId(nameof(PostTransparentPass).Replace("Pass", ""));

    private FilteringSettings _filteringSettings;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public PostTransparentPass()
    {
        _filteringSettings = new FilteringSettings(_renderQueueRange);
        renderPassEvent = _renderPassEvent;
    }

    /// <summary>
    /// レンダリング処理前に呼ばれる
    /// レンダーターゲットを変えたりできる
    /// </summary>
    /// <param name="cmd"></param>
    /// <param name="cameraTextureDescriptor"></param>
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        base.Configure(cmd, cameraTextureDescriptor);
    }

    //レンダリング処理を書く
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get(ProfilerTag);
        using(new ProfilingScope(cmd, _profilingSampler))
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);

            var drawingSetting = CreateDrawingSettings(_shaderTagId, ref renderingData, SortingCriteria.CommonTransparent);
            context.DrawRenderers(renderingData.cullResults, ref drawingSetting, ref _filteringSettings);
        }
    }
}
