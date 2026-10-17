using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// The player's team: every character is in the arena at once. One is driven by the player, the others by AllyAI.
/// Gamepad: left stick moves, right stick aims, R1 / R2 / L1 / L2 use moves 1-4, d-pad left/right changes the
/// controlled Pokémon. Keyboard and mouse: WASD (ZQSD on AZERTY) or arrows, mouse aims, left click / right click /
/// Space / Shift, Tab changes Pokémon. Dodge: ✕ (gamepad south button) or left Ctrl. This object follows the controlled Pokémon (camera and allies track it).
/// Standing on a knocked-out ally for a few seconds brings it back.
[DefaultExecutionOrder(50)] // after DungeonGenerator.Start, which places this object in the arena
public class PlayerController : MonoBehaviour
{
    const float DeadZone = 0.2f;
    const float AttackAnimTime = 0.3f;
    const float ArrowDistance = 0.9f, ArrowSize = 0.4f;
    static readonly Vector3 FeetOffset = new(0f, -0.25f, 0f);
    static readonly Color ArrowColor = new(1f, 1f, 1f, 0.8f);
    const float GaugeWidth = 1f, GaugeHeight = 0.12f, GaugeY = 1f;
    static readonly Color GaugeColor = new(1f, 0.85f, 0.2f);
    static readonly Vector3[] StartOffsets = { Vector3.zero, new(-1.2f, -0.8f, 0f), new(1.2f, -0.8f, 0f) };

    public int teamSize = 3;
    [Tooltip("Seconds the controlled Pokémon must stay on a knocked-out ally to bring it back")]
    public float reviveTime = 3f;
    [Tooltip("How close the controlled Pokémon must be to the knocked-out ally, in tiles")]
    public float reviveRadius = 1f;
    [Tooltip("Share of its max HP a revived Pokémon comes back with")]
    public float reviveHealthShare = 0.5f;

    /// The Pokémon the player currently controls.
    public Combatant Controlled => members.Count > 0 ? Current.combatant : null;
    public bool GameOver => gameOver;
    public int Count => members.Count;
    /// Every member of the team, knocked out or not.
    public IEnumerable<Combatant> Members => members.Select(m => m.combatant);

    /// Team member next to the controlled one in d-pad order (-1 = left, +1 = right), knocked out or not.
    public Combatant Neighbour(int step) =>
        members.Count < 2 ? null : members[((current + step) % members.Count + members.Count) % members.Count].combatant;

    class Member
    {
        public GameObject go;
        public Rigidbody2D body;
        public PmdAnimator animator;
        public Combatant combatant;
        public AllyAI ai;
        public float reviveProgress; // seconds accumulated by the player standing on this knocked-out member
        public Transform gaugeBack, gaugeFill;
    }

    readonly List<Member> members = new();
    int current;
    Vector2 move;
    Vector2 facing = Vector2.down;
    float attackUntil;
    string attackAnim = "Attack";
    Vector2 attackFacing = Vector2.down; // direction of the last attack, for its animation
    SpriteRenderer targetRing;
    const float TargetRingSize = 0.9f, TargetRingDrop = 0f; // tiles
    bool gameOver;
    Transform aimArrow;

    Member Current => members[current];

    void Awake()
    {
        aimArrow = new GameObject("AimArrow").transform;
        aimArrow.localScale = Vector3.one * ArrowSize;
        var arrowRenderer = aimArrow.gameObject.AddComponent<SpriteRenderer>();
        arrowRenderer.sprite = Shapes.Arrow;
        arrowRenderer.color = ArrowColor;
        arrowRenderer.sortingOrder = 0; // above the floor, under the Pokémon
        aimArrow.gameObject.SetActive(false);
        gameObject.AddComponent<Progression>();
        gameObject.AddComponent<Evolution>();
        gameObject.AddComponent<TeamHud>();
        gameObject.AddComponent<PauseMenu>();
    }

    void OnDestroy()
    {
        if (aimArrow != null) Destroy(aimArrow.gameObject);
    }

    void Start()
    {
        foreach (var set in ChooseTeam()) members.Add(SpawnMember(set, members.Count));
        SetControlled(0);
    }

