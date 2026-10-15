using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StatusDef
{
    public string key;
    public string name; // shown in menus
    public string tint = "#ffffff"; // sprite colour while the status is on
    public bool canMove = true, canAttack = true;
    public float speedMul = 1f;
    public float physicalAttackMul = 1f;
    public float damageShare; // share of max HP lost every tick (ignores defence)
    public float tick = 1f; // seconds between two damages
    public bool drainToSource; // damage heals whoever gave the status
    public bool wakeOnHit; // removed by any damaging hit
    public int thawType = -1; // removed by a hit of this type
    public float confuseAngle; // degrees: movement and aim are thrown off by up to this much
    public string becomes; // status that follows when this one ends
    public float duration = 3f; // used when this status follows another one
    public float playerDurationFactor = 1f, bossDurationFactor = 1f, bossDamageFactor = 1f;

    [NonSerialized] public Color color = Color.white;
}

/// Status definitions read from Resources/Data/statuses.json, in display priority order.
public static class StatusLibrary
{
    [Serializable] class File { public StatusDef[] statuses; }

    static List<StatusDef> all;
    static Dictionary<string, StatusDef> byKey;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { all = null; byKey = null; }

    public static IReadOnlyList<StatusDef> All
    {
        get
        {
            Load();
            return all;
        }
    }

    /// Null for a status the game does not handle yet.
    public static StatusDef Get(string key)
    {
        Load();
        return key != null && byKey.TryGetValue(key, out var def) ? def : null;
    }

    static void Load()
    {
        if (all != null) return;
        all = new List<StatusDef>();
        byKey = new Dictionary<string, StatusDef>();
        var asset = Resources.Load<TextAsset>("Data/statuses");
        if (asset == null) return;
        foreach (var def in JsonUtility.FromJson<File>(asset.text).statuses)
        {
            if (ColorUtility.TryParseHtmlString(def.tint, out var c)) def.color = c;
            all.Add(def);
            byKey[def.key] = def;
        }
    }
}
