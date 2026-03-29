using System;
using System.Collections.Generic;
using UnityEngine;

public class TileManager : MonoBehaviour
{
    public static TileManager Instance { get; private set; }

    public Camera sceneCamera; // assign in inspector; falls back to Camera.main

    // Fired whenever the loaded set or LOD assignments change.
    // Raymarcher subscribes to rebuild GPU resources.
    public event Action OnTilesChanged;

    public float tileWorldSize    = 2048f;
    public float tileHeight       = 2000f;  // AABB height for frustum tests and Gizmos
    public int   gridRadius       = 3;       // 3 → 7×7 grid; also used for LOD ring thresholds
    public float tileLoadDistance = 0f;      // 0 = auto: covers full grid diagonally

    [Header("Debug")]
    public bool debugShowLoadedTiles   = true;   // green/yellow/red per LOD
    public bool debugShowFrustumCulling = false; // dims tiles outside the frustum

    private const int GridSize = 7;
    private TerrainTile[,] _grid         = new TerrainTile[GridSize, GridSize];
    private Vector2Int     _cameraTile;   // camera's current tile, used for LOD
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

    void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning("[TileManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (sceneCamera == null) sceneCamera = Camera.main;
        _cameraTile = GetCameraTile();

        // Fixed grid: _grid[gx, gy] always holds world tile (gx, gy).
        // Tile (0,0) → world origin, tile (6,6) → far corner. Never shifts.
        for (int gx = 0; gx < GridSize; gx++)
            for (int gy = 0; gy < GridSize; gy++)
                _grid[gx, gy] = CreateTile(gx, gy);
    }

