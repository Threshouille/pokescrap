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
        public bool unlockAll; // tests: every playable Pokémon can be picked, whatever the save says
        public float recruitMultiplier = 1f;
        public float recruitMessageTime = 3f;
        public int startingMoveCount = 2;
        public float enemyBasicCooldownMul = 2.5f; // enemies fire their basic attack less often...
        public float enemyBasicDamageMul = 1.5f; // ...but it hits harder
        public int enemyMoveCount = 2;
        public float moveOfferChanceWhenFull = 0.5f; // chance that a level-up choice offers a new move once 4 are known
        public float evolvedMoveWeight = 2f; // after evolving, moves of the current form are this much more likely
        public float dodgeDistance = 2.5f; // tiles covered by a dodge (team only)
        public float dodgeTime = 0.2f;
        public float meleeStun = 0.4f; // seconds a melee hit stuns its target (bosses are immune)
        public float dodgeCooldown = 3f;
        public float enemyDodgeCooldown = 5f;
        public int enemyGroupSize = 3; // enemies of a wave are split into groups that share a target, each with a support
        public float groupRetargetTime = 2f;
        // Type matchups: multipliers replacing the chart's ×2 / ×0.5 / ×0, clamped once the defender's types are combined.
        public float superEffective = 1.5f, notVeryEffective = 0.6f, noEffect = 0.25f;
        public float typeFactorMin = 0.2f, typeFactorMax = 2f;
        public float stab = 1.25f; // move of one of the user's types
        // Damage numbers.
        public string normalDamageColor = "#ffffff", superEffectiveColor = "#ffb020", notVeryEffectiveColor = "#8ca4c8";
        public string noEffectColor = "#5a5a66", healColor = "#60e070";
        public string multiplierColor = "#ffb020"; // damage multiplier of chained strikes (Taillade)
        public float bigNumberScale = 1.5f, smallNumberScale = 0.75f;
        // Evolution (Evolution.cs).
        public float evolveTime = 2.5f; // silhouettes alternate, the Pokémon stands still and cannot be hurt
        public float shockwaveRadius = 5f, shockwaveKnockback = 2f; // tiles
        public float shockwaveDamage = 15f, shockwaveDamagePerLevel = 1f;
        public float iqPerSecond = 1f; // while controlled by the player
        public float beautyPerSecond = 1f, beautyPerHit = 10f, beautyThreshold = 100f;
        public float evolutionCardChance = 0.5f; // an item or trade card replaces an upgrade, when the Pokémon can use one
        public float statTieMargin = 0.1f; // Attack and Defence within this share count as equal (Debugant)
        public string nightColor = "#0a1440";
        public float nightAlpha = 0.3f;
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
            data ??= new File();
            // Starting Pokémon added to meta.json after the save was made are unlocked too.
            foreach (int id in Settings.startingUnlocked)
                if (!data.unlocked.Contains(id)) data.unlocked.Add(id);
            return data;
        }
    }

    public static IReadOnlyList<int> Unlocked => Data.unlocked;
    public static int[] LastTeam => Data.lastTeam;
    public static int Runs => Data.runs;
    public static int Wins => Data.wins;

    public static bool IsUnlocked(int pokemonId) => Settings.unlockAll || Data.unlocked.Contains(pokemonId);

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
