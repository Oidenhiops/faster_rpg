using System;
using UnityEngine;

/// <summary>
/// Un efecto activo aplicado a un rango de letras.
/// Se puede crear desde el inspector o por codigo con los helpers estaticos.
/// </summary>
[Serializable]
public class TextFxInstance
{
    public TextFxType type = TextFxType.Wave;

    [Tooltip("Primer caracter afectado. -1 = desde el inicio.")]
    public int from = -1;

    [Tooltip("Ultimo caracter afectado (inclusive). -1 = hasta el final.")]
    public int to = -1;

    [Tooltip("Intensidad del movimiento en unidades locales de TMP (o factor de escala segun el efecto).")]
    public float amplitude = 8f;

    [Tooltip("Que tan rapido oscila.")]
    public float speed = 6f;

    [Tooltip("Desfase entre letras contiguas. 0 = todas se mueven igual.")]
    public float frequency = 0.35f;

    [Tooltip("Color del efecto. Solo lo usan los efectos que lo necesitan (Outline).")]
    public Color color = Color.black;

    /// <summary>Identificador opcional para poder quitar el efecto luego con RemoveEffect(id).</summary>
    [HideInInspector] public string id;

    /// <summary>True si esta letra entra en el rango del efecto.</summary>
    public bool Covers(int charIndex, int totalChars)
    {
        int a = from < 0 ? 0 : from;
        int b = to < 0 ? totalChars - 1 : to;
        return charIndex >= a && charIndex <= b;
    }

    public TextFxInstance Clone()
    {
        return new TextFxInstance
        {
            type = type,
            from = from,
            to = to,
            amplitude = amplitude,
            speed = speed,
            frequency = frequency,
            color = color,
            id = id,
        };
    }

    /// <summary>Valores por defecto sensatos para cada tipo de efecto.</summary>
    public static TextFxInstance Default(TextFxType type, int from = -1, int to = -1)
    {
        TextFxInstance fx = new TextFxInstance { type = type, from = from, to = to };

        switch (type)
        {
            case TextFxType.Wave:
                fx.amplitude = 8f; fx.speed = 6f; fx.frequency = 0.35f; break;
            case TextFxType.Shake:
                fx.amplitude = 3f; fx.speed = 40f; fx.frequency = 0f; break;
            case TextFxType.Wiggle:
                fx.amplitude = 5f; fx.speed = 3f; fx.frequency = 0.5f; break;
            case TextFxType.Bounce:
                fx.amplitude = 14f; fx.speed = 5f; fx.frequency = 0.5f; break;
            case TextFxType.Pulse:
                fx.amplitude = 0.15f; fx.speed = 5f; fx.frequency = 0.3f; break;
            case TextFxType.Swing:
                fx.amplitude = 12f; fx.speed = 5f; fx.frequency = 0.4f; break;
            case TextFxType.Rainbow:
                fx.amplitude = 1f; fx.speed = 0.6f; fx.frequency = 0.08f; break;
            case TextFxType.Blink:
                fx.amplitude = 0.6f; fx.speed = 6f; fx.frequency = 0f; break;
            case TextFxType.Outline:
                // amplitude = grosor (0..1). Pasar de ~0.3 recorta si el font asset tiene poco padding.
                fx.amplitude = 0.2f; fx.speed = 0f; fx.frequency = 0f; fx.color = Color.black; break;
        }

        return fx;
    }
}

/// <summary>Color asignado a un rango concreto de letras.</summary>
[Serializable]
public class TextColorRange
{
    public int from;
    public int to;
    public Color colorA = Color.white;
    public Color colorB = Color.white;

    /// <summary>Si es true, interpola de colorA a colorB a lo largo del rango.</summary>
    public bool gradient;

    public bool Covers(int charIndex) => charIndex >= from && charIndex <= to;

    public Color Evaluate(int charIndex)
    {
        if (!gradient || to <= from) return colorA;
        float t = Mathf.InverseLerp(from, to, charIndex);
        return Color.Lerp(colorA, colorB, t);
    }
}
