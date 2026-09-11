Shader "Custom/StylizedCharacter_New"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)

        _ShadowColor ("Shadow Color", Color) = (0.35,0.25,0.22,1)
        _DarkColor ("Dark Color", Color) = (0.18,0.12,0.12,1)

        _ShadowThreshold ("Shadow Threshold", Range(0,1)) = 0.45
        _DarkThreshold ("Dark Threshold", Range(0,1)) = 0.2
        _ShadowSoftness ("Shadow Softness", Range(0.001,0.5)) = 0.08

        _AmbientStrength ("Ambient Strength", Range(0,2)) = 0.7

        _RimColor ("Rim Color", Color) = (1,0.65,0.35,1)
        _RimPower ("Rim Power", Range(0.1,10)) = 4
        _RimStrength ("Rim Strength", Range(0,2)) = 0.25

        _Saturation ("Saturation", Range(0,2)) = 1.1

        [Toggle] _UseBaseMapEmission ("Use Base Map as Emission", Float) = 0
        _EmissionStrength ("Emission Strength", Range(0,5)) = 0.0

        // OUTLINE
        _OutlineColor ("Outline Color", Color) = (0.05,0.025,0.02,1)
        _OutlineWidth ("Outline Width", Range(0,0.1)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }

        // ============================================================
        // MAIN CHARACTER PASS
        // ============================================================

        Pass
        {
            Name "StylizedForward"

            Tags
            {
                "LightMode"="UniversalForward"
            }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fog

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            // Additional (point/spot) light support
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
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
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)

                float4 _BaseMap_ST;
                float4 _BaseColor;

                float4 _ShadowColor;
                float4 _DarkColor;

                float _ShadowThreshold;
                float _DarkThreshold;
                float _ShadowSoftness;

                float _AmbientStrength;

                float4 _RimColor;
                float _RimPower;
                float _RimStrength;

                float _Saturation;

                float _UseBaseMapEmission;
                float _EmissionStrength;

                float4 _OutlineColor;
                float _OutlineWidth;

            CBUFFER_END


            // ========================================================
            // VERTEX
            // ========================================================

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(IN.positionOS.xyz);

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = positionInputs.positionCS;
                OUT.positionWS = positionInputs.positionWS;
                OUT.normalWS = normalize(normalInputs.normalWS);

                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);

                return OUT;
            }


            // ========================================================
            // SATURATION
            // ========================================================

            float3 AdjustSaturation(float3 color, float saturation)
            {
                float luminance =
                    dot(color, float3(0.299, 0.587, 0.114));

                return lerp(
                    luminance.xxx,
                    color,
                    saturation
                );
            }


            // ========================================================
            // FRAGMENT
            // ========================================================

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS =
                    normalize(IN.normalWS);


                // ----------------------------------------------------
                // BASE TEXTURE
                // ----------------------------------------------------

                float4 tex =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        IN.uv
                    );

                float3 baseColor =
                    tex.rgb * _BaseColor.rgb;


                // ----------------------------------------------------
                // MAIN LIGHT
                // ----------------------------------------------------

                Light mainLight =
                    GetMainLight(
                        TransformWorldToShadowCoord(IN.positionWS)
                    );

                float3 lightDir =
                    normalize(mainLight.direction);

                // mainLight.color already bakes in light intensity
                float3 lightColor = mainLight.color;

                float NdotL =
                    dot(normalWS, lightDir);

                NdotL =
                    NdotL * 0.5 + 0.5;

                float shadow =
                    mainLight.shadowAttenuation;

                float lightValue =
                    NdotL * shadow;


                // ----------------------------------------------------
                // TOON BANDS
                // ----------------------------------------------------

                float shadowBand =
                    smoothstep(
                        _ShadowThreshold - _ShadowSoftness,
                        _ShadowThreshold + _ShadowSoftness,
                        lightValue
                    );

                float darkBand =
                    smoothstep(
                        _DarkThreshold - _ShadowSoftness,
                        _DarkThreshold + _ShadowSoftness,
                        lightValue
                    );

                // Lit color is now actually driven by the main light's color/intensity
                float3 litColor =
                    baseColor * lightColor;

                // Dark → Shadow → Lit (tinted by main light)

                float3 color =
                    lerp(
                        _DarkColor.rgb,
                        _ShadowColor.rgb,
                        darkBand
                    );

                color =
                    lerp(
                        color,
                        litColor,
                        shadowBand
                    );


                // ----------------------------------------------------
                // AMBIENT
                // ----------------------------------------------------

                float3 ambient =
                    SampleSH(normalWS);

                color +=
                    ambient *
                    baseColor *
                    _AmbientStrength;


                // ----------------------------------------------------
                // ADDITIONAL LIGHTS (point / spot)
                // ----------------------------------------------------

                #ifdef _ADDITIONAL_LIGHTS
                    uint additionalLightsCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0u; lightIndex < additionalLightsCount; lightIndex++)
                    {
                        Light addLight = GetAdditionalLight(lightIndex, IN.positionWS);

                        float addNdotL = saturate(dot(normalWS, addLight.direction));
                        float addAtten = addLight.distanceAttenuation * addLight.shadowAttenuation;

                        color += baseColor * addLight.color * addNdotL * addAtten;
                    }
                #endif


                // ----------------------------------------------------
                // RIM LIGHT
                // ----------------------------------------------------

                float3 viewDir =
                    normalize(
                        GetWorldSpaceViewDir(IN.positionWS)
                    );

                float rim =
                    1.0 -
                    saturate(
                        dot(normalWS, viewDir)
                    );

                rim =
                    pow(rim, _RimPower);

                color +=
                    _RimColor.rgb *
                    rim *
                    _RimStrength;


                // ----------------------------------------------------
                // EMISSION
                // ----------------------------------------------------

                if (_UseBaseMapEmission > 0.5)
                {
                    color += baseColor * _EmissionStrength;
                }


                // ----------------------------------------------------
                // COLOR CONTROL
                // ----------------------------------------------------

                color =
                    AdjustSaturation(
                        color,
                        _Saturation
                    );


                // ----------------------------------------------------
                // FOG
                // ----------------------------------------------------

                float fogFactor =
                    ComputeFogFactor(IN.positionWS.z);

                color =
                    MixFog(
                        color,
                        fogFactor
                    );


                return float4(
                    color,
                    tex.a
                );
            }

            ENDHLSL
        }


        // ============================================================
        // OUTLINE PASS
        // ============================================================

        Pass
        {
            Name "Outline"

            Tags
            {
                "LightMode"="SRPDefaultUnlit"
            }

            Cull Front
            ZWrite On

            HLSLPROGRAM

            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct OutlineAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct OutlineVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            // NOTE: this CBUFFER layout must match the main pass exactly
            // (same fields, same order) for SRP Batcher compatibility.
            // The old version referenced a non-existent _EmissionColor
            // property here, which mismatched the main pass — fixed below.
            CBUFFER_START(UnityPerMaterial)

                float4 _BaseMap_ST;
                float4 _BaseColor;

                float4 _ShadowColor;
                float4 _DarkColor;

                float _ShadowThreshold;
                float _DarkThreshold;
                float _ShadowSoftness;

                float _AmbientStrength;

                float4 _RimColor;
                float _RimPower;
                float _RimStrength;

                float _Saturation;

                float _UseBaseMapEmission;
                float _EmissionStrength;

                float4 _OutlineColor;
                float _OutlineWidth;

            CBUFFER_END


            // ========================================================
            // OUTLINE VERTEX
            // ========================================================

            OutlineVaryings OutlineVert(
                OutlineAttributes IN
            )
            {
                OutlineVaryings OUT;

                float3 positionWS =
                    TransformObjectToWorld(IN.positionOS.xyz);

                float3 normalWS =
                    TransformObjectToWorldNormal(IN.normalOS);

                normalWS =
                    normalize(normalWS);

                // Expand mesh along its normal
                positionWS +=
                    normalWS *
                    _OutlineWidth;

                OUT.positionCS =
                    TransformWorldToHClip(positionWS);

                return OUT;
            }


            // ========================================================
            // OUTLINE FRAGMENT
            // ========================================================

            half4 OutlineFrag(
                OutlineVaryings IN
            ) : SV_Target
            {
                return _OutlineColor;
            }

            ENDHLSL
        }
    }
}