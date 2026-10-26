Shader "Outlined/URP/Diffuse"
{
    Properties
    {
        _BaseColor ("Main Color", Color) = (.5,.5,.5,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _Outline ("Outline Width", Range(0,1)) = 0.1
        _BaseMap ("Base Map", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        // ----------------------
        // OUTLINE PASS
        // ----------------------

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
            float _Outline;
            float4 _OutlineColor;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                float3 pos = IN.positionOS.xyz * (1 + _Outline);

                float3 positionWS = TransformObjectToWorld(pos);
                OUT.positionHCS = TransformWorldToHClip(positionWS);

                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }

            ENDHLSL
        }

        // ----------------------
        // MAIN LIT PASS
        // ----------------------

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);

                OUT.uv = IN.uv;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);

                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float3 albedo = tex.rgb * _BaseColor.rgb;

                float3 normal = normalize(IN.normalWS);

                Light mainLight = GetMainLight();

                float NdotL = saturate(dot(normal, mainLight.direction));

                float3 color = albedo * mainLight.color * NdotL;

                return float4(color, tex.a);
            }

            ENDHLSL
        }
    }
}