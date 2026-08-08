Shader "Unlit/Sample"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "RenderPipeline" = "ShadowRenderPipeline"
        }
        UsePass "Hidden/ShadowCaster/SHADOW_CASTER"

        Pass
        {
            Tags {"LightMode" = "Forward"}
            LOD 100
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Light.hlsl"

            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 normalWS : NORMAL;
                float4 positionCS : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.positionCS = UnityObjectToClipPos(v.positionOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.positionWS = mul(unity_ObjectToWorld, v.positionOS);
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half3 color = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                half3 diffuse = 0;
                
                return half4(color, 1);
            }
            ENDCG
        }
    }
}