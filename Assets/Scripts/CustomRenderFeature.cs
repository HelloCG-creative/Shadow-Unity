using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CustomRenderFeature : ScriptableRendererFeature
{
    public int downSample = 10;

    CustomRenderPass _customRenderPass;

    public override void Create()
    {
        _customRenderPass = new CustomRenderPass();
    }

    //このメソッドはレンダラーをカメラごと設定する際に呼び出される
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        _customRenderPass.SetParam(renderer.cameraColorTarget, downSample);
        renderer.EnqueuePass(_customRenderPass);
    }
}
