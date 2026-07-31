using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Componente de efectos para TMP_Text: color por letra, efectos animados acumulables
/// y typewriter (aparicion letra por letra) sin cambiar el layout del texto.
///
/// Uso rapido (por codigo, sin tocar el inspector):
///     label.ApplyColor(new[] { "fuego" }, new[] { Color.red });
///     label.ApplyEffect(TextFxType.Wave);
///     await label.Fx().TypeIn();
///
/// El componente se agrega solo la primera vez que usas cualquiera de esas extensiones.
/// </summary>
[DisallowMultipleComponent]
public class TextFx : MonoBehaviour
{
    // ---------------------------------------------------------------- inspector

    [Tooltip("Si se deja vacio se busca un TMP_Text en este GameObject.")]
    [SerializeField] private TMP_Text target;

    [Tooltip("Efectos que arrancan activos. Puedes agregar mas en runtime con ApplyEffect().")]
    [SerializeField] private List<TextFxInstance> effects = new List<TextFxInstance>();

    [Header("Typewriter")]
    [SerializeField] private TextEntranceType entrance = TextEntranceType.FadeIn;
    [SerializeField] private TextEntranceType exit = TextEntranceType.FadeIn;

    [Tooltip("Duracion de la animacion de entrada/salida de UNA letra.")]
    [SerializeField] private float entranceDuration = 0.22f;

    [Tooltip("Letras por segundo del typewriter.")]
    [SerializeField] private float charsPerSecond = 28f;

    [Tooltip("Pausa extra al llegar a . , ! ? : ;")]
    [SerializeField] private float punctuationPause = 0.12f;

    [Tooltip("Distancia de desplazamiento de los efectos de entrada tipo Slide.")]
    [SerializeField] private float entranceDistance = 30f;

    [Header("General")]
    [Tooltip("Escala amplitudes y distancias segun el fontSize, para que un mismo efecto se vea igual en textos grandes y pequenos.")]
    [SerializeField] private bool scaleWithFontSize = true;

    [Tooltip("Usar Time.unscaledTime (sigue animando con el juego en pausa).")]
    [SerializeField] private bool useUnscaledTime = true;

    // ---------------------------------------------------------------- estado

    private TMP_TextInfo textInfo;
    private Vector3[][] baseVertices;
    private Color32[][] baseColors;
    private string plainText = string.Empty;

    private readonly List<TextColorRange> colorRanges = new List<TextColorRange>();

    /// <summary>Pausas extra del typewriter. Clave = indice de letra, valor = segundos a esperar DESPUES de revelarla. -1 = antes de empezar.</summary>
    private readonly Dictionary<int, float> pauses = new Dictionary<int, float>();

    /// <summary>Se dispara cada vez que el typewriter revela una letra. Util para SFX.</summary>
    public event Action<int> OnCharacterRevealed;

    /// <summary>Momento en que cada letra fue revelada. -1 = todavia oculta.</summary>
    private float[] revealAt = Array.Empty<float>();

    /// <summary>Momento en que cada letra empezo a desaparecer. -1 = no esta saliendo.</summary>
    private float[] exitAt = Array.Empty<float>();

    // Outline: vive en el material, no en los vertices.
    private bool outlineCached;
    private bool outlineApplied;
    private Color originalOutlineColor;
    private float originalOutlineWidth;
    private Color lastOutlineColor;
    private float lastOutlineWidth = -1f;

    private bool hideUnrevealed;
    private bool geometryDirty = true;
    private bool wroteLastFrame;
    private bool caching;
    private int typeToken;

    // ---------------------------------------------------------------- props

    public TMP_Text Target => target;
    public bool IsTyping { get; private set; }
    public int CharacterCount => textInfo != null ? textInfo.characterCount : 0;

    private float Now => useUnscaledTime ? Time.unscaledTime : Time.time;

    /// <summary>Factor para que las amplitudes sean independientes del tamano de fuente.</summary>
    private float UnitScale
    {
        get
        {
            if (!scaleWithFontSize || target == null) return 1f;
            float size = target.fontSize;
            return size <= 0f ? 1f : size / 36f;
        }
    }

    // ---------------------------------------------------------------- ciclo de vida

