// Contorno de 1 px del fantasma de obra (Lucía, 9-nov). Bruno. SIN COMPILAR: escrito en la nube, sin Unity.
// Se dibuja en la RT del mundo (el pixelado final), así que 1 px aquí es 1 px del arte.
// Va como materiales EXTRA en el MeshRenderer del fantasma (más materiales que submallas: Unity repite la última submalla con cada uno):
//   1) Máscara (cola 2008): la silueta entera, sin tramado, escribe 1 en el stencil y no pinta color.
//   2) Contorno (cola 2009): la misma caja inflada 1 px de pantalla en sus tres ejes locales; pinta 283440 donde el stencil NO es 1.
//   3) El fantasma de siempre (TBD/Fantasma, cola 2010) pinta su tramado encima; por los huecos del damero se sigue viendo el mundo, no el contorno.
// Inflar la caja d en cada eje local contiene la caja más una esfera de radio d, y en ortográfica la esfera se proyecta en un disco de radio d:
// la silueta crece al menos 1 px por todos lados (también en las esquinas). Vale para cajas (WallVisual / Box); otras mallas, si su origen es el centro.
// Color: slate oscuro 283440 (L* 21): ΔL* 63 contra la cal de día d4d0cc, 46 contra la arena b4a080, 10 contra la horda 1c200c.
Shader "TBD/FantasmaContorno"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.0222, 0.0336, 0.0513, 1)   // 283440 en lineal
        _Inflar ("Inflado en px de la RT (0 = máscara)", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 6     // NotEqual
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Op", Float) = 0               // Keep
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+9" }
        Pass
        {
            Name "FantasmaContorno"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            ColorMask [_ColorMask]
            Stencil { Ref 1 Comp [_StencilComp] Pass [_StencilOp] }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Inflar;
                float _StencilComp, _StencilOp, _ColorMask;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };

            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v);
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                if (_Inflar > 0)
                {
                    float4 cs = TransformWorldToHClip(ws);
                    // tamaño de 1 px de la RT en metros a esa profundidad (ortográfica: w = 1; en perspectiva también vale, con w)
                    float px = 2.0 * cs.w / (_ScreenParams.y * abs(UNITY_MATRIX_P[1][1]));
                    float d = _Inflar * px;
                    float3 ax = normalize(UNITY_MATRIX_M._m00_m10_m20), ay = normalize(UNITY_MATRIX_M._m01_m11_m21), az = normalize(UNITY_MATRIX_M._m02_m12_m22);
                    float3 s = sign(v.positionOS.xyz);
                    ws += (s.x * ax + s.y * ay + s.z * az) * d;
                }
                o.positionCS = TransformWorldToHClip(ws);
                return o;
            }

            half4 frag(V i) : SV_Target { return half4(_BaseColor.rgb, 1); }
            ENDHLSL
        }
    }
    Fallback Off
}
