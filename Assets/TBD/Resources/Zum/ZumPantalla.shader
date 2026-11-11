// Estética del zum · la RT del mundo a pantalla con la CALIMA de día (D-N197 adaptada). Bruno (Arte técnico), 11-11.
// La calima ya no es una franja en el borde de la zona (el mundo es continuo): es perspectiva aérea que sube con la cámara.
// a27e5a al 0 % en SOLDADO, 10 % en PUEBLO y 22 % en COMARCA; en MAPA no hay (el plano táctico es un dibujo, no una vista).
// El porcentaje lo calcula ZumEstetica.CalimaDe(altura) y lo pone ZumEstetica.DibujarPantalla, que sustituye a GUI.DrawTexture(_rt) en OnGUI.
// Se tiñe la imagen entera (suelo, casas y unidades por igual); los iconos y las placas se dibujan después, sin calima.
// CGPROGRAM a propósito: Graphics.DrawTexture desde OnGUI es dibujo inmediato, como la UI de siempre (UI/Default es CG y va en URP).
// GAMMA (corregido el 11-11 tras la build Mundo6 de Marcos): la RT se lee en LINEAL y lo que se escribe desde OnGUI con Graphics.DrawTexture
// llega a la pantalla TAL CUAL, sin pasar a sRGB (salía todo oscuro: 303424 se veía 080904, que es su valor lineal). Con _Codificar = 1
// (ZumEstetica lo pone si el proyecto va en espacio lineal) el shader pasa a sRGB él mismo, y la calima se mezcla ya en sRGB, como en las láminas.
// Si algún día saliera todo lavado (sRGB dos veces), ZumEstetica.CodificarGamma = false.
// Compila en la build Mundo6 (Marcos, 11-11).
Shader "TBD/ZumPantalla"
{
    Properties
    {
        _MainTex ("RT del mundo", 2D) = "black" {}
        _CalimaColor ("Calima", Color) = (0.635, 0.494, 0.353, 1)   // a27e5a
        _Calima ("Calima (0–1)", Range(0, 1)) = 0
        _Codificar ("Pasar a sRGB al escribir (1 en proyecto lineal)", Float) = 1
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
            float4 _CalimaColor;
            float _Calima;
            float _Codificar;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            float4 frag(v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float3 cal = _CalimaColor.rgb;
                if (_Codificar > 0.5) { c.rgb = LinearToGammaSpace(c.rgb); cal = LinearToGammaSpace(cal); }
                c.rgb = lerp(c.rgb, cal, _Calima);
                c.a = 1;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
