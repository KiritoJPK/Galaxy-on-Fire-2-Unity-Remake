// GoF2/ModHologram: a mod ship's hologram projector (ships.json / shipkits.json "materials" with "hologram": true;
// EVERSPACE 2's M_ShipModules_Holograms, rebuilt from its parameters: Rotation speed 2, Hover height 10 cm, Hover speed 0.5;
// its graph isn't in the export). Additive and unlit: flat stacked rings whose pattern (_EmissionMap, the red channel of
// T_ShipModules_FX_001_M_A) turns about the ring's centre (UV 0.5, 0.5) and which bob along their normal.
// UV1 per ring: x its spin (-1..1: direction and share of _RotationSpeed), y its share of _HoverHeight (EVERSPACE 2's
// rings carry these as vertex colours: red the spin, blue the hover; the converter writes them into UV1).
Shader "GoF2/ModHologram"
{
    Properties
    {
        [HDR] _EmissionColor ("Colour", Color) = (0.15, 1.6, 3, 1)
        _EmissionMap ("Pattern", 2D) = "white" {}
        _RotationSpeed ("Rotation speed (rad/s)", Float) = 0.5
        _HoverHeight ("Hover height (model units)", Float) = 0.1
        _HoverSpeed ("Hover speed (cycles/s)", Float) = 0.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionColor;
                float4 _EmissionMap_ST;
                float _RotationSpeed, _HoverHeight, _HoverSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float2 ring : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // Each ring bobs along the disc's normal by its share of the hover height (rings out of phase by their share).
                float bob = sin((_Time.y * _HoverSpeed + i.ring.y) * 6.2831853) * _HoverHeight * i.ring.y;
                float3 pos = i.positionOS.xyz + normalize(i.normalOS) * bob;
                o.positionCS = TransformObjectToHClip(pos);
                // The pattern turns about the ring's centre by its spin.
                float a = _Time.y * _RotationSpeed * i.ring.x;
                float s = sin(a), c = cos(a);
                float2 d = i.uv - 0.5;
                o.uv = float2(d.x * c - d.y * s, d.x * s + d.y * c) + 0.5;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half m = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).r;
                return half4(_EmissionColor.rgb * m, 0);
            }
            ENDHLSL
        }
    }
}
