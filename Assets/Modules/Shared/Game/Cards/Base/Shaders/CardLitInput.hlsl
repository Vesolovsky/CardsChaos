#ifndef CARDSCHAOS_CARD_LIT_INPUT_INCLUDED
#define CARDSCHAOS_CARD_LIT_INPUT_INCLUDED

// Brings in _BaseMap / _BumpMap declarations and the helpers that the shared URP
// ShadowCaster / DepthOnly / DepthNormals passes rely on.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// Every material property lives in this one block so the SRP Batcher can batch
// all card materials together - they only differ by texture references.
CBUFFER_START(UnityPerMaterial)
    float4 _FrontTex_ST;
    float4 _BackTex_ST;
    float4 _BaseMap_ST;
    half4  _BaseColor;
    half4  _EdgeTint;
    half   _Smoothness;
    half   _Metallic;
    half   _EdgeDarken;
    half   _Cutoff;
CBUFFER_END

TEXTURE2D(_FrontTex);
SAMPLER(sampler_FrontTex);
TEXTURE2D(_BackTex);
SAMPLER(sampler_BackTex);

#endif
