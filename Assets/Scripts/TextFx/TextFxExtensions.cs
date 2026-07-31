using TMPro;
using UnityEngine;

/// <summary>
/// Atajos para trabajar directo sobre el TMP_Text sin tener que buscar/agregar el componente a mano.
/// Todas devuelven el TextFx, asi que se pueden encadenar:
///
///     label.Fx()
///          .ApplyEffect(TextFxType.Wave);
///
///     label.ApplyColor(new[] { "fuego", "hielo" }, new[] { Color.red, Color.cyan })
///          .ApplyEffect(TextFxType.Shake, "fuego");
/// </summary>
public static class TextFxExtensions
{
    /// <summary>Devuelve el TextFx del label, agregandolo si no existe.</summary>
    public static TextFx Fx(this TMP_Text label)
    {
        if (label == null) return null;

        if (!label.TryGetComponent(out TextFx fx))
            fx = label.gameObject.AddComponent<TextFx>();

        fx.Bind(label);
        return fx;
    }

    /// <summary>Pinta cada palabra con su color correspondiente.</summary>
    public static TextFx ApplyColor(this TMP_Text label, string[] words, Color[] colors, bool caseSensitive = false)
    {
        TextFx fx = label.Fx();
        fx.ApplyColor(words, colors, caseSensitive);
        return fx;
    }

    /// <summary>Pinta una sola palabra.</summary>
    public static TextFx ApplyColor(this TMP_Text label, string word, Color color, bool caseSensitive = false)
    {
        TextFx fx = label.Fx();
        fx.ApplyColor(new[] { word }, new[] { color }, caseSensitive);
        return fx;
    }

    /// <summary>Degradado a lo largo del texto (o de un rango).</summary>
    public static TextFx ApplyGradient(this TMP_Text label, Color from, Color to, int fromIndex = -1, int toIndex = -1)
    {
        TextFx fx = label.Fx();
        fx.ApplyGradient(from, to, fromIndex, toIndex);
        return fx;
    }

    /// <summary>Un color por letra, en orden.</summary>
    public static TextFx ApplyColorPerLetter(this TMP_Text label, Color[] colors, int startIndex = 0)
    {
        TextFx fx = label.Fx();
        fx.ApplyColorPerLetter(colors, startIndex);
        return fx;
    }

    /// <summary>Efecto animado sobre todo el texto (o un rango de caracteres).</summary>
    public static TextFx ApplyEffect(this TMP_Text label, TextFxType type, int from = -1, int to = -1)
    {
        TextFx fx = label.Fx();
        fx.ApplyEffect(type, from, to);
        return fx;
    }

    /// <summary>Efecto animado solo sobre las apariciones de una palabra.</summary>
    public static TextFx ApplyEffect(this TMP_Text label, TextFxType type, string word, bool caseSensitive = false)
    {
        TextFx fx = label.Fx();
        fx.ApplyEffect(type, word, caseSensitive);
        return fx;
    }

    /// <summary>Contorno del label completo (no admite rango: es una propiedad del material).</summary>
    public static TextFx ApplyOutline(this TMP_Text label, Color color, float width = 0.2f)
    {
        TextFx fx = label.Fx();
        fx.ApplyOutline(color, width);
        return fx;
    }

    /// <summary>Quita colores y efectos, y deja el texto visible.</summary>
    public static TextFx ClearFx(this TMP_Text label)
    {
        TextFx fx = label.Fx();
        fx.ClearAll();
        return fx;
    }

    /// <summary>Cambia el texto y limpia los rangos anteriores (los indices ya no son validos).</summary>
    public static TextFx SetTextFx(this TMP_Text label, string value)
    {
        TextFx fx = label.Fx();
        fx.ClearColors();
        fx.ClearEffects();
        fx.SetText(value);
        return fx;
    }
}
