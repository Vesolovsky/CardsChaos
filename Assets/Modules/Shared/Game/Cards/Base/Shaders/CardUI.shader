// The card as it is drawn flat in the album, and the 2D counterpart of CardLit.
//
// The source art is a full rectangle with a black band outside the printed card face, and the
// 3D side never shows it: the mesh silhouette is a superellipse cut exactly on the face
// boundary, and its UVs are pulled in a few pixels so no sample lands on the band. A UI Image
// has neither, so it draws the whole rectangle, band and all - which is where the black corners
// come from.
//
// So this does in the fragment what the mesh does in geometry: the same superellipse, from the
// same measured constants, as an alpha mask. Nothing about the art changes, one material serves
// every card, and because they all share it they still batch.
Shader "CardsChaos/Card UI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // Defaults mirror CardMeshSettings; CardSetBuilder rewrites them onto the material so
        // the flat card and the solid one cannot drift apart.
        _CornerRadius ("Corner Radius (fraction of width)", Range(0, 0.5)) = 0.0473633
        _Squareness ("Squareness (2 = circular corner)", Range(1, 4)) = 1.73
        _Aspect ("Aspect (width / height)", Float) = 0.6666667
        _UvInset ("UV Inset (u, v, unused, unused)", Vector) = (0.005859375, 0.00390625, 0, 0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            // Both are set by uGUI itself. UNITY_UI_CLIP_RECT is what makes the card obey a
            // RectMask2D - without it the pages either side of the open one would draw straight
            // over the album.
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _CornerRadius;
            float _Squareness;
            float _Aspect;
            float4 _UvInset;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;

                return OUT;
            }

            // Signed distance to a superellipse-cornered rectangle, in units of the card's
            // height. Negative inside. Squareness 2 would give the usual circular corner; the
            // card measures 1.73, which is visibly flatter through the turn.
            float CardDistance(float2 uv)
            {
                float2 size = float2(_Aspect, 1.0);
                float2 p = (uv - 0.5) * size;

                // The radius arrives as a fraction of the width, because that is how it was
                // measured off the 1024 px artwork.
                float radius = _CornerRadius * _Aspect;

                float2 q = abs(p) - (size * 0.5 - radius);
                float2 outside = max(q, 0.0);

                float n = 2.0 / max(_Squareness, 0.001);
                float corner = pow(pow(outside.x, n) + pow(outside.y, n), 1.0 / n);

                return corner + min(max(q.x, q.y), 0.0) - radius;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Sampled a few pixels in from the edge, exactly as the mesh UVs are, so the
                // filtered edge never picks up the black band just outside the face.
                float2 uv = (IN.texcoord - 0.5) * (1.0 - 2.0 * _UvInset.xy) + 0.5;

                half4 color = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;

                float distance = CardDistance(IN.texcoord);

                // One pixel of the distance field, so the silhouette stays smooth at any size
                // the card is drawn - the pile draws it small, the drag layer large.
                float edge = max(fwidth(distance), 0.00001);
                color.a *= 1.0 - smoothstep(-edge, edge, distance);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
