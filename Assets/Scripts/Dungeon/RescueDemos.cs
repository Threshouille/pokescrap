using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Room of the test arena (TerrainSandbox, room 6) that stages the real cases of the team's rescue and heals
/// (TeamRescue, RoleTactics heals) with the AIs as they play, one after the other on a loop, to be watched: the
/// controlled Pokémon is moved by the script (PlayerController.ScriptedMove; moving it by hand still works, to walk
/// back to the stairs). ↑/↓ (1/2) skips to another case, R / Select starts it again.
/// The team gets a third member if it has fewer, and much more health while here (nothing ends the run); an ally
/// may borrow Soin for the heal cases. Everything is given back when the room is left.
public class RescueDemos : MonoBehaviour
{
    const int Bulbizarre = 1, Salameche = 4, Rattata = 19, Mewtwo = 150, Soin = 117;
    const float Sturdy = 5f; // health × while in the room

    class Case
    {
        public string name, watch;
        public float duration = 14f; // seconds before the next case
        public Func<IEnumerator> setup;
    }

    readonly List<Case> cases = new();
    readonly List<Combatant> spawned = new();
    readonly Dictionary<Combatant, (float maxHp, MoveDef[] moves)> kept = new();
    PlayerController team;
    EnemySpawner spawner;
    int index;
    float endsAt;
    Combatant controlled, ally, downed; // the cast of the case running

    void Awake()
    {
        cases.Add(new Case { name = "Un allié réanime seul", watch = "aucun ennemi près du K.O. : l'allié va le réanimer.", duration = 10f, setup = AllyRevives });
        cases.Add(new Case { name = "Le Pokémon joué réanime, l'allié éloigne les ennemis", watch = "le Pokémon joué reste sur le K.O. : l'allié attire les deux Rattata loin de lui.", setup = YouRevive });
        cases.Add(new Case { name = "Le plus apte garde, l'autre réanime", watch = "le Pokémon joué reste à l'écart : l'allié garde ou réanime selon qui des deux encaisse et frappe le mieux.", setup = FittestGuards });
        cases.Add(new Case { name = "Boss : le garde attire le boss", watch = "le Pokémon joué reste sur le K.O. : l'allié attire Mewtwo loin de lui.", duration = 16f, setup = BossLured });
        cases.Add(new Case { name = "Soin d'un coéquipier presque K.O.", watch = "le Pokémon joué est à 30 % : l'allié (Soin) vient le soigner.", duration = 10f, setup = HealsYou });
        cases.Add(new Case { name = "Blessé, il ne soigne pas au milieu des ennemis", watch = "l'allié blessé garde ses distances ; au bout de 6 s le Pokémon joué s'éloigne des ennemis et l'allié vient le soigner.", duration = 16f, setup = NoRiskyHeal });
    }

    IEnumerator Start()
    {
        team = FindAnyObjectByType<PlayerController>();
        spawner = GetComponent<EnemySpawner>();
        yield return null;
        Play(0);
    }

    void Update()
    {
        if (team == null || Time.timeScale == 0f) return;
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        int step = 0;
        if ((pad != null && pad.dpad.up.wasPressedThisFrame) || (kb != null && kb.digit1Key.wasPressedThisFrame)) step = -1;
        if ((pad != null && pad.dpad.down.wasPressedThisFrame) || (kb != null && kb.digit2Key.wasPressedThisFrame)) step = 1;
        bool again = (pad != null && pad.selectButton.wasPressedThisFrame) || (kb != null && kb.rKey.wasPressedThisFrame);
        if (step != 0) Play((index + step + cases.Count) % cases.Count);
        else if (again) Play(index);
        else if (endsAt > 0f && Time.time >= endsAt) Play((index + 1) % cases.Count); // on to the next case
    }

    void OnDestroy()
    {
        StopAllCoroutines();
        Clear();
        if (team != null) team.ScriptedMove = null;
        foreach (var (c, (maxHp, moves)) in kept)
        {
            if (c == null) continue;
            c.maxHp = maxHp;
            c.hp = Mathf.Min(c.hp, maxHp);
            for (int i = 0; i < Combatant.MoveSlots; i++) c.LearnMove(i, moves[i]);
        }
        kept.Clear();
    }

    /// The banner's line: the case, what to watch, and the ally's rescue job right now.
    public string Line()
    {
        var c = cases[index];
        string job = "";
        if (ally != null && ally.IsAlive && downed != null && !downed.IsAlive)
        {
            var j = TeamRescue.JobOf(ally, team.Controlled, team.reviveRadius, out _, out _);
            job = j switch { TeamRescue.Job.Guard => "     Allié : garde", TeamRescue.Job.Revive => "     Allié : réanime", _ => "" };
        }
        float left = Mathf.Max(0f, endsAt - Time.time);
        return $"Secours ({index + 1}/{cases.Count}) : {c.name} : {c.watch}{job}     cas suivant dans {left:0} s     ↑/↓ ou 1/2 : changer     R ou Select : relancer     Escalier (à gauche) : retour";
    }

    void Play(int which)
    {
        index = which;
        endsAt = Time.time + cases[index].duration;
        StopAllCoroutines();
        if (team != null) team.ScriptedMove = Vector2.zero; // the player only watches
        StartCoroutine(Run(cases[index]));
    }

