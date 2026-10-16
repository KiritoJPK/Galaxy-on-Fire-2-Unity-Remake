// GoF2/ShieldBubble: the emergency system's bubble (mesh 14374 v_shield, material 27150; AbyssEngine::SimpleRefractionShader,
// combat_equipment.md 2). The sphere itself is invisible: it shows the screen behind it, pushed by the noise texture:
// coord = fragCoord + (noise.xy - 0.5) * rim * 180 px (at 1024x768), rim = smoothstep(0, 1, 1 - dot(eye, noise * 2 - 1))
// with the eye direction in tangent space and the noise read as the normal, so the whole sphere wobbles, most at its rim.
// The noise doesn't scroll (the shader reads u_CloakRate into an unused variable). The scene it shows is clamped to LDR,
// like the original's: the remake's HDR sun core smeared over the bubble bloomed the screen white (#45).
// Reads _CameraOpaqueTexture (the camera's opaque texture must be on).
Shader "GoF2/ShieldBubble"
{
    Properties
    {
        _NoiseMap ("Noise", 2D) = "gray" {}
        _Anim ("Anim", Float) = 0
        _Strength ("Strength", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Back

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseMap_ST;
                float _Anim, _Strength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 eyeTS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = i.uv;
                // v_eye_dir = TBN * (eye - position), per vertex like the original
                float3 n = TransformObjectToWorldNormal(i.normalOS);
                float3 t = TransformObjectToWorldDir(i.tangentOS.xyz);
                float3 b = cross(n, t) * i.tangentOS.w * GetOddNegativeScale();
                float3 v = normalize(GetWorldSpaceViewDir(ws));
                o.eyeTS = float3(dot(v, t), dot(v, b), dot(v, n));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, i.uv);
                float rim = smoothstep(0.0, 1.0, 1.0 - dot(normalize(i.eyeTS), noise.rgb * 2.0 - 1.0));
                float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
                float2 offset = (noise.xy - 0.5) * rim * 180.0 * _Strength / float2(1024.0, 768.0);
                return half4(min(SampleSceneColor(saturate(suv + offset)), 1.0), 1);
            }
            ENDHLSL
        }
    }
}
