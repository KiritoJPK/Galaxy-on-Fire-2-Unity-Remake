// GoF2/SpaceSky: the flight-level skybox. Level::renderBG draws the stars sphere (opaque) and adds the nebula sphere
// on top, both rotated by the per-station R_sky. Here both layers are baked cubemaps (GoF2 > Bake Space Skies),
// added together and sampled through _SkyRotation (world direction -> baked cube direction), which the built-in
// Skybox/Cubemap can't do (Y rotation only). Set as RenderSettings.skybox, so it also drives ambient + reflections.
// Hand-written because Shader Graph has no skybox target.
// Remake mods (systems.json "skybox", OrbitBuilder.SetupSky): either layer can instead be a mod's image, sampled as it is
// (no baking): a 2:1 panorama (equirectangular, its middle straight ahead: +Z of the sky) or a 6:1 strip of cube faces in
// the order right (+X), left (-X), up (+Y), down (-Y), front (+Z), back (-Z), each as seen from inside facing it (the up
// face with the front at its bottom edge, the down face with the front at its top edge). Mip level 0 (no seam at the
// panorama's wrap, no bleeding between the strip's faces: the face coordinates stay half a texel inside).
Shader "GoF2/SpaceSky"
{
    Properties
    {
        [NoScaleOffset] _Stars ("Stars", Cube) = "black" {}
        [NoScaleOffset] _Nebula ("Nebula (added)", Cube) = "black" {}
        [NoScaleOffset] _StarsMap ("Stars (mod image)", 2D) = "black" {}
        [NoScaleOffset] _NebulaMap ("Nebula (mod image)", 2D) = "black" {}
        _StarsGain ("Stars brightness", Float) = 1
        _NebulaGain ("Nebula brightness", Float) = 1
        _Exposure ("Exposure", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ _NEBULA_PANORAMA _NEBULA_STRIP
            #pragma multi_compile_local _ _STARS_PANORAMA _STARS_STRIP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURECUBE(_Stars);  SAMPLER(sampler_Stars);
            TEXTURECUBE(_Nebula); SAMPLER(sampler_Nebula);
            TEXTURE2D(_StarsMap);  SAMPLER(sampler_StarsMap);  float4 _StarsMap_TexelSize;
            TEXTURE2D(_NebulaMap); SAMPLER(sampler_NebulaMap); float4 _NebulaMap_TexelSize;
            float4x4 _SkyRotation;   // world direction -> baked cube direction, set by GoF2SpaceLevel
            half _Exposure, _StarsGain, _NebulaGain;

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.dir = mul((float3x3)_SkyRotation, i.positionOS.xyz);
                return o;
            }

            // Equirectangular: u around (0.5 = +Z, 0.75 = +X), v up (0.5 = the horizon).
            float2 PanoramaUV(float3 d)
            {
                return float2(atan2(d.x, d.z) * (0.5 / PI) + 0.5, asin(clamp(d.y, -1.0, 1.0)) / PI + 0.5);
            }

            // A 6:1 strip: the face and its coordinates (u to the right, v up as seen from inside), kept half a texel in.
            float2 StripUV(float3 d, float4 texel)
            {
                float3 a = abs(d);
                float face; float2 f;
                if (a.x >= a.y && a.x >= a.z)
                {
                    if (d.x > 0) { face = 0; f = float2(-d.z, d.y) / a.x; }   // right: its right is -Z
                    else { face = 1; f = float2(d.z, d.y) / a.x; }            // left: its right is +Z
                }
                else if (a.y >= a.z)
                {
                    if (d.y > 0) { face = 2; f = float2(d.x, -d.z) / a.y; }   // up: the front at the bottom
                    else { face = 3; f = float2(d.x, d.z) / a.y; }            // down: the front at the top
                }
                else
                {
                    if (d.z > 0) { face = 4; f = float2(d.x, d.y) / a.z; }    // front
                    else { face = 5; f = float2(-d.x, d.y) / a.z; }           // back: its right is -X
                }
                float2 uv = f * 0.5 + 0.5;
                float inset = 0.5 * texel.y;   // half a texel of the face (its height is the image's)
                uv = clamp(uv, inset, 1.0 - inset);
                return float2((face + uv.x) / 6.0, uv.y);
            }

            half3 Layer(TEXTURECUBE_PARAM(cube, cubeSampler), TEXTURE2D_PARAM(map, mapSampler), float4 texel, float3 d, int mode)
            {
                if (mode == 1) return SAMPLE_TEXTURE2D_LOD(map, mapSampler, PanoramaUV(normalize(d)), 0).rgb;
                if (mode == 2) return SAMPLE_TEXTURE2D_LOD(map, mapSampler, StripUV(d, texel), 0).rgb;
                return SAMPLE_TEXTURECUBE(cube, cubeSampler, d).rgb;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                #if defined(_STARS_PANORAMA)
                    const int starsMode = 1;
                #elif defined(_STARS_STRIP)
                    const int starsMode = 2;
                #else
                    const int starsMode = 0;
                #endif
                #if defined(_NEBULA_PANORAMA)
                    const int nebulaMode = 1;
                #elif defined(_NEBULA_STRIP)
                    const int nebulaMode = 2;
                #else
                    const int nebulaMode = 0;
                #endif
                half3 c = Layer(TEXTURECUBE_ARGS(_Stars, sampler_Stars), TEXTURE2D_ARGS(_StarsMap, sampler_StarsMap), _StarsMap_TexelSize, i.dir, starsMode) * _StarsGain
                        + Layer(TEXTURECUBE_ARGS(_Nebula, sampler_Nebula), TEXTURE2D_ARGS(_NebulaMap, sampler_NebulaMap), _NebulaMap_TexelSize, i.dir, nebulaMode) * _NebulaGain;
                return half4(c * _Exposure, 1);
            }
            ENDHLSL
        }
    }
}
