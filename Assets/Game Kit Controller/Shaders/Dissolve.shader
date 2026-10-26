Shader "Custom/URP/Dissolve"
{
    Properties
    {
        [MainTexture]
        _BaseMap ("Base Map", 2D) = "white" {}

        [MainColor]
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        _BumpMap ("Normal Map", 2D) = "bump" {}

        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Smoothness ("Smoothness", Range(0,1)) = 0.5

        _DissolveTexture ("Dissolve Texture", 2D) = "white" {}
        _Amount ("Amount", Range(0,1)) = 0

        _DissolveColor ("Dissolve Color", Color) = (1,1,1,1)
        _DissolveBorder ("Dissolve Border", Range(0,0.2)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM

            #pragma target 3.0

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 tangentWS   : TEXCOORD2;
                float2 uv          : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            TEXTURE2D(_DissolveTexture);
            SAMPLER(sampler_DissolveTexture);

            CBUFFER_START(UnityPerMaterial)

                float4 _BaseMap_ST;

                float4 _BaseColor;

                float _Metallic;
                float _Smoothness;

                float _Amount;

                float4 _DissolveColor;
                float _DissolveBorder;

            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(IN.positionOS.xyz);

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(
                        IN.normalOS,
                        IN.tangentOS
                    );

                OUT.positionHCS = positionInputs.positionCS;
                OUT.positionWS  = positionInputs.positionWS;

                OUT.normalWS = normalInputs.normalWS;

                OUT.tangentWS = float4(
                    normalInputs.tangentWS,
                    IN.tangentOS.w
                );

                OUT.uv =
                    TRANSFORM_TEX(IN.uv, _BaseMap);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // =========================
                // DISSOLVE
                // =========================

                float dissolve =
                    SAMPLE_TEXTURE2D(
                        _DissolveTexture,
                        sampler_DissolveTexture,
                        IN.uv
                    ).r;

                clip(dissolve - _Amount);

                // emissive edge
                float border =
                    step(
                        dissolve - _Amount,
                        _DissolveBorder
                    );

                // =========================
                // BASE MAP
                // =========================

                float4 baseTex =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        IN.uv
                    );

                float3 albedo =
                    baseTex.rgb *
                    _BaseColor.rgb;

                // =========================
                // NORMAL MAP
                // =========================

                float3 normalTS =
                    UnpackNormal(
                        SAMPLE_TEXTURE2D(
                            _BumpMap,
                            sampler_BumpMap,
                            IN.uv
                        )
                    );

                // =========================
                // SURFACE DATA
                // =========================

                SurfaceData surfaceData =
                    (SurfaceData)0;

                surfaceData.albedo = albedo;

                surfaceData.alpha =
                    baseTex.a *
                    _BaseColor.a;

                surfaceData.metallic =
                    _Metallic;

                surfaceData.smoothness =
                    _Smoothness;

                surfaceData.normalTS =
                    normalTS;

                surfaceData.occlusion = 1;

                surfaceData.emission =
                    _DissolveColor.rgb *
                    border;

                surfaceData.specular = 0;

                // =========================
                // INPUT DATA
                // =========================

                InputData inputData =
                    (InputData)0;

                inputData.positionWS =
                    IN.positionWS;

                inputData.normalWS =
                    normalize(IN.normalWS);

                inputData.viewDirectionWS =
                    GetWorldSpaceNormalizeViewDir(
                        IN.positionWS
                    );

                inputData.shadowCoord =
                    TransformWorldToShadowCoord(
                        IN.positionWS
                    );

                inputData.fogCoord =
                    ComputeFogFactor(
                        IN.positionHCS.z
                    );

                inputData.vertexLighting =
                    VertexLighting(
                        IN.positionWS,
                        inputData.normalWS
                    );

                inputData.bakedGI =
                    SampleSH(
                        inputData.normalWS
                    );

                // =========================
                // FINAL PBR
                // =========================

                half4 color =
                    UniversalFragmentPBR(
                        inputData,
                        surfaceData
                    );

                color.rgb =
                    MixFog(
                        color.rgb,
                        inputData.fogCoord
                    );

                return color;
            }

            ENDHLSL
        }
    }
}