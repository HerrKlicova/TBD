// Fantasma de obra (Lucía, 8-nov): un solo color plano tramado en pantalla, sin luz.
// Válido: hueso d4d0cc al 50 % (damero de píxeles). No válido: ámbar e08c30. Nunca rojo ni 442c0c (es de la horda).
// El tramado es por píxel de pantalla, así que en el RT del mundo (1/3 de resolución) sale un damero exacto de 1 px de mundo.
// _Paso: 0 = damero al 50 %; 1 = tramado abierto al 25 % (un píxel de cada 2x2), por si el 50 % se confunde con la selección.
// SIN COMPILAR: escrito en la nube, sin Unity.
Shader "TBD/Fantasma"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.658, 0.630, 0.604, 1)   // d4d0cc en lineal
        _Paso ("Tramado (0 = 50 %, 1 = 25 %)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }
        Pass
        {
            Name "Fantasma"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Paso;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };

            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }

            half4 frag(V i) : SV_Target
            {
                int2 p = (int2)floor(i.positionCS.xy);
                bool pinta = _Paso < 0.5 ? ((p.x + p.y) & 1) == 0 : ((p.x & 1) == 0 && (p.y & 1) == 0);
                clip(pinta ? 1 : -1);
                return half4(_BaseColor.rgb, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
