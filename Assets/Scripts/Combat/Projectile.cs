using UnityEngine;

/// A shot of the "projectile" move family: flies straight, hits the first opponent or wall.
public class Projectile : MonoBehaviour
{
    const float SpawnOffset = 0.4f; // tiles in front of the shooter

    Team team;
    float damage;
    float radius;
    Vector2 direction;
    float speed;
    float remaining; // tiles left to travel
    MoveDef move;

    public static void Spawn(Combatant owner, MoveDef move, Vector2 direction)
    {
        var go = new GameObject(move.name);
        go.transform.position = owner.transform.position + (Vector3)(direction * SpawnOffset);

        // Without a sprite sheet the shot falls back to a plain disc in the colour of its type.
        if (!EffectPlayer.Attach(go, move.effect, direction))
        {
            go.transform.localScale = Vector3.one * move.radius * 2f;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Shapes.Circle;
            renderer.color = GameData.TypeColor(move.type);
            renderer.sortingOrder = 2;
        }

        go.transform.localScale *= owner.projectileSizeMul; // "bigger projectiles" upgrades

        var projectile = go.AddComponent<Projectile>();
        projectile.team = owner.team;
        projectile.damage = owner.DamageOf(move);
        projectile.radius = move.radius * owner.projectileSizeMul;
        projectile.direction = direction;
        projectile.speed = move.speed * owner.projectileSpeedMul;
        projectile.remaining = move.range;
        projectile.move = move;
    }

    // The whole step is swept with a circle cast, so a fast shot or a slow frame can never skip over a target.
    void FixedUpdate()
    {
        float step = Mathf.Min(speed * Time.fixedDeltaTime, remaining);
        foreach (var hit in Physics2D.CircleCastAll(transform.position, radius, direction, step))
        {
            if (hit.collider.isTrigger) continue;
            var target = hit.collider.GetComponentInParent<Combatant>();
            if (target != null)
            {
                if (target.team == team) continue;
                if (target.TakeDamage(damage)) target.ApplyStatus(move);
            }
            // A cast that starts inside a collider reports no usable contact point.
            Vector3 at = hit.distance > 0f ? (Vector3)hit.point : transform.position;
            EffectPlayer.Spawn(move.impactEffect, at, direction);
            Destroy(gameObject);
            return;
        }

        transform.position += (Vector3)(direction * step);
        remaining -= step;
        if (remaining <= 0f) Destroy(gameObject);
    }
}
