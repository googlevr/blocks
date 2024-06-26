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

﻿Shader "Mogwai/DirectionalShadowTest"
{
	Properties
	{
		_Color( "Color", Color ) = ( 1, 1, 1, 1 )
		_EmissiveColor("EmissiveColor", Color) = ( 0, 0, 0, 0 )
    _EmissiveAmount("Emissive Amount", Float) = 1
    _Roughness("Roughness", Float) = 0.8
    _Metallic("Metallic", Float) = 1.0
    _RefractiveIndex("Fresnel Effect Refractive Index", Float) = 1.3
    _OverrideColor("Override Color", Color) = (0.5, 0.5, 0.5, 1)
    _OverrideAmount("Override Amount", Float) = 0
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
      #pragma target 5.0
			#include "UnityCG.cginc"
            #include "PenumbraShadows.cginc"
			#define INV_PI 0.31830988618

			struct VertexInput
			{
				float4 position : POSITION;
				float3 normal : NORMAL;
			};

			struct VertexOutput
			{
				float4 position : SV_POSITION;
				float3 normal : TEXCOORD0;
				float4 worldPosition : TEXCOORD1;
				float4 shadowPosition : TEXCOORD2;
			}; 
			
			VertexOutput vert (VertexInput vertex)
			{
				VertexOutput output;
				output.position = UnityObjectToClipPos(vertex.position);
				output.normal = UnityObjectToWorldNormal( vertex.normal );
				output.worldPosition = mul(unity_ObjectToWorld, vertex.position);
				output.shadowPosition = mul(_ShadowMatrix ,output.worldPosition);
				return output;
			}

			float4 _Color;
			float3 _OverrideColor;
			float _OverrideAmount;

			float4 frag (VertexOutput fragment) : SV_Target
			{
//			return float4(1, 0, 0, 1);
			  float3 L = normalize(-_LightDirection.xyz);
			  float3 N = normalize(fragment.normal);
			  float nDotL = dot(N, L);
                float3 shadow = calcShadow(fragment.worldPosition, N, fragment.shadowPosition);
                return float4(shadow, 1);
                return float4(shadow * nDotL, 1);
			}
			ENDCG
		}
		UsePass "VertexLit/SHADOWCASTER"
	}
}