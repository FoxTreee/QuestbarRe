/// <summary>
/// Describes what a discovered destination on a region map represents.
/// Visuals and gameplay behavior remain separately authored so node types can
/// share one reusable scene without being forced into one appearance.
/// </summary>
public enum RegionMapNodeType
{
    StartingLocation,
    Subregion,
    Dungeon,
    Town,
    RegionGateway
}
