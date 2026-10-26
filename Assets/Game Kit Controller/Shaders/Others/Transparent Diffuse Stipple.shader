Shader "Custom/URP/TransparentDiffuseStipple"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Main Color", Color) = (1,1,1,1)

        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.5

        _BumpMap("Normal Map", 2D) = "bump" {}

        _TransparentAmount("Transparent Amount", Range(0,1)) = 1
        _StippleSize("Stipple Size", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="AlphaTest"
        }

        Cull Off

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
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float4 screenPos   : TEXCOORD5;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Metallic;
                float _Smoothness;
                float _TransparentAmount;
                float _StippleSize;
            CBUFFER_END

            static const float4x4 kThresholdMatrix = {
                 1.0/17.0,  9.0/17.0,  3.0/17.0, 11.0/17.0,
                13.0/17.0,  5.0/17.0, 15.0/17.0,  7.0/17.0,
                 4.0/17.0, 12.0/17.0,  2.0/17.0, 10.0/17.0,
                16.0/17.0,  8.0/17.0, 14.0/17.0,  6.0/17.0
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv = IN.uv;

                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.tangentWS = TransformObjectToWorldDir(IN.tangentOS.xyz);
                OUT.bitangentWS = cross(OUT.normalWS, OUT.tangentWS) * IN.tangentOS.w;

                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float3 albedo = baseMap.rgb * _BaseColor.rgb;
                float alpha = baseMap.a * _BaseColor.a;

                float3 normalTS = UnpackNormal(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv)
                );

                float3x3 TBN = float3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS)
                );

                float3 normalWS = normalize(mul(normalTS, TBN));

                // --- STIPPLE ---
                float2 pos = IN.screenPos.xy / IN.screenPos.w;
                pos *= _ScreenParams.xy * _StippleSize;

                int x = (int)fmod(pos.x, 4);
                int y = (int)fmod(pos.y, 4);

                float threshold = kThresholdMatrix[y][x];

                clip(_TransparentAmount - threshold);
                // --- END STIPPLE ---

                InputData lightingInput;
                lightingInput.positionWS = IN.positionWS;
                lightingInput.normalWS = normalWS;
                lightingInput.viewDirectionWS =
                    normalize(GetWorldSpaceViewDir(IN.positionWS));
                lightingInput.shadowCoord =
                    TransformWorldToShadowCoord(IN.positionWS);
                lightingInput.bakedGI = SampleSH(normalWS);
                lightingInput.fogCoord = 0;
                lightingInput.vertexLighting = 0;

                SurfaceData surfaceData = (SurfaceData)0;

                surfaceData.albedo = albedo;
                surfaceData.alpha = alpha;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = 0;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1;
                surfaceData.emission = 0;

    
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 0;

                return UniversalFragmentPBR(lightingInput, surfaceData);
            }

            ENDHLSL
        }
    }
}