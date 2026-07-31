/// <summary>
/// Efectos continuos que se pueden acumular sobre un rango de letras.
/// </summary>
public enum TextFxType
{
    /// <summary>Ondulacion vertical tipo seno, desfasada por letra.</summary>
    Wave,
    /// <summary>Vibracion aleatoria en X/Y (ruido puro, sirve para dano / rabia).</summary>
    Shake,
    /// <summary>Movimiento suave y organico en circulos (perlin).</summary>
    Wiggle,
    /// <summary>Saltos verticales, la letra rebota hacia arriba.</summary>
    Bounce,
    /// <summary>Escala pulsante (respiracion).</summary>
    Pulse,
    /// <summary>Rotacion oscilante sobre el centro de la letra.</summary>
    Swing,
    /// <summary>Ciclo de matiz por letra (arcoiris animado).</summary>
    Rainbow,
    /// <summary>Alpha pulsante (parpadeo suave).</summary>
    Blink,

    /// <summary>
    /// Contorno alrededor de las letras. OJO: es una propiedad del material,
    /// no de los vertices, asi que aplica a TODO el label y se ignora el rango.
    /// Usa TextFxInstance.color para el color y .amplitude para el grosor (0..1).
    /// </summary>
    Outline,
}

/// <summary>
/// Animacion con la que cada letra "entra" cuando el typewriter la revela.
/// </summary>
public enum TextEntranceType
{
    /// <summary>Aparece de golpe.</summary>
    None,
    /// <summary>Alpha 0 -> 1.</summary>
    FadeIn,
    /// <summary>Crece desde 0 con fade.</summary>
    ScaleUp,
    /// <summary>Crece pasandose de tamano y vuelve (overshoot).</summary>
    Pop,
    /// <summary>Entra desde abajo.</summary>
    SlideUp,
    /// <summary>Entra desde arriba.</summary>
    SlideDown,
    /// <summary>Entra desde la izquierda.</summary>
    SlideLeft,
    /// <summary>Entra desde la derecha.</summary>
    SlideRight,
    /// <summary>Gira sobre si misma mientras crece.</summary>
    Spin,
}
