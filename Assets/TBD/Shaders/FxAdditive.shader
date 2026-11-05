// Capa 2 (efectos de luz a resolución completa): chispas, trazadoras y fogonazos como partículas aditivas con brillo HDR.
// El color de las partículas de Unity llega en 8 bits, así que la intensidad HDR (> 1) va en _Intensity y es el bloom
// (umbral 1) el que la "derrama". Se ve por la cámara "VFX_HD", después de ampliar el mundo pixelado.
Shader "TBD/FxAdditive"
{
    Properties
    {
        _Intensity ("Intensidad HDR", Range(0, 16)) = 4
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;
                float a = saturate(1.0 - d);
                a = a * a;
                return float4(i.color.rgb * i.color.a * a * _Intensity, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
