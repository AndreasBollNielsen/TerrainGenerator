// Blittable struct sent to the GPU via ComputeBuffer (stride = 8 bytes).
// Layout must match the TileData struct in Raymarch.compute exactly.
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct TileData
{
    public int arrayIndex; // slice index within the LOD Texture2DArray; -1 if not loaded (4 bytes)
    public int lodLevel;   // 0, 1, or 2 — determines which array to sample              (4 bytes)
}
