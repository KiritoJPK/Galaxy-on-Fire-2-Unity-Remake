// EmissiveCube.shader
// Remake: the GoF2/Unlit Shader Graph's look for the *_emissive layers (Assets/Shaders/Unlit.shadergraph: _MainTex *
// lerp(1, vertex colour, _UseVertexColor) * _Color * _Glow, opaque) plus GoF2/Hull's cubemap reflection and rim
// (HullForwardPass.hlsl, the original's BumpRimCubeShader terms) from the hull's own maps (EmissiveReflection: the hull
// material that shares the emissive layer's atlas lends its normal map and specular). The emissive layers cover parts
// of the hull (a station's lit panels, a gate's inner ring), and drawn unlit they hid the reflection the hull shows
// around them. Same globals as GoF2/Hull: _GoF2HullReflection (strength), _GoF2HullRimColor; the cubemap is the
// environment reflection (unity_SpecCube0: the system's cubemap, SkyReflection).
// Remake: the GoF2/AlphaTest layers too (a wrecked station's torn panels, *_alpha_emissive): _ALPHATEST_ON clips as
// Assets/Shaders/AlphaTest.shadergraph does (alpha of the same product below 0.5, both faces drawn: _Cull 0).

Shader "GoF2/EmissiveCube"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _UseVertexColor ("Multiply Vertex Color", Float) = 1
        _Glow ("Glow Intensity", Float) = 1
        _BumpMap ("Normal Map (the hull's)", 2D) = "bump" {}
        _MetallicGlossMap ("Specular in alpha (the hull's)", 2D) = "black" {}
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half _UseVertexColor;
            half _Glow;
            half _Cull;
        CBUFFER_END

        // Remake: GoF2/AlphaTest's cut (Alpha Clip Threshold 0.5 on the alpha of texture * vertex colour * tint * glow).
        void GoF2AlphaClip(float2 uv, half4 vertexColor)
        {
        #if defined(_ALPHATEST_ON)
            half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a * lerp(1.0h, vertexColor.a, _UseVertexColor) * _Color.a * _Glow;
            clip(a - 0.5h);
        #endif
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ _ALPHATEST_ON   // multi_compile: only run-time materials use it (EmissiveReflection)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half _GoF2HullReflection;
            half3 _GoF2HullRimColor;
            TEXTURECUBE(_GoF2ReflectionCube); SAMPLER(sampler_GoF2ReflectionCube);   // as GoF2/Hull (SkyReflection)

            // Remake: the distance fog by the real distance to the camera, as the original (computeLinearFogFactor(eyeDist), the
            // eye-to-vertex distance). URP's fog goes by the view depth (clip z), which is shorter for the same distance toward the
            // screen's edges: a fogged station in the middle of the screen came out clear and close-looking at the edge, though the
            // ship hadn't moved. unity_FogParams: x = density / sqrt(ln 2), y = density / ln 2, z = -1 / (end - start), w = end / (end - start).
            half3 GoF2RadialFog(half3 color, float d)
            {
            #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                #if defined(FOG_LINEAR)
                    half f = saturate(d * unity_FogParams.z + unity_FogParams.w);
                #elif defined(FOG_EXP)
                    half f = saturate(exp2(-unity_FogParams.y * d));
                #else
                    float x = unity_FogParams.x * d;
                    half f = saturate(exp2(-x * x));
                #endif
                color = lerp(unity_FogColor.rgb, color, f);
            #endif
                return color;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;   // xy the emissive texture (its tiling), zw the hull's maps (as the hull: none)
                half4 color : COLOR;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = nrm.normalWS;
                o.tangentWS = float4(nrm.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = float4(TRANSFORM_TEX(v.uv, _MainTex), v.uv);
                o.color = v.color;   // Remake: raw, the clip needs its alpha (the lerp by _UseVertexColor is in frag)
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                GoF2AlphaClip(i.uv.xy, i.color);
                half3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy).rgb * lerp(half3(1, 1, 1), i.color.rgb, _UseVertexColor)
                    * _Color.rgb * _Glow;

                // GoF2/Hull's BumpRimCubeShader terms, from the hull's normal map and specular.
                half spec = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, i.uv.zw).a;
                float3 n = normalize(i.normalWS) * (front ? 1.0 : -1.0);   // Remake: back faces (the AlphaTest layers' Cull Off)
                if (dot(i.tangentWS.xyz, i.tangentWS.xyz) > 1e-6)
                {
                    half3 nTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv.zw));
                    float3 t = normalize(i.tangentWS.xyz);
                    float3 b = cross(n, t) * i.tangentWS.w;
                    n = normalize(nTS.x * t + nTS.y * b + nTS.z * n);
                }
                half3 v = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                half reflectTex = spec * spec;
                half facing = 1.0h - saturate(dot(v, n));
                half rim = smoothstep(0.8h, 1.0h, facing);
                half reflectivity = clamp(smoothstep(0.6h, 1.0h, facing), reflectTex, spec * 2.0h + rim);
                half3 r = reflect(-v, n);
                half3 env = SAMPLE_TEXTURECUBE(_GoF2ReflectionCube, sampler_GoF2ReflectionCube, r).rgb;
                color += (env * reflectivity + rim * reflectTex * _GoF2HullRimColor) * _GoF2HullReflection;

                color = GoF2RadialFog(color, distance(i.positionWS, GetCameraPositionWS()));   // as GoF2/Hull
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ _ALPHATEST_ON
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }
            half frag(Varyings i) : SV_Target { GoF2AlphaClip(i.uv, i.color); return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ _ALPHATEST_ON
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }
            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                GoF2AlphaClip(i.uv, i.color);
                float3 n = NormalizeNormalPerPixel(i.normalWS);
                return half4(front ? n : -n, 0);
            }
            ENDHLSL
        }
    }
}
