Shader "Hidden/ShadowDepthColor"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4x4 _LightVP;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 clip : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                float4 ws = mul(unity_ObjectToWorld, v.vertex);
                o.clip = mul(_LightVP, ws);
                return o;
            }

            float frag(v2f i) : SV_Target
            {
                float d = i.clip.z / i.clip.w;   // ★PSM後のz/w
                return d;
            }
            ENDHLSL
        }
    }
}