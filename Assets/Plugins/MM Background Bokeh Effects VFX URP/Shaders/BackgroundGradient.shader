Shader "MM/BackgroundGradient" {
    Properties {
        _TopColor ("Top Color", Color) = (0.1, 0.3, 0.8, 1)
        _BottomColor ("Bottom Color", Color) = (0.8, 0.6, 0.2, 1)
        _Midpoint ("Midpoint", Range(0, 1)) = 0.5
        _Smoothness ("Smoothness", Range(0.01, 1)) = 0.1
    }

    SubShader {
        Tags {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Cull Off
        ZWrite Off

        // Standard URP Forward path
        Pass {
            Name "GradientPass"
            Tags {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _TopColor;
            float4 _BottomColor;
            float _Midpoint;
            float _Smoothness;

            struct MeshData {
                float4 vertex : POSITION;
                float3 positionOS : TEXCOORD0;
            };

            struct VertexToFragment {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            VertexToFragment vert (MeshData v) {
                VertexToFragment o;
                o.positionCS = TransformObjectToHClip(v.vertex.xyz);
                o.positionOS = v.positionOS;
                return o;
            }

            half4 frag (VertexToFragment i) : SV_Target {
                float y = saturate(i.positionOS.y);
                float t = saturate((y - _Midpoint) / _Smoothness);
                return lerp(_BottomColor, _TopColor, t);
            }
            ENDHLSL
        }

        // URP 2D Renderer path
        Pass {
            Name "GradientPass2D"
            Tags {
                "LightMode" = "Universal2D"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _TopColor;
            float4 _BottomColor;
            float _Midpoint;
            float _Smoothness;

            struct MeshData {
                float4 vertex : POSITION;
                float3 positionOS : TEXCOORD0;
            };

            struct VertexToFragment {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            VertexToFragment vert (MeshData v) {
                VertexToFragment o;
                o.positionCS = TransformObjectToHClip(v.vertex.xyz);
                o.positionOS = v.positionOS;
                return o;
            }

            half4 frag (VertexToFragment i) : SV_Target {
                float y = saturate(i.positionOS.y);
                float t = saturate((y - _Midpoint) / _Smoothness);
                return lerp(_BottomColor, _TopColor, t);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}