// Agua estilizada para el mundo voxel: color por profundidad, espuma en orillas,
// destellos pixelados y fresnel. URP, transparente, sin iluminación (estilizado).
// Requiere Depth Texture activado en el URP Asset.
Shader "FasterRPG/StylizedWater"
{
    Properties
    {
        _ShallowColor ("Agua poco profunda", Color) = (0.25, 0.75, 0.85, 0.55)
        _DeepColor ("Agua profunda", Color) = (0.05, 0.25, 0.60, 0.9)
        _DepthDistance ("Distancia de profundidad (m)", Float) = 3
        _FoamColor ("Espuma", Color) = (1, 1, 1, 0.85)
        _FoamDistance ("Grosor de espuma (m)", Float) = 0.45
        _PixelsPerMeter ("Pixeles por metro", Float) = 8
        _NoiseScale ("Escala del ruido", Float) = 0.9
        _WaveSpeed ("Velocidad", Float) = 0.8
        _SparkleStrength ("Destellos", Range(0, 1)) = 0.12
        _FresnelPower ("Fresnel (potencia)", Float) = 3
        _FresnelStrength ("Fresnel (intensidad)", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "StylizedWater"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
            half4 _ShallowColor, _DeepColor, _FoamColor;
            float _DepthDistance, _FoamDistance, _PixelsPerMeter, _NoiseScale;
            float _WaveSpeed, _SparkleStrength, _FresnelPower, _FresnelStrength;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // ---- profundidad VERTICAL del agua (estable con la cámara) ----
                // reconstruye la posición de mundo del fondo y compara alturas
                float2 screenUV = i.positionCS.xy / _ScaledScreenParams.xy;
                float rawDepth = SampleSceneDepth(screenUV);

                #if UNITY_REVERSED_Z
                bool noDepth = rawDepth <= 0.0001; // cielo / sin profundidad escrita
                #else
                bool noDepth = rawDepth >= 0.9999;
                #endif

                float3 sceneWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                float vertical = i.positionWS.y - sceneWS.y;
                // sin profundidad, o algo opaco POR ENCIMA del agua (player, orilla):
                // tratar como agua profunda para no pintar espuma falsa
                float depthDiff = (noDepth || vertical < 0.0) ? _DepthDistance : vertical;

                // ---- color por profundidad ----
                float depthT = saturate(depthDiff / _DepthDistance);
                half4 col = lerp(_ShallowColor, _DeepColor, depthT);

                // ---- ruido pixelado en espacio de mundo (continuo entre chunks) ----
                float2 cell = floor(i.positionWS.xz * _PixelsPerMeter) / _PixelsPerMeter;
                float t = _Time.y * _WaveSpeed;
                float n0 = hash21(cell * _NoiseScale + floor(t));
                float n1 = hash21(cell * _NoiseScale + floor(t) + 1.0);
                float n = lerp(n0, n1, smoothstep(0.0, 1.0, frac(t)));
                n = floor(n * 3.0) / 3.0; // posterizado a 3 tonos: pixel-art

                // ---- destellos sutiles ----
                col.rgb += n * _SparkleStrength;

                // ---- espuma en orillas: borde por profundidad, roto por el ruido ----
                float foamEdge = _FoamDistance * (0.6 + 0.8 * n);
                float foam = step(depthDiff, foamEdge);
                col.rgb = lerp(col.rgb, _FoamColor.rgb, foam * _FoamColor.a);
                col.a = max(col.a, foam * _FoamColor.a);

                // ---- fresnel: brillo y opacidad extra en ángulos rasantes ----
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS);
                float fres = pow(1.0 - saturate(dot(viewDir, normalize(i.normalWS))), _FresnelPower);
                col.rgb += fres * _FresnelStrength;
                col.a = saturate(col.a + fres * 0.2);

                return col;
            }
            ENDHLSL
        }
    }
}
