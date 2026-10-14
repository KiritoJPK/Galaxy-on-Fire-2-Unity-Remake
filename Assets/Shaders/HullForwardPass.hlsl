// HullForwardPass.hlsl: GoF2/Hull's ForwardLit fragment. URP Lit's own fragment (LitPassFragment, unchanged lighting:
// UniversalFragmentPBR with its lights, shadows and environment), then the original's BumpRimCubeShader terms on top
// (the fragment source in libgof2hdaa.so, 0x215575):
//   reflectTex   = spec^2                                   (spec = the specular map, the normal map's alpha in the original;
//                                                            here _MetallicGlossMap's alpha, the same values)
//   rim          = smoothstep(0.8, 1, 1 - N.V)
//   reflectivity = clamp(smoothstep(0.6, 1, 1 - N.V), reflectTex, 2 spec + rim)
//   out += cubemap(reflect(V, N)) * reflectivity + rim * reflectTex * rimColor
// The cubemap is the environment reflection (unity_SpecCube0: the system's cubemap, SkyReflection), sampled sharp like the
// original's textureCube (the GPU's mip choice). Only materials with a specular map (_METALLICSPECGLOSSMAP) get the terms, scaled down by the
// material's metallic (a mod's metal parts already reflect through URP). Globals (SRP batcher safe): _GoF2HullReflection
// (strength, 0 = URP Lit as it is), _GoF2HullRimColor (3 x the system's rim table, StarSystem::initLight).

#ifndef GOF2_HULL_FORWARD_PASS_INCLUDED
#define GOF2_HULL_FORWARD_PASS_INCLUDED

half _GoF2HullReflection;
half3 _GoF2HullRimColor;
// Remake: the system's reflection cubemap as a global of its own (SkyReflection.SetSystem): unity_SpecCube0 stayed empty in
// the Android build (Forward on the Mobile renderer).
TEXTURECUBE(_GoF2ReflectionCube);
SAMPLER(sampler_GoF2ReflectionCube);

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

void HullPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = input.viewDirTS;
    ApplyPerPixelDisplacement(viewDirTS, input.uv);
#endif

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

    LODFadeCrossFade(input.positionCS);

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    if (!IsSurfaceTypeTransparent())
        ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData, IsSpecularSetup());
#endif

    InitializeBakedGIData(input, inputData);

    URP_LIGHT_ACCUM4 color = UniversalFragmentPBR(inputData, surfaceData,
        IsSpecularSetup(), UseSpecularHighlights(), UseAlphaPremultiply(),
        UseClearCoat() || UseClearCoatMap(), ReceiveShadows(), IsSurfaceTypeTransparent(), UseEnvironmentReflections(), input.normalWS.xyz);

#if defined(_METALLICSPECGLOSSMAP)
    {
        half spec = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv).a;
        half reflectTex = spec * spec;
        half3 n = inputData.normalWS;
        half3 v = inputData.viewDirectionWS;
        half facing = 1.0h - saturate(dot(v, n));
        half rim = smoothstep(0.8h, 1.0h, facing);
        half reflectivity = clamp(smoothstep(0.6h, 1.0h, facing), reflectTex, spec * 2.0h + rim);
        half3 r = reflect(-v, n);
        // The GPU's own mip choice (the original's textureCube): a fixed mip 0 sparkled on far and grazing panels.
        half3 env = SAMPLE_TEXTURECUBE(_GoF2ReflectionCube, sampler_GoF2ReflectionCube, r).rgb;   // an LDR cubemap: nothing to decode
        half k = _GoF2HullReflection * (1.0h - surfaceData.metallic);
        color.rgb += (env * reflectivity + rim * reflectTex * _GoF2HullRimColor) * k * surfaceData.occlusion;
    }
#endif

    // The distance along the pixel's ray: its view depth over the ray's cosine to the camera's forward axis.
    float fogDist = LinearEyeDepth(input.positionCS.z, _ZBufferParams) / max(dot(-inputData.viewDirectionWS, -UNITY_MATRIX_V[2].xyz), 1e-4);
    color.rgb = ClampExposed(inputData.preExposureMultiplier * GoF2RadialFog(color.rgb, fogDist));
#if defined(_TRANSPARENT_RECEIVE_FOG)
    if (IsSurfaceTypeTransparent())
        color.rgb = MixVolumetricFog(color.rgb, color.a, _Blend, UseAlphaPremultiply(), input.positionCS);
#endif
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent());

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