    /// The team picked in the menu, or a random one among the unlocked Pokémon when the arena is started directly.
    List<PmdAnimSet> ChooseTeam()
    {
        var team = new List<PmdAnimSet>();
        if (Run.Team != null)
            foreach (int id in Run.Team)
                if (PokemonRoster.Get(id) is { } set) team.Add(set);
        if (team.Count > 0) return team;

        var pool = SaveData.Unlocked.Where(PokemonRoster.Playable).ToList();
        for (int i = 0; i < teamSize && pool.Count > 0; i++)
        {
            int pick = Random.Range(0, pool.Count);
            team.Add(PokemonRoster.Get(pool[pick]));
            pool.RemoveAt(pick);
        }
        return team;
    }

    Member SpawnMember(PmdAnimSet set, int index)
    {
        var go = new GameObject(GameData.Pokemon(set.pokemonId)?.name ?? "Ally");
        go.transform.position = transform.position + StartOffsets[index % StartOffsets.Length];

        go.AddComponent<SpriteRenderer>().sortingOrder = 1;
        var member = new Member { go = go, body = go.AddComponent<Rigidbody2D>() };
        member.body.gravityScale = 0f;
        member.body.freezeRotation = true;
        member.body.interpolation = RigidbodyInterpolation2D.Interpolate;
        member.body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        go.AddComponent<CircleCollider2D>().radius = 0.3f;

        member.animator = go.AddComponent<PmdAnimator>();
        member.animator.SetAnimSet(set);
        member.combatant = go.AddComponent<Combatant>();
        member.combatant.Init(set.pokemonId, Team.Player, SaveData.StartingMoves(set.pokemonId));
        member.combatant.Died += _ => OnKnockedOut(member);
        member.ai = go.AddComponent<AllyAI>();
        member.ai.leader = transform;
        go.AddComponent<HealthBar>();
        return member;
    }

    /// A Pokémon joining the team during the run (Munja), placed at that point; null when the team is full.
    public Combatant AddMember(int pokemonId, Vector3 position)
    {
        if (members.Count >= teamSize || PokemonRoster.Get(pokemonId) is not { } set) return null;
        var member = SpawnMember(set, members.Count);
        member.go.transform.position = position;
        member.body.position = position;
        members.Add(member);
        member.ai.enabled = true;
        return member.combatant;
    }

    void SetControlled(int index)
    {
        current = index;
        move = Vector2.zero;
        foreach (var m in members)
        {
            m.ai.enabled = m != Current && m.combatant.IsAlive;
            m.combatant.LiveAim = Vector2.zero; // the AI aims for itself
            m.combatant.AimTarget = null;
        }
    }

    /// Next member still standing in the given direction, or -1 when there is none.
    int NextAlive(int step)
    {
        for (int i = 1; i < members.Count; i++)
        {
            int index = ((current + step * i) % members.Count + members.Count) % members.Count;
            if (members[index].combatant.IsAlive) return index;
        }
        return -1;
    }

    void OnKnockedOut(Member member)
    {
        member.ai.enabled = false;
        member.body.linearVelocity = Vector2.zero;
        member.combatant.KnockOut();
        member.animator.Play(member.animator.Has("Sleep") ? "Sleep" : "Idle");
        if (member != Current) return;

        int next = NextAlive(1);
        if (next >= 0) SetControlled(next);
        else gameOver = true;
    }

    static bool InCone(Combatant from, Combatant target, Vector2 aim, float cone, float range)
    {
        Vector2 to = target.transform.position - from.transform.position;
        return to.magnitude <= range && (to.sqrMagnitude < 0.0001f || Vector2.Angle(aim, to) <= cone);
    }

    // Red circle under the player's target.
    void ShowTargetRing(Combatant target)
    {
        if (targetRing == null)
        {
            targetRing = new GameObject("TargetRing").AddComponent<SpriteRenderer>();
            targetRing.sprite = Shapes.Ring;
            targetRing.color = new Color(0.95f, 0.08f, 0.08f, 0.95f);
            targetRing.sortingOrder = 1; // above puddles, under the Pokémon
            targetRing.transform.localScale = Vector3.one * TargetRingSize;
        }
        targetRing.enabled = target != null;
        if (target != null) targetRing.transform.position = target.transform.position + Vector3.down * TargetRingDrop;
    }

