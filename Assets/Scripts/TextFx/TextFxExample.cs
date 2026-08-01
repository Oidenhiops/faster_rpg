using System;
using NaughtyAttributes;
using TMPro;
using UnityEngine;

/// <summary>
/// Ejemplos de uso de TextFx. Ponlo en el mismo GameObject del TMP_Text
/// y usa los botones del inspector para probar cada caso.
///
/// IMPORTANTE: los botones solo funcionan en Play Mode. TextFx anima en LateUpdate,
/// que no corre fuera del play mode.
/// </summary>
public class TextFxExample : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    private TMP_Text Label => label != null ? label : (label = GetComponent<TMP_Text>());

    // ---------------------------------------------------------------- color

    [Button("1. Color por palabras")]
    private void ColorPorPalabras()
    {
        Label.SetTextFx("Lanzas <b>Bola de fuego</b> y recibes dano de hielo");

        Label.ApplyColor(
            new[] { "fuego", "hielo", "dano" },
            new[] { new Color(1f, 0.4f, 0.1f), Color.cyan, Color.red });
    }

    [Button("2. Degradado")]
    private void Degradado()
    {
        Label.SetTextFx("NIVEL COMPLETADO");
        Label.ApplyGradient(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.35f, 0f));
    }

    [Button("3. Un color por letra")]
    private void ColorPorLetra()
    {
        Label.SetTextFx("ARCOIRIS");
        Color[] colores = new Color[8];
        for (int i = 0; i < colores.Length; i++)
            colores[i] = Color.HSVToRGB(i / (float)colores.Length, 0.9f, 1f);
        Label.ApplyColorPerLetter(colores);
    }

    // ---------------------------------------------------------------- efectos

    [Button("4. Wave en todo el texto")]
    private void Wave()
    {
        Label.SetTextFx("Texto ondulante");
        Label.ApplyEffect(TextFxType.Wave);
    }

    [Button("5. Efectos acumulados por palabra")]
    private void Acumulados()
    {
        Label.SetTextFx("El jefe esta enfurecido y maldito");

        Label.ApplyColor(new[] { "enfurecido", "maldito" }, new[] { Color.red, new Color(0.6f, 0.2f, 0.9f) })
             .ApplyEffect(TextFxType.Shake, "enfurecido");

        // Wave + Rainbow al mismo tiempo sobre la misma palabra
        Label.ApplyEffect(TextFxType.Wave, "maldito");
        Label.ApplyEffect(TextFxType.Rainbow, "maldito");
    }

    [Button("6. Efecto tuneado a mano")]
    private void EfectoTuneado()
    {
        Label.SetTextFx("temblor fuerte");

        TextFxInstance fx = TextFxInstance.Default(TextFxType.Shake);
        fx.amplitude = 6f;
        fx.speed = 60f;
        Label.Fx().ApplyEffect(fx);
    }

    [Button("6b. Efecto finito (se para solo)", EButtonEnableMode.Playmode)]
    private void EfectoFinito()
    {
        Label.SetTextFx("120 / 200");

        // Una sola oscilacion y el efecto se quita solo.
        Label.ApplyEffect(TextFxType.Wave);
        Label.StopEffectsAfterFinish(TextFxType.Wave);

        // Equivalente en una linea: Label.PlayEffect(TextFxType.Wave);
        // Tres oscilaciones:        Label.PlayEffect(TextFxType.Wave, 3);
        // Por tiempo:              Label.StopEffectAfter(TextFxType.Shake, 0.5f);
    }

    // ---------------------------------------------------------------- typewriter

    [Button("7. Typewriter (letra por letra)", EButtonEnableMode.Playmode)]
    private void Typewriter() => _ = TypewriterAsync();

    private async Awaitable TypewriterAsync()
    {
        try
        {
            Label.SetTextFx("El recuadro ya tiene su tamano final, las letras van apareciendo una a una.");
            TextFx fx = Label.Fx();

            fx.HideAll();                 // alpha 0, layout intacto
            await fx.TypeIn();             // aparecen con el efecto de entrada del inspector
            await Awaitable.WaitForSecondsAsync(1f);
            await fx.TypeOut();            // desaparecen una a una
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    [Button("8. Typewriter + color + wave", EButtonEnableMode.Playmode)]
    private void TypewriterCompleto() => _ = TypewriterCompletoAsync();

    private async Awaitable TypewriterCompletoAsync()
    {
        try
        {
            Label.SetTextFx("Has encontrado la Espada Maldita");
            TextFx fx = Label.Fx();

            fx.ApplyColor(new[] { "Espada Maldita" }, new[] { new Color(0.7f, 0.3f, 1f) });
            fx.ApplyEffect(TextFxType.Wave, "Espada Maldita");

            fx.HideAll();
            await fx.TypeIn(20f);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    [Button("11. Typewriter con pausas", EButtonEnableMode.Playmode)]
    private void TypewriterPausas() => _ = TypewriterPausasAsync();

    private async Awaitable TypewriterPausasAsync()
    {
        try
        {
            // Tres formas de pausar, se pueden mezclar:
            // a) tag inline {p:segundos}
            Label.SetTextFx("Espera...{p:1.2} algo se mueve en la oscuridad.");
            TextFx fx = Label.Fx();

            // b) despues / antes de una palabra
            fx.PauseAfter("oscuridad", 0.8f);
            fx.PauseBefore("algo", 0.4f);

            // c) por indice exacto de letra (-1 = antes de empezar)
            fx.AddPause(-1, 0.3f);

            fx.HideAll();
            await fx.TypeIn();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    [Button("12. Outline")]
    private void Outline()
    {
        Label.SetTextFx("Texto con contorno");
        Label.ApplyOutline(Color.black, 0.2f);

        // se combina sin problema con color y efectos
        Label.ApplyColor("contorno", Color.yellow);
        Label.ApplyEffect(TextFxType.Wave);
    }

    [Button("9. Skip")]
    private void SkipTypewriter() => Label.Fx().Skip();

    [Button("10. Limpiar todo")]
    private void Limpiar() => Label.ClearFx();
}
