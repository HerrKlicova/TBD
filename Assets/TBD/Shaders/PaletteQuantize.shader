// Reduce la imagen a la paleta fija de 32 colores (GDD §11). Se usa al dibujar la textura de baja resolución en pantalla.
// Pendiente (prototipo): contornos por profundidad y normales, luz toon y snapping de cámara.
Shader "Hidden/TBD/PaletteQuantize"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Palette[32];       // color de la paleta en espacio lineal (lo rellena PrototypeBootstrap)

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                float best = 1e9;
                float3 res = c;
                for (int k = 0; k < 32; k++)
                {
                    float3 d = c - _Palette[k].rgb;
                    float dd = dot(d, d);
                    if (dd < best)
                    {
                        best = dd;
                        res = _Palette[k].rgb;
                    }
                }
                return fixed4(res, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
