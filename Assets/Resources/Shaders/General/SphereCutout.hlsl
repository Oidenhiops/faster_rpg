// =============================================================================
//  SphereCutout.hlsl  -  Custom Function para VoxelCutout.shadergraph (URP Lit)
// =============================================================================
//  OJO: este archivo NO va en GeneralMaterial.shadergraph. Ese material lo usan
//  los personajes y props (42 referencias en Test.unity). El terreno usa
//  VoxelMaterial.mat, que hoy esta en URP/Lit de fabrica y es el que vamos a
//  reemplazar por un Shader Graph propio.
// =============================================================================
//  Recorte esferico con dither (screen-door) alrededor del jugador, para que la
//  camara pueda quedarse QUIETA en vez de alejarse cuando hay un obstaculo.
//
//  Clave anti x-ray: la puerta de profundidad. Solo se atenua lo que esta
//  CLARAMENTE mas cerca de la camara que el jugador. Nada que este mas alla del
//  jugador se toca nunca, asi que ver "a traves" del mundo es imposible por
//  construccion, no por ajuste de parametros.
//
//  Clave anti "agujero al skybox": nunca se llega a alpha 0. Queda un porcentaje
//  de pixeles vivos (_CutoutParams.z) => se ve el fantasma de la pared en lugar
//  de un hueco. Esto importa porque VoxelMesher.cs no genera caras interiores
//  (linea ~189: "vecino solido: oculta"), asi que detras de un bloque cortado no
//  hay geometria que dibujar.
//
//  Los globales los publica CameraSphereCutout.cs con Shader.SetGlobalX.
//  Van FUERA de UnityPerMaterial a proposito (mismo patron que usa
//  CharacterOutline.shader con _DissolveAmount).
// =============================================================================

#ifndef SPHERE_CUTOUT_INCLUDED
#define SPHERE_CUTOUT_INCLUDED

float4 _CutoutSphere;   // xyz = centro en mundo, w = radio (0 = apagado)
float4 _CutoutParams;   // x = dist camara->jugador, y = feather, z = alphaMin, w = bias
float  _CutoutEnabled;  // 0 / 1

// Interleaved Gradient Noise (Jimenez, SIGGRAPH 2014).
// Estable en espacio de pantalla, distribucion tipo blue-noise, sin arrays.
float SC_IGN(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
}

void SphereCutout_float(float3 PositionWS, float2 ScreenUV, out float Mask)
{
    Mask = 1.0;

    // El ShadowCaster NO se recorta: la pared sigue proyectando su sombra, que
    // es lo que hace que el efecto lea como "ventana" y no como "se borro".
    // Ademas en ese pase _WorldSpaceCameraPos es la de la luz, no la del jugador.
#if defined(SHADERPASS) && defined(SHADERPASS_SHADOWCASTER)
    #if (SHADERPASS == SHADERPASS_SHADOWCASTER)
        return;
    #endif
#endif

    if (_CutoutEnabled < 0.5) return;

    float radius = _CutoutSphere.w;
    if (radius <= 0.0) return;

    float dCam    = distance(PositionWS, _WorldSpaceCameraPos);
    float playerD = _CutoutParams.x;
    float feather = max(_CutoutParams.y, 1e-4);
    float bias    = _CutoutParams.w;

    // Puerta de profundidad. gate = 1 si el fragmento esta bien delante del
    // jugador, 0 si esta a su altura o mas alla.
    // El bias es lo que evita que se agujeree el suelo a los pies del jugador
    // (ese suelo esta casi a la misma distancia que el jugador).
    float gate = saturate(((playerD - bias) - dCam) / feather);

    // Mascara radial, suave en el borde para que el circulo no tenga alias.
    float d      = distance(PositionWS, _CutoutSphere.xyz);
    float radial = 1.0 - smoothstep(radius * 0.55, radius, d);

    float fade  = saturate(radial * gate);            // 1 = recorte maximo
    float alpha = lerp(1.0, _CutoutParams.z, fade);   // nunca 0 -> queda fantasma

    // Salida BINARIA a proposito. Aguas abajo esto se multiplica por el alpha del
    // atlas y se compara contra _Cutoff (0.5). Si devolvieramos un gradiente, las
    // hojas de las plantas (alpha de atlas intermedio) se recortarian mal.
    float threshold = SC_IGN(ScreenUV * _ScreenParams.xy);
    Mask = (alpha >= threshold) ? 1.0 : 0.0;
}

// Variante half por si el grafo esta en precision Half; delega en la float.
void SphereCutout_half(half3 PositionWS, half2 ScreenUV, out half Mask)
{
    float m;
    SphereCutout_float((float3)PositionWS, (float2)ScreenUV, m);
    Mask = (half)m;
}

#endif // SPHERE_CUTOUT_INCLUDED
