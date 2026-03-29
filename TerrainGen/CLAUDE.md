# TerrainGenerator — CLAUDE.md

## Project Overview
Unity 2022.3.62f3 (HDRP) project focused on ray marching terrain rendering using a GPU compute shader pipeline accelerated by an octree spatial structure.

Current branch: `raymarching-terrain`

---

## Architecture

### Primary System — Octree Ray Marching (`Assets/Scripts/`)
This is the active focus of the project.

| File | Role |
|------|------|
| `Octree.cs` | Builds the octree by sampling a heightmap; classifies nodes as filled/empty/mixed |
| `OctreeTreeNode.cs` | Individual node: bounds, type, children, corner distance sampling |
| `OctreeChunk.cs` | Groups nodes into chunks with mesh + collider GameObjects |
| `OctreeTest.cs` | Debug/test harness for octree generation and visualization |
| `Raymarcher.cs` | Main MonoBehaviour: binds compute shader, heightmap, camera matrices, dispatches per frame |
| `RaymarchPass.cs` | HDRP `CustomPass` that blits the ray marched `RenderTexture` to screen |
| `RaymarchDepthPass .cs` | Depth pass variant (WIP — note the space in the filename) |
| `Raymarch.compute` | Core GPU compute shader: camera frustum setup, ray march loop, height sampling, lighting |
| `RaymarchDepthPass.shader` | Depth-only shader pass |
| `RaymarchSG.shadergraph` | Shader Graph variant of the ray march pass |

**Key compute shader parameters (set in `Raymarcher.cs`):**
- `_HeightTex` — 2D heightmap texture
- `_TerrainSize` — world-space terrain dimensions (currently `2048 × 2048`)
- `_HeightScale` — vertical scale (currently `2000.0`)
- `_CamPos/Forward/Right/Up/Fov/Aspect` — updated every frame from `Camera.main`
- Dispatched at `width/8 × height/8` thread groups

### Tile Streaming (`Assets/Scripts/`)
| File | Role |
|------|------|
| `TerrainTile.cs` | Plain C# data container: `gridPosition`, `worldPosition`, `lodLevel`, `heightmap`, `isDirty` |
| `TileManager.cs` | MonoBehaviour managing a 7×7 sliding tile grid around the camera; streams heightmaps from `Resources/Textures/Heightmap_X_Y`; sets mipmap bias per LOD ring; exposes `GetVisibleTiles()` |

**LOD rings (Chebyshev distance from centre tile):**
- Distance 0–1 (3×3) → LOD 0, mip bias 0
- Distance 2 (5×5 ring) → LOD 1, mip bias 1
- Distance 3 (outermost ring) → LOD 2, mip bias 2

**TileManager inspector fields:** `cameraTransform`, `tileWorldSize` (default 2048), `gridRadius` (default 3)

> Raymarcher.cs integration is deferred — TileManager is data/streaming only for now.

### Shaders (`Assets/Shaders/`)
| File | Role |
|------|------|
| `RayMarching.shader` / `RayMarching_2.shader` | Earlier ray march shader variants (legacy) |
| `RenderPass.shader` | HDRP full-screen render pass (still in use) |
| `FullScreen_RenderPass.mat` | Material using `RenderPass.shader` |

---

## Other Files
| File | Notes |
|------|-------|
| `Assets/FPS_Controller.cs` | First-person camera/movement controller for navigating the scene |
| `Assets/Editor/generator_editorTool.cs` | EditorWindow for terrain generation utilities |
| `Assets/Scripts/NewBehaviourScript.cs` | Placeholder — unused |

---

## Render Pipeline
- **HDRP** (High Definition Render Pipeline)
- Ray marching runs as an HDRP `CustomPassVolume` — see `RaymarchPass.cs`
- `Raymarcher.cs` must be in the scene alongside a `CustomPassVolume` with `RaymarchPass` as its first custom pass

---

## Notes
- The `RaymarchDepthPass .cs` filename has a trailing space — be careful with file operations on this file
- `NewBehaviourScript.cs` in the Octree folder is unused and can be removed when tidying up
- The `RaymarchDepthPass.cs` file previously had a trailing space in its filename — confirm it's resolved before doing file operations on it
