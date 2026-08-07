Shader "CardsChaos/Letter Outline"
{
    // Second of the two letter-outline passes (see "Letter Outline Mask"). It draws the letter's
    // mesh swept outward across the camera plane - the same silhouette-preserving expansion the card
    // outline uses - on top of everything (ZTest Always), but only where the mask pass did NOT mark
    // the stencil. What is left is a ring hugging the letter's silhouette, immune to depth and to
    // whatever the letter is lying on.
    Properties
    {
        _OutlineColor ("Outline Colour", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+1"
        }

        Pass
        {
            Name "LetterOutline"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            // Draw only where the mask pass did not stamp 200 - i.e. outside the letter's footprint.
            Stencil
            {
                Ref 200
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                // Sweep across the camera plane rather than straight along the normal, so the ring is
                // an even screen-space width and a flat prop does not push its hull into the floor.
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(positionWS));
                float3 lateralWS = normalWS - viewDirWS * dot(normalWS, viewDirWS);
                float lateralLength = length(lateralWS);
                lateralWS = lateralLength > 1e-5
                    ? lateralWS / lateralLength
                    : float3(0.0, 0.0, 0.0);

                output.positionCS = TransformWorldToHClip(positionWS + lateralWS * _OutlineWidth);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
