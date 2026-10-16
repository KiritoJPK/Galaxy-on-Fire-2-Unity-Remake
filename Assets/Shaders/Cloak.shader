// GoF2/Cloak: the player's cloak (AbyssEngine::BumpShaderCloak, shader type 0xe; combat_equipment.md 1.6). The hull is
// lit like before, then dissolves by cloak_map.png into the screen behind it, refracted by the normal map with a slow
// wobble (x + n.x * 100 * sin(rate), y + n.y * 75 * cos(rate) at 1024x768), with a light-blue (0.6, 0.8, 1.0) edge.
// _AnimValue is the fade (0..1 over 2 s): the hull dissolves where the map is below it, the band glowing along the front,
// blended over the lit hull up to 0.5; 0.5..0.75 blends into pure refraction (a glass silhouette).
// Drawn by CloakPass after URP's transparents (LightMode GoF2Cloak) from its copy of the frame, _GoF2CloakScene, which
// holds the ship's own lights and glare as the original's refraction FBO does.
Shader "GoF2/Cloak"
{
    Properties
    {
        _BaseMap ("Base", 2D) = "white" {}
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _CloakMap ("Cloak map", 2D) = "white" {}
        _AnimValue ("Anim value", Range(0, 1)) = 0
        _CloakRate ("Cloak rate", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        ZWrite On
        Cull Back

        Pass
        {
            Tags { "LightMode" = "GoF2Cloak" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D_X(_GoF2CloakScene); SAMPLER(sampler_GoF2CloakScene);

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_CloakMap); SAMPLER(sampler_CloakMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _AnimValue, _CloakRate;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                VertexNormalInputs n = GetVertexNormalInputs(i.normalOS, i.tangentOS);
                o.normalWS = n.normalWS;
                o.tangentWS = float4(n.tangentWS, i.tangentOS.w * GetOddNegativeScale());
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 nTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv));
                float3 bit = i.tangentWS.w * cross(i.normalWS, i.tangentWS.xyz);
                float3 nWS = normalize(mul(nTS, float3x3(i.tangentWS.xyz, bit, i.normalWS)));
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                Light main = GetMainLight();
                half3 lit = albedo * (main.color * saturate(dot(nWS, main.direction)) + SampleSH(nWS));

                float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
                float2 wobble = float2(nTS.x * 100.0 * sin(_CloakRate), nTS.y * 75.0 * cos(_CloakRate)) / float2(1024.0, 768.0);
                half3 refr = SAMPLE_TEXTURE2D_X(_GoF2CloakScene, sampler_GoF2CloakScene, UnityStereoTransformScreenSpaceTex(saturate(suv + wobble))).rgb;

                // BumpShaderCloak's fragment shader as it is (libgof2hdaa.so 0x218183): where the dissolve map is below
                // u_AnimValue (+0.025) the screen behind shows, plus a light-blue band along the dissolve front
                // (r >= u_AnimValue - 0.05); up to 0.5 that blends over the lit hull (x4), from 0.5 to 0.75 into pure
                // refraction. The map's pattern stays on the hull for most of the 2 s fade (#47: the remake dissolved by
                // u_AnimValue x 2, so it was gone within the first second).
                float d = SAMPLE_TEXTURE2D(_CloakMap, sampler_CloakMap, i.uv).r;
                half3 color2 = lit;
                if (d - 0.025 < _AnimValue) color2 = refr + half3(0.6, 0.8, 1.0) * step(_AnimValue - 0.05, d);
                half3 col = _AnimValue < 0.5 ? lerp(lit, color2, saturate(_AnimValue * 4.0))
                                             : lerp(color2, refr, saturate((_AnimValue - 0.5) * 4.0));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
