// =============================================================================
//  OcclusionBubble.shader  -  cascaron oscuro que oculta todo mas alla del radio
// =============================================================================
//  Se dibuja por DENTRO (Cull Front) porque la camara esta dentro de la esfera.
//  Escribe profundidad, asi que todo lo que quede mas alla falla el ZTest y no
//  se dibuja: eso incluye el skybox, que es lo que un oscurecimiento hecho en el
//  shader del terreno no podria tapar.
//
//  Va en Queue Geometry-1 a proposito: al dibujarse ANTES del terreno, deja la
//  profundidad puesta y el terreno lejano se descarta por ZTest en vez de
//  sombrearse para nada.
//
//  _BubbleAmount es GLOBAL (Shader.SetGlobalFloat desde CameraOcclusionBubble.cs)
//  y controla la aparicion por dither, para no tener que pasar el cascaron a
//  cola transparente solo para poder desvanecerlo.
// =============================================================================

Shader "Voxels/OcclusionBubble"
{
    Properties
    {
        _Color    ("Color base (abajo)", Color) = (0.020, 0.028, 0.040, 1)
        _RimColor ("Color arriba",       Color) = (0.055, 0.075, 0.105, 1)
        _Gradient ("Fuerza del degradado", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry-1"
        }

        Pass
        {
            Name "Bubble"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front       // la camara esta dentro: se ve la cara interna
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _RimColor;
                float  _Gradient;
            CBUFFER_END

            // GLOBAL (fuera del CBUFFER): lo publica CameraOcclusionBubble.cs
            float _BubbleAmount;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 dirOS       : TEXCOORD0;
            };

            // Interleaved Gradient Noise, igual que en SphereCutout.hlsl.
            // Se repite aqui a proposito para no acoplar los dos archivos.
            float OB_IGN(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dirOS       = normalize(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Aparicion por dither. Con _BubbleAmount = 0 se descarta todo.
                // positionHCS.xy en el fragment ya son coordenadas de pixel.
                clip(_BubbleAmount - OB_IGN(IN.positionHCS.xy));

                // Degradado vertical suave para que no lea como pantalla en negro.
                float t = saturate(IN.dirOS.y * 0.5 + 0.5) * _Gradient;
                return half4(lerp(_Color.rgb, _RimColor.rgb, t), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
