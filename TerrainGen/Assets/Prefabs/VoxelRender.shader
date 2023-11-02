Shader "Custom/VoxelRender"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		 _MainTex("Texture", 3D) = "white" {}

	}
		SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		struct Input
		{
			float2 uv_MainTex;
		};


	sampler3D _MainTex;
		fixed4 _Color;
		fixed4 _SurfaceColor;


		// Voxel data parameters
		float _VoxelSize = 1.0; // Size of each voxel
		int _VoxelGridSize = 64; // Number of voxels per side in the grid

		// External voxel data buffer
		//SAMPLE_TEXTURE3D(_VoxelData, mip, float4);

		// Distance function for voxel-based SDF representation
		float DistanceFunction(float3 position) {
			// Convert position to voxel indices
			int3 voxelIndex = int3(floor(position / _VoxelSize));

			// Check if voxel is inside the grid bounds
			if (any(voxelIndex < 0) || any(voxelIndex >= _VoxelGridSize)) {
				return 0.0; // Return 0.0 for positions outside the grid
			}

			// Retrieve voxel data from external buffer
			float4 voxelValue = tex3D(_MainTex, float3(voxelIndex));

			// Calculate the signed distance based on voxel data
			if (voxelValue.r > 0.0) {
				return -_VoxelSize; // Voxel is solid, return negative voxel size
			}
			else {
				return _VoxelSize; // Voxel is empty, return positive voxel size
			}
		}

		// Ray marching function with bounding box
		fixed4 RayMarch(float3 rayOrigin, float3 rayDirection) {
			float maxDistance = 100.0; // Maximum distance to march
			float totalDistance = 0.0; // Accumulated distance



			for (int i = 0; i < 100; i++) {
				float3 currentPosition = rayOrigin + rayDirection * totalDistance;

				float distance = DistanceFunction(currentPosition);

				if (distance < 0.001 || totalDistance > maxDistance) {
					// Terminate the ray marching if the distance is below the threshold or the maximum distance is reached
					break;
				}

				totalDistance += distance;
			}

			if (totalDistance > maxDistance) {
				// Color based on distance from maxDistance
				float t = saturate((totalDistance - maxDistance) / _VoxelSize);
				return lerp(_Color, _SurfaceColor, t);
			}
			else {
				return _Color;
			}
		}

		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			// Normalize screen coordinates (0 to 1 range)
		//	float2 normalizedScreenPos = IN.uv_MainTex.xy;

			// Convert normalized screen coordinates to clip space coordinates (-1 to 1 range)
		//	float2 clipSpacePos = (normalizedScreenPos - 0.5) * 2.0;

			// Calculate view-space position
		//	float4 viewSpacePos = mul(UNITY_MATRIX_VP, float4(clipSpacePos, 0.0, 1.0));

			// Calculate world-space position
		//	float4 worldSpacePos = mul(UNITY_MATRIX_I_V, viewSpacePos);

			// Calculate the ray direction by subtracting the camera position from the world-space position
		//	float3 rayDirection = normalize(worldSpacePos.xyz - _WorldSpaceCameraPos);

		//	fixed4 finalColor = RayMarch(_WorldSpaceCameraPos, rayDirection);
			fixed4 testcol = _Color;
			o.Albedo = testcol;
		}
		ENDCG
	}
		FallBack "Diffuse"
}
