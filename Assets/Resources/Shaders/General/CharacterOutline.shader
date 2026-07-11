// Outline 3D por "inverted hull" para personajes (URP).
// Uso previsto: como material de OVERRIDE en un Render Objects Renderer Feature
// (no se anade ningun material al Renderer del personaje).
// Dissolve sincronizado: replica exactamente la logica de CharacterLit.shadergraph
//   visible cuando  (_DissolveAmount + texAlpha * SimpleNoise(uv0,_DissolveScale)) <= _DissolveEdge
// _DissolveAmount es GLOBAL (Shader.SetGlobalFloat) porque el material de override no
// esta en renderer.materials[]; Dissolve.cs lo publica con una linea extra.
Shader "Character/CharacterOutline"
{
    Properties
    {
        [HDR] _OutlineColor   ("Outline Color", Color)        = (0, 0.6497, 1, 1)
        _OutlineWidth         ("Outline Width (obj space)", Range(0, 0.2)) = 0.03
        _BaseTexture          ("Base Texture", 2D)            = "white" {}
        _DissolveScale        ("Dissolve Scale", Float)       = 100
        _DissolveEdge         ("Dissolve Edge (umbral del graph)", Range(0,1)) = 0.36
        [Toggle] _UseTextureAlpha ("Modular por alpha de textura", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"       = "Opaque"
            "RenderPipeline"   = "UniversalPipeline"
            "Queue"            = "Geometry+1"
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front      // clave del inverted hull: solo se ve el cascaron por detras del cuerpo
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseTexture);
            SAMPLER(sampler_BaseTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
                float4 _BaseTexture_ST;
                float  _DissolveScale;
                float  _DissolveEdge;
                float  _UseTextureAlpha;
            CBUFFER_END

            // GLOBAL (fuera del CBUFFER): lo publica Dissolve.cs con Shader.SetGlobalFloat.
            float _DissolveAmount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            // ---- Simple Noise de ShaderGraph (value noise, 3 octavas). Identico al del graph. ----
            float unity_noise_randomValue(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
            }
            float unity_noise_interpolate(float a, float b, float t)
            {
                return (1.0 - t) * a + t * b;
            }
            float unity_valueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);
                float r0 = unity_noise_randomValue(i + float2(0.0, 0.0));
                float r1 = unity_noise_randomValue(i + float2(1.0, 0.0));
                float r2 = unity_noise_randomValue(i + float2(0.0, 1.0));
                float r3 = unity_noise_randomValue(i + float2(1.0, 1.0));
                float bottom = unity_noise_interpolate(r0, r1, f.x);
                float top    = unity_noise_interpolate(r2, r3, f.x);
                return unity_noise_interpolate(bottom, top, f.y);
            }
            float SimpleNoise(float2 UV, float Scale)
            {
                float t = 0.0;
                float freq, amp;
                freq = pow(2.0, 0.0); amp = pow(0.5, 3.0 - 0.0);
                t += unity_valueNoise(float2(UV.x * Scale / freq, UV.y * Scale / freq)) * amp;
                freq = pow(2.0, 1.0); amp = pow(0.5, 3.0 - 1.0);
                t += unity_valueNoise(float2(UV.x * Scale / freq, UV.y * Scale / freq)) * amp;
                freq = pow(2.0, 2.0); amp = pow(0.5, 3.0 - 2.0);
                t += unity_valueNoise(float2(UV.x * Scale / freq, UV.y * Scale / freq)) * amp;
                return t;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // Inverted hull: inflamos la malla a lo largo de la normal en espacio objeto.
                float3 posOS = IN.positionOS.xyz + normalize(IN.normalOS) * _OutlineWidth;
                OUT.positionHCS = TransformObjectToHClip(posOS);
                OUT.uv = IN.uv; // uv0 cruda, igual que el Simple Noise del graph
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Mismo criterio que CharacterLit (ya en 3D, sin textura):
                //   visible  <=>  SimpleNoise(uv0, _DissolveScale) >= _DissolveAmount
                float noise = SimpleNoise(IN.uv, _DissolveScale);
                clip(noise - _DissolveAmount);

                return _OutlineColor;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
