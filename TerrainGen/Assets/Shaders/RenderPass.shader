Shader "FullScreen/RenderPass"
{
    Properties
    {
        _MainTex("Texture", 3D) = "white" {}
        _SurfaceColor("Color", Color) = (1,1,1,1)
        _Color("ValidColor", Color) = (1,1,1,1)
    }

    HLSLINCLUDE

    #pragma vertex Vert

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"


    sampler3D _MainTex;
    float4 _MainTex_ST;
    float4 _SurfaceColor;
    float4 _Color;

    // Voxel data parameters
    float _VoxelSize = 1.0; // Size of each voxel
    int _VoxelGridSize = 64; // Number of voxels per side in the grid

    // The PositionInputs struct allow you to retrieve a lot of useful information for your fullScreenShader:
    // struct PositionInputs
    // {
    //     float3 positionWS;  // World space position (could be camera-relative)
    //     float2 positionNDC; // Normalized screen coordinates within the viewport    : [0, 1) (with the half-pixel offset)
    //     uint2  positionSS;  // Screen space pixel coordinates                       : [0, NumPixels)
    //     uint2  tileCoord;   // Screen tile coordinates                              : [0, NumTiles)
    //     float  deviceDepth; // Depth from the depth buffer                          : [0, 1] (typically reversed)
    //     float  linearDepth; // View space Z coordinate                              : [Near, Far]
    // };

    // To sample custom buffers, you have access to these functions:
    // But be careful, on most platforms you can't sample to the bound color buffer. It means that you
    // can't use the SampleCustomColor when the pass color buffer is set to custom (and same for camera the buffer).
    // float4 CustomPassSampleCustomColor(float2 uv);
    // float4 CustomPassLoadCustomColor(uint2 pixelCoords);
    // float LoadCustomDepth(uint2 pixelCoords);
    // float SampleCustomDepth(float2 uv);

    // There are also a lot of utility function you can use inside Common.hlsl and Color.hlsl,
    // you can check them out in the source code of the core SRP package.

    // Distance function for voxel-based SDF representation
    float DistanceFunction(float3 position)
    {
        // Convert position to voxel indices
        float3 voxelIndex = float3(position / _VoxelGridSize);
        float3 normalizedIndex = voxelIndex / (_VoxelGridSize - 1.0);


        // Retrieve voxel data from external buffer
        float4 voxelValue = tex3D(_MainTex, normalizedIndex);

        // Calculate the signed distance based on voxel data
        if (voxelValue.r > 0.0)
        {
            return -_VoxelSize; // Voxel is solid, return negative voxel size
           
        }
        else
        {
            return _VoxelSize; // Voxel is empty, return positive voxel size
        }
    }


    // Ray marching function with bounding box
    float4 RayMarch(float3 rayOrigin, float3 rayDirection) {
        float maxDistance = 100.0; // Maximum distance to march
        float totalDistance = 0.0; // Accumulated distance
        const float epsilon = 0.001;
        float3 rayDir = normalize(rayDirection);

        for (int i = 0; i < 100; i++) {
            float3 currentPosition = rayOrigin + rayDir * totalDistance;

            float distance = DistanceFunction(currentPosition);

           /* if (distance < epsilon)
            {
                float t = distance / maxDistance;
                return float4(t, t, t, 1.0);
            }*/

            //if (distance < epsilon || totalDistance > maxDistance) {
            //	// Terminate the ray marching if the distance is below the threshold or the maximum distance is reached
            //    //return float4(0, 1.0, 1.0, 1.0);
            //	break;
            //}
           
            if (distance < epsilon)
            {
                // Normalize distance to the range [0, 1]
                float normalizedDistance = abs(distance) / maxDistance;
                float4 debug = float4(rayDir, 1.0);
                // Distance-based gradient color
                float4 color = float4(normalizedDistance, normalizedDistance, normalizedDistance, 1.0);
               // color = debug;
                return float4(rayDirection,1);
            }

            totalDistance += distance;

            //break if not ray is passed the object
            if (totalDistance >= maxDistance)
                break;
        }

        return float4(1.0, 0, 0, 1.0);

        
    }

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);
        float depth = LoadCameraDepth(varyings.positionCS.xy);
        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
        float3 viewDirection = GetWorldSpaceNormalizeViewDir(posInput.positionWS);
        float4 color = float4(0.0, 0.0, 0.0, 0.0);

        // Load the camera color buffer at the mip 0 if we're not at the before rendering injection point
        if (_CustomPassInjectionPoint != CUSTOMPASSINJECTIONPOINT_BEFORE_RENDERING)
            color = float4(CustomPassLoadCameraColor(varyings.positionCS.xy, 0), 1);

        // Add your custom pass code here
        // Calculate the ray direction by subtracting the camera position from the world-space position
        float3 rayDirection = viewDirection;

        float4 finalColor = RayMarch(posInput.positionWS, rayDirection);

        // Fade value allow you to increase the strength of the effect while the camera gets closer to the custom pass volume
       // float f = 1 - abs(_FadeValue * 2 - 1);
        return float4( finalColor.rgb, color.a );
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "Custom Pass 0"

            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
                #pragma fragment FullScreenPass
            ENDHLSL
        }
    }
    Fallback Off
}
