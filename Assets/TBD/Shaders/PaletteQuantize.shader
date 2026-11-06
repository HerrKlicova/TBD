// Capa 1 (mundo): ampliación con vecino más cercano de la textura de baja resolución, con contorno de 1 píxel
// y acercamiento a la paleta de 32 colores con mezcla ajustable (docs/arte/direccion-visual.md, v1.4).
// Se pinta sobre un cuadrilátero de pantalla completa visto por la cámara "Presentación".
// Pendiente (prototipo): contorno por profundidad y normales (hoy es por diferencia de luminosidad),
// corrección de subpíxel de la cámara y prueba de profundidad contra los efectos de alta resolución.
Shader "Hidden/TBD/PaletteQuantize"
{
    Properties
    {
        _MainTex ("Textura del mundo (baja resolución)", 2D) = "white" {}
        _PaletteMix ("Mezcla con la paleta (0-1)", Range(0, 1)) = 1
        _OutlineStrength ("Intensidad del contorno (0-1)", Range(0, 1)) = 0.6
        _OutlineColor ("Color del contorno", Color) = (0.1098, 0.1255, 0.0471, 1)   // #1c200c (Lucía, 6-nov), en vez de casi negro
        _OutlineThreshold ("Umbral del contorno", Range(0.02, 0.5)) = 0.16
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Palette[32];       // color de la paleta en espacio lineal (lo rellena PrototypeBootstrap)
            float _PaletteMix;
            float _OutlineStrength;
            float4 _OutlineColor;
            float _OutlineThreshold;

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

            float Luma(float3 c)
            {
                return dot(c, float3(0.299, 0.587, 0.114));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;

                // contorno de 1 píxel: el píxel más oscuro de un salto de luminosidad se oscurece
                float2 t = _MainTex_TexelSize.xy;
                float l = Luma(c);
                float l1 = Luma(tex2D(_MainTex, i.uv + float2(t.x, 0)).rgb);
                float l2 = Luma(tex2D(_MainTex, i.uv - float2(t.x, 0)).rgb);
                float l3 = Luma(tex2D(_MainTex, i.uv + float2(0, t.y)).rgb);
                float l4 = Luma(tex2D(_MainTex, i.uv - float2(0, t.y)).rgb);
                float diff = max(max(l1 - l, l2 - l), max(l3 - l, l4 - l));
                float edge = step(_OutlineThreshold, diff);
                c = lerp(c, _OutlineColor.rgb, edge * _OutlineStrength);

                // acercamiento a la paleta
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
                c = lerp(c, res, _PaletteMix);
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
