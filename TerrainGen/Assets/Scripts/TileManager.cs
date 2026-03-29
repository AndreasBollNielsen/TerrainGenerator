using System.Collections.Generic;
using UnityEngine;

public class TileManager : MonoBehaviour
{
    public Camera sceneCamera; // assign in inspector; falls back to Camera.main
    public float     tileWorldSize = 2048f;
    public float     tileHeight    = 2000f; // AABB height for frustum tests and Gizmos
    public int       gridRadius    = 3;     // 3 → 7×7 grid

    private const int GridSize = 7;
    private TerrainTile[,] _grid = new TerrainTile[GridSize, GridSize];
    private Vector2Int     _centerTile;
    private readonly Plane[] _frustumPlanes = new Plane[6];

    private static readonly Color[] LodColors =
    {
        new Color(0.2f, 1.0f, 0.2f, 0.9f), // LOD 0 — green
        new Color(1.0f, 0.9f, 0.1f, 0.9f), // LOD 1 — yellow
        new Color(1.0f, 0.3f, 0.2f, 0.9f), // LOD 2 — red
    };

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    void Start()
    {
        if (sceneCamera == null) sceneCamera = Camera.main;

        _centerTile = GetCameraTile();
        for (int gx = 0; gx < GridSize; gx++)
            for (int gy = 0; gy < GridSize; gy++)
                _grid[gx, gy] = CreateTile(gx, gy, GridToWorld(gx, gy, _centerTile));

        UpdateFrustumVisibility();
    }

    void Update()
    {
        Vector2Int currentTile = GetCameraTile();
        if (currentTile != _centerTile)
        {
            ShiftGrid(currentTile);
            _centerTile = currentTile;
        }

        UpdateFrustumVisibility();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public List<TerrainTile> GetVisibleTiles()
    {
        var result = new List<TerrainTile>(GridSize * GridSize);
        for (int gx = 0; gx < GridSize; gx++)
            for (int gy = 0; gy < GridSize; gy++)
            {
                var tile = _grid[gx, gy];
                if (tile != null && tile.heightmap != null)
                    result.Add(tile);
            }
        return result;
    }

    // -------------------------------------------------------------------------
    // Grid management
    // -------------------------------------------------------------------------

    private void ShiftGrid(Vector2Int newCenter)
    {
        // Snapshot current tiles keyed by world coord
        var existing = new Dictionary<Vector2Int, TerrainTile>(GridSize * GridSize);
        for (int gx = 0; gx < GridSize; gx++)
            for (int gy = 0; gy < GridSize; gy++)
            {
                var t = _grid[gx, gy];
                if (t != null) existing[t.gridPosition] = t;
            }

        // Rebuild grid, reusing tiles still in range
        var reused = new HashSet<Vector2Int>();
        for (int gx = 0; gx < GridSize; gx++)
        {
            for (int gy = 0; gy < GridSize; gy++)
            {
                Vector2Int worldCoord = GridToWorld(gx, gy, newCenter);
                if (existing.TryGetValue(worldCoord, out TerrainTile tile))
                {
                    int lod = ComputeLod(gx, gy);
                    if (tile.lodLevel != lod) SetLod(tile, lod);
                    _grid[gx, gy] = tile;
                    reused.Add(worldCoord);
                }
                else
                {
                    _grid[gx, gy] = CreateTile(gx, gy, worldCoord);
                }
            }
        }

        // Unload heightmaps for tiles that left the grid
        foreach (var kvp in existing)
            if (!reused.Contains(kvp.Key))
                UnloadHeightmap(kvp.Value);
    }

    // -------------------------------------------------------------------------
    // Frustum-based loading / unloading
    // -------------------------------------------------------------------------

    private void UpdateFrustumVisibility()
    {
        if (sceneCamera == null) return;

        GeometryUtility.CalculateFrustumPlanes(sceneCamera, _frustumPlanes);

        for (int gx = 0; gx < GridSize; gx++)
        {
            for (int gy = 0; gy < GridSize; gy++)
            {
                var tile = _grid[gx, gy];
                if (tile == null) continue;

                bool inFrustum = GeometryUtility.TestPlanesAABB(_frustumPlanes, TileBounds(tile));

                if (inFrustum  && tile.heightmap == null) LoadHeightmap(tile);
                if (!inFrustum && tile.heightmap != null) UnloadHeightmap(tile);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Tile helpers
    // -------------------------------------------------------------------------

    private TerrainTile CreateTile(int gx, int gy, Vector2Int worldCoord)
    {
        return new TerrainTile
        {
            gridPosition  = worldCoord,
            worldPosition = new Vector3(worldCoord.x * tileWorldSize, 0f, worldCoord.y * tileWorldSize),
            lodLevel      = ComputeLod(gx, gy),
            heightmap     = null,
            isDirty       = false
        };
    }

    private void LoadHeightmap(TerrainTile tile)
    {
        var tex = Resources.Load<Texture2D>($"Textures/Heightmap_{tile.gridPosition.x}_{tile.gridPosition.y}");
        if (tex == null) return;
        tile.heightmap      = tex;
        tex.mipMapBias      = MipBiasForLod(tile.lodLevel);
    }

    private void UnloadHeightmap(TerrainTile tile)
    {
        if (tile.heightmap == null) return;
        Resources.UnloadAsset(tile.heightmap);
        tile.heightmap = null;
    }

    private void SetLod(TerrainTile tile, int lod)
    {
        tile.lodLevel = lod;
        if (tile.heightmap != null)
            tile.heightmap.mipMapBias = MipBiasForLod(lod);
    }

    private Bounds TileBounds(TerrainTile tile)
    {
        var center = tile.worldPosition + new Vector3(tileWorldSize * 0.5f, tileHeight * 0.5f, tileWorldSize * 0.5f);
        var size   = new Vector3(tileWorldSize, tileHeight, tileWorldSize);
        return new Bounds(center, size);
    }

    // -------------------------------------------------------------------------
    // Coordinate helpers
    // -------------------------------------------------------------------------

    private Vector2Int GetCameraTile()
    {
        Vector3 pos = sceneCamera.transform.position;
        return new Vector2Int(
            Mathf.FloorToInt(pos.x / tileWorldSize),
            Mathf.FloorToInt(pos.z / tileWorldSize)
        );
    }

    private Vector2Int GridToWorld(int gx, int gy, Vector2Int center)
    {
        return new Vector2Int(
            center.x + gx - gridRadius + 1,
            center.y + gy - gridRadius + 1
        );
    }

    private int ComputeLod(int gx, int gy)
    {
        int dx   = Mathf.Abs(gx - (gridRadius - 1));
        int dy   = Mathf.Abs(gy - (gridRadius - 1));
        int dist = Mathf.Max(dx, dy);

        if (dist <= 1) return 0;
        if (dist == 2) return 1;
        return 2;
    }

    private static float MipBiasForLod(int lod) => lod;

    // -------------------------------------------------------------------------
    // Gizmos — visible in Scene view; only drawn for tiles with a loaded heightmap
    // -------------------------------------------------------------------------

    void OnDrawGizmos()
    {
        if (_grid == null) return;

        for (int gx = 0; gx < GridSize; gx++)
        {
            for (int gy = 0; gy < GridSize; gy++)
            {
                var tile = _grid[gx, gy];
                if (tile == null || tile.heightmap == null) continue;

                Gizmos.color = LodColors[Mathf.Clamp(tile.lodLevel, 0, LodColors.Length - 1)];
                Bounds b = TileBounds(tile);
                Gizmos.DrawWireCube(b.center, b.size);
            }
        }
    }
}
