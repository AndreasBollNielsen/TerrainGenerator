using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class Raymarcher : MonoBehaviour
{
    public RenderTexture    target;
    public RenderTexture    targetDepth;
    public ComputeShader    computeShader;
    public CustomPassVolume volume;
    public float            maxRayDistance = 0f; // 0 = auto: tileWorldSize * gridRadius * 1.5

    private static readonly int[] LodResolutions = { 2048, 1024, 512 };
    private const int GridSize = 7;

    private int             _kernel;
    private RaymarchPass    _pass;
    private Camera          _cam;

    private Texture2DArray  _lod0Array;
    private Texture2DArray  _lod1Array;
    private Texture2DArray  _lod2Array;
    private ComputeBuffer   _tileGridBuffer;

    private Texture2DArray  _dummyArray;
    private bool            _tilesDirty;

    // Full grid data for all loaded tiles — rebuilt only when OnTilesChanged fires.
    // UpdateVisibleBuffer() copies from this each frame, masking out frustum-culled tiles.
    private readonly TileData[] _fullGridData    = new TileData[GridSize * GridSize];
    private readonly TileData[] _visibleGridData = new TileData[GridSize * GridSize];

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    void Start()
    {
        if (volume == null)
        {
            Debug.LogError("[Raymarcher] No CustomPassVolume assigned.");
            return;
        }

        _pass = volume.customPasses[0] as RaymarchPass;
        if (_pass == null)
        {
            Debug.LogError("[Raymarcher] First custom pass is not a RaymarchPass.");
            return;
        }

        _kernel     = computeShader.FindKernel("CSMain");
        _dummyArray = new Texture2DArray(1, 1, 1, TextureFormat.RFloat, false, true);

        var tm = TileManager.Instance;
        if (tm != null)
        {
            computeShader.SetFloat("_HeightScale",   2000.0f);
            computeShader.SetFloat("_TileWorldSize", tm.tileWorldSize);

            tm.OnTilesChanged += OnTilesChanged;
            RebuildGpuResources();
        }
    }

    void OnDestroy()
    {
        if (TileManager.Instance != null)
            TileManager.Instance.OnTilesChanged -= OnTilesChanged;

        ReleaseBuffers();
        if (_dummyArray != null) Destroy(_dummyArray);
    }

    // -------------------------------------------------------------------------
    // Update — camera uniforms + per-frame frustum-culled buffer upload
    // -------------------------------------------------------------------------

    void Update()
    {
        if (_tilesDirty)
        {
            RebuildGpuResources();
            _tilesDirty = false;
        }

        if (_cam == null) _cam = Camera.main;
        if (_cam == null || _tileGridBuffer == null) return;

        var sun = RenderSettings.sun;
        Vector3 lightDir = sun != null ? sun.transform.forward.normalized : Vector3.down;

        computeShader.SetVector("_CamPos",     _cam.transform.position);
        computeShader.SetVector("_CamForward", _cam.transform.forward);
        computeShader.SetVector("_CamRight",   _cam.transform.right);
        computeShader.SetVector("_CamUp",      _cam.transform.up);
        computeShader.SetFloat ("_CamFov",     _cam.fieldOfView);
        computeShader.SetFloat ("_CamAspect",  (float)_cam.pixelWidth / _cam.pixelHeight);
        computeShader.SetVector("_LightDir",   lightDir);

        // Upload only frustum-visible tiles to the GPU each frame.
        // Texture2DArrays are unaffected — no expensive rebuild on camera rotation.
        UpdateVisibleBuffer();

        computeShader.SetInt    ("Width",       target.width);
        computeShader.SetInt    ("Height",      target.height);
        computeShader.SetTexture(_kernel, "Result",      target);
        computeShader.SetTexture(_kernel, "ResultDepth", targetDepth);

        computeShader.Dispatch(_kernel, target.width / 8, target.height / 8, 1);
    }

    // -------------------------------------------------------------------------
    // Event handler
    // -------------------------------------------------------------------------

    private void OnTilesChanged() => _tilesDirty = true;

    // -------------------------------------------------------------------------
    // GPU resource rebuild — runs only on load/LOD change, not on rotation
    // -------------------------------------------------------------------------

    private void RebuildGpuResources()
    {
        var tm = TileManager.Instance;
        if (tm == null) return;

        ReleaseBuffers();

        // Build LOD-tiered Texture2DArrays from all loaded tiles.
        // These are stable between frames; only rebuilt when tile set or LOD changes.
        var lod0Tiles = tm.GetTilesByLod(0);
        var lod1Tiles = tm.GetTilesByLod(1);
        var lod2Tiles = tm.GetTilesByLod(2);

        _lod0Array = BuildTextureArray(lod0Tiles, LodResolutions[0]);
        _lod1Array = BuildTextureArray(lod1Tiles, LodResolutions[1]);
        _lod2Array = BuildTextureArray(lod2Tiles, LodResolutions[2]);

        var lod0Map = BuildIndexMap(lod0Tiles);
        var lod1Map = BuildIndexMap(lod1Tiles);
        var lod2Map = BuildIndexMap(lod2Tiles);

        // Cache array indices for all loaded tiles into _fullGridData.
        // UpdateVisibleBuffer() copies from this per-frame, masking out culled slots.
        for (int i = 0; i < _fullGridData.Length; i++)
            _fullGridData[i] = new TileData { arrayIndex = -1, lodLevel = 0 };

        for (int gx = 0; gx < GridSize; gx++)
            for (int gy = 0; gy < GridSize; gy++)
            {
                var tile = tm.GetGridTile(gx, gy);
                if (tile == null || tile.heightmap == null) continue;

                var map = tile.lodLevel == 0 ? lod0Map : tile.lodLevel == 1 ? lod1Map : lod2Map;
                if (!map.TryGetValue(tile.gridPosition, out int arrayIdx)) continue;

                _fullGridData[gx * GridSize + gy] = new TileData
                {
                    arrayIndex = arrayIdx,
                    lodLevel   = tile.lodLevel
                };
            }

        float rayDist = maxRayDistance > 0f ? maxRayDistance : tm.EffectiveLoadDistance;
        computeShader.SetFloat("_MaxRayDistance", rayDist);
        Debug.Log($"[Raymarcher] MaxRayDistance={rayDist:F0}  LOD0={lod0Tiles.Count}  LOD1={lod1Tiles.Count}  LOD2={lod2Tiles.Count}");

        var center = tm.CenterTile;
        computeShader.SetInts   ("_CenterTile",      center.x, center.y);
        computeShader.SetTexture(_kernel, "_HeightTexLOD0", _lod0Array != null ? _lod0Array : _dummyArray);
        computeShader.SetTexture(_kernel, "_HeightTexLOD1", _lod1Array != null ? _lod1Array : _dummyArray);
        computeShader.SetTexture(_kernel, "_HeightTexLOD2", _lod2Array != null ? _lod2Array : _dummyArray);

        _tileGridBuffer = new ComputeBuffer(GridSize * GridSize, 8);
        computeShader.SetBuffer(_kernel, "_TileGrid", _tileGridBuffer);
        // Buffer content populated by UpdateVisibleBuffer() each frame
    }

    // -------------------------------------------------------------------------
    // Per-frame frustum-culled buffer upload — cheap (392 bytes)
    // -------------------------------------------------------------------------

    private void UpdateVisibleBuffer()
    {
        var tm = TileManager.Instance;
        if (tm == null) return;

        for (int i = 0; i < _visibleGridData.Length; i++)
            _visibleGridData[i] = new TileData { arrayIndex = -1, lodLevel = 0 };

        foreach (var tile in tm.GetVisibleTiles())
        {
            int idx = tile.gridPosition.x * GridSize + tile.gridPosition.y;
            _visibleGridData[idx] = _fullGridData[idx];
        }

        _tileGridBuffer.SetData(_visibleGridData);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Texture2DArray BuildTextureArray(List<TerrainTile> tiles, int resolution)
    {
        if (tiles.Count == 0) return null;

        var array = new Texture2DArray(resolution, resolution, tiles.Count,
                                       TextureFormat.RFloat, false, true)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp
        };

        for (int i = 0; i < tiles.Count; i++)
        {
            var rt = RenderTexture.GetTemporary(resolution, resolution, 0,
                                                RenderTextureFormat.RFloat,
                                                RenderTextureReadWrite.Linear);
            Graphics.Blit(tiles[i].heightmap, rt);
            Graphics.CopyTexture(rt, 0, 0, array, i, 0);
            RenderTexture.ReleaseTemporary(rt);
        }

        return array;
    }

    private static Dictionary<Vector2Int, int> BuildIndexMap(List<TerrainTile> tiles)
    {
        var map = new Dictionary<Vector2Int, int>(tiles.Count);
        for (int i = 0; i < tiles.Count; i++)
            map[tiles[i].gridPosition] = i;
        return map;
    }

    private void ReleaseBuffers()
    {
        if (_lod0Array      != null) { Destroy(_lod0Array);      _lod0Array      = null; }
        if (_lod1Array      != null) { Destroy(_lod1Array);      _lod1Array      = null; }
        if (_lod2Array      != null) { Destroy(_lod2Array);      _lod2Array      = null; }
        if (_tileGridBuffer != null) { _tileGridBuffer.Release(); _tileGridBuffer = null; }
    }
}
