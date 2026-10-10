// A glTF metallic-roughness texture (B = metallic, G = roughness) blitted into URP Lit's metallic / smoothness map
// (R = metallic, A = smoothness = 1 - roughness), the material's metallic / roughness factors applied (glTF: metallic =
// metallicFactor x B, roughness = roughnessFactor x G). Modding.ModMaterials uses it for mods' glTF materials.
Shader "Hidden/GoF2/MetallicRoughnessToGloss"
{
    Properties
    {
        _MainTex ("Metallic roughness", 2D) = "white" {}
        _MetallicFactor ("Metallic factor", Float) = 1
        _RoughnessFactor ("Roughness factor", Float) = 1
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _MetallicFactor, _RoughnessFactor;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
            float4 frag(v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                return float4(c.b * _MetallicFactor, 0, 0, 1 - c.g * _RoughnessFactor);
            }
            ENDHLSL
        }
    }
}
