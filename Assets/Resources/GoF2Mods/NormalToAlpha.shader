// A glTF normal map (RGB) blitted into the layout Android builds read normal maps in: X in alpha, Y in green (the Android
// normal map encoding, URP's UnpackNormalAG). Modding.ModMaterials uses it for mods' glTF normal maps on Android; the PNG
// normal maps are swizzled on the CPU before compression (ModMaterials.NormalToAlpha).
Shader "Hidden/GoF2/NormalToAlpha"
{
    Properties { _MainTex ("Normal map", 2D) = "bump" {} }
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
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
            float4 frag(v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                return float4(c.r, c.g, c.b, c.r);
            }
            ENDHLSL
        }
    }
}
