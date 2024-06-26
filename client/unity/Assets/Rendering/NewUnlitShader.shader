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
// limitations under the License.

Shader "Unlit/NewUnlitShader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			
			#include "UnityCG.cginc"

			struct VertexInput
			{
				float4 position : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct VertexOutput
			{
				float2 uv : TEXCOORD0;
				float4 position : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			VertexOutput vert (VertexInput vertex)
			{
				VertexOutput output;
				output.position = UnityObjectToClipPos(vertex.position);
				output.uv = TRANSFORM_TEX(vertex.uv, _MainTex);
				return output;
			}
			
			float4 frag (VertexOutput input) : SV_Target
			{
				
				float4 col = tex2D(_MainTex, input.uv);

				return col;
			}
			ENDCG
		}
	}
}
