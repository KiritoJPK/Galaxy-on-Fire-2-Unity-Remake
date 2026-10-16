// GoF2/ModEngineFlame: a mod ship's engine flame drawn from flame geometry in its model (ships.json / shipkits.json
// "materials" with "flame": true; EVERSPACE 2's M_Vehicle_EngineExhaust sections), rebuilt from that material's parameters
// (its graph can't be exported) and matched against EVERSPACE 2 running (the Sentinel behind, beside and chased at full
// thrust and boosting, in its photo mode). The mesh is short flat cards behind each nozzle whose UV u runs from the nozzle
// (0) to the tip (1); the vertex shader stretches them backward by u x the length (EngineFlame sets _FlameDir in object
// space, _FlameThrust 0..1, _FlameBoost 0..1 and _FlameLevel, the engines on / off, per renderer).
// UV1: x the coordinate across the card (its long edges fade, the card narrows toward the tip: _EdgePower, _Taper), y the
// card's kind read as |y - 0.5| (a V flip leaves it): 0 a main card (the thrust, the engine colour also through a boost),
// 0.25 a boost card (only while boosting, in _BoostColor: EVERSPACE 2's green-marked boost jets; also LateralThrusters'
// jets), 0.5 no UV1 (the cards fade by their bent normals' lean off the flat card, _EdgeSoftness, and the whole flame turns
// toward _BoostColor while boosting).
// The fragment: additive, gold near the nozzle (boost cards white-blue; _Core), fading hard toward the tip (_Fade), ES2's
// T_Noise_018_M stretched into fine streaks drifting outward, each ending at its own length; main cards fade as they turn
// edge-on (|N.V| ^ _EdgeOnFade, ES2's OpacityExponent: from behind only thin rays remain), boost cards don't (they glow from
// behind as bright slots). _Widen (off) widens UV1 cards toward the camera; _Graze (off) brightens edge-on cards.
// _BoostOverride (EngineFlame per renderer: an exhaust style on a ship that has no boost jets, Modding.ModExhausts): the
// boost cards stay hidden and the main cards turn to _BoostColor while boosting instead.
Shader "GoF2/ModEngineFlame"
{
    Properties
    {
        [HDR] _EmissionColor ("Flame colour", Color) = (3, 1.2, 0.3, 1)
        [HDR] _BoostColor ("Boost colour", Color) = (0.6, 1.8, 3, 1)
        _NoiseMap ("Noise", 2D) = "white" {}
        _NoiseTiling ("Noise tiling (x: per model unit along)", Vector) = (0.06, 1, 0, 0)
        _StreakTiling ("Streaks across a card", Float) = 5
        _NoiseSpeed ("Noise speed (along, /s)", Float) = 0.8
        _NoiseStrength ("Noise strength", Range(0, 1)) = 0.85
        _Length ("Length at full throttle (model units)", Float) = 5
        _IdleLength ("Length at idle (share of full)", Range(0, 1)) = 0.15
        _BoostLength ("Extra length at full boost (share of full)", Float) = 0.3
        _Fade ("Fade along the flame", Float) = 4
        _Widen ("Widen toward the camera (model units, UV1; 0 = off)", Float) = 0
        _StreakContrast ("Streak contrast (exponent)", Float) = 2.2
        _EdgePower ("Edge fade (UV1 across the card)", Float) = 1.2
        _Taper ("Taper toward the tip (UV1)", Range(0, 1)) = 0.25
        _EdgeSoftness ("Edge softness (cos of the normal's lean at a faded edge)", Range(0, 1)) = 0.72
        _Graze ("Edge-on brightening", Float) = 0
        _EdgeOnFade ("Fade seen edge-on (|N.V| ^ x, ES2's OpacityExponent)", Float) = 2
        _Core ("Gold core near the nozzle", Float) = 1.2
        _BoostBrightness ("Boost jets' brightness", Float) = 1.6
        _BoostCore ("Boost jets' white head (share of _Core)", Float) = 1
        _Brightness ("Brightness", Float) = 1
        _FlameDir ("Backward, object space (EngineFlame)", Vector) = (0, 0, -1, 0)
        _FlameThrust ("Thrust 0..1 (EngineFlame)", Float) = 1
        _FlameBoost ("Boost 0..1 (EngineFlame)", Float) = 0
        _FlameLevel ("Engines on 0..1 (EngineFlame)", Float) = 1
        _BoostOverride ("Boost recolours the main flame, no boost cards (EngineFlame)", Float) = 0
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

            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionColor, _BoostColor;
                float4 _NoiseMap_ST, _NoiseTiling, _FlameDir;
                float _Widen, _StreakContrast, _StreakTiling, _NoiseSpeed, _NoiseStrength, _Length, _IdleLength, _BoostLength, _Fade, _EdgePower, _Taper, _EdgeSoftness, _Graze, _EdgeOnFade, _Core, _Brightness, _BoostBrightness, _BoostCore;
                float _FlameThrust, _FlameBoost, _FlameLevel, _BoostOverride;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; float stretch : TEXCOORD3; float2 across : TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };

            // UV1.y's card kind, read as |y - 0.5| (a V flip leaves it): 0 a main flame card, 0.25 a boost card (shown only
            // while boosting), 0.5 (no UV1) neither, the card fading by its bent normals.
            bool HasAcross(float y) { return abs(y - 0.5) < 0.375; }
            bool IsBoostCard(float y) { return abs(abs(y - 0.5) - 0.25) < 0.125; }

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float thrust = saturate(_FlameThrust), boost = saturate(_FlameBoost), level = saturate(_FlameLevel);
                // Off: the flame shrinks back into its nozzle as it fades. A boost card grows only with the boost.
                float stretch = IsBoostCard(i.uv1.y) ? _Length * boost * level * (1 - saturate(_BoostOverride))
                              : _Length * (_IdleLength + (1 - _IdleLength) * thrust) * (1 + _BoostLength * boost) * level;
                float3 dir = normalize(_FlameDir.xyz + float3(0, 0, 1e-5));
                float3 pos = i.positionOS.xyz + dir * saturate(i.uv.x) * stretch;
                float3 world = TransformObjectToWorld(pos);
                // ES2's cards are thinner than its flames (the wings' are 0.1 m boxes): with UV1 each card also widens
                // across, turned toward the camera like an axial billboard (by _Widen model units, from a quarter at the
                // nozzle, where the grilles and rings show, to the full width further back); seen along the flame the widening fades out, so from behind the cards stay lines (ES2's rays).
                if (HasAcross(i.uv1.y) && _Widen > 0)
                {
                    float3 axis = TransformObjectToWorldDir(dir);
                    float3 side = cross(axis, normalize(_WorldSpaceCameraPos - world));
                    float len = length(side);
                    float scale = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                    world += side / max(len, 1e-4) * saturate(len * 3) * (i.uv1.x - 0.5) * _Widen * scale * (0.25 + 1.0 * saturate(i.uv.x)) * level;
                }
                o.positionCS = TransformWorldToHClip(world);
                o.uv = i.uv;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.positionWS = world;
                o.across = i.uv1;
                o.stretch = stretch;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float u = saturate(i.uv.x);
                float level = saturate(_FlameLevel), boost = saturate(_FlameBoost);
                // ES2's look: fine streaks along the flame (the noise stretched: _StreakTiling across a card, _NoiseTiling.x
                // per model unit along it, drifting outward), each streak ending at its own length (a noise read across only).
                bool hasAcross = HasAcross(i.across.y), boostCard = IsBoostCard(i.across.y);
                float side = hasAcross ? i.across.x : i.uv.y;
                float along = u * max(i.stretch, 0.5) * _NoiseTiling.x - _Time.y * _NoiseSpeed;
                half fine = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, float2(side * _StreakTiling, along)).r;
                half coarse = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, float2(side * _StreakTiling * 0.29 + 0.37, along * 0.5 + 0.21)).r;
                half streak = saturate(pow(fine, _StreakContrast) * (1 + _StreakContrast) * (0.55 + coarse * 0.9));
                half lengthNoise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, float2(side * _StreakTiling * 0.7 + 0.53, 0.5 + _Time.y * 0.03)).r;
                half reach = lerp(1, lerp(0.35, 1.1, lengthNoise), _NoiseStrength);
                half tip = saturate((reach - u) / 0.3);
                // Fading along the length.
                half body = pow(saturate(1 - u), _Fade);
                // (the streaks half closed at the nozzle, opening up along the first third)
                half a = body * tip * lerp(1, streak, _NoiseStrength * (0.5 + 0.5 * saturate(u * 3)));
                // Soft card edges: the bent normal against the card's flat face (the stretched card's own normal).
                float3 face = normalize(cross(ddy(i.positionWS), ddx(i.positionWS)));
                float3 nrm = normalize(i.normalWS), v = normalize(GetWorldSpaceViewDir(i.positionWS));
                // (with UV1 the flame also narrows toward its tip: the across profile squeezed by 1 - _Taper u)
                float across = saturate((i.across.x - 0.5) / max(1 - _Taper * u, 0.05) + 0.5);
                half edge = hasAcross ? pow(saturate(sin(across * PI)), _EdgePower)
                          : smoothstep(_EdgeSoftness, lerp(_EdgeSoftness, 1, 0.85), abs(dot(face, nrm)));
                half graze = lerp(1, min(1.0 / (abs(dot(face, v)) + 0.3), 2.5) * 0.6, _Graze);
                // ES2's OpacityExponent: a card fades as it turns edge-on, so from the side the flame burns bright and from
                // behind (the cards edge-on) only thin rays remain.
                // (not the boost cards: EVERSPACE 2's boost jets glow from behind as bright slots)
                half facing = edge * (boostCard ? 1 : graze * pow(saturate(abs(dot(face, v))), _EdgeOnFade));
                // ES2's main flames keep the engine colour through a boost, its boost cards burn in the boost colour; a
                // flame without card kinds (no UV1) turns toward the boost colour instead.
                // (_BoostOverride: the main cards turn to the boost colour too)
                float over = saturate(_BoostOverride);
                half3 col = boostCard ? _BoostColor.rgb : hasAcross ? lerp(_EmissionColor.rgb, _BoostColor.rgb, boost * over) : lerp(_EmissionColor.rgb, _BoostColor.rgb, boost);
                // Gold (the boost cards: white) and brighter near the nozzle (ES2's main flame never goes white there).
                half peak = max(max(col.r, col.g), col.b);
                // (the boost jets: a near-white head where each jet starts, like EVERSPACE 2's customization screen shows
                // the Wraith's; _BoostBrightness their strength against the main flame)
                half hot = saturate(exp(-u * (boostCard ? 8 : 4)) * _Core * (boostCard ? _BoostCore : 1));
                col = lerp(col, (boostCard ? half3(0.85, 0.97, 1) : half3(1, 0.5, 0.1)) * peak, hot) * (1 + hot * (boostCard ? 1 : 0.5));
                half3 rgb = col * a * facing * _Brightness * level * (boostCard ? boost * _BoostBrightness * (1 - over) : 1);
                return half4(rgb, 0);
            }
            ENDHLSL
        }
    }
}
