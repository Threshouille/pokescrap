using System.Collections.Generic;
using UnityEngine;

/// Stones circling a Pokémon (orbitCount on a projectile move, e.g. Pouv. Antique): while they turn, they stop the
/// opponents' shots; then they are launched one by one at the aim, as shots of the move.
public class OrbitStones : MonoBehaviour
{
    const float TurnSpeed = 300f; // degrees per second
    const float StoneRadius = 0.25f; // tiles: how wide a stone stops shots

    static readonly List<OrbitStones> active = new();

    Combatant owner;
    MoveDef move;
    readonly List<Transform> stones = new();
    float angle, launchAt, nextLaunch;

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);

    public static void Start(Combatant owner, MoveDef move)
    {
        var orbit = new GameObject(move.name + " (orbite)").AddComponent<OrbitStones>();
        orbit.owner = owner;
        orbit.move = move;
        orbit.launchAt = orbit.nextLaunch = Time.time + move.orbitTime;
        for (int i = 0; i < move.orbitCount; i++)
        {
            var stone = new GameObject("Pierre");
            stone.transform.SetParent(orbit.transform, false);
            if (!EffectPlayer.Attach(stone, move.effect, Vector2.right))
            {
                stone.transform.localScale = Vector3.one * move.radius * 2f;
                var renderer = stone.AddComponent<SpriteRenderer>();
                renderer.sprite = Shapes.Circle;
                renderer.color = GameData.TypeColor(move.type);
                renderer.sortingOrder = 2;
            }
            orbit.stones.Add(stone.transform);
        }
        orbit.Place();
    }

    /// True when a shot of the given team going from "from" to "to" runs into a stone of the other team.
    public static bool Blocks(Team shooter, Vector2 from, Vector2 to, float shotRadius)
    {
        foreach (var orbit in active)
        {
            if (orbit.owner == null || orbit.owner.team == shooter) continue;
            foreach (var stone in orbit.stones)
                if (DistanceToSegment(stone.position, from, to) <= StoneRadius + shotRadius) return true;
        }
        return false;
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    void Place()
    {
        transform.position = owner.transform.position;
        for (int i = 0; i < stones.Count; i++)
        {
            float a = (angle + i * 360f / Mathf.Max(1, move.orbitCount)) * Mathf.Deg2Rad;
            stones[i].position = transform.position + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * move.orbitRadius;
        }
    }

    void FixedUpdate()
    {
        if (owner == null || !owner.IsAlive || stones.Count == 0)
        {
            Destroy(gameObject);
            return;
        }
        angle += TurnSpeed * Time.fixedDeltaTime;
        Place();
        if (Time.time < launchAt || Time.time < nextLaunch) return;

        // One stone after the other leaves toward the aim (the player's target, led like any shot).
        nextLaunch = Time.time + move.orbitInterval;
        var stone = stones[0];
        stones.RemoveAt(0);
        Vector2 from = stone.position;
        Destroy(stone.gameObject);
        owner.FireStone(move, from);
    }
}