    private void Awake() => Bind(target);

    private void OnEnable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTMPTextChanged);
        geometryDirty = true;
    }

    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTMPTextChanged);
    }

    /// <summary>Asocia (o reasocia) el TMP_Text a animar.</summary>
    public void Bind(TMP_Text label)
    {
        TMP_Text previous = target;

        if (label != null) target = label;
        if (target == null) target = GetComponent<TMP_Text>();
        if (target == null)
        {
            Debug.LogError($"[TextFx] No hay un TMP_Text en '{name}'.", this);
            enabled = false;
            return;
        }

        // Solo invalidar si de verdad cambio el target: Fx() llama a Bind en cada uso.
        if (target != previous) geometryDirty = true;
    }

    private void OnTMPTextChanged(UnityEngine.Object obj)
    {
        // ForceMeshUpdate tambien dispara este evento: si venimos de nuestro propio
        // cacheo hay que ignorarlo, o entrariamos en un bucle de regeneracion por frame.
        if (caching) return;

        // No se puede regenerar el mesh dentro del evento: solo marcamos.
        if (obj == target) geometryDirty = true;
    }

    private void LateUpdate()
    {
        if (target == null) return;
        if (geometryDirty) CacheGeometry();

        UpdateOutline();

        if (textInfo == null || textInfo.characterCount == 0) return;

        bool active = effects.Count > 0 || colorRanges.Count > 0 || hideUnrevealed || IsExiting();

        if (!active)
        {
            // Un ultimo pase para devolver el mesh a su estado original.
            if (!wroteLastFrame) return;
            wroteLastFrame = false;
        }
        else
        {
            wroteLastFrame = true;
        }

        ApplyToMesh();
    }

    /// <summary>
    /// El outline es un uniform del shader (_OutlineColor / _OutlineWidth), no dato de vertice:
    /// se aplica al label completo. Solo se escribe cuando cambia, porque el setter marca
    /// el texto como sucio y forzaria una regeneracion del mesh cada frame.
    /// </summary>
    private void UpdateOutline()
    {
        TextFxInstance outlineFx = null;
        for (int e = 0; e < effects.Count; e++)
            if (effects[e] != null && effects[e].type == TextFxType.Outline)
                outlineFx = effects[e];

        if (outlineFx == null)
        {
            if (outlineApplied && outlineCached)
            {
                target.outlineColor = originalOutlineColor;
                target.outlineWidth = originalOutlineWidth;
                lastOutlineColor = originalOutlineColor;
                lastOutlineWidth = originalOutlineWidth;
                outlineApplied = false;
            }
            return;
        }

        if (!outlineCached)
        {
            originalOutlineColor = target.outlineColor;
            originalOutlineWidth = target.outlineWidth;
            outlineCached = true;
        }

        Color c = outlineFx.color;
        float w = Mathf.Clamp01(outlineFx.amplitude);

        if (outlineApplied && lastOutlineColor == c && Mathf.Approximately(lastOutlineWidth, w)) return;

        target.outlineColor = c;
        target.outlineWidth = w;
        lastOutlineColor = c;
        lastOutlineWidth = w;
        outlineApplied = true;
    }

    private bool IsExiting()
    {
        for (int i = 0; i < exitAt.Length; i++)
            if (exitAt[i] >= 0f) return true;
        return false;
    }

    // ---------------------------------------------------------------- cache del mesh

    /// <summary>Regenera el mesh de TMP y guarda una copia limpia de vertices y colores.</summary>
    public void EnsureCache(bool force = false)
    {
        if (force || geometryDirty || textInfo == null) CacheGeometry();
    }

    private void CacheGeometry()
    {
        geometryDirty = false;
        if (target == null) return;

        // forceTextReparsing = true para que TMP no reutilice el mesh que ya modificamos.
        caching = true;
        try { target.ForceMeshUpdate(true, true); }
        finally { caching = false; }

        textInfo = target.textInfo;
        if (textInfo == null) return;

        int meshes = textInfo.meshInfo.Length;
        baseVertices = new Vector3[meshes][];
        baseColors = new Color32[meshes][];

        for (int m = 0; m < meshes; m++)
        {
            Vector3[] v = textInfo.meshInfo[m].vertices;
            Color32[] c = textInfo.meshInfo[m].colors32;
            baseVertices[m] = v != null ? (Vector3[])v.Clone() : Array.Empty<Vector3>();
            baseColors[m] = c != null ? (Color32[])c.Clone() : Array.Empty<Color32>();
        }

        int count = textInfo.characterCount;

        // Texto plano tal como se ve (sin tags), para que los indices de ApplyColor coincidan.
        StringBuilder sb = new StringBuilder(count);
        for (int i = 0; i < count; i++) sb.Append(textInfo.characterInfo[i].character);
        plainText = sb.ToString();

        ResizeStateArrays(count);
    }

    private void ResizeStateArrays(int count)
    {
        if (revealAt.Length != count)
        {
            float[] newReveal = new float[count];
            float[] newExit = new float[count];
            for (int i = 0; i < count; i++)
            {
                newReveal[i] = i < revealAt.Length ? revealAt[i] : (hideUnrevealed ? -1f : 0f);
                newExit[i] = i < exitAt.Length ? exitAt[i] : -1f;
            }
            revealAt = newReveal;
            exitAt = newExit;
        }
    }

    // ---------------------------------------------------------------- render

    private void ApplyToMesh()
    {
        float time = Now;
        float unit = UnitScale;
        int count = textInfo.characterCount;

        for (int i = 0; i < count; i++)
        {
            TMP_CharacterInfo ci = textInfo.characterInfo[i];
            if (!ci.isVisible) continue;

            int m = ci.materialReferenceIndex;
            int vi = ci.vertexIndex;

            if (baseVertices == null || baseColors == null) continue;
            if (m >= baseVertices.Length || m >= baseColors.Length) continue;

            Vector3[] srcV = baseVertices[m];
            Color32[] srcC = baseColors[m];
            if (vi + 3 >= srcV.Length || vi + 3 >= srcC.Length) continue;

            Vector3[] dstV = textInfo.meshInfo[m].vertices;
            Color32[] dstC = textInfo.meshInfo[m].colors32;
            if (dstV == null || dstC == null) continue;
            if (vi + 3 >= dstV.Length || vi + 3 >= dstC.Length) continue;

            Vector3 offset = Vector3.zero;
            float scale = 1f;
            float rotation = 0f;
            float alphaMul = 1f;
            Color color = srcC[vi];

            // --- color por rango (el ultimo rango que cubre la letra manda)
            for (int r = 0; r < colorRanges.Count; r++)
            {
                if (!colorRanges[r].Covers(i)) continue;
                Color rc = colorRanges[r].Evaluate(i);
                color = new Color(rc.r, rc.g, rc.b, color.a * rc.a);
            }

            // --- efectos acumulables
            for (int e = 0; e < effects.Count; e++)
            {
                TextFxInstance fx = effects[e];
                if (fx == null || !fx.Covers(i, count)) continue;
                EvaluateEffect(fx, i, time, unit, ref offset, ref scale, ref rotation, ref color, ref alphaMul);
            }

            // --- typewriter: entrada / salida
            if (exitAt[i] >= 0f)
            {
                float t = entranceDuration <= 0f ? 1f : Mathf.Clamp01((time - exitAt[i]) / entranceDuration);
                ApplyTransition(exit, 1f - t, unit, ref offset, ref scale, ref rotation, ref alphaMul);
            }
            else if (hideUnrevealed)
            {
                if (revealAt[i] < 0f)
                {
                    alphaMul = 0f;
                }
                else
                {
                    float t = entranceDuration <= 0f ? 1f : Mathf.Clamp01((time - revealAt[i]) / entranceDuration);
                    ApplyTransition(entrance, t, unit, ref offset, ref scale, ref rotation, ref alphaMul);
                }
            }

            // --- escribir los 4 vertices
            color.a *= Mathf.Clamp01(alphaMul);
            Color32 finalColor = color;

            Vector3 mid = (srcV[vi] + srcV[vi + 2]) * 0.5f;
            bool rotate = !Mathf.Approximately(rotation, 0f);
            Quaternion q = rotate ? Quaternion.Euler(0f, 0f, rotation) : Quaternion.identity;

            for (int j = 0; j < 4; j++)
            {
                Vector3 v = srcV[vi + j] - mid;
                if (!Mathf.Approximately(scale, 1f)) v *= scale;
                if (rotate) v = q * v;
                dstV[vi + j] = mid + v + offset;
                dstC[vi + j] = finalColor;
            }
        }

        target.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }

    private static void EvaluateEffect(
        TextFxInstance fx, int i, float time, float unit,
        ref Vector3 offset, ref float scale, ref float rotation, ref Color color, ref float alphaMul)
    {
        float phase = time * fx.speed + i * fx.frequency;
        float amp = fx.amplitude;

        switch (fx.type)
        {
            case TextFxType.Wave:
                offset.y += Mathf.Sin(phase) * amp * unit;
                break;

            case TextFxType.Shake:
                offset.x += (Mathf.PerlinNoise(i * 13.7f, time * fx.speed) - 0.5f) * 2f * amp * unit;
                offset.y += (Mathf.PerlinNoise(i * 7.3f + 64f, time * fx.speed) - 0.5f) * 2f * amp * unit;
                break;

            case TextFxType.Wiggle:
                offset.x += Mathf.Sin(phase) * amp * unit;
                offset.y += Mathf.Cos(phase * 1.3f) * amp * unit;
                break;

            case TextFxType.Bounce:
                offset.y += Mathf.Abs(Mathf.Sin(phase)) * amp * unit;
                break;

            case TextFxType.Pulse:
                scale += Mathf.Sin(phase) * amp;
                break;

            case TextFxType.Swing:
                rotation += Mathf.Sin(phase) * amp;
                break;

            case TextFxType.Rainbow:
            {
                float hue = Mathf.Repeat(time * fx.speed + i * fx.frequency, 1f);
                Color rainbow = Color.HSVToRGB(hue, 1f, 1f);
                color = Color.Lerp(color, new Color(rainbow.r, rainbow.g, rainbow.b, color.a), Mathf.Clamp01(amp));
                break;
            }

            case TextFxType.Blink:
                alphaMul *= 1f - Mathf.Clamp01(amp) * (0.5f + 0.5f * Mathf.Sin(phase));
                break;

            case TextFxType.Outline:
                break; // se resuelve en UpdateOutline(), a nivel de material
        }
    }

    /// <summary>t = 0 (oculta) -> 1 (visible). Se usa al reves para las salidas.</summary>
    private void ApplyTransition(
        TextEntranceType type, float t, float unit,
        ref Vector3 offset, ref float scale, ref float rotation, ref float alphaMul)
    {
        t = Mathf.Clamp01(t);
        float dist = entranceDistance * unit;

        switch (type)
        {
            case TextEntranceType.None:
                if (t < 1f) alphaMul = 0f;
                break;

            case TextEntranceType.FadeIn:
                alphaMul *= t;
                break;

            case TextEntranceType.ScaleUp:
                scale *= t;
                alphaMul *= t;
                break;

            case TextEntranceType.Pop:
                scale *= EaseOutBack(t);
                alphaMul *= Mathf.Clamp01(t * 3f);
                break;

            case TextEntranceType.SlideUp:
                offset.y -= dist * (1f - EaseOutCubic(t));
                alphaMul *= Mathf.Clamp01(t * 2f);
                break;

            case TextEntranceType.SlideDown:
                offset.y += dist * (1f - EaseOutCubic(t));
                alphaMul *= Mathf.Clamp01(t * 2f);
                break;

            case TextEntranceType.SlideLeft:
                offset.x -= dist * (1f - EaseOutCubic(t));
                alphaMul *= Mathf.Clamp01(t * 2f);
                break;

            case TextEntranceType.SlideRight:
                offset.x += dist * (1f - EaseOutCubic(t));
                alphaMul *= Mathf.Clamp01(t * 2f);
                break;

            case TextEntranceType.Spin:
                rotation += 180f * (1f - EaseOutCubic(t));
                scale *= EaseOutCubic(t);
                alphaMul *= t;
                break;
        }
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    // ================================================================ API: COLOR

    /// <summary>Pinta cada palabra con su color. Si pasas un solo color, se usa para todas.</summary>
    public void ApplyColor(string[] words, Color[] colors, bool caseSensitive = false, bool clearPrevious = false)
    {
        EnsureCache(true);
        if (clearPrevious) colorRanges.Clear();
        if (words == null || words.Length == 0 || colors == null || colors.Length == 0) return;

        StringComparison cmp = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        for (int w = 0; w < words.Length; w++)
        {
            string needle = words[w];
            if (string.IsNullOrEmpty(needle)) continue;

            Color c = colors[Mathf.Min(w, colors.Length - 1)];

            int idx = 0;
            while (idx <= plainText.Length - needle.Length)
            {
                int found = plainText.IndexOf(needle, idx, cmp);
                if (found < 0) break;
                colorRanges.Add(new TextColorRange
                {
                    from = found,
                    to = found + needle.Length - 1,
                    colorA = c,
                    colorB = c,
                });
                idx = found + needle.Length;
            }
        }
    }

    /// <summary>Pinta un rango explicito de caracteres.</summary>
    public void ApplyColor(int from, int to, Color color)
    {
        EnsureCache();
        colorRanges.Add(new TextColorRange { from = from, to = to, colorA = color, colorB = color });
    }

    /// <summary>Degradado de colorA a colorB. Sin rango = todo el texto.</summary>
    public void ApplyGradient(Color colorA, Color colorB, int from = -1, int to = -1)
    {
        EnsureCache(true);
        int count = Mathf.Max(1, textInfo != null ? textInfo.characterCount : 1);
        colorRanges.Add(new TextColorRange
        {
            from = from < 0 ? 0 : from,
            to = to < 0 ? count - 1 : to,
            colorA = colorA,
            colorB = colorB,
            gradient = true,
        });
    }

    /// <summary>Un color por letra, en orden. Sirve para casos totalmente manuales.</summary>
    public void ApplyColorPerLetter(Color[] colors, int startIndex = 0)
    {
        EnsureCache(true);
        if (colors == null) return;
        for (int i = 0; i < colors.Length; i++)
            ApplyColor(startIndex + i, startIndex + i, colors[i]);
    }

    public void ClearColors() => colorRanges.Clear();

    // ================================================================ API: EFECTOS

    /// <summary>Agrega un efecto con valores por defecto. Sin rango = todo el texto.</summary>
    public TextFxInstance ApplyEffect(TextFxType type, int from = -1, int to = -1)
    {
        TextFxInstance fx = TextFxInstance.Default(type, from, to);
        effects.Add(fx);
        return fx;
    }

    /// <summary>Agrega un efecto sobre todas las apariciones de una palabra.</summary>
    public void ApplyEffect(TextFxType type, string word, bool caseSensitive = false)
    {
        EnsureCache(true);
        StringComparison cmp = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (string.IsNullOrEmpty(word)) return;

        int idx = 0;
        while (idx <= plainText.Length - word.Length)
        {
            int found = plainText.IndexOf(word, idx, cmp);
            if (found < 0) break;
            ApplyEffect(type, found, found + word.Length - 1);
            idx = found + word.Length;
        }
    }

    /// <summary>Agrega un efecto ya configurado (util para tunear amplitud/velocidad).</summary>
    public TextFxInstance ApplyEffect(TextFxInstance fx)
    {
        if (fx != null) effects.Add(fx);
        return fx;
    }

    /// <summary>
    /// Contorno del label. Aplica a TODO el texto (es un uniform del shader, no admite rango).
    /// width va de 0 a 1; por encima de ~0.3 se recorta si el font asset tiene poco padding.
    /// </summary>
    public TextFxInstance ApplyOutline(Color color, float width = 0.2f)
    {
        RemoveEffect(TextFxType.Outline); // solo tiene sentido uno

        TextFxInstance fx = TextFxInstance.Default(TextFxType.Outline);
        fx.color = color;
        fx.amplitude = width;
        effects.Add(fx);
        return fx;
    }

    /// <summary>Quita el contorno y devuelve el material a su estado original.</summary>
    public void RemoveOutline() => RemoveEffect(TextFxType.Outline);

    public void RemoveEffect(TextFxType type) => effects.RemoveAll(f => f != null && f.type == type);

    public void RemoveEffect(TextFxInstance fx) => effects.Remove(fx);

    public void RemoveEffect(string id) => effects.RemoveAll(f => f != null && f.id == id);

    public void ClearEffects() => effects.Clear();

    /// <summary>Quita colores, efectos y deja todo el texto visible.</summary>
    public void ClearAll()
    {
        ClearEffects();
        ClearColors();
        ClearPauses();
        ShowAll();
    }

    // ================================================================ API: TYPEWRITER

    /// <summary>Todas las letras con alpha 0. El recuadro conserva su tamano.</summary>
    public void HideAll()
    {
        typeToken++;
        IsTyping = false;
        EnsureCache(true);
        hideUnrevealed = true;
        for (int i = 0; i < revealAt.Length; i++) revealAt[i] = -1f;
        for (int i = 0; i < exitAt.Length; i++) exitAt[i] = -1f;
    }

    /// <summary>Todo visible de golpe. Cancela cualquier typewriter en curso.</summary>
    public void ShowAll()
    {
        typeToken++;
        IsTyping = false;
        hideUnrevealed = false;
        for (int i = 0; i < revealAt.Length; i++) revealAt[i] = 0f;
        for (int i = 0; i < exitAt.Length; i++) exitAt[i] = -1f;
    }

    /// <summary>Alias de ShowAll pensado para el boton de "skip" en dialogos.</summary>
    public void Skip() => ShowAll();

    /// <summary>
    /// Cambia el texto y deja todo listo para animar (sin esperar un frame).
    /// Admite tags de pausa inline: "Hola{p:1.5} mundo" espera 1.5s despues de la 'a'.
    /// </summary>
    public void SetText(string value)
    {
        if (target == null) return;
        pauses.Clear();
        target.text = ParsePauseTags(value);
        geometryDirty = true;
        EnsureCache(true);
        ShowAll(); // limpia reveal/exit del texto anterior y cancela el typewriter en curso
    }

    // ---------------------------------------------------------------- pausas

    /// <summary>Espera extra despues de revelar la letra indicada. Usa -1 para esperar antes de la primera.</summary>
    public void AddPause(int charIndex, float seconds)
    {
        if (seconds <= 0f || charIndex < -1) return;
        pauses.TryGetValue(charIndex, out float previous);
        pauses[charIndex] = previous + seconds;
    }

    /// <summary>Espera extra despues de cada aparicion de una palabra.</summary>
    public void PauseAfter(string word, float seconds, bool caseSensitive = false)
    {
        if (string.IsNullOrEmpty(word) || seconds <= 0f) return;
        EnsureCache(true);

        StringComparison cmp = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int idx = 0;
        while (idx <= plainText.Length - word.Length)
        {
            int found = plainText.IndexOf(word, idx, cmp);
            if (found < 0) break;
            AddPause(found + word.Length - 1, seconds);
            idx = found + word.Length;
        }
    }

    /// <summary>Espera extra antes de cada aparicion de una palabra.</summary>
    public void PauseBefore(string word, float seconds, bool caseSensitive = false)
    {
        if (string.IsNullOrEmpty(word) || seconds <= 0f) return;
        EnsureCache(true);

        StringComparison cmp = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int idx = 0;
        while (idx <= plainText.Length - word.Length)
        {
            int found = plainText.IndexOf(word, idx, cmp);
            if (found < 0) break;
            AddPause(found - 1, seconds);
            idx = found + word.Length;
        }
    }

    public void ClearPauses() => pauses.Clear();

    /// <summary>
    /// Extrae los tags "{p:segundos}" del texto y los convierte en pausas,
    /// llevando la cuenta de los caracteres reales para que los indices coincidan con TMP.
    /// </summary>
    private string ParsePauseTags(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.IndexOf('{') < 0) return raw;

        StringBuilder sb = new StringBuilder(raw.Length);
        int plainIndex = 0; // cuantos caracteres visibles llevamos

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];

            if (c == '{')
            {
                int close = raw.IndexOf('}', i);
                if (close > i)
                {
                    string body = raw.Substring(i + 1, close - i - 1);
                    if (body.Length > 2 && (body[0] == 'p' || body[0] == 'P') && body[1] == ':' &&
                        float.TryParse(body.Substring(2), NumberStyles.Float, CultureInfo.InvariantCulture, out float secs))
                    {
                        AddPause(plainIndex - 1, secs);
                        i = close;
                        continue;
                    }
                }
            }

            // Los rich text tags de TMP no cuentan como caracteres... salvo <br> y <sprite>.
            if (c == '<')
            {
                int close = raw.IndexOf('>', i);
                if (close > i)
                {
                    string tag = raw.Substring(i + 1, close - i - 1);
                    sb.Append(raw, i, close - i + 1);
                    if (tag.StartsWith("br", StringComparison.OrdinalIgnoreCase) ||
                        tag.StartsWith("sprite", StringComparison.OrdinalIgnoreCase))
                        plainIndex++;
                    i = close;
                    continue;
                }
            }

            sb.Append(c);
            plainIndex++;
        }

        return sb.ToString();
    }

    /// <summary>Revela las letras una a una con el efecto de entrada configurado.</summary>
    public Awaitable TypeIn() => TypeIn(charsPerSecond);

    /// <summary>Revela las letras una a una a la velocidad indicada (letras por segundo).</summary>
    public async Awaitable TypeIn(float cps)
    {
        EnsureCache(true);
        hideUnrevealed = true;

        int count = textInfo != null ? textInfo.characterCount : 0;
        for (int i = 0; i < revealAt.Length; i++) revealAt[i] = -1f;
        for (int i = 0; i < exitAt.Length; i++) exitAt[i] = -1f;

        int token = ++typeToken;
        IsTyping = true;
        float step = cps <= 0f ? 0f : 1f / cps;

        try
        {
            // pausa antes de la primera letra
            if (pauses.TryGetValue(-1, out float lead) && lead > 0f)
                await Awaitable.WaitForSecondsAsync(lead, destroyCancellationToken);

            for (int i = 0; i < count; i++)
            {
                if (token != typeToken) return;   // cancelado por Skip / otro TypeIn
                if (i < revealAt.Length) revealAt[i] = Now;
                OnCharacterRevealed?.Invoke(i);

                float wait = step;

                char c = textInfo.characterInfo[i].character;
                if (punctuationPause > 0f && (c == '.' || c == ',' || c == '!' || c == '?' || c == ':' || c == ';'))
                    wait += punctuationPause;

                if (pauses.TryGetValue(i, out float extra)) wait += extra;

                if (wait > 0f) await Awaitable.WaitForSecondsAsync(wait, destroyCancellationToken);
            }

            if (entranceDuration > 0f)
                await Awaitable.WaitForSecondsAsync(entranceDuration, destroyCancellationToken);
        }
        catch (OperationCanceledException)
        {
            return; // el GameObject se destruyo mientras escribia
        }
        finally
        {
            if (token == typeToken) IsTyping = false;
        }
    }

    /// <summary>Oculta las letras una a una con el efecto de salida configurado.</summary>
    public Awaitable TypeOut() => TypeOut(charsPerSecond);

    /// <summary>Oculta las letras una a una a la velocidad indicada.</summary>
    public async Awaitable TypeOut(float cps)
    {
        EnsureCache();

        int count = textInfo != null ? textInfo.characterCount : 0;
        int token = ++typeToken;
        IsTyping = true;
        float step = cps <= 0f ? 0f : 1f / cps;

        try
        {
            for (int i = 0; i < count; i++)
            {
                if (token != typeToken) return;
                if (i < exitAt.Length) exitAt[i] = Now;
                if (step > 0f) await Awaitable.WaitForSecondsAsync(step, destroyCancellationToken);
            }

            if (entranceDuration > 0f)
                await Awaitable.WaitForSecondsAsync(entranceDuration, destroyCancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (token == typeToken) IsTyping = false;
        }
    }

    /// <summary>Revela una sola letra (para typewriters manuales o sincronizados con audio).</summary>
    public void RevealCharacter(int index)
    {
        EnsureCache();
        hideUnrevealed = true;
        if (index >= 0 && index < revealAt.Length) revealAt[index] = Now;
    }
}