    void Update()
    {
        // Recompute LOD for all tiles when the camera crosses a tile boundary.
        // No grid shifting, no load/unload — just LOD reassignment.
        Vector2Int currentTile = GetCameraTile();
        if (currentTile != _cameraTile)
        {
            _cameraTile = currentTile;
            UpdateLodLevels();
        }
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    // Fixed buffer center for the compute shader.
    // _grid[gx, gy] maps to buffer index gx*7+gy.
    // Shader slot = tileCoord - CenterTile + GRID_HALF = tileCoord - 3 + 3 = tileCoord. ✓
    public Vector2Int CenterTile => new(GridSize / 2, GridSize / 2);

    public TerrainTile GetGridTile(int gx, int gy) => _grid[gx, gy];

    public float EffectiveLoadDistance => tileLoadDistance > 0f
        ? tileLoadDistance
        : tileWorldSize * gridRadius * 1.5f;

    public List<TerrainTile> GetTilesByLod(int lod)
    {
        var result = new List<TerrainTile>(GridSize * GridSize);
        for (int gx = 0; gx < GridSize; gx++)
            for (int gy = 0; gy < GridSize; gy++)
            {
                var tile = _grid[gx, gy];
                if (tile != null && tile.heightmap != null && tile.lodLevel == lod)
                    result.Add(tile);
            }
        return result;
    }

    // Returns only frustum-visible tiles (for GPU culling in GetVisibleTiles callers).
    // Does NOT affect which tiles are kept in memory.
    public List<TerrainTile> GetVisibleTiles()
    {
        if (sceneCamera == null) return new List<TerrainTile>();

        GeometryUtility.CalculateFrustumPlanes(sceneCamera, _frustumPlanes);
        float dist = tileLoadDistance > 0f ? tileLoadDistance : tileWorldSize * gridRadius * 1.5f;
        _frustumPlanes[5] = new Plane(-sceneCamera.transform.forward,
                                       sceneCamera.transform.position + sceneCamera.transform.forward * dist);

        var result = new List<TerrainTile>(GridSize * GridSize);
        for (int gx = 0; gx < GridSize; gx++)
            for (int gy = 0; gy < GridSize; gy++)
            {
                var tile = _grid[gx, gy];
                if (tile != null && tile.heightmap != null &&
                    GeometryUtility.TestPlanesAABB(_frustumPlanes, TileBounds(tile)))
                    result.Add(tile);
            }
        return result;
    }

    // -------------------------------------------------------------------------
    // LOD update — called when camera crosses a tile boundary
    // -------------------------------------------------------------------------

    private void UpdateLodLevels()
    {
        bool changed = false;
        for (int gx = 0; gx < GridSize; gx++)
            for (int gy = 0; gy < GridSize; gy++)
            {
                var tile = _grid[gx, gy];
                if (tile == null) continue;
                int newLod = ComputeLod(tile.gridPosition);
                if (tile.lodLevel != newLod)
                {
                    tile.lodLevel = newLod;
                    changed = true;
                }
            }
        if (changed) OnTilesChanged?.Invoke();
    }

    // -------------------------------------------------------------------------
    // Tile helpers
    // -------------------------------------------------------------------------

    // Fixed grid: grid slot (gx, gy) IS the world tile coordinate.
    private TerrainTile CreateTile(int gx, int gy)
    {
        var coord = new Vector2Int(gx, gy);
        var tile = new TerrainTile
        {
            gridPosition  = coord,
            worldPosition = new Vector3(gx * tileWorldSize, 0f, gy * tileWorldSize),
            lodLevel      = ComputeLod(coord),
            heightmap     = null,
            isDirty       = false
        };
        LoadHeightmap(tile);
        return tile;
    }

    private void LoadHeightmap(TerrainTile tile)
    {
        var tex = Resources.Load<Texture2D>($"Textures/heightmap_{tile.gridPosition.x}_{tile.gridPosition.y}");
        if (tex == null) return;
        tile.heightmap = tex;
        OnTilesChanged?.Invoke();
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

    // LOD by Chebyshev distance from the camera's current tile.
    private int ComputeLod(Vector2Int tileCoord)
    {
        int dx   = Mathf.Abs(tileCoord.x - _cameraTile.x);
        int dy   = Mathf.Abs(tileCoord.y - _cameraTile.y);
        int dist = Mathf.Max(dx, dy);

        if (dist <= 1) return 0;
        if (dist == 2) return 1;
        return 2;
    }

    // -------------------------------------------------------------------------
    // Gizmos — visible in Scene view for all loaded tiles
    // -------------------------------------------------------------------------

    void OnDrawGizmos()
    {
        if (_grid == null) return;
        if (!debugShowLoadedTiles && !debugShowFrustumCulling) return;

        // Pre-compute frustum visibility if needed
        bool[] inFrustum = null;
        if (debugShowFrustumCulling && sceneCamera != null)
        {
            GeometryUtility.CalculateFrustumPlanes(sceneCamera, _frustumPlanes);
            float dist = tileLoadDistance > 0f ? tileLoadDistance : tileWorldSize * gridRadius * 1.5f;
            _frustumPlanes[5] = new Plane(-sceneCamera.transform.forward,
                                           sceneCamera.transform.position + sceneCamera.transform.forward * dist);
            inFrustum = new bool[GridSize * GridSize];
            for (int gx = 0; gx < GridSize; gx++)
                for (int gy = 0; gy < GridSize; gy++)
                {
                    var t = _grid[gx, gy];
                    if (t != null && t.heightmap != null)
                        inFrustum[gx * GridSize + gy] = GeometryUtility.TestPlanesAABB(_frustumPlanes, TileBounds(t));
                }
        }

        for (int gx = 0; gx < GridSize; gx++)
            for (int gy = 0; gy < GridSize; gy++)
            {
                var tile = _grid[gx, gy];
                if (tile == null || tile.heightmap == null) continue;

                Color lodColor = LodColors[Mathf.Clamp(tile.lodLevel, 0, LodColors.Length - 1)];

                // Dim tiles outside the frustum when frustum culling debug is on
                if (debugShowFrustumCulling && inFrustum != null && !inFrustum[gx * GridSize + gy])
                    Gizmos.color = new Color(lodColor.r * 0.2f, lodColor.g * 0.2f, lodColor.b * 0.2f, 0.4f);
                else if (debugShowLoadedTiles)
                    Gizmos.color = lodColor;
                else
                    continue;

                Gizmos.DrawWireCube(TileBounds(tile).center, TileBounds(tile).size);
            }
    }
}
