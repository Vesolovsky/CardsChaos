Shader "MM/EmmisiveBlinkingParticleUnlit" {
    Properties {
        [NoScaleOffset] _MainTex ("Texture", 2D) = "white" {}
        _EmissionStrength ("Emission Strength", Range(1, 10)) = 1.0
        _BlinkSpeed ("Blink Speed", Range(0, 20)) = 2.0
    }

    SubShader {
        Tags {
            "Queue"="Transparent"
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

            // SRP texture/sampler style
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _EmissionStrength;
            float _BlinkSpeed;
            float4 _RendererColor;

            VertexToFragment vert (MeshData v) {
                VertexToFragment o;
                o.positionCS = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag (VertexToFragment i) : SV_Target {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                // Per-particle phase from vertex color as a seed
                float seed = dot(i.color.rgba, float4(12.9898, 78.233, 45.164, 94.673));
                float phaseOffset = frac(sin(seed) * 43758.5453);
                float freq = 1.0 + frac(sin(seed * 1.3) * 43758.5453) * _BlinkSpeed;

                // clamp speed to a sane range but preserve zero = steady
                float clampedSpeed = saturate(_BlinkSpeed / 20.0) * 20.0;

                float blink = (clampedSpeed > 0.001)
                    ? (0.5 + 0.5 * sin(_Time.y * clampedSpeed + phaseOffset * 6.2831))
                    : 1.0;

                half3 emissive = texColor.rgb * i.color.rgb * _RendererColor.rgb * _EmissionStrength;
                half alpha = texColor.a * i.color.a * _RendererColor.a * blink;

                return half4(emissive, alpha);
            }
            ENDHLSL
        }

        // URP 2D Renderer path
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

            // SRP texture/sampler style
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _EmissionStrength;
            float _BlinkSpeed;
            float4 _RendererColor;

            VertexToFragment vert (MeshData v) {
                VertexToFragment o;
                o.positionCS = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag (VertexToFragment i) : SV_Target {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                // Same blinking logic as Forward path
                float seed = dot(i.color.rgba, float4(12.9898, 78.233, 45.164, 94.673));
                float phaseOffset = frac(sin(seed) * 43758.5453);
                float freq = 1.0 + frac(sin(seed * 1.3) * 43758.5453) * _BlinkSpeed;

                float clampedSpeed = saturate(_BlinkSpeed / 20.0) * 20.0;

                float blink = (clampedSpeed > 0.001)
                    ? (0.5 + 0.5 * sin(_Time.y * clampedSpeed + phaseOffset * 6.2831))
                    : 1.0;

                half3 emissive = texColor.rgb * i.color.rgb * _RendererColor.rgb * _EmissionStrength;
                half alpha = texColor.a * i.color.a * _RendererColor.a * blink;

                return half4(emissive, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}