using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// Run progression: experience shared by the team, levels, and at regular levels one upgrade to pick among three
/// for each Pokémon. The game is paused while the player chooses.
[RequireComponent(typeof(PlayerController))]
public class Progression : MonoBehaviour
{
    public const int MaxLevel = 100;
    const int Options = 3;
    const float StickThreshold = 0.6f;

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

    readonly Queue<Combatant> pending = new();
    readonly System.Random rng = new();
    PlayerController team;
    UpgradeDef[] options;
    int selected;
    bool stickNeutral = true;
    GUIStyle title, cardTitle, cardText;

    void Awake()
    {
        team = GetComponent<PlayerController>();
    }

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
            if (member.Stats != null) member.ApplyGrowth(LevelData.Growth(member.Stats.id, Level), Level);
        if (choiceInterval > 0 && Level % choiceInterval == 0)
            foreach (var member in team.Members) pending.Enqueue(member);
        if (pending.Count > 0 && options == null) NextChoice();
    }

    void NextChoice()
    {
        while (pending.Count > 0)
        {
            options = GameData.RandomUpgrades(pending.Peek(), Options, rng);
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

    /// Applies the option at that index to the Pokémon being upgraded, then moves on to the next one.
    public void Choose(int index)
    {
        if (options == null || index < 0 || index >= options.Length) return;
        pending.Dequeue().ApplyUpgrade(options[index]);
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

        if (step != 0) selected = (selected + step + options.Length) % options.Length;
        if (confirm) Choose(selected);
    }

    void OnGUI()
    {
        if (options == null || pending.Count == 0) return;
        var who = pending.Peek();
        float s = Mathf.Max(1f, Mathf.Round(Screen.height / 360f));
        title ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        cardTitle ??= new GUIStyle(title);
        cardText ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = true };
        title.fontSize = Mathf.RoundToInt(11f * s);
        cardTitle.fontSize = Mathf.RoundToInt(9f * s);
        cardText.fontSize = Mathf.RoundToInt(7f * s);

        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.6f));

        float cardW = 90f * s, cardH = 70f * s, gap = 10f * s;
        float total = options.Length * cardW + (options.Length - 1) * gap;
        float x0 = (Screen.width - total) / 2f, y0 = (Screen.height - cardH) / 2f;

        GUI.Label(new Rect(0, y0 - 46f * s, Screen.width, 16f * s), $"Niveau {Level} — {who.Stats?.name}", title);
        GUI.Label(new Rect(0, y0 - 28f * s, Screen.width, 14f * s), "Choisis une amélioration", cardTitle);

        for (int i = 0; i < options.Length; i++)
        {
            var card = new Rect(x0 + i * (cardW + gap), y0, cardW, cardH);
            bool current = i == selected;
            Fill(new Rect(card.x - 2f * s, card.y - 2f * s, card.width + 4f * s, card.height + 4f * s),
                current ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 0.3f, 0.35f));
            Fill(card, current ? new Color(0.18f, 0.2f, 0.3f) : new Color(0.1f, 0.1f, 0.14f));
            GUI.Label(new Rect(card.x, card.y + 8f * s, card.width, 14f * s), options[i].name, cardTitle);
            GUI.Label(new Rect(card.x + 6f * s, card.y + 28f * s, card.width - 12f * s, card.height - 32f * s), options[i].description, cardText);
        }

        GUI.Label(new Rect(0, y0 + cardH + 10f * s, Screen.width, 14f * s),
            "Croix ou stick : choisir     ✕ (manette) ou Entrée : valider", cardText);
    }

    static void Fill(Rect rect, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
