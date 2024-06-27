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
Shader "Custom/PrototypeGrid" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_GridColor("Grid Color", Color) = (0,0,0,0)
		_MainTex ("Color (RGB) Alpha (A)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
	}
	SubShader {
		Tags { "Queue"="Transparent" "RenderType"="Opaque" }
		LOD 200
		
		
			Cull Off
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows alpha

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
		};

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;
		fixed4 _GridColor;

		void surf (Input IN, inout SurfaceOutputStandard o) {
			// Albedo comes from a texture tinted by color
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			fixed4 gc = tex2D (_MainTex, IN.uv_MainTex) * _GridColor;
			o.Albedo = c.rgb;

			if (IN.uv_MainTex.xy.x < 0.005 || IN.uv_MainTex.xy.x > 0.995
				|| IN.uv_MainTex.xy.y < 0.005 || IN.uv_MainTex.xy.y > 0.995) {
				o.Albedo = gc.rgb;
				o.Alpha = 1;
			}
			else {
				o.Albedo;
				o.Alpha = c.a;
			}

			// Metallic and smoothness come from slider variables
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
