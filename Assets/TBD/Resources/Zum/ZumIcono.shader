// Estética del zum · iconos de unidades, trenes, estaciones, cuadrilla y depósito. Bruno (Arte técnico), 11-11.
// La textura la hace ZumEstetica.Icono (dibujos de IconosMapa más cuadrilla y depósito): R = 1 relleno, R = 0 contorno 0f120c, A = máscara.
// _Relleno tiñe el relleno (hueso d4d0cc; el tren, del color de su línea). _Visible (0–1) entra o sale con tramado Bayer 4×4 en píxeles de
// pantalla, en 0,3 s (ZumEstetica.Tramar): nada semitransparente. Se dibuja desde OnGUI con ZumEstetica.DibujarIcono.
// SIN COMPILAR: escrito en la nube, sin Unity.
Shader "TBD/ZumIcono"
{
    Properties
    {
        _MainTex ("Icono (R relleno, A máscara)", 2D) = "black" {}
        _Relleno ("Relleno", Color) = (0.831, 0.816, 0.800, 1)       // d4d0cc (sRGB, Unity lo pasa a lineal)
        _Contorno ("Contorno", Color) = (0.059, 0.071, 0.047, 1)     // 0f120c
        _Visible ("Visible (tramado 0–1)", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Relleno, _Contorno;
            float _Visible;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            static const float BAYER4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                clip(t.a - 0.5);
                uint2 sp = (uint2)floor(i.pos.xy) & 3u;
                clip(_Visible - (BAYER4[sp.y * 4 + sp.x] / 16.0 + 1.0 / 32.0));
                return fixed4(t.r > 0.5 ? _Relleno.rgb : _Contorno.rgb, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
