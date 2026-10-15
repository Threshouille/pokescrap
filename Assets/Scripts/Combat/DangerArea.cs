using System.Collections.Generic;
using UnityEngine;

/// Area where an announced enemy attack is about to land (the red disc or strip of a wind-up). Lives on the
/// telegraph object, so it disappears with it. Allies read it to get out of the way.
public class DangerArea : MonoBehaviour
{
    static readonly List<DangerArea> active = new();

    Team team;
    bool circle;
    Vector2 centre, axis; // axis: unit direction of a strip
    float length, halfWidth; // circle: halfWidth is the radius

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);

    public static void AddCircle(GameObject telegraph, Team team, Vector2 centre, float radius)
    {
        var area = telegraph.AddComponent<DangerArea>();
        area.team = team;
        area.circle = true;
        area.centre = centre;
        area.halfWidth = radius;
    }

    /// Strip starting at "from" and running "length" tiles along "direction".
    public static void AddStrip(GameObject telegraph, Team team, Vector2 from, Vector2 direction, float length, float halfWidth)
    {
        var area = telegraph.AddComponent<DangerArea>();
        area.team = team;
        area.centre = from;
        area.axis = direction.normalized;
        area.length = length;
        area.halfWidth = halfWidth;
    }

    /// Direction that leaves an attack announced against the given team (margin: the victim's own size), or zero.
    /// A disc is left straight away from its centre, a strip sideways.
    public static Vector2 Escape(Vector2 point, Team victim, float margin)
    {
        foreach (var area in active)
        {
            if (area.team == victim) continue;
            Vector2 rel = point - area.centre;
            if (area.circle)
            {
                if (rel.magnitude > area.halfWidth + margin) continue;
                return rel.sqrMagnitude > 0.0001f ? rel.normalized : Vector2.right;
            }
            float along = Vector2.Dot(rel, area.axis);
            var normal = new Vector2(-area.axis.y, area.axis.x);
            float lateral = Vector2.Dot(rel, normal);
            if (along < -margin || along > area.length + margin || Mathf.Abs(lateral) > area.halfWidth + margin) continue;
            return lateral >= 0f ? normal : -normal;
        }
        return Vector2.zero;
    }
}
