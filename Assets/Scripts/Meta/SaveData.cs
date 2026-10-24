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
    class TypeLevel
    {
        public int type, level;
    }

    /// The special items a Pokémon carries (one slot per family) and the one active for a run.
    [Serializable]
    public class Loadout
    {
        public int pokemonId;
        public string mega = ""; // mega stone key ("rayquaza": none needed)
        public int z = -1, tera = -1; // types
        public string active = ""; // "mega", "z", "tera" or ""
    }

    [Serializable]
    class File
    {
        public int version = Version;
        public List<int> unlocked = new();
        public int[] lastTeam = new int[0];
        public List<MoveChoice> startingMoves = new();
        public int runs, wins;
        // Special items: mega stones owned (unique), Z crystals and Tera shards by type with their level.
        public List<string> megaStones = new();
        public List<TypeLevel> zCrystals = new(), teraShards = new();
        public List<Loadout> loadouts = new();
    }

    [Serializable]
    public class Meta
    {
        public int[] startingUnlocked = new int[0];
        public bool unlockAll; // tests: every playable Pokémon can be picked, whatever the save says
        public bool allItems; // tests: every special item owned (level 1 at least), whatever the save says
        public float teraStab = 2f; // Terastal: damage of a move of the Tera type when it was already one of the Pokémon's types
        public bool devMode; // tests: the dev characters of dev_characters.json are offered in the menu
        public bool obstaclesInRuns = true; // real arenas get the obstacles of their décor (arena_obstacles.json)
        public int devStartingMoveCount = 4; // starting moves of a dev character
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
        public float aimCone = 45f; // degrees either side of the aim within which the player's target is picked
        public float aimRange = 8f; // tiles
        public float aimDistanceWeight = 12f; // degrees of aim one tile of distance is worth when picking the target
        public float aimKeepCone = 60f; // the current target is kept while it stays within this cone
        public float meleeStun = 0.4f; // seconds a melee hit stuns its target (bosses are immune)
        public float dodgeCooldown = 3f;
        public float enemyDodgeCooldown = 5f;
        public int enemyGroupSize = 3; // enemies of a wave are split into groups that share a target, each with a support
        public float groupRetargetTime = 2f;
        // Target choice of the AIs (TargetChoice.cs), allies and enemies alike: score of an opponent.
        public float aiTargetDistance = 0.25f; // lost per tile of distance
        public float aiTargetFinish = 3f; // gained when the AI's best move knocks it out in one hit (divided by the hits needed)
        public float aiTargetEfficacy = 1f; // per doubling of the AI's best type multiplier against it
        public float aiTargetThreat = 1f; // when it is close to a team-mate its moves hit hard (more when that one is hurt)
        public float aiTargetFocus = 0.5f; // per team-mate already after it
        public float aiTargetKeep = 1f; // for the current target, so that the AI does not switch back and forth
        public float aiTargetTaunt = 3f; // for a team member guarding a knocked-out team-mate (TeamRescue): it draws the opponents near it
        // Allies (AllyAI.cs).
        public float allyAggroRange = 9f; // opponents farther than this are ignored
        public float allyLeash = 9f; // beyond this distance from the leader, an ally drops the fight and comes back
        public float allyFollowBreak = 4f; // the leader walking away beyond this distance: the allies follow
        public float allyLeaderRetreatSpeed = 1.5f; // tiles per second away from the ally that count as walking away
        public float allyRetreatHealth = 0.3f, allyRetreatRecover = 0.5f; // hurt: keeps close to the leader until healed
        public float allyCautiousAggro = 4f; // opponents a hurt ally still fights
        // Rescue (TeamRescue.cs): opponents this close to a knocked-out team member are drawn away from it by a guard.
        public float allyGuardRadius = 6f;
        public float allyLureDistance = 3f; // ranged guard: how far beyond the opponent it stands, to lead it away
        // Heals (RoleTactics): a Pokémon that is not a support walks to heal a team-mate under allyHealLow of its health;
        // nobody walks to heal when under allyHealSafeHealth with an opponent within allyHealSafeGap tiles of where it would stand.
        public float allyHealLow = 0.4f, allyHealSafeHealth = 0.5f, allyHealSafeGap = 3f;
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
        // Ultimates (Ultimates.cs: mega evolution, Z move, Terastal): a gauge shared by the team, filled by knocking enemies out.
        public float megaKillsToFill = 25f; // enemies knocked out to fill the gauge
        public float megaBossFill = 0.5f; // share of the gauge a boss gives
        public float megaTime = 50f; // seconds before the Pokémon goes back to its form
        public float megaCastTime = 1.5f; // silhouettes alternate, the Pokémon stands still and cannot be hurt
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

    public static bool IsUnlocked(int pokemonId) => Settings.unlockAll || Data.unlocked.Contains(pokemonId) || GameData.IsDev(pokemonId);

    /// Number of moves a Pokémon starts a run with.
    public static int StartingMoveCount(int pokemonId) => GameData.IsDev(pokemonId) ? Settings.devStartingMoveCount : Settings.startingMoveCount;

    public static void Unlock(int pokemonId)
    {
        if (IsUnlocked(pokemonId) || GameData.IsDev(pokemonId)) return;
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
        int count = StartingMoveCount(pokemonId);
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

    // ---- Special items (SpecialItems.cs) ----

    public static bool HasStone(string key) => Settings.allItems || Data.megaStones.Contains(key);
    public static int ZLevel(int type) => LevelOf(Data.zCrystals, type);
    public static int TeraLevel(int type) => LevelOf(Data.teraShards, type);

    static int LevelOf(List<TypeLevel> list, int type)
    {
        int level = list.Find(t => t.type == type)?.level ?? 0;
        return Settings.allItems ? Mathf.Max(1, level) : level;
    }

    /// A mega stone found: false when it was owned already (mega stones are unique).
    public static bool AddStone(string key)
    {
        if (Data.megaStones.Contains(key)) return false;
        Data.megaStones.Add(key);
        Write();
        return true;
    }

    /// A Z crystal or Tera shard found: its level now (1 the first time, up to 3 with duplicates).
    public static int AddZ(int type) => AddLevel(Data.zCrystals, type);
    public static int AddTera(int type) => AddLevel(Data.teraShards, type);

    static int AddLevel(List<TypeLevel> list, int type)
    {
        var entry = list.Find(t => t.type == type);
        if (entry == null) list.Add(entry = new TypeLevel { type = type });
        entry.level = Mathf.Min(3, entry.level + 1);
        Write();
        return entry.level;
    }

    /// The items a Pokémon carries (a fresh, empty loadout when none was saved).
    public static Loadout LoadoutOf(int pokemonId) =>
        Data.loadouts.Find(l => l.pokemonId == pokemonId) ?? new Loadout { pokemonId = pokemonId };

    public static void SetLoadout(Loadout loadout)
    {
        Data.loadouts.RemoveAll(l => l.pokemonId == loadout.pokemonId);
        Data.loadouts.Add(loadout);
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
