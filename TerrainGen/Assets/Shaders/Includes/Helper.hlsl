#ifndef HELPER
#define HELPER



static const uint numThreads = 8;
int _Metrics_VoxelWidth;
int _Metrics_VoxelSize;


int indexFromCoord(uint x, uint y, uint z, int height)
{
    uint gx = x * _Metrics_VoxelSize;
    uint gy = y * _Metrics_VoxelSize;
    uint gz = z * _Metrics_VoxelSize;
    
   // float chunkWidth = (_Metrics_VoxelWidth / _Metrics_VoxelSize);
    float chunkWidth = _Metrics_VoxelWidth;
   // float chunkHeight = (height / 1);
    float chunkHeight = height;
  
  // return gx + gy * chunkWidth + gz * (chunkWidth * height);
    
    //return x + chunkWidth * (y + chunkHeight * z);
    return gx + chunkWidth * (gy + height * gz);
}

#endif