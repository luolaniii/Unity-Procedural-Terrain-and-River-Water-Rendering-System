Shader "Universal Render Pipeline/River/Water"
{
    Properties
    {
        _NormalTex ("Normal", 2D) = "white" {}
        _FoamNoiseTex("Foam Noise", 2D) = "white"{}
        _Fresnel("Fresnel",2D) = "white"{}
        _Foam_Softness ("Foam Softness", Range(0,1)) = 0.18
        _WaveSpeed("WaveSpeed",vector) = (0.5,0.5,-0.5,-0.5)
        _DistortAmount("Distort Amount",float) = 100
        _FoamFade("Foam Fade",Range(0,2)) = 1
        _BaseColor ("Base Color", Color) = (0.172, 0.463, 0.435, 0.7)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _REQUIRES_OPAQUE_TEXTURE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NormalTex);
            SAMPLER(sampler_NormalTex);
            TEXTURE2D(_FoamNoiseTex);
            SAMPLER(sampler_FoamNoiseTex);
            TEXTURE2D(_Fresnel);
            SAMPLER(sampler_Fresnel);

            CBUFFER_START(UnityPerMaterial)
                float4 _NormalTex_ST;
                float4 _FoamNoiseTex_ST;
                float4 _BaseColor;
                float4 _WaveSpeed;
                float _DistortAmount;
                float _FoamFade;
                float _Foam_Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 tangentWS  : TEXCOORD2;
                float3 bitangentWS: TEXCOORD3;
                float2 uvNormal   : TEXCOORD4;
                float2 uvFoam     : TEXCOORD5;
                float3 viewDirWS  : TEXCOORD6;
                float4 screenPos  : TEXCOORD7;
            };

            Varyings vert (Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;
                output.bitangentWS = normalInputs.bitangentWS;
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                output.uvNormal = TRANSFORM_TEX(input.uv, _NormalTex);
                output.uvFoam = TRANSFORM_TEX(input.uv, _FoamNoiseTex);
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);

                return output;
            }

            float3 SampleWaterNormal(float2 uv, float3 tangent, float3 bitangent, float3 normal)
            {
                float3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv + _WaveSpeed.xy * _Time.x), 1.0);
                float3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv + _WaveSpeed.zw * _Time.x), 1.0);
                float3 blended = normalize(n1 + n2);

                float3x3 TBN = float3x3(normalize(tangent), normalize(bitangent), normalize(normal));
                return normalize(mul(blended, TBN));
            }

            half4 frag (Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 tangentWS = normalize(input.tangentWS);
                float3 bitangentWS = normalize(input.bitangentWS);
                float3 viewDir = normalize(input.viewDirWS);

                float3 waterNormal = SampleWaterNormal(input.uvNormal, tangentWS, bitangentWS, normalWS);
                float NdotUp = 1 - saturate(dot(normalWS, float3(0,1,0)));

                float3 foamNoise = SAMPLE_TEXTURE2D(_FoamNoiseTex, sampler_FoamNoiseTex, input.uvFoam - float2(0.05,0.1) * _Time.y * abs(_WaveSpeed.y)).rgb;

                float3 finalColor = _BaseColor.rgb;

                #if defined(REQUIRES_OPAQUE_TEXTURE)
                    float2 screenUV = input.screenPos.xy / input.screenPos.w;
                    #if UNITY_UV_STARTS_AT_TOP
                        screenUV.y = 1.0 - screenUV.y;
                    #endif
                    float2 grabOffset = waterNormal.xy * _DistortAmount * _CameraOpaqueTexture_TexelSize.xy;
                    float4 opaqueSample = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + grabOffset);
                    finalColor = opaqueSample.rgb;
                #endif

                finalColor += foamNoise * (_Foam_Softness + NdotUp * 2.0);

                float fresnelFactor = saturate(dot(viewDir, waterNormal));
                float fresnel = SAMPLE_TEXTURE2D(_Fresnel, sampler_Fresnel, float2(fresnelFactor, fresnelFactor)).r;
                float3 reflectionColor = float3(0.65, 0.85, 0.95);
                finalColor = lerp(finalColor, reflectionColor, fresnel);

                half4 output;
                output.rgb = finalColor;
                output.a = saturate(_BaseColor.a + _FoamFade * 0.1);

                #ifdef UNIVERSAL_FRAGMENT_FOG
                    output.rgb = ApplyFog(output.rgb, input.positionWS);
                #endif

                return output;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/InternalErrorShader"
}
