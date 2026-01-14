//======= Copyright (c) Stereolabs Corporation, All rights reserved. ===============
// URP-compatible point cloud shader (no geometry shader)
Shader "ZED/ZED PointCloud URP"
{
	Properties
	{
		_ColorTex("Texture", 2D) = "white" {}
		_ScaleSizeMultiplier("Size Multiplier", Range(0.1, 16)) = 2

		[Header(A divided (B plus distance) plus C)]

		_a("A", Range(0.01, 1)) = 0.02
		_b("B", Range(0, 0.1)) = 0.05
		_c("C", Range(0, 3)) = 1
		_d("D", Range(0, 5)) = 1.2


		//https://www.desmos.com/calculator/tfx33zkhga
	}
	SubShader
	{
		Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
		LOD 100

		Pass
		{
			Name "Forward"
			Cull Off
			ZWrite On
			HLSLPROGRAM
			#pragma target 4.5
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma multi_compile _ UNITY_SINGLE_PASS_STEREO STEREO_MULTIVIEW_ON STEREO_INSTANCING_ON
			#pragma multi_compile _ _USE_VFX_FAKE // placeholder to keep keyword list non-empty for some platforms

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct VSOutput
			{
				float4 positionCS : SV_POSITION;
				float4 color : COLOR0;
				nointerpolation float size : PSIZE;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			TEXTURE2D(_XYZTex);
			SAMPLER(sampler_XYZTex);
			TEXTURE2D(_ColorTex);
			SAMPLER(sampler_ColorTex);
			float4 _XYZTex_TexelSize;
			float4x4 _Position;
			float _ScaleSizeMultiplier;
			float _a;
			float _b;
			float _c;
			float _d;

			// Compute UV from instance_id based on texture texel layout
			float2 ComputeUV(uint instance_id)
			{
				// _XYZTex_TexelSize = (1/width, 1/height, width, height)
				float width = _XYZTex_TexelSize.z;
				float height = _XYZTex_TexelSize.w; // some pipelines pack height in .w; if 0, fallback using 1/_XYZTex_TexelSize.y
				width = max(width, 1.0);
				height = (height > 0.0) ? height : max(1.0, 1.0 / max(_XYZTex_TexelSize.y, 1e-6));
				uint x = instance_id % (uint)width;
				uint y = instance_id / (uint)width;
				float2 uv;
				uv.x = (x + 0.5) * (1.0 / width);
				uv.y = (y + 0.5) * (1.0 / height);
				return uv;
			}

			VSOutput vert(uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
			{
				VSOutput o;
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				float2 uv = ComputeUV(instance_id);
				float3 xyz = SAMPLE_TEXTURE2D_LOD(_XYZTex, sampler_XYZTex, uv, 0).rgb;
				float4 worldPos = mul(_Position, float4(xyz, 1.0));
				float4 positionCS = TransformWorldToHClip(worldPos.xyz);
				o.positionCS = positionCS;
				float3 colorBGR = SAMPLE_TEXTURE2D_LOD(_ColorTex, sampler_ColorTex, uv, 0).rgb;
				o.color = float4(colorBGR.bgr, 1.0);


				//Size calculation based on distance from camera
				// Distance-based point size (clamped between min/max), scaled by _Size
				float dist = 1; // fallback; replaced below
				// Prefer camera position when available
				#ifdef UNITY_DECLARE_DEPTH_TEXTURE
					dist = distance(worldPos.xyz, _WorldSpaceCameraPos);
				#else
					dist = distance(worldPos.xyz, GetWorldSpaceViewDir(worldPos.xyz) + worldPos.xyz); // fallback; replaced below
				#endif
				float sizeRange = _a/(_b+  pow(dist,_d)) + _c;
				o.size =  sizeRange *  _ScaleSizeMultiplier; // Scale the size by the object's X scale (assumes 1:1:1 ratio)

				return o;
			}

			half4 frag(VSOutput i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				return i.color;
			}
			ENDHLSL
		}
	}
}


