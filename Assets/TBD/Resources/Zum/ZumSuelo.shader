// Estética del zum (Hito 1, núcleo v2) · suelo del mundo continuo. Bruno (Arte técnico), 11-11.
// Va en las mallas de 128 m del terreno de Álvaro (MundoBootstrap.MontarTerrenoAlvaro) y en el plano del mar. Sustituye a URP/Lit con
// la textura de colores por uso: aquí el color sale de la PALETA DE 32 (D-N46) según el uso del suelo, con el dibujo de las láminas
// (Art/Codigo/zum/build_zum.py, página «Estética del zum»).
// - _Usos: ids de usos del suelo (0 mar … 10 naranjal) en R, textura R8 LINEAL, sin filtro, 1 texel = 1 m, texel (x, z) = metros (X, Z).
//   La hace ZumEstetica.TexturaUsos a partir del PNG de Álvaro (LoadImage deja la fila 0 al sur, igual que Z).
// - _ZumAltura (global, la pone ZumEstetica.Actualizar cada frame): altura de la cámara. Por encima de 900·(1+F) se ve el PLANO TÁCTICO
//   (MAPA, sin luz); por debajo de 900·(1−F), el SUELO DE DÍA (con luz y sombras); en medio, tramado Bayer 4×4 en píxeles de la RT
//   (alfa binaria, nada semitransparente). F = _Fundido = 0,25.
// - La calima NO va aquí: es un tinte de toda la imagen (ZumPantalla), para que caiga igual sobre suelo, casas y unidades.
// Compila en la build Mundo6 (Marcos, 11-11). El color de salida es LINEAL y correcto: el oscuro de Mundo6 venía de ZumPantalla (ver allí).
Shader "TBD/ZumSuelo"
{
    Properties
    {
        [NoScaleOffset] _Usos ("Usos del suelo (R8 lineal, ids 0–10, Point)", 2D) = "black" {}
        _UsoFijo ("Uso fijo (-1 = leer _Usos; 0 = mar, para el plano del mar)", Float) = -1
        _Lado ("Lado del mundo (m)", Float) = 2048
        _UmbralMapa ("Umbral MAPA↔COMARCA (m de altura de cámara)", Float) = 900
        _Fundido ("Fundido (fracción del umbral a cada lado)", Float) = 0.25
        _Relieve ("Cuánto pesa la luz en el suelo de día (0 = paleta pura, 1 = Lambert entero)", Range(0, 1)) = 0.5
        _NaranjosPintados ("Pintar copas de naranjo en el suelo (0 si ya hay naranjos 3D)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Usos_TexelSize;
            float _UsoFijo;
            float _Lado;
            float _UmbralMapa;
            float _Fundido;
            float _Relieve;
            float _NaranjosPintados;
        CBUFFER_END

        float _ZumAltura;   // global (ZumEstetica.Actualizar)
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Usos);

            // Paleta de 32 (D-N46), en LINEAL (el proyecto va en espacio lineal). Comentario: el hex sRGB de la paleta.
            static const half3 OLIVE_BLACK = half3(0.0116, 0.0144, 0.0037);   // 1c200c
            static const half3 NAVY = half3(0.0091, 0.0116, 0.0296);          // 181c30
            static const half3 OLIVE_SHADOW = half3(0.0296, 0.0343, 0.0176);  // 303424
            static const half3 SLATE_DARK = half3(0.0212, 0.0343, 0.0513);    // 283440
            static const half3 GREY_BROWN = half3(0.0648, 0.0513, 0.0452);    // 48403c
            static const half3 SLATE = half3(0.0513, 0.0578, 0.1170);         // 404460
            static const half3 BROWN = half3(0.0887, 0.0395, 0.0144);         // 543820
            static const half3 OLIVE_DARK = half3(0.0802, 0.0887, 0.0116);    // 50541c
            static const half3 OLIVE_GREY = half3(0.1170, 0.1070, 0.0648);    // 605c48
            static const half3 OCHRE_DARK = half3(0.2307, 0.1070, 0.0212);    // 845c28
            static const half3 OLIVE = half3(0.1620, 0.1878, 0.0723);         // 70784c
            static const half3 CONCRETE = half3(0.1500, 0.1878, 0.1746);      // 6c7874
            static const half3 TAN = half3(0.3916, 0.1500, 0.0802);           // a86c50
            static const half3 OLIVE_LIGHT = half3(0.2462, 0.2462, 0.1070);   // 88885c
            static const half3 CONCRETE_LIGHT = half3(0.2961, 0.2961, 0.2623); // 94948c
            static const half3 BEIGE = half3(0.4564, 0.3515, 0.2159);         // b4a080

            // Usos de Álvaro (terreno_hito1.json)
            #define U_MAR 0
            #define U_PLAYA 1
            #define U_MARJAL 2
            #define U_HUERTA 3
            #define U_SECANO 4
            #define U_MONTE 5
            #define U_CAUCE 6
            #define U_CARRETERA 7
            #define U_EXPLANADA 8
            #define U_CASERIO 9
            #define U_NARANJAL 10

            static const float BAYER4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            // Ruido de valor determinista, el mismo de build_zum.py (enteros: no depende de sin() en la GPU)
            float Hash(int2 c, uint semilla)
            {
                uint v = (uint)c.x * 374761393u + (uint)c.y * 668265263u + semilla * 1274126177u;
                v = (v ^ (v >> 13)) * 1274126177u;
                v = v ^ (v >> 16);
                return (float)(v & 0xffffu) / 65535.0;
            }

            float Ruido(float2 p, float s, uint semilla)
            {
                float2 g = p / s;
                float2 g0 = floor(g);
                float2 f = g - g0;
                f = f * f * (3.0 - 2.0 * f);
                int2 i0 = (int2)g0;
                float a = Hash(i0, semilla), b = Hash(i0 + int2(1, 0), semilla);
                float c = Hash(i0 + int2(0, 1), semilla), d = Hash(i0 + int2(1, 1), semilla);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Uso en el punto del mundo (X, Z en m). Fuera del mundo: -1.
            int UsoEn(float2 xz)
            {
                if (_UsoFijo > -0.5) return (int)round(_UsoFijo);
                if (xz.x < 0 || xz.y < 0 || xz.x > _Lado || xz.y > _Lado) return -1;
                int2 tam = (int2)_Usos_TexelSize.zw;
                int2 t = clamp((int2)round(xz), int2(0, 0), tam - 1);
                return (int)round(LOAD_TEXTURE2D(_Usos, t).r * 255.0);
            }

            // Suelo de día (lámina de comarca, pueblo y soldado). p = (X, 2048 − Z): el sur hacia +y, como en las láminas.
            half3 Dia(int uso, float2 p, float mpp, float bayer, bool bordeCauce, bool cuneta)
            {
                float mota = Ruido(p, max(mpp * 2.0, 0.2), 9u);
                float mota2 = Ruido(p, max(mpp * 6.0, 0.6), 19u);
                half3 c = TAN;                                                   // tierra apisonada
                if (uso == U_SECANO)
                {
                    c = mota > 0.55 ? BEIGE : TAN;
                    if (mota2 > 0.72) c = OLIVE_GREY;
                }
                else if (uso == U_CASERIO)
                {
                    float motaG = Ruido(p, max(mpp * 2.0, 1.2), 29u);           // manchas de metro, no sal y pimienta
                    c = motaG > 0.74 ? BEIGE : TAN;
                    if (motaG < 0.2 && bayer < 0.5) c = OCHRE_DARK;
                }
                else if (uso == U_HUERTA)
                {
                    c = mota > 0.62 ? OCHRE_DARK : TAN;
                    float col = floor(p.x / 34.0);
                    float desf = Hash(int2((int)col, 0), 41u) * 46.0;          // cada franja de parcelas, con su desfase
                    float fila = floor((p.y + desf) / 46.0);
                    int parcela = (int)(Hash(int2((int)col, (int)fila), 43u) * 6.999);   // 0–1 labrada, 2 verde, 3–6 barbecho
                    if (parcela <= 1) c = (fmod(floor(p.y / 2.4), 2.0) < 0.5) ? OCHRE_DARK : BROWN;
                    else if (parcela == 2) c = mota > 0.6 ? OLIVE : OLIVE_DARK;
                    bool linde = (col != floor((p.x - mpp) / 34.0)) || (fila != floor((p.y + desf - mpp) / 46.0));
                    if (linde) c = OLIVE_GREY;
                }
                else if (uso == U_NARANJAL)
                {
                    c = OCHRE_DARK;                                              // hileras de este a oeste, marco de 5 × 4 m
                    if (_NaranjosPintados > 0.5)
                    {
                        float cu = fmod(p.x, 5.0) - 2.5, cv = fmod(p.y, 4.0) - 2.0;
                        bool copa = length(float2(cu, cv)) < 1.3;
                        bool sombra = !copa && length(float2(cu - 0.6, cv - 0.6)) < 1.3;
                        if (sombra) c = OLIVE_SHADOW;
                        if (copa) c = length(float2(cu + 0.4, cv + 0.4)) < 0.6 ? OLIVE_LIGHT : OLIVE;
                    }
                    else if (fmod(p.y, 4.0) < 1.0) c = BROWN;                   // sin copas pintadas: el surco de la hilera
                }
                else if (uso == U_MONTE)
                {
                    c = GREY_BROWN;
                    if (mota2 > 0.5) c = OLIVE_SHADOW;
                    if (mota > 0.7) c = OLIVE;
                }
                else if (uso == U_MARJAL)
                {
                    c = OLIVE;
                    if (mota2 > 0.55) c = OLIVE_GREY;
                    if (mota2 < 0.22) c = SLATE;
                }
                else if (uso == U_CAUCE)
                {
                    c = mota > 0.6 ? CONCRETE : OLIVE_GREY;                      // lecho de grava
                    if (bordeCauce) c = CONCRETE_LIGHT;
                }
                else if (uso == U_CARRETERA)
                {
                    // La franja de usos de la carretera mide unos 11 m; el asfalto de Álvaro, 7 m. Los 2 m de cada borde son arcén de tierra
                    // (para que case con la losa de 7 m del paso a nivel de Teo, en 6c7874). Motas 94948c en manchas de metro.
                    float motaG = Ruido(p, max(mpp * 2.0, 1.0), 37u);
                    c = motaG > 0.7 ? CONCRETE_LIGHT : CONCRETE;
                    if (cuneta) c = GREY_BROWN;
                }
                else if (uso == U_EXPLANADA)
                {
                    float motaG = Ruido(p, max(mpp * 2.0, 1.2), 33u);           // grava en manchas de metro (Mundo6: a 0,2 m salía sal y pimienta)
                    c = motaG > 0.68 ? CONCRETE : GREY_BROWN;
                }
                else if (uso == U_PLAYA)
                {
                    c = BEIGE;
                }
                else if (uso == U_MAR)
                {
                    c = mota > 0.75 ? CONCRETE : SLATE;
                }
                // SOLDADO: grietas de las texturas de cerca
                if (_ZumAltura < 40.0 && uso != U_HUERTA && uso != U_NARANJAL && abs(Ruido(p, 3.0, 21u) - 0.5) < 0.0025) c = BROWN;
                if (uso < 0) c = OLIVE_BLACK;                                    // fuera del mundo
                return c;
            }

            // Plano táctico (MAPA, D-N113): bandas de altura, curvas cada 5 m y maestras cada 25, sin luz (es un dibujo, no una vista).
            half3 Mapa(int uso, float h, float3 n, float mpp, float bayer, bool costa, bool bordeMundo)
            {
                int banda = h < 15.0 ? 0 : (h < 45.0 ? 1 : 2);
                half3 c = banda == 0 ? OLIVE_SHADOW : (banda == 1 ? GREY_BROWN : OLIVE_GREY);
                bool luz = (-n.x + n.z) > 0.12;                                  // ladera al noroeste: un escalón más clara, tramada al 50 %
                if (luz && bayer < 0.5) c = banda == 0 ? GREY_BROWN : (banda == 1 ? OLIVE_GREY : c);
                if (uso == U_NARANJAL && bayer < 0.35) c = OLIVE_DARK;
                if (uso == U_MARJAL && bayer < 0.35) c = SLATE;
                float fw = max(fwidth(h), 1e-4);
                if (h > 0.5 && uso != U_CAUCE)
                {
                    if (frac(h / 5.0) < fw / 5.0) c = OLIVE_DARK;
                    if (frac(h / 25.0) < fw / 25.0) c = CONCRETE;
                }
                if (uso == U_CAUCE) c = SLATE_DARK;
                if (uso == U_CARRETERA) c = CONCRETE;
                if (uso == U_EXPLANADA) c = GREY_BROWN;
                if (uso == U_MAR) c = costa ? CONCRETE_LIGHT : NAVY;
                if (uso < 0) c = bordeMundo ? CONCRETE : OLIVE_BLACK;
                return c;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 xz = i.positionWS.xz;
                float2 p = float2(xz.x, _Lado - xz.y);
                float mpp = max(length(fwidth(xz)), 0.005);                     // metros por píxel de la RT

                uint2 sp = (uint2)floor(i.positionCS.xy) & 3u;
                float bayer = BAYER4[sp.y * 4 + sp.x] / 16.0 + 1.0 / 32.0;

                // ¿mapa o día? (fundido con tramado en píxeles de la RT)
                float t = saturate((_ZumAltura - _UmbralMapa * (1.0 - _Fundido)) / (_UmbralMapa * 2.0 * _Fundido));
                bool mapa = bayer < t;

                if (mapa)
                {
                    int uso = UsoEn(xz);
                    bool costa = uso == U_MAR && UsoEn(xz - float2(mpp, 0)) != U_MAR && UsoEn(xz - float2(mpp, 0)) >= 0;
                    bool bordeMundo = uso < 0 && (UsoEn(xz + float2(mpp, 0)) >= 0 || UsoEn(xz - float2(mpp, 0)) >= 0
                                                  || UsoEn(xz + float2(0, mpp)) >= 0 || UsoEn(xz - float2(0, mpp)) >= 0);
                    return half4(Mapa(uso, i.positionWS.y, normalize(i.normalWS), mpp, bayer, costa, bordeMundo), 1);
                }

                // Día: de cerca (menos de 0,5 m por píxel) el borde entre usos se rompe con ruido de ±1,2 m, como en las láminas de pueblo y soldado
                float2 q = xz;
                if (mpp < 0.5)
                    q += 1.2 * (float2(Ruido(p, 2.5, 31u), Ruido(p, 2.5, 32u)) - 0.5) * 2.0;
                int usoD = UsoEn(q);
                bool bordeCauce = usoD == U_CAUCE && (UsoEn(q + float2(0, 2)) != U_CAUCE || UsoEn(q - float2(0, 2)) != U_CAUCE);
                // arcén: los 2 m de cada borde de la franja de la carretera (la franja va casi de norte a sur: se mira en X)
                bool cuneta = usoD == U_CARRETERA && (UsoEn(q + float2(2, 0)) != U_CARRETERA || UsoEn(q - float2(2, 0)) != U_CARRETERA);
                half3 albedo = Dia(usoD, p, mpp, bayer, bordeCauce, cuneta);

                float3 n = normalize(i.normalWS);
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light L = GetMainLight(sc);
                half ndl = saturate(dot(n, L.direction));
                half3 luz = L.color * (ndl * L.shadowAttenuation) + SampleSH(n);
                return half4(albedo * lerp(half3(1, 1, 1), luz, _Relieve), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertSombra
            #pragma fragment fragVacio
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };

            V vertSombra(A v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float3 nws = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - ws);
                #else
                    float3 ld = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(ws, nws, ld));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs;
                return o;
            }
            half4 fragVacio(V i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertProf
            #pragma fragment fragProf
            #pragma multi_compile_instancing

            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };
            V vertProf(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half fragProf(V i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
    Fallback Off
}
