using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameSfx
{
    UiHover,
    UiClick,
    StoryAdvance,
    MissionConfirm,
    PlayerFootstep,
    PlayerJump,
    PlayerLand,
    PlayerSwing,
    PlayerHurt,
    PlayerDeath,
    BossFootstep,
    BossAttack,
    BossHurt,
    BossDeath,
    BossRoar,
    Impact,
    GlitchPulse,
    MissionStart,
    BossDefeated
}

public enum GameMusic
{
    None,
    Prologue,
    WorldMap,
    Battle
}

public enum GameAmbience
{
    None,
    Prologue,
    WorldMap,
    Mission
}

/// <summary>
/// Sistema central y persistente de audio. Carga los clips desde Resources,
/// evita AudioSources duplicados y mantiene musica/ambiente entre escenas.
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class GameAudioManager : MonoBehaviour
{
    private const int PoolSize = 14;
    private static GameAudioManager instance;

    private readonly Dictionary<GameSfx, AudioClip[]> sfxCache = new Dictionary<GameSfx, AudioClip[]>();
    private readonly List<AudioSource> worldSources = new List<AudioSource>();

    private AudioSource musicSource;
    private AudioSource ambienceSource;
    private AudioSource uiSource;
    private int nextWorldSource;
    private Coroutine musicRoutine;
    private Coroutine ambienceRoutine;

    public static GameAudioManager Instance
    {
        get
        {
            EnsureInstance();
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        instance = FindFirstObjectByType<GameAudioManager>();
        if (instance != null)
        {
            return;
        }

        GameObject root = new GameObject("[Game Audio]");
        instance = root.AddComponent<GameAudioManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        CreateSources();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        ConfigureSceneAudio(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    private void CreateSources()
    {
        musicSource = Create2DSource("Music", true, 0.58f);
        ambienceSource = Create2DSource("Ambience", true, 0.34f);
        uiSource = Create2DSource("UI", false, 0.8f);

        for (int i = 0; i < PoolSize; i++)
        {
            GameObject child = new GameObject($"World SFX {i + 1:00}");
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = 34f;
            source.dopplerLevel = 0.15f;
            worldSources.Add(source);
        }
    }

    private AudioSource Create2DSource(string sourceName, bool loop, float volume)
    {
        GameObject child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.volume = volume;
        return source;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ConfigureSceneAudio(scene.name);
    }

    private void ConfigureSceneAudio(string sceneName)
    {
        switch (sceneName)
        {
            case FlujoEscenas.Prologo:
                StopLegacyMenuMusic();
                PlayMusic(GameMusic.Prologue);
                PlayAmbience(GameAmbience.Prologue);
                break;
            case FlujoEscenas.MapaMundial:
                StopLegacyMenuMusic();
                PlayMusic(GameMusic.WorldMap);
                PlayAmbience(GameAmbience.WorldMap);
                break;
            case FlujoEscenas.Mision:
                StopLegacyMenuMusic();
                PlayMusic(GameMusic.Battle);
                PlayAmbience(GameAmbience.Mission);
                break;
            case FlujoEscenas.MenuPrincipal:
                StopMusic(0.35f);
                StopAmbience(0.35f);
                break;
            case FlujoEscenas.Opciones:
                // Conserva la musica del origen mientras se muestran opciones.
                break;
        }
    }

    private static void StopLegacyMenuMusic()
    {
        MusicaPersistente[] legacyMusic = FindObjectsByType<MusicaPersistente>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (MusicaPersistente legacy in legacyMusic)
        {
            if (legacy != null)
            {
                Destroy(legacy.gameObject);
            }
        }
    }

    public void PlayUI(GameSfx cue, float volume = 1f, float pitch = 1f)
    {
        AudioClip clip = GetRandomClip(cue);
        if (clip == null || uiSource == null)
        {
            return;
        }

        uiSource.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        uiSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    public void PlayAt(
        GameSfx cue,
        Vector3 position,
        float volume = 1f,
        float minPitch = 0.96f,
        float maxPitch = 1.04f
    )
    {
        AudioClip clip = GetRandomClip(cue);
        if (clip == null || worldSources.Count == 0)
        {
            return;
        }

        AudioSource source = GetAvailableWorldSource();
        source.transform.position = position;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = Random.Range(minPitch, maxPitch);
        source.Play();
    }

    public void PlayMusic(GameMusic track, float fadeDuration = 0.8f)
    {
        AudioClip clip = LoadMusic(track);
        if (clip == null)
        {
            return;
        }

        if (musicSource.clip == clip && musicSource.isPlaying)
        {
            return;
        }

        if (musicRoutine != null)
        {
            StopCoroutine(musicRoutine);
        }

        musicRoutine = StartCoroutine(
            CrossFade(musicSource, clip, 0.58f, Mathf.Max(0.01f, fadeDuration))
        );
    }

    public void PlayAmbience(GameAmbience ambience, float fadeDuration = 0.8f)
    {
        AudioClip clip = LoadAmbience(ambience);
        if (clip == null)
        {
            return;
        }

        if (ambienceSource.clip == clip && ambienceSource.isPlaying)
        {
            return;
        }

        if (ambienceRoutine != null)
        {
            StopCoroutine(ambienceRoutine);
        }

        ambienceRoutine = StartCoroutine(
            CrossFade(ambienceSource, clip, 0.34f, Mathf.Max(0.01f, fadeDuration))
        );
    }

    public void StopMusic(float fadeDuration = 0.5f)
    {
        if (musicRoutine != null)
        {
            StopCoroutine(musicRoutine);
        }

        musicRoutine = StartCoroutine(FadeOut(musicSource, fadeDuration));
    }

    public void StopAmbience(float fadeDuration = 0.5f)
    {
        if (ambienceRoutine != null)
        {
            StopCoroutine(ambienceRoutine);
        }

        ambienceRoutine = StartCoroutine(FadeOut(ambienceSource, fadeDuration));
    }

    private AudioSource GetAvailableWorldSource()
    {
        for (int i = 0; i < worldSources.Count; i++)
        {
            int index = (nextWorldSource + i) % worldSources.Count;
            if (!worldSources[index].isPlaying)
            {
                nextWorldSource = (index + 1) % worldSources.Count;
                return worldSources[index];
            }
        }

        AudioSource fallback = worldSources[nextWorldSource];
        nextWorldSource = (nextWorldSource + 1) % worldSources.Count;
        return fallback;
    }

    private AudioClip GetRandomClip(GameSfx cue)
    {
        if (!sfxCache.TryGetValue(cue, out AudioClip[] clips))
        {
            clips = LoadSfx(cue);
            sfxCache[cue] = clips;
        }

        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning($"No se encontraron clips para {cue}.", this);
            return null;
        }

        return clips[Random.Range(0, clips.Length)];
    }

    private static AudioClip[] LoadSfx(GameSfx cue)
    {
        return cue switch
        {
            GameSfx.UiHover => One("Audio/SFX/UI/ui_hover"),
            GameSfx.UiClick => One("Audio/SFX/UI/ui_click"),
            GameSfx.StoryAdvance => One("Audio/SFX/UI/story_advance"),
            GameSfx.MissionConfirm => One("Audio/SFX/UI/mission_confirm"),
            GameSfx.PlayerFootstep => Resources.LoadAll<AudioClip>("Audio/SFX/Player/Footsteps"),
            GameSfx.PlayerJump => One("Audio/SFX/Player/jump"),
            GameSfx.PlayerLand => One("Audio/SFX/Player/land"),
            GameSfx.PlayerSwing => Resources.LoadAll<AudioClip>("Audio/SFX/Player").FilterPrefix("swing_"),
            GameSfx.PlayerHurt => Resources.LoadAll<AudioClip>("Audio/SFX/Player").FilterPrefix("hurt_"),
            GameSfx.PlayerDeath => One("Audio/SFX/Player/death"),
            GameSfx.BossFootstep => Resources.LoadAll<AudioClip>("Audio/SFX/Boss/Footsteps"),
            GameSfx.BossAttack => Resources.LoadAll<AudioClip>("Audio/SFX/Boss").FilterPrefix("attack_"),
            GameSfx.BossHurt => One("Audio/SFX/Boss/hurt"),
            GameSfx.BossDeath => One("Audio/SFX/Boss/death"),
            GameSfx.BossRoar => One("Audio/SFX/Boss/roar"),
            GameSfx.Impact => One("Audio/SFX/World/impact"),
            GameSfx.GlitchPulse => One("Audio/SFX/World/glitch_pulse"),
            GameSfx.MissionStart => One("Audio/SFX/World/mission_start"),
            GameSfx.BossDefeated => One("Audio/SFX/World/boss_defeated"),
            _ => System.Array.Empty<AudioClip>()
        };
    }

    private static AudioClip[] One(string resourcePath)
    {
        AudioClip clip = Resources.Load<AudioClip>(resourcePath);
        return clip == null ? System.Array.Empty<AudioClip>() : new[] { clip };
    }

    private static AudioClip LoadMusic(GameMusic track)
    {
        string path = track switch
        {
            GameMusic.Prologue => "Audio/Music/prologue_theme",
            GameMusic.WorldMap => "Audio/Music/map_theme",
            GameMusic.Battle => "Audio/Music/battle_theme",
            _ => null
        };
        return string.IsNullOrWhiteSpace(path) ? null : Resources.Load<AudioClip>(path);
    }

    private static AudioClip LoadAmbience(GameAmbience ambience)
    {
        string path = ambience switch
        {
            GameAmbience.Prologue => "Audio/Ambience/prologue_ambience",
            GameAmbience.WorldMap => "Audio/Ambience/map_ambience",
            GameAmbience.Mission => "Audio/Ambience/mission_ambience",
            _ => null
        };
        return string.IsNullOrWhiteSpace(path) ? null : Resources.Load<AudioClip>(path);
    }

    private IEnumerator CrossFade(AudioSource source, AudioClip nextClip, float targetVolume, float duration)
    {
        if (source.isPlaying && source.clip != null)
        {
            float startVolume = source.volume;
            for (float elapsed = 0f; elapsed < duration * 0.5f; elapsed += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(startVolume, 0f, elapsed / (duration * 0.5f));
                yield return null;
            }
        }

        source.Stop();
        source.clip = nextClip;
        source.volume = 0f;
        source.Play();

        for (float elapsed = 0f; elapsed < duration * 0.5f; elapsed += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(0f, targetVolume, elapsed / (duration * 0.5f));
            yield return null;
        }

        source.volume = targetVolume;
    }

    private static IEnumerator FadeOut(AudioSource source, float duration)
    {
        if (source == null || !source.isPlaying)
        {
            yield break;
        }

        float start = source.volume;
        float safeDuration = Mathf.Max(0.01f, duration);
        for (float elapsed = 0f; elapsed < safeDuration; elapsed += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(start, 0f, elapsed / safeDuration);
            yield return null;
        }

        source.Stop();
        source.clip = null;
        source.volume = start;
    }
}

internal static class AudioClipArrayExtensions
{
    public static AudioClip[] FilterPrefix(this AudioClip[] clips, string prefix)
    {
        if (clips == null || clips.Length == 0)
        {
            return System.Array.Empty<AudioClip>();
        }

        List<AudioClip> filtered = new List<AudioClip>();
        foreach (AudioClip clip in clips)
        {
            if (clip != null && clip.name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            {
                filtered.Add(clip);
            }
        }

        return filtered.ToArray();
    }
}
