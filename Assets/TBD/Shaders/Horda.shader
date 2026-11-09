// D-N80 · Horda: un solo material para todas las siluetas de Teo. Bruno (Arte técnico), 8-nov.
// - Máscara en el COLOR DE VÉRTICE (canales puros): R = camisa, G = pantalón, B = piel/pelo; negro = zapatos/suciedad (#1c200c).
//   Alfa del color de vértice = semilla por cuerpo (255 = ninguna): varía el tono dentro de una misma malla (pila de zombis).
// - Tono por instancia desde horda_paleta_lut.png (8x3, Point): fila 0 camisas (8), fila 1 pantalones (6), fila 2 piel (4).
//   Todos en L* 9–13 (la horda es lo más oscuro de la escena, D-N61); la variedad va en matiz, nunca en valor.
// - _HordaPack por instancia (Entities Graphics, componente ZombiePaleta): x camisa 0–7, y pantalón 0–5, z piel 0–3, w sangre 0–15.
// - Sangre: rampa 16x1 (sangre_rampa.png), se pinta si nivel > umbral Bayer 4x4 en espacio de mundo (sin alfa parcial).
// - Luz: Lambert del sol/luna con sombras + ambiente SH. Sin especular (mate, como el resto). SRP Batcher y DOTS instancing.
// SIN COMPILAR: escrito en la nube, sin Unity. Lo tiene que abrir Marcos o Teo y mirar la consola.
Shader "TBD/Horda"
{
    Properties
    {
        [NoScaleOffset] _HordaLut ("LUT de la horda (8x3, Point)", 2D) = "black" {}
        [NoScaleOffset] _SangreRampa ("Rampa de sangre (16x1, Point)", 2D) = "black" {}
        _HordaPack ("Camisa, pantalón, piel, sangre", Vector) = (0, 0, 0, 0)
        _Resto ("Zapatos y suciedad", Color) = (0.0110, 0.0140, 0.0060, 1)   // #1c200c en lineal
        _BayerCeldas ("Celdas Bayer por metro", Float) = 8
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _HordaPack;
            float4 _Resto;
            float _BayerCeldas;
        CBUFFER_END

        #ifdef UNITY_DOTS_INSTANCING_ENABLED
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP(float4, _HordaPack)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
            #define _HordaPack UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _HordaPack)
        #endif
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_HordaLut);    SAMPLER(sampler_point_clamp);
            TEXTURE2D(_SangreRampa);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 color      : COLOR;
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
                o.color = v.color;
                return o;
            }

            // Fila r de la LUT (0 = arriba en el PNG). En Unity la v crece hacia arriba: v = 1 - (r + 0.5) / 3.
            half3 Lut(float idx, float fila)
            {
                float2 uv = float2((idx + 0.5) / 8.0, 1.0 - (fila + 0.5) / 3.0);
                return SAMPLE_TEXTURE2D_LOD(_HordaLut, sampler_point_clamp, uv, 0).rgb;
            }

            static const float BAYER4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 pack = _HordaPack;
                // Semilla por cuerpo en el alfa del color de vértice (Teo, pila de zombis contra el muro): 255 = sin cambio (zombi suelto);
                // cualquier otro valor desplaza camisa, pantalón y piel dentro de la misma instancia, para que cada cuerpo de la pila sea distinto.
                // OJO: el desplazamiento no aplica la regla de ΔE < 4 de la mezcla; en una pila de cuerpos revueltos no se nota.
                float semilla = 255.0 - round(i.color.a * 255.0);
                if (semilla > 0.5)
                {
                    pack.x = fmod(pack.x + semilla * 3.0, 8.0);
                    pack.y = fmod(pack.y + semilla * 5.0, 6.0);
                    pack.z = fmod(pack.z + semilla, 4.0);
                }
                // La máscara es de canales puros; el mayor gana (el FBX puede traer interpolación en las costuras).
                float3 m = i.color.rgb;
                half3 albedo = _Resto.rgb;
                float mx = max(m.r, max(m.g, m.b));
                if (mx > 0.5)
                {
                    if (m.r >= mx)      albedo = Lut(pack.x, 0);
                    else if (m.g >= mx) albedo = Lut(pack.y, 1);
                    else                albedo = Lut(pack.z, 2);
                }
                // Sangre acumulada con tramado en mundo (D-N80): nunca alfa parcial.
                float nivel = floor(pack.w);
                if (nivel > 0)
                {
                    int2 c = (int2)floor(i.positionWS.xz * _BayerCeldas) & 3;
                    if (nivel > BAYER4[c.y * 4 + c.x])
                        albedo = SAMPLE_TEXTURE2D_LOD(_SangreRampa, sampler_point_clamp, float2((nivel + 0.5) / 16.0, 0.5), 0).rgb;
                }

                float3 n = normalize(i.normalWS);
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light L = GetMainLight(sc);
                half ndl = saturate(dot(n, L.direction));
                half3 luz = L.color * (ndl * L.shadowAttenuation) + SampleSH(n);
                return half4(albedo * luz, 1);
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };
            V vertProf(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half fragProf(V i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
    Fallback Off
}
