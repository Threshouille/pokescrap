using System.Collections.Generic;

/// Short French descriptions of a move for menus: family, power, recharge, effects.
public static class MoveText
{
    public static string Family(MoveDef move) => move.family switch
    {
        "melee" => move.lunge > 0f ? "Ruée" : "Corps à corps",
        "projectile" => move.count > 1 ? $"Projectiles ×{move.count}" : "Projectile",
        "burst" => "Autour de soi",
        "zone" => "Zone",
        "heal" => "Soin",
        "modifier" => move.target == "enemies" ? "Malus" : "Bonus",
        "climate" => move.radius > 0f ? "Zone autour de soi" : "Météo",
        _ => move.family,
    };

    public static string Summary(MoveDef move)
    {
        string power = move.power > 0 ? $"Puiss. {move.power}  ·  " : "";
        return $"{Family(move)}  ·  {power}Recharge {move.cooldown:0.#} s";
    }

    static readonly Dictionary<string, string> StatNames = new() { { "attack", "Attaque" }, { "defense", "Défense" }, { "speed", "Vitesse" } };

    public static string Effects(MoveDef move)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(move.status))
        {
            string name = StatusLibrary.Get(move.status)?.name ?? move.status;
            float chance = move.statusChance > 0f ? move.statusChance : 1f;
            parts.Add(chance < 1f ? $"{name} {chance * 100f:0} %" : name);
        }
        if (!string.IsNullOrEmpty(move.modStat))
            parts.Add($"{StatNames.GetValueOrDefault(move.modStat, move.modStat)} {(move.modValue > 1f ? "+" : "")}{(move.modValue - 1f) * 100f:0} %");
        if (!string.IsNullOrEmpty(move.selfModStat))
            parts.Add($"{StatNames.GetValueOrDefault(move.selfModStat, move.selfModStat)} du lanceur +{(move.selfModValue - 1f) * 100f:0} %");
        if (move.healShare > 0f) parts.Add($"Soigne {move.healShare * 100f:0} %");
        if (move.drain > 0f) parts.Add("Draine");
        if (move.recoil > 0f) parts.Add($"Recul {move.recoil * 100f:0} %");
        return parts.Count > 0 ? string.Join("  ·  ", parts) : "Pas d'effet";
    }
}
