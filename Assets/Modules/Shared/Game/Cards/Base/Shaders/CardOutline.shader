Shader "CardsChaos/Card Outline"
{
    Properties
    {
        _OutlineColor ("Outline Colour", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Range(0,0.02)) = 0.002
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+1"
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
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

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                // Expand across the camera plane. Expanding a flat card directly along its
                // normal pushes the back-facing half of the hull into the floor instead of
                // producing a visible ring around the silhouette.
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(positionWS));
                float3 lateralWS = normalWS - viewDirWS * dot(normalWS, viewDirWS);
                float lateralLength = length(lateralWS);
                lateralWS = lateralLength > 1e-5
                    ? lateralWS / lateralLength
                    : float3(0.0, 0.0, 0.0);

                output.positionCS = TransformWorldToHClip(
                    positionWS + lateralWS * _OutlineWidth);

                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
