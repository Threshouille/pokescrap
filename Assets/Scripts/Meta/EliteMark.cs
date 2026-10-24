using UnityEngine;

/// Marks an elite enemy (Loot.MakeElite, or an elite lot of encounters.json: champion, squad): a golden ring under it,
/// changes obstacles like the allies do; a special item when knocked out — by chance, or for sure for an elite lot
/// (the squad's once all of it is down).
public class EliteMark : MonoBehaviour
{
    public bool sureLoot;
    public EnemyGroup squad; // the squad it belongs to: the loot drops when the last one falls
}
