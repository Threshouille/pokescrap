using UnityEngine;
using UnityEngine.SceneManagement;

/// Hands the chosen team from the menu to the arena, and moves between the two scenes.
public static class Run
{
    public const string MenuScene = "Menu", ArenaScene = "SampleScene";

    /// Pokémon ids picked in the menu; null when the arena scene is started directly (random team).
    public static int[] Team;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => Team = null;

    public static void Start(int[] team)
    {
        Team = team;
        SaveData.SetLastTeam(team);
        SceneManager.LoadScene(ArenaScene);
    }

    public static void Restart() => SceneManager.LoadScene(ArenaScene);

    public static void ToMenu() => SceneManager.LoadScene(MenuScene);
}
