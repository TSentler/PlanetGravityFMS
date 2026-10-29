// Шейдер "заражённой" планеты для URP.
// Параметр _Infection (0..1) двигает фронт заражения от точки _Origin по поверхности планеты.
// При _Infection = 0 виден исходный материал (BaseMap * BaseColor), при 1 планета заражена целиком.
// Форма фронта и жилки считаются шумом по направлению от центра планеты, поэтому UV не нужны
// и результат не зависит от масштаба меша. Пивот планеты должен стоять в её центре.
Shader "Custom/URP/InfectedPlanet"
{
    Properties
    {
        [Header(Original look)]
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Infection)]
        _Infection ("Infection (0 clean, 1 infected)", Range(0, 1)) = 1
        _Origin ("Origin Direction (object space)", Vector) = (0, 1, 0, 0)
        _InfectedColor ("Infected Color A", Color) = (0.16, 0.04, 0.22, 1)
        _InfectedColor2 ("Infected Color B", Color) = (0.32, 0.08, 0.30, 1)
        _NoiseScale ("Noise Scale", Range(0.5, 12)) = 3

        [Header(Front)]
        _EdgeNoise ("Front Raggedness", Range(0, 1)) = 0.4
        _EdgeWidth ("Front Width", Range(0.005, 0.4)) = 0.08
        [HDR] _EdgeColor ("Front Glow Color", Color) = (1.5, 0.4, 1.8, 1)
        _EdgeStrength ("Front Glow Strength", Range(0, 4)) = 1.5

        [Header(Veins)]
        [HDR] _VeinColor ("Vein Glow Color", Color) = (1.2, 0.1, 1.6, 1)
        _VeinStrength ("Vein Strength", Range(0, 4)) = 1.2
        _VeinSharpness ("Vein Sharpness", Range(1, 16)) = 8
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // Общие объявления для всех проходов (одинаковый CBUFFER нужен для SRP Batcher)
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Infection;
            float4 _Origin;
            half4 _InfectedColor;
            half4 _InfectedColor2;
            half _NoiseScale;
            half _EdgeNoise;
            half _EdgeWidth;
            half4 _EdgeColor;
            half _EdgeStrength;
            half4 _VeinColor;
            half _VeinStrength;
            half _VeinSharpness;
            half _PulseSpeed;
        CBUFFER_END
        ENDHLSL

        // ---------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS   : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
            };

            // ----- шум -----
            float Hash13(float3 p3)
            {
                p3 = frac(p3 * 0.1031);
                p3 += dot(p3, p3.zyx + 31.32);
                return frac((p3.x + p3.y) * p3.z);
            }

            float ValueNoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash13(i + float3(0, 0, 0));
                float n100 = Hash13(i + float3(1, 0, 0));
                float n010 = Hash13(i + float3(0, 1, 0));
                float n110 = Hash13(i + float3(1, 1, 0));
                float n001 = Hash13(i + float3(0, 0, 1));
                float n101 = Hash13(i + float3(1, 0, 1));
                float n011 = Hash13(i + float3(0, 1, 1));
                float n111 = Hash13(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);

                return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
            }

            // Три октавы, результат нормализован в диапазон 0..1
            float Fbm01(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                [unroll]
                for (int k = 0; k < 3; k++)
                {
                    v += a * ValueNoise(p);
                    p *= 2.03;
                    a *= 0.5;
                }
                return v / 0.875;
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = nrm.normalWS;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // Направление от центра планеты: основа для шума и расстояния от источника
                float3 dirOS = normalize(IN.positionOS);
                float3 originDir = normalize(_Origin.xyz + float3(0.0, 1e-5, 0.0));

                float noise = Fbm01(dirOS * _NoiseScale);

                // Угловое расстояние от источника: 0 в источнике, 1 на противоположной стороне
                float angle = acos(clamp(dot(dirOS, originDir), -1.0, 1.0)) / PI;

                // Рваный фронт: шум сдвигает расстояние
                float E = _EdgeNoise;
                float W = max(_EdgeWidth, 0.0001);
                float m = angle + (noise - 0.5) * E;

                // Положение фронта: при 0 заражения мёртвая зона ниже любого m, при 1 выше любого m
                float thr = lerp(-E * 0.5, 1.0 + E * 0.5 + W, _Infection);
                float mask = 1.0 - smoothstep(thr - W, thr, m);

                // Светящаяся кромка на границе заражения
                float edge = 1.0 - saturate(abs(m - (thr - W * 0.5)) / (W * 0.5));
                edge *= edge;

                // Жилки: гребни шума, пульсирующие во времени
                float ridge = 1.0 - abs(Fbm01(dirOS * _NoiseScale * 2.0 + 17.0) * 2.0 - 1.0);
                float veins = pow(saturate(ridge), _VeinSharpness);
                float pulse = 0.65 + 0.35 * sin(_Time.y * _PulseSpeed + noise * 6.2831);

                // Цвета: исходный и заражённый
                half3 baseAlbedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb * _BaseColor.rgb;
                half3 infectedAlbedo = lerp(_InfectedColor.rgb, _InfectedColor2.rgb, noise);
                half3 albedo = lerp(baseAlbedo, infectedAlbedo, mask);

                half3 emission = _VeinColor.rgb * (veins * pulse * _VeinStrength * mask)
                               + _EdgeColor.rgb * (edge * _EdgeStrength);

                // Освещение: главный источник с тенями + окружающий свет
                float3 normalWS = normalize(IN.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 direct = mainLight.color
                             * (saturate(dot(normalWS, mainLight.direction))
                                * mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                half3 ambient = SampleSH(normalWS);

                half3 color = albedo * (direct + ambient) + emission;
                color = MixFog(color, IN.fogFactor);

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // Lighting.hlsl тянет за собой CommonMaterial.hlsl (LerpWhiteTo), без него Shadows.hlsl в URP 14 не компилируется
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half DepthFrag(Varyings IN) : SV_Target
            {
                return IN.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
