// PreviewAdditive.shader
// Remake: the Additive Shader Graph's look (Assets/Shaders/Additive.shadergraph: _MainTex * lerp(1, vertex colour,
// _UseVertexColor) * _Color * _Glow, added to what is behind) for the ship previews (ShipPreview), which render into a
// transparent texture the UI lays over the floor glow. The graph blends its alpha like an alpha blend (One
// OneMinusSrcAlpha), so a light's sprite left its whole quad's alpha in the texture and the UI drew its dark corners
// as a black star around the light. Here the alpha grows only by the light's own brightness: the dark parts of the
// sprite stay transparent, over the hull nothing changes (its alpha is already 1).

Shader "GoF2/PreviewAdditive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _UseVertexColor ("Multiply Vertex Color", Float) = 1
        _Glow ("Glow Intensity", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend One One, One One

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _UseVertexColor;
                half _Glow;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = lerp(half4(1, 1, 1, 1), v.color, _UseVertexColor);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color * _Color * _Glow;
                half3 added = max(c.rgb * saturate(c.a), 0);   // the graph's SrcAlpha One on the colour
                return half4(added, max(added.r, max(added.g, added.b)));
            }
            ENDHLSL
        }
    }
}
