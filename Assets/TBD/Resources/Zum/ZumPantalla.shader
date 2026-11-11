// Estética del zum · la RT del mundo a pantalla con la CALIMA de día (D-N197 adaptada). Bruno (Arte técnico), 11-11.
// La calima ya no es una franja en el borde de la zona (el mundo es continuo): es perspectiva aérea que sube con la cámara.
// a27e5a al 0 % en SOLDADO, 10 % en PUEBLO y 22 % en COMARCA; en MAPA no hay (el plano táctico es un dibujo, no una vista).
// El porcentaje lo calcula ZumEstetica.CalimaDe(altura) y lo pone ZumEstetica.DibujarPantalla, que sustituye a GUI.DrawTexture(_rt) en OnGUI.
// Se tiñe la imagen entera (suelo, casas y unidades por igual); los iconos y las placas se dibujan después, sin calima.
// CGPROGRAM a propósito: Graphics.DrawTexture desde OnGUI es dibujo inmediato, como la UI de siempre (UI/Default es CG y va en URP).
// SIN COMPILAR: escrito en la nube, sin Unity.
Shader "TBD/ZumPantalla"
{
    Properties
    {
        _MainTex ("RT del mundo", 2D) = "black" {}
        _CalimaColor ("Calima", Color) = (0.635, 0.494, 0.353, 1)   // a27e5a
        _Calima ("Calima (0–1)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "IgnoreProjector" = "True" "RenderType" = "Opaque" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _CalimaColor;
            float _Calima;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                c.rgb = lerp(c.rgb, _CalimaColor.rgb, _Calima);
                c.a = 1;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
