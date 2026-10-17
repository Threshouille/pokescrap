using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// Run progression: experience shared by the team, levels, evolutions at the level of the original game, and at
/// regular levels one upgrade to pick among three for each Pokémon. One of the three is a new move while the
/// Pokémon knows fewer than 4, then only sometimes (it replaces a known move). The game is paused while choosing.
[RequireComponent(typeof(PlayerController))]
public class Progression : MonoBehaviour
{
    public const int MaxLevel = 100;
    const int Options = 3;
    const float StickThreshold = 0.6f;
    const float BannerTime = 3f;

    /// One card of the choice screen: an upgrade or a new move.
    class Offer
    {
        public UpgradeDef upgrade;
        public MoveDef move;
        public Evolution.Card card; // evolution item or trade
    }

    [Tooltip("An upgrade is offered to each Pokémon every this many levels")]
    public int choiceInterval = 10;
    [Tooltip("Experience needed to go from level L to L+1 = xpBase + xpPerLevel * L (provisional pacing)")]
    public float xpBase = 20f;
    public float xpPerLevel = 4f;
    [Tooltip("Multiplier on the experience yield of defeated enemies")]
    public float xpFactor = 1f;

    public int Level { get; private set; } = 1;
    public float Xp { get; private set; }
    public float XpToNext => xpBase + xpPerLevel * Level;
    /// True while the upgrade screen is open (the game is paused).
    public bool Choosing => options != null;
    /// Messages shown at the top of the screen for a moment (evolutions), with the unscaled time they end.
    readonly List<(string text, float until)> banners = new();

    readonly Queue<Combatant> pending = new();
    readonly System.Random rng = new();
    PlayerController team;
    Offer[] options;
    int selected;
    bool replacing; // choosing which known move the new one replaces
    int replaceSelected; // 0-3: a known move, 4: keep them all
    bool stickNeutral = true;
    GUIStyle title, cardTitle, cardText;

    Evolution evolution;

    void Awake()
    {
        team = GetComponent<PlayerController>();
    }

    void Start() => evolution = GetComponent<Evolution>(); // added after this component

    /// A message at the top of the screen for a moment.
    public void Announce(string text) => banners.Add((text, Time.unscaledTime + BannerTime));

    void OnEnable() => Combatant.AnyDied += OnDied;

    void OnDisable()
    {
        Combatant.AnyDied -= OnDied;
        Time.timeScale = 1f; // never leave the game paused behind us (scene reload during a choice)
    }

    void OnDied(Combatant combatant)
    {
        if (combatant.team == Team.Enemy && combatant.Stats != null) AddXp(combatant.Stats.expYield * xpFactor);
    }

    public void AddXp(float amount)
    {
        if (Level >= MaxLevel) return;
        Xp += amount;
        while (Level < MaxLevel && Xp >= XpToNext)
        {
            Xp -= XpToNext;
            LevelUp();
        }
        if (Level >= MaxLevel) Xp = 0f;
    }

    void LevelUp()
    {
        Level++;
        foreach (var member in team.Members)
        {
            if (member.Stats == null) continue;
            member.ApplyGrowth(LevelData.Growth(member.Stats.id, Level), Level);
        }
        if (choiceInterval > 0 && Level % choiceInterval == 0)
            foreach (var member in team.Members) pending.Enqueue(member);
        if (pending.Count > 0 && options == null) NextChoice();
    }

    void NextChoice()
    {
        replacing = false;
        while (pending.Count > 0)
        {
            var who = pending.Peek();
            var offers = new List<Offer>();
            foreach (var upgrade in GameData.RandomUpgrades(who, Options, rng)) offers.Add(new Offer { upgrade = upgrade });
            var move = MoveOffer(who);
            if (move != null)
            {
                if (offers.Count >= Options) offers.RemoveAt(rng.Next(offers.Count));
                offers.Insert(rng.Next(offers.Count + 1), new Offer { move = move });
            }
            if (evolution != null && evolution.CardFor(who, rng) is { } card)
            {
                // The card takes the place of an upgrade (never of the new move).
                var upgrades = offers.FindAll(o => o.upgrade != null);
                if (offers.Count >= Options && upgrades.Count > 0) offers.Remove(upgrades[rng.Next(upgrades.Count)]);
                offers.Insert(rng.Next(offers.Count + 1), new Offer { card = card });
            }
            options = offers.ToArray();
            if (options.Length > 0)
            {
                selected = 0;
                Time.timeScale = 0f;
                return;
            }
            pending.Dequeue(); // nothing to offer to this one
        }
        options = null;
        Time.timeScale = 1f;
    }

    /// A move to offer this time, or null: always while the Pokémon knows fewer than 4, then only by chance.
    MoveDef MoveOffer(Combatant who)
    {
        var settings = SaveData.Settings;
        if (who.KnownMoveCount >= Combatant.MoveSlots && rng.NextDouble() >= settings.moveOfferChanceWhenFull) return null;
        var learnable = who.LearnableMoves(settings.evolvedMoveWeight);
        float total = 0f;
        foreach (var (_, weight) in learnable) total += weight;
        double roll = rng.NextDouble() * total;
        foreach (var (move, weight) in learnable)
            if ((roll -= weight) < 0) return move;
        return null;
    }

    /// Applies the option at that index to the Pokémon being upgraded, then moves on to the next one.
    /// A new move goes to a free slot, or asks which known move it replaces.
    public void Choose(int index)
    {
        if (options == null || index < 0 || index >= options.Length) return;
        var who = pending.Peek();
        var offer = options[index];
        if (offer.card != null) evolution.Take(who, offer.card);
        else if (offer.move == null) who.ApplyUpgrade(offer.upgrade);
        else
        {
            int free = System.Array.IndexOf(who.Moves, null);
            if (free < 0)
            {
                replacing = true;
                replaceSelected = 0;
                return;
            }
            who.LearnMove(free, offer.move);
        }
        pending.Dequeue();
        NextChoice();
    }

