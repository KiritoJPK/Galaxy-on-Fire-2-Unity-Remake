// A weapon effect atlas made grey for a mod's recolour (items.json "fx" parts with a "color" and no sprite / model):
// each texel's brightest channel, alpha kept, so the game's light-blue bolt keeps its bright core and soft edge and the
// part's _Color gives it the mod's hue. Modding.ModWeapons blits the base item's texture through it once per texture.
Shader "Hidden/GoF2/FxRecolor"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
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
                float m = max(c.r, max(c.g, c.b));
                return float4(m, m, m, c.a);
            }
            ENDHLSL
        }
    }
}
