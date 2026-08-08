using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 実際の描画処理
/// </summary>
public class CustomRenderPass : ScriptableRenderPass
{
    private const string CommnadBufferName = nameof(CustomRenderPass);
    private const int RenderTextureId = 0;

    private RenderTargetIdentifier _currentTarget;

    private int _dawnSample = 1;

    /// <summary>
    /// 毎フレームの描画処理
    /// </summary>
    /// <param name="context"></param>
    /// <param name="renderingData"></param>
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        //描画処理
        var commandBuffer = CommandBufferPool.Get(CommnadBufferName);

        var cameraData = renderingData.cameraData;

        //現在描画しているカメラの解像度をdownSmapleで除算
        var w = cameraData.camera.scaledPixelWidth / _dawnSample;
        var h = cameraData.camera.scaledPixelHeight / _dawnSample;

        //Rendertexture生成
        commandBuffer.GetTemporaryRT(RenderTextureId, w, h, 0, FilterMode.Point, RenderTextureFormat.Default);

        //現在のカメラ描画画像をRenderTextureにコピー
        commandBuffer.Blit(_currentTarget, RenderTextureId);

        //RenderTextureを現在のRenderTarget(カメラ)に戻して映し出す
        commandBuffer.Blit(RenderTextureId, _currentTarget);

        context.ExecuteCommandBuffer(commandBuffer);
        context.Submit();

        CommandBufferPool.Release(commandBuffer);

    }

    /// <summary>
    /// コンストラクト
    /// </summary>
    public CustomRenderPass()
    {
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    /// <summary>
    /// Execute実行前にパラメーターを渡す
    /// </summary>
    /// <param name="renderTarget"></param>
    /// <param name="downSample"></param>
    public void SetParam(RenderTargetIdentifier renderTarget, int downSample)
    {
        _currentTarget = renderTarget;
        _dawnSample = downSample;
        if (downSample <= 0) _dawnSample = 1;
    }
}
