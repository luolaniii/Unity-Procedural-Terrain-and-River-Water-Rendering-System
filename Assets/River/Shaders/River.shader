Shader "Universal Render Pipeline/River_LitIntegrated_URP16_Fixed2"
{
    Properties
    {
        [MainTexture]_Water_Height("Water Height", 2D) = "white" {}
        _Water_Normal("Water Normal", 2D) = "bump" {}
        _Pebble_Height("Pebble Height", 2D) = "white" {}
        _Pebble_Albodo("Pebble Albedo", 2D) = "white" {}
        _Pebble_Normal("Pebble Normal", 2D) = "bump" {}

        [MainColor]_Water_Tint("Water Tint", Color) = (0.1,0.3,0.4,1)
        _Water_Speed("Water Speed", Vector) = (0,0.5,0,0)

        [KeywordEnum(Final, WaterMask, BaseColor, Normal, WaterUV, PebbleUV, Heights, PHeight, WHeight, WaterNormal, PebbleNormal)]
        _DebugMode("Debug Mode", Float) = 0

        _Parallax_Amount("Parallax Amount", float) = 0.05
        _SDF_Multi("SDF Multi", Range(0,10)) = 1
        _Smooth_Blend("Smooth Blend", Range(0,10)) = 0.5
        _Refraction_Strength("Refraction Strength", Range(0,10)) = 0.5

        _Water_Normal_Strength("Water Normal Strength", Range(0,20)) = 1
        _Pebble_Normal_Strength("Pebble Normal Strength", Range(0,20)) = 0.8

        _Water_Smoothness("Water Smoothness", Range(0,1)) = 0.9
        _Pebble_Smoothness("Pebble Smoothness", Range(0,1)) = 0.4

        _Pebble_Depth("Pebble Depth", Range(0,10)) = 0.5
        _Pebble_Size("Pebble Size", Range(0,10)) = 0.5
        _Pebble_Offset("Pebble Offset", float) = 0.5

        _Water_Depth("Water Depth", float) = 0.5
        _Water_Deform_From_Height("Water Deform From Height", float) = 0.5
        _Water_Offset("Water Offset", float) = 0.5
        _Water_Size("Water Size", float) = 0.5

        _Noise_Scale("Noise Scale", Range(0.01, 10)) = 1.0
        _Noise_Strength("Noise Strength", Range(0, 1)) = 0.2
        _Noise_Speed("Noise Speed", Range(0, 5)) = 0.5
        _Noise_Scale_Detail("Noise Detail Scale", Range(0.01, 20)) = 5.0
        _Noise_Strength_Detail("Noise Detail Strength", Range(0, 1)) = 0.3
        _Water_Scattering("Water Scattering", Range(0, 1)) = 0.3
        _Water_Depth_Fade("Water Depth Fade", Range(0, 10)) = 2.0

    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        LOD 200

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _DEBUGMODE_FINAL _DEBUGMODE_WATERMASK _DEBUGMODE_BASECOLOR _DEBUGMODE_NORMAL \
                                   _DEBUGMODE_WATERUV _DEBUGMODE_PEBBLEUV _DEBUGMODE_HEIGHTS _DEBUGMODE_PHEIGHT \
                                   _DEBUGMODE_WHEIGHT _DEBUGMODE_WATERNORMAL _DEBUGMODE_PEBBLENORMAL
            #pragma target 3.5

            // URP includes (URP16)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // textures
            TEXTURE2D(_Water_Height); SAMPLER(sampler_Water_Height);
            TEXTURE2D(_Water_Normal); SAMPLER(sampler_Water_Normal);
            TEXTURE2D(_Pebble_Height); SAMPLER(sampler_Pebble_Height);
            TEXTURE2D(_Pebble_Albodo); SAMPLER(sampler_Pebble_Albodo);
            TEXTURE2D(_Pebble_Normal); SAMPLER(sampler_Pebble_Normal);
            // _CameraOpaqueTexture 已在 DeclareOpaqueTexture.hlsl 中声明，无需重复声明

            float4 _Water_Tint;
            float4 _Water_Speed;

            float _Parallax_Amount;
            float _SDF_Multi;
            float _Smooth_Blend;
            float _Refraction_Strength;

            float _Water_Normal_Strength;
            float _Pebble_Normal_Strength;

            float _Water_Smoothness;
            float _Pebble_Smoothness;

            float _Pebble_Depth;
            float _Pebble_Size;
            float _Pebble_Offset;

            float _Water_Depth;
            float _Water_Deform_From_Height;
            float _Water_Offset;
            float _Water_Size;

            float _Noise_Scale;
            float _Noise_Strength;
            float _Noise_Speed;
            float _Noise_Scale_Detail;
            float _Noise_Strength_Detail;
            float _Water_Scattering;
            float _Water_Depth_Fade;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                float3 wnormal : TEXCOORD2;
                float3 wtangent : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float4 clipPos = TransformObjectToHClip(v.positionOS);
                o.positionCS = clipPos;
                o.screenPos = ComputeScreenPos(clipPos);
                o.uv = v.uv;

                float3 worldPos = mul(unity_ObjectToWorld, v.positionOS).xyz;
                o.wpos = worldPos;

                o.wnormal = TransformObjectToWorldNormal(v.normalOS);
                o.wtangent = normalize(mul((float3x3)unity_ObjectToWorld, v.tangent.xyz));

                return o;
            }

            float4 SampleTex2D(TEXTURE2D(t), SAMPLER(s), float2 uv)
            {
                return SAMPLE_TEXTURE2D(t, s, uv);
            }

            // 简单的伪随机函数
            float random(float2 st)
            {
                return frac(sin(dot(st.xy, float2(12.9898, 78.233))) * 43758.5453123);
            }

            // 平滑插值函数
            float smoothInterp(float a, float b, float t)
            {
                t = t * t * (3.0 - 2.0 * t);
                return lerp(a, b, t);
            }

            // Value Noise - 2D噪声函数
            float noise(float2 st)
            {
                float2 i = floor(st);
                float2 f = frac(st);
                
                float a = random(i);
                float b = random(i + float2(1.0, 0.0));
                float c = random(i + float2(0.0, 1.0));
                float d = random(i + float2(1.0, 1.0));
                
                float2 u = f * f * (3.0 - 2.0 * f);
                
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 分形噪声（多层叠加）
            float fbm(float2 st, int octaves)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                
                for (int i = 0; i < octaves; i++)
                {
                    value += amplitude * noise(st * frequency);
                    frequency *= 2.0;
                    amplitude *= 0.5;
                }
                
                return value;
            }

            float2 GetScreenUV(float4 screenPos)
            {
                float2 uv = screenPos.xy / screenPos.w;
                #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1.0 - uv.y;
                #endif
                return uv;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 worldPos = i.wpos;
                float3 worldNormal = normalize(i.wnormal);
                float3 wTangent = normalize(i.wtangent);
                float3 wBinormal = normalize(cross(worldNormal, wTangent));

                // main directional light (best-effort fallback)
                float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                float2 screenUV = GetScreenUV(i.screenPos);

                // 1. Pebble UV and height (在计算UV时直接应用噪声扰动)
                float2 pebbleUV = i.uv * _Pebble_Size;
                
                // 基于UV本身计算噪声，打破纹理平铺重复
                float2 pebbleNoiseUV = pebbleUV * _Noise_Scale + _Time.y * _Noise_Speed * 0.1;
                float2 pebbleNoiseDetailUV = pebbleUV * _Noise_Scale_Detail + _Time.y * _Noise_Speed * 0.15;
                
                // 低频噪声（大范围变化）
                float2 pebbleNoiseOffset = float2(
                    fbm(pebbleNoiseUV, 3) - 0.5,
                    fbm(pebbleNoiseUV + float2(100.0, 100.0), 3) - 0.5
                ) * _Noise_Strength;
                
                // 高频噪声（细节扰动，打破小尺度重复）
                float2 pebbleNoiseDetail = float2(
                    fbm(pebbleNoiseDetailUV, 2) - 0.5,
                    fbm(pebbleNoiseDetailUV + float2(50.0, 50.0), 2) - 0.5
                ) * _Noise_Strength_Detail;
                
                // 组合噪声并应用到UV
                pebbleUV += pebbleNoiseOffset + pebbleNoiseDetail;
                
                float3 pebbleHeightSample = SampleTex2D(_Pebble_Height, sampler_Pebble_Height, frac(pebbleUV));
                float p_height = pebbleHeightSample.r * _Pebble_Depth + _Pebble_Offset;

                // 2. Water UV (基于鹅卵石高度变形，不需要额外噪声)
                float2 waterUV = i.uv * _Water_Size;
                waterUV += frac(_Water_Speed.xy * _Time.y);

                float deform_water = (p_height - 0.5) * _Water_Deform_From_Height;
                waterUV += deform_water;

                // 3. Water height
                float w_height = SampleTex2D(_Water_Height, sampler_Water_Height, frac(waterUV)).r * _Water_Depth + _Water_Offset;

                // 4. SDF Blend
                float h = clamp(0.5 + 0.5 * (p_height - (-w_height)) / _Smooth_Blend, 0.0, 1.0);
                float smoothSubtract = lerp(p_height, -w_height, h) + _Smooth_Blend * h * (1.0 - h);
                float waterMask = saturate(_SDF_Multi * smoothSubtract);

                // 5. Water normal (tangent space then to world)
                float3 waterNormalTS = SampleTex2D(_Water_Normal, sampler_Water_Normal, frac(waterUV)).xyz * 2 - 1;
                waterNormalTS.xy *= _Water_Normal_Strength;
                waterNormalTS.z = sqrt(max(0.0, 1.0 - saturate(dot(waterNormalTS.xy, waterNormalTS.xy))));
                waterNormalTS = normalize(waterNormalTS);

                float3x3 tangentToWorld = float3x3(
                    wTangent.x, wBinormal.x, worldNormal.x,
                    wTangent.y, wBinormal.y, worldNormal.y,
                    wTangent.z, wBinormal.z, worldNormal.z
                );
                float3 waterNormalWS = normalize(
                    wTangent * waterNormalTS.x +
                    wBinormal * waterNormalTS.y +
                    worldNormal * waterNormalTS.z
                );

                // 6. Parallax + refraction offset
                float3x3 worldToTangent = transpose(tangentToWorld);
                float3 viewDirTS = mul(worldToTangent, viewDir);
                float2 parallaxOffset = viewDirTS.xy / (viewDirTS.z + 0.42);
                float p_Amount = _Parallax_Amount * 0.01;
                float Re_Strength = _Refraction_Strength * 0.01;
                float3 refractionOffset = waterNormalTS * Re_Strength + float3(0, 0, p_Amount);
                float2 finalParallaxOffset = parallaxOffset * refractionOffset.xy;
                float2 parallaxUV = pebbleUV + finalParallaxOffset;

                // 7. Pebble normal
                float3 pebbleNormalTS = SampleTex2D(_Pebble_Normal, sampler_Pebble_Normal, frac(parallaxUV)).xyz * 2 - 1;
                pebbleNormalTS.xy *= _Pebble_Normal_Strength;
                pebbleNormalTS.z = sqrt(max(0.0, 1.0 - saturate(dot(pebbleNormalTS.xy, pebbleNormalTS.xy))));
                pebbleNormalTS = normalize(pebbleNormalTS);

                // Mix normals in tangent space and convert to world
                float waterBlend = smoothstep(0.2, 0.6, waterMask);
                float3 finalNormalTS = normalize(lerp(pebbleNormalTS, waterNormalTS, waterBlend));
                float3 finalNormalWS = normalize(
                    wTangent * finalNormalTS.x +
                    wBinormal * finalNormalTS.y +
                    worldNormal * finalNormalTS.z
                );

                // 8. Colors
                float3 pebbleCol = SampleTex2D(_Pebble_Albodo, sampler_Pebble_Albodo, frac(parallaxUV)).rgb;
                float3 waterCol = _Water_Tint.rgb * pebbleCol;
                float smoothness = lerp(_Pebble_Smoothness, _Water_Smoothness, waterMask);
                float roughness = 1.0 - smoothness;

                // 简单水深与散射
                float depthDiff = saturate((w_height - p_height) * _Water_Depth_Fade);
                float scatteringFactor = lerp(1.0, _Water_Scattering, depthDiff);
                float3 underwaterColor = lerp(pebbleCol, waterCol, scatteringFactor);
                underwaterColor = lerp(underwaterColor, waterCol, waterMask * 0.5);

                float3 baseColor = lerp(pebbleCol, underwaterColor, waterMask);

                #if defined(_DEBUGMODE_WATERMASK)
                    return float4(waterMask, waterMask, waterMask, 1);
                #elif defined(_DEBUGMODE_NORMAL)
                    return float4(finalNormalWS * 0.5 + 0.5, 1);
                #elif defined(_DEBUGMODE_WATERUV)
                    return float4(frac(waterUV), 0, 1);
                #elif defined(_DEBUGMODE_PEBBLEUV)
                    return float4(frac(pebbleUV), 0, 1);
                #elif defined(_DEBUGMODE_HEIGHTS)
                    return float4(p_height, w_height, 0, 1);
                #elif defined(_DEBUGMODE_PHEIGHT)
                    return float4(p_height, p_height, p_height, 1);
                #elif defined(_DEBUGMODE_WHEIGHT)
                    return float4(w_height, w_height, w_height, 1);
                #elif defined(_DEBUGMODE_WATERNORMAL)
                    return float4(waterNormalTS * 0.5 + 0.5, 1);
                #elif defined(_DEBUGMODE_PEBBLENORMAL)
                    return float4(pebbleNormalTS * 0.5 + 0.5, 1);
                #elif defined(_DEBUGMODE_BASECOLOR)
                    return float4(baseColor, 1);
                #endif

                float3 ambient = SampleSH(finalNormalWS);
                float3 lighting = baseColor * ambient;

                float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
                Light mainLight = GetMainLight(shadowCoord);
                float NdotL = saturate(dot(finalNormalWS, mainLight.direction));
                lighting += baseColor * mainLight.color * NdotL * mainLight.shadowAttenuation;

                float3 halfDir = normalize(mainLight.direction + viewDir);
                float specPow = lerp(8.0, 128.0, smoothness);
                float specStrength = pow(saturate(dot(finalNormalWS, halfDir)), specPow) * smoothness;
                float3 specular = specStrength * mainLight.color * waterMask;

                float3 finalColor = lighting + specular;

                return float4(finalColor, 1.0);
            }

            ENDHLSL
        }
    }
}
