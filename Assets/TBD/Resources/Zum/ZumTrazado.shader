// Estética del zum · trazado de vía con vista previa (lámina 5). Bruno (Arte técnico), 11-11.
// Para la malla de la vista previa que monte Iker (una cinta sobre el terreno, uv.x = metros a lo largo del eje):
// - Válido: hueso d4d0cc, banda con el ancho del balasto tramada al 55 % (_Tramado = 0.55) y el eje en guiones (otra cinta fina con _Guion = 6).
// - Inválido: ámbar e08c30 lleno (_Tramado = 1) de 6 m, con borde 0f120c (una cinta 2 px más ancha debajo, en _Color = 0f120c).
// - Confirmado, mientras la cuadrilla lo tiende: hueso tramado al 30 % (_Tramado = 0.3).
// El tramado es Bayer 4×4 en píxeles de la RT (alfa binaria). Sin luz. Se dibuja encima del suelo (Offset) y no proyecta sombra.
// ZumEstetica.MaterialTrazado(color, tramado, guion) lo prepara. SIN COMPILAR: escrito en la nube, sin Unity.
Shader "TBD/ZumTrazado"
{
    Properties
    {
        _Color ("Color", Color) = (0.831, 0.816, 0.800, 1)   // d4d0cc (sRGB, Unity lo pasa a lineal)
        _Tramado ("Tramado (1 = lleno)", Range(0, 1)) = 1
        _Guion ("Largo de guion en m (0 = continuo)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest+10" }
        Pass
        {
            Name "Trazado"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Offset -2, -2

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Tramado;
                float _Guion;
            CBUFFER_END

            static const float BAYER4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            V vert(A v)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                if (_Guion > 0 && fmod(floor(i.uv.x / _Guion), 2.0) > 0.5) discard;
                uint2 sp = (uint2)floor(i.positionCS.xy) & 3u;
                if (BAYER4[sp.y * 4 + sp.x] / 16.0 + 1.0 / 32.0 >= _Tramado) discard;
                return half4(_Color.rgb, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
