using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tarjeta narrativa del mapa mundial. Permite revisar misiones desbloqueadas
/// e iniciar la seleccionada sin depender de una UI preconfigurada.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldMapStoryController : MonoBehaviour
{
    private Canvas canvas;
    private TextMeshProUGUI counterText;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bossText;
    private TextMeshProUGUI objectiveText;
    private TextMeshProUGUI statusText;
    private Button previousButton;
    private Button nextButton;
    private Button startButton;
    private int selectedMission;
    private bool loading;

    private void Start()
    {
        Time.timeScale = 1f;
        selectedMission = Mathf.Clamp(
            GameProgress.CurrentMission,
            0,
            GameProgress.HighestUnlockedMission
        );
        BuildUI();
        Refresh();
    }

    private void Update()
    {
        if (loading || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            SelectPrevious();
        }
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            SelectNext();
        }
        else if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            StartMission();
        }
    }

    private void BuildUI()
    {
        canvas = RuntimeUIFactory.CreateCanvas("Mapa Mundial Story UI", 250);
        canvas.transform.SetParent(transform, false);

        RectTransform topBar = RuntimeUIFactory.CreatePanel(
            canvas.transform,
            "Top Bar",
            new Color(0.02f, 0.035f, 0.055f, 0.93f),
            new Vector2(0f, 0.9f),
            new Vector2(1f, 1f),
            Vector2.zero,
            Vector2.zero
        );
        RuntimeUIFactory.CreateText(
            topBar,
            "Header",
            "AGENCIA NEXO  //  MAPA DE ANOMALÍAS",
            29f,
            Color.white,
            TextAlignmentOptions.Left,
            new Vector2(0.055f, 0f),
            new Vector2(0.65f, 1f),
            Vector2.zero,
            Vector2.zero
        ).fontStyle = FontStyles.Bold;
        counterText = RuntimeUIFactory.CreateText(
            topBar,
            "Counter",
            string.Empty,
            20f,
            RuntimeUIFactory.Cyan,
            TextAlignmentOptions.Right,
            new Vector2(0.65f, 0f),
            new Vector2(0.945f, 1f),
            Vector2.zero,
            Vector2.zero
        );

        RectTransform card = RuntimeUIFactory.CreatePanel(
            canvas.transform,
            "Mission Card",
            new Color(0.035f, 0.055f, 0.085f, 0.95f),
            new Vector2(0.61f, 0.12f),
            new Vector2(0.94f, 0.84f),
            Vector2.zero,
            Vector2.zero
        );
        RuntimeUIFactory.CreatePanel(
            card,
            "Accent",
            RuntimeUIFactory.Cyan,
            new Vector2(0f, 0f),
            new Vector2(0.012f, 1f),
            Vector2.zero,
            Vector2.zero
        );

        statusText = RuntimeUIFactory.CreateText(
            card,
            "Status",
            string.Empty,
            19f,
            RuntimeUIFactory.Magenta,
            TextAlignmentOptions.Left,
            new Vector2(0.08f, 0.84f),
            new Vector2(0.92f, 0.94f),
            Vector2.zero,
            Vector2.zero
        );
        statusText.fontStyle = FontStyles.Bold;
        statusText.characterSpacing = 2f;

        titleText = RuntimeUIFactory.CreateText(
            card,
            "Title",
            string.Empty,
            40f,
            Color.white,
            TextAlignmentOptions.TopLeft,
            new Vector2(0.08f, 0.65f),
            new Vector2(0.92f, 0.84f),
            Vector2.zero,
            Vector2.zero
        );
        titleText.fontStyle = FontStyles.Bold;

        bossText = RuntimeUIFactory.CreateText(
            card,
            "Boss",
            string.Empty,
            23f,
            RuntimeUIFactory.Cyan,
            TextAlignmentOptions.TopLeft,
            new Vector2(0.08f, 0.53f),
            new Vector2(0.92f, 0.66f),
            Vector2.zero,
            Vector2.zero
        );

        objectiveText = RuntimeUIFactory.CreateText(
            card,
            "Objective",
            string.Empty,
            24f,
            RuntimeUIFactory.SoftText,
            TextAlignmentOptions.TopLeft,
            new Vector2(0.08f, 0.27f),
            new Vector2(0.92f, 0.54f),
            Vector2.zero,
            Vector2.zero
        );
        objectiveText.lineSpacing = 8f;

        previousButton = RuntimeUIFactory.CreateButton(
            card,
            "Previous",
            "◀",
            new Vector2(0.08f, 0.08f),
            new Vector2(0.22f, 0.2f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.12f, 0.16f, 0.22f, 1f)
        );
        previousButton.onClick.AddListener(SelectPrevious);

        nextButton = RuntimeUIFactory.CreateButton(
            card,
            "Next",
            "▶",
            new Vector2(0.24f, 0.08f),
            new Vector2(0.38f, 0.2f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.12f, 0.16f, 0.22f, 1f)
        );
        nextButton.onClick.AddListener(SelectNext);

        startButton = RuntimeUIFactory.CreateButton(
            card,
            "Start Mission",
            "INICIAR MISIÓN",
            new Vector2(0.46f, 0.08f),
            new Vector2(0.92f, 0.2f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.04f, 0.56f, 0.64f, 1f)
        );
        startButton.onClick.AddListener(StartMission);

        RectTransform hint = RuntimeUIFactory.CreatePanel(
            canvas.transform,
            "Map Hint",
            new Color(0.02f, 0.035f, 0.055f, 0.82f),
            new Vector2(0.055f, 0.07f),
            new Vector2(0.52f, 0.15f),
            Vector2.zero,
            Vector2.zero
        );
        RuntimeUIFactory.CreateText(
            hint,
            "Hint Text",
            "SELECCIONA UN NODO DESBLOQUEADO  //  ← → CAMBIAR  //  ENTER INICIAR",
            18f,
            new Color(0.68f, 0.76f, 0.82f, 1f),
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            new Vector2(14f, 4f),
            new Vector2(-14f, -4f)
        );
    }

    private void SelectPrevious()
    {
        int next = Mathf.Max(0, selectedMission - 1);
        if (next == selectedMission)
        {
            return;
        }

        selectedMission = next;
        GameAudioManager.Instance.PlayUI(GameSfx.StoryAdvance, 0.45f, 0.94f);
        Refresh();
    }

    private void SelectNext()
    {
        int next = Mathf.Min(GameProgress.HighestUnlockedMission, selectedMission + 1);
        if (next == selectedMission)
        {
            return;
        }

        selectedMission = next;
        GameAudioManager.Instance.PlayUI(GameSfx.StoryAdvance, 0.45f, 1.06f);
        Refresh();
    }

    private void Refresh()
    {
        MissionStory mission = GameStoryDatabase.Get(selectedMission);
        bool completed = GameProgress.HasFragment(selectedMission);
        counterText.text = $"NODOS ABIERTOS: {GameProgress.HighestUnlockedMission + 1:00}  //  SELECCIÓN {selectedMission + 1:00}";
        statusText.text = completed
            ? $"FRAGMENTO RECUPERADO // {mission.threatLevel}"
            : $"FALLA ACTIVA // {mission.threatLevel}";
        statusText.color = completed ? RuntimeUIFactory.Cyan : RuntimeUIFactory.Magenta;
        titleText.text = mission.DisplayTitle;
        bossText.text = $"OBJETIVO HOSTIL:  {mission.bossName}";
        objectiveText.text = mission.objective;
        previousButton.interactable = selectedMission > 0;
        nextButton.interactable = selectedMission < GameProgress.HighestUnlockedMission;
        startButton.GetComponentInChildren<TextMeshProUGUI>().text = completed
            ? "REPETIR MISIÓN"
            : "INICIAR MISIÓN";
    }

    private void StartMission()
    {
        if (loading)
        {
            return;
        }

        loading = true;
        GameProgress.CurrentMission = selectedMission;
        GameAudioManager.Instance.PlayUI(GameSfx.MissionConfirm, 0.9f, 1f);
        GameAudioManager.Instance.PlayAt(GameSfx.MissionStart, Vector3.zero, 0.82f, 0.98f, 1.02f);
        SceneManager.LoadSceneAsync(FlujoEscenas.Mision);
    }
}
