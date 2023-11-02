Shader "Unlit/RayMarching"
{
	Properties
	{
		_MainTex("Texture", 3D) = "white" {}
		_SurfaceColor("Color", Color) = (1,1,1,1)
		_Color("ValidColor", Color) = (1,1,1,1)
	}
		SubShader
		{
			Tags { "RenderType" = "Opaque" }
			LOD 100

			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				// make fog work


				#include "UnityCG.cginc"

				struct appdata
				{
					float4 vertex : POSITION;
					float2 uv : TEXCOORD0;
				};

				struct v2f
				{
					float2 uv : TEXCOORD0;
					float4 vertex : SV_POSITION;
				};

				sampler3D _MainTex;
				float4 _MainTex_ST;
				float4 _SurfaceColor;
				float4 _Color;

				// Voxel data parameters
				float _VoxelSize = 1.0; // Size of each voxel
				int _VoxelGridSize = 64; // Number of voxels per side in the grid

				

				// Distance function for voxel-based SDF representation
				float DistanceFunction(float3 position)
				{
					// Convert position to voxel indices
					int3 voxelIndex = int3(floor(position / _VoxelSize));

					

					// Retrieve voxel data from external buffer
					float4 voxelValue = tex3D(_MainTex, float3(voxelIndex));

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
				fixed4 RayMarch(float3 rayOrigin, float3 rayDirection) {
					float maxDistance = 100.0; // Maximum distance to march
					float totalDistance = 0.0; // Accumulated distance
					const float epsilon = 0.001;


					for (int i = 0; i < 100; i++) {
						float3 currentPosition = rayOrigin + rayDirection * totalDistance;

						float distance = DistanceFunction(currentPosition);

						if (distance < epsilon)
						{
							float t = distance * maxDistance;
							return fixed4(t, t, t, 1.0);
						}

						//if (distance < epsilon || totalDistance > maxDistance) {
						//	// Terminate the ray marching if the distance is below the threshold or the maximum distance is reached
						//	break;
						//}

						if (distance < epsilon)
						{
							// Distance-based gradient color
							float t = distance / maxDistance;
							return fixed4(t, t, t, 1.0);
						}

						totalDistance += distance;

						//break if not ray is passed the object
						if (totalDistance >= maxDistance)
							break;
					}

					return fixed4(0.0, 0.0, 0.0, 1.0);

					//if (totalDistance > maxDistance) {
					//	// Color based on distance from maxDistance
					//	float t = saturate((totalDistance - maxDistance) / _VoxelSize);
					//	return lerp(_Color, _SurfaceColor, t);
					//}
					//else {
					//	return _Color;
					//}
				}

				v2f vert(appdata v)
				{
					v2f o;
					o.vertex = UnityObjectToClipPos(v.vertex);
					o.uv = TRANSFORM_TEX(v.uv, _MainTex);

					return o;
				}

				fixed4 frag(v2f i) : SV_Target
				{
					// sample the texture
				//	fixed4 col = tex2D(_MainTex, i.uv);

					// Normalize screen coordinates (0 to 1 range)
					float2 normalizedScreenPos = i.uv.xy;

					// Convert normalized screen coordinates to clip space coordinates (-1 to 1 range)
					float2 clipSpacePos = (normalizedScreenPos - 0.5) * 2.0;

					// Calculate view-space position
					float4 viewSpacePos = mul(UNITY_MATRIX_VP, float4(clipSpacePos, 0.0, 1.0));

					// Calculate world-space position
					float4 worldSpacePos = mul(UNITY_MATRIX_I_V, viewSpacePos);

					// Calculate the ray direction by subtracting the camera position from the world-space position
					float3 rayDirection = normalize(worldSpacePos.xyz - _WorldSpaceCameraPos);

					fixed4 finalColor = RayMarch(_WorldSpaceCameraPos, rayDirection);
					fixed4 col = finalColor;
					return col;
				}
				ENDCG
			}
		}
}