    /// slot 0-3 replaces that move with the offered one; any other value goes back to the three cards.
    public void Replace(int slot)
    {
        if (!replacing) return;
        replacing = false;
        if (slot < 0 || slot >= Combatant.MoveSlots) return;
        pending.Dequeue().LearnMove(slot, options[selected].move);
        NextChoice();
    }

    void Update()
    {
        if (options == null) return;
        int step = 0;
        bool confirm = false;

        var pad = Gamepad.current;
        if (pad != null)
        {
            if (pad.dpad.left.wasPressedThisFrame) step = -1;
            if (pad.dpad.right.wasPressedThisFrame) step = 1;
            float x = pad.leftStick.ReadValue().x;
            if (Mathf.Abs(x) < StickThreshold / 2f) stickNeutral = true;
            else if (stickNeutral && Mathf.Abs(x) >= StickThreshold)
            {
                step = x > 0f ? 1 : -1; // one step per push of the stick
                stickNeutral = false;
            }
            if (pad.buttonSouth.wasPressedThisFrame) confirm = true;
        }
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) step = -1;
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) step = 1;
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) confirm = true;
        }

        bool back = (pad != null && pad.buttonEast.wasPressedThisFrame) || (kb != null && kb.escapeKey.wasPressedThisFrame);
        if (replacing)
        {
            const int entries = Combatant.MoveSlots + 1; // the known moves, then "keep"
            if (step != 0) replaceSelected = (replaceSelected + step + entries) % entries;
            if (confirm) Replace(replaceSelected);
            else if (back) Replace(-1);
            return;
        }
        if (step != 0) selected = (selected + step + options.Length) % options.Length;
        if (confirm) Choose(selected);
    }

    void OnGUI()
    {
        float s = Mathf.Max(1f, Mathf.Round(Screen.height / 360f));
        title ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        cardTitle ??= new GUIStyle(title);
        cardText ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = true };
        title.fontSize = Mathf.RoundToInt(11f * s);
        cardTitle.fontSize = Mathf.RoundToInt(9f * s);
        cardText.fontSize = Mathf.RoundToInt(7f * s);

        banners.RemoveAll(b => Time.unscaledTime >= b.until);
        for (int i = 0; i < banners.Count; i++)
        {
            var rect = new Rect(Screen.width / 2f - 160f * s, (52f + i * 16f) * s, 320f * s, 14f * s);
            Fill(rect, new Color(0.08f, 0.08f, 0.12f, 0.85f));
            GUI.Label(rect, banners[i].text, cardTitle);
        }

        if (options == null || pending.Count == 0) return;
        var who = pending.Peek();

        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.6f));

        float cardW = 90f * s, cardH = 76f * s, gap = 10f * s;
        int count = replacing ? Combatant.MoveSlots + 1 : options.Length;
        float total = count * cardW + (count - 1) * gap;
        float x0 = (Screen.width - total) / 2f, y0 = (Screen.height - cardH) / 2f;

        GUI.Label(new Rect(0, y0 - 46f * s, Screen.width, 16f * s), $"Niveau {Level} — {who.Stats?.name}", title);
        string heading = replacing ? $"{options[selected].move.name} remplace quelle attaque ?" : "Choisis une amélioration";
        GUI.Label(new Rect(0, y0 - 28f * s, Screen.width, 14f * s), heading, cardTitle);

        for (int i = 0; i < count; i++)
        {
            var card = new Rect(x0 + i * (cardW + gap), y0, cardW, cardH);
            bool current = i == (replacing ? replaceSelected : selected);
            Fill(new Rect(card.x - 2f * s, card.y - 2f * s, card.width + 4f * s, card.height + 4f * s),
                current ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 0.3f, 0.35f));
            Fill(card, current ? new Color(0.18f, 0.2f, 0.3f) : new Color(0.1f, 0.1f, 0.14f));
            var move = replacing ? (i < Combatant.MoveSlots ? who.Moves[i] : null) : options[i].move;
            string name, description;
            if (replacing && i == Combatant.MoveSlots) (name, description) = ("Garder", "Ne pas apprendre la nouvelle attaque");
            else if (move != null)
            {
                name = move.name;
                description = (replacing ? "" : "Nouvelle attaque\n") + MoveText.Summary(move) + "\n" + MoveText.Effects(move);
                Fill(new Rect(card.x + 4f * s, card.y + 4f * s, card.width - 8f * s, 2f * s), GameData.TypeColor(move.type));
            }
            else if (options[i].card != null) (name, description) = (options[i].card.name, options[i].card.description);
            else (name, description) = (options[i].upgrade.name, options[i].upgrade.description);
            GUI.Label(new Rect(card.x, card.y + 8f * s, card.width, 14f * s), name, cardTitle);
            GUI.Label(new Rect(card.x + 6f * s, card.y + 26f * s, card.width - 12f * s, card.height - 30f * s), description, cardText);
        }

        string help = replacing ? "Croix ou stick : choisir     ✕ (manette) ou Entrée : valider     ○ ou Échap : retour"
            : "Croix ou stick : choisir     ✕ (manette) ou Entrée : valider";
        GUI.Label(new Rect(0, y0 + cardH + 10f * s, Screen.width, 14f * s), help, cardText);
    }

    static void Fill(Rect rect, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
