// Copyright 2020 The Blocks Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.using UnityEngine;
Shader "Custom/PaperCraft" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		// Enable Debug
		// #pragma enable_d3d11_debug_symbols

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
			float4 screenPos;
		};

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;

		float noise(float2 uv) {
			float _uv_x = uv.x * 10000;
			//modf(_uv_x, out _uv_xI);
			//uv.x = _uv_xI / 100;
			float _uv_y = uv.y * 10000;
			//modf(_uv_y, out _uv_yI);
			//uv.y = _uv_yI / 100;

			//uv.x = floor(_uv_x) / 1000000000;
			//uv.y = floor(_uv_y) / 1000000000;

			// Checkered step pattern ??
			uv.x = sin(floor(_uv_x) / 1000000000);
			uv.y = sin(floor(_uv_y) / 1000000000);

			// Wood grain like ??
			//uv.x = sin(floor(_uv_x) / 100000000);
			//uv.y = sin(floor(_uv_y) / 10000000);
			
			//return frac(sin(dot(uv.xy, float2(532.1231, 1378.3453))) * 53211.1223);
			return frac(sin(dot(uv.xy, float2(532.1231, 1378.3453))) * 53211.1223);
			//return frac(dot(uv.xy, float2(532.1231, 1378.3453)) * 53211.1223);
			
		}

		void surf (Input IN, inout SurfaceOutputStandard o) {
			// Albedo comes from a texture tinted by color
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb;
			float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
			float2 texUV = float2(IN.uv_MainTex.xy.x, IN.uv_MainTex.xy.y);

			// papercraft
			if (IN.uv_MainTex.xy.x > .4f && IN.uv_MainTex.xy.x < .6f
				&& IN.uv_MainTex.xy.y > .4f && IN.uv_MainTex.xy.y < .6f) {
				//o.Albedo += noise(IN.uv_MainTex.xy) / 12.;
				//float2 texUV = float2(0.00000001f, 0.00000001f);
				o.Albedo -= noise(texUV) / 15.;
			} else {
				//float2 texUV = float2(0.000000000f, 0.10000000f);
				o.Albedo -= noise(texUV) / 25.;
			}
			
			// Metallic and smoothness come from slider variables
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
