using UnityEngine;

/// The stairs at the back of the sealed cave (IslandPlan.sealedCave; GDD, "Grottes scellées"), added by IslandRun:
/// shown once its door is open (CaveSeal), they lead to a room of their own (cave_seals.json "room", empty for now),
/// back onto them; they stay.
public class CaveStairs : IslandStairs
{
    protected override Vector2Int Tile => Plan != null ? Plan.sealedCave : new Vector2Int(-1, -1);
    protected override string StairsName => "Escalier de la grotte";
    protected override string RoomName => "Grotte";
    protected override Color Colour => new(1f, 0.55f, 0.2f);
    protected override SecretRooms.Room Room => CaveSeals.Data.room;
    protected override bool Hidden(Vector2Int cell) => Plan == null || !Plan.caveOpen;
}