    IEnumerator Run(Case c)
    {
        Clear();
        var arena = DungeonGenerator.Current;
        if (arena == null || team == null) yield break;
        // Everybody back up, healed, around the centre; a third member if needed; the moves lent before given back.
        team.PrepareForNextArena(Centre, 1f);
        while (team.Count < 3)
        {
            int id = HasMember(Bulbizarre) ? Salameche : Bulbizarre;
            team.AddMember(id, Centre + Vector2.left * team.Count);
        }
        foreach (var m in team.Members)
        {
            if (!kept.ContainsKey(m)) kept[m] = (m.maxHp, (MoveDef[])m.Moves.Clone());
            for (int i = 0; i < Combatant.MoveSlots; i++) m.LearnMove(i, kept[m].moves[i]);
            m.maxHp = kept[m].maxHp * Sturdy;
            m.hp = m.maxHp;
        }
        controlled = team.Controlled;
        ally = null;
        downed = null;
        foreach (var m in team.Members)
        {
            if (m == controlled) continue;
            if (ally == null) ally = m;
            else downed = m;
        }
        yield return new WaitForFixedUpdate();
        yield return c.setup();
    }

    bool HasMember(int id)
    {
        foreach (var m in team.Members)
            if (m.OriginId == id) return true;
        return false;
    }

    static Vector2 Centre => DungeonGenerator.Current.CentreOf(DungeonGenerator.Current.Size / 2, DungeonGenerator.Current.Size / 2);

    static void Teleport(Combatant c, Vector2 offset)
    {
        Vector2 at = Centre + offset;
        var body = c.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = at;
            body.linearVelocity = Vector2.zero;
        }
        c.transform.position = at;
        var ai = c.GetComponent<AllyAI>();
        if (ai != null) ai.ForgetLeaderMotion();
    }

    void Place(Vector2 you, Vector2 mate, Vector2 knockedOut)
    {
        Teleport(controlled, you);
        team.transform.position = controlled.transform.position;
        Teleport(ally, mate);
        Teleport(downed, knockedOut);
    }

    static void KnockOut(Combatant c) => c.TakeFixedDamage(c.hp + 1f);

    // A real enemy (its AI, its moves), or a still one without its AI.
    Combatant Foe(int pokemonId, Vector2 offset, bool boss = false, bool still = false)
    {
        if (spawner == null) return null;
        var c = spawner.SpawnFighter(pokemonId, null, Centre + offset, boss, false, boss ? 8 : 3, boss ? 4f : 2f);
        if (c == null) return null;
        if (boss) c.gameObject.AddComponent<HealthBar>(); // no boss bar in the test arena
        if (still)
        {
            var ai = c.GetComponent<EnemyAI>();
            if (ai != null) ai.enabled = false;
            c.Drive(Vector2.zero);
        }
        spawned.Add(c);
        return c;
    }

    void Clear()
    {
        foreach (var c in spawned)
            if (c != null) Destroy(c.gameObject);
        spawned.Clear();
        foreach (var zone in FindObjectsByType<Zone>()) Destroy(zone.gameObject);
        foreach (var shot in FindObjectsByType<Projectile>()) Destroy(shot.gameObject);
    }

    void LendSoin(Combatant c)
    {
        for (int i = 0; i < Combatant.MoveSlots; i++) c.LearnMove(i, i == 0 ? GameData.Move(Soin) : null);
    }

    // ---- Cases ----

    IEnumerator AllyRevives()
    {
        Place(new Vector2(-7f, 0f), new Vector2(-3f, 2f), new Vector2(5f, -1f));
        KnockOut(downed);
        yield break;
    }

    IEnumerator YouRevive()
    {
        Place(Vector2.zero, new Vector2(-3f, -2f), Vector2.zero);
        KnockOut(downed);
        Foe(Rattata, new Vector2(3f, 1.5f));
        Foe(Rattata, new Vector2(3f, -1.5f));
        yield break;
    }

    IEnumerator FittestGuards()
    {
        Place(new Vector2(-8f, 0f), new Vector2(-2f, 2f), new Vector2(2f, 0f));
        KnockOut(downed);
        Foe(Rattata, new Vector2(5f, 1f));
        yield break;
    }

    IEnumerator BossLured()
    {
        Place(Vector2.zero, new Vector2(-3f, -2f), Vector2.zero);
        KnockOut(downed);
        Foe(Mewtwo, new Vector2(0f, 6f), boss: true);
        yield break;
    }

    IEnumerator HealsYou()
    {
        Place(new Vector2(6f, 0f), new Vector2(-4f, 0f), new Vector2(-8f, 6f));
        LendSoin(ally);
        controlled.hp = controlled.maxHp * 0.3f;
        yield break;
    }

    IEnumerator NoRiskyHeal()
    {
        Place(new Vector2(5f, 0f), new Vector2(-3f, 0f), new Vector2(-8f, 6f));
        LendSoin(ally);
        Foe(Rattata, new Vector2(3f, 1f), still: true);
        Foe(Rattata, new Vector2(3f, -1f), still: true);
        controlled.hp = controlled.maxHp * 0.3f;
        ally.hp = ally.maxHp * 0.4f;
        yield return new WaitForSeconds(6f);
        // The controlled Pokémon walks away from the opponents, up the room: now the ally can come safely.
        Vector2 goal = Centre + new Vector2(5f, 8f);
        while (Vector2.Distance(controlled.transform.position, goal) > 0.5f)
        {
            team.ScriptedMove = (goal - (Vector2)controlled.transform.position).normalized;
            yield return new WaitForFixedUpdate();
        }
        team.ScriptedMove = Vector2.zero;
    }
}
