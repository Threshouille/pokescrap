using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// What survives between runs: unlocked Pokémon, last team, run counters. Stored as JSON in the persistent data
/// folder and written as soon as something changes. Starting values come from Resources/Data/meta.json.
public static class SaveData
{
    const int Version = 1;

    [Serializable]
    class MoveChoice
    {
        public int pokemonId;
        public int[] moves;
    }

    [Serializable]
    class File
    {
        public int version = Version;
        public List<int> unlocked = new();
        public int[] lastTeam = new int[0];
        public List<MoveChoice> startingMoves = new();
        public int runs, wins;
    }

    [Serializable]
    public class Meta
    {
        public int[] startingUnlocked = new int[0];
        public float recruitMultiplier = 1f;
        public float recruitMessageTime = 3f;
        public int startingMoveCount = 2;
    }

    static File data;
    static Meta meta;

    public static string Path => System.IO.Path.Combine(Application.persistentDataPath, "save.json");

    // Statics can outlive a play session in the editor.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { data = null; meta = null; }

    public static Meta Settings
    {
        get
        {
            if (meta == null)
            {
                var asset = Resources.Load<TextAsset>("Data/meta");
                meta = asset != null ? JsonUtility.FromJson<Meta>(asset.text) : new Meta();
            }
            return meta;
        }
    }

    static File Data
    {
        get
        {
            if (data != null) return data;
            try
            {
                if (System.IO.File.Exists(Path)) data = JsonUtility.FromJson<File>(System.IO.File.ReadAllText(Path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Unreadable save, starting over: {e.Message}");
            }
            if (data == null)
            {
                data = new File();
                data.unlocked.AddRange(Settings.startingUnlocked);
            }
            return data;
        }
    }

    public static IReadOnlyList<int> Unlocked => Data.unlocked;
    public static int[] LastTeam => Data.lastTeam;
    public static int Runs => Data.runs;
    public static int Wins => Data.wins;

    public static bool IsUnlocked(int pokemonId) => Data.unlocked.Contains(pokemonId);

    public static void Unlock(int pokemonId)
    {
        if (IsUnlocked(pokemonId)) return;
        Data.unlocked.Add(pokemonId);
        Write();
    }

    public static void SetLastTeam(int[] team)
    {
        Data.lastTeam = team;
        Write();
    }

    /// Moves a Pokémon starts a run with: the player's choice if it is still valid, else the default ones.
    public static int[] StartingMoves(int pokemonId)
    {
        int count = Settings.startingMoveCount;
        var pool = GameData.MovePool(pokemonId).ConvertAll(m => m.id);
        var choice = Data.startingMoves.Find(c => c.pokemonId == pokemonId);
        if (choice != null && choice.moves.Length == Math.Min(count, pool.Count) && Array.TrueForAll(choice.moves, pool.Contains))
            return choice.moves;
        return GameData.DefaultStartingMoves(pokemonId, count);
    }

    public static void SetStartingMoves(int pokemonId, int[] moves)
    {
        Data.startingMoves.RemoveAll(c => c.pokemonId == pokemonId);
        Data.startingMoves.Add(new MoveChoice { pokemonId = pokemonId, moves = moves });
        Write();
    }

    public static void RecordRun(bool won)
    {
        Data.runs++;
        if (won) Data.wins++;
        Write();
    }

    /// Back to the starting state (editor menu and tests).
    public static void Erase()
    {
        if (System.IO.File.Exists(Path)) System.IO.File.Delete(Path);
        data = null;
    }

    static void Write()
    {
        try
        {
            Data.version = Version;
            System.IO.File.WriteAllText(Path, JsonUtility.ToJson(Data, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not write the save: {e.Message}");
        }
    }
}
