Shader "MM/EmmisiveParticleUnlit" {
    Properties {
        [NoScaleOffset] _MainTex ("Texture", 2D) = "white" {}
        _EmissionStrength ("Emission Strength", Range(1, 10)) = 1.0
    }

    SubShader {
        Tags {
            "Queue"="Transparent+50"
            "RenderType"="Transparent"
        }

        // Standard URP Forward (3D) path
        Pass {
            Name "Unlit"
            Tags {
                "LightMode"="UniversalForward"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // --- Textures & uniforms (SRP style) ---
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _EmissionStrength;
            float4 _RendererColor;

            struct MeshData {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct VertexToFragment {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            VertexToFragment vert (MeshData v) {
                VertexToFragment o;
                o.positionCS = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag (VertexToFragment i) : SV_Target {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                // Multiply by vertex color and SpriteRenderer's _RendererColor if present
                half3 emissive = texColor.rgb * i.color.rgb * _RendererColor.rgb * _EmissionStrength;
                half alpha = texColor.a * i.color.a * _RendererColor.a;

                return half4(emissive, alpha);
            }
            ENDHLSL
        }

        // URP 2D Renderer path (so it renders with the 2D Renderer)
        Pass {
            Name "Unlit2D"
            Tags {
                "LightMode"="Universal2D"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // --- Textures & uniforms (SRP style) ---
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _EmissionStrength;
            float4 _RendererColor;

            struct MeshData {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct VertexToFragment {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            VertexToFragment vert (MeshData v) {
                VertexToFragment o;
                o.positionCS = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag (VertexToFragment i) : SV_Target {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                // Same math as Forward path
                half3 emissive = texColor.rgb * i.color.rgb * _RendererColor.rgb * _EmissionStrength;
                half alpha = texColor.a * i.color.a * _RendererColor.a;

                return half4(emissive, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}