    void Update()
    {
        if (gameOver) { ShowTargetRing(null); return; } // EnemySpawner draws the end screen and handles what comes next

        if (Time.timeScale == 0f) return; // paused by the upgrade screen

        var member = Current;
        move = member.combatant.CanMove ? ReadMove() : Vector2.zero; // asleep, frozen or trapped
        Vector2 aim = ReadAim();
        if (aim != Vector2.zero) facing = aim;
        else if (move != Vector2.zero) facing = move.normalized;
        // Target: the opponent closest to where the player aims (or faces); the moves go toward it.
        var settings = SaveData.Settings;
        // The current target is kept while it stays within a wider cone, so it does not jump around inside a group.
        var target = member.combatant.AimTarget;
        if (target == null || !target.IsAlive || !InCone(member.combatant, target, facing, settings.aimKeepCone, settings.aimRange))
            target = Combatant.FindAimTarget(member.combatant, facing, settings.aimCone, settings.aimRange);
        member.combatant.AimTarget = target;
        Vector2 shoot = target != null && target.transform.position != member.combatant.transform.position
            ? ((Vector2)(target.transform.position - member.combatant.transform.position)).normalized : facing;
        member.combatant.LiveAim = shoot; // moves leaving after a cast follow the aim
        ShowTargetRing(target);

        if (DodgePressed()) member.combatant.TryDodge(move != Vector2.zero ? move : facing);

        for (int slot = 0; slot < Combatant.MoveSlots; slot++)
            if (SlotHeld(slot) && member.combatant.TryUseMove(slot, shoot))
            {
                attackFacing = shoot;
                attackUntil = Time.time + AttackAnimTime;
                attackAnim = GameData.AttackAnim(member.combatant.Moves[slot], member.animator);
            }

        if (Time.time < attackUntil)
        {
            member.animator.direction = PmdAnimator.DirectionIndex(attackFacing);
            member.animator.Play(attackAnim);
        }
        else if (move != Vector2.zero)
        {
            member.animator.direction = PmdAnimator.DirectionIndex(move);
            member.animator.Play("Walk");
        }
        else
        {
            member.animator.Play("Idle");
        }

        int step = ReadSwitch();
        if (step != 0)
        {
            int next = NextAlive(step);
            if (next >= 0)
            {
                attackUntil = 0f;
                SetControlled(next);
            }
        }
    }

    void FixedUpdate()
    {
        if (gameOver || members.Count == 0) return;
        UpdateRevives();
        if (Current.combatant.IsDashing) return; // a melee dash drives the body
        Current.body.linearVelocity = Current.combatant.Steer(move) * Current.combatant.CurrentSpeed;
    }

