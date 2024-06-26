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

Shader "Mogwai/Poly/Wireframe/Double-Sided"
{
	Properties 
	{
		_Color ("Line Color", Color) = (1,1,1,1)
		_MainTex ("Main Texture", 2D) = "white" {}
		_Thickness ("Thickness", Float) = 1
	}

	SubShader 
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" }
		
		// First pass that renders the back faces of the model (cull front faces)
		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha 
			ZWrite Off
			Cull Front
			LOD 200
			
			CGPROGRAM
				#pragma target 5.0
				#include "UnityCG.cginc"
				#include "PolyWireframeFunctions.cginc"
				#pragma vertex vert
				#pragma fragment frag
				#pragma geometry geom

				// Vertex Shader
				MOG_v2g vert(appdata_full v)
				{
					return MOG_vert(v);
				}
				
				// Geometry Shader
				[maxvertexcount(3)]
				void geom(triangle MOG_v2g p[3], inout TriangleStream<MOG_g2f> triStream)
				{
					MOG_geom( p, triStream);
				}
				
				// Fragment Shader
				float4 frag(MOG_g2f input) : COLOR
				{	
					return MOG_frag(input);
				}
			
			ENDCG
		}
		// Second pass to render the fronts of polygons.
		// Guarantees render order of back then front to avoid render artifacts
		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha 
			ZWrite Off
			Cull Back
			LOD 200
			
			CGPROGRAM
				#pragma target 5.0
				#include "UnityCG.cginc"
				#include "PolyWireframeFunctions.cginc"
				#pragma vertex vert
				#pragma fragment frag
				#pragma geometry geom

				// Vertex Shader
				MOG_v2g vert(appdata_full v)
				{
					return MOG_vert(v);
				}
				
				// Geometry Shader
				[maxvertexcount(3)]
				void geom(triangle MOG_v2g p[3], inout TriangleStream<MOG_g2f> triStream)
				{
					MOG_geom( p, triStream);
				}
				
				// Fragment Shader
				float4 frag(MOG_g2f input) : COLOR
				{	
					return MOG_frag(input);
				}
			
			ENDCG
		}
	} 
}
