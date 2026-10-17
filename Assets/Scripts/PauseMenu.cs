using UnityEngine;
using UnityEngine.InputSystem;

/// In-run pause menu: Options (gamepad) or Escape opens it and freezes time. "Reprendre" closes it, "Retour au
/// choix des Pokémon" abandons the run (not counted in the run statistics) and goes back to the menu.
/// D-pad / left stick / arrows / ZQSD move, Cross / Enter / Space confirm, Options / Circle / Escape close.
/// Unavailable during the upgrade screen, an evolution, the fades between stages and the end screens.
public class PauseMenu : MonoBehaviour
{
    const float StickThreshold = 0.6f;
    static readonly string[] Entries = { "Reprendre", "Retour au choix des Pokémon" };
    static readonly Color Frame = new(0.08f, 0.08f, 0.12f, 0.9f), Cursor = new(1f, 0.85f, 0.2f);

    public bool Open { get; private set; }

    PlayerController team;
    Progression progression;
    Evolution evolution;
    EnemySpawner spawner;
    int selected;
    bool stickNeutral = true;
    GUIStyle title, entry;

    void Awake()
    {
        team = GetComponent<PlayerController>();
        progression = GetComponent<Progression>();
        evolution = GetComponent<Evolution>();
    }

    void Start() => spawner = FindAnyObjectByType<EnemySpawner>();

    void OnDisable()
    {
        if (Open) Time.timeScale = 1f; // never leave the game paused behind us
        Open = false;
    }

    bool CanPause => Time.timeScale != 0f
        && (progression == null || !progression.Choosing)
        && (evolution == null || !evolution.Evolving)
        && (team == null || !team.GameOver)
        && (spawner == null || (!spawner.Fading && !spawner.Victory));

    void Update()
    {
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        bool toggle = (pad != null && pad.startButton.wasPressedThisFrame) || (kb != null && kb.escapeKey.wasPressedThisFrame);
        if (!Open)
        {
            if (toggle && CanPause) SetOpen(true);
            return;
        }

        int step = 0;
        bool confirm = false, back = toggle;
        if (pad != null)
        {
            if (pad.dpad.up.wasPressedThisFrame) step = -1;
            if (pad.dpad.down.wasPressedThisFrame) step = 1;
            float y = pad.leftStick.ReadValue().y;
            if (Mathf.Abs(y) < StickThreshold / 2f) stickNeutral = true;
            else if (stickNeutral && Mathf.Abs(y) >= StickThreshold)
            {
                step = y > 0f ? -1 : 1; // one step per push of the stick
                stickNeutral = false;
            }
            if (pad.buttonSouth.wasPressedThisFrame) confirm = true;
            if (pad.buttonEast.wasPressedThisFrame) back = true;
        }
        if (kb != null)
        {
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) step = -1;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) step = 1;
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) confirm = true;
        }

        if (back) { SetOpen(false); return; }
        if (step != 0) selected = (selected + step + Entries.Length) % Entries.Length;
        if (confirm) Choose(selected);
    }

    void SetOpen(bool open)
    {
        Open = open;
        selected = 0;
        stickNeutral = false; // the stick may still be held from moving
        Time.timeScale = open ? 0f : 1f;
    }

    void Choose(int index)
    {
        SetOpen(false);
        if (index == 1) Run.ToMenu(); // the run is only recorded by the end screens, so it is not counted
    }

    void OnGUI()
    {
        if (!Open) return;
        GUI.depth = -10; // above the rest of the interface
        float s = Mathf.Max(1f, Mathf.Round(Screen.height / 360f));
        title ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        entry ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        title.fontSize = Mathf.RoundToInt(12f * s);
        entry.fontSize = Mathf.RoundToInt(9f * s);

        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.6f));
        float w = 170f * s, line = 18f * s, h = 30f * s + Entries.Length * line;
        var panel = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        Fill(panel, Frame);
        GUI.Label(new Rect(panel.x, panel.y + 4f * s, w, 18f * s), "Pause", title);
        for (int i = 0; i < Entries.Length; i++)
        {
            var rect = new Rect(panel.x + 8f * s, panel.y + 26f * s + i * line, w - 16f * s, line - 2f * s);
            if (i == selected)
            {
                Fill(rect, new Color(1f, 1f, 1f, 0.1f));
                entry.normal.textColor = Cursor;
            }
            else entry.normal.textColor = Color.white;
            GUI.Label(rect, Entries[i], entry);
        }
    }

    static void Fill(Rect rect, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