    /// Between two arenas: knocked-out members get back up, the others are healed a little, effects are removed
    /// and everybody is placed around the given point.
    public void PrepareForNextArena(Vector3 centre, float healShare)
    {
        transform.position = centre;
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member.combatant.IsAlive) member.combatant.Heal(member.combatant.maxHp * healShare);
            else
            {
                member.combatant.Revive(reviveHealthShare);
                member.animator.Play("Idle");
            }
            member.combatant.Cleanse();
            member.reviveProgress = 0f;
            if (member.gaugeFill != null) ShowGauge(member);
            Vector2 spot = centre + StartOffsets[i % StartOffsets.Length];
            member.body.position = spot;
            member.go.transform.position = spot;
            member.body.linearVelocity = Vector2.zero;
        }
        SetControlled(current);
    }

    // The gauge fills while the controlled Pokémon stays on a knocked-out ally, and drains twice as fast otherwise.
    void UpdateRevives()
    {
        Vector2 position = Current.go.transform.position;
        foreach (var member in members)
        {
            if (member.combatant.IsAlive) continue;
            bool near = Vector2.Distance(position, member.go.transform.position) <= reviveRadius;
            member.reviveProgress = near
                ? member.reviveProgress + Time.fixedDeltaTime
                : Mathf.Max(0f, member.reviveProgress - 2f * Time.fixedDeltaTime);
            if (member.reviveProgress >= reviveTime)
            {
                member.reviveProgress = 0f;
                member.combatant.Revive(reviveHealthShare);
                member.animator.Play("Idle");
                member.ai.enabled = member != Current;
            }
            ShowGauge(member);
        }
    }

    void ShowGauge(Member member)
    {
        if (member.gaugeFill == null)
        {
            member.gaugeBack = MakeBar(member.go.transform, new Color(0f, 0f, 0f, 0.6f), 10);
            member.gaugeFill = MakeBar(member.go.transform, GaugeColor, 11);
        }
        bool visible = member.reviveProgress > 0f;
        member.gaugeBack.gameObject.SetActive(visible);
        member.gaugeFill.gameObject.SetActive(visible);
        float ratio = Mathf.Clamp01(member.reviveProgress / reviveTime);
        member.gaugeFill.localScale = new Vector3(GaugeWidth * ratio, GaugeHeight, 1f);
        member.gaugeFill.localPosition = new Vector3(-GaugeWidth * (1f - ratio) / 2f, GaugeY, 0f);
    }

    static Transform MakeBar(Transform parent, Color color, int order)
    {
        var bar = new GameObject("ReviveGauge").transform;
        bar.SetParent(parent, false);
        bar.localPosition = new Vector3(0f, GaugeY, 0f);
        bar.localScale = new Vector3(GaugeWidth, GaugeHeight, 1f);
        var renderer = bar.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = Shapes.Square;
        renderer.color = color;
        renderer.sortingOrder = order;
        return bar;
    }

    void LateUpdate()
    {
        if (members.Count == 0) return;
        transform.position = Current.go.transform.position;

        Vector2 aim = gameOver ? Vector2.zero : ReadAim();
        aimArrow.gameObject.SetActive(aim != Vector2.zero);
        if (aim == Vector2.zero) return;
        aimArrow.position = transform.position + FeetOffset + (Vector3)(aim * ArrowDistance);
        aimArrow.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
    }

    static Vector2 ReadMove()
    {
        var v = Vector2.zero;
        var pad = Gamepad.current;
        if (pad != null) v = pad.leftStick.ReadValue();
        var kb = Keyboard.current;
        if (kb != null && v.magnitude < DeadZone)
        {
            v.x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);
            v.y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0);
        }
        return v.magnitude < DeadZone ? Vector2.zero : Vector2.ClampMagnitude(v, 1f);
    }

    /// Aim direction, or zero when the player is not aiming.
    Vector2 ReadAim()
    {
        var pad = Gamepad.current;
        if (pad != null)
        {
            Vector2 stick = pad.rightStick.ReadValue();
            if (stick.magnitude >= DeadZone) return stick.normalized;
        }
        var mouse = Mouse.current;
        var cam = Camera.main;
        if (mouse != null && cam != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
        {
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            Vector2 toMouse = world - (Vector2)transform.position;
            if (toMouse != Vector2.zero) return toMouse.normalized;
        }
        return Vector2.zero;
    }

    static bool SlotHeld(int slot)
    {
        var pad = Gamepad.current;
        if (pad != null)
        {
            bool held = slot switch
            {
                0 => pad.rightShoulder.isPressed,
                1 => pad.rightTrigger.isPressed,
                2 => pad.leftShoulder.isPressed,
                _ => pad.leftTrigger.isPressed,
            };
            if (held) return true;
        }
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        return slot switch
        {
            0 => mouse != null && mouse.leftButton.isPressed,
            1 => mouse != null && mouse.rightButton.isPressed,
            2 => kb != null && kb.spaceKey.isPressed,
            _ => kb != null && kb.leftShiftKey.isPressed,
        };
    }

    static bool DodgePressed()
    {
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        return (pad != null && pad.buttonSouth.wasPressedThisFrame) || (kb != null && kb.leftCtrlKey.wasPressedThisFrame);
    }

    static int ReadSwitch()
    {
        var pad = Gamepad.current;
        if (pad != null)
        {
            if (pad.dpad.right.wasPressedThisFrame) return 1;
            if (pad.dpad.left.wasPressedThisFrame) return -1;
        }
        var kb = Keyboard.current;
        return kb != null && kb.tabKey.wasPressedThisFrame ? 1 : 0;
    }
}
