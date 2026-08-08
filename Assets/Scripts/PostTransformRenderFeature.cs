using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PostTransformRenderFeature : ScriptableRendererFeature
{
    private PostTransparentPass _postTransparentPass;

    public override void Create()
    {
        _postTransparentPass = new PostTransparentPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_postTransparentPass);
    }
}
