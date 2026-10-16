// GoF2/SpaceDust: the particle sets the original attaches to the camera (Level::initParticleSystems 0xcc990,
// ParticleSystemSprite::updateAreaExitParticle): SET_STARS (500 small sprites) and SET_FOG (15 huge tinted sprites).
// They don't move; the ship flies through them, which gives the sense of speed. Instead of respawning particles on
// the CPU, every sprite's centre wraps around the camera inside a cube of 2R (same effect), and the alpha follows
// the original's distance fades: 0 below _Inner, fade in to _FadeInEnd, full, fade out from _FadeOutStart to _Radius.
// One draw call; the mesh holds the centres (POSITION) and the corner offsets in metres (TEXCOORD1).
Shader "GoF2/SpaceDust"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Texture", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Radius ("Radius R (m)", Float) = 500
        _FadeOutStart ("Fade out start (m)", Float) = 250
        _FadeInEnd ("Fade in end (m)", Float) = 100
        _Inner ("Inner (m)", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Radius, _FadeOutStart, _FadeInEnd, _Inner;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 corner : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half alpha : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 cam = _WorldSpaceCameraPos;
                float size = 2 * _Radius;
                float3 rel = i.positionOS.xyz - cam;
                rel = (frac(rel / size + 0.5) - 0.5) * size;          // wrap into the cube around the camera
                float d2 = dot(rel, rel);
                float r2 = _Radius * _Radius, o2 = _FadeOutStart * _FadeOutStart;
                float f2 = _FadeInEnd * _FadeInEnd, n2 = _Inner * _Inner;
                half a = d2 >= r2 ? 0 : d2 >= o2 ? (r2 - d2) / (r2 - o2) : d2 >= f2 ? 1 : d2 >= n2 ? (d2 - n2) / max(f2 - n2, 1e-3) : 0;
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float3 world = cam + rel + right * i.corner.x + up * i.corner.y;
                o.positionCS = TransformWorldToHClip(world);
                o.uv = i.uv;
                o.alpha = a;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                return half4(t.rgb, t.a * i.alpha);
            }
            ENDHLSL
        }
    }
}
