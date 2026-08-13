// One translucent preview draw for the selected card and slot. It shares the selected card's
// textures through a MaterialPropertyBlock; no sprite, renderer or texture copy is created.
Shader "CardsChaos/Card Placement Ghost"
{
    Properties
    {
        [MainTexture] _FrontTex ("Front Face", 2D) = "white" {}
        _BackTex ("Back Face", 2D) = "white" {}
        [MainColor] _GhostTint ("Ghost Tint", Color) = (0.2,0.9,1,0.5)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+10"
        }

        Pass
        {
            Name "Ghost"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GhostVertex
            #pragma fragment GhostFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FrontTex);
            SAMPLER(sampler_FrontTex);
            TEXTURE2D(_BackTex);
            SAMPLER(sampler_BackTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _FrontTex_ST;
                float4 _BackTex_ST;
                half4 _GhostTint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uvFront : TEXCOORD0;
                float2 uvBack : TEXCOORD1;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uvFront : TEXCOORD0;
                float2 uvBack : TEXCOORD1;
                half faceMix : TEXCOORD2;
            };

            Varyings GhostVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uvFront = TRANSFORM_TEX(input.uvFront, _FrontTex);
                output.uvBack = TRANSFORM_TEX(input.uvBack, _BackTex);
                output.faceMix = saturate(input.color.r);
                return output;
            }

            half4 GhostFragment(Varyings input) : SV_Target
            {
                half3 front = SAMPLE_TEXTURE2D(_FrontTex, sampler_FrontTex, input.uvFront).rgb;
                half3 back = SAMPLE_TEXTURE2D(_BackTex, sampler_BackTex, input.uvBack).rgb;
                half3 artwork = lerp(back, front, input.faceMix);

                // Keep enough of the picture to identify the selected card while the cyan tint and
                // translucency unmistakably read as a proposed position rather than a placed card.
                half3 colour = lerp(artwork, _GhostTint.rgb, 0.55h);
                return half4(colour, _GhostTint.a);
            }
            ENDHLSL
        }
    }
}
