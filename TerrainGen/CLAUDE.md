# TerrainGenerator — CLAUDE.md

## Project Overview
Unity 2022.3.62f3 (HDRP) project focused on ray marching terrain rendering using a GPU compute shader pipeline accelerated by an octree spatial structure.

Current branch: `raymarching-terrain`

---

## Architecture

### Primary System — Octree Ray Marching (`Assets/Scripts/Octree/`)
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

### Legacy Ray Marching (`Assets/Scripts/VoxelRender/`)
Older prototype kept for reference. Uses `OnRenderImage` instead of HDRP CustomPass.

| File | Role |
|------|------|
| `RayMarchingCamera.cs` | Image-effect style ray march applied to camera |
| `TextureGenerator.cs` | Generates 64³ 3D Perlin noise texture for volumetric marching |

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
| `Assets/Scripts/Octree/NewBehaviourScript.cs` | Placeholder — unused |

---

## Render Pipeline
- **HDRP** (High Definition Render Pipeline)
- Ray marching runs as an HDRP `CustomPassVolume` — see `RaymarchPass.cs`
- `Raymarcher.cs` must be in the scene alongside a `CustomPassVolume` with `RaymarchPass` as its first custom pass

---

## Notes
- The `RaymarchDepthPass .cs` filename has a trailing space — be careful with file operations on this file
- `NewBehaviourScript.cs` in the Octree folder is unused and can be removed when tidying up
- The `VoxelRender` system is legacy; the Octree pipeline supersedes it
