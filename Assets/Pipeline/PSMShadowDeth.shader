Shader "Hidden/PSMShadowDepth"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4x4 _ShadowVP;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float4 ws = mul(unity_ObjectToWorld, v.vertex);
                o.pos = mul(_ShadowVP, ws);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return 0; // depth only
            }
            ENDHLSL
        }
    }
}