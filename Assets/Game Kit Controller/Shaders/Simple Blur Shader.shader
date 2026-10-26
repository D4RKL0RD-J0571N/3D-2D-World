Shader "Custom/URP/SimpleBlur"
{
    Properties
    {
        _Size ("Blur", Range(0,30)) = 1
        _MainTex ("Mask", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent-100"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Blur"

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            float4 _CameraOpaqueTexture_TexelSize;

            CBUFFER_START(UnityPerMaterial)
            float _Size;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                OUT.color = IN.color;

                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a * IN.color.a;

                half4 sum = 0;

                float2 texel = _CameraOpaqueTexture_TexelSize.xy * _Size * alpha;

                sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + texel * float2(-4,0)) * 0.05;
                sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + texel * float2(-3,0)) * 0.09;
                sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + texel * float2(-2,0)) * 0.12;
                sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + texel * float2(-1,0)) * 0.15;
                sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV) * 0.18;
                sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + texel * float2(1,0)) * 0.15;
                sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + texel * float2(2,0)) * 0.12;
                sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + texel * float2(3,0)) * 0.09;
                sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + texel * float2(4,0)) * 0.05;

                // vertical blur
                sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + texel * float2(0,-4)) * 0.05;
                sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + texel * float2(0,-3)) * 0.09;
                sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + texel * float2(0,-2)) * 0.12;
                sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + texel * float2(0,-1)) * 0.15;
                sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + texel * float2(0,1)) * 0.15;
                sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + texel * float2(0,2)) * 0.12;
                sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + texel * float2(0,3)) * 0.09;
                sum += SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + texel * float2(0,4)) * 0.05;

                return sum;
            }

            ENDHLSL
        }
    }
}