// Single-material card shader: front face, back face and a bevelled rim in one draw call.
//
// The mesh encodes which surface a vertex belongs to in vertex colour R:
//   1 = front face, 0 = back face, 0..1 across the rim (0.5 at the silhouette).
// uv0 always holds front-face coordinates, uv1 holds back-face coordinates
// (mirrored in X), so the rim can blend between both textures without a seam.
Shader "CardsChaos/Card Lit"
{
    Properties
    {
        [MainTexture] _FrontTex ("Front Face", 2D) = "white" {}
        _BackTex ("Back Face", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.55
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _EdgeTint ("Rim Tint", Color) = (1,1,1,1)
        _EdgeDarken ("Rim Darken", Range(0,1)) = 0.18
        _MipBias ("Close View Mip Bias", Range(-1,0)) = 0
        _InspectSharpen ("Inspect Sharpen", Range(0,0.35)) = 0

    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex CardVertex
            #pragma fragment CardFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "CardLitInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uvFront    : TEXCOORD0;
                float2 uvBack     : TEXCOORD1;
                float2 lightmapUV : TEXCOORD2;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uvFront     : TEXCOORD0;
                float2 uvBack      : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                half3  normalWS    : TEXCOORD3;
                half4  color       : TEXCOORD4;
                half4  fogFactorAndVertexLight : TEXCOORD5;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 6);
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord : TEXCOORD7;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings CardVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uvFront = TRANSFORM_TEX(input.uvFront, _FrontTex);
                output.uvBack = TRANSFORM_TEX(input.uvBack, _BackTex);
                output.color = input.color;

                half3 vertexLight = VertexLighting(positionInputs.positionWS, normalInputs.normalWS);
                half fogFactor = 0.0h;
                #if !defined(_FOG_FRAGMENT)
                    fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                #endif
                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(normalInputs.normalWS.xyz, output.vertexSH);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    output.shadowCoord = GetShadowCoord(positionInputs);
                #endif

                return output;
            }

            half3 SafeSharpen5(
                half3 center, half3 left, half3 right, half3 down, half3 up, half strength)
            {
                // The correction is clipped both to a small absolute range and to the colours
                // already present in the five samples. It therefore cannot create the bright or
                // dark overshoot that gives a conventional unsharp mask its visible halo.
                half3 localMin = min(center, min(min(left, right), min(down, up)));
                half3 localMax = max(center, max(max(left, right), max(down, up)));
                half3 average = (left + right + down + up) * 0.25h;

                const half3 Luminance = half3(0.2126h, 0.7152h, 0.0722h);
                half centerLuma = dot(center, Luminance);
                half leftLuma = dot(left, Luminance);
                half rightLuma = dot(right, Luminance);
                half downLuma = dot(down, Luminance);
                half upLuma = dot(up, Luminance);
                half lumaMin = min(centerLuma,
                    min(min(leftLuma, rightLuma), min(downLuma, upLuma)));
                half lumaMax = max(centerLuma,
                    max(max(leftLuma, rightLuma), max(downLuma, upLuma)));

                // Back away on very strong authored edges, where extra contrast is least useful
                // and most likely to turn compression blocks into a visible contour.
                half edgeGuard = 1.0h - smoothstep(0.25h, 0.65h, lumaMax - lumaMin);
                half3 delta = clamp(
                    (center - average) * (strength * edgeGuard), -0.03h, 0.03h);

                return clamp(center + delta, localMin, localMax);
            }

            half SharpenAnisotropyGuard(
                float2 pixelDx, float2 pixelDy, float2 textureTexelSize)
            {
                float2 safeTexelSize = max(textureTexelSize, float2(1e-8, 1e-8));
                float footprintX = length(pixelDx / safeTexelSize);
                float footprintY = length(pixelDy / safeTexelSize);
                float anisotropy = max(footprintX, footprintY)
                                   / max(min(footprintX, footprintY), 1e-4);

                // Fade out before a flip or steep tilt can turn sharpening into temporal shimmer.
                return (half)(1.0 - smoothstep(2.0, 4.0, anisotropy));
            }

            half3 SampleCardFace(
                float2 uv,
                float2 pixelDx,
                float2 pixelDy,
                float2 sampleDx,
                float2 sampleDy,
                float2 textureTexelSize,
                TEXTURE2D_PARAM(faceTexture, faceSampler))
            {
                half3 center = SAMPLE_TEXTURE2D_GRAD(
                    faceTexture, faceSampler, uv, sampleDx, sampleDy).rgb;

                // Uniform for the entire draw. Ground and ordinary held cards return after their
                // original single sample; only the one inspected card takes the four neighbours.
                UNITY_BRANCH
                if (_InspectSharpen <= 0.0001h)
                    return center;

                half strength = _InspectSharpen
                                * SharpenAnisotropyGuard(pixelDx, pixelDy, textureTexelSize);

                UNITY_BRANCH
                if (strength <= 0.0001h)
                    return center;

                half3 left = SAMPLE_TEXTURE2D_GRAD(
                    faceTexture, faceSampler, uv - pixelDx, sampleDx, sampleDy).rgb;
                half3 right = SAMPLE_TEXTURE2D_GRAD(
                    faceTexture, faceSampler, uv + pixelDx, sampleDx, sampleDy).rgb;
                half3 down = SAMPLE_TEXTURE2D_GRAD(
                    faceTexture, faceSampler, uv - pixelDy, sampleDx, sampleDy).rgb;
                half3 up = SAMPLE_TEXTURE2D_GRAD(
                    faceTexture, faceSampler, uv + pixelDy, sampleDx, sampleDy).rgb;

                return SafeSharpen5(center, left, right, down, up, strength);
            }

            half4 CardFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half faceMix = saturate(input.color.r);
                // Keep the raw one-screen-pixel derivatives for the sharpen taps. Separate copies
                // are scaled for texture LOD, so the -0.415 bias does not shrink the filter radius.
                float2 frontPixelDx = ddx(input.uvFront);
                float2 frontPixelDy = ddy(input.uvFront);
                float2 backPixelDx = ddx(input.uvBack);
                float2 backPixelDy = ddy(input.uvBack);
                float2 frontSampleDx = frontPixelDx;
                float2 frontSampleDy = frontPixelDy;
                float2 backSampleDx = backPixelDx;
                float2 backSampleDy = backPixelDy;

                // The default scale is one, so resting cards keep the same LOD selection as before.
                // Held/inspected renderers set a small negative bias through their existing property
                // block, selecting a sharper resident mip without another texture sample.
                // The branch is uniform for the whole draw: the 1,000+ resting cards skip the
                // exponent and gradient multiplies; only the handful in a close view pay for them.
                UNITY_BRANCH
                if (_MipBias < -0.0001h)
                {
                    float mipGradientScale = exp2((float)_MipBias);
                    frontSampleDx *= mipGradientScale;
                    frontSampleDy *= mipGradientScale;
                    backSampleDx *= mipGradientScale;
                    backSampleDy *= mipGradientScale;
                }
                half3 albedo;

                // Every flat-face triangle carries a constant 0 or 1, so its branch is coherent
                // across the quad and samples only the texture that can contribute. Rim pixels
                // retain the exact two-texture blend. Gradients are evaluated before branching so
                // mip selection stays defined at the face/rim boundary.
                UNITY_BRANCH
                if (faceMix >= 1.0h)
                {
                    albedo = SampleCardFace(
                        input.uvFront,
                        frontPixelDx,
                        frontPixelDy,
                        frontSampleDx,
                        frontSampleDy,
                        _FrontTex_TexelSize.xy,
                        TEXTURE2D_ARGS(_FrontTex, sampler_FrontTex));
                }
                else if (faceMix <= 0.0h)
                {
                    albedo = SampleCardFace(
                        input.uvBack,
                        backPixelDx,
                        backPixelDy,
                        backSampleDx,
                        backSampleDy,
                        _BackTex_TexelSize.xy,
                        TEXTURE2D_ARGS(_BackTex, sampler_BackTex));
                }
                else
                {
                    // The narrow rim keeps its original two samples. Sharpening across two faces
                    // would cost ten taps and could turn their blend into a visible seam.
                    half3 front = SAMPLE_TEXTURE2D_GRAD(
                        _FrontTex, sampler_FrontTex, input.uvFront,
                        frontSampleDx, frontSampleDy).rgb;
                    half3 back = SAMPLE_TEXTURE2D_GRAD(
                        _BackTex, sampler_BackTex, input.uvBack,
                        backSampleDx, backSampleDy).rgb;
                    albedo = lerp(back, front, faceMix);
                }

                albedo *= _BaseColor.rgb;

                // 0 on the flat faces, 1 at the outermost point of the rim.
                half rimMask = 1.0h - abs(faceMix * 2.0h - 1.0h);
                albedo *= lerp(half3(1.0h, 1.0h, 1.0h), _EdgeTint.rgb * (1.0h - _EdgeDarken), rimMask);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion = 1.0h;
                surfaceData.alpha = 1.0h;
                surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    inputData.shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif

                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
                inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
                inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0h;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CardLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CardLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CardLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
