// A weapon effect's texture recoloured (Flight.WeaponTint, the Customize screen's weapon colour): each texel's brightness (its
// strongest channel, so a red laser's red counts in full) times the colour, its hottest part kept white-hot; alpha kept.
// Blitted once per texture and colour into a render texture the tinted material copies use.
Shader "Hidden/GoF2/WeaponTint"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Colour (linear)", Color) = (1, 1, 1, 1)
        _Core ("White-hot core", Float) = 0.6
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
            float4 _Tint;
            float _Core;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
            float4 frag(v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float v = max(c.r, max(c.g, c.b));
                float3 rgb = lerp(_Tint.rgb * v, v.xxx, smoothstep(0.7, 1.0, v) * _Core);
                return float4(rgb, c.a);
            }
            ENDHLSL
        }
    }
}
