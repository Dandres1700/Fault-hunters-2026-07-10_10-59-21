using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Prologo narrativo autoconstruido. Funciona incluso si la escena Prologo
/// solo contiene una camara y una luz.
/// </summary>
[DisallowMultipleComponent]
public sealed class PrologoController : MonoBehaviour
{
    private sealed class StoryBeat
    {
        public readonly string tag;
        public readonly string title;
        public readonly string body;

        public StoryBeat(string tag, string title, string body)
        {
            this.tag = tag;
            this.title = title;
            this.body = body;
        }
    }

    private readonly StoryBeat[] beats =
    {
        new StoryBeat(
            "RED GLOBAL // ESTADO: INESTABLE",
            "EL SISTEMA INVISIBLE",
            "El mundo que conoces no funciona por casualidad. Detrás del clima, las ciudades y la tecnología existe una red invisible: millones de procesos que mantienen la realidad en equilibrio. Nadie la ve. Nadie, excepto los Cazadores."
        ),
        new StoryBeat(
            "ALERTA // ANOMALÍA MATERIALIZADA",
            "LAS FALLAS",
            "Cuando una regla del sistema se rompe, el error no desaparece. Toma forma. Las Fallas son criaturas hechas de energía inestable, materia corrompida y fragmentos de código roto. Todo lo que tocan comienza a olvidar cómo debería funcionar."
        ),
        new StoryBeat(
            "AGENCIA NEXO // ACCESO RESTRINGIDO",
            "LOS CAZADORES",
            "La Agencia Nexo recluta a las pocas personas capaces de ver la corrupción antes del colapso. Los Cazadores rastrean, contienen y eliminan Fallas. Cada victoria recupera una parte del código perdido y evita que una región entera sea reescrita."
        ),
        new StoryBeat(
            "EXPEDIENTE 07 // RANGO: NOVATO",
            "TU PRIMERA MISIÓN",
            "Hoy recibes una misión mundial. Seguirás una cadena de Fallas, país por país. Cada una imita símbolos, criaturas y monumentos de la cultura que invade. Tu trabajo es aprender su patrón, sobrevivir y cerrar el nodo corrupto."
        ),
        new StoryBeat(
            "ANÁLISIS DE PATRÓN // COINCIDENCIA: 99.8%",
            "ALGUIEN LAS ESTÁ PROGRAMANDO",
            "Las apariciones no son aleatorias. Todas contienen la misma firma oculta, como si una inteligencia estuviera escribiendo errores desde el origen del sistema. Cada Falla derrotada libera un fragmento. Juntos podrían revelar quién está cambiando las reglas."
        ),
        new StoryBeat(
            "DESTINO INICIAL // EGIPTO",
            "OPERACIÓN: PROTOCOLO KHEPRI",
            "La primera señal surge bajo la meseta de Guiza. Las redes de El Cairo repiten una secuencia imposible y una figura con cabeza de chacal aparece en cada transmisión. Designación temporal: ANUBIS.EXE. Cazador, inicia el rastreo."
        )
    };

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private TextMeshProUGUI tagText;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI progressText;
    private TextMeshProUGUI continueLabel;
    private RectTransform[] glitchBars;
    private Coroutine typeRoutine;
    private int beatIndex;
    private bool typing;
    private bool finishing;

    private void Start()
    {
        Time.timeScale = 1f;
        BuildUI();
        ShowBeat(0);
        GameAudioManager.Instance.PlayAt(GameSfx.GlitchPulse, Vector3.zero, 0.45f, 0.9f, 1.08f);
    }

