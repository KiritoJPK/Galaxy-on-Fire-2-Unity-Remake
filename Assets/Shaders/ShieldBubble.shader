// GoF2/ShieldBubble: the emergency system's bubble (mesh 14374 v_shield, material 27150; AbyssEngine::SimpleRefractionShader,
// combat_equipment.md 2). The sphere itself is invisible: it shows the screen behind it, pushed by the noise texture:
// coord = fragCoord + (noise.xy - 0.5) * rim * 180 px (at 1024x768), rim = smoothstep(0, 1, 1 - dot(eye, noise * 2 - 1))
// with the eye direction in tangent space and the noise read as the normal, so the whole sphere wobbles, most at its rim.
// The noise doesn't scroll (the shader reads u_CloakRate into an unused variable). The scene it shows is clamped to LDR,
// like the original's: the remake's HDR sun core smeared over the bubble bloomed the screen white (#45).
// Remake (Discord report "Emergency system invisible"): drawn by CloakPass (LightMode GoF2Cloak) from its copy of the frame
// taken after the transparents, like the original's draw after Engine::CopyFBO, so the ship's additive lights, engine glow
// and exhaust show through it (from URP's opaque texture the bubble painted them over); and the noise, a data texture the
// import marks as colour, is read back in gamma space: linearised, its mid grey read 0.21, so (noise.xy - 0.5) shifted the
// whole picture down-left instead of a wobble at the rim.
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
            Tags { "LightMode" = "GoF2Cloak" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            TEXTURE2D_X(_GoF2CloakScene); SAMPLER(sampler_GoF2CloakScene);
            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseMap_ST;
                float _Anim, _Strength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 eyeTS : TEXCOORD1; float3 dirOS : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = i.uv;
                o.dirOS = i.positionOS.xyz;
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
                // Remake: the noise is sampled around the sphere by three blended planar projections of its direction, not by
                // the mesh's UVs: the noise image doesn't tile, so where the UVs wrap (the side the chase camera looks at) the
                // refraction broke into a cut (the user's report, 2026-10-21). 0.25 a unit of direction is about the UVs' own
                // density (a turn of the sphere is one texture width).
                float3 d = normalize(i.dirOS);
                float3 w = pow(abs(d), 4.0);
                w /= max(w.x + w.y + w.z, 1e-5);
                half4 noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, d.yz * 0.25 + 0.5) * w.x
                            + SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, d.xz * 0.25 + 0.5) * w.y
                            + SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, d.xy * 0.25 + 0.5) * w.z;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                noise.rgb = LinearToSRGB(noise.rgb);   // a data texture imported as colour
                #endif
                float rim = smoothstep(0.0, 1.0, 1.0 - dot(normalize(i.eyeTS), noise.rgb * 2.0 - 1.0));
                float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
                float2 offset = (noise.xy - 0.5) * rim * 180.0 * _Strength / float2(1024.0, 768.0);
                half3 scene = SAMPLE_TEXTURE2D_X(_GoF2CloakScene, sampler_GoF2CloakScene, UnityStereoTransformScreenSpaceTex(saturate(suv + offset))).rgb;
                return half4(min(scene, 1.0), 1);
            }
            ENDHLSL
        }
    }
}
