Shader "PSM/Receiver"
{
    Properties
    {
        _BaseColor ("BaseColor", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            ZWrite On
            ZTest LEqual
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BaseColor;

            float4x4 _PSMWorldToShadowClip;
            sampler2D _ShadowTex;    // ★ Depth RT を sampler2D として読む
            float _ShadowBias;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 posCS : SV_POSITION;
                float3 posWS : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.posCS = UnityObjectToClipPos(v.vertex);
                o.posWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 sc = mul(_PSMWorldToShadowClip, float4(i.posWS, 1));
                sc.xyz /= sc.w;

                float2 uv = sc.xy * 0.5 + 0.5;

                // 範囲外は影なし
                if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                    return _BaseColor;

                // ★Depth取得：tex2D の .r を使う（環境差が少ない）
                float shadowD = tex2D(_ShadowTex, uv).r;

                // reversed-Z(Metal/DX)：近い=値が大きい → lit なら receiver >= shadowD
                // 非reversed(OpenGL)：近い=値が小さい → lit なら receiver <= shadowD
                #if defined(UNITY_REVERSED_Z)
                    float receiver = sc.z + _ShadowBias;
                    float lit = (receiver >= shadowD) ? 1.0 : 0.3;
                #else
                    float receiver = sc.z - _ShadowBias;
                    float lit = (receiver <= shadowD) ? 1.0 : 0.3;
                #endif

                return fixed4(_BaseColor.rgb * lit, 1);
            }
            ENDCG
        }

        // ShadowCaster（必須）
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return 0;
            }
            ENDCG
        }
    }
}
