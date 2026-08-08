using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DownSampleAnimation : MonoBehaviour
{
    [SerializeField] private CustomRenderFeature _customRenderFeature;

    private void Update()
    {
        var tmp = (Mathf.Sin(Time.frameCount * Mathf.PI / 180f) + 1) / 2;

        var value = tmp * 120f;

        _customRenderFeature.downSample = (int)value;
    }
}
