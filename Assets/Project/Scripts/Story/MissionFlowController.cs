using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controla el briefing, el inicio del combate y la revelacion del fragmento
/// al derrotar a la Falla. Se instala automaticamente en SampleScene.
/// </summary>
[DisallowMultipleComponent]
public sealed class MissionFlowController : MonoBehaviour
{
    private MissionStory mission;
    private Canvas canvas;
    private RectTransform briefingPanel;
    private RectTransform victoryPanel;
    private CanvasGroup briefingGroup;
    private CanvasGroup victoryGroup;
    private MutantStats[] bosses;
    private bool missionStarted;
    private bool victoryShown;
    private bool loading;

    private void Start()
    {
        mission = GameStoryDatabase.Get(GameProgress.CurrentMission);
        BuildUI();
        SubscribeBosses();
        ShowBriefing();
    }

    private void Update()
    {
        if (loading || Keyboard.current == null)
        {
            return;
        }

        if (!missionStarted && Keyboard.current.enterKey.wasPressedThisFrame)
        {
            StartMission();
        }
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        UnsubscribeBosses();
    }

    private void SubscribeBosses()
    {
        bosses = FindObjectsByType<MutantStats>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MutantStats boss in bosses)
        {
            if (boss != null)
            {
                boss.OnDeath += OnBossDefeated;
            }
        }
    }

    private void UnsubscribeBosses()
    {
        if (bosses == null)
        {
            return;
        }

        foreach (MutantStats boss in bosses)
        {
            if (boss != null)
            {
                boss.OnDeath -= OnBossDefeated;
            }
        }
    }

    private void BuildUI()
    {
        canvas = RuntimeUIFactory.CreateCanvas("Mission Story UI", 450);
        canvas.transform.SetParent(transform, false);

        briefingPanel = RuntimeUIFactory.CreatePanel(
            canvas.transform,
            "Briefing Overlay",
            new Color(0.015f, 0.025f, 0.04f, 0.97f),
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        briefingGroup = briefingPanel.gameObject.AddComponent<CanvasGroup>();

        RectTransform briefingCard = RuntimeUIFactory.CreatePanel(
            briefingPanel,
            "Briefing Card",
            new Color(0.04f, 0.06f, 0.09f, 0.97f),
            new Vector2(0.16f, 0.13f),
            new Vector2(0.84f, 0.87f),
            Vector2.zero,
            Vector2.zero
        );
        RuntimeUIFactory.CreatePanel(
            briefingCard,
            "Accent",
            RuntimeUIFactory.Magenta,
            new Vector2(0f, 0f),
            new Vector2(0.007f, 1f),
            Vector2.zero,
            Vector2.zero
        );

        TextMeshProUGUI tag = RuntimeUIFactory.CreateText(
            briefingCard,
            "Tag",
            $"INFORME DE CAMPO // {mission.threatLevel}",
            20f,
            RuntimeUIFactory.Magenta,
            TextAlignmentOptions.Left,
            new Vector2(0.07f, 0.84f),
            new Vector2(0.93f, 0.93f),
            Vector2.zero,
            Vector2.zero
        );
        tag.fontStyle = FontStyles.Bold;
        tag.characterSpacing = 2f;

        TextMeshProUGUI title = RuntimeUIFactory.CreateText(
            briefingCard,
            "Title",
            mission.DisplayTitle,
            50f,
            Color.white,
            TextAlignmentOptions.TopLeft,
            new Vector2(0.07f, 0.66f),
            new Vector2(0.93f, 0.84f),
            Vector2.zero,
            Vector2.zero
        );
        title.fontStyle = FontStyles.Bold;

        RuntimeUIFactory.CreateText(
            briefingCard,
            "Target",
            $"FALLA IDENTIFICADA:  <color=#23EAFD>{mission.bossName}</color>\nOBJETIVO:  {mission.objective}",
            24f,
            RuntimeUIFactory.SoftText,
            TextAlignmentOptions.TopLeft,
            new Vector2(0.07f, 0.48f),
            new Vector2(0.93f, 0.65f),
            Vector2.zero,
            Vector2.zero
        ).lineSpacing = 6f;

        RuntimeUIFactory.CreateText(
            briefingCard,
            "Briefing",
            mission.briefing,
            25f,
            new Color(0.74f, 0.82f, 0.88f, 1f),
            TextAlignmentOptions.TopLeft,
            new Vector2(0.07f, 0.2f),
            new Vector2(0.93f, 0.47f),
            Vector2.zero,
            Vector2.zero
        ).lineSpacing = 8f;

        Button beginButton = RuntimeUIFactory.CreateButton(
            briefingCard,
            "Begin",
            "INICIAR CACERÍA  [ENTER]",
            new Vector2(0.61f, 0.07f),
            new Vector2(0.93f, 0.17f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.62f, 0.06f, 0.33f, 1f)
        );
        beginButton.onClick.AddListener(StartMission);

        Button abortButton = RuntimeUIFactory.CreateButton(
            briefingCard,
            "Return",
            "VOLVER AL MAPA",
            new Vector2(0.07f, 0.07f),
            new Vector2(0.29f, 0.17f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.13f, 0.16f, 0.21f, 1f)
        );
        abortButton.onClick.AddListener(ReturnToMap);

        victoryPanel = RuntimeUIFactory.CreatePanel(
            canvas.transform,
            "Victory Overlay",
            new Color(0.015f, 0.025f, 0.04f, 0.96f),
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );
        victoryGroup = victoryPanel.gameObject.AddComponent<CanvasGroup>();

        RectTransform victoryCard = RuntimeUIFactory.CreatePanel(
            victoryPanel,
            "Victory Card",
            new Color(0.035f, 0.065f, 0.08f, 0.98f),
            new Vector2(0.16f, 0.16f),
            new Vector2(0.84f, 0.84f),
            Vector2.zero,
            Vector2.zero
        );
        RuntimeUIFactory.CreatePanel(
            victoryCard,
            "Accent",
            RuntimeUIFactory.Cyan,
            new Vector2(0f, 0f),
            new Vector2(0.007f, 1f),
            Vector2.zero,
            Vector2.zero
        );

        TextMeshProUGUI complete = RuntimeUIFactory.CreateText(
            victoryCard,
            "Complete",
            "FALLA CONTENIDA",
            21f,
            RuntimeUIFactory.Cyan,
            TextAlignmentOptions.Left,
            new Vector2(0.07f, 0.84f),
            new Vector2(0.93f, 0.93f),
            Vector2.zero,
            Vector2.zero
        );
        complete.fontStyle = FontStyles.Bold;
        complete.characterSpacing = 3f;

        TextMeshProUGUI victoryTitle = RuntimeUIFactory.CreateText(
            victoryCard,
            "Victory Title",
            $"FRAGMENTO {mission.index + 1:00} RECUPERADO",
            48f,
            Color.white,
            TextAlignmentOptions.TopLeft,
            new Vector2(0.07f, 0.68f),
            new Vector2(0.93f, 0.84f),
            Vector2.zero,
            Vector2.zero
        );
        victoryTitle.fontStyle = FontStyles.Bold;

        RuntimeUIFactory.CreateText(
            victoryCard,
            "Fragment",
            mission.recoveredFragment,
            27f,
            new Color(0.62f, 0.95f, 1f, 1f),
            TextAlignmentOptions.TopLeft,
            new Vector2(0.07f, 0.47f),
            new Vector2(0.93f, 0.67f),
            Vector2.zero,
            Vector2.zero
        ).fontStyle = FontStyles.Bold;

        RuntimeUIFactory.CreateText(
            victoryCard,
            "Conclusion",
            mission.conclusion,
            25f,
            RuntimeUIFactory.SoftText,
            TextAlignmentOptions.TopLeft,
            new Vector2(0.07f, 0.2f),
            new Vector2(0.93f, 0.46f),
            Vector2.zero,
            Vector2.zero
        ).lineSpacing = 8f;

        Button continueButton = RuntimeUIFactory.CreateButton(
            victoryCard,
            "Continue",
            mission.index < GameStoryDatabase.Count - 1
                ? "DESBLOQUEAR SIGUIENTE NODO"
                : "VOLVER AL MAPA",
            new Vector2(0.55f, 0.07f),
            new Vector2(0.93f, 0.17f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.04f, 0.56f, 0.64f, 1f)
        );
        continueButton.onClick.AddListener(CompleteAndReturn);

        Button replayButton = RuntimeUIFactory.CreateButton(
            victoryCard,
            "Replay",
            "REPETIR MISIÓN",
            new Vector2(0.07f, 0.07f),
            new Vector2(0.29f, 0.17f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.13f, 0.16f, 0.21f, 1f)
        );
        replayButton.onClick.AddListener(ReloadMission);

        victoryPanel.gameObject.SetActive(false);
    }

    private void ShowBriefing()
    {
        Time.timeScale = 0f;
        briefingPanel.gameObject.SetActive(true);
        briefingGroup.alpha = 1f;
        GameAudioManager.Instance.PlayAt(GameSfx.GlitchPulse, Vector3.zero, 0.4f, 0.94f, 1.06f);
    }

    private void StartMission()
    {
        if (missionStarted || loading)
        {
            return;
        }

        missionStarted = true;
        Time.timeScale = 1f;
        GameAudioManager.Instance.PlayUI(GameSfx.MissionConfirm, 0.85f, 1f);
        GameAudioManager.Instance.PlayAt(GameSfx.MissionStart, Vector3.zero, 0.9f, 0.98f, 1.02f);
        StartCoroutine(HideBriefingRoutine());
    }

    private IEnumerator HideBriefingRoutine()
    {
        for (float elapsed = 0f; elapsed < 0.35f; elapsed += Time.unscaledDeltaTime)
        {
            briefingGroup.alpha = 1f - elapsed / 0.35f;
            yield return null;
        }
        briefingPanel.gameObject.SetActive(false);
    }

    private void OnBossDefeated()
    {
        if (victoryShown)
        {
            return;
        }

        victoryShown = true;
        StartCoroutine(ShowVictoryRoutine());
    }

    private IEnumerator ShowVictoryRoutine()
    {
        yield return new WaitForSecondsRealtime(2.1f);
        Time.timeScale = 0f;
        victoryPanel.gameObject.SetActive(true);
        victoryGroup.alpha = 0f;
        for (float elapsed = 0f; elapsed < 0.55f; elapsed += Time.unscaledDeltaTime)
        {
            victoryGroup.alpha = elapsed / 0.55f;
            yield return null;
        }
        victoryGroup.alpha = 1f;
    }

    private void CompleteAndReturn()
    {
        if (loading)
        {
            return;
        }

        GameProgress.CompleteMission(mission.index);
        ReturnToMap();
    }

    private void ReturnToMap()
    {
        if (loading)
        {
            return;
        }

        loading = true;
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(FlujoEscenas.MapaMundial);
    }

    private void ReloadMission()
    {
        if (loading)
        {
            return;
        }

        loading = true;
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(FlujoEscenas.Mision);
    }
}
