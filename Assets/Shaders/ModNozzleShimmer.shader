// GoF2/ModNozzleShimmer: a mod ship's nozzle shimmer (ships.json / shipkits.json "materials" with "shimmer": true;
// EVERSPACE 2's M_Vehicles_Floaters_Emissive scrolls T_Noise_001_M over its glow at ScrollSpeed 0.3; its graph isn't in the
// export). An additive layer drawn over the nozzle's own glowing faces (the converter gives them twice): the glow pattern
// (_EmissionMap) times a noise scrolling along the surface, so the glow flickers in moving streaks instead of burning flat.
// _EmissionColor is driven like the nozzle glow (EngineMaterialGlow: dark parked, dim at idle, full with the thrust).
Shader "GoF2/ModNozzleShimmer"
{
    Properties
    {
        [HDR] _EmissionColor ("Colour", Color) = (1, 0.3, 0.05, 1)
        _EmissionMap ("Glow pattern", 2D) = "white" {}
        _NoiseMap ("Noise", 2D) = "gray" {}
        _NoiseTiling ("Noise tiling", Float) = 6
        _ScrollSpeed ("Scroll speed (/s)", Float) = 0.3
        _Contrast ("Noise contrast (exponent)", Float) = 2
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionColor;
                float4 _EmissionMap_ST, _NoiseMap_ST;
                float _NoiseTiling, _ScrollSpeed, _Contrast;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half mask = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).r;
                float t = _Time.y * _ScrollSpeed;
                // Two layers drifting apart (one along v, one slower across), multiplied: moving streaks.
                half n1 = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, i.uv * _NoiseTiling + float2(0, -t)).r;
                half n2 = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, i.uv * _NoiseTiling * 0.57 + float2(t * 0.41, 0.37)).r;
                half n = saturate(pow(saturate(n1 * n2 * 2), _Contrast) * 1.5);
                return half4(_EmissionColor.rgb * mask * n, 0);
            }
            ENDHLSL
        }
    }
}
