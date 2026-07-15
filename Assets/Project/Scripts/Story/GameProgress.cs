using UnityEngine;

/// <summary>
/// Progreso minimo persistente para el flujo de misiones y fragmentos.
/// </summary>
public static class GameProgress
{
    private const string CurrentMissionKey = "FH_CurrentMission";
    private const string UnlockedMissionKey = "FH_UnlockedMission";
    private const string PrologueSeenKey = "FH_PrologueSeen";

    public static int CurrentMission
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(CurrentMissionKey, 0), 0, GameStoryDatabase.Count - 1);
        set
        {
            PlayerPrefs.SetInt(CurrentMissionKey, Mathf.Clamp(value, 0, GameStoryDatabase.Count - 1));
            PlayerPrefs.Save();
        }
    }

    public static int HighestUnlockedMission => Mathf.Clamp(
        PlayerPrefs.GetInt(UnlockedMissionKey, 0),
        0,
        GameStoryDatabase.Count - 1
    );

    public static bool PrologueSeen => PlayerPrefs.GetInt(PrologueSeenKey, 0) == 1;

    public static void MarkPrologueSeen()
    {
        PlayerPrefs.SetInt(PrologueSeenKey, 1);
        PlayerPrefs.Save();
    }

    public static void CompleteMission(int missionIndex)
    {
        int next = Mathf.Clamp(missionIndex + 1, 0, GameStoryDatabase.Count - 1);
        int unlocked = Mathf.Max(HighestUnlockedMission, next);
        PlayerPrefs.SetInt(UnlockedMissionKey, unlocked);
        PlayerPrefs.SetInt(CurrentMissionKey, next);
        PlayerPrefs.SetInt($"FH_Fragment_{missionIndex}", 1);
        PlayerPrefs.Save();
    }

    public static bool HasFragment(int missionIndex)
    {
        return PlayerPrefs.GetInt($"FH_Fragment_{missionIndex}", 0) == 1;
    }

    public static void ResetProgress()
    {
        PlayerPrefs.SetInt(CurrentMissionKey, 0);
        PlayerPrefs.SetInt(UnlockedMissionKey, 0);
        PlayerPrefs.SetInt(PrologueSeenKey, 0);
        for (int i = 0; i < GameStoryDatabase.Count; i++)
        {
            PlayerPrefs.DeleteKey($"FH_Fragment_{i}");
        }
        PlayerPrefs.Save();
    }
}
