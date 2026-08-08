Shader "Custom/SampleLit"
{
    Properties
    {
        _MainTex ("MainTex", 2D) = "white" {}
        _BaseColor ("BaseColor", Color) = (0.8,0.8,0.8,1)
        _ShadowColor ("ShadowColor", Color) = (0.1,0.1,0.1,1)
        _Bias ("Shadow Bias", Float) = 0.0005
    }

    SubShader
    {
        Tags
        {
            "Queue"="Geometry"
            "RenderType"="Opaque"
            "RenderPipeline"="ShadowRenderPipeline"
        }

        Pass
        {
            Tags
            {
                "LightMode" = "Forward"
            }

            HLSLPROGRAM
            #include "UnityCG.cginc"
            #include "Light.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                // 頂点の座標(オブジェクト空間)
                float4 positionOS : POSITION;
                // 頂点のUV座標
                float2 uv : TEXCOORD0;
                // 頂点の法線(オブジェクト空間)
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                // クリップ空間の座標
                float4 positionCS : SV_POSITION;
                // ワールド空間の座標
                float4 positionWS : TEXCOORD0;
                // UV座標
                float2 uv : TEXCOORD1;
                // ワールド空間の法線
                float3 normalWS : NORMAL;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _BaseColor;
            half4 _ShadowColor;

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = UnityObjectToClipPos(i.positionOS);
                // 頂点座標をオブジェクト空間からワールド空間に変換
                o.positionWS = mul(unity_ObjectToWorld, i.positionOS);
                // 法線をオブジェクト空間からワールド空間に変換
                o.normalWS = UnityObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // ディレクショナルライトの取得
                DirectionalLight light = GetDirectionalLight();
                // 法線の向きとライトの向きの内積を求める
                float dotNL = saturate(dot(normalize(i.normalWS), light.lightDir));

                half3 color = tex2D(_MainTex, i.uv).xyz;
                half alpha = tex2D(_MainTex, i.uv).a;
                clip(alpha - 0.5);
                color *= _BaseColor.xyz;
                
                //return float4(i.uv, 0, 1);

                // Lambert拡散反射光を求める
                half3 diffuse = light.lightColor * dotNL;
                // ライトの減衰度を求める
                float4 shadowAttenuation = GetShadowAttenuation(i.positionWS, dotNL); 

                float shadow = shadowAttenuation.r; // 1=光, 0=影

                // 影が diffuse の大小に関係なく見えるように、色全体を暗く落とす。
                // 影の所は base の 20%、光の所は 40%環境光 + 直接光。
                half3 lit    = color * 0.4 + color * diffuse;
                half3 shaded = color * 0.2;
                color = lerp(shaded, lit, shadow);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // シャドウキャスター：このマテリアルの物体を _LightShadow に焼く（影を落とす側）
        Pass
        {
            Tags
            {
                "LightMode" = "ShadowCaster"
                "RenderPipeline" = "ShadowRenderPipeline"
            }

            HLSLPROGRAM
            #include "Light.hlsl"
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionLVP : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionLVP = TransformObjectToLightViewProjection(i.positionOS);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }

            float frag(Varyings i) : SV_Target
            {
                // Forward パスと同じアルファ抜き → 影も同じ形にする
                clip(tex2D(_MainTex, i.uv).a - 0.5);
                return CalcLightViewProjectionDepth(i.positionLVP);
            }
            ENDHLSL
        }
    }
}
