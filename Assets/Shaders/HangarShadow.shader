// GoF2/HangarShadow: the hangar ships' contact shadows (World.HangarShipShadow), projected like a decal: drawn on a box
// around the ship's footprint (its back faces, where the scene lies in front of them), each pixel's scene point is
// rebuilt from the camera depth texture (the station camera asks for it), taken into the box's space and darkened by the
// ship's map (ShipShadowSet: x / z of the box = the texture): under the silhouette (G) and in a wide soft halo around it (A,
// what a camera looking down past the hull sees), only below the hull's underside there (R, so never on the hull itself),
// only on surfaces facing up, full over the box's lower part and fading toward its top: on the pads, cradles, rims and
// crates under and around the ship. Clipped outside the box. Hand-written: Shader Graph has no decal target without URP's decal renderer feature.
Shader "GoF2/HangarShadow"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Silhouette (alpha)", 2D) = "white" {}
        _Color ("Colour (alpha = strength)", Color) = (0, 0, 0, 0.7)
        _Hull ("Hull bottom y, height range, fade from y, gone at y (m)", Vector) = (0, 1, 2, 8)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull Front
        ZWrite Off
        ZTest GEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Hull;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screen : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.screen = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.screen.xy / i.screen.w;
                float depth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 local = TransformWorldToObject(world);   // the box is the unit cube, -0.5..0.5
                clip(0.5 - abs(local));
                half4 m = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, local.xz + 0.5, 0);
                half mask = max(m.g, saturate(m.a * 2.2) * 0.85);
                // Below the hull's underside over this point (spread outward past the hull): under the hull a little below
                // it, so the hull doesn't shade itself; around it up to 3 m above (the pads' rims around a sunk hull).
                float under = _Hull.x + m.r * _Hull.y + (m.g > 0.5 ? -0.15 : 3.0);
                mask *= saturate((under - world.y) * 4.0);
                // Full strength up to _Hull.z, fading out to _Hull.w (heights above the floor, HangarShipShadow); only on
                // surfaces facing up (the normal from the rebuilt positions' screen derivatives), so the hull's sides and
                // belly stay lit.
                half height = saturate((_Hull.w - world.y) / max(0.01, _Hull.w - _Hull.z));
                float3 n = normalize(cross(ddy(world), ddx(world)));
                half up = saturate((abs(n.y) - 0.35) * 2.5);
                half a = mask * _Color.a * height * up;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
