using UnityEngine;

public class TerrainTile
{
    public Vector2Int gridPosition;  // tile coords in world grid (e.g. 3, 7)
    public Vector3    worldPosition; // world-space origin of this tile
    public int        lodLevel;      // 0 = closest ring, 1 = mid ring, 2 = outer ring
    public Texture2D  heightmap;     // loaded via Resources.Load, null if unloaded
    public bool       isDirty;       // flagged when tile data has been modified
}
