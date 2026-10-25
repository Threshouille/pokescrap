using UnityEngine;

/// The secret stairs of the island (IslandPlan.secret; GDD, "Escaliers secrets"), added by IslandRun: shown once
/// nothing covers them any more (a cracked rock is broken), they lead to a room of their own (secret_rooms.json
/// "room", empty for now), back onto them; then they are gone.
public class SecretStairs : IslandStairs
{
    protected override Vector2Int Tile => Plan != null ? Plan.secret : new Vector2Int(-1, -1);
    protected override string StairsName => "Escalier secret";
    protected override string RoomName => "Salle secrète";
    protected override Color Colour => new(0.3f, 1f, 0.9f);
    protected override SecretRooms.Room Room => SecretRooms.Data.room;
    protected override bool OneUse => true;
    protected override bool Hidden(Vector2Int cell) => Obstacles.At(cell.x, cell.y)?.key == SecretRooms.Data.rock;
}