    private void Update()
    {
        AnimateGlitchBars();
        if (finishing || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame ||
            Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            Advance();
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            FinishPrologue();
        }
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    private void BuildUI()
    {
        canvas = RuntimeUIFactory.CreateCanvas("Prologo UI", 500);
        canvas.transform.SetParent(transform, false);
        canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();

        RectTransform background = RuntimeUIFactory.CreatePanel(
            canvas.transform,
            "Background",
            RuntimeUIFactory.Ink,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );

        RuntimeUIFactory.CreatePanel(
            background,
            "Cyan Line",
            new Color(0.12f, 0.92f, 1f, 0.9f),
            new Vector2(0f, 0.97f),
            new Vector2(1f, 0.974f),
            Vector2.zero,
            Vector2.zero
        );

        RectTransform content = RuntimeUIFactory.CreatePanel(
            background,
            "Narrative Panel",
            new Color(0.035f, 0.055f, 0.085f, 0.92f),
            new Vector2(0.12f, 0.16f),
            new Vector2(0.88f, 0.84f),
            Vector2.zero,
            Vector2.zero
        );

        RuntimeUIFactory.CreatePanel(
            content,
            "Accent",
            RuntimeUIFactory.Cyan,
            new Vector2(0f, 0f),
            new Vector2(0.006f, 1f),
            Vector2.zero,
            Vector2.zero
        );

        tagText = RuntimeUIFactory.CreateText(
            content,
            "Tag",
            string.Empty,
            21f,
            RuntimeUIFactory.Cyan,
            TextAlignmentOptions.Left,
            new Vector2(0.07f, 0.83f),
            new Vector2(0.93f, 0.94f),
            Vector2.zero,
            Vector2.zero
        );
        tagText.fontStyle = FontStyles.Bold;
        tagText.characterSpacing = 3f;

        titleText = RuntimeUIFactory.CreateText(
            content,
            "Title",
            string.Empty,
            52f,
            Color.white,
            TextAlignmentOptions.Left,
            new Vector2(0.07f, 0.64f),
            new Vector2(0.93f, 0.84f),
            Vector2.zero,
            Vector2.zero
        );
        titleText.fontStyle = FontStyles.Bold;

        bodyText = RuntimeUIFactory.CreateText(
            content,
            "Body",
            string.Empty,
            28f,
            RuntimeUIFactory.SoftText,
            TextAlignmentOptions.TopLeft,
            new Vector2(0.07f, 0.25f),
            new Vector2(0.93f, 0.64f),
            Vector2.zero,
            Vector2.zero
        );
        bodyText.lineSpacing = 12f;

        progressText = RuntimeUIFactory.CreateText(
            content,
            "Progress",
            string.Empty,
            19f,
            new Color(0.56f, 0.66f, 0.72f, 1f),
            TextAlignmentOptions.BottomLeft,
            new Vector2(0.07f, 0.08f),
            new Vector2(0.45f, 0.18f),
            Vector2.zero,
            Vector2.zero
        );

        Button continueButton = RuntimeUIFactory.CreateButton(
            content,
            "Continue",
            "CONTINUAR  [ESPACIO]",
            new Vector2(0.67f, 0.07f),
            new Vector2(0.93f, 0.19f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.04f, 0.56f, 0.64f, 1f)
        );
        continueButton.onClick.AddListener(Advance);
        continueLabel = continueButton.GetComponentInChildren<TextMeshProUGUI>();

        Button skipButton = RuntimeUIFactory.CreateButton(
            background,
            "Skip",
            "SALTAR PRÓLOGO",
            new Vector2(0.76f, 0.88f),
            new Vector2(0.89f, 0.93f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.14f, 0.16f, 0.2f, 0.9f)
        );
        skipButton.onClick.AddListener(FinishPrologue);

        RuntimeUIFactory.CreateText(
            background,
            "Classification",
            "CAZADORES DE FALLAS // ARCHIVO DE INICIACIÓN",
            17f,
            new Color(0.46f, 0.55f, 0.62f, 1f),
            TextAlignmentOptions.BottomLeft,
            new Vector2(0.12f, 0.06f),
            new Vector2(0.7f, 0.12f),
            Vector2.zero,
            Vector2.zero
        );

        glitchBars = new RectTransform[7];
        for (int i = 0; i < glitchBars.Length; i++)
        {
            float y = 0.08f + i * 0.13f;
            glitchBars[i] = RuntimeUIFactory.CreatePanel(
                background,
                $"Glitch {i}",
                i % 2 == 0
                    ? new Color(0.12f, 0.92f, 1f, 0.055f)
                    : new Color(1f, 0.16f, 0.58f, 0.04f),
                new Vector2(0f, y),
                new Vector2(1f, y + 0.012f),
                Vector2.zero,
                Vector2.zero
            );
        }
    }

    private void ShowBeat(int index)
    {
        beatIndex = Mathf.Clamp(index, 0, beats.Length - 1);
        StoryBeat beat = beats[beatIndex];
        tagText.text = beat.tag;
        titleText.text = beat.title;
        progressText.text = $"REGISTRO {beatIndex + 1:00} / {beats.Length:00}";
        continueLabel.text = beatIndex == beats.Length - 1
            ? "INICIAR MISIÓN  [ESPACIO]"
            : "CONTINUAR  [ESPACIO]";

        if (typeRoutine != null)
        {
            StopCoroutine(typeRoutine);
        }
        typeRoutine = StartCoroutine(TypeBody(beat.body));
    }

    private IEnumerator TypeBody(string fullText)
    {
        typing = true;
        bodyText.text = fullText;
        bodyText.maxVisibleCharacters = 0;
        bodyText.ForceMeshUpdate();
        int count = bodyText.textInfo.characterCount;

        for (int i = 0; i <= count; i++)
        {
            bodyText.maxVisibleCharacters = i;
            if (i > 0 && i % 22 == 0)
            {
                GameAudioManager.Instance.PlayUI(GameSfx.UiHover, 0.08f, Random.Range(1.15f, 1.32f));
            }
            yield return new WaitForSecondsRealtime(0.018f);
        }

        bodyText.maxVisibleCharacters = int.MaxValue;
        typing = false;
        typeRoutine = null;
    }

    private void Advance()
    {
        if (finishing)
        {
            return;
        }

        if (typing)
        {
            if (typeRoutine != null)
            {
                StopCoroutine(typeRoutine);
            }
            bodyText.maxVisibleCharacters = int.MaxValue;
            typing = false;
            typeRoutine = null;
            return;
        }

        GameAudioManager.Instance.PlayUI(GameSfx.StoryAdvance, 0.68f, Random.Range(0.98f, 1.04f));
        if (beatIndex >= beats.Length - 1)
        {
            FinishPrologue();
            return;
        }

        if ((beatIndex + 1) % 2 == 0)
        {
            GameAudioManager.Instance.PlayAt(GameSfx.GlitchPulse, Vector3.zero, 0.32f, 0.9f, 1.1f);
        }
        ShowBeat(beatIndex + 1);
    }

    private void FinishPrologue()
    {
        if (finishing)
        {
            return;
        }

        finishing = true;
        GameProgress.MarkPrologueSeen();
        StartCoroutine(FinishRoutine());
    }

    private IEnumerator FinishRoutine()
    {
        GameAudioManager.Instance.PlayAt(GameSfx.MissionStart, Vector3.zero, 0.85f, 0.98f, 1.02f);
        float duration = 0.7f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            canvasGroup.alpha = 1f - elapsed / duration;
            yield return null;
        }

        canvasGroup.alpha = 0f;
        SceneManager.LoadSceneAsync(FlujoEscenas.MapaMundial);
    }

    private void AnimateGlitchBars()
    {
        if (glitchBars == null)
        {
            return;
        }

        for (int i = 0; i < glitchBars.Length; i++)
        {
            RectTransform bar = glitchBars[i];
            if (bar == null)
            {
                continue;
            }

            float offset = Mathf.Sin(Time.unscaledTime * (1.7f + i * 0.31f) + i) * (2f + i * 0.45f);
            bar.anchoredPosition = new Vector2(offset, 0f);
        }
    }
}
