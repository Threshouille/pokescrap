using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// The team's shared bag of consumables (BagItems, bag_items.json; GDD, "Objets de partie"), added by
/// PlayerController: a few slots, each a stack of one consumable; lost when the run ends. The player uses a slot with
/// 1 to 4 (keyboard), or picks it with the d-pad up / down and uses it with □ (pad); on the controlled Pokémon (a seed
/// or an orb acts around it). Allies use it too (AllyAI.TryBag). In the test arena it starts with sandboxStart.
[RequireComponent(typeof(PlayerController))]
public class Bag : MonoBehaviour
{
    public class Slot
    {
        public BagItems.Item item;
        public int count;
    }

    Slot[] slots;
    PlayerController team;
    Progression progression;

    public IReadOnlyList<Slot> Contents => slots;

    /// The slot the pad's □ uses.
    public int Selected { get; private set; }

    void Awake()
    {
        slots = new Slot[Mathf.Max(1, BagItems.Slots)];
        team = GetComponent<PlayerController>();
        progression = GetComponent<Progression>();
    }

    void Start()
    {
        if (!Run.Sandbox) return;
        foreach (var s in BagItems.SandboxStart) Add(s.key, s.count);
    }

    /// Puts consumables in the bag: on their stack, else in a free slot. False when it does not fit (bag full).
    public bool Add(string key, int count = 1)
    {
        var item = BagItems.Get(key);
        if (item == null) return false;
        foreach (var s in slots)
            if (s != null && s.item == item && s.count < BagItems.Stack)
            {
                s.count = Mathf.Min(BagItems.Stack, s.count + count);
                return true;
            }
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == null)
            {
                slots[i] = new Slot { item = item, count = Mathf.Min(BagItems.Stack, count) };
                return true;
            }
        return false;
    }

    /// Empties the bag (the test bench).
    public void Clear()
    {
        for (int i = 0; i < slots.Length; i++) slots[i] = null;
    }

    /// How many of that consumable the bag holds.
    public int Count(string key)
    {
        int n = 0;
        foreach (var s in slots)
            if (s != null && s.item.key == key) n += s.count;
        return n;
    }

    /// The first slot holding a consumable that passes the test, or -1.
    public int Find(System.Func<BagItems.Item, bool> fits)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && fits(slots[i].item)) return i;
        return -1;
    }

    void Update()
    {
        if (team == null || team.GameOver || team.AutoPilot || Time.timeScale == 0f) return;
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        for (int i = 0; i < slots.Length && i < 4 && kb != null; i++)
            if (kb[Key.Digit1 + i].wasPressedThisFrame)
            {
                Selected = i; // the HUD describes it
                Use(i, team.Controlled);
            }
        if (pad == null) return;
        if (pad.dpad.up.wasPressedThisFrame) Selected = (Selected + slots.Length - 1) % slots.Length;
        if (pad.dpad.down.wasPressedThisFrame) Selected = (Selected + 1) % slots.Length;
        if (pad.buttonWest.wasPressedThisFrame) Use(Selected, team.Controlled);
    }

    /// Uses a slot's consumable for "user" (the controlled Pokémon, or an ally). False when it does nothing now (a
    /// berry at full health, no one knocked out...): nothing is spent.
    public bool Use(int index, Combatant user)
    {
        if (index < 0 || index >= slots.Length || slots[index] == null || user == null || !user.IsAlive) return false;
        var item = slots[index].item;
        if (!Apply(item, user)) return false;
        if (!user.KeepsBerry(item) && --slots[index].count <= 0) slots[index] = null; // Harvest: kept
        progression?.Announce($"{user.Stats.name} utilise {item.name} !");
        return true;
    }

    // ---- Effects ----

    bool Apply(BagItems.Item item, Combatant user)
    {
        Vector2 at = user.transform.position;
        switch (item.effect)
        {
            case "heal":
                if (user.hp >= user.maxHp) return false;
                user.Heal(user.maxHp * item.value * user.BagHealMul); // Gluttony
                Burst("evolution_eclat", at, item.Colour, 1.5f);
                return true;
            case "cure":
                if (!user.HasStatus(item.status)) return false;
                user.RemoveStatus(item.status);
                Burst("evolution_eclat", at, item.Colour, 1.5f);
                return true;
            case "blast":
            {
                Vector2 centre = Aimed(user, at);
                Burst("evolution_onde", centre, item.Colour, item.radius * 2f);
                foreach (var foe in Foes(user, centre, item.radius))
                {
                    foe.TakeFixedDamage(foe.maxHp * (foe.isBoss && item.bossValue >= 0f ? item.bossValue : item.value));
                    Vector2 away = (Vector2)foe.transform.position - centre;
                    if (away != Vector2.zero) foe.Knockback(away.normalized * 2f / 0.4f, 0.4f);
                }
                return true;
            }
            case "sleep":
                Burst("evolution_onde", at, item.Colour, item.radius * 2f);
                foreach (var foe in Foes(user, at, item.radius)) foe.AddStatus("sleep", item.duration, user);
                return true;
            case "revive":
                return team != null && team.ReviveFirstDown(item.value);
            case "boost":
                user.AddModifier(item.stat, item.value, item.duration);
                Burst("evolution_eclat", at, item.Colour, 1.5f);
                return true;
            case "stun":
                Burst("evolution_onde", at, item.Colour, 10f);
                foreach (var foe in Ultimates.VisibleFoes(user))
                {
                    foe.Stun(item.duration); // a boss is not frozen...
                    if (foe.isBoss && item.bossSlow > 0f) foe.AddModifier("speed", item.bossSlow, item.duration); // ...but slowed
                }
                return true;
            case "freeze":
                if (Obstacles.ChangeAll("water", "frozen", item.duration) == 0) return false; // no water: kept
                Burst("evolution_onde", at, item.Colour, 10f);
                return true;
            default:
                return false;
        }
    }

    // Where a seed thrown by "user" lands: on its target when it has one near, else two tiles ahead.
    static Vector2 Aimed(Combatant user, Vector2 at)
    {
        var target = user.AimTarget;
        if (target != null && target.IsAlive && Vector2.Distance(at, target.transform.position) <= 6f) return target.transform.position;
        var foe = Combatant.Nearest(user.team == Team.Player ? Team.Enemy : Team.Player, at, 6f);
        if (foe != null) return foe.transform.position;
        Vector2 ahead = user.LiveAim != Vector2.zero ? user.LiveAim.normalized : Vector2.down;
        return at + ahead * 2f;
    }

    static List<Combatant> Foes(Combatant user, Vector2 centre, float radius)
    {
        var list = new List<Combatant>();
        foreach (var c in Combatant.All)
            if (c != null && c.IsAlive && c.team != user.team && Vector2.Distance(c.transform.position, centre) <= radius) list.Add(c);
        return list;
    }

    static void Burst(string sheet, Vector2 at, Color colour, float size)
    {
        var go = EffectPlayer.Spawn(sheet, at, Vector2.right, null, 0f, size);
        if (go != null) go.GetComponent<SpriteRenderer>().color = colour;
    }
}
