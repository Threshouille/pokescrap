using UnityEngine;

/// The secret stairs of the island (IslandPlan.secret; GDD, "Escaliers secrets"), added by IslandRun: shown once
/// nothing covers them any more (a cracked rock is broken), they lead to a room of their own (secret_rooms.json
/// "room", its content: RoomContent), back onto them; then they are gone. Their hideout gives them away to a careful
/// eye only (entrances.json, GDD « Entrées des donjons »): a ring of mushrooms under dead leaves in a clearing, blown
/// away when the team comes near; a big cracked rock that bursts when the right attack breaks it.
public class SecretStairs : IslandStairs
{
    protected override Vector2Int Tile => Plan != null ? Plan.secret : new Vector2Int(-1, -1);
    protected override string StairsName => "Escalier secret";
    protected override string Kind => "secret";
    protected override string RoomName => "Salle secrète";
    protected override Color Colour => new(0.3f, 1f, 0.9f);
    protected override SecretRooms.Room Room => SecretRooms.Data.room;
    protected override bool OneUse => true;
    protected override EntranceArt.Icon Icon => EntranceArt.Icon.Secret;
    protected override bool Hidden(Vector2Int cell) => Obstacles.At(cell.x, cell.y)?.key == SecretRooms.Data.rock || covered;

    bool InClearing => Plan != null && Plan.secretSpot == "clearing";
    bool covered; // the clearing's stairs, still under their leaves
    IslandPlan uncoveredIn; // the island where the team blew them away (they stay uncovered there)
    EntranceSprite leaves, rock;

    protected override GameObject MakeEntrance(Vector2 at)
    {
        var go = InClearing
            ? EntranceArt.Stairs(StairsName, at, "secret_clearing")
            : EntranceArt.Stairs(StairsName, at, "entrance_rim_biomes", EntranceArt.RimOf(BiomeKey));
        // The hideout, apart from the stairs (they are hidden while it holds).
        if (leaves != null) Destroy(leaves.gameObject);
        if (rock != null) Destroy(rock.gameObject);
        covered = InClearing && uncoveredIn != Plan;
        if (covered) leaves = EntranceSprite.Make(null, at, EntranceArt.Centre, EntranceArt.GroundOrder).Still("secret_clearing_hidden");
        else rock = EntranceSprite.Make(null, at + new Vector2(0f, -0.5f), EntranceArt.Foot, EntranceArt.StandOrder).Still("secret_rock");
        if (leaves != null) leaves.transform.SetParent(transform, true);
        if (rock != null) rock.transform.SetParent(transform, true);
        return go;
    }

    void Update()
    {
        if (island == null || Plan == null || Tile.x < 0) return;
        bool away = island.Away || !Waiting(island.Zone);
        if (leaves != null) leaves.gameObject.SetActive(!away);
        if (rock != null) rock.gameObject.SetActive(!away);
        if (away) return;
        Vector2 at = island.Cell(Tile);
        var team = FindAnyObjectByType<PlayerController>();
        // The leaves blow away as the team comes near.
        if (covered && leaves != null && team != null && team.Controlled != null
            && Vector2.Distance(team.Controlled.transform.position, at) <= EntranceArt.Data.clearingReveal)
        {
            covered = false;
            uncoveredIn = Plan;
            Destroy(leaves.gameObject);
            leaves = null;
            EffectPlayer.Spawn("entrance_reveal", at, Vector2.right);
        }
        // The rock bursts once broken (its obstacle gone).
        if (rock != null && DungeonGenerator.Current != null)
        {
            var cell = DungeonGenerator.Current.CellOf(at);
            if (Obstacles.At(cell.x, cell.y)?.key != SecretRooms.Data.rock)
            {
                rock.Play("secret_rock_break", false, finished: () => EffectPlayer.Spawn("entrance_reveal", at, Vector2.right));
                var bursting = rock;
                rock = null;
                Destroy(bursting.gameObject, 1.5f);
            }
        }
    }

    void OnDisable()
    {
        if (leaves != null) Destroy(leaves.gameObject);
        if (rock != null) Destroy(rock.gameObject);
    }
}
